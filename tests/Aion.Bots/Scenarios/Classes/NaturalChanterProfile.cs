using Aion.GameServer.Model;

namespace Aion.Bots.Scenarios.Classes;

/// <summary>
/// CP-32: the Chanter, the Priest's other choice at Ascension (docs/natural-class-profiles.md). The line priest-chanter
/// plays the accepted Priest's levels 1-9, sends SETPRO13 at Munin and stops after the ceremony; it has no leg of its own
/// yet. So the Chanter fights, rests and moves as the Priest it was: the Priest catalog through the Priest adapter, the
/// Priest rest plan and the Priest line's distances, thresholds and restock table. Its own skills are all excluded
/// until a Chanter leg exists. Only its gear is its own: the table form, with mace or staff by the physical stat.
/// </summary>
public static class NaturalChanterProfile
{
	private const string NoLeg = "no Chanter leg exists yet, and the line stops after the ceremony; its first leg decides how a Chanter uses it.";

	/// <summary>Every active or toggle skill a Chanter learns by itself up to level 10 (skill_tree.xml), and why none is cast.</summary>
	public static readonly IReadOnlyDictionary<int, string> Excluded = new Dictionary<int, string>
	{
		[246] = "Herb Treatment (powder): " + NoLeg,
		[249] = "MP Recovery (powder): " + NoLeg,
		[1562] = "Booming Strike: " + NoLeg,
		[1638] = "Winter Circle: " + NoLeg,
		[1685] = "Protectorate's Prayer: " + NoLeg,
		[1699] = "Light of Resurrection revives another player; the bot plays solo.",
		[1715] = "Thunderbolt Strike: " + NoLeg,
		[1778] = "Meteor Strike: " + NoLeg,
		[1809] = "Celerity Mantra (toggle): " + NoLeg,
	};

	/// <summary>
	/// A mace or a staff, ranked per swing by the physical stat (CP-Q7, on its default), so the Karmic Staff (58-88, mean
	/// 73) is held before the Karmic Warhammer (44-66 with 7 physical attack, 55 plus 7). Chain first, as the Chanter's
	/// level-9 mastery unlocks it. The staff rule by magic boost stays the Cleric's.
	/// </summary>
	public static NaturalClassGearTable GearTable { get; } = new(PlayerClass.CHANTER, ["MACE", "STAFF"], NaturalWeaponStat.Physical,
		["CHAIN", "LEATHER", "ROBE", "CLOTHES"], NaturalClassGearTable.DefaultConsumableOrder);

	// The supplies are the kit of every level: at the ceremony the level-10 bands are supplied to a Chanter as to a
	// Cleric, because the allowlist is keyed by level and not by class.
	private static readonly NaturalGearRules ChanterGear = GearTable.Rules(NaturalClassLineContract.LoadDefault(),
		NaturalHelpItemAllowlist.AllLevels, NaturalPriestSkills.All);

	public static NaturalClassProfile Chanter { get; } = new()
	{
		Class = PlayerClass.CHANTER,
		// A Chanter keeps what it learned as a Priest.
		Skills = NaturalPriestSkills.All,
		Excluded = Excluded,
		Combat = new NaturalPriestProfile.StaticPolicy(NaturalPriestSkills.All),
		HelpItems = new(NaturalHelpItemAllowlist.AllLevels.ToArray(), null),
		Upkeep = [NaturalPriestProfile.Blessing],
		PatrolRule = NaturalPatrolRule.Baseline,
		RangedHold = NaturalRangedHold.RunOption,
		Rest = NaturalPriestProfile.RestWith(NaturalPriestSkills.All),
		Ranges = NaturalPriestProfile.PriestLineRanges,
		Readiness = NaturalPriestProfile.PriestLineReadiness,
		Movement = NaturalPriestProfile.PriestLineMovement,
		Campaign = NaturalPriestProfile.PriestLineCampaign,
		Gear = ChanterGear,
		RewardGear = ChanterGear,
		Restock = NaturalPriestProfile.PriestLineRestock,
	};
}
