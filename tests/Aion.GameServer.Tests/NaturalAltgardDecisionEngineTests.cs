using Aion.Bots.Scenarios;
using Aion.Bots.World;

namespace Aion.GameServer.Tests;

/// <summary>AF-08: the Leg 1 decision engine over the contract, from the <c>altgard</c> snapshot to the endpoint.</summary>
public sealed class NaturalAltgardDecisionEngineTests
{
	private static readonly NaturalAltgardContract Contract = NaturalAltgardContract.LoadDefault();
	private static readonly IReadOnlyDictionary<int, NaturalTemplateObjective> Objectives =
		NaturalTemplateObjective.From(NaturalAltgardContract.LoadPlans());
	private static readonly BotPosition Hub = new(Contract.Hub.Anchor[0] - 3, Contract.Hub.Anchor[1], Contract.Hub.Anchor[2], 0);

	private sealed class Journal
	{
		public int Level = 10;
		public bool Dead;
		public int? Map = 220030000;
		public BotPosition Position = Hub;
		public readonly Dictionary<int, BotQuestState> Quests = new() { [24011] = new(24011, 6, 0, 0, null) };
		public readonly HashSet<int> Completed = [.. Contract.Start.CompletedQuestIds];
		public readonly Dictionary<int, long> Items = [];

		public void Set(int quest, byte status, int var = 0) => Quests[quest] = new(quest, status, var, 0, null);
		public void Complete(int quest) { Quests.Remove(quest); Completed.Add(quest); }
		public NaturalAltgardDecision Decide() => NaturalAltgardDecisionEngine.Decide(Contract,
			new NaturalAltgardObservation(true, Map, Level, Dead, Quests, Completed, Position, Items), Objectives, 1);
	}

	[Fact]
	public void FromTheSnapshotTheTemplateQuestsAreAcceptedWorkedAndClaimedHubStyle()
	{
		var journal = new Journal();
		int[] templates = Contract.Order.Where(id => Contract.Quest(id).IsTemplate).ToArray();
		foreach (int quest in templates)
		{
			Assert.Equal(("template-accept", quest), (journal.Decide().Action, journal.Decide().QuestId));
			journal.Set(quest, 3);
		}
		foreach (int quest in templates)
		{
			Assert.Equal(("template-work", quest), (journal.Decide().Action, journal.Decide().QuestId));
			// A hunt stays at START with its counter full (Java monster_hunt); a collection is done when the items are held.
			if (Objectives[quest].ItemId is int item) journal.Items[item] = Objectives[quest].ItemCount;
			else
			{
				journal.Set(quest, 3, Objectives[quest].KillCount - 1);
				Assert.Equal(("template-work", quest), (journal.Decide().Action, journal.Decide().QuestId));
				journal.Set(quest, 3, Objectives[quest].KillCount);
			}
		}
		foreach (int quest in templates)
		{
			Assert.Equal(("template-claim", quest), (journal.Decide().Action, journal.Decide().QuestId));
			journal.Complete(quest);
		}
		// Level 10 with the templates done: Q2209 is next; Q2207, Q2208 and the campaign wait for level 11.
		NaturalAltgardDecision next = journal.Decide();
		Assert.Equal(("talk", "q2209-offer-thrud"), (next.Action, next.StepKey));
		Assert.Contains(next.Checks, check => check.Rule == "level" && check.Reason.Contains("Q2207", StringComparison.Ordinal));
	}

	[Fact]
	public void ScriptedQuestsFollowTheirContractStepsTheRemedyAndTheAirKills()
	{
		var journal = new Journal { Level = 11 };
		foreach (int quest in Contract.Order.Where(id => Contract.Quest(id).IsTemplate)) journal.Complete(quest);
		journal.Set(2209, 3, 1);
		Assert.Equal("q2209-v1-borender", journal.Decide().StepKey);
		journal.Set(2209, 3, 3);
		Assert.Equal("q2209-v3-thrud", journal.Decide().StepKey);
		journal.Complete(2209);
		Assert.Equal("q2207-offer-emgata", journal.Decide().StepKey);
		journal.Complete(2207);
		Assert.Equal("q2208-offer-itu", journal.Decide().StepKey);
		journal.Set(2208, 3, 0);
		Assert.Equal(("use-item", 2208), (journal.Decide().Action, journal.Decide().QuestId));
		journal.Set(2208, 3, 1);
		Assert.Equal("q2208-v1-mumu-bon", journal.Decide().StepKey);
		journal.Set(2208, 4, 1);
		Assert.Equal("q2208-reward-itu", journal.Decide().StepKey);
		journal.Complete(2208);

		// The campaign: LOCKED waits; START walks its steps, then the air kills from var 2 to 6, then the reward.
		NaturalAltgardDecision locked = journal.Decide();
		Assert.Equal("awaiting-capability", locked.Outcome);
		journal.Set(24011, 3, 0);
		Assert.Equal("q24011-v0-valurion", journal.Decide().StepKey);
		journal.Set(24011, 3, 1);
		Assert.Equal("q24011-v1-borender", journal.Decide().StepKey);
		foreach (int var in Enumerable.Range(Contract.RequiredAirKills.FromVar, Contract.RequiredAirKills.RewardVar - Contract.RequiredAirKills.FromVar + 1))
		{
			journal.Set(24011, 3, var);
			Assert.Equal("air-kills", journal.Decide().Action);
		}
		journal.Set(24011, 4, 6);
		Assert.Equal("q24011-reward-valurion", journal.Decide().StepKey);
		journal.Complete(24011);

		// Everything done: walk back into the fortress, then the endpoint.
		journal.Position = new BotPosition(1480, 1800, 247, 0);
		Assert.Equal("return-to-hub", journal.Decide().Action);
		journal.Position = Hub;
		NaturalAltgardDecision done = journal.Decide();
		Assert.Equal(("leg-complete", "complete"), (done.Action, done.Outcome));
	}

	[Fact]
	public void OnlyLevelGatedQuestsLeftMeansHuntingAndTheOddStatesStopPrecisely()
	{
		var journal = new Journal();
		foreach (int quest in Contract.Order.Where(id => Contract.Quest(id).MinimumLevel == 10)) journal.Complete(quest);
		Assert.Equal("hunt-for-level", journal.Decide().Action);

		journal.Dead = true;
		Assert.Equal("revive-at-bind", journal.Decide().Action);
		journal.Dead = false;
		journal.Map = 220010000;
		Assert.Equal(("wrong-map", "blocked"), (journal.Decide().Action, journal.Decide().Outcome));
		journal.Map = 220030000;
		journal.Level = 11;
		journal.Set(2207, 3, 7);
		Assert.Equal(("unexpected-var", "blocked"), (journal.Decide().Action, journal.Decide().Outcome));
	}
}
