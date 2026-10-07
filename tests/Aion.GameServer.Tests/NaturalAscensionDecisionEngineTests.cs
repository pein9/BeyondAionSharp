using Aion.Bots.Scenarios;
using Aion.Bots.Scenarios.Classes;
using Aion.Bots.World;
using Aion.GameServer.Model;

namespace Aion.GameServer.Tests;

/// <summary>NA-11: every route row, reset state and stop of the Ascension bridge's decision rule.</summary>
public sealed class NaturalAscensionDecisionEngineTests
{
	private const int Ishalgen = 220010000, Ataxiar = 320020000, Pandaemonium = 120010000, Altgard = 220030000;
	private static readonly Lazy<NaturalAscensionContract> Contract = new(NaturalAscensionContract.LoadDefault);
	private static readonly byte Priest = PlayerClass.PRIEST.GetClassId(), Cleric = PlayerClass.CLERIC.GetClassId();
	private static readonly BotBindPoint Fortress = new(Altgard, new BotPosition(1658.4f, 1815.3f, 254.1f, 0), 0);
	private static readonly int[] Ishalgen41 = [2000, 2001, 2002, 2003, 2004, 2005, 2006, 2007];

	private static NaturalAscensionObservation State(int map, byte playerClass, int level, int[] completed,
		(int Quest, byte Status, int Var)[] active, long kinah = 0, BotBindPoint? bind = null, bool shop = false, bool dead = false) =>
		new(true, map, level, playerClass, dead,
			active.ToDictionary(q => q.Quest, q => new BotQuestState(q.Quest, q.Status, q.Var, 0, null)),
			Ishalgen41.Concat(completed).ToHashSet(), kinah, bind, new Dictionary<int, long>(), shop);

	private static NaturalAscensionDecision Decide(NaturalAscensionObservation state) =>
		NaturalAscensionDecisionEngine.Decide(Contract.Value, state, 1);

	[Theory]
	[InlineData(0, Ishalgen, "q2008-v0-munin")]
	[InlineData(1, Ishalgen, "q2008-v1-urd")]
	[InlineData(2, Ishalgen, "q2008-v2-verdandi")]
	[InlineData(3, Ishalgen, "q2008-v3-skuld")]
	[InlineData(4, Ishalgen, "q2008-v4-munin")]
	[InlineData(99, Ataxiar, "q2008-v99-hagen")]
	public void TheAscensionCircuitTalksToTheRightNpcOnTheRightMap(int var, int map, string step)
	{
		NaturalAscensionDecision decision = Decide(State(map, Priest, 9, [], [(2008, 3, var)]));
		Assert.Equal(("talk", step, "planned"), (decision.Action, decision.StepKey, decision.Outcome));
	}

	[Fact]
	public void TheTrialIsFlownThenFoughtThenTheClassIsChosenAndTheInstanceLeft()
	{
		Assert.Equal("wait-flight", Decide(State(Ataxiar, Priest, 9, [], [(2008, 3, 50)])).Action);
		Assert.Equal("fight-trial", Decide(State(Ataxiar, Priest, 9, [], [(2008, 3, 51)])).Action);
		Assert.Contains("1 left", Decide(State(Ataxiar, Priest, 9, [], [(2008, 3, 54)])).Reason);
		Assert.Contains("Hellion", Decide(State(Ataxiar, Priest, 9, [], [(2008, 3, 5)])).Reason);
		Assert.Equal("q2008-v6-munin-class", Decide(State(Ataxiar, Priest, 9, [], [(2008, 3, 6)])).StepKey);
		// SETPRO14 makes the character a Cleric before NOREWARD completes the quest inside Ataxiar.
		Assert.Equal("q2008-reward-munin", Decide(State(Ataxiar, Cleric, 9, [], [(2008, 4, 6)])).StepKey);
	}

	[Fact]
	public void ADeathOrARelogThatResetsAscensionSendsTheBotBackToMunin()
	{
		Assert.Equal("revive-at-bind", Decide(State(Ataxiar, Priest, 9, [], [(2008, 3, 52)], dead: true)).Action);
		// Java resets var to 4 on death in the instance, or on login outside it with var > 4 and var != 6.
		Assert.Equal("q2008-v4-munin", Decide(State(Ishalgen, Priest, 9, [], [(2008, 3, 4)])).StepKey);
		// A trial var outside the instance is not a bridge state.
		Assert.Equal("wrong-map", Decide(State(Ishalgen, Priest, 9, [], [(2008, 3, 52)])).Action);
		Assert.Equal("wrong-map", Decide(State(Ishalgen, Priest, 9, [], [(2008, 3, 99)])).Action);
	}

	[Theory]
	[InlineData(3, 0, Ishalgen, "q2009-v0-munin")]
	[InlineData(3, 1, Pandaemonium, "q2009-v1-heimdall")]
	[InlineData(3, 2, Pandaemonium, "q2009-v2-balder")]
	[InlineData(4, 40, Pandaemonium, "q2009-reward-lyfjaberga")]
	public void TheCeremonyWalksPandaemonium(byte status, int var, int map, string step) =>
		Assert.Equal(step, Decide(State(map, Cleric, 9, [2008], [(2009, status, var)])).StepKey);

	[Fact]
	public void TheDispatchTeleportsBindsThenReportsAndSuthranFollows()
	{
		int[] ceremonyDone = [2008, 2009];
		Assert.Equal("q2904-v0-doman", Decide(State(Pandaemonium, Cleric, 10, ceremonyDone, [(2904, 3, 0)], 250_000)).StepKey);
		Assert.Equal("teleport", Decide(State(Pandaemonium, Cleric, 10, ceremonyDone, [(2904, 3, 1)], 250_000)).Action);
		// In Altgard the bind comes first, then Meiyer, then Suthran (Q24010 starts on arrival).
		Assert.Equal("bind", Decide(State(Altgard, Cleric, 10, ceremonyDone, [(2904, 3, 1), (24010, 3, 0)], 249_000)).Action);
		Assert.Equal("q2904-reward-meiyer",
			Decide(State(Altgard, Cleric, 10, ceremonyDone, [(2904, 3, 1), (24010, 3, 0)], 248_000, Fortress)).StepKey);
		Assert.Equal("q24010-reward-suthran",
			Decide(State(Altgard, Cleric, 10, [2008, 2009, 2904], [(24010, 3, 0)], 248_000, Fortress)).StepKey);
	}

	[Fact]
	public void EarlyCeremonyStopsBeforeDispatchWithoutRequiringAllIshalgenQuests()
	{
		var partial = State(Ishalgen, Priest, 9, [], [(2008, 3, 0)]) with
		{ CompletedQuestIds = new HashSet<int> { 2000, 2001, 2005 } };
		Assert.Equal("q2008-v0-munin", NaturalAscensionDecisionEngine.Decide(Contract.Value, partial, 1,
			ceremonyOnly: true).StepKey);
		var ceremony = State(Pandaemonium, Cleric, 10, [2008, 2009], [(2904, 3, 0)]);
		Assert.Equal(("ceremony-complete", "complete"), Outcome(NaturalAscensionDecisionEngine.Decide(
			Contract.Value, ceremony, 2, ceremonyOnly: true)));
		Assert.Equal("q2904-v0-doman", Decide(ceremony).StepKey);
		Assert.Equal("q2009-v1-heimdall", NaturalAscensionDecisionEngine.Decide(Contract.Value,
			State(Pandaemonium, Cleric, 9, [2008], [(2009, 3, 1)]), 3, ceremonyOnly: true).StepKey);
		Assert.Equal(("level", "blocked"), Outcome(NaturalAscensionDecisionEngine.Decide(Contract.Value,
			ceremony with { Level = 9 }, 4, ceremonyOnly: true)));
		Assert.Equal(("identity", "blocked"), Outcome(NaturalAscensionDecisionEngine.Decide(Contract.Value,
			ceremony with { ClassId = Priest }, 5, ceremonyOnly: true)));
	}

	[Fact]
	public void MissingKinahAndAutoStartedQuestsStopPrecisely()
	{
		Assert.Equal(("not-enough-kinah", "blocked"),
			Outcome(Decide(State(Pandaemonium, Cleric, 10, [2008, 2009], [(2904, 3, 1)], kinah: 100))));
		Assert.Equal(("not-enough-kinah", "blocked"),
			Outcome(Decide(State(Altgard, Cleric, 10, [2008, 2009], [(2904, 3, 1)], kinah: 100))));
		Assert.Equal(("wait-journal", "awaiting-capability"), Outcome(Decide(State(Ishalgen, Cleric, 9, [2008], []))));
		Assert.Equal(("wait-journal", "awaiting-capability"), Outcome(Decide(State(Pandaemonium, Cleric, 10, [2008, 2009], []))));
		Assert.Equal(("level", "blocked"), Outcome(Decide(State(Pandaemonium, Cleric, 9, [2008, 2009], []))));
		Assert.Equal(("refresh-observation", "planned"),
			Outcome(Decide(State(Ishalgen, Priest, 9, [], [(2008, 3, 0)]) with { Synchronized = false })));
	}

	[Fact]
	public void ACharacterOutsideTheBridgeIsBlockedNotFixed()
	{
		Assert.Equal(("identity", "blocked"), Outcome(Decide(State(Altgard, PlayerClass.CHANTER.GetClassId(), 10, [2008], []))));
		Assert.Equal(("identity", "blocked"), Outcome(Decide(State(Pandaemonium, Priest, 9, [], [(2008, 3, 0)]))));
		// A Priest who has somehow completed Ascension contradicts the journal.
		Assert.Equal(("identity", "blocked"), Outcome(Decide(State(Ishalgen, Priest, 9, [2008], []))));
	}

	[Fact]
	public void TheShopStopComesLastThenTheEndpoint()
	{
		int[] all = [2008, 2009, 2904, 24010];
		Assert.Equal("shop", Decide(State(Altgard, Cleric, 10, all, [], 247_000, Fortress)).Action);
		Assert.Equal(("bridge-complete", "complete"), Outcome(Decide(State(Altgard, Cleric, 10, all, [], 240_000, Fortress, shop: true))));
		Assert.Equal("bind", Decide(State(Altgard, Cleric, 10, all, [], 247_000, shop: true)).Action);
		Assert.Equal(("wrong-map", "blocked"), Outcome(Decide(State(Pandaemonium, Cleric, 10, all, [], 247_000, Fortress, shop: true))));
	}

	[Theory]
	[InlineData(6, 0, Ishalgen, false)]  // LOCKED mid-Ishalgen (Java lists it from about level 8): not a start
	[InlineData(0, 0, Ishalgen, false)]
	[InlineData(3, 0, Ishalgen, false)]  // START/0: the Munin stop itself; the Ishalgen leg finishes first
	[InlineData(3, 1, Ishalgen, true)]
	[InlineData(3, 52, Ataxiar, true)]
	[InlineData(4, 6, Ataxiar, true)]
	[InlineData(3, 0, Pandaemonium, true)]
	[InlineData(3, 0, Altgard, true)]
	public void TheBridgeResumesOnlyOnceAscensionIsUnderway(byte status, int var, int map, bool started) =>
		Assert.Equal(started, NaturalAscensionDecisionEngine.BridgeStarted(new HashSet<int>(),
			new Dictionary<int, BotQuestState> { [2008] = new(2008, status, var, 0, null) }, map));

	[Fact]
	public void ACompletedAscensionAlwaysResumesTheBridge() =>
		Assert.True(NaturalAscensionDecisionEngine.BridgeStarted(new HashSet<int> { 2008 }, new Dictionary<int, BotQuestState>(), Ishalgen));

	[Fact]
	public void TheReviewedBridgesStepsAndReasonsReadAsTheyAlwaysDid()
	{
		// CP-26: the class-dependent steps and reasons now come from the contract. For the reviewed bridge they are these.
		static (string?, string) Read(NaturalAscensionDecision decision) => (decision.StepKey, decision.Reason);
		int[] ceremonyDone = [2008, 2009], all = [2008, 2009, 2904, 24010];
		Assert.Equal(("q2008-v6-munin-class", "Choose Cleric (SETPRO14)."), Read(Decide(State(Ataxiar, Priest, 9, [], [(2008, 3, 6)]))));
		Assert.Equal(("q2009-v0-munin", "Munin sends the Cleric to Pandaemonium."), Read(Decide(State(Ishalgen, Cleric, 9, [2008], [(2009, 3, 0)]))));
		Assert.Equal(("q2009-reward-lyfjaberga", "Lyfjaberga: the Karmic Staff (REWARD2)."),
			Read(Decide(State(Pandaemonium, Cleric, 9, [2008], [(2009, 4, 40)]))));
		Assert.Equal((null, "Q2904 needs level 10; observed 9."), Read(Decide(State(Pandaemonium, Cleric, 9, ceremonyDone, []))));
		Assert.Equal((null, "Q2904 starts when Q2009 completes; not in the journal yet."), Read(Decide(State(Pandaemonium, Cleric, 10, ceremonyDone, []))));
		Assert.Equal(("q2904-v0-doman", "Doman takes the dispatch."), Read(Decide(State(Pandaemonium, Cleric, 10, ceremonyDone, [(2904, 3, 0)], 250_000))));
		NaturalAscensionDecision teleport = Decide(State(Pandaemonium, Cleric, 10, ceremonyDone, [(2904, 3, 1)], 250_000));
		Assert.Equal((2904, "Doman's teleporter to Altgard (location 9)."), (teleport.QuestId, teleport.Reason));
		NaturalAscensionDecision poor = Decide(State(Pandaemonium, Cleric, 10, ceremonyDone, [(2904, 3, 1)], 100));
		Assert.Equal((2904, "Doman's fare is at least 500; 100 Kinah."), (poor.QuestId, poor.Reason));
		Assert.Equal(("q2904-reward-meiyer", "Report to Meiyer."),
			Read(Decide(State(Altgard, Cleric, 10, ceremonyDone, [(2904, 3, 1), (24010, 3, 0)], 248_000, Fortress))));
		Assert.Equal((null, "Every bridge quest is done, but the Cleric is on map 120010000, not Altgard."),
			Read(Decide(State(Pandaemonium, Cleric, 10, all, [], 247_000, Fortress, shop: true))));
		NaturalAscensionDecision done = Decide(State(Altgard, Cleric, 10, all, [], 240_000, Fortress, shop: true));
		Assert.Contains(done.Checks, check => check.Rule == "endpoint" && check.Reason == "Four quests, Cleric, bound in Altgard, shop stop done.");
		Assert.Contains(done.Checks, check => check.Rule == "identity" && check.Reason == "AscensionCleric on map 220030000, level 10.");
	}

	[Fact]
	public void AChanterIsDecidedByItsOwnBridgeAndStoppedByTheClerics()
	{
		// CP-26: the identity check uses the contract's pair. Under the Chanter's bridge a level-9 Chanter in Ataxiar at
		// Q2008 REWARD goes on; under the reviewed bridge it is still blocked, as before.
		NaturalAscensionContract chanter = NaturalAscensionContract.ForChoice(Contract.Value, NaturalClassLineContract.LoadDefault(),
			PlayerClass.PRIEST, PlayerClass.CHANTER);
		NaturalAscensionDecision Chanter(NaturalAscensionObservation state, bool ceremonyOnly = false) =>
			NaturalAscensionDecisionEngine.Decide(chanter, state, 1, ceremonyOnly);
		byte chanterId = PlayerClass.CHANTER.GetClassId();
		NaturalAscensionObservation chosen = State(Ataxiar, chanterId, 9, [], [(2008, 4, 6)]);
		Assert.Equal(("talk", "q2008-reward-munin", "planned"), (Chanter(chosen).Action, Chanter(chosen).StepKey, Chanter(chosen).Outcome));
		Assert.Equal(("talk", "q2008-reward-munin", "planned"),
			(Chanter(chosen, ceremonyOnly: true).Action, Chanter(chosen, ceremonyOnly: true).StepKey, Chanter(chosen, ceremonyOnly: true).Outcome));
		Assert.Equal(("identity", "blocked"), Outcome(Decide(chosen)));
		Assert.Contains("CHANTER level 9 on map 320020000", Decide(chosen).Reason, StringComparison.Ordinal);

		// Its route: SETPRO13 at Munin, the same preceptor and staff, the same dispatch quest.
		NaturalAscensionDecision choice = Chanter(State(Ataxiar, Priest, 9, [], [(2008, 3, 6)]));
		Assert.Equal(("q2008-v6-munin-class", "Choose Chanter (SETPRO13)."), (choice.StepKey, choice.Reason));
		Assert.Equal("Munin sends the Chanter to Pandaemonium.", Chanter(State(Ishalgen, chanterId, 9, [2008], [(2009, 3, 0)])).Reason);
		NaturalAscensionDecision ceremony = Chanter(State(Pandaemonium, chanterId, 9, [2008], [(2009, 4, 40)]));
		Assert.Equal(("q2009-reward-lyfjaberga", "Lyfjaberga: the Karmic Staff (REWARD2)."), (ceremony.StepKey, ceremony.Reason));
		Assert.Equal(("ceremony-complete", "complete"), Outcome(Chanter(State(Pandaemonium, chanterId, 10, [2008, 2009], [(2904, 3, 0)]), ceremonyOnly: true)));
		Assert.Equal("q2904-v0-doman", Chanter(State(Pandaemonium, chanterId, 10, [2008, 2009], [(2904, 3, 0)], 250_000)).StepKey);
		Assert.Equal(("bridge-complete", "complete"),
			Outcome(Chanter(State(Altgard, chanterId, 10, [2008, 2009, 2904, 24010], [], 240_000, Fortress, shop: true))));
		// The Chanter's bridge refuses the Cleric, and the mace is named by its id.
		Assert.Equal(("identity", "blocked"), Outcome(Chanter(State(Altgard, Cleric, 10, [2008], []))));
		NaturalAscensionContract mace = NaturalAscensionContract.ForChoice(Contract.Value, NaturalClassLineContract.LoadDefault(),
			PlayerClass.PRIEST, PlayerClass.CHANTER, 100100495);
		Assert.Equal("Preceptor 204083: ceremony item 100100495 (REWARD1).",
			NaturalAscensionDecisionEngine.Decide(mace, State(Pandaemonium, chanterId, 9, [2008], [(2009, 4, 40)]), 1).Reason);
	}

	[Fact]
	public void AnotherStartersBridgeUsesItsOwnPreceptorAndDispatchQuest()
	{
		NaturalAscensionContract templar = NaturalAscensionContract.ForChoice(Contract.Value, NaturalClassLineContract.LoadDefault(),
			PlayerClass.WARRIOR, PlayerClass.TEMPLAR, 100000640);
		NaturalAscensionDecision Templar(NaturalAscensionObservation state) => NaturalAscensionDecisionEngine.Decide(templar, state, 1);
		byte warrior = PlayerClass.WARRIOR.GetClassId(), templarId = PlayerClass.TEMPLAR.GetClassId();
		Assert.Equal("q2008-v0-munin", Templar(State(Ishalgen, warrior, 9, [], [(2008, 3, 0)])).StepKey);
		NaturalAscensionDecision choice = Templar(State(Ataxiar, warrior, 9, [], [(2008, 3, 6)]));
		Assert.Equal(("q2008-v6-munin-class", "Choose Templar (SETPRO8)."), (choice.StepKey, choice.Reason));
		NaturalAscensionDecision ceremony = Templar(State(Pandaemonium, templarId, 9, [2008], [(2009, 4, 10)]));
		Assert.Equal(("q2009-reward-preceptor-204080", "Preceptor 204080: ceremony item 100000640 (REWARD1)."), (ceremony.StepKey, ceremony.Reason));
		// The Warrior-born dispatch is Q2901; the Priest-born Q2904 in the journal does not move this bridge.
		int[] ceremonyDone = [2008, 2009];
		NaturalAscensionDecision foreign = Templar(State(Pandaemonium, templarId, 10, ceremonyDone, [(2904, 3, 0)]));
		Assert.Equal(("wait-journal", "awaiting-capability", (int?)2901), (foreign.Action, foreign.Outcome, foreign.QuestId));
		Assert.Equal("Q2901 needs level 10; observed 9.", Templar(State(Pandaemonium, templarId, 9, ceremonyDone, [])).Reason);
		Assert.Equal("q2901-v0-doman", Templar(State(Pandaemonium, templarId, 10, ceremonyDone, [(2901, 3, 0)], 250_000)).StepKey);
		NaturalAscensionDecision teleport = Templar(State(Pandaemonium, templarId, 10, ceremonyDone, [(2901, 3, 1)], 250_000));
		Assert.Equal(("teleport", (int?)2901), (teleport.Action, teleport.QuestId));
		Assert.Equal("q2901-reward-meiyer",
			Templar(State(Altgard, templarId, 10, ceremonyDone, [(2901, 3, 1), (24010, 3, 0)], 248_000, Fortress)).StepKey);
		Assert.Equal("q24010-reward-suthran", Templar(State(Altgard, templarId, 10, [2008, 2009, 2901], [(24010, 3, 0)], 248_000, Fortress)).StepKey);
		NaturalAscensionDecision done = Templar(State(Altgard, templarId, 10, [2008, 2009, 2901, 24010], [], 240_000, Fortress, shop: true));
		Assert.Equal(("bridge-complete", "complete"), Outcome(done));
		Assert.Contains(done.Checks, check => check.Rule == "endpoint" && check.Reason == "Four quests, Templar, bound in Altgard, shop stop done.");
		// A Cleric or a Gladiator is outside this bridge.
		Assert.Equal(("identity", "blocked"), Outcome(Templar(State(Altgard, Cleric, 10, [2008], []))));
		Assert.Equal(("identity", "blocked"), Outcome(Templar(State(Altgard, PlayerClass.GLADIATOR.GetClassId(), 10, [2008], []))));
	}

	private static (string, string) Outcome(NaturalAscensionDecision decision) => (decision.Action, decision.Outcome);
}
