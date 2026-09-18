using Aion.Bots.Scenarios;

namespace Aion.GameServer.Tests;

public sealed class QuestRunPlanTests
{
	[Fact]
	public async Task XmlQuestPlanLoadsIntoExecutableQuestObjectRunBook()
	{
		QuestRunPlan plan = Load("""
			{
			  "schemaVersion": 1,
			  "quest": { "id": 1127, "name": "Ancient Cube", "zone": "Poeta", "race": "ELYOS" },
			  "gates": { "minimumLevel": 2 },
			  "handler": { "kind": "template", "template": "xml_quest" },
			  "startTrigger": { "kind": "npc", "npcs": [
			    { "id": 798008, "name": "baevrunerk", "positions": [], "handlerSpawned": true }
			  ] },
			  "startNpcs": [{ "id": 798008, "name": "baevrunerk", "positions": [], "handlerSpawned": true }],
			  "endNpcs": [{ "id": 798008, "name": "baevrunerk", "positions": [], "handlerSpawned": true }],
			  "steps": [
			    { "kind": "collect", "count": 1, "item_id": 182200215, "sources": [
			      { "kind": "questObject", "npc": {
			        "id": 700001, "name": "ancient cube", "positions": [
			          { "mapId": 210010000, "x": 1.0, "y": 2.0, "z": 3.0, "heading": 4, "source": "fixture.xml" }
			        ], "handlerSpawned": false
			      } }
			    ] },
			    { "kind": "report", "sequence": 1, "npcs": [
			      { "id": 798008, "name": "baevrunerk", "positions": [], "handlerSpawned": true }
			    ] }
			  ],
			  "rewards": { "standard": [], "classSelectable": {} }
			}
			""");

		QuestRunBook book = QuestRunBook.Build(plan);
		Assert.Equal(
			[
				QuestRunOperationKind.Prepare,
				QuestRunOperationKind.StartAtNpc,
				QuestRunOperationKind.UseQuestObject,
				QuestRunOperationKind.Report,
				QuestRunOperationKind.ClaimReward,
			],
			book.Operations.Select(operation => operation.Kind));
		Assert.Equal(182200215, book.Operations[2].ItemId);

		var driver = new RecordingQuestRunDriver();
		await QuestRunExecutor.ExecuteAsync(book, driver);
		Assert.Equal(book.Operations, driver.Operations);
	}

	[Fact]
	public void WorkOrderCollectOutputComesFromItsCraftStep()
	{
		QuestRunPlan plan = Load("""
			{
			  "schemaVersion": 1,
			  "quest": { "id": 5500, "name": "Cooking Work Order", "zone": null, "race": "ELYOS" },
			  "gates": { "minimumLevel": 10 },
			  "handler": { "kind": "template", "template": "work_order" },
			  "startTrigger": { "kind": "npc", "npcs": [
			    { "id": 203784, "name": "hestia", "positions": [], "handlerSpawned": true }
			  ] },
			  "startNpcs": [{ "id": 203784, "name": "hestia", "positions": [], "handlerSpawned": true }],
			  "endNpcs": [{ "id": 203784, "name": "hestia", "positions": [], "handlerSpawned": true }],
			  "steps": [
			    { "kind": "collect", "count": 3, "item_id": 182290522, "sources": [] },
			    { "kind": "craft", "recipeId": 155004206, "components": [] }
			  ],
			  "rewards": { "standard": [], "classSelectable": {} }
			}
			""");

		QuestRunBook book = QuestRunBook.Build(plan);
		QuestRunOperation craft = Assert.Single(book.Operations, operation => operation.Kind == QuestRunOperationKind.Craft);
		Assert.Equal(155004206, craft.RecipeId);
	}

	[Fact]
	public void QuestDropRunBookKeepsEveryReachableSourceForFallback()
	{
		QuestRunPlan plan = Load("""
			{
			  "schemaVersion": 1,
			  "quest": { "id": 1129, "name": "Scouting Timolia Mine", "zone": "Poeta", "race": "ELYOS" },
			  "gates": { "minimumLevel": 8 },
			  "handler": { "kind": "template", "template": "item_collecting" },
			  "startTrigger": { "kind": "npc", "npcs": [
			    { "id": 203085, "name": "poa", "positions": [], "handlerSpawned": true }
			  ] },
			  "startNpcs": [{ "id": 203085, "name": "poa", "positions": [], "handlerSpawned": true }],
			  "endNpcs": [{ "id": 203067, "name": "kalio", "positions": [], "handlerSpawned": true }],
			  "steps": [{ "kind": "collect", "count": 5, "item_id": 182200213, "sources": [
			    { "kind": "questDrop", "chance": 80, "npc": {
			      "id": 210182, "name": "tursin loudmouth", "positions": [], "handlerSpawned": true
			    } },
			    { "kind": "questDrop", "npc": {
			      "id": 210162, "name": "tursin sentry", "positions": [], "handlerSpawned": true
			    } }
			  ] }],
			  "rewards": { "standard": [], "classSelectable": {} }
			}
			""");

		QuestRunOperation collect = Assert.Single(
			QuestRunBook.Build(plan).Operations,
			operation => operation.Kind == QuestRunOperationKind.CollectQuestDrop);
		Assert.Equal(210182, collect.Source?.NpcId);
		Assert.Equal([210182, 210162], collect.Sources?.Select(source => source.NpcId));
	}

	private static QuestRunPlan Load(string json)
	{
		string path = Path.Combine(Path.GetTempPath(), $"aion-quest-plan-{Guid.NewGuid():N}.json");
		try
		{
			File.WriteAllText(path, json);
			return QuestRunPlan.Load(path);
		}
		finally
		{
			File.Delete(path);
		}
	}

	private sealed class RecordingQuestRunDriver : IQuestRunDriver
	{
		public List<QuestRunOperation> Operations { get; } = [];

		public Task ExecuteAsync(QuestRunPlan plan, QuestRunOperation operation, CancellationToken cancellationToken)
		{
			Operations.Add(operation);
			return Task.CompletedTask;
		}
	}
}
