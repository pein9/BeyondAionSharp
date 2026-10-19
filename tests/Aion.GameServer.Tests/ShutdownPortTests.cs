using System.Reflection;
using Aion.Commons.Nio;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Controllers;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.Account;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Players;
using Aion.GameServer.Tests.Ai;
using Aion.GameServer.Utils;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class ShutdownPortTests
{
    [Fact]
    public void LogoutWaitsTenSecondsAfterTheLaterOfMovementAndCombatAndGatesReentryUntilSaved()
    {
        using var harness = BossAiHarness.For().WithAi(typeof(AggressiveNpcAI)).Build();
        var player = harness.SpawnPlayer();
        var connection = Attach(player);
        var saved = new List<int>();
        using var capture = ExecutionCapture(saved);
        harness.Clock.Advance(TimeSpan.FromSeconds(2));
        player.GetMoveController().UpdateLastMove();
        harness.Clock.Advance(TimeSpan.FromSeconds(3));
        SetCombatTime(player, SystemClock.CurrentMillis());
        PlayerLeaveWorldService.RegisterLeaveWorld(player);
        Assert.True(PlayerLeaveWorldService.IsLeavingWorld(player));
        Assert.True(PlayerLeaveWorldService.IsLeavingWorld(player.ObjectId));
        harness.Clock.Advance(TimeSpan.FromMilliseconds(9_999));
        Assert.Empty(saved);
        harness.Clock.Advance(TimeSpan.FromMilliseconds(1));
        Assert.Equal(new[] { player.ObjectId }, saved);
        Assert.False(PlayerLeaveWorldService.IsLeavingWorld(player));
        Assert.False(PlayerLeaveWorldService.IsLeavingWorld(player.ObjectId));
    }

    [Fact]
    public void RepeatedRegistrationDoesNotMoveTheOriginalDeadline()
    {
        using var harness = BossAiHarness.For().WithAi(typeof(AggressiveNpcAI)).Build();
        var player = harness.SpawnPlayer();
        Attach(player);
        var saved = new List<int>();
        using var capture = ExecutionCapture(saved);
        PlayerLeaveWorldService.RegisterLeaveWorld(player);
        harness.Clock.Advance(TimeSpan.FromSeconds(4));
        player.GetMoveController().UpdateLastMove();
        PlayerLeaveWorldService.RegisterLeaveWorld(player);
        harness.Clock.Advance(TimeSpan.FromSeconds(6));
        Assert.Single(saved);
        harness.Clock.Advance(TimeSpan.FromSeconds(10));
        Assert.Single(saved);
    }

    [Fact]
    public void AnIdlePlayerLeavesSynchronouslyAndRemainsGatedDuringTheSave()
    {
        using var harness = BossAiHarness.For().WithAi(typeof(AggressiveNpcAI)).Build();
        var player = harness.SpawnPlayer();
        Attach(player);
        harness.Clock.Advance(TimeSpan.FromSeconds(10));
        int saves = 0;
        int callingThread = Environment.CurrentManagedThreadId;
        using var capture = PlayerLeaveWorldService.CaptureForCurrentContext((saving, registration) =>
        {
            Assert.False(registration);
            Assert.Equal(callingThread, Environment.CurrentManagedThreadId);
            Assert.True(PlayerLeaveWorldService.IsLeavingWorld(saving));
            Assert.True(PlayerLeaveWorldService.IsLeavingWorld(saving.ObjectId));
            saves++;
        }, captureRegistration: false);
        PlayerLeaveWorldService.RegisterLeaveWorld(player);
        Assert.Equal(1, saves);
        Assert.False(PlayerLeaveWorldService.IsLeavingWorld(player));
    }

    [Fact]
    public void ShutdownDrainRunsPendingSavesOnceAndCancelsTheirTimers()
    {
        using var harness = BossAiHarness.For().WithAi(typeof(AggressiveNpcAI)).Build();
        var player = harness.SpawnPlayer();
        Attach(player);
        var saved = new List<int>();
        using var capture = ExecutionCapture(saved);
        PlayerLeaveWorldService.RegisterLeaveWorld(player);
        PlayerLeaveWorldService.ProcessPendingLeaveWorldTasks();
        PlayerLeaveWorldService.ProcessPendingLeaveWorldTasks();
        harness.Clock.Advance(TimeSpan.FromSeconds(20));
        Assert.Equal(new[] { player.ObjectId }, saved);
        Assert.False(PlayerLeaveWorldService.IsLeavingWorld(player));
    }

    [Fact]
    public void FailedOrAlreadyOfflineSavesCannotLeaveTheReentryGateStuck()
    {
        using var harness = BossAiHarness.For().WithAi(typeof(AggressiveNpcAI)).Build();
        var player = harness.SpawnPlayer();
        Attach(player);
        int attempts = 0;
        using var capture = PlayerLeaveWorldService.CaptureForCurrentContext((_, _) =>
        {
            attempts++;
            throw new InvalidOperationException("Expected save failure");
        }, captureRegistration: false);
        PlayerLeaveWorldService.RegisterLeaveWorld(player);
        PlayerLeaveWorldService.ProcessPendingLeaveWorldTasks();
        Assert.Equal(1, attempts);
        Assert.False(PlayerLeaveWorldService.IsLeavingWorld(player));
        player.SetClientConnection(null!);
        PlayerLeaveWorldService.RegisterLeaveWorld(player);
        PlayerLeaveWorldService.ProcessPendingLeaveWorldTasks();
        harness.Clock.Advance(TimeSpan.FromSeconds(20));
        Assert.Equal(1, attempts);
        Assert.False(PlayerLeaveWorldService.IsLeavingWorld(player));
    }

    [Fact]
    public void PendingSaveBlocksBothCharacterReentryAndAnotherCharacterOnTheAccountWithoutADatabaseQuery()
    {
        using var harness = BossAiHarness.For().WithAi(typeof(AggressiveNpcAI)).Build();
        var player = harness.SpawnPlayer();
        Attach(player);
        player.GetCommonData().SetOnline(true);
        var account = player.GetAccount();
        account.AddPlayerAccountData(player.GetAccountData());
        int otherId = player.ObjectId + 1;
        account.AddPlayerAccountData(new PlayerAccountData(new PlayerCommonData(otherId), new PlayerAppearance()));
        var saved = new List<int>();
        using var capture = ExecutionCapture(saved);
        PlayerLeaveWorldService.RegisterLeaveWorld(player);
        try
        {
            var reconnect = new Connection();
            reconnect.SetAccount(account);
            foreach (int id in new[] { player.ObjectId, otherId })
            {
                PlayerEnterWorldService.EnterWorld(reconnect, id);
                var response = Assert.IsType<SM_ENTER_WORLD_CHECK>(Assert.Single(reconnect.Sent));
                var message = (byte)typeof(SM_ENTER_WORLD_CHECK).GetField("msg", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(response)!;
                Assert.Equal((byte)SM_ENTER_WORLD_CHECK.Msg.REENTRY_TIME, message);
                reconnect.Sent.Clear();
            }
        }
        finally { PlayerLeaveWorldService.ProcessPendingLeaveWorldTasks(); }
    }

    [Fact]
    public void QuitRegistersBeforeClosingAndDisconnectDoesNotResetAnAlreadyLeavingPlayersPosition()
    {
        using var harness = BossAiHarness.For().WithAi(typeof(AggressiveNpcAI)).Build();
        var player = harness.SpawnPlayer();
        var connection = Attach(player);
        var saved = new List<int>();
        using var capture = ExecutionCapture(saved);
        var quit = new CM_QUIT(3, new HashSet<AionConnection.State> { AionConnection.State.IN_GAME });
        quit.SetConnection(connection);
        quit.SetBuffer(ByteBuffer.Wrap([0]));
        Assert.True(quit.Read());
        quit.Run();
        Assert.True(connection.IsClosed());
        Assert.Equal(0, connection.PositionResets);
        Assert.IsType<SM_QUIT_RESPONSE>(Assert.Single(connection.Sent));
        Assert.True(PlayerLeaveWorldService.IsLeavingWorld(player));
        Assert.Empty(saved);
        PlayerLeaveWorldService.ProcessPendingLeaveWorldTasks();
        Assert.Single(saved);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StaffOnlyCountdownShortcutHonorsConfigAndNeverSkipsARegularPlayer(bool enabled)
    {
        using var harness = BossAiHarness.For().WithAi(typeof(AggressiveNpcAI)).Build();
        var staff = harness.SpawnPlayer();
        staff.GetAccount().SetAccessLevel(1);
        var regular = harness.SpawnPlayer();
        // The harness gives players separate account objects even though their ids are the same.
        regular.GetAccount().SetAccessLevel(0);
        bool previous = ShutdownConfig.INSTANT_SHUTDOWN_WITH_ONLY_STAFF_ONLINE;
        try
        {
            ShutdownConfig.INSTANT_SHUTDOWN_WITH_ONLY_STAFF_ONLINE = enabled;
            Assert.True(ShutdownHook.CanSkipCountdown(Array.Empty<Player>()));
            Assert.Equal(enabled, ShutdownHook.CanSkipCountdown(new[] { staff }));
            Assert.False(ShutdownHook.CanSkipCountdown(new[] { staff, regular }));
        }
        finally { ShutdownConfig.INSTANT_SHUTDOWN_WITH_ONLY_STAFF_ONLINE = previous; }
    }

    [Fact]
    public void FastCountdownStillRunsThroughTheDeterministicScheduler()
    {
        using var harness = BossAiHarness.For().WithAi(typeof(AggressiveNpcAI)).Build();
        var lifetime = new Lifetime();
        var hook = new ShutdownHook(lifetime, NullLogger<ShutdownHook>.Instance, TimeSpan.FromSeconds(1), () => true);
        hook.InitShutdown(0, 120);
        Assert.Equal(0, lifetime.Stops);
        harness.Clock.Advance(TimeSpan.Zero);
        Assert.Equal(1, lifetime.Stops);
        Assert.Equal(0, hook.RemainingSeconds);
    }

    [Fact]
    public void StaffReconnectionIncludesCharacterSelectionClientsButOnlyInGameStaffReceiveTheQuitPacket()
    {
        using var harness = BossAiHarness.For().WithAi(typeof(AggressiveNpcAI)).Build();
        var staffPlayer = harness.SpawnPlayer();
        staffPlayer.GetAccount().SetAccessLevel(1);
        var inGame = Attach(staffPlayer);
        inGame.SetState(AionConnection.State.IN_GAME);
        var selecting = new Connection();
        var selectingAccount = new Aion.GameServer.Model.Account.Account(44);
        selectingAccount.SetAccessLevel(1);
        selecting.SetAccount(selectingAccount);
        var regular = Attach(harness.SpawnPlayer());
        inGame.Sent.Clear(); // Ignore player-info broadcasts from setup before observing shutdown packets.
        var reconnects = new List<AionConnection>();
        Assert.True(GameServer.ForceStaffClientsToServerSelectionScreen(new[] { inGame, selecting, regular }, (_, client) => reconnects.Add(client)));
        Assert.Equal(new AionConnection[] { inGame, selecting }, reconnects);
        var quit = Assert.IsType<SM_QUIT_RESPONSE>(Assert.Single(inGame.Sent));
        Assert.True((bool)typeof(SM_QUIT_RESPONSE).GetField("edit_mode", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(quit)!);
        Assert.Empty(selecting.Sent);
        Assert.Empty(regular.Sent);
        Assert.False(GameServer.ForceStaffClientsToServerSelectionScreen(new[] { regular }, (_, _) => throw new Exception("Not staff")));
    }

    private static IDisposable ExecutionCapture(List<int> saved) =>
        PlayerLeaveWorldService.CaptureForCurrentContext((player, registration) =>
        {
            Assert.False(registration);
            saved.Add(player.ObjectId);
            player.GetClientConnection()?.SetActivePlayer(null!);
            player.SetClientConnection(null!);
        }, captureRegistration: false);

    private static void SetCombatTime(Player player, long time) => typeof(PlayerController)
        .GetField("lastAttackMillis", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(player.GetController(), time);

    private static Connection Attach(Player player)
    {
        var connection = new Connection();
        connection.SetAccount(player.GetAccount());
        player.SetClientConnection(connection);
        Assert.True(connection.SetActivePlayer(player));
        return connection;
    }

    private sealed class Connection() : AionConnection("127.0.0.1")
    {
        private readonly Queue<AionServerPacket> queue = new();
        public List<AionServerPacket> Sent { get; } = new();
        public int PositionResets;
        protected override Queue<AionServerPacket> GetSendMsgQueue() => queue;
        protected override void EnqueuePacket(AionServerPacket packet, bool closing) => Sent.Add(packet);
        protected override void ResetPlayerPositionAfterDisconnect(Player player) => PositionResets++;
    }

    private sealed class Lifetime : IHostApplicationLifetime
    {
        public int Stops;
        public CancellationToken ApplicationStarted => CancellationToken.None;
        public CancellationToken ApplicationStopping => CancellationToken.None;
        public CancellationToken ApplicationStopped => CancellationToken.None;
        public void StopApplication() => Stops++;
    }
}
