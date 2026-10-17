using System.Reflection;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Model;

namespace Aion.GameServer.Tests;

/// <summary>
/// Java parity: GameServer.main's ratio block sets ASMOS_COUNT, then ELYOS_COUNT, from PlayerDAO and calls
/// updateRatio(null, 0), which returns at once, so the ratios stay 0 until the first real updateRatio.
/// </summary>
[Collection("GoldenDataManager")]
public sealed class GameServerRatioTests : IDisposable
{
    private static readonly string[] StaticFields = ["ELYOS_COUNT", "ASMOS_COUNT", "ELYOS_RATIO", "ASMOS_RATIO"];

    private readonly Dictionary<string, object?> previous = new();
    private readonly int previousMinCharacters = GSConfig.RATIO_MIN_CHARACTERS_COUNT;

    public GameServerRatioTests()
    {
        foreach (string name in StaticFields)
            previous[name] = Field(name).GetValue(null);
        Field("ELYOS_COUNT").SetValue(null, 0);
        Field("ASMOS_COUNT").SetValue(null, 0);
        Field("ELYOS_RATIO").SetValue(null, 0f);
        Field("ASMOS_RATIO").SetValue(null, 0f);
        GSConfig.RATIO_MIN_CHARACTERS_COUNT = 50; // Java's default
    }

    public void Dispose()
    {
        foreach (string name in StaticFields)
            Field(name).SetValue(null, previous[name]);
        GSConfig.RATIO_MIN_CHARACTERS_COUNT = previousMinCharacters;
    }

    [Fact]
    public void BootCountsAreReadBackAndLeaveTheRatiosAtZero()
    {
        GameServer.SetCharacterCounts(asmodiansCount: 60, elyosCount: 40);

        Assert.Equal(60, GameServer.GetCountFor(Race.ASMODIANS));
        Assert.Equal(40, GameServer.GetCountFor(Race.ELYOS));
        Assert.Equal(0f, GameServer.GetRatiosFor(Race.ASMODIANS));
        Assert.Equal(0f, GameServer.GetRatiosFor(Race.ELYOS));
    }

    [Fact]
    public void ALaterUpdateRatioUsesJavasIntegerDivision()
    {
        GameServer.SetCharacterCounts(asmodiansCount: 60, elyosCount: 40);

        GameServer.UpdateRatio(Race.ELYOS, 1);

        // Java: ASMOS_RATIO = 60 * 100 / 101 = 59, ELYOS_RATIO = 41 * 100 / 101 = 40 (int division, then widened to float).
        Assert.Equal(60, GameServer.GetCountFor(Race.ASMODIANS));
        Assert.Equal(41, GameServer.GetCountFor(Race.ELYOS));
        Assert.Equal(59f, GameServer.GetRatiosFor(Race.ASMODIANS));
        Assert.Equal(40f, GameServer.GetRatiosFor(Race.ELYOS));
    }

    private static FieldInfo Field(string name) =>
        typeof(GameServer).GetField(name, BindingFlags.Static | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("GameServer." + name + " not found");
}
