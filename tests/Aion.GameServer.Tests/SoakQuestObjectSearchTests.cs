using Aion.Bots.Navigation;
using Aion.Bots.Scenarios;
using Aion.Bots.World;
using Aion.GameServer.Model;

namespace Aion.GameServer.Tests;

public sealed class SoakQuestObjectSearchTests
{
	[Theory]
	[InlineData(ScenarioRace.Elyos, 700105, 1103)]
	[InlineData(ScenarioRace.Asmodians, 700124, 2104)]
	public void UsesAllShippedCollectionSpotsWithoutChangingTheRequiredItemCount(ScenarioRace race, int template, int quest)
	{
		var positions = SoakQuestObjectSearch.ShippedPositions(race, template);
		// Spawn placement is tuned toward retail over time (Ishalgen went from 32 to 27 baskets in the 5.8
		// pass); what the soak needs is one distinct starting hint per subject, not a fixed count.
		Assert.True(positions.Count >= 20, $"Only {positions.Count} shipped spots for {template}; the soak needs 20 distinct hints.");
		var objective = StarterSoakQuest.ForRace(race).Single(stage => stage.Id == quest).Objective!;
		Assert.Equal(3, objective.Positions.Count);
		Assert.All(objective.Positions, position => Assert.Contains(position, positions));
		// Twenty quest subjects share a channel at 500 population. Their initial hints are distinct.
		Assert.Equal(20, Enumerable.Range(0, 20).Select(offset => new SoakQuestObjectSearch(positions, offset).Next()).Distinct().Count());
	}

	[Fact]
	public void FormerIshalgenSearchCannotSupplyEightSubjectsWithinTheActivityDeadline()
	{
		var objective = StarterSoakQuest.ForRace(ScenarioRace.Asmodians).Single(stage => stage.Id == 2104).Objective!;
		var positions = SoakQuestObjectSearch.ShippedPositions(ScenarioRace.Asmodians, objective.TemplateId);
		int formerlyVisible = positions.Count(position => objective.Positions.Any(hint =>
			MathF.Pow(position.X - hint.X, 2) + MathF.Pow(position.Y - hint.Y, 2) + MathF.Pow(position.Z - hint.Z, 2) <= 30 * 30));
		Assert.Equal(3, formerlyVisible);
		int minimumRespawnRounds = (8 * objective.Positions.Count - 1) / formerlyVisible;
		Assert.True(minimumRespawnRounds * 295 > 1800);
	}

	[Fact]
	public void CyclesThroughAlternativesAndRejectsOnlyKnownUnreachablePositions()
	{
		BotPosition[] points = [new(1, 2, 3, 0), new(4, 5, 6, 0), new(7, 8, 9, 0)];
		var search = new SoakQuestObjectSearch(points, 1);
		Assert.Equal(points[1], search.Next());
		search.Reject(points[2]);
		Assert.Equal(points[0], search.Next());
		Assert.Equal(points[1], search.Next());
		search.Reject(points[1]); search.Reject(points[0]);
		Assert.Null(search.Next());
		Assert.Throws<ArgumentException>(() => search.Reject(new(10, 11, 12, 0)));
	}

	[Fact]
	public void InvalidInputsAndNonCollectionTargetsAreRejected()
	{
		Assert.Throws<ArgumentException>(() => new SoakQuestObjectSearch([], 0));
		Assert.Throws<ArgumentException>(() => new SoakQuestObjectSearch([new(1, 2, 3, 0)], -1));
		Assert.Throws<ArgumentException>(() => new SoakQuestObjectSearch([new(1, 2, 3, 0), new(1, 2, 3, 0)], 0));
		Assert.Throws<ArgumentException>(() => SoakQuestObjectSearch.ShippedPositions(ScenarioRace.Asmodians, 700105));
		Assert.Throws<ArgumentException>(() => SoakQuestObjectSearch.ShippedPositions(ScenarioRace.Elyos, 210133));
	}

	[SkippableFact]
	public async Task RealGeometryOffersEnoughReturnableCollectionSpotsForEveryChannel()
	{
		Skip.IfNot(Environment.GetEnvironmentVariable("AION_SOAK_NAV_INTEGRATION") == "1", "Set AION_SOAK_NAV_INTEGRATION=1 for real quest collection geometry.");
		string root = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ScenarioManifest.FindDefaultPath())!, "../.."));
		var assets = await BotNavigationAssets.LoadAsync(root, Path.Combine(root, "run/soak-navigation-test-cache"), CancellationToken.None);
		foreach (var (race, serverRace, map, quest) in new[] {
			(ScenarioRace.Elyos, Race.ELYOS, 210010000, 1103), (ScenarioRace.Asmodians, Race.ASMODIANS, 220010000, 2104),
		})
		{
			var stage = StarterSoakQuest.ForRace(race).Single(value => value.Id == quest);
			var nav = assets.StarterRoute(serverRace, 1);
			int returnable = 0;
			foreach (var position in SoakQuestObjectSearch.ShippedPositions(race, stage.Objective!.TemplateId))
			{
				var outbound = Find(stage.Start!.Position, position);
				if (outbound.Count != 0 && Find(outbound[^1], stage.End.Position).Count != 0) returnable++;
			}
			Assert.True(returnable >= 24, $"Only {returnable} returnable collection spots in {map}; eight subjects require 24 items before any respawn.");
			IReadOnlyList<BotPosition> Find(BotPosition start, BotPosition end)
			{
				var route = nav.Graph.FindPath(map, start, end);
				if (route.Count == 0) route = nav.Geometry.FindLocalPath(map, start, end);
				return route.Count != 0 ? route : nav.Geometry.FindJourneyPath(map, start, end);
			}
		}
	}
}
