using Aion.GameServer.Utils;

namespace Aion.GameServer.Tests;

public sealed class JavaPatternSplitParityTests
{
	private const string PreviewItemSeparator = @",|(?<=[^,])(?=\[)|(?<=[\]])(?=[^\[])"; // Preview.parse

	// Expected values were produced by String.split on JDK 25.
	[Theory]
	[InlineData("a=1,b=2,", " *, *", new[] { "a=1", "b=2" })] // Configure.toMap: a trailing comma is accepted
	[InlineData("a=1 , b=2", " *, *", new[] { "a=1", "b=2" })]
	[InlineData("", " +", new[] { "" })] // no match: the input itself
	[InlineData(" ", " +", new string[0])] // only empty strings: all of them are trailing
	[InlineData("a  b ", " +", new[] { "a", "b" })]
	[InlineData(",", ",", new string[0])]
	[InlineData(",a", ",", new[] { "", "a" })] // a positive-width match at the beginning keeps the leading empty string
	[InlineData("a,,b,,", ",", new[] { "a", "", "b" })]
	[InlineData("abc", "", new[] { "a", "b", "c" })] // a zero-width match at the beginning adds no leading empty string
	[InlineData("100000001,", PreviewItemSeparator, new[] { "100000001" })]
	[InlineData("[item:100000001][item:100000002],", PreviewItemSeparator, new[] { "[item:100000001]", "[item:100000002]" })]
	[InlineData("100000001[item:100000002]red", PreviewItemSeparator, new[] { "100000001", "[item:100000002]", "red" })]
	public void Split_MatchesJavaStringSplit(string input, string regex, string[] expected)
	{
		Assert.Equal(expected, JavaPattern.Split(input, regex));
	}
}
