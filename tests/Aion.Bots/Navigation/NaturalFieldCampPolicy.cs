using Aion.Bots.World;

namespace Aion.Bots.Navigation;

/// <summary>Use only client-observed monster positions and their shipped aggro radii
/// when deciding whether ordinary rest can begin between quest pulls.</summary>
public static class NaturalFieldCampPolicy
{
	public static bool CanRest(BotPosition position, IReadOnlyList<BotNavigationHazard> observedHostiles,
		float margin = 5)
	{
		ArgumentNullException.ThrowIfNull(observedHostiles);
		if (!float.IsFinite(margin) || margin < 0) throw new ArgumentOutOfRangeException(nameof(margin));
		return observedHostiles.All(hostile => Distance(position, hostile.Position) > hostile.Radius + margin);
	}

	private static float Distance(BotPosition a, BotPosition b) => MathF.Sqrt(
		MathF.Pow(a.X - b.X, 2) + MathF.Pow(a.Y - b.Y, 2) + MathF.Pow(a.Z - b.Z, 2));
}
