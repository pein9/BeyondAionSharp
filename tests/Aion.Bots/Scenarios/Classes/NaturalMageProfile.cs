using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;

namespace Aion.Bots.Scenarios.Classes;

/// <summary>
/// CP-46: the Mage, levels 1-9 (docs/natural-class-profiles.md). Its catalog is generated from the shipped data, so the
/// profile is built from the static data of the run. It stands off and casts; it does not kite.
/// </summary>
public static class NaturalMageProfile
{
	private const int TopLevel = 9;

	/// <summary>The Mage's seven active skills. Flame Bolt opens the chain Blaze follows, Ice Chain the one Frozen Shock
	/// follows, each inside 3 s. Erosion, Root and Stone Skin have no chain.</summary>
	private static readonly IReadOnlyDictionary<int, string> Roles = new Dictionary<int, string>
	{
		[1282] = "bolt", [1403] = "blaze", [1363] = "ice", [1226] = "shock", [1447] = "erosion", [1328] = "root", [1155] = "skin",
	};

	/// <summary>
	/// From range: Ice Chain, whose snare keeps the monster away longer, then Frozen Shock at once; Flame Bolt, then Blaze
	/// at once; Flame Bolt has no cooldown and fills the rest. An open follow-up is always cast first, so nothing comes
	/// between an opener and its follow-up. With the monster on it: Erosion first whenever it is ready, the one attack with
	/// no cast time a hit can push back, and never inside a pair. Stone Skin (a 300 s shield for 130 MP) goes up before the
	/// first hit. Root (20 s, but every hit on the rooted monster removes it nine times in ten) is cast only on the way out,
	/// before a retreat.
	/// <para>
	/// The ladder, CP-Q11: the shield scroll at 50% HP and the life potion at or below 75%; the Mage leaves at two
	/// attackers, or at 25% HP with nothing ready. CP-Q12: the mana potion only when the cheapest attack cannot be paid,
	/// and the ladder is read before it, so the life potion wins the delay the two share. The spellbook swings only when
	/// no attack can be paid for.
	/// </para>
	/// </summary>
	private static readonly NaturalRotationRules Rules = new("natural-mage-v1",
		Adjacent: ["erosion", "ice", "shock", "bolt", "blaze"], AtRange: ["ice", "shock", "bolt", "blaze"],
		Upkeep: [new("skin")],
		Recovery: [new(NaturalRecoveryKind.ShieldScroll, 50), new(NaturalRecoveryKind.LifePotion, 75)],
		SwarmAttackers: 2, FleeHpPercent: 25, AutoAttack: NaturalAutoAttack.LastResort, ControlRole: "root");

	/// <summary>CP-Q11: the Minor Life Elixir of trade list 721, bought at 5 or fewer up to 12, and no purchase takes
	/// Kinah below 500.</summary>
	internal static readonly NaturalRestockRules Restock = new(NaturalPriestProfile.PriestLineRestock.Lines, kinahFloor: 500);

	public static NaturalClassProfile Create(StaticData data)
	{
		ArgumentNullException.ThrowIfNull(data);
		IReadOnlyDictionary<int, string> excluded = NaturalSkillCatalog.CommonExcluded;
		NaturalPriestSkill[] skills = NaturalSkillCatalog.Build(data, PlayerClass.MAGE, Roles, excluded);
		NaturalGearRules gear = NaturalClassGearTable.Mage.Rules(NaturalClassLineContract.LoadDefault());
		NaturalFightMovement movement = NaturalPriestProfile.PriestLineMovement;
		NaturalProfileValidator.Require(data, PlayerClass.MAGE, TopLevel, skills, excluded, Rules.Lines(skills), gear);
		return new NaturalClassProfile
		{
			Class = PlayerClass.MAGE,
			Skills = skills,
			Excluded = excluded,
			Combat = new NaturalRotationCombatPolicy(Rules, skills, movement),
			// CP-05: the level 1-9 kit. The shared speed slot holds the casting-speed scroll (Castafodin, the Awakening family).
			HelpItems = new(NaturalHelpItemAllowlist.Starter, NaturalHelpItemAllowlist.StarterMaxLevel),
			// Stone Skin is kept up between fights as the Priest keeps its Blessing.
			Upkeep = [new("skin", "buff-stone-skin")],
			// NR-36: what it shoots with in flight.
			AirAttackRoles = Rules.AtRange,
			PatrolRule = NaturalPatrolRule.Baseline,
			Patrol = NaturalPatrolView.From(Rules, skills, PlayerClass.MAGE),
			RangedHold = NaturalRangedHold.RunOption,
			// CP-Q11 and CP-Q12, answered: a life potion below 90% HP when it is ready and a sit to 90% while it is on its
			// delay; a sit for mana below 40% until 80%. No bandage.
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
