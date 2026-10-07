using Aion.Bots.Scenarios;
using Aion.Bots.World;

namespace Aion.GameServer.Tests;

/// <summary>
/// CP-06: below level 10 the bot uses the level 1-9 kit of CP-05's manifest: the help policy picks its scrolls by the
/// restrict gate, the potion policy drinks its potion first, and the Priest's keep rule holds it. From level 10 on
/// every pick is what it was.
/// </summary>
public sealed class NaturalStarterHelpKitUseTests
{
	private const int LesserShield = 164000067, Shield = 164000068, MajorShield = 164000070;
	private const int LesserRunning = 164000074, Running = 164000075, GreaterRunning = 164000076;
	private const int LesserAwakening = 164000132, Castafodin = 164002118, Accelerox = 164002116, Blitzopan = 164002117;
	private const int MinorLifePotion = 162000002, LifePotion = 162000004, MajorLifePotion = 162000006, MinorManaPotion = 162000007;
	private const int Bandage = 169300002, VendorElixir = 162000052;
	private static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch.AddHours(1);

	[Theory]
	[InlineData(1)]
	[InlineData(5)]
	[InlineData(9)]
	public void BelowLevelTenTheShieldIsTheManifestsScrollWhateverItsItemLevel(int level)
	{
		Assert.Equal(LesserShield, NaturalHelpItemPolicy.DecideShield(State(level, [LesserShield, Shield, MajorShield], hp: 50), Now).Item?.ItemId);
		// A stronger tier that is not in the manifest is not picked below level 10, and neither is used above 50% HP.
		Assert.Null(NaturalHelpItemPolicy.DecideShield(State(level, [Shield, MajorShield], hp: 50), Now).Item);
		Assert.Null(NaturalHelpItemPolicy.DecideShield(State(level, [LesserShield], hp: 51), Now).Item);
	}

	[Theory]
	[InlineData(1)]
	[InlineData(9)]
	public void BelowLevelTenTheScrollUpkeepIsTheManifests(int level)
	{
		int[] kit = [GreaterRunning, Accelerox, Castafodin, Blitzopan, LesserShield];
		// Casting speed first, from the owned event scroll; never the attack-speed scroll in the same slot.
		Assert.Equal(Castafodin, Buffs(State(level, kit), NaturalHelpTrigger.PrePull).Item?.ItemId);
		Assert.Null(Buffs(State(level, [Blitzopan, LesserAwakening]), NaturalHelpTrigger.PrePull).Item);
		// With casting speed up: the Greater Running Scroll before a long leg, and only then.
		NaturalHelpItemObservation buffed = State(level, kit) with { Effects = [new BotVisibleEffect(1, 10467, 3, 0, 1_700_000)] };
		Assert.Equal(GreaterRunning, Buffs(buffed, NaturalHelpTrigger.TravelLeg, 150).Item?.ItemId);
		Assert.Null(Buffs(buffed, NaturalHelpTrigger.TravelLeg, 149).Item);
		Assert.Null(Buffs(buffed, NaturalHelpTrigger.PrePull, 500).Item);
		// Accelerox is the Running scroll when no Greater Running Scroll is owned; other tiers are not in the manifest.
		Assert.Equal(Accelerox, Buffs(buffed with { ItemCounts = Owned(Accelerox, LesserRunning, Running) }, NaturalHelpTrigger.TravelLeg, 150).Item?.ItemId);
		Assert.Null(Buffs(buffed with { ItemCounts = Owned(LesserRunning, Running) }, NaturalHelpTrigger.TravelLeg, 150).Item);
		// A speed effect that is running is not replaced.
		Assert.Null(Buffs(buffed with { Effects = [.. buffed.Effects!, new BotVisibleEffect(1, 9960, 3, 0, 200_000)] }, NaturalHelpTrigger.TravelLeg, 150).Item);
	}

	[Fact]
	public void FromLevelTenOnThePicksAreWhatTheyWere()
	{
		// The tier rule: the Lesser Anti-Shock at 10, the plain one from 20; a stronger tier waits for its level.
		Assert.Equal(LesserShield, NaturalHelpItemPolicy.DecideShield(State(10, [LesserShield, Shield, MajorShield], hp: 50), Now).Item?.ItemId);
		Assert.Equal(Shield, NaturalHelpItemPolicy.DecideShield(State(20, [LesserShield, Shield, MajorShield], hp: 50), Now).Item?.ItemId);
		// What is left of the Greater Running Scrolls waits for level 30; Accelerox is what a level 10 character runs on.
		NaturalHelpItemObservation ten = State(10, [GreaterRunning, Accelerox, Castafodin]) with { Effects = [new BotVisibleEffect(1, 10467, 3, 0, 1_700_000)] };
		Assert.Equal(Accelerox, Buffs(ten, NaturalHelpTrigger.TravelLeg, 150).Item?.ItemId);
		Assert.Null(Buffs(ten with { ItemCounts = Owned(GreaterRunning) }, NaturalHelpTrigger.TravelLeg, 150).Item);
		Assert.Equal(GreaterRunning, Buffs(ten with { Level = 30, ItemCounts = Owned(GreaterRunning) }, NaturalHelpTrigger.TravelLeg, 150).Item?.ItemId);
	}

	[Fact]
	public void TheStarterScrollsAreTheKitsSuppliedScrollsAndTheThreeEventScrolls()
	{
		Assert.Equal(new[] { LesserShield, GreaterRunning, Accelerox, Blitzopan, Castafodin }, NaturalHelpItemPolicy.StarterScrollIds);
		// Every supplied scroll of the kit is one the policy picks, and every starter scroll is in the catalog.
		Assert.All(NaturalHelpItemAllowlist.Starter.Where(supply => supply.Family != "life-potion"),
			supply => Assert.Contains(supply.ItemId, NaturalHelpItemPolicy.StarterScrollIds));
		Assert.All(NaturalHelpItemPolicy.StarterScrollIds, id => Assert.Single(NaturalHelpItemPolicy.All, item => item.ItemId == id));
		Assert.Equal(9, NaturalHelpItemAllowlist.StarterMaxLevel);
	}

	[Fact]
	public void TheKitsPotionIsDrunkFirstAndCountsAsHealingStock()
	{
		Assert.Equal(MajorLifePotion, NaturalIshalgenPotionPolicy.SelectOwnedPotion(
			[Item(1, MinorLifePotion), Item(2, MajorLifePotion), Item(3, LifePotion), Item(4, VendorElixir)])?.ItemId);
		// Without it the order is what it was: the starter potion before the bought elixir.
		Assert.Equal(MinorLifePotion, NaturalIshalgenPotionPolicy.SelectOwnedPotion([Item(1, VendorElixir), Item(2, MinorLifePotion)])?.ItemId);
		Assert.Equal(15, NaturalIshalgenPotionPolicy.TotalHealingCount([Item(1, MinorLifePotion), Item(2, MajorLifePotion), Item(3, VendorElixir)]));
		Assert.False(NaturalIshalgenPotionPolicy.NeedsRestock([Item(1, MajorLifePotion, 6)]));
		// Its heal over time is seen as active healing, so a second potion is not drunk on top of it.
		Assert.True(NaturalIshalgenPotionPolicy.HasActiveHealing([new BotVisibleEffect(1, 9893, 1, 0, 10_000)]));
		// The starter's own mana potion is the Priest's mana potion.
		Assert.Equal(MinorManaPotion, NaturalIshalgenPotionPolicy.SelectOwnedManaPotion([Item(1, MinorManaPotion)])?.ItemId);
	}

	[Fact]
	public void ThePriestHoldsTheKitAtAVendorAndTheClericHoldsWhatIsLeftOfIt()
	{
		string root = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ScenarioManifest.FindDefaultPath())!, "../.."));
		int[] kit = [MajorLifePotion, LesserShield, GreaterRunning, Accelerox, Castafodin, Blitzopan, MinorLifePotion, MinorManaPotion];
		NaturalIshalgenInventoryPolicy policy = NaturalIshalgenInventoryPolicy.Load(root, [.. kit, Bandage]);
		BotInventoryItem[] bag = [.. kit.Select((id, index) => Item(index + 1, id, 20)), Item(20, Bandage, 20)];
		foreach (int level in new[] { 1, 9 })
		{
			NaturalInventoryPlan priest = policy.Decide(bag, level, 27);
			Assert.All(priest.Decisions.Where(decision => decision.ObjectId != 20), decision => Assert.Equal("hold", decision.Action));
			// The supplied items are sellable by their mask; only the supply rule holds them.
			Assert.All(priest.Decisions.Where(decision => decision.ItemId is MajorLifePotion or LesserShield or GreaterRunning),
				decision => Assert.Equal("combat-supply", decision.Reason));
			// The bandages are not a supply: no class uses one (CP-Q11).
			Assert.Equal(new[] { 20 }, priest.Sales.Select(decision => decision.ObjectId));
		}
		NaturalInventoryPlan cleric = policy.Decide(bag, 10, 27, cleric: true);
		Assert.Equal(new[] { 20 }, cleric.Sales.Select(decision => decision.ObjectId));
		Assert.Equal("combat-supply", cleric.Decisions.Single(decision => decision.ItemId == MajorLifePotion).Reason);
	}

	private static NaturalHelpItemChoice Buffs(NaturalHelpItemObservation state, NaturalHelpTrigger trigger, float meters = 0) =>
		NaturalHelpItemPolicy.DecideBuffs(state with { PlannedTravelMeters = meters }, Now, trigger);

	private static NaturalHelpItemObservation State(int level, int[] owned, int hp = 100) =>
		new(level, hp * 13, 1300, false, false, false, false, false, [], 0, Owned(owned), new Dictionary<int, DateTimeOffset>());

	private static Dictionary<int, long> Owned(params int[] ids) => ids.ToDictionary(id => id, _ => 5L);

	private static BotInventoryItem Item(int objectId, int itemId, long count = 5) =>
		new(objectId, itemId, "item", count, 4, "", 0, false) { Details = new BotItemDetails(EquippedSlot: 0) };
}
