using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

/// <summary>
/// Java parity: SiegeService.getPreparationCronString (SiegeService.java:697-715). The preparation starts five minutes
/// before a fortress siege and ten before a Panesterra one (id 10000 and above); every expected value is worked out from
/// the Java method: String.split(" "), Byte.parseByte, byte arithmetic, String.valueOf, String.join.
/// </summary>
public sealed class SiegePreparationCronTests
{
    // SiegeServiceTestInit has already built the singleton, with sieges off; the method reads no state.
    private static string Prepare(string siegeTime, int fortressId) =>
        SiegeService.GetInstance().GetPreparationCronString(siegeTime, fortressId);

    [Theory]
    [InlineData("0 0 17 ? * TUE,THU,SAT", 1131, "0 55 16 ? * TUE,THU,SAT")] // siege_schedule.xml, Siel's Western Fortress
    [InlineData("0 0 21 ? * FRI", 1011, "0 55 20 ? * FRI")]
    [InlineData("0 30 21 ? * SAT", 1221, "0 25 21 ? * SAT")] // no borrow from the hour
    [InlineData("0 5 0 ? * MON", 1131, "0 0 0 ? * MON")] // minutes reach 0, not below it: no borrow, no midnight
    [InlineData("0 0 17 ? * MON", 9999, "0 55 16 ? * MON")] // fortressId < 10000: five minutes
    public void FortressPreparationStartsFiveMinutesEarlier(string siegeTime, int fortressId, string expected)
    {
        Assert.Equal(expected, Prepare(siegeTime, fortressId));
    }

    [Theory]
    [InlineData("0 0 19 ? * SUN", 10111, "0 50 18 ? * SUN")] // siege_schedule.xml, Panesterra
    [InlineData("0 30 21 ? * SAT", 10211, "0 20 21 ? * SAT")]
    [InlineData("0 0 17 ? * MON", 10000, "0 50 16 ? * MON")] // Java tests fortressId < 10000, so 10000 itself gets ten
    public void PanesterraPreparationStartsTenMinutesEarlier(string siegeTime, int fortressId, string expected)
    {
        Assert.Equal(expected, Prepare(siegeTime, fortressId));
    }

    [Theory]
    [InlineData("0 0 0 ? * MON", 1131)]
    [InlineData("0 4 0 ? * MON", 1131)]
    [InlineData("0 9 0 ? * SUN", 10111)]
    public void PreparationOverMidnightThrowsAsJavaDoes(string siegeTime, int fortressId)
    {
        // Java: UnsupportedOperationException, which the port maps to NotSupportedException.
        var e = Assert.Throws<NotSupportedException>(() => Prepare(siegeTime, fortressId));

        Assert.Equal("Failed converting cron expression: " + siegeTime + "\nPreparation over midnight not supported.", e.Message);
        Assert.Null(e.InnerException);
    }

    [Theory]
    [InlineData("0 */5 17 ? * MON")] // minutes not a number
    [InlineData("0 0 * ? * MON")] // hours not a number
    [InlineData("0 0 200 ? * MON")] // Byte.parseByte: "Value out of range" is a NumberFormatException too
    [InlineData("0  0 17 ? * MON")] // a double space leaves an empty field, which Byte.parseByte rejects
    public void NonNumericFieldIsRethrownAsJavaDoes(string siegeTime)
    {
        var e = Assert.Throws<NotSupportedException>(() => Prepare(siegeTime, 1131));

        Assert.Equal("Failed converting cron expression: " + siegeTime, e.Message);
        Assert.IsAssignableFrom<FormatException>(e.InnerException);
    }

    [Fact]
    public void TrailingSpaceIsDroppedAsJavasSplitDoes()
    {
        // String.split(" ") removes trailing empty strings, so the rejoined expression has no trailing space.
        Assert.Equal("0 55 16 ? * MON", Prepare("0 0 17 ? * MON ", 1131));
    }

    [Fact]
    public void MinutesWrapAsAJavaByte()
    {
        // Byte.parseByte("-128") = -128; minutes -= (byte) 5 narrows -133 to 123, which is not below 0: no borrow.
        Assert.Equal("0 123 17 ? * MON", Prepare("0 -128 17 ? * MON", 1131));
    }

    [Fact]
    public void TooFewFieldsIsNotWrapped()
    {
        // Java catches only NumberFormatException; cronParts[2] on two fields throws ArrayIndexOutOfBoundsException past it.
        Assert.Throws<IndexOutOfRangeException>(() => Prepare("0 0", 1131));
    }
}
