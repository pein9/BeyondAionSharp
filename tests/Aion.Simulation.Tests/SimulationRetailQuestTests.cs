using Aion.Bots.Scenarios;
using Aion.Bots.World;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Quest;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;

namespace Aion.Simulation.Tests;

public sealed partial class SimulationFastScenarioTests
{
	/// <summary>
	/// D32 (docs/retail-quest-completion.md): each quest that 4.8 retail ran and Java never implemented is played end to
	/// end from its compiled plan (parity-artifacts/e2e/retail-quest-plans/): accepted at its giver, its kills or items
	/// done, reported, and rewarded with what quest_data.xml says. Prerequisites, ascension and the level are GM setup;
	/// the rest is the dialog, combat and loot protocol a player uses. The cases are the D32 register
	/// (parity-artifacts/e2e/retail-quest-implemented.json), which gives each quest its own fixture account.
	/// </summary>
	public static TheoryData<int, int, string> RetailQuestCases
	{
		get
		{
			var data = new TheoryData<int, int, string>();
			string path = Path.Combine(Aion.GameServer.TestKit.RealStaticData.RepoRoot(),
				"parity-artifacts", "e2e", "retail-quest-implemented.json");
			using var register = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
			foreach (System.Text.Json.JsonProperty quest in register.RootElement.GetProperty("quests").EnumerateObject())
			{
				data.Add(int.Parse(quest.Name, System.Globalization.CultureInfo.InvariantCulture),
					quest.Value.GetProperty("simAccount").GetInt32(), quest.Value.GetProperty("simCharacter").GetString()!);
			}
			return data;
		}
	}

	[SkippableTheory]
	[MemberData(nameof(RetailQuestCases))]
	public async Task RetailQuestPlaysEndToEnd(int questId, int accountId, string characterName)
	{
		Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
		QuestRunPlan plan = QuestRunPlan.Load(Path.Combine(Aion.GameServer.TestKit.RealStaticData.RepoRoot(),
			"parity-artifacts", "e2e", "retail-quest-plans", $"{questId}.json"));
		Race race = plan.Race == "ELYOS" ? Race.ELYOS : Race.ASMODIANS;
		using var policy = NewPolicy($"D32-{questId}", includeHistory: false);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
		CancellationToken token = timeout.Token;
		await using var session = new SimulationL0Session(fixture, policy, "b01", accountId, characterName, race);

		session.BeginStep("s00", "login-create-enter-and-level");
		await session.LoginAndAuthenticateAsync(token);
		await session.CreateCharacterAsync(token, PlayerClass.PRIEST);
		await session.EnterWorldAsync(token);
		await session.SynchronizeAsync(token);
		Player player = fixture.World.GetPlayer(session.CharacterId);
		// Ascend first (it completes the ascension quest and makes a Daeva); a non-Daeva is capped at level 9.
		if (plan.MinimumLevel >= 10)
			Assert.True(ClassChangeService.SetClass(player, PlayerClass.CLERIC, validate: false, updateDaevaStatus: true));
		player.GetCommonData().SetLevel(Math.Max(plan.MinimumLevel, 1));
		Assert.Equal(Math.Max(plan.MinimumLevel, 1), player.GetLevel());
		await session.DrainServerPacketsAsync(token);
		long kinahBefore = player.GetInventory().GetKinah();

		session.BeginStep("s01", $"quest-plan:{plan.Template}:{plan.Name}");
		var driver = new SimulationQuestRunDriver(this, session, player);
		await QuestRunExecutor.ExecuteAsync(QuestRunBook.Build(plan), driver, token);

		QuestState state = player.GetQuestStateList().GetQuestState(questId);
		Assert.Equal(QuestStatus.COMPLETE, state.GetStatus());
		Assert.True(session.Api.World.CompletedQuests.ContainsKey(questId) ||
			session.Api.World.Quests.TryGetValue(questId, out BotQuestState? seen) && seen.Status == 5,
			$"Bot world did not observe Q{questId} complete.");
		Rewards rewards = DataManager.QUEST_DATA.GetQuestById(questId).GetRewards()[0];
		Assert.Equal(rewards.GetKinah(), player.GetInventory().GetKinah() - kinahBefore);
		foreach (QuestItems item in rewards.GetRewardItem())
			Assert.Equal((long)item.GetCount(), player.GetInventory().GetItemCountByItemId(item.GetItemId()));
		QuestWorkItems? workItems = DataManager.QUEST_DATA.GetQuestById(questId).GetQuestWorkItems();
		foreach (QuestItems item in workItems?.GetQuestWorkItem() ?? [])
			Assert.Equal(0L, player.GetInventory().GetItemCountByItemId(item.GetItemId()));
		policy.AssertClean();
	}
}
