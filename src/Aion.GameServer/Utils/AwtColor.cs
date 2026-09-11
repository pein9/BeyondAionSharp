using System.Drawing;

namespace Aion.GameServer.Utils;

/// <summary>
/// Java parity: the java.awt.Color constants that Java code passes to ChatUtil.color or resolves by name. Use these rather than the System.Drawing
/// color of the same name, which does not always have the same RGB: Color.Green is (0,128,0) where java.awt.Color.GREEN is (0,255,0), and PINK,
/// ORANGE, LIGHT_GRAY and DARK_GRAY differ as well.
/// </summary>
public static class AwtColor
{
	public static readonly Color WHITE = Color.FromArgb(255, 255, 255);
	public static readonly Color LIGHT_GRAY = Color.FromArgb(192, 192, 192);
	public static readonly Color GRAY = Color.FromArgb(128, 128, 128);
	public static readonly Color DARK_GRAY = Color.FromArgb(64, 64, 64);
	public static readonly Color BLACK = Color.FromArgb(0, 0, 0);
	public static readonly Color RED = Color.FromArgb(255, 0, 0);
	public static readonly Color PINK = Color.FromArgb(255, 175, 175);
	public static readonly Color ORANGE = Color.FromArgb(255, 200, 0);
	public static readonly Color YELLOW = Color.FromArgb(255, 255, 0);
	public static readonly Color GREEN = Color.FromArgb(0, 255, 0);
	public static readonly Color MAGENTA = Color.FromArgb(255, 0, 255);
	public static readonly Color CYAN = Color.FromArgb(0, 255, 255);
	public static readonly Color BLUE = Color.FromArgb(0, 0, 255);

	private static readonly Dictionary<string, Color> ColorsByFieldName = new()
	{
		[nameof(WHITE)] = WHITE,
		[nameof(LIGHT_GRAY)] = LIGHT_GRAY,
		[nameof(GRAY)] = GRAY,
		[nameof(DARK_GRAY)] = DARK_GRAY,
		[nameof(BLACK)] = BLACK,
		[nameof(RED)] = RED,
		[nameof(PINK)] = PINK,
		[nameof(ORANGE)] = ORANGE,
		[nameof(YELLOW)] = YELLOW,
		[nameof(GREEN)] = GREEN,
		[nameof(MAGENTA)] = MAGENTA,
		[nameof(CYAN)] = CYAN,
		[nameof(BLUE)] = BLUE,
	};

	/// <summary>
	/// Java parity: <c>(Color) Color.class.getField(fieldName).get(null)</c>, which callers invoke with <c>name.toUpperCase()</c>. Only the 13
	/// constants above resolve: java.awt.Color's other public fields are its lower-case aliases, which an upper-cased name cannot reach, and the
	/// Transparency int constants (OPAQUE, BITMASK, TRANSLUCENT), whose cast to Color throws, so Java falls through to its hex parsing for them.
	/// </summary>
	public static bool TryGetByFieldName(string fieldName, out Color color)
	{
		return ColorsByFieldName.TryGetValue(fieldName, out color);
	}
}
