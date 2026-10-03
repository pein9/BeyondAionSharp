using System.Text.Json;
using System.Xml.Linq;
using Aion.Bots.Scenarios;
using Aion.Bots.World;
using Aion.GameServer.TestKit;

namespace Aion.GameServer.Tests;

public sealed class NaturalAltgardCoinGearContractTests
{
	private static readonly NaturalAltgardContract Leg = NaturalAltgardContract.LoadLeg("cg");
	private static readonly NaturalCoinGear Gear = Leg.CoinGear!;
	private static readonly IReadOnlyDictionary<int, NaturalTemplateObjective> Objectives = NaturalTemplateObjective.From(NaturalAltgardContract.LoadPlans("cg"));

	[Fact]
	public void ContractPinsTheIncomingJournalApprovedArmourAndTwoIndependentCounters()
	{
		Assert.Equal(("altgard-l11", 24, 700065, 144), (Leg.Start.Snapshot, Leg.Start.Level, Leg.Start.BindNpcId, Leg.Start.CompletedQuestIds.Length));
		NaturalAltgardContract prior = NaturalAltgardContract.LoadLeg("l11");
		Assert.All(prior.Start.CompletedQuestIds.Concat(prior.Order), id => Assert.Contains(id, Leg.Start.CompletedQuestIds));
		Assert.Equal([2293], Leg.Order);
		Assert.Equal(("altgard-coingear", 700067), (Leg.Endpoint.Snapshot, Leg.Endpoint.BindNpcId));
		Assert.Equal(4, Gear.Cost);
		Assert.Equal([new NaturalTemplateKill(0, 6), new NaturalTemplateKill(1, 16)], Objectives[2293].Kills!);
		Assert.False(Objectives[2293].IsDone(new(2293, 3, 6 | 15 << 6, 0, null), new Dictionary<int, long>()));
		Assert.False(Objectives[2293].IsDone(new(2293, 3, 5 | 16 << 6, 0, null), new Dictionary<int, long>()));
		Assert.True(Objectives[2293].IsDone(new(2293, 3, 6 | 16 << 6, 0, null), new Dictionary<int, long>()));
		Assert.Contains(203689, Leg.GraphNpcIds(NaturalAltgardContract.LoadPlans("cg")));
		Assert.Equal("template-accept", Decide(Incoming()).Action);
		Assert.Throws<InvalidDataException>(() => (Gear with { Purchases = [new(100100785, 3, 1)] }).Validate(Leg));
	}

	[Fact]
	public void ManifestMatchesRewardAcquisitionsAndAllSevenRegionalOffers()
	{
		string data = Path.Combine(RealStaticData.RepoRoot(), "game-server/data/static_data");
		XElement items = XDocument.Load(Path.Combine(data, "items/item_templates.xml")).Root!;
		foreach (NaturalCoinGearPurchase p in Gear.Purchases)
		{
			XElement item = items.Elements().Single(i => (int?)i.Attribute("id") == p.ItemId);
			Assert.Equal("RARE", (string?)item.Attribute("quality"));
			Assert.Equal(16, (int?)item.Attribute("level"));
			XElement acquisition = item.Element("acquisition")!;
			Assert.Equal(("REWARD", Gear.CoinItemId, p.Cost), ((string)acquisition.Attribute("type")!,
				(int)acquisition.Attribute("item")!, (int)acquisition.Attribute("count")!));
		}
		string[] catalogues = Directory.GetFiles(Path.Combine(data, "goodslists"), "goodslists*.xml");
		Assert.Equal(7, catalogues.Length);
		foreach (string path in catalogues)
		{
			XElement tab = XDocument.Load(path).Descendants("list").Single(l => (int?)l.Attribute("id") == Gear.GoodsListId);
			Assert.All(Gear.Purchases, p => Assert.Contains(tab.Elements("item"), i => (int?)i.Attribute("id") == p.ItemId));
		}
	}

	[Fact]
	public void CompletionMembershipCannotTriggerAnotherRepeatOrShoppingWithoutItsCountAndReceipt()
	{
		NaturalAltgardObservation done = Rewarded();
		Assert.Equal("coin-receipts-missing", Decide(done).Action);
		Assert.Equal("coin-repeat-count", Decide(done with { CompletedQuestCounts = new Dictionary<int, byte>() }).Action);
		Assert.Equal("coin-repeat-count", Decide(done with { CompletedQuestCounts = new Dictionary<int, byte> { [2293] = 2 } }).Action);
		Assert.Equal("coin-repeat-count", Decide(done with { Quests = new Dictionary<int, BotQuestState> { [2293] = new(2293, 3, 0, 1, null) } }).Action);
		NaturalCoinGearProgress progress = NaturalCoinGearProgress.Empty.ObserveReward(Gear, Incoming(), done);
		Assert.Equal("coin-purchase", Decide(done with { CoinGearProgress = progress }).Action);
		Assert.Throws<InvalidDataException>(() => progress.ObserveReward(Gear, Incoming(), done));
		Assert.Throws<InvalidDataException>(() => NaturalCoinGearProgress.Empty.ObserveReward(Gear, Incoming(), done with { ItemCounts = new Dictionary<int, long>(done.ItemCounts) { [Gear.CoinItemId] = 28 } }));
	}

	[Fact]
	public void ExactReceiptsDrivePurchasesAndEquipmentAndSurviveSerialization()
	{
		NaturalAltgardObservation state = Rewarded();
		NaturalCoinGearProgress progress = NaturalCoinGearProgress.Empty.ObserveReward(Gear, Incoming(), state);
		foreach (NaturalCoinGearPurchase item in Gear.Purchases)
		{
			state = state with { CoinGearProgress = progress };
			Assert.Equal(("coin-purchase", item.ItemId.ToString()), (Decide(state).Action, Decide(state).StepKey));
			NaturalAltgardObservation bought = Purchased(state, item);
			Assert.Throws<InvalidDataException>(() => progress.ObservePurchase(Gear, item.ItemId, state, bought with { Kinah = bought.Kinah - 1 }));
			progress = progress.ObservePurchase(Gear, item.ItemId, state, bought);
			state = bought with { CoinGearProgress = progress };
			Assert.Equal("coin-equip", Decide(state).Action);
			state = state with { Inventory = state.Inventory!.Select(i => i.ItemId == item.ItemId ? i with { EquipmentSlot = item.Slot }
				: i.EquipmentSlot == item.Slot ? i with { EquipmentSlot = 0 } : i).ToArray() };
		}
		Assert.Equal(19, state.ItemCounts[Gear.CoinItemId]);
		Assert.Equal("leg-complete", Decide(state).Action);
		NaturalCoinGearProgress restored = JsonSerializer.Deserialize<NaturalCoinGearProgress>(JsonSerializer.Serialize(progress))!;
		Assert.Equal("leg-complete", Decide(state with { CoinGearProgress = restored }).Action);
		Assert.Equal("coin-staff-changed", Decide(state with { Inventory = state.Inventory!.Select(i => i.ItemId == Gear.StaffItemId ? i with { ObjectId = 999 } : i).ToArray() }).Action);
		Assert.Equal("coin-journal-changed", Decide(state with { CompletedQuestIds = state.CompletedQuestIds.Where(id => id != 2900).ToHashSet() }).Action);
		Assert.Equal("coin-stigma-protection", Decide(state with { SkillIds = new HashSet<int> { 11504 } }).Action);
	}

	[Fact]
	public void UnrecordedOwnedArmourAndMissingCubeSpaceCannotCauseDuplicateSpending()
	{
		NaturalAltgardObservation state = Rewarded();
		NaturalCoinGearProgress progress = NaturalCoinGearProgress.Empty.ObserveReward(Gear, Incoming(), state);
		state = state with { CoinGearProgress = progress };
		Assert.Equal("town-service", Decide(state with { FreeCubeSlots = 0 }).Action);
		Assert.Equal("coin-cube-space", NaturalCoinGearPolicy.Decide(Gear, state with { FreeCubeSlots = 0 }).Action);
		Assert.Equal("coin-cube-space", Decide(state with { FreeCubeSlots = null }).Action);
		Assert.Equal("coin-balance-changed", Decide(state with { ItemCounts = new Dictionary<int, long>(state.ItemCounts) { [Gear.CoinItemId] = 1 } }).Action);
		NaturalAltgardObservation bought = Purchased(state, Gear.Purchases[0]);
		Assert.Equal("coin-balance-changed", Decide(bought).Action);
		Assert.Equal("coin-unrecorded-purchase", Decide(bought with { ItemCounts = new Dictionary<int, long>(bought.ItemCounts) { [Gear.CoinItemId] = 23 } }).Action);
	}

	[Fact]
	public void TheActiveInventoryPolicyProtectsBothCurrenciesArmourAndEveryWeaponFromAutomaticSwaps()
	{
		int[] ids = Gear.ProtectedItemIds.Concat([101500810, 100100785, 115001118]).Distinct().ToArray();
		NaturalIshalgenInventoryPolicy policy = NaturalIshalgenInventoryPolicy.Load(RealStaticData.RepoRoot(), ids);
		BotInventoryItem[] bag = ids.Select((id, i) => new BotInventoryItem(i + 1, id, "item", 1, 4, "", 0, false)
			{ Details = new BotItemDetails(EquippedSlot: 0) }).ToArray();
		NaturalInventoryPlan plan = policy.Decide(bag, 24, 63, cleric: true, coinGear: Gear);
		Assert.Empty(plan.Sales);
		Assert.Empty(plan.Equips);
	}

	[Fact]
	public void RelogRejectsChangedRepeatCountsEvenWhenCompletedIdsStillMatch()
	{
		var checkpoint = new NaturalJourneyCheckpoint(42, 1, 220030000, new(2656, 1660, 325, 0), 24, 1, 1, 1, 1, false,
			[], [2293], [], [], new(1, "journey-complete", null, "complete", "done", [], []),
			CompletedQuests: [new(2293, 1)]);
		NaturalJourneyPersistence.Verify(checkpoint, checkpoint with { ConnectionGeneration = 2 });
		Assert.Throws<InvalidDataException>(() => NaturalJourneyPersistence.Verify(checkpoint,
			checkpoint with { ConnectionGeneration = 2, CompletedQuests = [new(2293, 2)] }));
		Assert.Throws<InvalidDataException>(() => NaturalJourneyPersistence.Verify(checkpoint,
			checkpoint with { ConnectionGeneration = 2, CompletedQuests = null }));
	}

	private static NaturalAltgardDecision Decide(NaturalAltgardObservation state) => NaturalAltgardDecisionEngine.Decide(Leg, state, Objectives, 1);
	private static NaturalAltgardObservation Incoming()
	{
		float[] at = Leg.Bind!.Position;
		NaturalJourneyItem[] inventory = [new(137763, Gear.StaffItemId, 1, 3), new(1, 110551139, 1, 8),
			new(2, 114501726, 1, 32), new(3, 111101650, 1, 16), new(4, 113100773, 1, 4096),
			new(5, Gear.CoinItemId, 18, 65535), new(6, Gear.SealedBundleId, 1, 65535)];
		var position = new BotPosition(at[0], at[1], at[2], 0);
		return new(true, 220030000, 24, false, new Dictionary<int, BotQuestState>(), Leg.Start.CompletedQuestIds.ToHashSet(),
			position, inventory.ToDictionary(i => i.ItemId, i => i.Count), new(220030000, position, 0),
			FreeCubeSlots: 30, Kinah: 536193, SkillIds: new HashSet<int> { 1842 }, CompletedQuestCounts: new Dictionary<int, byte>(), Inventory: inventory);
	}
	private static NaturalAltgardObservation Rewarded()
	{
		NaturalAltgardObservation state = Incoming();
		return state with { CompletedQuestIds = state.CompletedQuestIds.Append(2293).ToHashSet(),
			CompletedQuestCounts = new Dictionary<int, byte> { [2293] = 1 },
			ItemCounts = new Dictionary<int, long>(state.ItemCounts) { [Gear.CoinItemId] = 23 },
			Inventory = state.Inventory!.Select(i => i.ItemId == Gear.CoinItemId ? i with { Count = 23 } : i).ToArray() };
	}
	private static NaturalAltgardObservation Purchased(NaturalAltgardObservation state, NaturalCoinGearPurchase purchase) => state with
	{
		ItemCounts = new Dictionary<int, long>(state.ItemCounts) { [Gear.CoinItemId] = state.ItemCounts[Gear.CoinItemId] - purchase.Cost, [purchase.ItemId] = 1 },
		Inventory = [.. state.Inventory!.Select(i => i.ItemId == Gear.CoinItemId ? i with { Count = i.Count - purchase.Cost } : i), new(purchase.ItemId, purchase.ItemId, 1, 0)],
	};
}
