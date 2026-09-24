namespace Aion.Bots.Navigation;

public readonly record struct BotRoadPoint(float X, float Y);

/// <summary>Map-art route hints, never traversability evidence. Pixel centerlines were
/// transcribed from the extracted DF1/Ishalgen map (3072 px) using the portal's
/// calibrated-game-y-x projection: game X = pixel V * 2300/3072;
/// game Y = 740 + pixel U * 2300/3072. Every generated ground step is still checked.</summary>
public static class NaturalIshalgenRoads
{
	public const int MapId = 220010000;

	// Mijou/Derot-side approach to the Mau grain farms. The endpoints remain
	// off-road objectives; this is only the winding public approach corridor.
	public static readonly BotRoadPoint[] MijouToMauFarms =
	[
		Pixel(1300, 1270), Pixel(1230, 1285), Pixel(1160, 1270),
		Pixel(1110, 1230), Pixel(1080, 1160), Pixel(1050, 1090),
		Pixel(1020, 1030), Pixel(1000, 990),
	];

	private static BotRoadPoint Pixel(float u, float v) =>
		new(v * 2300f / 3072f, 740f + u * 2300f / 3072f);
}
