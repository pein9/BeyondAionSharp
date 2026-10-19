using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Xml.Serialization;
using Aion.Commons.Database;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Trade;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.TestKit;
using MySqlConnector;
using ExchangeAction = Aion.GameServer.Network.Aion.ServerPackets.SM_EXCHANGE_CONFIRMATION.Action;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class ExchangePreflightPortTests
{
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private const BindingFlags Statics = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    [Fact]
    public void ActualPreflightMatchesCompiledJavaAtStockAndWalletBoundaries()
    {
        using var fixture = Fixture(); var cases = fixture.RootElement.GetProperty("preflightCases"); Assert.Equal(12, cases.GetArrayLength());
        foreach (var c in cases.EnumerateArray())
        {
            using var s = new Scope(); s.First.GetInventory().GetKinahItem().SetItemCount(c.GetProperty("balance").GetInt64());
            var offer = new Exchange(s.First, s.Second); offer.AddKinah(c.GetProperty("offeredKinah").GetInt64());
            foreach (var row in c.GetProperty("items").EnumerateArray())
            {
                int id = row.GetProperty("id").GetInt32();
                if (row.GetProperty("stock").ValueKind != JsonValueKind.Null) s.Item(s.First, 162000052, row.GetProperty("stock").GetInt64(), id);
                offer.AddItem(id, new ExchangeItem(id, row.GetProperty("offered").GetInt64(), null!));
            }
            Assert.Equal(c.GetProperty("accepted").GetBoolean(), (bool)typeof(ExchangeService).GetMethod("CanGive", Fields)!.Invoke(s.Service, [s.First, offer])!);
        }
    }

    [Theory]
    [InlineData("COMPLETED")]
    [InlineData("CANCELED")]
    [InlineData("PARTNER_CONFIRMED")]
    [InlineData("PARTNER_LOCKED")]
    public void NamedActionsKeepTheExactJavaConfirmationByte(string action)
    {
        using var fixture = Fixture(); var expected = fixture.RootElement.GetProperty("packetCases").EnumerateArray().Single(c => c.GetProperty("action").GetString() == action);
        Assert.Equal(Convert.FromHexString(expected.GetProperty("payloadHex").GetString()!), Payload(new SM_EXCHANGE_CONFIRMATION(Enum.Parse<ExchangeAction>(action))));
    }

    [Theory]
    [InlineData(false, "kinah")]
    [InlineData(true, "kinah")]
    [InlineData(false, "missing")]
    [InlineData(true, "missing")]
    [InlineData(false, "short")]
    [InlineData(true, "short")]
    public void AChangedOfferOnEitherSideCancelsBeforeEitherInventoryOrWalletIsDebited(bool damageSecond, string change)
    {
        using var s = new Scope(); using var sql = new RefusingDatabase();
        var first = s.Offer(s.First, 162000052, 10, 3); var second = s.Offer(s.Second, 162000053, 10, 4);
        s.Service.AddKinah(s.First, 20); s.Service.AddKinah(s.Second, 30); s.LockBoth(); s.Service.ConfirmExchange(s.First);
        var damagedPlayer = damageSecond ? s.Second : s.First; var damagedItem = damageSecond ? second : first;
        if (change == "kinah") damagedPlayer.GetInventory().GetKinahItem().SetItemCount(damageSecond ? 29 : 19);
        if (change == "missing") damagedPlayer.GetInventory().Remove(damagedItem);
        if (change == "short") damagedItem.SetItemCount(2);
        long aCount = first.GetItemCount(), bCount = second.GetItemCount(), aKinah = s.First.GetInventory().GetKinah(), bKinah = s.Second.GetInventory().GetKinah();
        s.Clear(); s.Service.ConfirmExchange(s.Second);
        Assert.Equal(aCount, first.GetItemCount()); Assert.Equal(bCount, second.GetItemCount());
        Assert.Equal(aKinah, s.First.GetInventory().GetKinah()); Assert.Equal(bKinah, s.Second.GetInventory().GetKinah());
        Assert.Equal(0, s.First.GetInventory().GetItemCountByItemId(162000053)); Assert.Equal(0, s.Second.GetInventory().GetItemCountByItemId(162000052));
        Assert.Equal(new byte[] {2, 1}, Actions(s.First)); Assert.Equal(new byte[] {1}, Actions(s.Second));
        Assert.False(s.Service.IsPlayerInExchange(s.First)); Assert.False(s.Service.IsPlayerInExchange(s.Second));
        Assert.Equal(0, sql.Accepted);
    }

    [Theory]
    [InlineData("target")]
    [InlineData("partner")]
    [InlineData("active")]
    public void ConfirmationAfterAParticipantLeftReturnsWithoutPacketsOrSideEffects(string missing)
    {
        using var s = new Scope();
        if (missing == "target") s.Exchanges[s.First.GetObjectId()] = new Exchange(s.First, null!);
        if (missing == "partner") s.Exchanges.TryRemove(s.Second.GetObjectId(), out _);
        if (missing == "active") s.Exchanges.TryRemove(s.First.GetObjectId(), out _);
        s.Clear(); s.Service.ConfirmExchange(s.First);
        Assert.Empty(Actions(s.First)); Assert.Empty(Actions(s.Second));
        Assert.Equal(100, s.First.GetInventory().GetKinah()); Assert.Equal(100, s.Second.GetInventory().GetKinah());
        if (missing != "active") Assert.True(s.Exchanges[s.First.GetObjectId()].IsConfirmed());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ValidExchangeTransfersBothSidesAndCleansUpThroughTheExistingSqlFailurePath(bool partial)
    {
        using var s = new Scope(); using var sql = new RefusingDatabase();
        int firstId = partial ? 162000052 : 100000111;
        var first = s.Offer(s.First, firstId, partial ? 10 : 1, partial ? 3 : 1);
        var second = s.Offer(s.Second, 162000053, 10, partial ? 4 : 10);
        // Whole items retain their object and all attributes; partial-stack expiry was added by07959a2ef.
        if (!partial)
        {
            first.SetActivationCount(2); first.SetColorExpireTime(654321); first.SetRndPlumeBonusValue(37);
            first.GetConditioningInfo().UpdateChargePoints(723456); first.SetEnchantLevel(5); first.SetItemCreator("Crafter");
        }
        s.Service.AddKinah(s.First, partial ? 20 : 100); s.Service.AddKinah(s.Second, partial ? 30 : 100);
        s.LockBoth(); s.Service.ConfirmExchange(s.First); s.Clear(); s.Service.ConfirmExchange(s.Second);
        Assert.Equal(new byte[] {2, 0}, Actions(s.First)); Assert.Equal(new byte[] {0}, Actions(s.Second));
        Assert.False(s.Service.IsPlayerInExchange(s.First)); Assert.False(s.Service.IsPlayerInExchange(s.Second));
        Assert.Equal(partial ? 110 : 100, s.First.GetInventory().GetKinah()); Assert.Equal(partial ? 90 : 100, s.Second.GetInventory().GetKinah());
        var receivedFirst = Assert.Single(s.Second.GetInventory().GetItemsByItemId(firstId));
        var receivedSecond = Assert.Single(s.First.GetInventory().GetItemsByItemId(162000053));
        Assert.Equal(partial ? 3 : 1, receivedFirst.GetItemCount()); Assert.Equal(partial ? 4 : 10, receivedSecond.GetItemCount());
        Assert.Equal(123456, receivedFirst.GetExpireTime()); Assert.Equal(123456, receivedSecond.GetExpireTime());
        if (partial)
        {
            Assert.NotSame(first, receivedFirst); Assert.NotSame(second, receivedSecond);
            Assert.Equal(7, first.GetItemCount()); Assert.Equal(6, second.GetItemCount());
        }
        else
        {
            Assert.Same(first, receivedFirst); Assert.Same(second, receivedSecond);
            Assert.Equal(2, receivedFirst.GetActivationCount()); Assert.Equal(654321, receivedFirst.GetColorExpireTime());
            Assert.Equal(37, receivedFirst.GetRndPlumeBonusValue()); Assert.Equal(723456, receivedFirst.GetChargePoints());
            Assert.Equal(5, receivedFirst.GetEnchantLevel()); Assert.Equal("Crafter", receivedFirst.GetItemCreator());
        }
        Assert.True(sql.Accepted >= 2); // Own loopback sockets close before a MySQL handshake; no SQL can execute.
    }

    [Fact]
    public void ExistingCapacityCheckStillPrecedesPreflightAndLeavesBothInventoriesIntact()
    {
        using var s = new Scope(); var first = s.Offer(s.First, 162000052, 10, 3); var second = s.Offer(s.Second, 162000053, 10, 4);
        s.LockBoth(); s.Service.ConfirmExchange(s.First); s.First.GetInventory().SetLimit(0); s.Clear(); s.Service.ConfirmExchange(s.Second);
        Assert.Equal(10, first.GetItemCount()); Assert.Equal(10, second.GetItemCount());
        Assert.Equal(new byte[] {2}, Actions(s.First)); Assert.Empty(Actions(s.Second));
        Assert.False(s.Service.IsPlayerInExchange(s.First)); Assert.False(s.Service.IsPlayerInExchange(s.Second));
    }

    [Fact]
    public void LockAndExplicitCancellationStillUseTheOriginalWireActions()
    {
        using var s = new Scope(); s.Service.LockExchange(s.First); Assert.Equal(new byte[] {3}, Actions(s.Second));
        s.Clear(); s.Service.CancelExchange(s.First); Assert.Empty(Actions(s.First)); Assert.Equal(new byte[] {1}, Actions(s.Second));
        Assert.False(s.Service.IsPlayerInExchange(s.First)); Assert.False(s.Service.IsPlayerInExchange(s.Second));
    }

    private static byte[] Payload(Aion.GameServer.Network.Aion.AionServerPacket packet) => LimitedRecipeMorphPortTests.Payload(packet);
    private static byte[] Actions(Player player) => ((PvpExpPortTests.Connection)player.GetClientConnection()).Packets.OfType<SM_EXCHANGE_CONFIRMATION>().Select(p => Assert.Single(Payload(p))).ToArray();
    private static JsonDocument Fixture()
    {
        var fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(RealStaticData.RepoRoot(), "parity-artifacts/golden/formulas/ExchangePreflight.ea257e5e9.json")));
        Assert.Equal("ea257e5e9bd8e6e6e091feb607a56d5fa187ef0a", fixture.RootElement.GetProperty("sourceJavaCommit").GetString()); return fixture;
    }

    private sealed class Scope : IDisposable
    {
        internal PvpExpPortTests.Scope Common { get; } = new();
        internal Player First { get; }
        internal Player Second { get; }
        internal ExchangeService Service { get; } = (ExchangeService)Activator.CreateInstance(typeof(ExchangeService), true)!;
        internal ConcurrentDictionary<int, Exchange> Exchanges => (ConcurrentDictionary<int, Exchange>)typeof(ExchangeService).GetField("exchanges", Fields)!.GetValue(Service)!;
        private readonly FieldInfo shutdown = typeof(ShutdownHook).GetField("_instance", Statics)!;
        private readonly object? previousShutdown;
        private readonly bool previousPunishment = PunishmentConfig.PUNISHMENT_ENABLE;
        private int nextId = 992000;
        internal Scope()
        {
            previousShutdown = shutdown.GetValue(null); shutdown.SetValue(null, null); PunishmentConfig.PUNISHMENT_ENABLE = false;
            using var input = new StringReader("""
                <item_templates>
                  <item_template id="100000111" name="Weapon" level="65" quality="EPIC" item_group="SWORD" mask="6"><improve level="2"/></item_template>
                  <item_template id="162000052" name="Goods" level="1" quality="COMMON" max_stack_count="100" mask="6"/>
                  <item_template id="162000053" name="Other" level="1" quality="COMMON" max_stack_count="100" mask="6"/>
                  <item_template id="182400001" name="Kinah" level="1" quality="COMMON" max_stack_count="2147483647"/>
                </item_templates>
                """);
            var data = (ItemData)new XmlSerializer(typeof(ItemData)).Deserialize(input)!; data.AfterUnmarshal(null!);
            typeof(StaticData).GetField("<ItemDataDh>k__BackingField", Fields)!.SetValue(Common.H.Data.StaticData, data);
            First = Common.Player(65); Second = Common.Player(65);
            First.GetInventory().SetLimit(10); Second.GetInventory().SetLimit(10);
            Item(First, 182400001, 100); Item(Second, 182400001, 100); Service.RegisterExchange(First, Second); Clear();
        }
        internal Item Item(Player player, int id, long count, int? objectId = null)
        {
            var item = new Item(objectId ?? nextId++, DataManager.ITEM_DATA.GetItemTemplate(id)); item.SetItemCount(count); player.GetInventory().OnLoadHandler(item); return item;
        }
        internal Item Offer(Player player, int id, int owned, int offered)
        {
            var item = Item(player, id, owned); item.SetExpireTime(123456); Service.AddItem(player, item.GetObjectId(), offered); return item;
        }
        internal void LockBoth() { Service.LockExchange(First); Service.LockExchange(Second); }
        internal void Clear() { ((PvpExpPortTests.Connection)First.GetClientConnection()).Clear(); ((PvpExpPortTests.Connection)Second.GetClientConnection()).Clear(); }
        public void Dispose()
        {
            if (Service.IsPlayerInExchange(First)) Service.CancelExchange(First);
            if (Service.IsPlayerInExchange(Second)) Service.CancelExchange(Second);
            PunishmentConfig.PUNISHMENT_ENABLE = previousPunishment; shutdown.SetValue(null, previousShutdown); Common.Dispose();
        }
    }

    // The production exchange ignores the DAO's false result. Exercise that unchanged boundary without a real database.
    private sealed class RefusingDatabase : IDisposable
    {
        private readonly FieldInfo slot = typeof(DatabaseFactory).GetField("_dataSource", Statics)!;
        private readonly object? previous;
        private readonly TcpListener listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource stop = new();
        private readonly Task acceptLoop;
        private readonly MySqlDataSource dataSource;
        private int accepted;
        internal int Accepted => Volatile.Read(ref accepted);
        internal RefusingDatabase()
        {
            listener.Start();
            acceptLoop = Task.Run(async () =>
            {
                try
                {
                    while (!stop.IsCancellationRequested)
                    {
                        using var client = await listener.AcceptTcpClientAsync(stop.Token);
                        Interlocked.Increment(ref accepted);
                    }
                }
                catch (Exception) when (stop.IsCancellationRequested) { }
            });
            var options = new MySqlConnectionStringBuilder { Server = "127.0.0.1", Port = (uint)((IPEndPoint)listener.LocalEndpoint).Port, UserID = "exchange-test", Database = "unused", Pooling = false, ConnectionTimeout = 2, SslMode = MySqlSslMode.None };
            dataSource = new MySqlDataSourceBuilder(options.ConnectionString).Build(); previous = slot.GetValue(null); slot.SetValue(null, dataSource);
        }
        public void Dispose()
        {
            slot.SetValue(null, previous); dataSource.Dispose(); stop.Cancel(); listener.Stop(); acceptLoop.GetAwaiter().GetResult(); stop.Dispose();
        }
    }
}
