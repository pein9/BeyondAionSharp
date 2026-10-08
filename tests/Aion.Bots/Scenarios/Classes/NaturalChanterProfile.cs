using Aion.GameServer.Dataholders;
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

	// CP-29a (CP-Q7, answered 2026-10-07): the Chanter's table is the Cleric's, the staff with the most magic boost and
	// chain first. The supplies are the kit of every level: at the ceremony the level-10 bands are supplied to a Chanter
	// as to a Cleric, because the allowlist is keyed by level and not by class.
	private static readonly NaturalGearRules ChanterGear = NaturalClassGearTable.Chanter.Rules(NaturalClassLineContract.LoadDefault(),
		NaturalHelpItemAllowlist.AllLevels, NaturalPriestSkills.All);

	public static NaturalClassProfile Create(StaticData data)
	{
		ArgumentNullException.ThrowIfNull(data);
		// A Chanter keeps what it learned as a Priest (NR-13: the Priest's generated catalog).
		NaturalPriestSkill[] skills = NaturalPriestProfile.PriestCatalog(data);
		return new NaturalClassProfile
		{
			Class = PlayerClass.CHANTER,
			Skills = skills,
			Excluded = Excluded,
			Combat = new NaturalPriestProfile.StaticPolicy(skills),
			HelpItems = new(NaturalHelpItemAllowlist.AllLevels.ToArray(), null),
			Upkeep = [NaturalPriestProfile.Blessing],
			PatrolRule = NaturalPatrolRule.Baseline,
			RangedHold = NaturalRangedHold.RunOption,
			Rest = NaturalPriestProfile.RestWith(skills),
			Ranges = NaturalPriestProfile.PriestLineRanges,
			Readiness = NaturalPriestProfile.PriestLineReadiness,
			Movement = NaturalPriestProfile.PriestLineMovement,
			Campaign = NaturalPriestProfile.PriestLineCampaign,
			Gear = ChanterGear,
			Restock = NaturalPriestProfile.PriestLineRestock,
		};
	}
}
