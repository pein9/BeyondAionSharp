using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Xml.Serialization;
using Aion.Commons.Nio;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Controllers;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Limiteditems;
using Aion.GameServer.Model.Templates.Goods;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.Model.Templates.Npc;
using Aion.GameServer.Model.Templates.Pet;
using Aion.GameServer.Model.Templates.Tradelist;
using Aion.GameServer.Model.Trade;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Players;
using Aion.GameServer.TestKit;
using Aion.GameServer.Utils;
using Aion.GameServer.World.Knownlist;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class NpcShopPortTests
{
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private const BindingFlags Statics = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    [Fact]
    public void ActualPacketReaderMatchesAllCompiledJavaDecisions()
    {
        using var s = new Scope(); using var fixture = Fixture("f0c1a3695", "f0c1a36950f7a8aa11dbc3a5f12d179fe293ba85");
        RepurchaseService.GetInstance().AddRepurchaseItems(s.Player, [s.Item(162000052)]);
        var cases = fixture.RootElement.GetProperty("packetCases"); Assert.Equal(57, cases.GetArrayLength());
        foreach (var c in cases.EnumerateArray())
        {
            var packet = s.Read(Convert.FromHexString(c.GetProperty("bodyHex").GetString()!));
            Assert.Equal(c.GetProperty("audit").GetBoolean(), Get<bool>(packet, "isAudit"));
            Assert.Equal(c.GetProperty("action").GetString(), typeof(CM_BUY_ITEM).GetField("tradeAction", Fields)!.GetValue(packet)?.ToString());
            var list = Get<TradeList?>(packet, "tradeList");
            Assert.Equal(c.GetProperty("rows").GetString(), list == null ? "" : string.Join("|", list.GetTradeItems().Select(i => $"{i.GetItemId()}:{i.GetCount()}")));
            var repurchase = Get<RepurchaseList?>(packet, "repurchaseList");
            Assert.Equal(c.GetProperty("repurchases").GetString(), repurchase == null ? "" : "[" + string.Join(", ", repurchase.GetRepurchaseItems()) + "]");
        }
    }

    [Fact]
    public void ApTotalsAndLongNarrowingMatchTheExactJavaClassIncludingIntegerOverflow()
    {
        using var s = new Scope(); using var fixture = Fixture();
        foreach (var c in fixture.RootElement.GetProperty("apCases").EnumerateArray())
        {
            s.SetAp(c.GetProperty("available").GetInt32());
            PricesConfig.VENDOR_BUY_MODIFIER = c.GetProperty("vendor").GetInt32();
            DataManager.ITEM_DATA.GetItemTemplate(162000052).acquisition = new Acquisition { Type = AcquisitionType.AP, Ap = c.GetProperty("price").GetInt32() };
            var list = new TradeList();
            for (int i = 0; i < c.GetProperty("rows").GetInt32(); i++) list.AddItem(162000052, c.GetProperty("count").GetInt64());
            s.Connection.Clear();
            Assert.Equal(c.GetProperty("accepted").GetBoolean(), list.CalculateAbyssRewardBuyList(s.Player, c.GetProperty("modifier").GetInt32()));
            Assert.Equal(c.GetProperty("total").GetInt64(), list.GetRequiredAp());
            Assert.Equal(c.GetProperty("messages").GetInt32(), s.Connection.Packets.OfType<SM_SYSTEM_MESSAGE>().Count());
        }
        foreach (var c in fixture.RootElement.GetProperty("castCases").EnumerateArray())
            Assert.Equal(c.GetProperty("value").GetInt64(), JavaMath.ToLong(BitConverter.Int64BitsToDouble(c.GetProperty("bits").GetInt64())));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("oversell")]
    [InlineData("unsellable")]
    public void BadLaterSaleRowCannotRemoveEarlierItemsPayKinahOrSpendDailyLimit(string invalid)
    {
        using var s = new Scope(); var first = s.Item(162000052, 10, true); var second = s.Item(162000053, 10, true);
        if (invalid == "unsellable") second.GetItemTemplate().mask = 0;
        var sale = new TradeList(); sale.AddItem(first.GetObjectId(), 3);
        sale.AddItem(invalid == "missing" ? int.MaxValue : second.GetObjectId(), invalid == "oversell" ? 11 : 1);
        var limits = s.SetDailyLimit(1000);
        s.Connection.Clear(); Assert.False(TradeService.PerformSellToShop(s.Player, sale, null!, 100));
        Assert.Equal(10, first.GetItemCount()); Assert.Equal(10, second.GetItemCount());
        Assert.Same(first, s.Player.GetInventory().GetItemByObjId(first.GetObjectId()));
        Assert.Equal(0, s.Player.GetInventory().GetKinah()); Assert.Equal(1000, limits[s.Player.GetAccount().GetId()]);
        Assert.Empty(RepurchaseService.GetInstance().GetRepurchaseItems(s.Player.GetObjectId()));
        Assert.Empty(s.Connection.Packets.OfType<SM_DELETE_ITEM>());
    }

    [Fact]
    public void ValidSaleStillAppliesTheDailyClampAndKeepsSplitExpiry()
    {
        using var s = new Scope(); var item = s.Item(162000052, 10, true); item.SetExpireTime(123456);
        var limit = s.SetDailyLimit(500); var sale = new TradeList(); sale.AddItem(item.GetObjectId(), 8);
        Assert.True(TradeService.PerformSellToShop(s.Player, sale, null!, 100));
        Assert.Equal(5, item.GetItemCount()); Assert.Equal(500, s.Player.GetInventory().GetKinah());
        Assert.Equal(0, limit[s.Player.GetAccount().GetId()]);
        var split = Assert.Single(RepurchaseService.GetInstance().GetRepurchaseItems(s.Player.GetObjectId()));
        Assert.Equal(5, split.GetItemCount()); Assert.Equal(123456, split.GetExpireTime());
    }

    [Fact]
    public void ApSaleWithoutAcquisitionRejectsBeforeRemovingTheItem()
    {
        using var s = new Scope(); var item = s.Item(162000052, 10, true); s.SetAp(17);
        var sale = new TradeList(); sale.AddItem(item.GetObjectId(), 3);
        Assert.False(TradeService.PerformSellForAPToShop(s.Player, sale, s.Purchase));
        Assert.Equal(10, item.GetItemCount()); Assert.Equal(17, s.Player.GetAbyssRank().GetAp());
        Assert.Same(item, s.Player.GetInventory().GetItemByObjId(item.GetObjectId()));
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, true)]
    public void PurchaseRequiresSlotsForAllAllocatedStacks(int slots, bool accepted)
    {
        using var s = new Scope(); s.Player.GetInventory().SetLimit(slots);
        var buy = new TradeList(); buy.AddItem(162000052, 11);
        Assert.Equal(accepted, TradeService.PerformBuyTransaction(s.Npc, s.Player, buy, false));
        Assert.Equal(accepted ? 11 : 0, s.Player.GetInventory().GetItemCountByItemId(162000052));
        Assert.Equal(accepted ? 2 : 0, s.Player.GetInventory().GetItemsByItemId(162000052).Count);
    }

    [Theory]
    [InlineData(true, 3, false)]
    [InlineData(false, 3, false)]
    [InlineData(true, 4, true)]
    [InlineData(false, 4, true)]
    public void RepeatedItemRowsShareTheBuyOrSellLimit(bool perPlayer, int maximum, bool accepted)
    {
        using var s = new Scope(); var limited = s.Limit(perPlayer ? 0 : maximum, perPlayer ? maximum : 0);
        var buy = new TradeList(); buy.AddItem(162000052, 2); buy.AddItem(162000052, 2);
        Assert.Equal(accepted, TradeService.PerformBuyTransaction(s.Npc, s.Player, buy, false));
        Assert.Equal(accepted ? 4 : 0, s.Player.GetInventory().GetItemCountByItemId(162000052));
        Assert.Equal(perPlayer && accepted ? 4 : 0, limited.GetBuyCount(s.Player.GetObjectId()));
        Assert.Equal(perPlayer ? 0 : maximum - (accepted ? 4 : 0), limited.GetSellLimit());
    }

    [Theory]
    [InlineData(true, 17, true)]
    [InlineData(false, 17, false)]
    [InlineData(true, 1, false)]
    public void MerchantPetOnlySellsForItsOwnerAndThePetSellAction(bool owned, byte action, bool sold)
    {
        using var s = new Scope(); var item = s.Item(162000052, 10, true);
        var master = owned ? s.Player : s.Common.Player(65);
        var template = new PetTemplate { id = 700001, name = "merchant", petFunctions = [new PetFunction { type = PetFunctionType.MERCHANT, ratePrice = 100 }] };
        // This merchant has no feeding/doping state; avoid the unrelated global pet-data loaders.
        var petData = (PetCommonData)RuntimeHelpers.GetUninitializedObject(typeof(PetCommonData));
        typeof(PetCommonData).GetField("objectId", Fields)!.SetValue(petData, 51);
        var pet = new Pet(template, new PetController(), petData, master);
        s.Player.SetKnownlist(new DirectKnownList(s.Player, pet));
        var packet = s.Read(Body(action, item.GetObjectId(), 3)); packet.Run();
        Assert.Equal(sold ? 7 : 10, item.GetItemCount()); Assert.Equal(sold ? 300 : 0, s.Player.GetInventory().GetKinah());
    }

    private static byte[] Body(byte action, int id, long count)
    {
        using var stream = new MemoryStream(); using var w = new BinaryWriter(stream);
        w.Write(51); w.Write(action); w.Write((byte)0); w.Write((ushort)1); w.Write(id); w.Write(count); return stream.ToArray();
    }
    private static T Get<T>(object target, string field) => (T)target.GetType().GetField(field, Fields)!.GetValue(target)!;
    private static JsonDocument Fixture(string revision = "e2b0c35e2", string sha = "e2b0c35e2bd364366821db65cdfa274a38aa71b8")
    {
        var fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(RealStaticData.RepoRoot(), $"parity-artifacts/golden/formulas/NpcShop.{revision}.json")));
        Assert.Equal(sha, fixture.RootElement.GetProperty("sourceJavaCommit").GetString()); return fixture;
    }
    private sealed class DirectKnownList : KnownList
    {
        internal DirectKnownList(Player player, VisibleObject target) : base(player)
        {
            KnownObjects[target.ObjectId] = new KnownObject(target);
        }
    }

    private sealed class Scope : IDisposable
    {
        internal PvpExpPortTests.Scope Common { get; } = new();
        internal Player Player { get; }
        internal PvpExpPortTests.Connection Connection => (PvpExpPortTests.Connection)Player.GetClientConnection();
        internal Npc Npc { get; }
        internal TradeListTemplate Purchase { get; }
        private const int NpcId = 990991, Tab = 990992;
        private readonly Stack<Action> restore = new();
        private int nextId = 401;
        internal Scope()
        {
            var shutdown = typeof(ShutdownHook).GetField("_instance", Statics)!; var prior = shutdown.GetValue(null); shutdown.SetValue(null, null); restore.Push(() => shutdown.SetValue(null, prior));
            bool limits = CustomConfig.LIMITS_ENABLED, apSales = CustomConfig.SELLING_APITEMS_ENABLED, punishment = PunishmentConfig.PUNISHMENT_ENABLE;
            int vendor = PricesConfig.VENDOR_BUY_MODIFIER;
            restore.Push(() => { CustomConfig.LIMITS_ENABLED = limits; CustomConfig.SELLING_APITEMS_ENABLED = apSales; PunishmentConfig.PUNISHMENT_ENABLE = punishment; PricesConfig.VENDOR_BUY_MODIFIER = vendor; });
            CustomConfig.LIMITS_ENABLED = false; CustomConfig.SELLING_APITEMS_ENABLED = true; PunishmentConfig.PUNISHMENT_ENABLE = false;
            using var input = new StringReader("""
                <item_templates>
                  <item_template id="162000052" name="Goods" level="1" quality="COMMON" max_stack_count="10" mask="6" price="100"/>
                  <item_template id="162000053" name="Other" level="1" quality="COMMON" max_stack_count="10" mask="6" price="100"/>
                  <item_template id="182400001" name="Kinah" level="1" quality="COMMON" max_stack_count="2147483647"/>
                </item_templates>
                """);
            var data = (ItemData)new XmlSerializer(typeof(ItemData)).Deserialize(input)!; data.AfterUnmarshal(null!);
            typeof(StaticData).GetField("<ItemDataDh>k__BackingField", Fields)!.SetValue(Common.H.Data.StaticData, data);
            Player = Common.Player(65); Player.GetInventory().SetLimit(10); SetAp(0);
            Npc = (Npc)RuntimeHelpers.GetUninitializedObject(typeof(Npc));
            typeof(VisibleObject).GetField("_objectTemplate", Fields)!.SetValue(Npc, new NpcTemplate { XmlUid = NpcId.ToString() });
            var trades = (TradeListData)typeof(TradeService).GetField("tradeListData", Statics)!.GetValue(null)!;
            var goods = (GoodsListData)typeof(TradeService).GetField("goodsListData", Statics)!.GetValue(null)!;
            Purchase = new TradeListTemplate { npcId = NpcId, buyPriceRate = 100, tradeTablist = [new() { id = Tab }] };
            Put(Get<Dictionary<int, TradeListTemplate>>(trades, "npctlistData"), NpcId, Purchase);
            Put(Get<Dictionary<int, TradeListTemplate>>(trades, "npcPurchaseTemplateData"), NpcId, Purchase);
            var list = new GoodsList(); list.AfterUnmarshal(null!); list.GetItemIdList().AddRange([162000052, 162000053]);
            Put(Get<Dictionary<int, GoodsList>>(goods, "goodsListData"), Tab, list);
            Put(Get<Dictionary<int, GoodsList>>(goods, "goodsPurchaseListData"), Tab, list);
        }
        internal void SetAp(int ap)
        {
            var rank = (AbyssRank)RuntimeHelpers.GetUninitializedObject(typeof(AbyssRank)); typeof(AbyssRank).GetField("currentAp", Fields)!.SetValue(rank, ap); Player.SetAbyssRank(rank);
        }
        internal Item Item(int id, int count = 1, bool owned = false)
        {
            var item = new Item(nextId++, DataManager.ITEM_DATA.GetItemTemplate(id)); item.SetItemCount(count); if (owned) Player.GetInventory().OnLoadHandler(item); return item;
        }
        internal CM_BUY_ITEM Read(byte[] body)
        {
            var packet = new CM_BUY_ITEM(0, new HashSet<AionConnection.State> { AionConnection.State.IN_GAME });
            packet.SetConnection(Connection); packet.SetBuffer(ByteBuffer.Wrap(body).Order(ByteOrder.LITTLE_ENDIAN)); Assert.True(packet.Read()); return packet;
        }
        internal IReadOnlyDictionary<int, long> SetDailyLimit(long cap)
        {
            CustomConfig.LIMITS_ENABLED = true;
            // ConcurrentDictionary uses the IDictionary contract while restoring only this account's entry.
            var map = (System.Collections.Concurrent.ConcurrentDictionary<int, long>)typeof(PlayerLimitService).GetField("sellLimit", Statics)!.GetValue(null)!;
            int id = Player.GetAccount().GetId(); bool had = map.TryGetValue(id, out long old); map[id] = cap;
            restore.Push(() => { if (had) map[id] = old; else map.TryRemove(id, out _); });
            return map;
        }
        internal LimitedItem Limit(int sell, int buy)
        {
            var item = new LimitedItem(162000052, sell, buy, "unused"); var npc = new LimitedTradeNpc(); npc.AddLimitedItems([item]);
            Put(Get<Dictionary<int, LimitedTradeNpc>>(LimitedItemTradeService.GetInstance(), "limitedTradeNpcs"), NpcId, npc); return item;
        }
        private void Put<T>(Dictionary<int, T> map, int id, T value)
        {
            bool had = map.TryGetValue(id, out var old); map[id] = value; restore.Push(() => { if (had) map[id] = old!; else map.Remove(id); });
        }
        public void Dispose()
        {
            RepurchaseService.GetInstance().RemoveRepurchaseItems(Player);
            while (restore.TryPop(out var action)) action();
            Common.Dispose();
        }
    }
}
