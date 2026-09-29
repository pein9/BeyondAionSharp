using System.Xml.Linq;
using Aion.Bots.Scenarios;
using Aion.Bots.World;

namespace Aion.GameServer.Tests;

/// <summary>NA-19: the buff-ourself check's help scrolls and the Anti-Shock shield's place in combat.</summary>
public sealed class NaturalHelpItemPolicyTests
{
	private static readonly DateTimeOffset Now = new(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);
	private const int LesserAwakening = 164000132, Awakening = 164000133, LesserCourage = 164000071;
	private const int LesserRunning = 164000074, MovementSpeed = 164000033, LesserShield = 164000067, Shield = 164000068;

	[Fact]
	public void CatalogAgreesWithShippedItemsAndSkills()
	{
		string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
		XDocument items = XDocument.Load(Path.Combine(root, "game-server/data/static_data/items/item_templates.xml"));
		XDocument skills = XDocument.Load(Path.Combine(root, "game-server/data/static_data/skills/skill_templates.xml"));
		foreach (NaturalHelpItem help in NaturalHelpItemPolicy.All)
		{
			XElement item = Assert.Single(items.Descendants("item_template"), node => (int?)node.Attribute("id") == help.ItemId);
			Assert.Equal(help.ItemLevel, (int?)item.Attribute("level"));
			// A required level (only Fine Anti-Shock: 50) never exceeds the first level the tier rule picks it at.
			int firstPick = help.ItemLevel - (help.Family == "anti-shock" ? NaturalHelpItemPolicy.ShieldLevelAllowance : 0);
			Assert.True(((string?)item.Attribute("restrict"))?.Split(' ').Select(int.Parse).Max() is not int required || required <= firstPick,
				$"{help.ItemId} requires a level above {firstPick}.");
			Assert.Equal(help.SkillId, (int?)item.Element("actions")?.Element("skilluse")?.Attribute("skillid"));
			Assert.Equal(help.UseDelayId, (int?)item.Element("uselimits")?.Attribute("usedelayid"));
			Assert.Equal(help.UseDelayMillis, (int?)item.Element("uselimits")?.Attribute("usedelay"));
			XElement skill = Assert.Single(skills.Descendants("skill_template"), node => (int?)node.Attribute("skill_id") == help.SkillId);
			XElement effect = skill.Element("effects")!.Elements().First(node => node.Attribute("duration2") != null);
			Assert.Equal(help.DurationMillis, (int?)effect.Attribute("duration2"));
			Assert.Equal(help.EffectSlot, (int?)effect.Attribute("effectid"));
		}
	}

	[Theory]
	[InlineData(10, LesserAwakening)]
	[InlineData(19, LesserAwakening)]
	[InlineData(20, Awakening)]
	public void AwakeningUsesTheHighestTierAtOrBelowTheLevel(int level, int expected) =>
		Assert.Equal(expected, Buffs(State(level, owned: [LesserAwakening, Awakening])).Item?.ItemId);

	[Theory]
	[InlineData(10, LesserShield)]
	[InlineData(19, LesserShield)]
	[InlineData(20, Shield)]
	[InlineData(9, null)]
	public void AntiShockAllowsTenLevelsAbove(int level, int? expected) =>
		Assert.Equal(expected, NaturalHelpItemPolicy.DecideShield(State(level, owned: [LesserShield, Shield], hp: 50), Now).Item?.ItemId);

	[Fact]
	public void AwakeningIsRefreshedOnlyInsideTheWindowAndCourageIsLeftToExpire()
	{
		NaturalHelpItemObservation owned = State(10, owned: [LesserAwakening, LesserCourage]);
		Assert.Null(Buffs(owned with { Effects = [Effect(9965, 25_000)] }).Item);
		Assert.Equal(LesserAwakening, Buffs(owned with { Effects = [Effect(9965, 15_000)] }).Item?.ItemId);
		// The snapshot's time is as of when it arrived: 60 s then, 45 s ago.
		Assert.Equal(LesserAwakening, Buffs(owned with { Effects = [Effect(9965, 60_000)], EffectsAgeMillis = 45_000 }).Item?.ItemId);
		Assert.Null(Buffs(owned with { Effects = [Effect(9959, 5_000)] }).Item);
		// Courage is never chosen, even when it is the only scroll owned.
		Assert.Null(Buffs(State(10, owned: [LesserCourage])).Item);
	}

	[Fact]
	public void SharedUseDelayGroupsHoldTheScroll()
	{
		NaturalHelpItemObservation owned = State(10, owned: [LesserAwakening, LesserRunning, LesserShield], hp: 40);
		Assert.Null(Buffs(owned with { UseDelays = Delay((34, 10)) }).Item);
		Assert.Equal(LesserAwakening, Buffs(owned with { UseDelays = Delay((35, 10), (32, 50)) }).Item?.ItemId);
		Assert.Null(NaturalHelpItemPolicy.DecideShield(owned with { UseDelays = Delay((32, 50)) }, Now).Item);
		Assert.Equal(LesserShield, NaturalHelpItemPolicy.DecideShield(owned with { UseDelays = Delay((34, 10)) }, Now).Item?.ItemId);
		NaturalHelpItemObservation awake = owned with { Effects = [Effect(9965, 200_000)] };
		Assert.Null(Buffs(awake with { UseDelays = Delay((35, 10)) }, NaturalHelpTrigger.TravelLeg, 400).Item);
	}

	[Fact]
	public void RunningOnlyForALongLegAndNeverOnTopOfAnotherSpeedEffect()
	{
		NaturalHelpItemObservation owned = State(10, owned: [LesserRunning, MovementSpeed]) with { Effects = [] };
		Assert.Null(Buffs(owned, NaturalHelpTrigger.TravelLeg, 149).Item);
		Assert.Equal(LesserRunning, Buffs(owned, NaturalHelpTrigger.TravelLeg, 150).Item?.ItemId);
		Assert.Equal(LesserRunning, Buffs(owned with { CrossMapTravel = true }, NaturalHelpTrigger.TravelLeg, 20).Item?.ItemId);
		Assert.Null(Buffs(owned, NaturalHelpTrigger.PrePull, 400).Item);
		// One speed family: the Movement Speed scroll or a running Running effect already fills slot 30182.
		Assert.Null(Buffs(owned with { Effects = [Effect(9943, 500_000)] }, NaturalHelpTrigger.TravelLeg, 400).Item);
		Assert.Null(Buffs(owned with { Effects = [Effect(9960, 100_000)] }, NaturalHelpTrigger.TravelLeg, 400).Item);
		Assert.Equal(LesserRunning, Buffs(owned with { Effects = [Effect(9960, 10_000)] }, NaturalHelpTrigger.TravelLeg, 400).Item?.ItemId);
		// The Movement Speed scroll itself is never chosen.
		Assert.Null(Buffs(State(15, owned: [MovementSpeed]), NaturalHelpTrigger.TravelLeg, 400).Item);
	}

	[Fact]
	public void NoItemUseMidCastInACutsceneInFlightDeadStunnedOrUnobserved()
	{
		NaturalHelpItemObservation owned = State(10, owned: [LesserAwakening, LesserShield], hp: 40);
		Assert.NotNull(Buffs(owned).Item);
		Assert.NotNull(NaturalHelpItemPolicy.DecideShield(owned, Now).Item);
		foreach (NaturalHelpItemObservation blocked in new[]
		{
			owned with { Casting = true }, owned with { Cutscene = true }, owned with { Flying = true },
			owned with { Dead = true }, owned with { Disabled = true }, owned with { Effects = null },
		})
		{
			Assert.Null(Buffs(blocked).Item);
			Assert.Null(NaturalHelpItemPolicy.DecideShield(blocked, Now).Item);
		}
		// In combat the buff check never fires; only the shield, through the combat policy.
		Assert.Null(Buffs(owned, NaturalHelpTrigger.Combat).Item);
	}

	[Fact]
	public void TheShieldFiresAtHalfHpWithoutAnActiveShield()
	{
		NaturalHelpItemObservation owned = State(10, owned: [LesserShield]);
		Assert.Null(NaturalHelpItemPolicy.DecideShield(owned with { Hp = 651 }, Now).Item);
		Assert.Equal(LesserShield, NaturalHelpItemPolicy.DecideShield(owned with { Hp = 650 }, Now).Item?.ItemId);
		Assert.Null(NaturalHelpItemPolicy.DecideShield(owned with { Hp = 300, Effects = [Effect(9954, 10_000)] }, Now).Item);
	}

	[Fact]
	public void TheShieldComesAfterTheRetreatRulesAndBeforeSalvationAndPotions()
	{
		var learned = new[] { 1838, 1839, 4012, 4013, 1614, 1615, 1684, 1814, 3922, 3939, 4127 }
			.ToDictionary(id => id, id => new BotSkill(checked((ushort)id), 1, 0, 0, 0, 0));
		var fight = new NaturalCombatObservation(10, 650, 1300, 800, 1300, false, true, 2, 71, learned,
			new Dictionary<int, DateTimeOffset>(), NearbyAggressors: 1, TargetAdjacent: true, Dp: 2000,
			HasHotPotion: true, HotPotionReady: true, HasLifePotion: true, LifePotionReady: true, InEmergency: true,
			ShieldScrollReady: true);
		NaturalCombatChoice shield = NaturalPriestCombatPolicy.Decide(fight, Now, NaturalClericSkills.All);
		Assert.Equal("shield-scroll", shield.Action);
		Assert.Contains(NaturalPriestCombatPolicy.CandidateActions(fight, Now, shield, NaturalClericSkills.All),
			candidate => candidate.Action == "shield-scroll" && candidate.Legal);
		// Without the scroll, Salvation is the emergency choice as before.
		Assert.Equal((ushort)3922, NaturalPriestCombatPolicy.Decide(fight with { ShieldScrollReady = false }, Now, NaturalClericSkills.All).Skill?.Id);
		// The swarm retreat (here Root, then the retreat) still comes first.
		Assert.Equal((ushort)4127, NaturalPriestCombatPolicy.Decide(fight with { NearbyAggressors = 3 }, Now, NaturalClericSkills.All).Skill?.Id);
		// At 30% with nothing left to heal with, the retreat still wins.
		var drained = fight with
		{
			Hp = 300, Mp = 0, Dp = 0, HasHotPotion = false, HasLifePotion = false, Cooldowns = new Dictionary<int, DateTimeOffset>(),
		};
		Assert.Equal("retreat", NaturalPriestCombatPolicy.Decide(drained, Now, NaturalClericSkills.All).Action);
	}

	[Fact]
	public void NothingOwnedMeansNothingUsedAndCombatUnchanged()
	{
		NaturalHelpItemObservation empty = State(10, owned: [], hp: 20);
		foreach (NaturalHelpTrigger trigger in Enum.GetValues<NaturalHelpTrigger>())
			Assert.Null(Buffs(empty with { CrossMapTravel = true }, trigger, 1000).Item);
		Assert.Null(NaturalHelpItemPolicy.DecideShield(empty, Now).Item);
		var learned = new[] { 1838, 1839, 4012, 4013 }.ToDictionary(id => id, id => new BotSkill(checked((ushort)id), 1, 0, 0, 0, 0));
		var state = new NaturalCombatObservation(9, 300, 669, 500, 1211, false, true, 2, 71, learned,
			new Dictionary<int, DateTimeOffset>(), NearbyAggressors: 1, TargetAdjacent: true);
		Assert.Equal(NaturalPriestCombatPolicy.Decide(state, Now).Reason,
			NaturalPriestCombatPolicy.Decide(state with { ShieldScrollReady = false }, Now).Reason);
		Assert.Contains(NaturalPriestCombatPolicy.CandidateActions(state, Now, NaturalPriestCombatPolicy.Decide(state, Now)),
			candidate => candidate.Action == "shield-scroll" && !candidate.Legal);
	}

	private static NaturalHelpItemChoice Buffs(NaturalHelpItemObservation state,
		NaturalHelpTrigger trigger = NaturalHelpTrigger.PrePull, float meters = 0) =>
		NaturalHelpItemPolicy.DecideBuffs(state with { PlannedTravelMeters = meters }, Now, trigger);

	private static NaturalHelpItemObservation State(int level, int[] owned, int hp = 100) =>
		new(level, hp * 13, 1300, false, false, false, false, false, [], 0,
			owned.ToDictionary(id => id, _ => 5L), new Dictionary<int, DateTimeOffset>());

	private static BotVisibleEffect Effect(int skillId, int remainingMillis) => new(1, skillId, 1, 0, remainingMillis);

	private static IReadOnlyDictionary<int, DateTimeOffset> Delay(params (int Group, int Seconds)[] groups) =>
		groups.ToDictionary(group => group.Group, group => Now.AddSeconds(group.Seconds));
}
