using System.Globalization;

namespace Aion.Bots.Transport;

/// <summary>Stable local test identities; no dependence on process hash randomization or wall time.</summary>
public static class BotIdentity
{
	public const int MaximumSubjects = 1000;
	private const int DirectorAddress = 0xFE;

	public static string CharacterName(int index)
	{
		Validate(index);
		// Preserve b01..b676's existing two-letter names, then extend to three
		// letters instead of spilling past 'z' in the old quotient-as-char scheme.
		int value = index - 1;
		Span<char> suffix = stackalloc char[3];
		int start = suffix.Length;
		do
		{
			suffix[--start] = (char)('a' + value % 26);
			value /= 26;
		} while (value > 0 || suffix.Length - start < 2);
		return "Aelive" + suffix[start..].ToString();
	}

	public static byte[] MacBytes(int index, bool director = false)
	{
		Validate(index);
		// Keep the historical director address and low-numbered subjects stable.
		// Skip its reserved value when expanding the population past 253 subjects.
		int address = director ? DirectorAddress : index >= DirectorAddress ? index + 1 : index;
		return [0x02, 0x00, 0x00, 0x00, (byte)(address >> 8), (byte)address];
	}

	public static string MacAddress(ReadOnlySpan<byte> bytes)
	{
		if (bytes.Length != 6) throw new ArgumentException("A MAC address needs six bytes.", nameof(bytes));
		return string.Join('-', bytes.ToArray().Select(value => value.ToString("X2", CultureInfo.InvariantCulture)));
	}

	public static int ParseSubjectNumber(string bot)
	{
		if (string.IsNullOrEmpty(bot) || bot[0] != 'b' ||
			!int.TryParse(bot.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out int index))
			throw new ArgumentException("A subject bot id must be b followed by a positive integer.", nameof(bot));
		Validate(index);
		return index;
	}

	private static void Validate(int index)
	{
		if (index is < 1 or > MaximumSubjects) throw new ArgumentOutOfRangeException(nameof(index));
	}
}
