using Aion.Bots.Scenarios;
using Aion.Bots.World;

namespace Aion.GameServer.Tests;

public sealed class NaturalGearPolicyTests
{
	private const long Main = 1, Sub = 2, Torso = 8, Boots = 32, RingLeft = 256, RingRight = 512, Pants = 4096;

	// The recorded human session at Nalto (2026-09-24): the bot's bag after Q2006, level 9.
	private static readonly Dictionary<int, NaturalGearInfo> Items = new()
	{
		[100100011] = new(Main | Sub, 1, 1, true),       // Training Mace (worn)
		[110300292] = new(Torso, 1, 1, true),            // Training Leather Armor (worn)
		[113300278] = new(Pants, 1, 1, true),            // Training Leather Leg Armor (worn)
		[100100024] = new(Main | Sub, 3, 3, true),       // Raider's Mace
		[100200604] = new(Main | Sub, -1, 5, true),      // Ulgorn's Dagger: not a Priest weapon
		[114100794] = new(Boots, 4, 4, true),            // Boromer's Shoes
		[114100795] = new(Boots, 8, 8, true),            // Anturoon Shoes
		[113100773] = new(Pants, 8, 8, true),            // Anturoon Leggings
		[122000869] = new(RingLeft | RingRight, 7, 7, true), // Spirit Ring
		[182400001] = null!,                             // kinah: not gear
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
}
