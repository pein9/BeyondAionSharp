using System.Xml.Linq;
using Aion.Bots.Scenarios;
using Aion.Bots.World;
using Aion.GameServer.Model;

namespace Aion.GameServer.Tests;

/// <summary>NA-18: the Cleric's level 10 catalog, rotation, emergency heal and powder rest.</summary>
public sealed class NaturalClericCombatPolicyTests
{
	private static readonly DateTimeOffset Now = new(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);
	private const int Target = 71;
	private static readonly int[] PriestLevel9 = [1838, 1839, 4012, 4013, 1614, 1615, 1684, 1814];
	private static readonly int[] ClericLevel10 = [.. PriestLevel9, 246, 249, 3922, 3939, 4025, 4061, 4083, 4127];
	private static readonly NaturalPriestSkill[] Catalog = NaturalClericSkills.All;

	[Fact]
	public void CatalogAgreesWithShippedSkillTreeAndTemplates()
	{
		string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
		XDocument tree = XDocument.Load(Path.Combine(root, "game-server/data/static_data/skill_tree/skill_tree.xml"));
		XDocument templates = XDocument.Load(Path.Combine(root, "game-server/data/static_data/skills/skill_templates.xml"));
		foreach (NaturalPriestSkill skill in NaturalClericSkills.Cleric)
		{
			// Herb Treatment and MP Recovery rows repeat per class; the Cleric's own row is the one that counts.
			XElement entry = Assert.Single(tree.Descendants("skill"), node => (int?)node.Attribute("skillId") == skill.Id &&
				(string?)node.Attribute("classId") == "CLERIC");
			Assert.Null(entry.Attribute("race"));
			Assert.Equal(skill.MinimumLevel, (int?)entry.Attribute("minLevel"));
			Assert.Equal("true", (string?)entry.Attribute("autolearn"));
			XElement template = Assert.Single(templates.Descendants("skill_template"),
				node => (int?)node.Attribute("skill_id") == skill.Id);
			Assert.Equal(skill.CooldownId, (int?)template.Attribute("cooldownId"));
			Assert.Equal(skill.CooldownDeciseconds, (int?)template.Attribute("cooldown"));
			Assert.Equal(skill.Range, (float?)template.Element("properties")?.Attribute("first_target_range"));
			Assert.Equal(skill.ManaCost, (int?)template.Element("endconditions")?.Element("mp")?.Attribute("value") ?? 0);
			Assert.Equal(skill.DpCost, (int?)template.Element("startconditions")?.Element("dp")?.Attribute("value") ?? 0);
			XElement? chain = template.Element("startconditions")?.Element("chain");
			Assert.Equal(skill.ChainCategory, (string?)chain?.Attribute("category"));
			Assert.Equal(skill.RequiresChainCategory, (string?)chain?.Attribute("precategory"));
			Assert.Equal(skill.RequiresChainCategory == null ? 0 : skill.ChainWindowMillis, (int?)chain?.Attribute("time") ?? 0);
			XElement? reagent = template.Element("actions")?.Element("itemuse");
			Assert.Equal(skill.ReagentItemId, (int?)reagent?.Attribute("itemid") ?? 0);
			Assert.Equal(skill.ReagentCount, (int?)reagent?.Attribute("count") ?? 0);
		}
	}

	[Fact]
	public void TheObservedClassChoosesTheCatalog()
	{
		Assert.Same(NaturalPriestSkills.All, NaturalClericSkills.ForClass(PlayerClass.PRIEST.GetClassId()));
		Assert.Same(NaturalPriestSkills.All, NaturalClericSkills.ForClass(null));
		Assert.Same(NaturalClericSkills.All, NaturalClericSkills.ForClass(PlayerClass.CLERIC.GetClassId()));
	}

	[Fact]
	public void SmiteOpensAndFlashboltFollowsBeforeAnyOtherOpener()
	{
		NaturalCombatObservation ranged = Cleric(1000, 20);
		// Flashbolt is ready, so Smite opens the chain even though Earth's Wrath and Slashing Wind are ready too.
		Assert.Equal((ushort)4013, Decide(ranged).Skill?.Id);
		NaturalCombatObservation opened = ranged with
		{
			OpenChainCategory = "P_CHAINA_1TH_1", OpenChainTargetId = Target, ChainExpiresAt = DateTimeOffset.MaxValue,
			Cooldowns = Cool((1229, 2)),
		};
		Assert.Equal((ushort)4025, Decide(opened).Skill?.Id);
		// Adjacent, the chain still comes before the instants.
		Assert.Equal((ushort)4025, Decide(opened with { TargetDistance = 2, TargetAdjacent = true }).Skill?.Id);
		Assert.Equal((ushort)4013, Decide(ranged with { TargetDistance = 2, TargetAdjacent = true }).Skill?.Id);
		// A chain opened on another monster is not this target's chain.
		Assert.Equal((ushort)4083, Decide(opened with { OpenChainTargetId = 99 }).Skill?.Id);
	}

	[Fact]
	public void WithFlashboltCoolingTheOtherAttacksFollowByRangeCooldownAndMana()
	{
		NaturalCombatObservation ranged = Cleric(1000, 20) with { Cooldowns = Cool((1230, 8)) };
		Assert.Equal((ushort)4083, Decide(ranged).Skill?.Id);
		Assert.Equal((ushort)4061, Decide(ranged with { Cooldowns = Cool((1230, 8), (1236, 10)) }).Skill?.Id);
		Assert.Equal((ushort)4013, Decide(ranged with { Cooldowns = Cool((1230, 8), (1236, 10), (1234, 14)) }).Skill?.Id);
		// Earth's Wrath (85 MP) is skipped when it would eat the healing reserve; the cheaper opener goes instead.
		Assert.Equal((ushort)4061, Decide(ranged with { Mp = 100 }).Skill?.Id);
		// At melee the uninterruptible instants come first and the 1.5 s Earth's Wrath last.
		NaturalCombatObservation melee = ranged with { TargetDistance = 2, TargetAdjacent = true };
		Assert.Equal((ushort)1814, Decide(melee).Skill?.Id);
		Assert.Equal((ushort)1615, Decide(melee with { Cooldowns = Cool((1230, 8), (1549, 20)) }).Skill?.Id);
		Assert.Equal((ushort)4061, Decide(melee with { Cooldowns = Cool((1230, 8), (1549, 20), (1512, 6)) }).Skill?.Id);
		Assert.Equal((ushort)4083, Decide(melee with { Cooldowns = Cool((1230, 8), (1549, 20), (1512, 6), (1234, 14)) }).Skill?.Id);
	}

	[Fact]
	public void SalvationIsTheEmergencyHealOnlyWithTheDpToPayForIt()
	{
		NaturalCombatObservation emergency = Cleric(1000, 2) with
		{
			Hp = 300, Aggro = true, NearbyAggressors = 1, TargetAdjacent = true, InEmergency = true, Dp = 2000,
		};
		NaturalCombatChoice salvation = Decide(emergency);
		Assert.Equal(("cast-self", (ushort?)3922), (salvation.Action, salvation.Skill?.Id));
		AssertLegal(emergency, salvation);
		Assert.Equal((ushort)1839, Decide(emergency with { Dp = 1999 }).Skill?.Id);
		Assert.Contains(NaturalPriestCombatPolicy.CandidateActions(emergency with { Dp = 1999 }, Now, Decide(emergency with { Dp = 1999 }), Catalog),
			candidate => candidate.SkillId == 3922 && !candidate.Legal);
		Assert.Equal((ushort)1839, Decide(emergency with { Cooldowns = Cool((1211, 40)) }).Skill?.Id);
		// Not an emergency: Salvation is kept for one.
		Assert.NotEqual((ushort)3922, Decide(emergency with { Hp = 700, InEmergency = false }).Skill?.Id);
	}

	[Fact]
	public void TheHealOverTimeIsKeptUpUnderAttack()
	{
		NaturalCombatObservation hit = Cleric(1000, 2) with
		{
			Hp = 900, Aggro = true, NearbyAggressors = 1, TargetAdjacent = true, HasRejuvenation = false,
		};
		NaturalCombatChoice choice = Decide(hit);
		Assert.Equal(("cast-self", (ushort?)3939), (choice.Action, choice.Skill?.Id));
		AssertLegal(hit, choice);
		Assert.NotEqual((ushort)3939, Decide(hit with { HasRejuvenation = true }).Skill?.Id);
		Assert.NotEqual((ushort)3939, Decide(hit with { HasRejuvenation = null }).Skill?.Id);
		Assert.NotEqual((ushort)3939, Decide(hit with { Aggro = false, NearbyAggressors = 0 }).Skill?.Id);
	}

	[Fact]
	public void RootComesBeforeARetreatThenTheRetreat()
	{
		NaturalCombatObservation swarmed = Cleric(1000, 2) with
		{
			Aggro = true, NearbyAggressors = NaturalPriestCombatPolicy.SwarmedAttackers, TargetAdjacent = true,
		};
		NaturalCombatChoice root = Decide(swarmed);
		Assert.Equal(("cast-target", (ushort?)4127), (root.Action, root.Skill?.Id));
		AssertLegal(swarmed, root);
		Assert.Equal("retreat", Decide(swarmed with { Cooldowns = Cool((1241, 9)) }).Action);
	}

	[Fact]
	public void PowderSkillsAreNeverCastInAFight()
	{
		NaturalCombatObservation hit = Cleric(1000, 2) with
		{
			Hp = 700, Mp = 100, Aggro = true, NearbyAggressors = 1, TargetAdjacent = true,
		};
		for (int hp = 50; hp <= 1300; hp += 125)
		for (int mp = 0; mp <= 1300; mp += 260)
		{
			NaturalCombatObservation state = hit with { Hp = hp, Mp = mp };
			NaturalCombatChoice choice = Decide(state);
			Assert.False(choice.Skill?.IsPowderRest == true, $"hp={hp} mp={mp} chose {choice.Skill?.Id}");
			Assert.All(NaturalPriestCombatPolicy.CandidateActions(state, Now, choice, Catalog)
				.Where(candidate => candidate.SkillId is 246 or 249), candidate => Assert.False(candidate.Legal));
		}
		// Engaged at rest: fight first, a hit would cancel the 4 s cast.
		Assert.Equal("defend", Rest(700, 200, true, 30).With(engaged: true).Action);
	}

	[Fact]
	public void PowderRestAlternatesOnTheSharedCooldownAndSitsOnlyAsAFallback()
	{
		// Larger deficit first: MP 15% vs HP 30% missing.
		Assert.Equal("herb", Rest(910, 1100, recovering: true, powder: 30).Choose().Action);
		Assert.Equal("mp-recovery", Rest(1200, 200, recovering: true, powder: 30).Choose().Action);
		Assert.Equal("herb", Rest(700, 1300, recovering: false, powder: 30).Choose().Action);
		// Both needed: alternate with the one not cast last.
		Assert.Equal("mp-recovery", Rest(700, 200, true, 30).With(last: 246).Action);
		Assert.Equal("herb", Rest(700, 200, true, 30).With(last: 249).Action);
		// One cooldown group for both (1153): no powder cast while it runs.
		NaturalPowderRestChoice cooling = Rest(700, 200, true, 30).With(cooldown: 10);
		Assert.Equal(("sit", Now.AddSeconds(10)), (cooling.Action, cooling.ReadyAt));
		Assert.Equal("light-heal", Rest(700, 1300, false, 30).With(cooldown: 10).Action);
		// Powder gates: MP Recovery needs two, Herb Treatment one; without powder, sit for mana.
		Assert.Equal("sit", Rest(1200, 200, true, 1).Choose().Action);
		Assert.Equal("herb", Rest(700, 200, true, 1).Choose().Action);
		Assert.Equal("sit", Rest(1200, 200, true, 0).Choose().Action);
		Assert.Equal("light-heal", Rest(700, 1300, false, 0).Choose().Action);
		Assert.Equal("done", Rest(1250, 1300, false, 30).Choose().Action);
	}

	[Fact]
	public void ThePriestRotationAndRestAreUnchanged()
	{
		var learned = Learn(PriestLevel9);
		foreach (float distance in new[] { 2f, 10f, 24f, 30f })
		foreach (int hp in new[] { 100, 250, 400, 600 })
		foreach (int mp in new[] { 20, 300, 1100 })
		foreach (int attackers in new[] { 0, 1, 2, 3 })
		foreach (var cooling in new[] { Cool(), Cool((1229, 2)), Cool((1549, 20), (1512, 6)) })
		{
			var state = new NaturalCombatObservation(9, hp, 669, mp, 1211, false, attackers > 0, distance, Target,
				learned, cooling, NearbyAggressors: attackers, TargetAdjacent: distance <= 3, InEmergency: hp <= 250,
				OpenChainCategory: "P_CHAINA_1TH_1", OpenChainTargetId: Target, ChainExpiresAt: DateTimeOffset.MaxValue);
			NaturalCombatChoice priest = NaturalPriestCombatPolicy.Decide(state, Now);
			NaturalCombatChoice widened = NaturalPriestCombatPolicy.Decide(state, Now, NaturalClericSkills.All);
			Assert.Equal((priest.Action, priest.Skill?.Id, priest.Reason), (widened.Action, widened.Skill?.Id, widened.Reason));
			Assert.Equal(priest.Checks.Select(check => check.Reason), widened.Checks.Select(check => check.Reason));
		}
		var rest = new NaturalPowderRestObservation(9, 300, 669, 900, 1211, false, learned, Cool(), new Dictionary<int, long>());
		Assert.Equal(("light-heal", (ushort?)1839), (NaturalPowderRestPolicy.Decide(rest, Now).Action, NaturalPowderRestPolicy.Decide(rest, Now).Skill?.Id));
		Assert.Equal("sit", NaturalPowderRestPolicy.Decide(rest with { Mp = 300, RecoveringMana = true }, Now).Action);
	}

	private static NaturalCombatChoice Decide(NaturalCombatObservation state) =>
		NaturalPriestCombatPolicy.Decide(state, Now, Catalog);

	private static void AssertLegal(NaturalCombatObservation state, NaturalCombatChoice choice) =>
		Assert.Contains(NaturalPriestCombatPolicy.CandidateActions(state, Now, choice, Catalog),
			candidate => candidate.Action == choice.Action && candidate.SkillId == choice.Skill?.Id && candidate.Legal);

	private static NaturalCombatObservation Cleric(int mp, float distance) =>
		new(10, 1300, 1300, mp, 1300, false, false, distance, Target, Learn(ClericLevel10), Cool());

	private static IReadOnlyDictionary<int, DateTimeOffset> Cool(params (int Group, int Seconds)[] groups) =>
		groups.ToDictionary(group => group.Group, group => Now.AddSeconds(group.Seconds));

	private static IReadOnlyDictionary<int, BotSkill> Learn(params int[] ids) => ids.ToDictionary(id => id,
		id => new BotSkill(checked((ushort)id), 1, 0, 0, 0, 0));

	private static RestCase Rest(int hp, int mp, bool recovering, long powder) => new(hp, mp, recovering, powder);

	private sealed record RestCase(int Hp, int Mp, bool Recovering, long Powder)
	{
		public NaturalPowderRestChoice Choose() => With();

		public NaturalPowderRestChoice With(bool engaged = false, ushort? last = null, int cooldown = 0) =>
			NaturalPowderRestPolicy.Decide(new NaturalPowderRestObservation(10, Hp, 1300, Mp, 1300, Recovering,
				Learn(ClericLevel10), cooldown > 0 ? Cool((NaturalClericSkills.PowderCooldownId, cooldown)) : Cool(),
				new Dictionary<int, long> { [NaturalClericSkills.LesserOdellaPowder] = Powder }, engaged, last), Now);
	}
}
