using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;

namespace Aion.Bots.Scenarios.Classes;

/// <summary>
/// CP-47: the Artist, levels 1-9 (docs/natural-class-profiles.md). Its catalog is generated from the shipped data, so the
/// profile is built from the static data of the run. It stands off and casts, as the Mage does; from level 5 it heals
/// itself, as the Priest does.
/// </summary>
public static class NaturalArtistProfile
{
	private const int TopLevel = 9;

	/// <summary>The Artist's five skills that are cast. Every one needs a harp in the main hand. Song of Ice and Soothing
	/// Melody each open a chain whose follow-up is learned after level 9, so no cast here resets anything worth keeping.</summary>
	private static readonly IReadOnlyDictionary<int, string> Roles = new Dictionary<int, string>
	{
		[4408] = "pulse", [4409] = "pulse", [4221] = "ice", [4222] = "ice", [4339] = "heal",
	};

	/// <summary>The three common skills, and Fiery Descant: its activation is CHARGE, and the bot's packet builder sends
	/// a cast with no charge held (CP-Q16).</summary>
	private static readonly IReadOnlyDictionary<int, string> Excluded = new Dictionary<int, string>(NaturalSkillCatalog.CommonExcluded)
	{
		[4300] = "Fiery Descant is a charge skill; the bot holds no charge (CP-Q16).",
	};

	/// <summary>
	/// From range and with the monster on it alike: Song of Ice whenever its 12 s cooldown is over, and Pulse (2 s cooldown)
	/// for the pull and everything between. The harp swings only when no attack can be paid for.
	/// <para>
	/// The ladder, CP-Q11: the shield scroll at 50% HP, the life potion at or below 75%, and Soothing Melody at or below 55%,
	/// the Priest's own number for one attacker. Until the heal is observed in the skill list (level 5) its step is passed
	/// over; from then on the step is read and the attacks keep the heal's mana back. The Artist leaves at two attackers,
	/// or at 25% HP with nothing ready. CP-Q12: the mana potion only when the cheapest attack cannot be paid, after the
	/// ladder.
	/// </para>
	/// </summary>
	private static readonly NaturalRotationRules Rules = new("natural-artist-v1",
		Adjacent: ["ice", "pulse"], AtRange: ["ice", "pulse"],
		Upkeep: [],
		Recovery: [new(NaturalRecoveryKind.ShieldScroll, 50), new(NaturalRecoveryKind.LifePotion, 75), new(NaturalRecoveryKind.Skill, 55, "heal")],
		SwarmAttackers: 2, FleeHpPercent: 25, AutoAttack: NaturalAutoAttack.LastResort);

	/// <summary>CP-Q11: the Minor Life Elixir of trade list 721, bought at 5 or fewer up to 12, and no purchase takes
	/// Kinah below 500.</summary>
	private static readonly NaturalRestockRules Restock = new(NaturalPriestProfile.PriestLineRestock.Lines, kinahFloor: 500);

	public static NaturalClassProfile Create(StaticData data)
	{
		ArgumentNullException.ThrowIfNull(data);
		NaturalPriestSkill[] skills = NaturalSkillCatalog.Build(data, PlayerClass.ARTIST, Roles, Excluded);
		NaturalGearRules gear = NaturalClassGearTable.Artist.Rules(NaturalClassLineContract.LoadDefault());
		NaturalFightMovement movement = NaturalPriestProfile.PriestLineMovement;
		NaturalProfileValidator.Require(data, PlayerClass.ARTIST, TopLevel, skills, Excluded, Rules.Lines(skills), gear);
		return new NaturalClassProfile
		{
			Class = PlayerClass.ARTIST,
			Skills = skills,
			Excluded = Excluded,
			Combat = new NaturalRotationCombatPolicy(Rules, skills, movement),
			// CP-05: the level 1-9 kit. The shared speed slot holds the casting-speed scroll (Castafodin, the Awakening
			// family): every Artist skill has a 1 s cast that the scroll shortens.
			HelpItems = new(NaturalHelpItemAllowlist.Starter, NaturalHelpItemAllowlist.StarterMaxLevel),
			Upkeep = [],
			PatrolRule = NaturalPatrolRule.Baseline,
			RangedHold = NaturalRangedHold.RunOption,
			// Until Soothing Melody is observed in the skill list: a life potion below 90% HP when it is ready and a sit
			// to 90% while it is on its delay (CP-Q11). From then on: Soothing Melody below 90% HP, as the Priest heals.
			// At every level a sit for mana below 40% until 80% (CP-Q12). No bandage.
			Rest = new NaturalRestRules(skills, HealBelowPercent: 90, ManaSitBelowPercent: 40, ManaSitUntilPercent: 80, MaximumQuietSits: 12,
				PotionPlan: new NaturalPotionRestPlan(HpTargetPercent: 90, UsesMana: true)),
			// A stand-off at 22 m with skills that reach 25 m: the Priest line's distances, thresholds and campaign numbers.
			Ranges = NaturalPriestProfile.PriestLineRanges,
			Readiness = NaturalPriestProfile.PriestLineReadiness,
			Movement = movement,
			Campaign = NaturalPriestProfile.PriestLineCampaign,
			Gear = gear,
			Restock = Restock,
		};
	}
}
