using System.Reflection;
using System.Runtime.CompilerServices;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Items.Storage;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.Model.Templates.Items.Purification;
using Aion.GameServer.Services.Items;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class ItemPurificationCostTests
{
    [Theory]
    [InlineData(0L)]
    [InlineData(1000L)]
    [InlineData(5_000_000_001L)]
    public void MaterialConsumptionRequestsTheExactPositiveKinahCost(long cost)
    {
        // This unit pins the service-to-storage contract; G5 must separately prove real balances/SQL.
        // Java ce54b7931 passes -cost here, but Storage.decreaseKinah ignores nonpositive amounts.
        var previous = DataManager.GetRegisteredInstance();
        try
        {
            var data = new ItemPurificationData
            {
                itemPurificationTemplates = [new ItemPurificationTemplate
                {
                    baseItemId = 100001440,
                    purificationResults = [new PurificationResult
                    {
                        resultItemId = 100001765, necessaryKinah = cost,
                        requiredMaterials = [new RequiredMaterial { itemId = 186000005, itemCount = 10 }]
                    }]
                }]
            };
            data.AfterUnmarshal(new object());
            var staticData = (StaticData)RuntimeHelpers.GetUninitializedObject(typeof(StaticData));
            typeof(StaticData).GetProperty(nameof(StaticData.ItemPurificationDataDh))!.SetValue(staticData, data);
            var constructor = typeof(DataManager).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null, [typeof(StaticData)], modifiers: null)!;
            DataManager.RegisterInstance((DataManager)constructor.Invoke([staticData]));
            var player = (Player)RuntimeHelpers.GetUninitializedObject(typeof(Player));
            var storage = new RecordingStorage(player);
            typeof(Player).GetField("inventory", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(player, storage);
            var item = new Item(123, new ItemTemplate { itemId = 100001440 });

            Assert.True(ItemPurificationService.DecreaseMaterials(player, item, 100001765));
            Assert.Equal(new[] { (186000005, 10L) }, storage.Materials);
            Assert.Equal(new[] { (123, 1L) }, storage.BaseItems);
            Assert.Equal(cost == 0 ? Array.Empty<long>() : [cost], storage.KinahCosts);
        }
        finally { DataManager.RestoreInstance(previous); }
    }

    private sealed class RecordingStorage(Player player) : PlayerStorage(player, StorageType.CUBE)
    {
        public List<long> KinahCosts { get; } = [];
        public List<(int, long)> Materials { get; } = [];
        public List<(int, long)> BaseItems { get; } = [];
        public override void DecreaseKinah(long amount) => KinahCosts.Add(amount);
        public override bool DecreaseByItemId(int itemId, long count) { Materials.Add((itemId, count)); return true; }
        public override bool DecreaseByObjectId(int objectId, long count) { BaseItems.Add((objectId, count)); return true; }
    }
}
