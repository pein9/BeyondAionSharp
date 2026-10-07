using Aion.Bots.Scenarios;
using Aion.Bots.Scenarios.Classes;
using Aion.Bots.World;

namespace Aion.GameServer.Tests;

/// <summary>
/// CP-17: the rest rules on the class profile answer as the journey's rest loop decided before they were lifted out of it
/// (RestAsync at commit 1cdc27aaa: the mana sit below 50% until 80%, the heal below 90% HP, 12 quiet sits).
/// </summary>
public sealed class NaturalRestRulesTests
{
	private static readonly DateTimeOffset Now = new(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);
	private static readonly int[] PriestLevel9 = [1838, 1839, 4012, 4013, 1614, 1615, 1684, 1814];
	private static readonly int[] ClericLevel10 = [.. PriestLevel9, 246, 249, 3922, 3939, 4025, 4061, 4083, 4127];

	[Fact]
	public void EveryStateDecidesAsTheRestLoopDid()
	{
		(NaturalClassProfile Profile, int Level, int[] Learned)[] characters =
		[
			(NaturalPriestProfile.Priest, 9, PriestLevel9),
			(NaturalPriestProfile.Priest, 4, [4012]),
			(NaturalPriestProfile.Cleric, 10, ClericLevel10),
			(NaturalPriestProfile.Cleric, 24, NaturalClericSkills.All.Where(skill => skill.MinimumLevel <= 24).Select(skill => (int)skill.Id).ToArray()),
		];
		int states = 0;
		var actions = new HashSet<string>();
		foreach ((NaturalClassProfile profile, int level, int[] learnedIds) in characters)
		{
			IReadOnlyDictionary<int, BotSkill> learned = learnedIds.ToDictionary(id => id, id => new BotSkill(checked((ushort)id), 1, 0, 0, 0, 0));
			foreach (int hp in new[] { 50, 499, 500, 699, 700, 899, 900, 1000 })
			foreach (int mp in new[] { 0, 10, 60, 499, 500, 799, 800, 1000 })
			foreach (bool recovering in new[] { false, true })
			foreach (int quiet in new[] { 0, 11, 12, 13 })
			foreach (long powder in new[] { 0L, 1L, 2L, 30L })
			foreach (bool cooling in new[] { false, true })
			foreach (ushort? last in new ushort?[] { null, 246, 249 })
			{
				var state = new NaturalRestObservation(level, hp, 1000, mp, 1000, recovering, quiet, learned,
					cooling ? new Dictionary<int, DateTimeOffset> { [NaturalClericSkills.PowderCooldownId] = Now.AddSeconds(10) } : new Dictionary<int, DateTimeOffset>(),
					new Dictionary<int, long> { [NaturalClericSkills.LesserOdellaPowder] = powder }, last, Now);
				NaturalRestDecision expected = Before(state, profile.Skills);
				NaturalRestDecision actual = profile.Rest.Decide(state);
				Assert.Equal(expected, actual);
				actions.Add(actual.Action);
				states++;
			}
		}
		Assert.Equal(4 * 8 * 8 * 2 * 4 * 4 * 2 * 3, states);
		Assert.Equal(new[] { "blocked", "cast-heal", "done", "powder", "sit-for-mana" }, actions.Order());
	}

	[Fact]
	public void ThePriestHealsBelowNinetyPercentAndSitsOnlyForMana()
	{
		NaturalRestRules rules = NaturalPriestProfile.Priest.Rest;
		Assert.Equal((90, 50, 80, 12), (rules.HealBelowPercent, rules.ManaSitBelowPercent, rules.ManaSitUntilPercent, rules.MaximumQuietSits));
		Assert.Same(NaturalPriestSkills.All, rules.Skills);
		Assert.Same(NaturalClericSkills.All, NaturalPriestProfile.Cleric.Rest.Skills);
		Assert.Equal(rules with { Skills = NaturalClericSkills.All }, NaturalPriestProfile.Cleric.Rest);

		// HP: Healing Light II below 90%, nothing at 90%.
		Assert.Equal((NaturalRestRules.CastHeal, (ushort?)1839), Step(rules, Priest(hp: 899, mp: 1000)));
		Assert.Equal((NaturalRestRules.Done, null), Step(rules, Priest(hp: 900, mp: 1000)));
		// Missing HP alone never sits, and a heal the Priest cannot pay for stops the rest.
		Assert.Equal((NaturalRestRules.CastHeal, (ushort?)1839), Step(rules, Priest(hp: 50, mp: 500)));
		NaturalRestDecision broke = rules.Decide(Priest(hp: 50, mp: 500) with { Learned = Learn(4012) });
		Assert.Equal((NaturalRestRules.Blocked, NaturalRestRules.NoSelfHeal), (broke.Action, broke.BlockedReason));
		Assert.Equal("Priest has mana but no client-observed usable self-heal between fights.", NaturalRestRules.NoSelfHeal);

		// Mana: the sit starts below 50% and holds until 80%, whatever HP is.
		NaturalRestDecision low = rules.Decide(Priest(hp: 300, mp: 499));
		Assert.Equal((NaturalRestRules.SitForMana, true, false), (low.Action, low.RecoveringMana, low.ManaRecovered));
		Assert.Equal(NaturalRestRules.CastHeal, rules.Decide(Priest(hp: 300, mp: 500)).Action);
		NaturalRestDecision still = rules.Decide(Priest(hp: 1000, mp: 799, recovering: true));
		Assert.Equal((NaturalRestRules.SitForMana, true, false), (still.Action, still.RecoveringMana, still.ManaRecovered));
		NaturalRestDecision recovered = rules.Decide(Priest(hp: 1000, mp: 800, recovering: true));
		Assert.Equal((NaturalRestRules.Done, false, true), (recovered.Action, recovered.RecoveringMana, recovered.ManaRecovered));
		// The sit is bounded: the thirteenth is refused after twelve quiet ones.
		Assert.Equal(NaturalRestRules.SitForMana, rules.Decide(Priest(hp: 1000, mp: 100, quiet: 11)).Action);
		NaturalRestDecision bounded = rules.Decide(Priest(hp: 1000, mp: 100, quiet: 12));
		Assert.Equal((NaturalRestRules.Blocked, NaturalRestRules.NotRecovered), (bounded.Action, bounded.BlockedReason));
		Assert.Equal("Priest could not recover HP/MP before the next pull within bounded healing and mana-rest attempts.", NaturalRestRules.NotRecovered);
		// No powder skill is learned, so the powder policy is not asked.
		Assert.Null(low.PowderChoice);
	}

	[Fact]
	public void TheClericRestsWithPowderFirstAndFallsBackToTheSameRule()
	{
		NaturalRestRules rules = NaturalPriestProfile.Cleric.Rest;
		NaturalRestObservation hurt = Priest(hp: 700, mp: 200) with { Level = 10, Learned = Learn(ClericLevel10) };
		// Powder first: the powder policy's own choice is returned for the trace.
		NaturalRestDecision powder = rules.Decide(hurt);
		Assert.Equal(NaturalRestRules.Powder, powder.Action);
		Assert.Equal(NaturalPowderRestPolicy.Decide(new NaturalPowderRestObservation(10, 700, 1000, 200, 1000, true, hurt.Learned, hurt.Cooldowns,
			hurt.ItemCounts, LastPowderSkillId: null), Now, NaturalClericSkills.All), powder.PowderChoice);
		Assert.Same(powder.PowderChoice!.Skill, powder.Skill);
		Assert.True(powder.Skill!.IsRestSkill);
		// Out of powder: the powder policy answers, and the rule below it decides: sit for mana, heal for HP.
		NaturalRestObservation empty = hurt with { ItemCounts = new Dictionary<int, long>() };
		NaturalRestDecision sit = rules.Decide(empty);
		Assert.Equal((NaturalRestRules.SitForMana, "sit"), (sit.Action, sit.PowderChoice?.Action));
		NaturalRestDecision heal = rules.Decide(empty with { Mp = 900 });
		Assert.Equal((NaturalRestRules.CastHeal, "light-heal"), (heal.Action, heal.PowderChoice?.Action));
		NaturalRestDecision done = rules.Decide(empty with { Hp = 1000, Mp = 900 });
		Assert.Equal((NaturalRestRules.Done, "done"), (done.Action, done.PowderChoice?.Action));
	}

	/// <summary>The decisions of RestAsync as they stood inline, with their literals (Combat.cs:830-901 at 1cdc27aaa).</summary>
	private static NaturalRestDecision Before(NaturalRestObservation world, NaturalPriestSkill[] catalog)
	{
		bool recoveringMana = world.RecoveringMana, relocate = false;
		if (world.Mp * 100 < world.MaxMp * 50) recoveringMana = true;
		if (recoveringMana && world.Mp * 100 >= world.MaxMp * 80)
		{
			recoveringMana = false;
			relocate = true;
		}
		NaturalPowderRestChoice? powder = null;
		if (catalog.Any(skill => skill.IsPowderRest && world.Learned.ContainsKey(skill.Id)))
		{
			powder = NaturalPowderRestPolicy.Decide(new NaturalPowderRestObservation(
				world.Level, world.Hp, world.MaxHp, world.Mp, world.MaxMp, recoveringMana, world.Learned,
				world.Cooldowns, world.ItemCounts, LastPowderSkillId: world.LastPowderSkillId), world.Now, catalog);
			if (powder.Skill is { IsRestSkill: true } restSkill) return new("powder", restSkill, recoveringMana, relocate, powder, null);
		}
		if (!recoveringMana)
		{
			if (world.Hp * 100 < world.MaxHp * 90)
			{
				NaturalPriestSkill? heal = NaturalPriestSkills.Best("heal", world.Level, world.Learned, catalog);
				if (heal == null || world.Mp < heal.ManaCost)
					return new("blocked", null, recoveringMana, relocate, powder, "Priest has mana but no client-observed usable self-heal between fights.");
				return new("cast-heal", heal, recoveringMana, relocate, powder, null);
			}
			return new("done", null, recoveringMana, relocate, powder, null);
		}
		if (world.QuietSits >= 12)
			return new("blocked", null, recoveringMana, relocate, powder,
				"Priest could not recover HP/MP before the next pull within bounded healing and mana-rest attempts.");
		return new("sit-for-mana", null, recoveringMana, relocate, powder, null);
	}

	private static (string Action, ushort? SkillId) Step(NaturalRestRules rules, NaturalRestObservation state)
	{
		NaturalRestDecision decision = rules.Decide(state);
		return (decision.Action, decision.Skill?.Id);
	}

	private static NaturalRestObservation Priest(int hp, int mp, bool recovering = false, int quiet = 0) =>
		new(9, hp, 1000, mp, 1000, recovering, quiet, Learn(PriestLevel9), new Dictionary<int, DateTimeOffset>(),
			new Dictionary<int, long> { [NaturalClericSkills.LesserOdellaPowder] = 30 }, null, Now);

	private static IReadOnlyDictionary<int, BotSkill> Learn(params int[] ids) => ids.ToDictionary(id => id,
		id => new BotSkill(checked((ushort)id), 1, 0, 0, 0, 0));
}
