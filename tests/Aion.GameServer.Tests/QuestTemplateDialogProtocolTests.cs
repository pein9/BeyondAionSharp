using System.Text.Json;
using Aion.Bots.Scenarios;

namespace Aion.GameServer.Tests;

public sealed class QuestTemplateDialogProtocolTests
{
	private static QuestTemplateDialogProtocolDocument Load() =>
		QuestTemplateDialogProtocol.Load(QuestTemplateDialogProtocol.FindDefaultPath());

	[Fact]
	public void CheckedInTableCoversEveryPhaseSevenTemplateAndPinnedJavaSource()
	{
		QuestTemplateDialogProtocolDocument protocol = Load();

		Assert.Equal("ce54b7931546cddafb970d20c9f71fec6d48c83b", protocol.JavaCommit);
		Assert.Equal(11, protocol.Templates.Count);
		Assert.All(protocol.Templates.Values, template =>
		{
			Assert.StartsWith("game-server/src/com/aionemu/gameserver/questEngine/handlers/template/", template.JavaSource);
			Assert.NotEmpty(template.Transitions);
		});
		Assert.Equal(1009, protocol.Actions["SELECT_QUEST_REWARD"]);
		Assert.Equal(10000, protocol.Actions["SETPRO1"]);
		Assert.Equal(10255, protocol.Actions["SET_SUCCEED"]);
		Assert.Equal([5, 6, 7, 8, 45, 46, 47, 48, 49, 50], protocol.RewardPages);
		Assert.Equal("baseDialog", protocol.Templates["report_to"].StartableDefaultHelper);
		Assert.Null(protocol.Templates["work_order"].StartableDefaultHelper);
	}

	[Theory]
	[InlineData("report_to", "start-select", 1011, 4762)]
	[InlineData("monster_hunt", "start-select", 1011, 4762)]
	[InlineData("item_collecting", "start-select", 1011, 4762)]
	[InlineData("kill_in_world", "start-select", 1011, 4762)]
	[InlineData("kill_in_zone", "start-select", 1011, 4762)]
	[InlineData("kill_spawned", "start-select", 1011, 4762)]
	public void StartSelectionPinsLegacyAndDataDrivenPages(string template, string transitionId, int legacyPage, int dataDrivenPage)
	{
		JsonElement response = Transition(Load(), template, transitionId).Response;

		Assert.Equal("page", response.GetProperty("kind").GetString());
		Assert.Equal(legacyPage, response.GetProperty("page").GetInt32());
		Assert.Equal(dataDrivenPage, response.GetProperty("dataDrivenPage").GetInt32());
	}

	[Fact]
	public void MultiReportAndItemCollectionKeepTheirConditionalPageRules()
	{
		QuestTemplateDialogProtocolDocument protocol = Load();
		JsonElement many = Transition(protocol, "report_to_many", "step-select").Response;
		Assert.Equal(
			"last step ? (dataDriven ? 10002 : 2375) : (dataDriven ? 1011 : 1352) + 341 * var0",
			many.GetProperty("pageExpression").GetString());

		JsonElement collect = Transition(protocol, "item_collecting", "check-items").Response;
		Assert.Equal(5, collect.GetProperty("true").GetProperty("page").GetInt32());
		Assert.Equal(10000, collect.GetProperty("true").GetProperty("dataDrivenPage").GetInt32());
		Assert.Equal(2716, collect.GetProperty("false").GetProperty("page").GetInt32());
		Assert.Equal(10001, collect.GetProperty("false").GetProperty("dataDrivenPage").GetInt32());
	}

	[Fact]
	public void SpecializedTemplatesPinTheirNonGenericPages()
	{
		QuestTemplateDialogProtocolDocument protocol = Load();
		AssertPage(protocol, "item_order", "talk-select", 1352);
		AssertPage(protocol, "work_order", "combine-task", 28);
		AssertPage(protocol, "work_order", "reward-use", 1008);
		AssertPage(protocol, "skill_use", "start-select", 4762);
		Assert.Equal("endDialog", Transition(protocol, "report_on_levelup", "reward").Response.GetProperty("kind").GetString());
	}

	private static void AssertPage(QuestTemplateDialogProtocolDocument protocol, string template, string transitionId, int page)
	{
		JsonElement response = Transition(protocol, template, transitionId).Response;
		Assert.Equal("page", response.GetProperty("kind").GetString());
		Assert.Equal(page, response.GetProperty("page").GetInt32());
	}

	private static QuestDialogProtocolTransition Transition(
		QuestTemplateDialogProtocolDocument protocol,
		string template,
		string id) => protocol.Templates[template].Transitions.Single(transition => transition.Id == id);
}
