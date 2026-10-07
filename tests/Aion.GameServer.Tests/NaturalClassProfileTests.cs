using Aion.Bots.Scenarios;
using Aion.Bots.Scenarios.Classes;
using Aion.Bots.World;
using Aion.GameServer.Model;

namespace Aion.GameServer.Tests;

/// <summary>
/// CP-14: the seam types of docs/natural-class-profiles.md. The Priest and Cleric profiles are adapters over the static
/// combat policy and must answer exactly as it does; the class line and the profile lookup refuse what they do not hold.
/// </summary>
public sealed class NaturalClassProfileTests
{
	private static readonly DateTimeOffset Now = new(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);
	private const int Target = 71;
	private static readonly int[] PriestLevel9 = [1838, 1839, 4012, 4013, 1614, 1615, 1684, 1814];
	private static readonly int[] ClericLevel10 = [.. PriestLevel9, 246, 249, 3922, 3939, 4025, 4061, 4083, 4127];

	[Fact]
	public void ThePriestAndClericAdaptersAnswerAsTheStaticPolicyDoes()
	{
		NaturalMauPolicyParameters baseline = NaturalMauPolicyParameters.Baseline;
		NaturalMauPolicyParameters tuned = new(PullDistanceMeters: 20f, HealSinglePercent: 60, HealMultiplePercent: 75, FinishTargetHpPercent: 10);
		int states = 0;
		// The 576 states of NaturalClericCombatPolicyTests.ThePriestRotationAndRestAreUnchanged, a level 9 Priest.
		foreach (NaturalCombatObservation state in Sweep(9, 669, 1211, PriestLevel9))
		{
			states++;
			AssertSame(NaturalPriestProfile.Priest, state, baseline, NaturalPriestSkills.All);
			AssertSame(NaturalPriestProfile.Cleric, state, baseline, NaturalClericSkills.All);
			// The static policy's own default catalog is the Priest's.
			AssertSame(NaturalPriestProfile.Priest, state, baseline, null);
		}
		Assert.Equal(576, states);
		// The same sweep for a level 10 Cleric with its first skills, and with parameters other than the baseline.
		foreach (NaturalCombatObservation state in Sweep(10, 760, 1340, ClericLevel10))
		{
			AssertSame(NaturalPriestProfile.Cleric, state, baseline, NaturalClericSkills.All);
			AssertSame(NaturalPriestProfile.Cleric, state, tuned, NaturalClericSkills.All);
			AssertSame(NaturalPriestProfile.Priest, state, tuned, NaturalPriestSkills.All);
		}
	}

	[Fact]
	public void TheAdaptersReportTheRunsPolicyVersion()
	{
		NaturalMauPolicyParameters tuned = new(HealSinglePercent: 60);
		foreach (NaturalClassProfile profile in new[] { NaturalPriestProfile.Priest, NaturalPriestProfile.Cleric })
		{
			Assert.Equal(NaturalMauPolicyParameters.Baseline.Id, profile.Combat.PolicyVersion(NaturalMauPolicyParameters.Baseline));
			Assert.Equal(tuned.Id, profile.Combat.PolicyVersion(tuned));
		}
		Assert.NotEqual(NaturalMauPolicyParameters.Baseline.Id, tuned.Id);
	}

	[Fact]
	public void TheEmergencyBandIsTheStaticPolicys()
	{
		foreach (NaturalClassProfile profile in new[] { NaturalPriestProfile.Priest, NaturalPriestProfile.Cleric })
		foreach (int attackers in Enumerable.Range(0, 6))
		foreach (bool seasoned in new[] { false, true })
		{
			Assert.Equal(NaturalPriestCombatPolicy.EmergencyEnterPercent(attackers, seasoned), profile.Combat.EmergencyEnterPercent(attackers, seasoned));
			Assert.Equal(NaturalPriestCombatPolicy.EmergencyExitPercent(attackers, seasoned), profile.Combat.EmergencyExitPercent(attackers, seasoned));
		}
	}

	[Fact]
	public void TheHelpItemGatesAreWhatTheLevelOneToNineKitLeft()
	{
		// Before CP-16 all four gates asked one rule: the Cleric, or any character at level 9 or below.
		foreach ((NaturalClassProfile profile, bool cleric) in new[] { (NaturalPriestProfile.Priest, false), (NaturalPriestProfile.Cleric, true) })
		for (int level = 1; level <= 65; level++)
		{
			bool before = cleric || level <= NaturalHelpItemAllowlist.StarterMaxLevel;
			NaturalHelpItemRules rules = profile.HelpItems;
			Assert.Equal((before, before, before, before),
				(rules.Supplied(level), rules.ShieldScroll(level), rules.ManaPotion(level), rules.ScrollUpkeep(level)));
		}
		Assert.Equal("awakening", NaturalPriestProfile.Priest.HelpItems.SharedSlotFamily);
		Assert.Equal("awakening", NaturalPriestProfile.Cleric.HelpItems.SharedSlotFamily);
	}

	[Fact]
	public void TheKitsAreAllowlistRowsAndPlanTheSameSupply()
	{
		Assert.Equal(NaturalHelpItemAllowlist.Starter, NaturalPriestProfile.Priest.HelpItems.Kit);
		Assert.Equal(NaturalHelpItemAllowlist.AllLevels, NaturalPriestProfile.Cleric.HelpItems.Kit);
		var nothing = new Dictionary<int, long>();
		var some = NaturalHelpItemAllowlist.AllLevels.Select(supply => supply.ItemId).Distinct().ToDictionary(id => id, id => (long)(id % 7));
		foreach (NaturalClassProfile profile in new[] { NaturalPriestProfile.Priest, NaturalPriestProfile.Cleric })
		for (int level = 1; level <= 45; level++)
		{
			if (!profile.HelpItems.Supplied(level)) continue;
			// Wherever the supply is on, the profile's kit plans what the whole allowlist planned.
			Assert.Equal(NaturalHelpItemSupply.Plan(level, nothing), NaturalHelpItemSupply.Plan(level, nothing, profile.HelpItems.Kit));
			Assert.Equal(NaturalHelpItemSupply.Plan(level, some), NaturalHelpItemSupply.Plan(level, some, profile.HelpItems.Kit));
		}
	}

	[Fact]
	public void UpkeepPatrolAndRangedHoldAreTodaysRules()
	{
		foreach (NaturalClassProfile profile in new[] { NaturalPriestProfile.Priest, NaturalPriestProfile.Cleric })
		{
			// One buff, Blessing of Guardianship, found and recognised as before: the Priest table's rows.
			Assert.Equal(new NaturalUpkeepBuff("blessing", "buff-blessing"), Assert.Single(profile.Upkeep));
			Assert.Equal(NaturalPriestSkills.Ids("blessing"), profile.EffectIds("blessing"));
			Assert.Same(profile.EffectIds("blessing"), profile.EffectIds("blessing"));
			foreach (int level in Enumerable.Range(1, 30))
			{
				var learned = NaturalClericSkills.All.Where(skill => skill.MinimumLevel <= level)
					.ToDictionary(skill => (int)skill.Id, skill => new BotSkill(skill.Id, 1, 0, 0, 0, 0));
				Assert.Equal(NaturalPriestSkills.Best("blessing", level, learned), NaturalPriestSkills.Best("blessing", level, learned, profile.Skills));
			}
			// The run's option still decides the ranged hold.
			Assert.Equal(NaturalRangedHold.RunOption, profile.RangedHold);
			Assert.True(profile.HoldsAtRange(true));
			Assert.False(profile.HoldsAtRange(false));
		}
		// The heal over time the fight recognises: the Cleric's ranks. A Priest has none to observe.
		Assert.Equal(NaturalClericSkills.Cleric.Where(skill => skill.Role == "rejuvenation").Select(skill => (int)skill.Id).Order(),
			NaturalPriestProfile.Cleric.EffectIds("rejuvenation").Order());
		Assert.Empty(NaturalPriestProfile.Priest.EffectIds("rejuvenation"));
		Assert.Equal(NaturalPatrolRule.Baseline, NaturalPriestProfile.Priest.PatrolRule);
		Assert.Equal(NaturalPatrolRule.HoldAndAssess, NaturalPriestProfile.Cleric.PatrolRule);
		NaturalClassProfile always = Copy(NaturalPriestProfile.Priest, NaturalRangedHold.Always), never = Copy(NaturalPriestProfile.Priest, NaturalRangedHold.Never);
		Assert.True(always.HoldsAtRange(false) && !never.HoldsAtRange(true));
	}

	private static NaturalClassProfile Copy(NaturalClassProfile profile, NaturalRangedHold hold, NaturalEngageRanges? ranges = null) => new()
	{
		Class = profile.Class, Skills = profile.Skills, Excluded = profile.Excluded, Combat = profile.Combat,
		HelpItems = profile.HelpItems, Upkeep = profile.Upkeep, PatrolRule = profile.PatrolRule, RangedHold = hold, Rest = profile.Rest,
		Ranges = ranges ?? profile.Ranges, Readiness = profile.Readiness, Movement = profile.Movement,
	};

	[Fact]
	public void TheEngageRangesAndReadinessAreTheNumbersTheHelpersHeld()
	{
		foreach (NaturalClassProfile profile in new[] { NaturalPriestProfile.Priest, NaturalPriestProfile.Cleric })
		{
			// CP-18: every site keeps its number. 20, 22, 23, 25 and 30 stay separate, and no 21 is stored.
			Assert.Equal(new NaturalEngageRanges(MeleeReach: 3, SpellRange: 22, PullDistance: null, FiringRange: 23, SpawnApproachRange: 23,
				SpawnPullScanRange: 30, FightThroughPullRange: 30, StandoffSpellRange: 25, StandoffArrivalTolerance: 3, StandoffSafetyMargin: 1,
				RangedApproachRadius: 20), profile.Ranges);
			Assert.Equal(new NaturalReadinessThresholds(new(80), new(60, 40), new(60, 40), new(80, 60)), profile.Readiness);
			// The pull distance is still the run's parameter.
			Assert.Equal(22f, profile.PullDistance(NaturalMauPolicyParameters.Baseline));
			Assert.Equal(19.5f, profile.PullDistance(new NaturalMauPolicyParameters(PullDistanceMeters: 19.5f)));
			Assert.Equal(18f, Copy(profile, profile.RangedHold, profile.Ranges with { PullDistance = 18 }).PullDistance(NaturalMauPolicyParameters.Baseline));
			// What the refusal text of the shipped-spawn approach prints.
			Assert.Equal("inside 23 m of", $"inside {profile.Ranges.SpawnApproachRange} m of");
		}
	}

	[Fact]
	public void ReadinessAsksForRestBelowEitherPercentage()
	{
		// The integer comparison the helpers wrote inline: hp * 100 < maxHp * percent, or the same for MP.
		BotWorldModel World(int hp, int mp)
		{
			var world = new BotWorldModel();
			world.Apply(new Aion.Bots.Protocol.DecodedBotServerPacket(typeof(Aion.GameServer.Network.Aion.ServerPackets.SM_STATUPDATE_HP),
				new Dictionary<string, object?> { ["currentHp"] = hp, ["maxHp"] = 669 }));
			world.Apply(new Aion.Bots.Protocol.DecodedBotServerPacket(typeof(Aion.GameServer.Network.Aion.ServerPackets.SM_STATUPDATE_MP),
				new Dictionary<string, object?> { ["currentMp"] = mp, ["maxMp"] = 1211 }));
			Assert.Equal((hp, 669, mp, 1211), (world.CurrentHp, world.MaxHp, world.CurrentMp, world.MaxMp));
			return world;
		}
		foreach (int hp in new[] { 0, 1, 334, 335, 401, 402, 535, 536, 669 })
		foreach (int mp in new[] { 0, 1, 484, 485, 726, 727, 1211 })
		foreach ((int hpPercent, int mpPercent) in new[] { (80, 0), (60, 40), (80, 60), (90, 50) })
		{
			bool inline = hp * 100 < 669 * hpPercent || mp * 100 < 1211 * mpPercent;
			Assert.Equal(inline, new NaturalReadiness(hpPercent, mpPercent).RestFirst(World(hp, mp)));
		}
		Assert.Equal(new NaturalReadiness(80, 0), new NaturalReadiness(80));
	}

	[Fact]
	public void TheProfilesCarryTheFrozenSkillTables()
	{
		Assert.Equal(PlayerClass.PRIEST, NaturalPriestProfile.Priest.Class);
		Assert.Same(NaturalPriestSkills.All, NaturalPriestProfile.Priest.Skills);
		Assert.Empty(NaturalPriestProfile.Priest.Excluded);
		Assert.Equal(PlayerClass.CLERIC, NaturalPriestProfile.Cleric.Class);
		Assert.Same(NaturalClericSkills.All, NaturalPriestProfile.Cleric.Skills);
		Assert.Same(NaturalClericSkills.Excluded, NaturalPriestProfile.Cleric.Excluded);
		// What the journey reads today for each observed class.
		Assert.Same(NaturalClericSkills.ForClass(PlayerClass.PRIEST.GetClassId()), NaturalPriestProfile.Priest.Skills);
		Assert.Same(NaturalClericSkills.ForClass(null), NaturalPriestProfile.Priest.Skills);
		Assert.Same(NaturalClericSkills.ForClass(PlayerClass.CLERIC.GetClassId()), NaturalPriestProfile.Cleric.Skills);
	}

	[Fact]
	public void ForGivesTheObservedClassOfTheLineAndRefusesEveryOther()
	{
		NaturalClassLine line = NaturalClassLine.PriestCleric;
		Assert.Same(NaturalPriestProfile.Priest, NaturalClassProfiles.For(PlayerClass.PRIEST.GetClassId(), line));
		Assert.Same(NaturalPriestProfile.Priest, NaturalClassProfiles.For(null, line));
		Assert.Same(NaturalPriestProfile.Cleric, NaturalClassProfiles.For(PlayerClass.CLERIC.GetClassId(), line));
		PlayerClass[] others = Enumerable.Range(0, 256)
			.Select(id => PlayerClassExtensions.GetPlayerClassById((byte)id, true))
			.OfType<PlayerClass>().Where(playerClass => playerClass is not (PlayerClass.PRIEST or PlayerClass.CLERIC)).ToArray();
		Assert.Equal(15, others.Length);
		foreach (PlayerClass other in others)
		{
			var refused = Assert.Throws<InvalidDataException>(() => NaturalClassProfiles.For(other.GetClassId(), line));
			Assert.Contains(other.ToString(), refused.Message, StringComparison.Ordinal);
			Assert.Contains(line.Id, refused.Message, StringComparison.Ordinal);
		}
		Assert.Throws<InvalidDataException>(() => NaturalClassProfiles.For(200, line));
		// A class the line holds but no profile is written for is refused too, not played with another class's rules.
		NaturalClassLine chanter = new("priest-chanter", PlayerClass.PRIEST, PlayerClass.CHANTER, 0, "none");
		Assert.Same(NaturalPriestProfile.Priest, NaturalClassProfiles.For(null, chanter));
		Assert.Contains("no natural class profile",
			Assert.Throws<InvalidDataException>(() => NaturalClassProfiles.For(PlayerClass.CHANTER.GetClassId(), chanter)).Message, StringComparison.Ordinal);
		Assert.Throws<InvalidDataException>(() => NaturalClassProfiles.For(PlayerClass.CLERIC.GetClassId(), chanter));
	}

	[Fact]
	public void TheTableOfLinesStartsWithTheAcceptedLineAndParseRefusesAnUnknownId()
	{
		NaturalClassLine line = NaturalClassLine.Default;
		Assert.Same(NaturalClassLine.PriestCleric, line);
		Assert.Same(line, NaturalClassLine.All[0]);
		Assert.Equal(new NaturalClassLine("priest-cleric", PlayerClass.PRIEST, PlayerClass.CLERIC, 41, "Asimnjour"), line);
		Assert.Equal(NaturalClassLine.All.Count, NaturalClassLine.All.Select(entry => entry.Id).Distinct().Count());
		Assert.Equal("CP_CLASS", NaturalClassLine.EnvironmentVariable);
		Assert.Same(line, NaturalClassLine.Parse(null));
		Assert.Same(line, NaturalClassLine.Parse(""));
		Assert.Same(line, NaturalClassLine.Parse("priest-cleric"));
		foreach (string unknown in new[] { "warrior", "Priest-Cleric", "priest-cleric ", "cleric" })
			Assert.Contains($"'{unknown}'", Assert.Throws<ArgumentException>(() => NaturalClassLine.Parse(unknown)).Message, StringComparison.Ordinal);
		Assert.True(line.Holds(PlayerClass.PRIEST) && line.Holds(PlayerClass.CLERIC));
		Assert.False(line.Holds(PlayerClass.CHANTER) || line.Holds(PlayerClass.WARRIOR));
	}

	[Fact]
	public void EveryLineIsAStarterAndASecondClassTheServerContractKnows()
	{
		NaturalClassLineContract contract = NaturalClassLineContract.LoadDefault();
		foreach (NaturalClassLine line in NaturalClassLine.All)
		{
			Assert.Equal(line.Starter, contract.Starter(line.Starter).PlayerClass);
			if (line.Second is PlayerClass second)
				Assert.Contains(contract.SecondClassesOf(line.Starter), row => row.PlayerClass == second);
			Assert.Matches("^[A-Z][a-z]+$", line.CharacterName);
		}
	}

	private static IEnumerable<NaturalCombatObservation> Sweep(int level, int maxHp, int maxMp, int[] learnedIds)
	{
		IReadOnlyDictionary<int, BotSkill> learned = learnedIds.ToDictionary(id => id, id => new BotSkill(checked((ushort)id), 1, 0, 0, 0, 0));
		foreach (float distance in new[] { 2f, 10f, 24f, 30f })
		foreach (int hp in new[] { 100, 250, 400, 600 })
		foreach (int mp in new[] { 20, 300, 1100 })
		foreach (int attackers in new[] { 0, 1, 2, 3 })
		foreach (var cooling in new[] { Cool(), Cool((1229, 2)), Cool((1549, 20), (1512, 6)) })
			yield return new NaturalCombatObservation(level, hp, maxHp, mp, maxMp, false, attackers > 0, distance, Target,
				learned, cooling, NearbyAggressors: attackers, TargetAdjacent: distance <= 3, InEmergency: hp <= 250,
				OpenChainCategory: "P_CHAINA_1TH_1", OpenChainTargetId: Target, ChainExpiresAt: DateTimeOffset.MaxValue);
	}

	/// <summary>The same action, skill, reason and checks from Decide, and the same candidate list from CandidateActions.</summary>
	private static void AssertSame(NaturalClassProfile profile, NaturalCombatObservation state, NaturalMauPolicyParameters parameters,
		NaturalPriestSkill[]? catalog)
	{
		NaturalCombatChoice expected = NaturalPriestCombatPolicy.Decide(state, Now, catalog, parameters);
		NaturalCombatChoice actual = profile.Combat.Decide(state, Now, parameters);
		Assert.Equal((expected.Action, expected.Skill, expected.TargetObjectId, expected.Reason),
			(actual.Action, actual.Skill, actual.TargetObjectId, actual.Reason));
		Assert.Equal(expected.Checks, actual.Checks);
		NaturalCombatCandidate[] expectedCandidates = NaturalPriestCombatPolicy.CandidateActions(state, Now, expected, catalog, parameters);
		NaturalCombatCandidate[] actualCandidates = profile.Combat.CandidateActions(state, Now, actual, parameters);
		Assert.Equal(expectedCandidates.Length, actualCandidates.Length);
		for (int index = 0; index < expectedCandidates.Length; index++)
		{
			NaturalCombatCandidate wanted = expectedCandidates[index], given = actualCandidates[index];
			Assert.Equal((wanted.Action, wanted.SkillId, wanted.TargetObjectId, wanted.Legal, wanted.BaselineRejection, wanted.NeedsNavigationCheck),
				(given.Action, given.SkillId, given.TargetObjectId, given.Legal, given.BaselineRejection, given.NeedsNavigationCheck));
			Assert.Equal(wanted.IllegalReasons, given.IllegalReasons);
		}
	}

	private static IReadOnlyDictionary<int, DateTimeOffset> Cool(params (int Group, int Seconds)[] groups) =>
		groups.ToDictionary(group => group.Group, group => Now.AddSeconds(group.Seconds));
}
