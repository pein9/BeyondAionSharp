using Aion.Bots.Scenarios;
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

	private static (string, string) Outcome(NaturalAscensionDecision decision) => (decision.Action, decision.Outcome);
}
