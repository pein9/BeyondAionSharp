using Aion.Bots.Scenarios;
using Aion.Bots.Scenarios.Classes;
using Aion.Bots.World;
using Aion.GameServer.TestKit;

namespace Aion.GameServer.Tests;

/// <summary>NA-18, AC-00: the Cleric's powder rest. NR-18: these are the tests of NaturalClericCombatPolicyTests that were
/// not of the removed static fight rule; the catalog is the Cleric's generated one.</summary>
public sealed class NaturalPowderRestPolicyTests
{
	private static readonly DateTimeOffset Now = new(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);
	private static readonly int[] ClericLevel10 = [1838, 1839, 4012, 4013, 1614, 1615, 1684, 1814, 246, 249, 3922, 3939, 4025, 4061, 4083, 4127];

	private static async Task<NaturalPriestSkill[]> CatalogAsync() =>
		NaturalPriestProfile.CreateCleric((await RealStaticData.LoadAsync()).StaticData).Skills;

	[Fact]
	public async Task PenanceBuysManaAtRest()
	{
		NaturalPriestSkill[] catalog = await CatalogAsync();
		var learned = Learn(catalog.Where(skill => skill.MinimumLevel <= 15).Select(skill => (int)skill.Id).ToArray());
		NaturalPowderRestChoice RestAt(int hp, int mp, bool recovering, int penanceCooling = 0, bool engaged = false) =>
			NaturalPowderRestPolicy.Decide(new NaturalPowderRestObservation(15, hp, 1300, mp, 1300, recovering, learned,
				penanceCooling > 0 ? Cool((1200, penanceCooling)) : Cool(),
				new Dictionary<int, long> { [NaturalClericSkills.LesserOdellaPowder] = 30 }, engaged), Now, catalog);
		Assert.Equal(("penance", (ushort?)3867), (RestAt(1300, 300, true).Action, RestAt(1300, 300, true).Skill?.Id));
		// HP below 70%: no Penance; the larger deficit's powder skill instead. Penance cooling: MP Recovery II.
		Assert.Equal(("mp-recovery", (ushort?)250), (RestAt(800, 300, true).Action, RestAt(800, 300, true).Skill?.Id));
		Assert.Equal(("herb", (ushort?)247), (RestAt(500, 600, true).Action, RestAt(500, 600, true).Skill?.Id));
		Assert.Equal(("mp-recovery", (ushort?)250), (RestAt(1300, 300, true, 60).Action, RestAt(1300, 300, true, 60).Skill?.Id));
		Assert.Equal("done", RestAt(1300, 1200, false).Action);
		Assert.Equal("defend", RestAt(1300, 300, true, engaged: true).Action);
	}

	[Fact]
	public async Task PowderRestAlternatesOnTheSharedCooldownAndSitsOnlyAsAFallback()
	{
		NaturalPriestSkill[] catalog = await CatalogAsync();
		RestCase Rest(int hp, int mp, bool recovering, long powder) => new(catalog, hp, mp, recovering, powder);
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
		// Engaged at rest: fight first, a hit would cancel the 4 s cast.
		Assert.Equal("defend", Rest(700, 200, true, 30).With(engaged: true).Action);
	}

	private static IReadOnlyDictionary<int, DateTimeOffset> Cool(params (int Group, int Seconds)[] groups) =>
		groups.ToDictionary(group => group.Group, group => Now.AddSeconds(group.Seconds));

	private static IReadOnlyDictionary<int, BotSkill> Learn(params int[] ids) => ids.ToDictionary(id => id,
		id => new BotSkill(checked((ushort)id), 1, 0, 0, 0, 0));

	private sealed record RestCase(NaturalPriestSkill[] Catalog, int Hp, int Mp, bool Recovering, long Powder)
	{
		public NaturalPowderRestChoice Choose() => With();

		public NaturalPowderRestChoice With(bool engaged = false, ushort? last = null, int cooldown = 0) =>
			NaturalPowderRestPolicy.Decide(new NaturalPowderRestObservation(10, Hp, 1300, Mp, 1300, Recovering,
				Learn(ClericLevel10), cooldown > 0 ? Cool((NaturalClericSkills.PowderCooldownId, cooldown)) : Cool(),
				new Dictionary<int, long> { [NaturalClericSkills.LesserOdellaPowder] = Powder }, engaged, last), Now, Catalog);
	}
}
