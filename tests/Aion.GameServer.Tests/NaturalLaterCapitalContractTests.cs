using System.Text.Json;
using System.Xml.Linq;
using Aion.Bots.Scenarios;
using Aion.Bots.World;

namespace Aion.GameServer.Tests;

public sealed class NaturalLaterCapitalContractTests
{
	private static readonly Lazy<NaturalLaterCapitalContract> Contract = new(NaturalLaterCapitalContract.LoadDefault);
	private static NaturalJourneyCheckpoint State(ushort level = 20, BotQuestState[]? active = null,
		int[]? completed = null, NaturalJourneyItem[]? items = null) => new(42, 1, 220030000,
		new(100, 200, 300, 0), level, 100, 100, 100, 100, false, active ?? [], completed ?? [], items ?? [], [],
		new(1, "observe", null, "planned", "observe", [], []), PlayerClass: 10,
		CompletedQuests: (completed ?? []).Select(id => new NaturalJourneyCompletedQuest(id, 1)).ToArray());

	[Fact]
	public void ApprovedInventoryMatchesShippedLevelsAndProtectsAllSuppliedAndCollectedItems()
	{
		Assert.Equal([2916, 2917, 2918, 2919, 2920, 2938, 2954, 2959, 2984], Contract.Value.CompletedQuestIds.Order());
		string root = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ScenarioManifest.FindDefaultPath())!, "../.."));
		var data = XDocument.Load(Path.Combine(root, "game-server/data/static_data/quest_data/quest_data.xml"));
		foreach (NaturalLaterCapitalQuest quest in Contract.Value.Quests)
		{
			XElement source = data.Root!.Elements("quest").Single(q => (int?)q.Attribute("id") == quest.Id);
			Assert.Equal((int)source.Attribute("minlevel_permitted")!, quest.MinimumLevel);
			foreach (XElement item in source.Elements("quest_work_items").Elements().Concat(source.Elements("collect_items").Elements()))
				Assert.Contains((int)item.Attribute("item_id")!, quest.ProtectedItemIds);
		}
		Assert.Equal(Contract.Value.Assignments.Length, Contract.Value.Assignments.Select(a => a.Key).Distinct().Count());
		Assert.Contains(182207009, Contract.Value.ProtectedItemIds);
		Assert.Contains(169100000, Contract.Value.ProtectedItemIds);
		Assert.DoesNotContain(24114, Contract.Value.CompletedQuestIds);
		Assert.DoesNotContain(4933, Contract.Value.CompletedQuestIds);
	}

	[Theory]
	[InlineData("heritage-pickup", 9)]
	[InlineData("maternal-love", 9)]
	[InlineData("book-prepare", 12)]
	[InlineData("robe-prepare", 14)]
	[InlineData("juice-first", 18)]
	[InlineData("family-letter", 19)]
	[InlineData("dye", 19)]
	[InlineData("elementary", 15)]
	[InlineData("library-pickup", 19)]
	public void ActualLevelGatesDeferTheAssignment(string key, ushort level) =>
		Assert.Equal("deferred-level", Contract.Value.Observe(key, State(level)).Outcome);

	[Fact]
	public void BothElementaryPrerequisitesAndTheLibraryCampaignPermissionAreRequired()
	{
		Assert.Equal("deferred-prerequisites", Contract.Value.Observe("elementary", State(completed: [2268])).Outcome);
		Assert.Equal("work", Contract.Value.Observe("elementary", State(completed: [2268, 2269])).Outcome);
		var library = State(active: [new(2938, 3, 0, 0, null)]);
		Assert.Equal("deferred-prerequisites", Contract.Value.Observe("library-permission", library).Outcome);
		Assert.Equal("work", Contract.Value.Observe("library-permission", library with { CompletedQuestIds = [24016] }).Outcome);
		Assert.Equal("deferred-prerequisites", Contract.Value.Observe("maternal-love", State()).Outcome);
	}

	[Fact]
	public void CollectedItemsNeverSubstituteForAcceptanceAndDistanceDoesNotSubstituteForLoot()
	{
		var tails = State(items: [new(1, 182207011, 2, 65535)]);
		Assert.Equal("blocked-incoming-state", Contract.Value.Observe("book-ampha", tails).Outcome);
		Assert.Equal("complete", Contract.Value.Observe("book-ampha", tails with { Quests = [new(2919, 3, 4, 0, null)] }).Outcome);
		var clothing = State(active: [new(2916, 3, 6, 0, null)]);
		Assert.Equal("work", Contract.Value.Observe("robe-clothing", clothing).Outcome);
		Assert.Equal("complete", Contract.Value.Observe("robe-clothing", clothing with { Inventory = [new(2, 182207007, 1, 65535)] }).Outcome);
		Assert.Equal("complete", Contract.Value.Observe("robe-berth", State(active: [new(2916, 3, 0x01000004, 0, null)])).Outcome);
	}

	[Fact]
	public void CompletedRepeatableIsNeverScheduledAgainAtTheFallbackVisit()
	{
		var paid = State(completed: [2954]);
		Assert.Equal("complete", Contract.Value.Observe("juice-first", paid).Outcome);
		Assert.Equal("complete", Contract.Value.Observe("juice-fallback", paid).Outcome);
		Assert.Equal((2954, (byte)1), (Contract.Value.Capture(paid).CompletedQuests.Single().QuestId,
			Contract.Value.Capture(paid).CompletedQuests.Single().CompleteCount));
		Assert.Throws<InvalidDataException>(() => Contract.Value.Capture(paid with { CompletedQuests = [new(2954, 2)] }));
		Assert.Throws<InvalidDataException>(() => Contract.Value.Capture(paid with { CompletedQuests = [] }));
	}

	[Fact]
	public async Task CarriedQuestAndItemIdentitySurviveReceiptAndOrdinaryCheckpointSerialization()
	{
		var before = State(active: [new(2917, 3, 1, 0, null), new(2938, 4, 0, 0, null)], completed: [2954],
			items: [new(11, 182207008, 1, 65535), new(12, 182207026, 1, 65535), new(13, 169100000, 8, 65535)]);
		var fresh = JsonSerializer.Deserialize<NaturalJourneyCheckpoint>(JsonSerializer.Serialize(before))! with { ConnectionGeneration = 2 };
		NaturalJourneyPersistence.Verify(before, fresh);
		var retained = Contract.Value.Capture(fresh);
		Assert.Equal([2917, 2938], retained.ActiveQuests.Select(q => q.QuestId));
		Assert.Equal(before.Inventory, retained.Inventory);
		string directory = Path.Combine(Path.GetTempPath(), "aion-later-capital-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(directory);
		try
		{
			await Contract.Value.WriteCheckpointAsync(directory, "l7", before, fresh, CancellationToken.None);
			using var receipt = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "later-capital-checkpoint.json")));
			Assert.True(receipt.RootElement.GetProperty("verified").GetBoolean());
			Assert.Equal(42, receipt.RootElement.GetProperty("after").GetProperty("characterId").GetInt32());
			await Assert.ThrowsAsync<InvalidDataException>(() => Contract.Value.WriteCheckpointAsync(directory, "l7", before,
				fresh with { Inventory = [] }, CancellationToken.None));
		}
		finally { Directory.Delete(directory, true); }
	}

	[Fact]
	public void ScopeIsExplicitAndDoesNotChangeHistoricalOrLiveRuns()
	{
		var scoped = new NaturalJourneyOptions(AscensionBridge: true, LaterCapital: true);
		Contract.Value.ValidateScope(scoped, "SIM-natural");
		Contract.Value.ValidateScope(new(), "LIVE-natural");
		Contract.Value.ValidateScope(new(AltgardLegId: "l5", LaterCapital: true), "SIM-natural");
		Assert.Throws<InvalidOperationException>(() => Contract.Value.ValidateScope(scoped, "LIVE-natural"));
		Assert.Throws<InvalidOperationException>(() => Contract.Value.ValidateScope(scoped with { CapitalStage = "first" }, "SIM-natural"));
		Assert.Throws<InvalidOperationException>(() => Contract.Value.ValidateScope(scoped with { AltgardOnlyQuests = [2917] }, "SIM-natural"));
		Assert.Throws<InvalidOperationException>(() => Contract.Value.ValidateScope(new(LaterCapital: true), "SIM-natural"));
	}
}
