using Aion.Bots.World;

namespace Aion.Bots.Scenarios.Classes;

/// <summary>
/// CP-18: the distances, in metres, at which the journey's shared helpers engage (docs/natural-class-profiles.md).
/// Each is a number of its own: two that are equal today are not one value, and the standoff's 21 m is not stored but
/// follows from its three inputs (spell range less arrival tolerance less safety margin).
/// </summary>
/// <param name="MeleeReach">A monster within this is on the bot: its bound radius plus the swing reach.</param>
/// <param name="SpellRange">The pull planner's spell range: an engaged attacker within it can be answered, and a
/// shipped spawn is approached to it plus a margin.</param>
/// <param name="PullDistance">The opening distance of a planned pull; null leaves it to the run's own parameters.</param>
/// <param name="FiringRange">The fight-through's firing range: a circle wider than this cannot be pulled from its edge.</param>
/// <param name="SpawnApproachRange">How near a shipped combat spawn is explored to, and how near its target must be observed.</param>
/// <param name="SpawnPullScanRange">How far around itself the bot looks for a clean pull among a shipped spawn's monsters.</param>
/// <param name="FightThroughPullRange">How near a route's blocker must be before it is pulled.</param>
/// <param name="StandoffSpellRange">The spell range the in-fight standoff is measured from.</param>
/// <param name="StandoffArrivalTolerance">How far short of a point the ground navigator may stop.</param>
/// <param name="RangedApproachRadius">How near to a target a ranged approach route must end, with line of sight.</param>
public sealed record NaturalEngageRanges(float MeleeReach, float SpellRange, float? PullDistance, float FiringRange,
	float SpawnApproachRange, float SpawnPullScanRange, float FightThroughPullRange, float StandoffSpellRange,
	float StandoffArrivalTolerance, float StandoffSafetyMargin, float RangedApproachRadius);

/// <summary>The bot rests first when HP or MP is below these percentages. An MP percentage of 0 asks for HP only.</summary>
public readonly record struct NaturalReadiness(int HpPercent, int MpPercent = 0)
{
	public bool RestFirst(BotWorldModel world) =>
		world.CurrentHp * 100 < world.MaxHp * HpPercent || world.CurrentMp * 100 < world.MaxMp * MpPercent;
}

/// <summary>CP-18: the named readiness thresholds the shared helpers ask for.</summary>
/// <param name="BeforePull">Before planning a pull.</param>
/// <param name="BeforeUseBar">After clearing around a quest object: its respawn window is short, so rest only when needed.</param>
/// <param name="BetweenAdds">After each add of a pull that brings several.</param>
/// <param name="BeforeNamedTarget">Before the target of a planned fight: a named is engaged rested.</param>
public sealed record NaturalReadinessThresholds(NaturalReadiness BeforePull, NaturalReadiness BeforeUseBar,
	NaturalReadiness BetweenAdds, NaturalReadiness BeforeNamedTarget);

/// <summary>
/// CP-21: the numbers of the hand-written Ishalgen quest executors and of the wait for Return (docs/natural-class-profiles.md).
/// The executors keep their shape; only these numbers are the class's. Distances are metres.
/// </summary>
/// <param name="SpriggRouteBeyond">Q2002: a Sprigg farther than this is approached along a route first.</param>
/// <param name="SpriggStandoff">Q2002: the route's last point at least this far from the Sprigg is where the bot stands.</param>
/// <param name="SpriggSelectWithin">Q2002: an observed Sprigg within this is taken as the target.</param>
/// <param name="FiringEdgeWithin">Q2005: ground within this of the target, with sight of it, is a firing edge.</param>
/// <param name="StalkerSearchRange">Q2005: how near the isolated stalker's spot is explored to.</param>
/// <param name="BlockerReplanBeyond">Q2005: a blocker farther than this is planned for again.</param>
/// <param name="ReturnCooldownHpFraction">While Return is on cooldown the bot rests below this fraction of its HP.</param>
/// <param name="StalkerPull">Q2005: a stalker pull is planned at this HP, and the search holds below it.</param>
/// <param name="BeforeSack">Q2006: before a supply sack is used.</param>
/// <param name="BeforeCamp">Q2007: before the bot enters the camp.</param>
public sealed record NaturalCampaignRules(float SpriggRouteBeyond, float SpriggStandoff, float SpriggSelectWithin,
	float FiringEdgeWithin, float StalkerSearchRange, float BlockerReplanBeyond, float ReturnCooldownHpFraction,
	NaturalReadiness StalkerPull, NaturalReadiness BeforeSack, NaturalReadiness BeforeCamp);
