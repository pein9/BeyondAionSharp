using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using Aion.Commons.Logging;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Data;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Skill;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;
using Aion.GameServer.Services;
using Aion.GameServer.TestKit;
using Aion.GameServer.Tests.Ai;
using Aion.GameServer.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class LoggingImprovementsPortTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task PeriodicTimeSavePrecedesBroadcastEvenWhenTheStoreReturnsFalse(bool stores)
    {
        var instance = typeof(GameTimeService).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic)!;
        var previous = instance.GetValue(null);
        using var capture = new CapturingLoggerProvider();
        using var factory = Factory(capture);
        await using var clock = new VirtualThreadPool(strict: true);
        var order = new List<string>();
        var repository = new TimeRepository(order, stores);
        try
        {
            var service = new GameTimeService(factory.CreateLogger<GameTimeService>(), clock, repository,
                TimeSpan.FromHours(1), TimeSpan.FromHours(1), TimeSpan.FromMinutes(3), TimeSpan.FromMinutes(3));
            service.SetWorldBroadcaster((_, _) => { order.Add("broadcast"); return Task.FromResult(0); });
            await service.InitAsync(); service.StartClock(); clock.Advance(TimeSpan.FromMinutes(3));
            Assert.Equal(new[] {"store", "broadcast"}, order);
            Assert.Single(capture.Entries, e => e.Message == "Saving current game time and sending it to all players");
            Assert.Equal(stores ? 0 : 1, capture.Entries.Count(e => e.Level == LogLevel.Warning));
            Assert.DoesNotContain(capture.Entries, e => e.Message is "Initialized GameTime" or "Game time saved...");
        }
        finally { instance.SetValue(null, previous); }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CrossRaceTradeUsesTheConfiguredAuditChannel(bool enabled)
    {
        using var h = BossAiHarness.For().Build();
        var player = h.SpawnPlayer(100, 100, 200, race: Race.ELYOS);
        var target = h.SpawnPlayer(101, 100, 200, race: Race.ASMODIANS);
        var client = new LocalConnection(); client.SetAccount(player.GetAccount()); client.SetActivePlayer(player);
        using var capture = new CapturingLoggerProvider(); using var factory = Factory(capture); using var scope = AionLog.OverrideFactory(factory);
        var oldAudit = LoggingConfig.LOG_AUDIT; var oldPunishment = PunishmentConfig.PUNISHMENT_ENABLE;
        try
        {
            LoggingConfig.LOG_AUDIT = enabled; PunishmentConfig.PUNISHMENT_ENABLE = false;
            var packet = new CM_EXCHANGE_REQUEST(1, new HashSet<AionConnection.State> {AionConnection.State.IN_GAME}) {targetObjectId=target.GetObjectId()};
            packet.SetConnection(client); packet.Run();
            Assert.Equal(enabled ? 1 : 0, capture.Entries.Count(e => e.Category == "AUDIT_LOG" && e.Message.Contains("tried to trade with player of another race:")));
            Assert.DoesNotContain(capture.Entries, e => e.Category == nameof(CM_EXCHANGE_REQUEST) && e.Message.Contains("[AUDIT]"));
            Assert.DoesNotContain(capture.Entries, e => e.Level == LogLevel.Error);
        }
        finally { LoggingConfig.LOG_AUDIT = oldAudit; PunishmentConfig.PUNISHMENT_ENABLE = oldPunishment; }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void BrokerOwnershipFailureUsesAuditAndRetainsTheForeignListing(bool enabled)
    {
        using var h = BossAiHarness.For().Build(); var player = h.SpawnPlayer(100, 100, 200);
        var client = new LocalConnection(); client.SetAccount(player.GetAccount()); client.SetActivePlayer(player); player.SetClientConnection(client);
        var broker = (BrokerService)RuntimeHelpers.GetUninitializedObject(typeof(BrokerService));
        var item = (BrokerItem)RuntimeHelpers.GetUninitializedObject(typeof(BrokerItem));
        typeof(BrokerItem).GetField("itemId", PrivateInstance)!.SetValue(item, 100000001);
        typeof(BrokerItem).GetField("sellerId", PrivateInstance)!.SetValue(item, player.GetObjectId() + 1000);
        var items = new ConcurrentDictionary<int, BrokerItem>(); items[77] = item;
        typeof(BrokerService).GetField("elyosBrokerItems", PrivateInstance)!.SetValue(broker, items);
        using var capture = new CapturingLoggerProvider(); using var factory = Factory(capture); using var scope = AionLog.OverrideFactory(factory);
        var oldAudit = LoggingConfig.LOG_AUDIT; var oldPunishment = PunishmentConfig.PUNISHMENT_ENABLE;
        var shutdownSlot = typeof(ShutdownHook).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic)!;
        var priorShutdown = shutdownSlot.GetValue(null);
        try
        {
            // Other shutdown tests deliberately leave a completed countdown in the singleton bridge.
            shutdownSlot.SetValue(null, null);
            LoggingConfig.LOG_AUDIT = enabled; PunishmentConfig.PUNISHMENT_ENABLE = false;
            Assert.True(Aion.GameServer.Restrictions.PlayerRestrictions.CanTrade(player));
            broker.CancelRegisteredItem(player, 77);
            Assert.Same(item, items[77]);
            var entries = capture.Entries.Where(e => e.Message.Contains("tried to get item")).ToArray();
            if (enabled)
            {
                var entry = Assert.Single(entries); Assert.Equal("AUDIT_LOG", entry.Category);
                Assert.Contains("100000001", entry.Message); Assert.Contains("Seller ID: " + (player.GetObjectId() + 1000), entry.Message);
            }
            else Assert.Empty(entries);
        }
        finally { shutdownSlot.SetValue(null, priorShutdown); LoggingConfig.LOG_AUDIT = oldAudit; PunishmentConfig.PUNISHMENT_ENABLE = oldPunishment; player.SetClientConnection(null!); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NativeSchedulerReportsActualShutdownCompletionOrOutstandingWork(bool blocked)
    {
        using var capture = new CapturingLoggerProvider(); using var factory = Factory(capture); using var scope = AionLog.OverrideFactory(factory);
        await using var pool = new ThreadPoolManager(NullLogger<ThreadPoolManager>.Instance);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var work = pool.Schedule(async _ => { entered.SetResult(); if (blocked) await release.Task; }, TimeSpan.Zero);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            if (!blocked) await work.Completion.WaitAsync(TimeSpan.FromSeconds(5));
            await pool.ShutdownAsync(TimeSpan.FromMilliseconds(20));
            var entry = Assert.Single(capture.Entries, e => e.Category == nameof(ThreadPoolManager));
            Assert.Contains(blocked ? "shutdown timed out" : "shutdown completed", entry.Message);
            if (blocked) Assert.EndsWith("with 1 tasks left", entry.Message);
            Assert.Contains("{ElapsedMs}", entry.Template);
        }
        finally { release.TrySetResult(); await work.Completion.WaitAsync(TimeSpan.FromSeconds(5)); }
    }

    [Fact]
    public void DuplicateNpcSkillDiagnosticHasAStableTemplateAndTheActualNpcId()
    {
        using var h = BossAiHarness.For().Build();
        using var capture = new CapturingLoggerProvider(); using var factory = Factory(capture); using var scope = AionLog.OverrideFactory(factory);
        var data = new NpcSkillData {npcSkills = new()
        {
            new() {NpcIdsXml="201010", npcSkills=new()},
            new() {NpcIdsXml="201010", npcSkills=new()},
        }};
        data.AfterUnmarshal(null!);
        var entry = Assert.Single(capture.Entries, e => e.Category == nameof(NpcSkillData));
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal("Npc 201010 has multiple skill lists in npc_skills.xml", entry.Message);
        Assert.Contains("{NpcId}", entry.Template);
    }

    private static ILoggerFactory Factory(CapturingLoggerProvider capture) => LoggerFactory.Create(b => b.SetMinimumLevel(LogLevel.Trace).AddProvider(capture));
    private sealed class LocalConnection() : AionConnection("127.0.0.1");
    private sealed class TimeRepository(List<string> order, bool result) : IServerVariablesRepository
    {
        public Task<int?> LoadIntAsync(string key, CancellationToken token = default) => Task.FromResult<int?>(0);
        public Task<long?> LoadLongAsync(string key, CancellationToken token = default) => Task.FromResult<long?>(null);
        public Task<bool> StoreAsync(string key, object value, CancellationToken token = default) { order.Add("store"); return Task.FromResult(result); }
    }
}
