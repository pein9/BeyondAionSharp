using System.Reflection;
using System.Text.Json;
using Aion.Commons.Nio;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Limiteditems;
using Aion.GameServer.Model.Team.Legion;
using Aion.GameServer.Model.Templates.Goods;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.Model.Templates.Npc;
using Aion.GameServer.Model.Templates.Tradelist;
using Aion.GameServer.Model.Trade;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Trade;
using Aion.GameServer.TestKit;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class TradeLegionPortTests
{
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private const BindingFlags Statics = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private const string Sha = "638ed3dd4032ff01e4691a4ec3d6eb85cdf951d3";

    public static IEnumerable<object[]> JavaFilterCases()
    {
        using var f = Fixture();
        foreach (var c in f.RootElement.GetProperty("filterCases").EnumerateArray())
            yield return [c.GetProperty("level").GetInt32(), c.GetProperty("query").GetInt32(),
                c.GetProperty("tabs").EnumerateArray().Select(v => v.GetInt32()).ToArray(), c.GetProperty("accepted").GetBoolean()];
    }

    [Theory]
    [MemberData(nameof(JavaFilterCases))]
    public void FilteringAndValidationMatchExactJava(int level, int query, int[] tabs, bool accepted)
    {
        using var s = new Scope(); s.Level(level);
        s.S.Purchase.tradeTablist = new[] { 103, 999, 101, 102, 101, 104 }.Select(id => new TradeListTemplate.TradeTab { id = id }).ToList();
        if (query == 6) s.S.Purchase.tradeTablist.RemoveAll(t => t.id == 101);
        var buy = new TradeList();
        if (query > 0) buy.AddItem(query == 1 ? 162000054 : 162000053, query == 3 ? 0 : query == 4 ? -1 : query == 5 ? long.MaxValue : 1);
        Assert.Equal(tabs, TradeService.GetTradeListTabIds(s.S.Purchase, s.S.Player));
        Assert.Equal(accepted, (bool)typeof(TradeService).GetMethod("ValidateBuyItems", Statics)!.Invoke(null, [s.S.Purchase, buy, s.S.Player])!);
        // Unlike the model snapshot, the filtered result is a fresh, mutable list.
        var filtered = TradeService.GetTradeListTabIds(s.S.Purchase, s.S.Player); filtered.Add(123);
        Assert.Equal(tabs, TradeService.GetTradeListTabIds(s.S.Purchase, s.S.Player));
    }

    [Fact]
    public void TabIdsAreAnImmutableSnapshotAndDoNotInitializeMissingXmlTabs()
    {
        var t = new TradeListTemplate(); Assert.Null(t.tradeTablist); Assert.Empty(t.GetTradeTabIds()); Assert.Null(t.tradeTablist);
        Assert.Throws<NotSupportedException>(() => ((IList<int>)t.GetTradeTabIds()).Add(1));
        t.tradeTablist = [new() { id = 101 }, new() { id = 102 }, new() { id = 101 }];
        var snapshot = t.GetTradeTabIds(); t.tradeTablist[0].id = 999; t.tradeTablist.Clear();
        Assert.Equal(new[] { 101, 102, 101 }, snapshot);
        Assert.Throws<NotSupportedException>(() => ((IList<int>)snapshot)[0] = 999);
        t.tradeTablist = [null!]; Assert.Throws<NullReferenceException>(() => t.GetTradeTabIds());
    }

    [Theory]
    [InlineData(TradeNpcType.NORMAL)]
    [InlineData(TradeNpcType.ABYSS)]
    [InlineData(TradeNpcType.LEGION_COIN)]
    [InlineData(TradeNpcType.REWARD)]
    [InlineData(TradeNpcType.ABYSS_KINAH)]
    public void ThreePacketWritersMatchExactJavaForEveryWireType(TradeNpcType type)
    {
        using var s = new Scope(); using var fixture = Fixture();
        s.S.Purchase.tradeNpcType = type; s.S.Purchase.buyPriceRate = 115;
        s.S.Purchase.tradeTablist = [new() { id = 101 }, new() { id = 102 }, new() { id = 101 }];
        foreach (var c in fixture.RootElement.GetProperty("packetCases").EnumerateArray().Where(c => c.GetProperty("npcType").GetString() == type.ToString()))
        {
            // Explicit caller IDs are already filtered; the writer must not filter again.
            AionServerPacket packet = c.GetProperty("kind").GetString() switch
            {
                "buy" => new SM_TRADELIST(s.S.Player, s.S.Npc, type, new List<int> { 101, 102, 101 }, 83),
                "sell" => new SM_SELL_ITEM(s.S.Npc),
                _ => new SM_TRADE_IN_LIST(s.S.Npc, s.S.Purchase, 83)
            };
            Assert.Equal(c.GetProperty("payloadHex").GetString(), Hex(packet));
        }
    }

    [Fact]
    public void TradeListUsesActualNpcStockAndKeepsCallerAndStockListsLive()
    {
        using var s = new Scope(); using var fixture = Fixture();
        // A mismatched template ID cannot redirect the limited-stock lookup.
        s.S.Purchase.npcId = 0;
        var stock = s.S.Limit(9, 10); stock.SetBuyCount(s.S.Player.GetObjectId(), 2);
        var ids = new List<int> { 101 }; var packet = new SM_TRADELIST(s.S.Player, s.S.Npc, TradeNpcType.LEGION_COIN, ids, 83);
        ids.Add(102);
        var extra = new LimitedItem(162000053, 65538, 100000, "unused"); extra.SetBuyCount(s.S.Player.GetObjectId(), 65537);
        LimitedItemTradeService.GetInstance().GetLimitedTradeNpc(s.S.Npc.GetNpcId())!.GetLimitedItems().Add(extra);
        SetId(s.S.Npc, 123); // Object ID was cached at construction; list contents remain live.
        Assert.Equal(fixture.RootElement.GetProperty("special").GetProperty("live").GetString(), Hex(packet));
    }

    [Fact]
    public void SellAndTradeInSnapshotTemplateIdsAndTypeAndTradeInRequiresTemplate()
    {
        using var s = new Scope(); using var fixture = Fixture();
        s.S.Purchase.tradeNpcType = TradeNpcType.ABYSS_KINAH; s.S.Purchase.buyPriceRate = 115;
        s.S.Purchase.tradeTablist = [new() { id = 101 }, new() { id = 102 }, new() { id = 101 }];
        var sell = new SM_SELL_ITEM(s.S.Npc); var tradein = new SM_TRADE_IN_LIST(s.S.Npc, s.S.Purchase, 83);
        s.S.Purchase.tradeTablist[0].id = 999; s.S.Purchase.tradeTablist.Clear(); s.S.Purchase.tradeNpcType = TradeNpcType.NORMAL;
        Assert.Equal(fixture.RootElement.GetProperty("special").GetProperty("sellSnapshot").GetString(), Hex(sell));
        Assert.Equal(fixture.RootElement.GetProperty("special").GetProperty("tradeinSnapshot").GetString(), Hex(tradein));
        Assert.Throws<NullReferenceException>(() => new SM_TRADE_IN_LIST(s.S.Npc, null!, 83));
    }

    [Fact]
    public void MissingSellTemplateUsesNormalTypeEmptyTabsAndVendorPrice()
    {
        using var s = new Scope(); using var fixture = Fixture();
        int previous = PricesConfig.VENDOR_SELL_MODIFIER;
        try
        {
            PricesConfig.VENDOR_SELL_MODIFIER = 73; s.S.Npc.GetObjectTemplate().XmlUid = "990990";
            Assert.Equal(fixture.RootElement.GetProperty("special").GetProperty("sellMissing").GetString(), Hex(new SM_SELL_ITEM(s.S.Npc)));
        }
        finally { PricesConfig.VENDOR_SELL_MODIFIER = previous; }
    }

    [Theory]
    [InlineData(TradeNpcType.NORMAL, false)]
    [InlineData(TradeNpcType.ABYSS, true)]
    [InlineData(TradeNpcType.LEGION_COIN, false)]
    [InlineData(TradeNpcType.REWARD, true)]
    [InlineData(TradeNpcType.ABYSS_KINAH, false)]
    public void UnifiedBuyEntrySelectsKinahForAllFiveNpcTypes(TradeNpcType type, bool accepted)
    {
        using var s = new Scope(); s.S.Purchase.tradeNpcType = type;
        var buy = new TradeList(); buy.AddItem(162000052, 1);
        Assert.Equal(accepted, TradeService.BuyFromShop(s.S.Npc, s.S.Player, buy));
        Assert.Equal(accepted ? 1 : 0, s.S.Player.GetInventory().GetItemCountByItemId(162000052));
        Assert.Equal(0, s.S.Player.GetInventory().GetKinah());
        if (!accepted) AssertMessage(s.S.Connection, SM_SYSTEM_MESSAGE.STR_MSG_NOT_ENOUGH_MONEY());
    }

    [Theory]
    [InlineData(TradeNpcType.NORMAL)]
    [InlineData(TradeNpcType.LEGION_COIN)]
    [InlineData(TradeNpcType.ABYSS_KINAH)]
    public void FundedPurchaseUsesSecondaryKinahRateOnlyForAbyssKinah(TradeNpcType type)
    {
        using var s = new Scope(); s.S.Purchase.tradeNpcType = type;
        s.S.Purchase.sellPriceRate = 137; s.S.Purchase.sellPriceRate2 = 59;
        s.S.Item(182400001, 10000, true);
        var buy = new TradeList(); buy.AddItem(162000052, 2);
        long price = PricesService.GetBuyPrice(100, s.S.Player.GetRace()) * 2 * (type == TradeNpcType.ABYSS_KINAH ? 59 : 137) / 100;
        Assert.True(TradeService.BuyFromShop(s.S.Npc, s.S.Player, buy));
        Assert.Equal(10000 - price, s.S.Player.GetInventory().GetKinah());
        Assert.Equal(2, s.S.Player.GetInventory().GetItemCountByItemId(162000052));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    public void RestrictedPurchaseRejectsBeforePricesCostsInventoryOrStockChange(int level)
    {
        using var s = new Scope(); s.Level(level); s.S.Purchase.tradeTablist = [new() { id = 102 }];
        s.S.Purchase.tradeNpcType = TradeNpcType.ABYSS_KINAH; s.S.SetAp(1000);
        s.S.Item(182400001, 10000, true); var token = s.S.Item(162000052, 10, true);
        DataManager.ITEM_DATA.GetItemTemplate(162000053).acquisition = new Acquisition { Type = AcquisitionType.AP, Ap = 100, ItemId = 162000052, ItemCount = 2 };
        var stock = s.S.Limit(9, 9); stock.SetItem(162000053);
        var buy = new TradeList(); buy.AddItem(162000053, 1); s.S.Connection.Clear();
        Assert.False(TradeService.BuyFromShop(s.S.Npc, s.S.Player, buy));
        Assert.Equal(10000, s.S.Player.GetInventory().GetKinah()); Assert.Equal(1000, s.S.Player.GetAbyssRank().GetAp());
        Assert.Equal(10, token.GetItemCount()); Assert.Equal(0, s.S.Player.GetInventory().GetItemCountByItemId(162000053));
        Assert.Equal(9, stock.GetSellLimit()); Assert.Equal(0, stock.GetBuyCount(s.S.Player.GetObjectId()));
        Assert.Equal(0, buy.GetRequiredKinah()); Assert.Equal(0, buy.GetRequiredAp()); Assert.Empty(buy.GetRequiredItems());
        AssertMessage(s.S.Connection, SM_SYSTEM_MESSAGE.STR_BUY_SELL_USER_BUY_FAILED());
    }

    [Theory]
    [InlineData(-1, false, false)]
    [InlineData(2, false, false)]
    [InlineData(3, false, true)]
    [InlineData(-1, true, true)]
    public void DialogAndActualPurchaseAgreeAtLegionThresholdAndForMixedSourceItems(int level, bool mixed, bool allowed)
    {
        using var s = new Scope(); s.Level(level); s.S.Purchase.tradeNpcType = TradeNpcType.ABYSS;
        s.S.Purchase.tradeTablist = mixed ? [new() { id = 102 }, new() { id = 101 }] : [new() { id = 102 }];
        s.S.Connection.Clear(); DialogService.OnDialogSelect(DialogAction.BUY, s.S.Player, s.S.Npc, 0, 0);
        if (allowed)
        {
            var packet = Assert.Single(s.S.Connection.Packets.OfType<SM_TRADELIST>());
            Assert.Equal(TradeService.GetTradeListTabIds(s.S.Purchase, s.S.Player), Get<IReadOnlyList<int>>(packet, "tradeTabIds"));
        }
        else
        {
            Assert.Empty(s.S.Connection.Packets.OfType<SM_TRADELIST>());
            AssertMessage(s.S.Connection, SM_SYSTEM_MESSAGE.STR_BUY_SELL_HE_DOES_NOT_SELL_ITEM(s.S.Npc.GetObjectTemplate().GetL10n()!));
        }
        var buy = new TradeList(); buy.AddItem(162000053, 1);
        Assert.Equal(allowed, TradeService.BuyFromShop(s.S.Npc, s.S.Player, buy));
        Assert.Equal(allowed ? 1 : 0, s.S.Player.GetInventory().GetItemCountByItemId(162000053));
    }

    [Fact]
    public void LimitedStockStartAcceptsOmittedTabsAndSkipsMissingGoods()
    {
        using var s = new Scope();
        var map = Get<Dictionary<int, TradeListTemplate>>(DataManager.TRADE_LIST_DATA, "npctlistData");
        var prior = map.ToArray();
        try
        {
            map.Clear(); map[990991] = new TradeListTemplate();
            map[990990] = new TradeListTemplate { tradeTablist = [new() { id = 999 }] };
            var service = new LimitedItemTradeService(); service.Start();
            Assert.False(service.IsLimitedTradeNpc(990991)); Assert.False(service.IsLimitedTradeNpc(990990));
            Assert.Null(map[990991].tradeTablist);
        }
        finally { map.Clear(); foreach (var pair in prior) map.Add(pair.Key, pair.Value); }
    }

    [Theory]
    [InlineData(TradeNpcType.NORMAL, 137)]
    [InlineData(TradeNpcType.ABYSS_KINAH, 59)]
    public void ApPriceUsesSecondaryApRateOnlyForAbyssKinah(TradeNpcType type, int expectedRate)
    {
        using var s = new Scope(); s.S.Purchase.tradeNpcType = type;
        s.S.Purchase.sellPriceRate = 137; s.S.Purchase.apSellPriceRate2 = 59;
        PricesConfig.VENDOR_BUY_MODIFIER = 100; s.S.Item(182400001, 10000, true);
        DataManager.ITEM_DATA.GetItemTemplate(162000052).acquisition = new Acquisition { Type = AcquisitionType.AP, Ap = 100 };
        var buy = new TradeList(); buy.AddItem(162000052, 1);
        Assert.False(TradeService.BuyFromShop(s.S.Npc, s.S.Player, buy));
        Assert.Equal(expectedRate, buy.GetRequiredAp()); Assert.Equal(10000, s.S.Player.GetInventory().GetKinah());
        AssertMessage(s.S.Connection, SM_SYSTEM_MESSAGE.STR_MSG_NOT_ENOUGH_ABYSSPOINT());
    }

    [Fact]
    public void TradeFlagsUseNativeNpcActionsAndAreCachedAtConstruction()
    {
        using var s = new Scope();
        s.S.Npc.GetObjectTemplate().talkInfo = new TalkInfo { FuncDialogIdsRaw = DialogAction.TRADE_SELL_LIST.ToString() };
        var sell = new SM_SELL_ITEM(s.S.Npc);
        Assert.False(Get<bool>(sell, "showBuyTab")); Assert.True(Get<bool>(sell, "showSellTab"));
        s.S.Npc.GetObjectTemplate().talkInfo = new TalkInfo { FuncDialogIdsRaw = DialogAction.BUY.ToString() };
        var trade = new SM_TRADELIST(s.S.Player, s.S.Npc, TradeNpcType.NORMAL, Array.Empty<int>(), 83);
        s.S.Npc.GetObjectTemplate().talkInfo = null!;
        Assert.True(Get<bool>(trade, "showBuyTab")); Assert.True(Get<bool>(trade, "showSellTab"));
        Assert.False(Get<bool>(sell, "showBuyTab")); Assert.True(Get<bool>(sell, "showSellTab"));
    }

    [Fact]
    public void BotsDecodeAllFourCurrentJavaTradeInPayloadsIncludingTheEmptyHeader()
    {
        using var f = JsonDocument.Parse(File.ReadAllText(Path.Combine(RealStaticData.RepoRoot(), "parity-artifacts/golden/packets/SM_TRADE_IN_LIST.638ed3dd4.json")));
        Assert.Equal(Sha, f.RootElement.GetProperty("sourceJavaCommit").GetString());
        var decoder = new Aion.Bots.Protocol.BotServerPacketDecoder();
        foreach (var c in f.RootElement.GetProperty("cases").EnumerateArray())
        {
            var bytes = Convert.FromHexString(c.GetProperty("payloadHex").GetString()!);
            var packet = decoder.Decode(typeof(SM_TRADE_IN_LIST), bytes);
            Assert.Equal(c.GetProperty("inputs").GetProperty("tabIds").EnumerateArray().Select(v => v.GetInt32()), packet.Get<int[]>("tabs"));
            for (int n = 1; n < bytes.Length; n++)
                Assert.Throws<InvalidDataException>(() => decoder.Decode(typeof(SM_TRADE_IN_LIST), bytes[..n]));
        }
    }

    private static JsonDocument Fixture()
    {
        var f = JsonDocument.Parse(File.ReadAllText(Path.Combine(RealStaticData.RepoRoot(), "parity-artifacts/golden/formulas/TradeLegion.638ed3dd4.json")));
        Assert.Equal(Sha, f.RootElement.GetProperty("sourceJavaCommit").GetString()); return f;
    }
    private static T Get<T>(object o, string name) => (T)o.GetType().GetField(name, Fields)!.GetValue(o)!;
    private static void SetId(AionObject o, int id) => typeof(AionObject).GetField("_objectId", Fields)!.SetValue(o, id);
    private static string Hex(AionServerPacket packet)
    {
        var buf = ByteBuffer.Allocate(8192).Order(ByteOrder.LITTLE_ENDIAN); packet.SetBuf(buf);
        typeof(AionServerPacket).GetMethod("WriteImpl", Fields)!.Invoke(packet, new object?[] { null });
        byte[] bytes = new byte[buf.Position()]; buf.Flip(); buf.Get(bytes); return Convert.ToHexString(bytes);
    }
    private static void AssertMessage(PvpExpPortTests.Connection connection, SM_SYSTEM_MESSAGE message) =>
        Assert.Contains(connection.Packets.OfType<SM_SYSTEM_MESSAGE>(), p => Hex(p) == Hex(message));

    private sealed class Scope : IDisposable
    {
        internal NpcShopPortTests.Scope S { get; } = new();
        private readonly Stack<Action> restore = new();
        internal Scope()
        {
            SetId(S.Npc, 700123);
            // TradeService captures its holders once; packet/dialog lookups use the current
            // DataManager installed by the harness. Supply the fixture to both boundaries.
            Put(Get<Dictionary<int, TradeListTemplate>>(DataManager.TRADE_LIST_DATA, "npctlistData"), 990991, S.Purchase);
            Put(Get<Dictionary<int, TradeListTemplate>>(DataManager.TRADE_LIST_DATA, "npcPurchaseTemplateData"), 990991, S.Purchase);
            var holder = typeof(TradeService).GetField("goodsListData", Statics)!.GetValue(null)!;
            var goods = Get<Dictionary<int, GoodsList>>(holder, "goodsListData");
            Put(goods, 101, Goods(0, 162000052, 162000053)); Put(goods, 102, Goods(3, 162000053));
            Put(goods, 103, Goods(7, 162000052)); Put(goods, 104, new GoodsList());
            bool had = goods.Remove(999, out var old); restore.Push(() => { if (had) goods[999] = old!; });
        }
        private static GoodsList Goods(int level, params int[] ids)
        {
            var g = new GoodsList { legionLevel = level }; g.AfterUnmarshal(null!); g.GetItemIdList().AddRange(ids); return g;
        }
        private void Put<T>(Dictionary<int, T> map, int id, T value)
        {
            bool had = map.TryGetValue(id, out var old); map[id] = value;
            restore.Push(() => { if (had) map[id] = old!; else map.Remove(id); });
        }
        internal void Level(int level)
        {
            if (level < 0) return;
            var legion = new Legion(987611, "PortFixture");
            // Exercise the oracle's raw level-zero boundary without invoking warehouse expansion.
            if (level == 0) typeof(Legion).GetField("legionLevel", Fields)!.SetValue(legion, level);
            else legion.SetLegionLevel(level);
            typeof(Player).GetField("legionMember", Fields)!.SetValue(S.Player, new LegionMember(S.Player.GetObjectId(), legion));
        }
        public void Dispose() { while (restore.TryPop(out var action)) action(); S.Dispose(); }
    }
}
