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
		// Q24110 (D32) is a template too, but needs level 12; it waits for the level, after the level 11 quests.
		int[] templates = Contract.Order.Where(id => Contract.Quest(id).IsTemplate && Contract.Quest(id).MinimumLevel <= journal.Level).ToArray();
		Assert.Contains(24110, Contract.Order);
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

	[Fact]
	public void ALimitedRunWorksOnlyTheChosenQuestsAndEndsWhenTheyAreDone()
	{
		// AM-06: Leg 2 limited to its three hunts, from the altgard-l12 snapshot (level 13, Q24012 started).
		NaturalAltgardContract leg2 = NaturalAltgardContract.LoadLeg("l2");
		IReadOnlyDictionary<int, NaturalTemplateObjective> objectives = NaturalTemplateObjective.From(NaturalAltgardContract.LoadPlans("l2"));
		var quests = new Dictionary<int, BotQuestState> { [24012] = new(24012, 3, 0, 0, null) };
		var completed = new HashSet<int>(leg2.Start.CompletedQuestIds);
		HashSet<int> only = [2211, 2212, 2220];
		NaturalAltgardDecision Decide() => NaturalAltgardDecisionEngine.Decide(leg2, new NaturalAltgardObservation(true, 220030000, 13, false,
			quests, completed, new BotPosition(1628, 1450, 256, 0), new Dictionary<int, long>()), objectives, 1, only);
		Assert.Equal(("template-accept", 2211), (Decide().Action, Decide().QuestId));
		quests[2211] = new(2211, 3, 0, 0, null);
		Assert.Equal(("template-accept", 2220), (Decide().Action, Decide().QuestId));
		foreach (int quest in only) { quests.Remove(quest); completed.Add(quest); }
		NaturalAltgardDecision done = Decide();
		Assert.Equal(("only-complete", "complete"), (done.Action, done.Outcome));
		// Without the filter the rest of Leg 2 is still open: Q2210 is accepted from Rion first.
		Assert.Equal(("template-accept", 2210), (NaturalAltgardDecisionEngine.Decide(leg2, new NaturalAltgardObservation(true, 220030000, 13, false,
			quests, completed, new BotPosition(1628, 1450, 256, 0), new Dictionary<int, long>()), objectives, 1) is { } open ? (open.Action, open.QuestId) : default));
	}

	[Fact]
	public void Leg2UsesObjectsEntersTheZoneCollectsAndEndsWithTheHandInAtManir()
	{
		NaturalAltgardContract leg2 = NaturalAltgardContract.LoadLeg("l2");
		IReadOnlyDictionary<int, NaturalTemplateObjective> objectives = NaturalTemplateObjective.From(NaturalAltgardContract.LoadPlans("l2"));
		var quests = new Dictionary<int, BotQuestState>();
		var completed = new HashSet<int>(leg2.Start.CompletedQuestIds);
		var itemCounts = new Dictionary<int, long>();
		var at = new BotPosition(1628, 1450, 256, 0);
		NaturalAltgardDecision Decide() => NaturalAltgardDecisionEngine.Decide(leg2,
			new NaturalAltgardObservation(true, 220030000, 14, false, quests, completed, at, itemCounts), objectives, 1);
		void Set(int quest, byte status, int var = 0) => quests[quest] = new(quest, status, var, 0, null);
		void Complete(int quest) { quests.Remove(quest); completed.Add(quest); }
		// Every template quest done except Q2215 (taken, not handed in).
		foreach (int quest in leg2.Quests.Where(quest => quest.IsTemplate && quest.Id != 2215).Select(quest => quest.Id)) Complete(quest);
		Set(2215, 3);

		// Q2213: the Okaru Tree at var 0, Tigg at var 1.
		Set(2213, 3, 0);
		Assert.Equal(("use-object", "q2213-okaru-tree"), (Decide().Action, Decide().StepKey));
		Set(2213, 3, 1);
		Assert.Equal("q2213-v1-tigg", Decide().StepKey);
		Complete(2213);

		// Q24012: Loriniah, the zone, three carts, the collection from var 5, Loriniah again.
		Set(24012, 3, 0);
		Assert.Equal("q24012-v0-loriniah", Decide().StepKey);
		Set(24012, 3, 1);
		Assert.Equal("enter-zone", Decide().Action);
		foreach (int var in new[] { 2, 3, 4 })
		{
			Set(24012, 3, var);
			Assert.Equal(("use-object", "q24012-mumu-carts"), (Decide().Action, Decide().StepKey));
		}
		Set(24012, 3, 5);
		Assert.Equal("collect", Decide().Action);
		NaturalAltgardCollection collection = leg2.CollectionList.Single();
		foreach (NaturalAltgardCollectedItem item in collection.Items) itemCounts[item.ItemId] = item.Count;
		Assert.Equal("q24012-v5-loriniah", Decide().StepKey);
		Complete(24012);

		// Only Q2215 is left: its hand-in at Manir comes last, then the endpoint is Manir's Campsite (AM-Q1).
		Assert.Equal(("template-claim", 2215), (Decide().Action, Decide().QuestId));
		Complete(2215);
		Assert.Equal("return-to-endpoint", Decide().Action);
		at = new BotPosition(leg2.Endpoint.Anchor![0] + 5, leg2.Endpoint.Anchor[1], leg2.Endpoint.Anchor[2], 0);
		NaturalAltgardDecision done = Decide();
		Assert.Equal(("leg-complete", "complete"), (done.Action, done.Outcome));
	}

	[Fact]
	public void Leg3OpensTheSafeEscortsGrokenAndEndsWithTheHandInAtNokir()
	{
		// AC-06: Leg 3 from the altgard-l2 snapshot (level 15, Q24013 started and outside the leg).
		NaturalAltgardContract leg3 = NaturalAltgardContract.LoadLeg("l3");
		IReadOnlyDictionary<int, NaturalTemplateObjective> objectives = NaturalTemplateObjective.From(NaturalAltgardContract.LoadPlans("l3"));
		var quests = new Dictionary<int, BotQuestState> { [24013] = new(24013, 3, 0, 0, null) };
		var completed = new HashSet<int>(leg3.Start.CompletedQuestIds);
		var items = new Dictionary<int, long>();
		var at = new BotPosition(1459.56f, 1192.68f, 259, 0);
		NaturalAltgardDecision Decide() => NaturalAltgardDecisionEngine.Decide(leg3,
			new NaturalAltgardObservation(true, 220030000, 15, false, quests, completed, at, items), objectives, 1);
		void Set(int quest, byte status, int var = 0) => quests[quest] = new(quest, status, var, 0, null);
		void Complete(int quest) { quests.Remove(quest); completed.Add(quest); }

		// Q24111 (D32): accepted and worked first (the primer at Manir's Dock), handed in to Nokir at the end.
		Assert.Equal(("template-accept", 24111), (Decide().Action, Decide().QuestId));
		Set(24111, 3);
		Assert.Equal(("template-work", 24111), (Decide().Action, Decide().QuestId));
		items[objectives[24111].ItemId!.Value] = objectives[24111].ItemCount;
		Assert.Equal(("talk", "q2221-offer-manir"), (Decide().Action, Decide().StepKey));
		Set(2221, 3, 0);
		Assert.Equal("q2221-v0-groken", Decide().StepKey);
		Set(2221, 3, 1);
		Assert.Equal(("use-object", "q2221-grokens-safe"), (Decide().Action, Decide().StepKey));
		Set(2221, 3, 2);
		Assert.Equal("q2221-v2-groken", Decide().StepKey);
		Complete(2221);

		// The escort covers the offer, following (var 1) and a loss (var 0); var 3 is Manir's hand-in.
		NaturalAltgardEscort escort = leg3.EscortList.Single();
		Assert.Equal(("escort", escort.Key, 2290), (Decide().Action, Decide().StepKey, Decide().QuestId));
		foreach (int var in new[] { escort.FollowVar, escort.LostVar })
		{
			Set(2290, 3, var);
			Assert.Equal(("escort", escort.Key), (Decide().Action, Decide().StepKey));
		}
		Set(2290, 3, escort.SuccessVar);
		Assert.Equal(("talk", "q2290-v3-manir"), (Decide().Action, Decide().StepKey));
		Complete(2290);

		Assert.Equal("q2222-offer-manir", Decide().StepKey);
		Set(2222, 3, 0);
		Assert.Equal("q2222-v0-karl", Decide().StepKey);
		Set(2222, 3, 1);
		Assert.Equal("q2222-v1-nokir", Decide().StepKey);
		Complete(2222);
		Assert.Equal(("template-claim", 24111), (Decide().Action, Decide().QuestId));
		Complete(24111);

		// AC-Q1: the leg ends beside Nokir at Basfelt; the campaign Q24013 stays open for a later leg.
		Assert.Equal("return-to-endpoint", Decide().Action);
		at = new BotPosition(leg3.Endpoint.Anchor![0] + 5, leg3.Endpoint.Anchor[1], leg3.Endpoint.Anchor[2], 0);
		NaturalAltgardDecision done = Decide();
		Assert.Equal(("leg-complete", "complete"), (done.Action, done.Outcome));
	}

	[Fact]
	public void Leg5HuntsTheRingCarriersByTheHourAndHoldsTwoHandIns()
	{
		// AK-08: Leg 5 from the altgard-l4 snapshot (level 19, bound at Basfelt), at noon game time.
		NaturalAltgardContract leg5 = NaturalAltgardContract.LoadLeg("l5");
		IReadOnlyDictionary<int, NaturalTemplateObjective> objectives = NaturalTemplateObjective.From(NaturalAltgardContract.LoadPlans("l5"));
		var quests = new Dictionary<int, BotQuestState>();
		var completed = new HashSet<int>(leg5.Start.CompletedQuestIds);
		var items = new Dictionary<int, long>();
		NaturalAltgardBind bind = leg5.Bind!;
		BotBindPoint bound = new(220030000, new BotPosition(bind.Position[0], bind.Position[1], bind.Position[2], 0), 0);
		var at = new BotPosition(leg5.Hub.Anchor[0] + 3, leg5.Hub.Anchor[1], leg5.Hub.Anchor[2], 0);
		long? minutes = 367 * NaturalGameClock.MinutesPerDay + 12 * 60;
		int? free = 10;
		NaturalAltgardDecision Decide() => NaturalAltgardDecisionEngine.Decide(leg5,
			new NaturalAltgardObservation(true, 220030000, 19, false, quests, completed, at, items, bound, minutes, new HashSet<int>(), free), objectives, 1);
		void Set(int quest, byte status, int var = 0) => quests[quest] = new(quest, status, var, 0, null);
		void Complete(int quest) { quests.Remove(quest); completed.Add(quest); }
		void MeetObjective(int quest)
		{
			if (objectives[quest].ItemId is int item) items[item] = objectives[quest].ItemCount;
			else Set(quest, 3, objectives[quest].KillCount);
		}
		const int passion = 122000039, jealousy = 122000040, love = 122000041;

		// A cube too full to loot (the AK-08 smoke run: 35 stacks against 27) is emptied at Gilungk in Basfelt first.
		free = 2;
		Assert.Equal("town-service", Decide().Action);
		Assert.Contains("203613", Decide().Reason, StringComparison.Ordinal);
		free = 10;

		// Every template whose prerequisite is met is accepted first; Q2234 and Q2242 wait for Q2233 and Q2241.
		foreach (int quest in leg5.Order.Where(id => id is not (2234 or 2242)))
		{
			Assert.Equal(("template-accept", quest), (Decide().Action, Decide().QuestId));
			Set(quest, 3);
		}
		// Q2292 by day: a day carrier is hunted while no ring is held.
		foreach (int quest in leg5.Order.TakeWhile(id => id != 2292))
		{
			Assert.Equal(("template-work", quest), (Decide().Action, Decide().QuestId));
			MeetObjective(quest);
		}
		NaturalAltgardDecision ring = Decide();
		Assert.Equal(("carrier-hunt", 2292), (ring.Action, ring.QuestId));
		Assert.Contains(int.Parse(ring.StepKey!), new[] { 210599, 210622, 210623 });
		// With Passion and Jealousy held, the Love Ring waits for the night, and the wait goes to the work after it.
		items[passion] = items[jealousy] = 1;
		Assert.Equal(("template-work", 24233), (Decide().Action, Decide().QuestId));
		Assert.Contains(Decide().Checks, check => check.Rule == "carrier" && check.Verdict == "wait");
		foreach (int quest in new[] { 24233, 2233, 2235, 2241 }) MeetObjective(quest);
		// The claims (and the follow-ups they open, Q2234 and Q2242) come before the wait; Q2292 is not claimed without its Love
		// Ring, nor Q24233 (held for Suthran), nor Q2242 (held for Gemyu).
		var claimed = new List<int>();
		for (NaturalAltgardDecision next = Decide(); next.Action is "template-accept" or "template-work" or "template-claim"; next = Decide())
		{
			int quest = next.QuestId!.Value;
			if (next.Action == "template-accept") Set(quest, 3);
			else if (next.Action == "template-work") MeetObjective(quest);
			else { claimed.Add(quest); Complete(quest); }
		}
		Assert.Equal(new[] { 24230, 24231, 24232, 2238, 2236, 2237, 2233, 2234, 2235, 2241 }.Order(), claimed.Order());
		Assert.Equal(new[] { 24230, 24231, 24232, 2238, 2236, 2237, 2233 }, claimed.Take(7));
		// Only the Love Ring is left: wait for 22:00 (10 game hours), then hunt Zoo or Di.
		NaturalAltgardDecision wait = Decide();
		Assert.Equal(("wait-for-carrier", 2292), (wait.Action, wait.QuestId));
		Assert.Contains("Wait 600 game minutes", wait.Reason, StringComparison.Ordinal);
		minutes += 11 * 60;
		ring = Decide();
		Assert.Equal(("carrier-hunt", 2292), (ring.Action, ring.QuestId));
		Assert.Contains(int.Parse(ring.StepKey!), new[] { 210621, 210624 });
		items[love] = 1;
		Assert.Equal(("template-claim", 2292), (Decide().Action, Decide().QuestId));
		Complete(2292);
		// Q2242 (taken, a report_to) and Q24233 (Manumumu killed) are held: the leg is complete at Basfelt without them.
		NaturalAltgardDecision end = Decide();
		Assert.Equal(("leg-complete", "complete"), (end.Action, end.Outcome));
		Assert.Equal(leg5.Endpoint.CompletedQuestIds.Order(), completed.Intersect(leg5.Order).Order());
		Assert.Equal(new[] { 2242, 24233 }, quests.Keys.Order());
		// Without the client's game clock, a carrier quest is not guessed at: the run stops and says why.
		completed.Remove(2292);
		Set(2292, 3);
		items.Remove(love);
		minutes = null;
		Assert.Equal(("no-game-clock", "blocked"), (Decide().Action, Decide().Outcome));
	}

	[Fact]
	public void Leg4BindsFirstThenRunsTimersSpawnsHuntsObjectsAndTheHornAtVar7()
	{
		// AB-08: Leg 4 from the altgard-l3 snapshot (level 16, Q24013 started, bound at the fortress).
		NaturalAltgardContract leg4 = NaturalAltgardContract.LoadLeg("l4");
		IReadOnlyDictionary<int, NaturalTemplateObjective> objectives = NaturalTemplateObjective.From(NaturalAltgardContract.LoadPlans("l4"));
		var quests = new Dictionary<int, BotQuestState> { [24013] = new(24013, 3, 0, 0, null) };
		var completed = new HashSet<int>(leg4.Start.CompletedQuestIds);
		var items = new Dictionary<int, long>();
		BotBindPoint? bound = new(220030000, new BotPosition(1658.44f, 1815.301f, 254.099f, 0), 0);
		var at = new BotPosition(leg4.Hub.Anchor[0] + 3, leg4.Hub.Anchor[1], leg4.Hub.Anchor[2], 0);
		NaturalAltgardDecision Decide() => NaturalAltgardDecisionEngine.Decide(leg4,
			new NaturalAltgardObservation(true, 220030000, 16, false, quests, completed, at, items, bound), objectives, 1);
		void Set(int quest, byte status, int var = 0) => quests[quest] = new(quest, status, var, 0, null);
		void Complete(int quest) { quests.Remove(quest); completed.Add(quest); }

		// Bound at the fortress: bind at Basfelt first (AB-Q5).
		Assert.Equal("bind", Decide().Action);
		NaturalAltgardBind bind = leg4.Bind!;
		bound = new(220030000, new BotPosition(bind.Position[0], bind.Position[1], bind.Position[2], 0), 0);
		Assert.Equal(("template-accept", 2226), (Decide().Action, Decide().QuestId));
		foreach (int quest in leg4.Quests.Where(quest => quest.IsTemplate).Select(quest => quest.Id)) Complete(quest);

		// The campaign's Basfelt steps, its poison in the zone, then its kill counter.
		Assert.Equal("q24013-v0-nokir", Decide().StepKey);
		Set(24013, 3, 2);
		Assert.Equal(("use-item", 24013), (Decide().Action, Decide().QuestId));
		foreach (int var in new[] { 3, 5, 7 })
		{
			Set(24013, 3, var);
			Assert.Equal(("hunt", 24013), (Decide().Action, Decide().QuestId));
		}
		Complete(24013);

		// The timed quests stay with the timed executor from the offer to the hand-in.
		Assert.Equal(("timed", 2288), (Decide().Action, Decide().QuestId));
		Set(2288, 3, 2);
		Assert.Equal(("timed", 2288), (Decide().Action, Decide().QuestId));
		Complete(2288);
		Assert.Equal(("timed", 2230), (Decide().Action, Decide().QuestId));
		Complete(2230);

		// Q2289: the counter, then the talks, then the horn at var 7 (a collection), then the hand-in.
		Set(2289, 3, 2);
		Assert.Equal(("hunt", 2289), (Decide().Action, Decide().QuestId));
		Set(2289, 3, 5);
		Assert.Equal("q2289-v5-gefion", Decide().StepKey);
		Set(2289, 3, 7);
		Assert.Equal(("collect", 2289), (Decide().Action, Decide().QuestId));
		items[182203016] = 1;
		Assert.Equal("q2289-v7-gefion", Decide().StepKey);
		Complete(2289);

		// Q2232: beehives until nine are in the bag, then Gilungk.
		Set(2232, 3, 1);
		Assert.Equal(("use-object", "q2232-beehives"), (Decide().Action, Decide().StepKey));
		// Each use is progress the runner can see: the reason carries the looted count, so it differs from the last one.
		string first = Decide().Reason;
		items[182203224] = 3;
		Assert.NotEqual(first, Decide().Reason);
		Assert.Contains("3 of 182203224", Decide().Reason, StringComparison.Ordinal);
		items[182203224] = 9;
		Assert.Equal("q2232-v1-gilungk", Decide().StepKey);
		Complete(2232);
		foreach (int quest in new[] { 2239, 2231 }) Complete(quest);

		// Q2223: the spawn at var 1.
		Set(2223, 3, 1);
		Assert.Equal(("spawn-kill", "q2223-infernus"), (Decide().Action, Decide().StepKey));
		Complete(2223);

		// Q24112: the Sumarhon kill, then Brodir.
		Set(24112, 3, 0);
		Assert.Equal(("hunt", 24112), (Decide().Action, Decide().QuestId));
		Set(24112, 3, 1);
		Assert.Equal("q24112-v1-brodir", Decide().StepKey);
		Complete(24112);

		// Everything done, in the hub, bound at Basfelt.
		NaturalAltgardDecision done = Decide();
		Assert.Equal(("leg-complete", "complete"), (done.Action, done.Outcome));
	}
}
