using System.Xml.Linq;
using Aion.Bots.Navigation;
using Aion.Bots.Scenarios;
using Aion.Bots.World;
using Aion.GameServer.Model;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class StarterSoakQuestTests
{
	[Theory]
	[InlineData(ScenarioRace.Elyos, 7, 9)]
	[InlineData(ScenarioRace.Asmodians, 6, 10)]
	public void FiniteJourneyPinsShippedRewardsObjectivesAndSpawnCoordinates(ScenarioRace race, int quests, int objects)
	{
		string root = Root();
		var data = XElement.Load(Path.Combine(root, "game-server/data/static_data/quest_data/quest_data.xml"));
		var spawns = XElement.Load(Path.Combine(root, "game-server/data/static_data/spawns/Npcs",
			race == ScenarioRace.Elyos ? "210010000_Poeta.xml" : "220010000_Ishalgen.xml"));
		var stages = StarterSoakQuest.ForRace(race);
		Assert.Equal(quests, stages.Count);
		Assert.Equal(quests, stages.Select(stage => stage.Id).Distinct().Count());
		Assert.Equal(objects, stages.Sum(stage => stage.Objective?.Positions.Count ?? 0));
		foreach (var stage in stages)
		{
			var quest = data.Elements("quest").Single(node => (int?)node.Attribute("id") == stage.Id);
			Assert.Equal(1, (int?)quest.Attribute("max_repeat_count"));
			var rewards = quest.Element("rewards")!;
			Assert.Equal(stage.Gold, (int?)rewards.Attribute("gold") ?? 0);
			Assert.Equal(stage.Experience, (int?)rewards.Attribute("exp") ?? 0);
			if (stage.RewardItem != 0)
			{
				var item = rewards.Elements(stage.Campaign ? "selectable_reward_item" : "reward_item").First();
				Assert.Equal(stage.RewardItem, (int?)item.Attribute("item_id"));
				Assert.Equal(stage.RewardCount, (int?)item.Attribute("count"));
			}
			if (stage.Start is { } start) Spawn(start.TemplateId, start.Position);
			Spawn(stage.End.TemplateId, stage.End.Position);
			if (stage.Objective is { } objective)
			{
				foreach (var position in objective.Positions) Spawn(objective.TemplateId, position);
				if (objective.ItemId != 0)
				{
					Assert.Contains(quest.Elements("quest_drop"), drop => (int?)drop.Attribute("npc_id") == objective.TemplateId && (int?)drop.Attribute("item_id") == objective.ItemId);
					Assert.Equal(objective.Positions.Count, (int?)quest.Element("collect_items")!.Element("collect_item")!.Attribute("count"));
				}
				else Assert.Equal(objective.Positions.Count, (int?)quest.Element("quest_kill")!.Attribute("count"));
			}
		}
		void Spawn(int id, BotPosition position) => Assert.Contains(spawns.Descendants("spawn").Where(spawn => (int?)spawn.Attribute("npc_id") == id).Elements("spot"),
			spot => (float)spot.Attribute("x")! == position.X && (float)spot.Attribute("y")! == position.Y && (float)spot.Attribute("z")! == position.Z);
	}

	[SkippableFact]
	public async Task BothFiniteJourneysHaveCheckedRoutesIncludingReturnToSoakHub()
	{
		Skip.IfNot(Environment.GetEnvironmentVariable("AION_SOAK_NAV_INTEGRATION") == "1", "Set AION_SOAK_NAV_INTEGRATION=1 for real quest route geometry.");
		var assets = await BotNavigationAssets.LoadAsync(Root(), Path.Combine(Root(), "run/soak-navigation-test-cache"), CancellationToken.None);
		foreach (var (race, serverRace, map, home) in new[] {
			(ScenarioRace.Elyos, Race.ELYOS, 210010000, VendorScenario.Position),
			(ScenarioRace.Asmodians, Race.ASMODIANS, 220010000, GatheringTarget.YoungAzpha.Position),
		})
		{
			var nav = assets.StarterRoute(serverRace, 1);
			var current = home;
			foreach (var next in StarterSoakQuest.ForRace(race).SelectMany(stage => stage.Stops()).Concat(StarterSoakQuest.ReturnVia(race)).Append(home))
			{
				var path = nav.Graph.FindPath(map, current, next);
				if (path.Count == 0) path = nav.Geometry.FindLocalPath(map, current, next);
				if (path.Count == 0) path = nav.Geometry.FindJourneyPath(map, current, next);
				Assert.True(path.Count > 0, $"No checked quest path in {map}: {current} -> {next}");
				current = path[^1];
			}
		}
	}
	private static string Root() => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ScenarioManifest.FindDefaultPath())!, "../.."));
}

public sealed class SoakQuestResourcesTests
{
	[Fact]
	public async Task ConcurrentClaimsAreExclusiveAndCompletedCorpsesCannotBeReused()
	{
		var resources = new SoakQuestResources(3);
		var leases = await Task.WhenAll(Enumerable.Range(0, 100).Select(_ => Task.Run(() => resources.TryAcquire(210010000, 0, 42))));
		var owner = Assert.Single(leases, lease => lease != null)!;
		Assert.Equal(1, resources.Count);
		owner.Complete(); owner.Dispose(); owner.Dispose();
		Assert.Null(resources.TryAcquire(210010000, 0, 42));
		Assert.Throws<InvalidOperationException>(owner.Complete);
		using var next = resources.TryAcquire(210010000, 0, 43);
		using var otherChannel = resources.TryAcquire(210010000, 1, 42);
		Assert.NotNull(next); Assert.NotNull(otherChannel);
		Assert.Equal(3, resources.Count);
		Assert.Throws<InvalidOperationException>(() => resources.TryAcquire(210010000, 0, 44));
	}

	[Fact]
	public void FailedApproachReleasesReservationAndInvalidKeysFail()
	{
		var resources = new SoakQuestResources(1);
		resources.TryAcquire(220010000, 4, 7)!.Dispose();
		Assert.Equal(0, resources.Count);
		using var retry = resources.TryAcquire(220010000, 4, 7);
		Assert.NotNull(retry);
		Assert.Throws<ArgumentOutOfRangeException>(() => resources.TryAcquire(220010000, 5, 7));
		Assert.Throws<ArgumentOutOfRangeException>(() => new SoakQuestResources(0));
	}
}
