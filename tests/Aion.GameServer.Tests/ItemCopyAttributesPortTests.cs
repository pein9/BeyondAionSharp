using System.Collections.Concurrent;
using System.Reflection;
using System.Xml.Serialization;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Items;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.Model.Trade;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Items;
using Aion.GameServer.Taskmanager.Tasks;
using State = Aion.GameServer.Model.GameObjects.IPersistable.PersistentState;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class ItemCopyAttributesPortTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CopyKeepsFusionOwnedStonesAndChargesIncludingFusionOnlyConditioning(bool baseConditioned)
    {
        using var s = new Scope();
        if (baseConditioned) DataManager.ITEM_DATA.GetItemTemplate(100000111).improvement = new Improvement { Level = 2 };
        var source = s.Item(100000111);
        source.SetFusionedItem(DataManager.ITEM_DATA.GetItemTemplate(100000112), 0, 3);
        var stone = new ManaStone(source.GetObjectId(), 167000001, 2, State.UPDATED);
        source.GetFusionStones().Add(stone);
        source.GetConditioningInfo().UpdateChargePoints(723456);
        source.SetExpireTime(751234); source.SetActivationCount(2); source.SetColorExpireTime(123456);
        source.SetRndPlumeBonusValue(37); source.SetEnchantLevel(5); source.SetItemCreator("Crafter");
        source.SetItemColor(0x123456); source.SetOptionalSockets(2); source.SetItemLocation(1);
        var copy = s.Item(100000111);
        ItemService.CopyItemInfo(source, copy);
        Assert.Equal(751234, copy.GetExpireTime()); Assert.Equal(2, copy.GetActivationCount());
        Assert.Equal(123456, copy.GetColorExpireTime()); Assert.Equal(37, copy.GetRndPlumeBonusValue());
        Assert.Same(source.GetFusionedItemTemplate(), copy.GetFusionedItemTemplate());
        Assert.Equal(3, copy.GetFusionedItemOptionalSockets()); Assert.Equal(723456, copy.GetChargePoints());
        var copiedStone = Assert.Single(copy.GetFusionStones());
        Assert.NotSame(stone, copiedStone); Assert.Equal(copy.GetObjectId(), copiedStone.GetItemObjId());
        Assert.Equal(167000001, copiedStone.GetItemId()); Assert.Equal(2, copiedStone.GetSlot()); Assert.Equal(State.NEW, copiedStone.GetPersistentState());
        Assert.Equal(State.UPDATED, stone.GetPersistentState()); Assert.Equal(source.GetObjectId(), stone.GetItemObjId());
        Assert.Equal(5, copy.GetEnchantLevel()); Assert.Equal("Crafter", copy.GetItemCreator());
        Assert.Equal(source.GetItemColor(), copy.GetItemColor()); Assert.Equal(2, copy.GetOptionalSockets());
        Assert.Equal(0, copy.GetItemLocation()); Assert.Equal(1, source.GetItemLocation());
        Assert.NotSame(source.GetConditioningInfo(), copy.GetConditioningInfo());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-2147483648)]
    [InlineData(2147483647)]
    public void ExpirySetterPreservesSignedSecondsAndMarksLoadedItemsDirty(int expiry)
    {
        using var s = new Scope(); var item = s.Item(100000111); item.SetPersistentState(State.UPDATED);
        item.SetExpireTime(expiry);
        Assert.Equal(expiry, item.GetExpireTime()); Assert.Equal(State.UPDATE_REQUIRED, item.GetPersistentState());
        var fresh = s.Item(100000111); fresh.SetExpireTime(expiry); Assert.Equal(State.NEW, fresh.GetPersistentState());
    }

    [Fact]
    public void NewlyAllocatedStacksKeepSourceExpiryAndExistingStacksKeepTheirOwnExpiry()
    {
        using var s = new Scope(); var source = s.Item(162000052); source.SetExpireTime(751234);
        var existing = s.Item(162000052, 9, owned: true); existing.SetExpireTime(950000);
        Assert.Equal(0, ItemService.AddItem(s.Player, source, 15));
        Assert.Equal(10, existing.GetItemCount()); Assert.Equal(950000, existing.GetExpireTime());
        var added = s.Player.GetInventory().GetItemsByItemId(162000052).Where(i => i != existing).ToArray();
        Assert.Equal(new long[] {4, 10}, added.Select(i => i.GetItemCount()).Order().ToArray());
        Assert.All(added, i => Assert.Equal(751234, i.GetExpireTime()));
        Assert.Equal(1, source.GetItemCount());
    }

    [Theory]
    [InlineData(0, 751234, true)]
    [InlineData(60, 0, false)]
    public void NonstackableExpiryIsCopiedBeforeTimerRegistrationAndPredicate(int templateMinutes, int sourceExpiry, bool registered)
    {
        using var s = new Scope(); var template = DataManager.ITEM_DATA.GetItemTemplate(100000111);
        template.expireTime = templateMinutes;
        var source = s.Item(100000111); source.SetExpireTime(sourceExpiry);
        var probe = new Probe(item =>
        {
            Assert.Equal(sourceExpiry, item.GetExpireTime()); Assert.Equal(registered, s.Expirables.ContainsKey(item));
        });
        Assert.Equal(0, ItemService.AddItem(s.Player, source, 1, false, probe));
        Assert.Equal(1, probe.Calls);
        Assert.Equal(sourceExpiry, Assert.Single(s.Player.GetInventory().GetItemsByItemId(100000111)).GetExpireTime());
    }

    [Theory]
    [InlineData(3, false)]
    [InlineData(10, true)]
    public void ExchangeOffersKeepAbsoluteExpiryWhenSplittingOrReusingAStack(int count, bool reused)
    {
        using var s = new Scope(); var item = s.Item(162000052, 10, owned: true); item.SetExpireTime(751234);
        var exchange = (ExchangeService)Activator.CreateInstance(typeof(ExchangeService), true)!;
        exchange.RegisterExchange(s.Player, s.Partner); exchange.AddItem(s.Player, item.GetObjectId(), count);
        var offered = Assert.Single(exchange.GetCurrentParnterExchange(s.Partner).GetItems()).Value.GetItem();
        Assert.Equal(reused, ReferenceEquals(item, offered)); Assert.Equal(count, offered.GetItemCount());
        Assert.Equal(751234, offered.GetExpireTime()); Assert.Equal(10, item.GetItemCount());
    }

    [Theory]
    [InlineData(3, false)]
    [InlineData(10, true)]
    public void SellingAndRepurchasingKeepsOriginalExpiryInsteadOfResettingItsLifetime(int count, bool reused)
    {
        using var s = new Scope(); var item = s.Item(162000052, 10, owned: true); item.SetExpireTime(751234);
        var sale = new TradeList(); sale.AddItem(item.GetObjectId(), count);
        bool limits = Aion.GameServer.Configs.Main.CustomConfig.LIMITS_ENABLED;
        try
        {
            Aion.GameServer.Configs.Main.CustomConfig.LIMITS_ENABLED = false;
            Assert.True(TradeService.PerformSellToShop(s.Player, sale, null!, 100));
        }
        finally { Aion.GameServer.Configs.Main.CustomConfig.LIMITS_ENABLED = limits; }
        var sold = Assert.Single(RepurchaseService.GetInstance().GetRepurchaseItems(s.Player.GetObjectId()));
        Assert.Equal(reused, ReferenceEquals(item, sold)); Assert.Equal(751234, sold.GetExpireTime());
        Assert.Equal(count, sold.GetItemCount()); Assert.Equal(count * 100L, s.Player.GetInventory().GetKinah());
        var buy = new RepurchaseList(0); buy.AddRepurchaseItem(s.Player, sold.GetObjectId(), count);
        RepurchaseService.GetInstance().RepurchaseFromShop(s.Player, buy);
        Assert.Empty(RepurchaseService.GetInstance().GetRepurchaseItems(s.Player.GetObjectId()));
        Assert.Equal(0, s.Player.GetInventory().GetKinah());
        Assert.Equal(10, s.Player.GetInventory().GetItemsByItemId(162000052).Sum(i => i.GetItemCount()));
        Assert.All(s.Player.GetInventory().GetItemsByItemId(162000052), i => Assert.Equal(751234, i.GetExpireTime()));
    }

    private sealed class Probe(Action<Item> check) : ItemService.ItemUpdatePredicate
    {
        internal int Calls;
        public override bool ChangeItem(Item item) { Calls++; check(item); return true; }
    }

    private sealed class Scope : IDisposable
    {
        private static readonly FieldInfo ShutdownSlot = typeof(ShutdownHook).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic)!;
        private readonly object? previousShutdown = ShutdownSlot.GetValue(null);
        internal PvpExpPortTests.Scope Common { get; } = new();
        internal Player Player { get; }
        internal Player Partner { get; }
        private int nextId = 990000;
        internal ConcurrentDictionary<IExpirable, Player> Expirables => (ConcurrentDictionary<IExpirable, Player>)typeof(ExpireTimerTask)
            .GetField("expirables", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(ExpireTimerTask.GetInstance())!;
        internal Scope()
        {
            // Completed countdowns are intentionally retained by shutdown tests. This world is open for trading.
            ShutdownSlot.SetValue(null, null);
            using var input = new StringReader("""
                <item_templates>
                  <item_template id="100000111" name="Weapon" level="65" quality="EPIC" item_group="SWORD" mask="6"/>
                  <item_template id="100000112" name="Fusion" level="65" quality="EPIC" item_group="SWORD" mask="6"><improve level="2"/></item_template>
                  <item_template id="162000052" name="Stack" level="1" quality="COMMON" max_stack_count="10" mask="6" price="100"/>
                  <item_template id="167000001" name="Stone" level="1" quality="COMMON"/>
                  <item_template id="182400001" name="Kinah" level="1" quality="COMMON" max_stack_count="2147483647"/>
                </item_templates>
                """);
            var data = (ItemData)new XmlSerializer(typeof(ItemData)).Deserialize(input)!; data.AfterUnmarshal(null!);
            typeof(StaticData).GetField("<ItemDataDh>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(Common.H.Data.StaticData, data);
            Player = Common.Player(65); Partner = Common.Player(65);
        }
        internal Item Item(int id, int count = 1, bool owned = false)
        {
            var item = new Item(nextId++, DataManager.ITEM_DATA.GetItemTemplate(id)); item.SetItemCount(count);
            if (owned) Player.GetInventory().OnLoadHandler(item);
            return item;
        }
        public void Dispose()
        {
            RepurchaseService.GetInstance().RemoveRepurchaseItems(Player);
            RepurchaseService.GetInstance().RemoveRepurchaseItems(Partner);
            ExpireTimerTask.GetInstance().UnregisterExpirables(Player);
            ExpireTimerTask.GetInstance().UnregisterExpirables(Partner);
            Common.Dispose();
            ShutdownSlot.SetValue(null, previousShutdown);
        }
    }
}
