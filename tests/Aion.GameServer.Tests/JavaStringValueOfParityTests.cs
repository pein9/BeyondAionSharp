using System.Globalization;
using Aion.GameServer.GeoEngine.Math;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

/// <summary>Expected strings are the output of Java 25 string concatenation for the same values.</summary>
public sealed class JavaStringValueOfParityTests
{
	[Theory]
	[InlineData(5f, "5.0")]
	[InlineData(1.5f, "1.5")]
	[InlineData(0.0001f, "1.0E-4")]
	[InlineData(1.0E7f, "1.0E7")]
	[InlineData(123456.7f, "123456.7")]
	[InlineData(9999999f, "9999999.0")]
	[InlineData(0.001f, "0.001")]
	[InlineData(9.999999E-4f, "9.999999E-4")]
	[InlineData(-7.3857914E-4f, "-7.3857914E-4")]
	[InlineData(100f, "100.0")]
	[InlineData(0.1f, "0.1")]
	[InlineData(-2.5f, "-2.5")]
	[InlineData(0f, "0.0")]
	[InlineData(-0f, "-0.0")]
	[InlineData(3.0E10f, "3.0E10")]
	[InlineData(float.MaxValue, "3.4028235E38")]
	[InlineData(float.Epsilon, "1.4E-45")]
	[InlineData(float.NaN, "NaN")]
	[InlineData(float.PositiveInfinity, "Infinity")]
	[InlineData(float.NegativeInfinity, "-Infinity")]
	public void ValueOfFloat_MatchesJavaFloatToString(float value, string expected)
	{
		Assert.Equal(expected, JavaString.ValueOf(value));
	}

	[Theory]
	[InlineData(0x4014000000000000L, "5.0")]
	[InlineData(0x3F1A36E2EB1C432DL, "1.0E-4")]
	[InlineData(0x416312D000000000L, "1.0E7")]
	[InlineData(0x44B52D02C7E14AF6L, "1.0E23")]
	[InlineData(0x4340000000000000L, "9.007199254740992E15")]
	[InlineData(0x3FD3333333333334L, "0.30000000000000004")]
	[InlineData(0x7FEFFFFFFFFFFFFFL, "1.7976931348623157E308")]
	[InlineData(0x0000000000000001L, "4.9E-324")]
	[InlineData(0x3E60000000000000L, "2.9802322387695312E-8")] // 2^-25: .NET's shortest does not round-trip here
	[InlineData(unchecked((long)0x8000000000000000UL), "-0.0")]
	public void ValueOfDouble_MatchesJavaDoubleToString(long bits, string expected)
	{
		Assert.Equal(expected, JavaString.ValueOf(BitConverter.Int64BitsToDouble(bits)));
	}

	[Fact]
	public void ValueOfFloat_IgnoresTheCurrentCulture()
	{
		CultureInfo previous = CultureInfo.CurrentCulture;
		CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
		try
		{
			Assert.Equal("1.5", JavaString.ValueOf(1.5f));
			Assert.Equal("-1.0E-4", JavaString.ValueOf(-0.0001));
		}
		finally
		{
			CultureInfo.CurrentCulture = previous;
		}
	}

	[Fact]
	public void ValueOfBool_IsLowercase()
	{
		Assert.Equal("true", JavaString.ValueOf(true));
		Assert.Equal("false", JavaString.ValueOf(false));
	}

	[Fact]
	public void ValueOfByte_IsSignedLikeJavaByte()
	{
		Assert.Equal("-1", JavaString.ValueOf((byte)255));
		Assert.Equal("10", JavaString.ValueOf((byte)10));
	}

	[Fact]
	public void ValueOfObject_MatchesJavaStringValueOfAndCollectionToString()
	{
		Assert.Equal("null", JavaString.ValueOf((object?)null));
		Assert.Equal("true", JavaString.ValueOf((object)true));
		Assert.Equal("5.0", JavaString.ValueOf((object)5f));
		Assert.Equal("[1.0, 2.5]", JavaString.ValueOf(new[] { 1f, 2.5f }));
		Assert.Equal("[null, 1.0]", JavaString.ValueOf(new object?[] { null, 1.0 }));
		Assert.Equal("[]", JavaString.ValueOf(Array.Empty<int>()));
		Assert.Equal("[a, b]", JavaString.ValueOf(new List<string> { "a", "b" }));
		Assert.Equal("{a=1, b=2}", JavaString.ValueOf(new Dictionary<string, int> { ["a"] = 1, ["b"] = 2 }));
		Assert.Equal("{}", JavaString.ValueOf(new Dictionary<string, int>()));
	}

	[Fact]
	public void WorldPositionToCoordString_PrintsJavaFloatsAndSignedHeading()
	{
		var position = new WorldPosition(210010000, 1.5f, 5f, 0.0001f, 255);

		Assert.Equal("Map ID: 210010000, Instance ID: 1, X: 1.5, Y: 5.0, Z: 1.0E-4, Heading: -1", position.ToCoordString());
	}

	[Fact]
	public void Vector3fToString_PrintsJavaFloats()
	{
		Assert.Equal("(1.0, 2.5, -1.0E-4)", new Vector3f(1f, 2.5f, -0.0001f).ToString());
	}
}
