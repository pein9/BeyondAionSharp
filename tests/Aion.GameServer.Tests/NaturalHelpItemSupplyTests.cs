using System.Text.Json;
using Aion.Bots.Scenarios;
using Aion.Bots.World;

namespace Aion.GameServer.Tests;

/// <summary>NA-21: supplying the approved help items (OD-13): the plan, the refusals, the switch, the profile record
/// and the potion tiers combat now knows.</summary>
public sealed class NaturalHelpItemSupplyTests
{
	private static Dictionary<int, long> Owned(params (int Id, long Count)[] items) => items.ToDictionary(i => i.Id, i => i.Count);

	[Fact]
	public void AtLevelTenTheKitIsShieldHealOverTimeManaJellyAndPowder()
	{
		var plan = NaturalHelpItemSupply.Plan(10, Owned()).ToDictionary(topUp => topUp.ItemId, topUp => topUp.Count);
		Assert.Equal(new Dictionary<int, long>
		{
			[164000067] = 30, [162000002] = 30, [162000017] = 40, [160002273] = 8, [169300003] = 200,
		}, plan);
		// Below level 10 the kit is the level 1-9 one the operator approved for every class line on 2026-10-07 (CP-05).
		Assert.Equal(new Dictionary<int, long> { [162000006] = 30, [164000067] = 30, [164000076] = 20 },
			NaturalHelpItemSupply.Plan(9, Owned()).ToDictionary(topUp => topUp.ItemId, topUp => topUp.Count));
	}

	[Fact]
	public void ItTopsUpToNOnlyWhenBelowM()
	{
		// The bridge endpoint owns 32 Minor Life Potions and 5 jellies: at or above M, so nothing for them.
		var plan = NaturalHelpItemSupply.Plan(10, Owned((162000002, 32), (160002273, 5), (169300003, 30), (164000067, 8)));
		Assert.DoesNotContain(plan, topUp => topUp.ItemId is 162000002 or 160002273 or 164000067);
		Assert.Equal(170, Assert.Single(plan, topUp => topUp.ItemId == 169300003).Count);
		Assert.Equal(23, Assert.Single(NaturalHelpItemSupply.Plan(10, Owned((164000067, 7))), topUp => topUp.ItemId == 164000067).Count);
	}

	[Theory]
	[InlineData(20, new[] { 164000133, 164000075, 164000068, 162000003, 162000018, 160002273, 169300003 })]
	[InlineData(25, new[] { 164000133, 164000075, 164000068, 162000003, 162000018, 160002273, 169300004 })]
	[InlineData(30, new[] { 164000134, 164000076, 164000069, 162000004, 162000019, 160002273, 169300004 })]
	public void EachBandSuppliesItsOwnTierAndLetsTheOldOneRunOut(int level, int[] ids)
	{
		// Old-tier stock does not stop the new tier from being supplied.
		var owned = Owned((164000067, 30), (162000002, 30), (162000017, 40));
		Assert.Equal(ids.Order(), NaturalHelpItemSupply.Plan(level, owned).Select(topUp => topUp.ItemId).Order());
	}

	[Theory]
	[InlineData(161001001, 3)]   // Revival Stone: dropped by the operator
	[InlineData(162000012, 30)]  // Minor Life Serum: replaced by the heal-over-time Life Potion
	[InlineData(164000071, 60)]  // Courage: never (OD-15)
	[InlineData(101500498, 1)]   // gear is never supplied
	[InlineData(182400001, 1000)] // nor Kinah
	public void AnythingNotApprovedIsRefused(int itemId, long count) =>
		Assert.Throws<InvalidOperationException>(() => NaturalHelpItemSupply.RequireApproved(itemId, count));

	[Fact]
	public void ApprovedIdsAreBoundedByTheirN()
	{
		NaturalHelpItemSupply.RequireApproved(164000067, 30);
		NaturalHelpItemSupply.RequireApproved(169300003, 200);
		Assert.Throws<InvalidOperationException>(() => NaturalHelpItemSupply.RequireApproved(164000067, 31));
		Assert.Throws<InvalidOperationException>(() => NaturalHelpItemSupply.RequireApproved(164000067, 0));
	}

	[Theory]
	[InlineData(null, true)]
	[InlineData("1", true)]
	[InlineData("", true)]
	[InlineData("0", false)]
	public void TheSwitchTurnsSupplyOffOnlyAtZero(string? value, bool enabled) =>
		Assert.Equal(enabled, NaturalHelpItemSupply.Enabled(value));

	[Fact]
	public void TheProfileRecordListsEverySupplyInOrder()
	{
		NaturalHelpSupplied[] supplied =
		[
			new("level-up", 164000067, "anti-shock", 30, 0, 30, 10, 164_000),
			new("town", 169300003, "powder", 170, 30, 200, 10, 305_000),
		];
		using JsonDocument profile = JsonDocument.Parse(NaturalHelpItemSupply.ProfileJson(supplied));
		JsonElement block = profile.RootElement.GetProperty("helpItems");
		Assert.True(block.GetProperty("enabled").GetBoolean());
		JsonElement[] rows = block.GetProperty("supplied").EnumerateArray().ToArray();
		Assert.Equal([164000067, 169300003], rows.Select(row => row.GetProperty("ItemId").GetInt32()));
		Assert.Equal(["level-up", "town"], rows.Select(row => row.GetProperty("Trigger").GetString()));
		Assert.Equal(200, rows[1].GetProperty("After").GetInt64());
	}

	[Fact]
	public void CombatKnowsEveryLifePotionTierAndDrinksSerumsFirstForMana()
	{
		BotInventoryItem Item(int objectId, int itemId) => new(objectId, itemId, "", 5, 0, "", ushort.MaxValue, false);
		Assert.Equal(162000004, NaturalIshalgenPotionPolicy.SelectOwnedPotion(
			[Item(1, 162000002), Item(2, 162000003), Item(3, 162000004), Item(4, 162000053)])?.ItemId);
		Assert.Equal(162000003, NaturalIshalgenPotionPolicy.SelectOwnedPotion([Item(1, 162000002), Item(2, 162000003)])?.ItemId);
		// The Priest's own order is unchanged: the starter potion before the bought elixirs.
		Assert.Equal(162000002, NaturalIshalgenPotionPolicy.SelectOwnedPotion([Item(1, 162000052), Item(2, 162000002)])?.ItemId);
		Assert.True(NaturalIshalgenPotionPolicy.HasActiveHealing([new BotVisibleEffect(1, 9890, 1, 0, 10_000)]));
		Assert.True(NaturalIshalgenPotionPolicy.HasActiveHealing([new BotVisibleEffect(1, 9891, 1, 0, 10_000)]));
		Assert.Equal(162000017, NaturalIshalgenPotionPolicy.SelectOwnedManaPotion([Item(1, 162000007), Item(2, 162000017)])?.ItemId);
		Assert.Equal(25, NaturalIshalgenPotionPolicy.TotalHealingCount([Item(1, 162000002), Item(2, 162000003),
			Item(3, 162000004), Item(4, 162000052), Item(5, 162000053)]));
	}
}
