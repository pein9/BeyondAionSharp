using System.Reflection;
using System.Xml.Serialization;
using Aion.Bots.Protocol;
using Aion.Commons.Nio;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Items.Storage;
using Aion.GameServer.Model.Trade;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;
using Aion.GameServer.Services;
using Aion.GameServer.Taskmanager.Tasks;
using Aion.GameServer.World.Knownlist;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class PrivateStorePurchasePortTests
{
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    [Theory]
    [InlineData("missing")]
    [InlineData("short")]
    [InlineData("listed")]
    [InlineData("index")]
    [InlineData("negative")]
    public void EveryRequestedRowIsValidatedBeforeAnyItemOrMoneyMoves(string invalid)
    {
        using var s = new Scope(); var first = s.Offer(162000052, 10, 10, 7); var second = s.Offer(162000053, 10, 10, 7);
        if (invalid == "missing") s.Seller.GetInventory().Delete(second);
        if (invalid == "short") second.SetItemCount(2);
        if (invalid == "listed") s.Store.GetTradeItemByObjId(second.GetObjectId()).DecreaseCount(8);
        var list = new TradeList(); list.AddItem(0, 3); list.AddItem(invalid == "index" ? 2 : invalid == "negative" ? -1 : 1, 3);
        PrivateStoreService.SellStoreItem(s.Seller, s.Buyer, list);
        Assert.Equal(10, first.GetItemCount()); Assert.Same(first, s.Seller.GetInventory().GetItemByObjId(first.GetObjectId()));
        Assert.Equal(10, s.Store.GetTradeItemByObjId(first.GetObjectId()).GetCount());
        Assert.Equal(1000, s.Payment.GetKinah()); Assert.Equal(0, s.Seller.GetInventory().GetKinah());
        Assert.Equal(0, s.Payment.Attempts); Assert.Equal(0, s.Buyer.GetInventory().GetItemCountByItemId(162000052));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CheckedPriceRejectsProductAndSumOverflowsThatWouldWrapToCheapPositiveTotals(bool sum)
    {
        using var s = new Scope(); var list = new TradeList();
        if (sum)
        {
            s.Offer(162000052, 1, 1, long.MaxValue); s.Offer(162000053, 1, 1, long.MaxValue); s.Offer(162000052, 1, 1, 10);
            list.AddItem(0, 1); list.AddItem(1, 1); list.AddItem(2, 1); // unchecked total would be 8
        }
        else
        {
            s.Offer(162000052, 4, 4, long.MaxValue / 2 + 2); list.AddItem(0, 4); // unchecked product would be 4
        }
        var counts = s.Store.GetSoldItems().Values.Select(i => i.GetCount()).ToArray();
        PrivateStoreService.SellStoreItem(s.Seller, s.Buyer, list);
        Assert.Equal(counts, s.Store.GetSoldItems().Values.Select(i => i.GetCount()).ToArray());
        Assert.Equal(1000, s.Payment.GetKinah()); Assert.Equal(0, s.Payment.Attempts);
        Assert.Equal(0, s.Buyer.GetInventory().GetItemCountByItemId(162000052)); Assert.Equal(0, s.Seller.GetInventory().GetKinah());
    }

    [Fact]
    public void PaymentRefusalCannotTransferItemsEvenWhenTheDisplayedBalanceWasEnough()
    {
        using var s = new Scope(); var item = s.Offer(162000052, 10, 10, 7); s.Payment.Allow = false;
        s.Payment.Before = () => { Assert.Equal(10, item.GetItemCount()); Assert.Equal(0, s.Buyer.GetInventory().GetItemCountByItemId(162000052)); };
        s.Buy((0, 3));
        Assert.Equal(1, s.Payment.Attempts); Assert.Equal(21, s.Payment.LastAmount);
        Assert.Equal(10, item.GetItemCount()); Assert.Equal(1000, s.Payment.GetKinah());
        Assert.Equal(0, s.Seller.GetInventory().GetKinah()); Assert.Equal(0, s.Buyer.GetInventory().GetItemCountByItemId(162000052));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SuccessfulPartialPurchasePaysFirstAndPreservesNewStackExpiryOrExistingStackExpiry(bool existing)
    {
        using var s = new Scope(); var source = s.Offer(162000052, 10, 10, 7); source.SetExpireTime(123456);
        if (existing) { var stack = s.Item(s.Buyer, 162000052, 1); stack.SetExpireTime(999999); }
        s.Payment.Before = () =>
        {
            Assert.Equal(10, source.GetItemCount()); Assert.Equal(10, s.Store.GetTradeItemByObjId(source.GetObjectId()).GetCount());
            Assert.Equal(existing ? 1 : 0, s.Buyer.GetInventory().GetItemCountByItemId(162000052)); Assert.Equal(0, s.Seller.GetInventory().GetKinah());
        };
        s.Buy((0, 3));
        Assert.Equal(1, s.Payment.Attempts); Assert.Equal(979, s.Payment.GetKinah()); Assert.Equal(21, s.Seller.GetInventory().GetKinah());
        Assert.Equal(7, source.GetItemCount()); Assert.Equal(7, s.Store.GetTradeItemByObjId(source.GetObjectId()).GetCount());
        var bought = Assert.Single(s.Buyer.GetInventory().GetItemsByItemId(162000052));
        Assert.Equal(existing ? 4 : 3, bought.GetItemCount()); Assert.Equal(existing ? 999999 : 123456, bought.GetExpireTime()); Assert.Same(s.Store, s.Seller.GetStore());
    }

    [Fact]
    public void WholeWeaponPurchaseKeepsAttributesAndClosesTheEmptyStore()
    {
        using var s = new Scope(); var source = s.Offer(100000111, 1, 1, 50);
        source.SetExpireTime(153000); source.SetActivationCount(2); source.SetColorExpireTime(123456);
        source.SetRndPlumeBonusValue(37); source.GetConditioningInfo().UpdateChargePoints(723456);
        source.SetEnchantLevel(5); source.SetItemCreator("Crafter"); source.SetItemColor(0x123456);
        s.Buy((0, 1));
        var copy = Assert.Single(s.Buyer.GetInventory().GetItemsByItemId(100000111));
        Assert.NotSame(source, copy); Assert.Equal(153000, copy.GetExpireTime()); Assert.Equal(2, copy.GetActivationCount());
        Assert.Equal(123456, copy.GetColorExpireTime()); Assert.Equal(37, copy.GetRndPlumeBonusValue()); Assert.Equal(723456, copy.GetChargePoints());
        Assert.Equal(5, copy.GetEnchantLevel()); Assert.Equal("Crafter", copy.GetItemCreator()); Assert.Equal(source.GetItemColor(), copy.GetItemColor());
        Assert.Equal(950, s.Payment.GetKinah()); Assert.Equal(50, s.Seller.GetInventory().GetKinah());
        Assert.Null(s.Seller.GetInventory().GetItemByObjId(source.GetObjectId())); Assert.Null(s.Seller.GetStore());
    }

    [Fact]
    public void StoreReferenceIsRetainedThroughPaymentAndItemDecrement()
    {
        using var s = new Scope(); var item = s.Offer(162000052, 10, 10, 7);
        s.Payment.Before = () => s.Seller.SetStore(null!);
        s.Buy((0, 3));
        Assert.Equal(1, s.Payment.Attempts); Assert.Equal(7, item.GetItemCount());
        Assert.Equal(7, s.Store.GetTradeItemByObjId(item.GetObjectId()).GetCount()); Assert.Null(s.Seller.GetStore());
        Assert.Equal(3, s.Buyer.GetInventory().GetItemCountByItemId(162000052)); Assert.Equal(979, s.Payment.GetKinah()); Assert.Equal(21, s.Seller.GetInventory().GetKinah());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RepeatedAndNegativeStoreIndexesCannotReachSettlement(bool negative)
    {
        using var s = new Scope(); var item = s.Offer(162000052, 10, 10, 7);
        s.Buyer.SetKnownlist(new SellerKnownList(s.Buyer, s.Seller));
        var body = GameClientPackets.BuyItem(s.Seller.GetObjectId(), 0, negative ? [(-1, 3L)] : [(0, 3L), (0, 3L)]).Body.ToArray();
        var packet = new CM_BUY_ITEM(0, new HashSet<AionConnection.State> { AionConnection.State.IN_GAME });
        packet.SetConnection(s.Buyer.GetClientConnection()); packet.SetBuffer(ByteBuffer.Wrap(body).Order(ByteOrder.LITTLE_ENDIAN)); Assert.True(packet.Read()); packet.Run();
        Assert.True((bool)typeof(CM_BUY_ITEM).GetField("isAudit", Fields)!.GetValue(packet)!);
        Assert.Equal(10, item.GetItemCount()); Assert.Equal(0, s.Payment.Attempts); Assert.Equal(1000, s.Payment.GetKinah());
        Assert.Equal(0, s.Buyer.GetInventory().GetItemCountByItemId(162000052));
    }

    [Theory]
    [InlineData("closed")]
    [InlineData("full")]
    [InlineData("race")]
    public void ExistingStoreInventoryAndFactionPreconditionsStillRefuseWithoutPayment(string reason)
    {
        using var s = new Scope(); var item = s.Offer(162000052, 10, 10, 7);
        if (reason == "closed") s.Seller.SetStore(null!);
        if (reason == "full") s.Payment.SetLimit(0);
        if (reason == "race") s.Buyer.GetCommonData().SetRace(Race.ASMODIANS);
        s.Buy((0, 3));
        Assert.Equal(10, item.GetItemCount()); Assert.Equal(0, s.Payment.Attempts); Assert.Equal(1000, s.Payment.GetKinah());
        Assert.Equal(0, s.Buyer.GetInventory().GetItemCountByItemId(162000052));
    }

    private sealed class SellerKnownList : KnownList
    {
        internal SellerKnownList(Player buyer, Player seller) : base(buyer) => KnownObjects[seller.ObjectId] = new KnownObject(seller);
    }
    private sealed class PaymentStorage(Player owner) : PlayerStorage(owner, StorageType.CUBE)
    {
        internal bool Allow = true;
        internal Action? Before;
        internal int Attempts;
        internal long LastAmount;
        public override bool TryDecreaseKinah(long amount)
        {
            Attempts++; LastAmount = amount; Before?.Invoke(); return Allow && base.TryDecreaseKinah(amount);
        }
    }
    private sealed class Scope : IDisposable
    {
        internal PvpExpPortTests.Scope Common { get; } = new();
        internal Player Seller { get; }
        internal Player Buyer { get; }
        internal PrivateStore Store { get; }
        internal PaymentStorage Payment { get; }
        private int nextId = 991000;
        private readonly bool priorPunishment = PunishmentConfig.PUNISHMENT_ENABLE;
        internal Scope()
        {
            PunishmentConfig.PUNISHMENT_ENABLE = false;
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
            Seller = Common.Player(65); Buyer = Common.Player(65); Seller.GetInventory().SetLimit(10);
            Payment = new PaymentStorage(Buyer); Payment.SetLimit(10); typeof(Player).GetField("inventory", Fields)!.SetValue(Buyer, Payment);
            Item(Buyer, 182400001, 1000); Store = new PrivateStore(Seller); Seller.SetStore(Store);
        }
        internal Item Item(Player owner, int id, int count)
        {
            var item = new Item(nextId++, DataManager.ITEM_DATA.GetItemTemplate(id)); item.SetItemCount(count); owner.GetInventory().OnLoadHandler(item); return item;
        }
        internal Item Offer(int id, int owned, int listed, long price)
        {
            var item = Item(Seller, id, owned); Store.AddItemToSell(item.GetObjectId(), new TradePSItem(item.GetObjectId(), id, listed, price)); return item;
        }
        internal void Buy(params (int Index, long Count)[] rows)
        {
            var list = new TradeList(); foreach (var row in rows) list.AddItem(row.Index, row.Count); PrivateStoreService.SellStoreItem(Seller, Buyer, list);
        }
        public void Dispose()
        {
            ExpireTimerTask.GetInstance().UnregisterExpirables(Seller); ExpireTimerTask.GetInstance().UnregisterExpirables(Buyer);
            PunishmentConfig.PUNISHMENT_ENABLE = priorPunishment; Common.Dispose();
        }
    }
}
