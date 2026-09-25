using Aion.Bots.Navigation;
using Aion.Bots.Scenarios;
using Aion.Bots.World;
using Aion.GameServer.Model;
using Aion.GameServer.TestKit;

namespace Aion.GameServer.Tests;

/// <summary>The Priest's level 1-9 rotation, from the recorded human run: open at range with Smite, then at
/// melee instants first (Infernal Blaze, Hallowed Strike), Smite as filler, the mace between skills.</summary>
public sealed class NaturalPriestRotationTests
{
	private static readonly DateTimeOffset Now = new(2026, 9, 25, 12, 0, 0, TimeSpan.Zero);

	private static IReadOnlyDictionary<int, BotSkill> Learned(params int[] ids) => ids.ToDictionary(id => id,
		id => new BotSkill(checked((ushort)id), 1, 0, 0, 0, 0));

	// A level-9 Priest with everything the tree grants, target 71 at the given distance, full health.
	private static NaturalCombatObservation At(float distance, bool adjacent = false, int hp = 100, int attackers = 0,
		Dictionary<int, DateTimeOffset>? cooldowns = null) =>
		new(9, hp, 100, 100, 100, false, attackers > 0, distance, 71,
			Learned(1838, 1839, 4012, 4013, 1614, 1615, 1684, 1814), cooldowns ?? new Dictionary<int, DateTimeOffset>(),
			NearbyAggressors: attackers, TargetAdjacent: adjacent);

	[Fact]
	public void OpensWithSmiteAtRangeAndHoldsPositionWhileTheMonsterCloses()
	{
		NaturalCombatChoice opener = NaturalPriestCombatPolicy.Decide(At(14), Now);
		Assert.Equal("cast-target", opener.Action);
		Assert.Equal((ushort)4013, opener.Skill?.Id);
		// Smite on its 2 s cooldown, monster still 8 m out: wait, never walk into melee.
		var smiteCooling = new Dictionary<int, DateTimeOffset> { [1229] = Now.AddSeconds(1.5) };
		Assert.Equal("wait", NaturalPriestCombatPolicy.Decide(At(8, cooldowns: smiteCooling), Now).Action);
		Assert.Equal("approach", NaturalPriestCombatPolicy.Decide(At(26, cooldowns: smiteCooling), Now).Action);
	}

	[Fact]
	public void AtMeleeInstantsComeFirstThenSmiteThenTheMace()
	{
		NaturalCombatChoice first = NaturalPriestCombatPolicy.Decide(At(2.3f, attackers: 1), Now);
		Assert.Equal((ushort)1814, first.Skill?.Id); // Infernal Blaze: instant, stuns
		var infernalUsed = new Dictionary<int, DateTimeOffset> { [1549] = Now.AddSeconds(24) };
		NaturalCombatChoice second = NaturalPriestCombatPolicy.Decide(At(2.3f, attackers: 1, cooldowns: infernalUsed), Now);
		Assert.Equal((ushort)1615, second.Skill?.Id); // Hallowed Strike: instant, slows
		var bothUsed = new Dictionary<int, DateTimeOffset> { [1549] = Now.AddSeconds(24), [1512] = Now.AddSeconds(8) };
		NaturalCombatChoice third = NaturalPriestCombatPolicy.Decide(At(2.3f, attackers: 1, cooldowns: bothUsed), Now);
		Assert.Equal((ushort)4013, third.Skill?.Id); // Smite as the filler
		var allUsed = new Dictionary<int, DateTimeOffset> { [1549] = Now.AddSeconds(24), [1512] = Now.AddSeconds(8), [1229] = Now.AddSeconds(2) };
		Assert.Equal("attack", NaturalPriestCombatPolicy.Decide(At(2.3f, attackers: 1, cooldowns: allUsed), Now).Action);
	}

	[Fact]
	public void AMonsterThatJustHitTheBotIsInMeleeReachWhateverTheClientDistanceSays()
	{
		// A chasing monster's client position lags; its swing proves adjacency. Hallowed Strike (range 1) is legal.
		var infernalUsed = new Dictionary<int, DateTimeOffset> { [1549] = Now.AddSeconds(24) };
		NaturalCombatChoice choice = NaturalPriestCombatPolicy.Decide(At(6, adjacent: true, attackers: 1, cooldowns: infernalUsed), Now);
		Assert.Equal((ushort)1615, choice.Skill?.Id);
		// Without that evidence, 6 m is not melee: Smite only.
		Assert.Equal((ushort)4013, NaturalPriestCombatPolicy.Decide(At(6, attackers: 1, cooldowns: infernalUsed), Now).Skill?.Id);
	}

	[Fact]
	public void HealsAtSeventyAgainstOneAttackerAndFiftyFiveAgainstTwo()
	{
		Assert.Equal("cast-self", NaturalPriestCombatPolicy.Decide(At(2, adjacent: true, hp: 65, attackers: 1), Now).Action);
		Assert.Equal("cast-target", NaturalPriestCombatPolicy.Decide(At(2, adjacent: true, hp: 65, attackers: 2), Now).Action);
		Assert.Equal("cast-self", NaturalPriestCombatPolicy.Decide(At(2, adjacent: true, hp: 55, attackers: 2), Now).Action);
	}

	[Fact]
	public void EmergencyMeansSustainOnlyUntilRecovered()
	{
		NaturalCombatObservation emergency = At(2, adjacent: true, hp: 42, attackers: 2) with
		{
			InEmergency = true, HasHealedThisFight = true, TargetHpPercent = 10,
		};
		NaturalCombatChoice choice = NaturalPriestCombatPolicy.Decide(emergency, Now);
		Assert.Equal("cast-self", choice.Action); // no finisher, no rotation: heal
		Assert.Equal("cast-target", NaturalPriestCombatPolicy.Decide(emergency with { InEmergency = false, Hp = 60 }, Now).Action);
	}

	[Fact]
	public async Task UntypedGrayManeStalkersCountAsAggressiveLikeTheServerSaysTheyAre()
	{
		var data = (await RealStaticData.LoadAsync()).StaticData;
		// 210750 and 211284 carry no type attribute (NpcTemplateType.NONE); the server aggroes by tribe.
		foreach (int stalker in new[] { 210750, 211284, 210407, 210408 })
			Assert.True(NaturalHostility.IsAggressive(data.NpcDataDh.GetNpcTemplate(stalker), data.TribeRelations, TribeClass.PC_DARK), stalker.ToString());
		Assert.Equal(9f, NaturalHostility.AggroRadius(data.NpcDataDh.GetNpcTemplate(211284), data.TribeRelations, TribeClass.PC_DARK));
		// Nalto (a quest NPC) and a null template are not.
		Assert.False(NaturalHostility.IsAggressive(data.NpcDataDh.GetNpcTemplate(203552), data.TribeRelations, TribeClass.PC_DARK));
		Assert.False(NaturalHostility.IsAggressive(null, data.TribeRelations, TribeClass.PC_DARK));
	}
}
