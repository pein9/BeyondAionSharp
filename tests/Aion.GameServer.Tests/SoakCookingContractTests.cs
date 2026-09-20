using System.Xml.Linq;
using Aion.Bots.Scenarios;

namespace Aion.GameServer.Tests;

public sealed class SoakCookingContractTests
{
	[Fact]
	public void EveryApprenticeSkillLevelHasAnEligibleShippedOrderForBothRaces()
	{
		Assert.Equal(20, SoakCookingCatalog.All.Count);
		Assert.Equal(new[] { 169400096, 169400098, 169400102, 169400121 }, SoakCookingCatalog.ShopMaterials.Order());
		foreach (var master in new[] { CookingMaster.Hestia, CookingMaster.Lainita })
			foreach (int level in Enumerable.Range(1, 99))
			{
				var selected = SoakCookingCatalog.Select(master, level);
				Assert.Equal(master, selected.Order.Master);
				Assert.InRange(level - selected.Order.SkillLevel, 0, 9);
				Assert.Equal(1, selected.Materials[selected.Order.IssuedItemId]);
				Assert.True(selected.Order.IssuedCount >= selected.Order.DeliverCount);
				Assert.NotEmpty(SoakCookingRewards.For(selected.Order));
			}
	}

	[Theory]
	[InlineData(0)]
	[InlineData(100)]
	public void UnsupportedMasteryLevelIsNotSilentlyDowngraded(int level) =>
		Assert.Throws<ArgumentOutOfRangeException>(() => SoakCookingCatalog.Select(CookingMaster.Hestia, level));

	[Fact]
	public void FirstOrdersMatchBreadthContractsAndUseRaceCorrectBonusItems()
	{
		foreach (var (master, recipeItem, otherRecipe) in new[] { (CookingMaster.Hestia, 152201381, 152206386), (CookingMaster.Lainita, 152206386, 152201381) })
		{
			var order = SoakCookingCatalog.Select(master, 1).Order;
			Assert.Equal(CookingWorkOrder.For(master)[0], order);
			var bonuses = SoakCookingRewards.For(order);
			Assert.Equal(2, bonuses.Count);
			Assert.Equal((3L, 5L), bonuses[CookingWorkOrder.SaltId]);
			Assert.Equal((1L, 1L), bonuses[recipeItem]);
			Assert.False(bonuses.ContainsKey(otherRecipe));
		}
	}

	[Theory]
	[InlineData(3)]
	[InlineData(4)]
	[InlineData(5)]
	public void MaterialRewardPreservesExistingStackAndAllUnrelatedItems(int amount)
	{
		var before = new Dictionary<int, long> { [CookingWorkOrder.SaltId] = 4000, [182400001] = 10 };
		var after = new Dictionary<int, long>(before) { [CookingWorkOrder.SaltId] = 4000 + amount };
		var delta = SoakCookingRewards.ValidateDelta(before, after, SoakCookingRewards.For(CookingWorkOrder.For(CookingMaster.Hestia)[0]));
		Assert.Equal(new KeyValuePair<int, long>(CookingWorkOrder.SaltId, amount), delta);
	}

	[Theory]
	[InlineData(0, 0, 0)] // Missing bonus, not an acceptable production outcome when a group matches.
	[InlineData(2, 0, 0)]
	[InlineData(6, 0, 0)]
	[InlineData(3, 1, 0)] // Unrelated kinah mutation.
	[InlineData(3, 0, 1)] // Two bonuses in a single completion.
	[InlineData(0, 0, 2)] // Recipe count must be exactly one.
	public void RewardOracleRejectsMissingDuplicateAndCorruptRewards(int salt, int kinah, int recipe)
	{
		var before = new Dictionary<int, long> { [182400001] = 100 };
		var after = new Dictionary<int, long> { [182400001] = 100 + kinah, [169400096] = salt, [152201381] = recipe };
		Assert.Throws<InvalidDataException>(() => SoakCookingRewards.ValidateDelta(before, after,
			SoakCookingRewards.For(CookingWorkOrder.For(CookingMaster.Hestia)[0])));
	}

	[Fact]
	public void RecipeWindowRespectsDecadeTierBoundaryAndEntryRaceOverride()
	{
		var groups = XElement.Parse("""
			<item_groups><craft_recipes bonusType="TASK" chance="4">
			<item id="1" skill="40001" level="90"/>
			<item id="2" skill="40001" level="90" race="ASMODIANS"/>
			</craft_recipes></item_groups>
			""");
		var races = new Dictionary<int, string> { [1] = "PC_ALL", [2] = "PC_ALL" };
		Assert.Single(SoakCookingRewards.Select(groups, races, "ELYOS", 40001, 99));
		Assert.Empty(SoakCookingRewards.Select(groups, races, "ELYOS", 40001, 100));
		Assert.Equal(2, SoakCookingRewards.Select(groups, races, "ASMODIANS", 40001, 99).Count);
	}
}
