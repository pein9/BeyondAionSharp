using System.Xml.Linq;
using Aion.Bots.Scenarios;
using Aion.Bots.World;

namespace Aion.GameServer.Tests;

public sealed class NaturalPriestCombatPolicyTests
{
	private static readonly DateTimeOffset Now = new(2026, 9, 22, 12, 0, 0, TimeSpan.Zero);

	[Theory]
	[InlineData(1, 1838, 4012)]
	[InlineData(3, 1838, 1614)]
	[InlineData(5, 1684, 4012)]
	[InlineData(6, 1839, 4013)]
	[InlineData(7, 1814, 4013)]
	[InlineData(8, 1615, 4013)]
	[InlineData(9, 1839, 4013)]
	public void CatalogFollowsShippedPriestTree(int level, int expectedUnlocked, int expectedPreferred)
	{
		var learned = NaturalPriestSkills.All.Where(skill => skill.MinimumLevel <= level)
			.ToDictionary(skill => (int)skill.Id, skill => new BotSkill(skill.Id, 1, 0, 0, 0, 0));
		Assert.Contains(expectedUnlocked, learned.Keys);
		Assert.Equal(expectedPreferred, NaturalPriestSkills.Best(
			expectedPreferred is 1614 or 1615 ? "hallowed" : "smite", level, learned)!.Id);
		Assert.Equal(level < 6 ? 1838 : 1839, NaturalPriestSkills.Best("heal", level, learned)!.Id);
	}

	[Fact]
	public void CatalogAgreesWithShippedSkillTreeAndTemplates()
	{
		string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
		XDocument tree = XDocument.Load(Path.Combine(root, "game-server/data/static_data/skill_tree/skill_tree.xml"));
		XDocument templates = XDocument.Load(Path.Combine(root, "game-server/data/static_data/skills/skill_templates.xml"));
		foreach (NaturalPriestSkill skill in NaturalPriestSkills.All)
		{
			XElement entry = Assert.Single(tree.Descendants("skill"), node => (int?)node.Attribute("skillId") == skill.Id);
			Assert.Equal("PRIEST", (string?)entry.Attribute("classId"));
			Assert.Equal(skill.MinimumLevel, (int?)entry.Attribute("minLevel"));
			Assert.Equal("true", (string?)entry.Attribute("autolearn"));
			XElement template = Assert.Single(templates.Descendants("skill_template"),
				node => (int?)node.Attribute("skill_id") == skill.Id);
			Assert.Equal(skill.CooldownId, (int?)template.Attribute("cooldownId"));
			Assert.Equal(skill.CooldownDeciseconds, (int?)template.Attribute("cooldown"));
			Assert.Equal(skill.Range, (float?)template.Element("properties")?.Attribute("first_target_range"));
			Assert.Equal(skill.ManaCost, (int?)template.Element("endconditions")?.Element("mp")?.Attribute("value"));
			XElement? chain = template.Element("startconditions")?.Element("chain");
			Assert.Equal(skill.ChainCategory, (string?)chain?.Attribute("category"));
			Assert.Equal(skill.RequiresChainCategory, (string?)chain?.Attribute("precategory"));
		}
	}

	[Fact]
	public void ObservedLearningAndSharedRankCooldownGateCasting()
	{
		NaturalCombatObservation atSix = Observe(6, 100, 100, 100, 100, [1838, 4012, 1839, 4013], 12);
		Assert.Equal((ushort)4013, NaturalPriestCombatPolicy.Decide(atSix, Now).Skill?.Id);
		Assert.Equal((ushort)4012, NaturalPriestCombatPolicy.Decide(atSix with
		{
			Learned = Learn(1838, 4012),
		}, Now).Skill?.Id);
		NaturalCombatChoice gated = NaturalPriestCombatPolicy.Decide(atSix with
		{
			Cooldowns = new Dictionary<int, DateTimeOffset> { [1229] = Now.AddSeconds(2) },
		}, Now);
		Assert.Equal("wait", gated.Action);
		Assert.Contains(gated.Checks, check => check.Rule == "skill-4013" && check.Verdict == "skip");
	}

	[Fact]
	public void HealReserveRestAggressionAndRetreatAreDeterministic()
	{
		var state = Observe(1, 40, 100, 40, 100, [1838, 4012], 10);
		Assert.Equal("cast-self", NaturalPriestCombatPolicy.Decide(state, Now).Action);
		Assert.Equal("cast-self", NaturalPriestCombatPolicy.Decide(state with { Hp = 70 }, Now).Action);
		Assert.Equal("cast-self", NaturalPriestCombatPolicy.Decide(state with
		{
			Hp = 31, Aggro = true, NearbyAggressors = 2,
		}, Now).Action);
		Assert.Equal("retreat", NaturalPriestCombatPolicy.Decide(state with
		{
			Hp = 30, Aggro = true, NearbyAggressors = 2,
		}, Now).Action);
		Assert.Equal("retreat", NaturalPriestCombatPolicy.Decide(state with
		{
			Hp = 30, Aggro = true, NearbyAggressors = 1,
		}, Now).Action);
		Assert.Equal("retreat", NaturalPriestCombatPolicy.Decide(state with
		{
			Hp = 30, Aggro = true, NearbyAggressors = 0,
		}, Now).Action);
		Assert.Equal("retreat", NaturalPriestCombatPolicy.Decide(state with
		{
			Hp = 30, Aggro = false, NearbyAggressors = 0,
		}, Now).Action);
		Assert.Equal("cast-self", NaturalPriestCombatPolicy.Decide(state with
		{
			Hp = 31, Aggro = true, NearbyAggressors = 1,
		}, Now).Action);
		Assert.Equal("cast-target", NaturalPriestCombatPolicy.Decide(state with { Hp = 71 }, Now).Action);
		Assert.Equal("cast-target", NaturalPriestCombatPolicy.Decide(state with { Hp = 80 }, Now).Action);
		Assert.Equal("cast-target", NaturalPriestCombatPolicy.Decide(state with { Hp = 85 }, Now).Action);
		Assert.Equal("retreat", NaturalPriestCombatPolicy.Decide(state with { Hp = 20, Mp = 0, Aggro = true }, Now).Action);
		Assert.Equal("attack", NaturalPriestCombatPolicy.Decide(state with { Hp = 100, Mp = 13, TargetDistance = 2 }, Now).Action);
		Assert.Equal("rest", NaturalPriestCombatPolicy.Decide(state with { Hp = 70, TargetObjectId = null, TargetDistance = null }, Now).Action);
		Assert.Equal("cast-self", NaturalPriestCombatPolicy.Decide(state with { Hp = 70, TargetObjectId = null, TargetDistance = null, Aggro = true }, Now).Action);
		Assert.Equal("cast-self", NaturalPriestCombatPolicy.Decide(state with
		{
			Hp = 70, TargetObjectId = null, TargetDistance = null, Aggro = false, NearbyAggressors = 1,
		}, Now).Action);
		Assert.Equal("retreat", NaturalPriestCombatPolicy.Decide(state with
		{
			Hp = 30, TargetObjectId = null, TargetDistance = null, Aggro = false, NearbyAggressors = 1,
		}, Now).Action);
		Assert.Equal("ready", NaturalPriestCombatPolicy.Decide(state with { Hp = 95, Mp = 95, TargetObjectId = null, TargetDistance = null }, Now).Action);
		Assert.Equal("revive", NaturalPriestCombatPolicy.Decide(state with { Dead = true }, Now).Action);
	}

	[Fact]
	public void OwnedConsumablesAndObservedBuffAreGated()
	{
		var state = Observe(5, 20, 100, 0, 100, [1838, 4012, 1684], 2) with
		{
			TargetObjectId = null, TargetDistance = null,
		};
		Assert.Equal("life-potion", NaturalPriestCombatPolicy.Decide(state with
		{
			HasLifePotion = true, LifePotionReady = true,
		}, Now).Action);
		Assert.Equal("retreat", NaturalPriestCombatPolicy.Decide(state with
		{
			Aggro = true, HasLifePotion = true, LifePotionReady = false,
		}, Now).Action);
		Assert.Equal("mana-potion", NaturalPriestCombatPolicy.Decide(state with
		{
			Hp = 90, HasManaPotion = true, ManaPotionReady = true,
		}, Now).Action);
		var noBuff = state with { Hp = 100, Mp = 100, TargetObjectId = null, TargetDistance = null,
			HasBlessing = false };
		Assert.Equal((ushort)1684, NaturalPriestCombatPolicy.Decide(noBuff, Now).Skill?.Id);
		Assert.Equal("ready", NaturalPriestCombatPolicy.Decide(noBuff with { HasBlessing = true }, Now).Action);
	}

	[Fact]
	public void ObservedNearDefeatAllowsOneFinishingSmiteAfterHealingButNeverOverridesRetreat()
	{
		NaturalCombatObservation state = Observe(8, 32, 100, 100, 100,
			[1839, 4013], 3) with { TargetHpPercent = 13, HasHealedThisFight = true };
		NaturalCombatChoice finisher = NaturalPriestCombatPolicy.Decide(state, Now);
		Assert.Equal("cast-target", finisher.Action);
		Assert.Equal((ushort)4013, finisher.Skill?.Id);
		Assert.Equal("cast-self", NaturalPriestCombatPolicy.Decide(state with
		{
			HasHealedThisFight = false,
		}, Now).Action);
		Assert.Equal("retreat", NaturalPriestCombatPolicy.Decide(state with
		{
			Hp = 30,
		}, Now).Action);
	}

	[Fact]
	public void FollowupRequiresObservedWindowAndItsOwnLongerCooldown()
	{
		var followup = new NaturalPriestSkill(9999, 1, "followup", 5, 25, 9999, 80,
			"P_CHAINA_2TH_1", "P_CHAINA_1TH_1", 3000);
		NaturalPriestSkill[] catalog = [.. NaturalPriestSkills.All, followup];
		var state = Observe(1, 100, 100, 100, 100, [1838, 4012, 9999], 10);
		Assert.Equal((ushort)4012, NaturalPriestCombatPolicy.Decide(state, Now, catalog).Skill?.Id);
		var opened = state with { OpenChainCategory = "P_CHAINA_1TH_1", OpenChainTargetId = 71,
			ChainExpiresAt = Now.AddSeconds(3), Cooldowns = new Dictionary<int, DateTimeOffset> { [1229] = Now.AddSeconds(2) } };
		Assert.Equal((ushort)9999, NaturalPriestCombatPolicy.Decide(opened, Now, catalog).Skill?.Id);
		Assert.Equal("wait", NaturalPriestCombatPolicy.Decide(opened with
		{
			Cooldowns = new Dictionary<int, DateTimeOffset> { [1229] = Now.AddSeconds(2), [9999] = Now.AddSeconds(8) },
		}, Now).Action);
		Assert.Equal("wait", NaturalPriestCombatPolicy.Decide(opened with { ChainExpiresAt = Now }, Now, catalog).Action);
		Assert.Equal("approach", NaturalPriestCombatPolicy.Decide(opened with
		{
			TargetDistance = 26,
			Cooldowns = new Dictionary<int, DateTimeOffset> { [1229] = Now.AddSeconds(2) },
		}, Now, catalog).Action);
	}

	private static NaturalCombatObservation Observe(int level, int hp, int maxHp, int mp, int maxMp,
		int[] learned, float? distance) => new(level, hp, maxHp, mp, maxMp, false, false, distance, 71,
		Learn(learned), new Dictionary<int, DateTimeOffset>());

	private static IReadOnlyDictionary<int, BotSkill> Learn(params int[] ids) => ids.ToDictionary(id => id,
		id => new BotSkill(checked((ushort)id), 1, 0, 0, 0, 0));
}
