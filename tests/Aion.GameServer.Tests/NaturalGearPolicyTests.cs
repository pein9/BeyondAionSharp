using Aion.Bots.Scenarios;
using Aion.Bots.Scenarios.Classes;
using Aion.Bots.World;

namespace Aion.GameServer.Tests;

public sealed class NaturalGearPolicyTests
{
	private const long Main = 1, Sub = 2, Torso = 8, Boots = 32, RingLeft = 256, RingRight = 512, Pants = 4096;

	// The recorded human session at Nalto (2026-09-24): the bot's bag after Q2006, level 9.
	private static readonly Dictionary<int, NaturalGearInfo> Items = new()
	{
		[100100011] = new(Main | Sub, 1, 1, true, "MACE", 80),      // Training Mace (worn)
		[110300292] = new(Torso, 1, 1, true, "LT_TORSO"),           // Training Leather Armor (worn)
		[113300278] = new(Pants, 1, 1, true, "LT_PANTS"),           // Training Leather Leg Armor (worn)
		[100100024] = new(Main | Sub, 3, 3, true, "MACE", 100),     // Raider's Mace
		[100200604] = new(Main | Sub, -1, 5, true, "DAGGER"),       // Ulgorn's Dagger: not a Priest weapon
		[114100794] = new(Boots, 4, 4, true, "RB_SHOES"),           // Boromer's Shoes
		[114100795] = new(Boots, 8, 8, true, "RB_SHOES"),           // Anturoon Shoes
		[113100773] = new(Pants, 8, 8, true, "RB_PANTS"),           // Anturoon Leggings
		[122000869] = new(RingLeft | RingRight, 7, 7, true, "RING"), // Spirit Ring
		[182400001] = null!,                             // kinah: not gear
		// AX-04, the staff rule: the Cleric's staffs with their magic boost, and the hand items that must not replace one.
		[101500498] = new(Main | Sub, 10, 10, true, "STAFF", 260),  // Karmic Staff
		[101501357] = new(Main | Sub, 21, 21, true, "STAFF", 370),  // Altgard Dark Legionary Staff
		[101501224] = new(Main | Sub, 25, 25, true, "STAFF", 460),  // Altruist's Staff
		[101500812] = new(Main | Sub, 26, 26, true, "STAFF", 420),  // Rank 7 Asmodian Staff
		[100101199] = new(Main | Sub, 25, 25, true, "MACE", 286),   // Altruist's Mace
		[115001119] = new(Sub, 21, 21, true, "SHIELD"),             // Rank 8 Asmodian Scale Shield
		[101500001] = new(Main | Sub, -1, 12, true, "STAFF", 300),  // a staff this class cannot wear
	};

	private static NaturalGearInfo? Describe(int itemId) => Items.TryGetValue(itemId, out var info) ? info : null;

	private static BotInventoryItem Worn(int objectId, int itemId, long slot) =>
		new(objectId, itemId, "", 1, 0, "", (ushort)slot, false) { Details = BotItemDetails.Empty with { EquippedSlot = slot } };

	private static BotInventoryItem Bag(int objectId, int itemId) =>
		new(objectId, itemId, "", 1, 0, "", 0, false) { Details = BotItemDetails.Empty with { EquippedSlot = 0 } };

	[Fact]
	public void WearsWhatTheHumanPutOnAndNothingElse()
	{
		BotInventoryItem[] inventory =
		[
			Worn(2, 100100011, Main), Worn(3, 110300292, Torso), Worn(4, 113300278, Pants),
			Bag(1, 182400001), Bag(14, 100100024), Bag(16, 100200604), Bag(15, 114100794), Bag(20, 114100795),
			Bag(19, 113100773), Bag(18, 122000869),
		];
		IReadOnlyList<NaturalGearUpgrade> upgrades = NaturalGearPolicy.SelectUpgrades(inventory, 9, Describe, offHandSlots: 0);
		// The human's four equips: mace, ring, the better shoes, the leggings.
		Assert.Equal([(14, Main), (18, RingLeft), (19, Pants), (20, Boots)],
			upgrades.Select(u => (u.ObjectId, u.Slot)).OrderBy(u => u.ObjectId).ToArray());
		Assert.Equal(1, upgrades.Single(u => u.ObjectId == 14).ReplacesItemLevel);
		Assert.Null(upgrades.Single(u => u.ObjectId == 20).ReplacesItemLevel);
	}

	[Fact]
	public void RespectsLevelRefusalsAndNeverSidegrades()
	{
		BotInventoryItem[] inventory = [Worn(2, 114100794, Boots), Bag(20, 114100795), Bag(21, 114100794)];
		Assert.Empty(NaturalGearPolicy.SelectUpgrades(inventory, 7, Describe, 0));          // level 8 shoes at level 7
		Assert.Single(NaturalGearPolicy.SelectUpgrades(inventory, 8, Describe, 0));         // now wearable; the same shoes are no upgrade
		Assert.Empty(NaturalGearPolicy.SelectUpgrades(inventory, 8, Describe, 0, refused: new HashSet<int> { 20 }));
	}

	[Fact]
	public void TwoRingsFillBothFingers()
	{
		BotInventoryItem[] inventory = [Bag(30, 122000869), Bag(31, 122000869)];
		Assert.Equal([RingLeft, RingRight],
			NaturalGearPolicy.SelectUpgrades(inventory, 9, Describe, 0).Select(u => u.Slot).OrderBy(s => s).ToArray());
	}

	[Fact]
	public void TheFirstWearableStaffReplacesAMaceWhateverItsItemLevel()
	{
		// Ascension: the Karmic Staff arrives while the level-3 mace is worn. A mace of a higher item level stays in the bag.
		BotInventoryItem[] inventory = [Worn(2, 100100024, Main), Bag(40, 101500498), Bag(41, 100101199), Bag(42, 114100795)];
		IReadOnlyList<NaturalGearUpgrade> upgrades = NaturalGearPolicy.SelectUpgrades(inventory, 25, Describe, offHandSlots: 0,
			rules: NaturalGearRules.Cleric);
		Assert.Equal([(40, Main), (42, Boots)], upgrades.Select(u => (u.ObjectId, u.Slot)).OrderBy(u => u.ObjectId).ToArray());
		Assert.Equal(3, upgrades.Single(u => u.ObjectId == 40).ReplacesItemLevel);
	}

	[Fact]
	public void AHeldStaffYieldsOnlyToAStaffWithMoreMagicBoost()
	{
		BotInventoryItem[] bag = [Bag(41, 100101199), Bag(43, 115001119)];
		// Magic boost decides, not item level: the level-26 staff with 420 loses to the level-25 staff with 460.
		Assert.Equal([(50, Main)], NaturalGearPolicy.SelectUpgrades([Worn(2, 101501357, Main | Sub), Bag(50, 101501224), Bag(51, 101500812), .. bag],
			26, Describe, 0, rules: NaturalGearRules.Cleric).Select(u => (u.ObjectId, u.Slot)));
		Assert.Empty(NaturalGearPolicy.SelectUpgrades([Worn(2, 101501224, Main | Sub), Bag(51, 101500812), Bag(52, 101501357), .. bag], 26, Describe, 0,
			rules: NaturalGearRules.Cleric));
		// The same staff again is no upgrade, and a staff the level does not allow yet waits.
		Assert.Empty(NaturalGearPolicy.SelectUpgrades([Worn(2, 101501357, Main | Sub), Bag(53, 101501357), Bag(50, 101501224), .. bag], 24, Describe, 0,
			rules: NaturalGearRules.Cleric));
		Assert.Empty(NaturalGearPolicy.SelectUpgrades([Worn(2, 101501357, Main | Sub), Bag(50, 101501224)], 26, Describe, 0, refused: new HashSet<int> { 50 },
			rules: NaturalGearRules.Cleric));
	}

	[Fact]
	public void WithoutAWearableStaffTheHandsFollowItemLevelAsBefore()
	{
		// The Priest in Ishalgen: a staff it cannot wear changes nothing, and the better mace still goes on.
		BotInventoryItem[] inventory = [Worn(2, 100100011, Main), Bag(14, 100100024), Bag(60, 101500001)];
		Assert.Equal([(14, Main)], NaturalGearPolicy.SelectUpgrades(inventory, 9, Describe, 0).Select(u => (u.ObjectId, u.Slot)));
	}
}
