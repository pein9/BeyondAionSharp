using System.Collections;
using System.Globalization;
using System.Numerics;
using System.Text;

namespace Aion.GameServer.Utils;

/// <summary>String primitives whose contracts are defined by the Java reference server.</summary>
public static class JavaString
{
	/// <summary>Matches Java <c>String.hashCode()</c> over UTF-16 code units.</summary>
	public static int HashCode(string value)
	{
		ArgumentNullException.ThrowIfNull(value);

		var hash = 0;
		foreach (var codeUnit in value)
		{
			unchecked
			{
				hash = 31 * hash + codeUnit;
			}
		}
		return hash;
	}

	/// <summary>Matches Java <c>String.valueOf(boolean)</c>: <c>true</c>/<c>false</c> where C# prints <c>True</c>/<c>False</c>.</summary>
	public static string ValueOf(bool value) => value ? "true" : "false";

	/// <summary>
	/// Matches Java <c>String.valueOf(byte)</c>. Java has no unsigned byte, so a C# <see cref="byte"/> ported from one prints
	/// signed: 255 prints as <c>-1</c>.
	/// </summary>
	public static string ValueOf(byte value) => ((sbyte)value).ToString(CultureInfo.InvariantCulture);

	/// <summary>Matches Java <c>String.valueOf(int)</c>. Declared so int arguments cannot bind to the float overload.</summary>
	public static string ValueOf(int value) => value.ToString(CultureInfo.InvariantCulture);

	/// <summary>Matches Java <c>String.valueOf(long)</c>. Declared so long arguments cannot bind to the float overload.</summary>
	public static string ValueOf(long value) => value.ToString(CultureInfo.InvariantCulture);

	/// <summary>
	/// Matches Java <c>Float.toString(float)</c> as of JDK 19 (the reference server runs JDK 25): the shortest decimal that
	/// rounds to the value, always a "." separator with at least one fraction digit (<c>5.0</c>), and computerized scientific
	/// notation (<c>1.0E-4</c>, <c>1.0E7</c>) outside [10^-3, 10^7).
	/// </summary>
	public static string ValueOf(float value) => FloatingPointToString(value, "G9");

	/// <summary>Matches Java <c>Double.toString(double)</c> as of JDK 19; see <see cref="ValueOf(float)"/>.</summary>
	public static string ValueOf(double value) => FloatingPointToString(value, "G17");

	/// <summary>
	/// Matches Java <c>String.valueOf(Object)</c> for the C# stand-ins of Java values: <c>null</c>, the primitives above,
	/// arrays as <c>Arrays.toString</c> and collections as <c>AbstractCollection.toString</c> (<c>[a, b]</c>), maps as
	/// <c>AbstractMap.toString</c> (<c>{k=v, k2=v2}</c>) and a <see cref="TimeZoneInfo"/> as its <c>ZoneId</c> region ID.
	/// </summary>
	public static string ValueOf(object? value) => value switch
	{
		null => "null",
		string text => text,
		bool flag => ValueOf(flag),
		byte number => ValueOf(number),
		float number => ValueOf(number),
		double number => ValueOf(number),
		TimeZoneInfo zone => !zone.HasIanaId && TimeZoneInfo.TryConvertWindowsIdToIanaId(zone.Id, out string? ianaId) ? ianaId : zone.Id,
		IDictionary map => MapToString(map),
		IEnumerable items => "[" + string.Join(", ", items.Cast<object?>().Select(item => ValueOf(item))) + "]",
		IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
		_ => value.ToString() ?? "null",
	};

	private static string FloatingPointToString<T>(T value, string roundTripFormat) where T : IBinaryFloatingPointIeee754<T>
	{
		if (T.IsNaN(value))
			return "NaN";
		if (T.IsInfinity(value))
			return T.IsNegative(value) ? "-Infinity" : "Infinity";
		string text = value.ToString("R", CultureInfo.InvariantCulture);
		// .NET's shortest can miss the narrower gap below a power of two: 2^-25 formats as 2.980232238769531E-08, which parses
		// back to its neighbour. Java prints the full round-trip precision there.
		if (T.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture) != value)
			text = value.ToString(roundTripFormat, CultureInfo.InvariantCulture);
		var decimalDigits = DecimalDigits.Parse(text);
		// When one digit is enough, Java still takes a closer two-digit decimal that rounds to the same value (Float.MIN_VALUE
		// prints 1.4E-45, not .NET's 1E-45). Only subnormals are coarse enough for such a second digit to exist.
		if (decimalDigits.Digits.Length == 1 && T.IsSubnormal(value))
		{
			string twoDigits = value.ToString("E1", CultureInfo.InvariantCulture);
			if (T.Parse(twoDigits, NumberStyles.Float, CultureInfo.InvariantCulture) == value)
				decimalDigits = DecimalDigits.Parse(twoDigits);
		}
		return decimalDigits.ToJavaString();
	}

	private static string MapToString(IDictionary map)
	{
		var text = new StringBuilder("{");
		foreach (DictionaryEntry entry in map)
		{
			if (text.Length > 1)
				text.Append(", ");
			text.Append(ValueOf(entry.Key)).Append('=').Append(ValueOf(entry.Value));
		}
		return text.Append('}').ToString();
	}

	/// <summary>
	/// A finite decimal as its significant digits (no leading or trailing zeros, empty for zero) and the exponent of the first
	/// digit, so <c>1.25</c> is <c>("125", 0)</c> and <c>0.001</c> is <c>("1", -3)</c>.
	/// </summary>
	private readonly record struct DecimalDigits(bool Negative, string Digits, int Exponent)
	{
		/// <summary>Reads invariant .NET output such as <c>123.45</c>, <c>-0</c>, <c>1E+15</c> or <c>1.4E-045</c>.</summary>
		public static DecimalDigits Parse(string text)
		{
			bool negative = text[0] == '-';
			int start = negative ? 1 : 0;
			int exponentIndex = text.IndexOf('E');
			int end = exponentIndex < 0 ? text.Length : exponentIndex;
			int exponent = exponentIndex < 0 ? 0 : int.Parse(text.AsSpan(exponentIndex + 1), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
			int pointIndex = text.IndexOf('.', start, end - start);
			int integerDigits = (pointIndex < 0 ? end : pointIndex) - start;
			string digits = text[start..end].Replace(".", "");
			int leadingZeros = digits.Length - digits.TrimStart('0').Length;
			return new DecimalDigits(negative, digits.Trim('0'), integerDigits + exponent - leadingZeros - 1);
		}

		/// <summary>Java's layout of the selected decimal (the "Formatting as a string" rules of <c>Double.toString</c>).</summary>
		public string ToJavaString()
		{
			string sign = Negative ? "-" : "";
			if (Digits.Length == 0)
				return sign + "0.0";
			if (Exponent is >= -3 and < 0)
				return sign + "0." + new string('0', -Exponent - 1) + Digits;
			if (Exponent is >= 0 and < 7)
			{
				int integerDigits = Exponent + 1;
				if (Digits.Length <= integerDigits)
					return sign + Digits + new string('0', integerDigits - Digits.Length) + ".0";
				return sign + Digits[..integerDigits] + "." + Digits[integerDigits..];
			}
			return sign + Digits[0] + "." + (Digits.Length == 1 ? "0" : Digits[1..]) + "E" + Exponent.ToString(CultureInfo.InvariantCulture);
		}
	}
}
