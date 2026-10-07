namespace Aion.Bots.Navigation;

/// <summary>Combat distances that belong to no class (docs/natural-class-profiles.md, CP-18).</summary>
public static class NaturalCombatGeometry
{
	/// <summary>Client distance at which a monster is on the bot (its bound radius plus the swing reach).</summary>
	public const float MeleeReach = 3f;
}
