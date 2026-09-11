using System.Text.RegularExpressions;

namespace Aion.GameServer.Utils;

/// <summary>Regex operations whose contracts are defined by the Java reference server (java.util.regex.Pattern).</summary>
public static class JavaPattern
{
	/// <summary>
	/// Matches Java <c>String.split(regex)</c>, which is <c>Pattern.split(input, 0)</c>. Unlike <see cref="Regex.Split(string, string)"/>,
	/// trailing empty strings are removed and a zero-width match at the beginning yields no leading empty string; an input without any
	/// match is returned as is (so <c>""</c> splits to <c>[""]</c>, but <c>" "</c> split on <c>" +"</c> is empty).
	/// </summary>
	public static string[] Split(string input, string regex)
	{
		ArgumentNullException.ThrowIfNull(input);

		var parts = new List<string>();
		int index = 0;
		for (Match m = Regex.Match(input, regex); m.Success; m = m.NextMatch())
		{
			if (index == 0 && m.Index == 0 && m.Length == 0)
				continue; // no empty leading substring for a zero-width match at the beginning
			parts.Add(input.Substring(index, m.Index - index));
			index = m.Index + m.Length;
		}
		if (index == 0) // no match found
			return [input];

		parts.Add(input.Substring(index));
		int size = parts.Count;
		while (size > 0 && parts[size - 1].Length == 0)
			size--;
		return parts.GetRange(0, size).ToArray();
	}
}
