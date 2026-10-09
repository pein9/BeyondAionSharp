namespace Aion.Bots.Scenarios.Classes;

/// <summary>How a class opens and holds a fight.</summary>
public enum NaturalPullStyle
{
	/// <summary>Cast from range and let the monster come (the Priest line).</summary>
	StandOff,
	/// <summary>Fight from the weapon's own range and keep that distance.</summary>
	WeaponRangeStandOff,
	/// <summary>Walk to the target and fight at weapon reach.</summary>
	WalkIn,
}

/// <summary>What an approach does next.</summary>
public enum NaturalApproachStep
{
	/// <summary>A checked route to ground in range of the target, with line of sight.</summary>
	RangedRoute,
	/// <summary>Walk up to the target.</summary>
	WalkToTarget,
	/// <summary>Stay: the target is inside the hold distance.</summary>
	Hold,
}

/// <summary>What to do when the server reports an obstacle the bot's geometry does not see.</summary>
public enum NaturalObstacleAnswer
{
	/// <summary>Close in to the target along checked ground and cast from there.</summary>
	CloseToMelee,
	/// <summary>Stay at range and find other ground with sight of the target.</summary>
	AnotherSightLine,
}

/// <summary>
/// CP-19: movement inside a fight, as pure answers the fight loop asks for (docs/natural-class-profiles.md): where an
/// approach stops, what counts as adjacent, how far to close in after the server refuses a cast for distance, and what
/// to do about an obstacle. The fight loop moves; this only decides. The Priest line is a stand-off: a ranged route
/// while the target is farther than <paramref name="RangedRouteBeyond"/>, then up to the target; a walk-in class always
/// walks to the target; a weapon-range class holds at <paramref name="HoldDistance"/>.
/// </summary>
/// <param name="MeleeReach">A monster within this is on the bot.</param>
/// <param name="RangeRefusalCloseIn">How near to come after a distance refusal of a skill that is not a melee skill.</param>
/// <param name="RecentHitMillis">A target that hit the bot within this is adjacent whatever its lagging position says.</param>
/// <param name="ObstacleAnswer">CP-48: the class's own answer to an obstacle; null leaves it to the style.</param>
/// <param name="WalksToItsTarget">NR-53c: when the class walks up, it walks to the monster it fights; null leaves it to
/// the style. A walk-in class always does. The Priest line walks to the nearest monster of its target's kind, as
/// recorded. A class that pulls from range and fights at melee (the Templar) must reach its own target when that one
/// does not come.</param>
public sealed record NaturalFightMovement(NaturalPullStyle Style, float MeleeReach, float RangedRouteBeyond, float RangeRefusalCloseIn,
	float HoldDistance = 0, int RecentHitMillis = 3000, NaturalObstacleAnswer? ObstacleAnswer = null, bool? WalksToItsTarget = null)
{
	/// <summary>NR-53c: a walk up goes to the fight's own target.</summary>
	public bool GoesToItsTarget => WalksToItsTarget ?? Style == NaturalPullStyle.WalkIn;

	/// <param name="targetRanged">The target attacks from range, so its hits say nothing about where it stands.</param>
	/// <param name="millisSinceHitByTarget">Game time since the target last hit the bot; null when it has not.</param>
	public bool Adjacent(float targetDistance, bool targetRanged, long? millisSinceHitByTarget) =>
		!targetRanged && millisSinceHitByTarget is long since && since <= RecentHitMillis || targetDistance <= MeleeReach;

	public NaturalApproachStep Approach(float targetDistance) => Style switch
	{
		NaturalPullStyle.WalkIn => NaturalApproachStep.WalkToTarget,
		NaturalPullStyle.WeaponRangeStandOff => targetDistance > HoldDistance ? NaturalApproachStep.RangedRoute : NaturalApproachStep.Hold,
		_ => targetDistance > RangedRouteBeyond ? NaturalApproachStep.RangedRoute : NaturalApproachStep.WalkToTarget,
	};

	/// <summary>How near to the target to come after STR_SKILL_NOT_ENOUGH_DISTANCE: inside melee reach for a melee skill.</summary>
	public float CloseInAfterRangeRefusal(float skillRange) => skillRange <= MeleeReach ? MeleeReach - 1 : RangeRefusalCloseIn;

	/// <summary>The answer to STR_SKILL_OBSTACLE.</summary>
	public NaturalObstacleAnswer AfterObstacleRefusal => ObstacleAnswer ??
		(Style == NaturalPullStyle.WeaponRangeStandOff ? NaturalObstacleAnswer.AnotherSightLine : NaturalObstacleAnswer.CloseToMelee);

	/// <summary>How near to the target <see cref="NaturalObstacleAnswer.CloseToMelee"/> comes.</summary>
	public float ObstacleCloseIn => MeleeReach - 1;
}
