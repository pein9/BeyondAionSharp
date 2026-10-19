using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Aion.GameServer.Controllers.Movement;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.Tests.Ai;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.Stats;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class MovementModifiersPortTests
{
    [Fact]
    public void EveryHeadingBytePairMatchesTheTargetJavaIntegerArithmetic()
    {
        using var fixture = LoadFixture();
        string expected = fixture.RootElement.GetProperty("headingsDestinationMajor").GetString()!;
        Assert.Equal(65536, expected.Length);
        for (int dest = 0; dest < 256; dest++)
            for (int own = 0; own < 256; own++)
                Assert.Equal(expected[dest * 256 + own], PlayableMoveController<Player>.CalculateDirection((byte)dest, (byte)own).ToString()[0]);
        Assert.Equal(new[] {"NONE", "FORWARD", "BACKWARD", "SIDEWAYS"}, Enum.GetNames<MovementModifierDirection>());
        Assert.Equal(new[] {0, 1, 2, 4}, Enum.GetValues<MovementModifierDirection>().Select(d => d.GetMoveStateFlag()));
    }

    [Fact]
    public void EveryMovementMaskMatchesTheTargetJavaRequestClassification()
    {
        using var fixture = LoadFixture();
        string expected = fixture.RootElement.GetProperty("moveRequests").GetString()!;
        Assert.Equal(256, expected.Length);
        for (int mask = 0; mask < 256; mask++)
            Assert.Equal(expected[mask], PlayerMoveController.GetMoveRequest((byte)mask).ToString()[0]);
    }

    [Fact]
    public void AllTableCombinationsAndImmediateSpeedFactorsMatchJavaBitForBit()
    {
        using var fixture = LoadFixture();
        foreach (var c in fixture.RootElement.GetProperty("table").EnumerateArray())
            AssertBits(Parse(c.GetProperty("resultFloat")), StatFunctions.AdjustStatByMoveState(
                c.GetProperty("moveState").GetInt32(), Enum.Parse<StatEnum>(c.GetProperty("stat").GetString()!), Parse(c.GetProperty("value"))));
        foreach (var c in fixture.RootElement.GetProperty("speed").EnumerateArray())
            AssertBits(Parse(c.GetProperty("resultFloat")), StatFunctions.AdjustSpeedByMovementModifier(
                Enum.Parse<MovementModifierDirection>(c.GetProperty("direction").GetString()!), Parse(c.GetProperty("value"))));
    }

    [Fact]
    public void TwoSlotTimingMatchesJavaIncludingInterruptionsAndSignedLongWrap()
    {
        using var fixture = LoadFixture();
        foreach (var trace in fixture.RootElement.GetProperty("traces").EnumerateArray())
        {
            var state = new MovementModifierState();
            foreach (var action in trace.GetProperty("actions").EnumerateArray())
            {
                long now = action.GetProperty("time").GetInt64();
                if (action.TryGetProperty("set", out var set))
                    state.SetMoveState(set.GetInt32(), now);
                int actual = state.GetMoveState(now);
                Assert.True(action.GetProperty("expected").GetInt32() == actual,
                    $"{trace.GetProperty("name").GetString()} at {now}: got {actual}");
                Assert.NotEqual(7, actual);
            }
        }
    }

    [Fact]
    public void PublicStateAndDiagnosticsUseTheSharedClock()
    {
        long now = 10000;
        SystemClock.UseSource(() => now);
        try
        {
            var state = new MovementModifierState();
            state.SetMoveState(1);
            now += 666; Assert.Equal(0, state.GetMoveState());
            now++; Assert.Equal(1, state.GetMoveState());
            state.SetMoveState(4);
            Assert.Equal("[moveState=1, pending=4 since 0ms, 1 fades out in 1332ms]", state.ToString());
            now += 1332;
            Assert.Equal("[moveState=4, pending=4 since 1332ms]", state.ToString());
        }
        finally { SystemClock.UseSystemClock(); }
    }

    [Fact]
    public void ClientUpdatesKeepActivationAndCombineOnlyTwoDirectionsUntilStopFades()
    {
        using var h = BossAiHarness.For().Build();
        var player = h.SpawnPlayer(100, 100, 200);
        var move = player.GetMoveController();
        long now = 10000;
        SystemClock.UseSource(() => now);
        try
        {
            move.SetNewDirection(120, 100, 200, 0);
            move.MovementMaskField = 0xC0;
            move.OnMoveFromClient();
            Assert.Equal(0, move.GetMoveState());
            now = 10600;
            move.SetNewDirection(80, 100, 200, 60); // a plain position update does not request a new direction
            move.MovementMaskField = 0x80;
            move.OnMoveFromClient();
            now = 10667;
            Assert.Equal(1, move.GetMoveState());
            now = 10700;
            move.SetNewDirection(100, 120, 200, 0);
            move.MovementMaskField = 0xC0;
            move.OnMoveFromClient();
            now = 11367;
            Assert.Equal(5, move.GetMoveState());
            AssertBits(1100, StatFunctions.AdjustStatByMovementModifier(player, StatEnum.PHYSICAL_ATTACK, 1000));
            AssertBits(1000, StatFunctions.AdjustStatByMovementModifier(player, StatEnum.PHYSICAL_DEFENSE, 1000));
            AssertBits(1300, StatFunctions.AdjustStatByMovementModifier(player, StatEnum.EVASION, 1000));
            AssertBits(6000, StatFunctions.AdjustStatByMovementModifier(player, StatEnum.SPEED, 6000));
            AssertBits(1234.5f, StatFunctions.AdjustStatByMovementModifier(player, null, 1234.5f));
            move.MovementMaskField = 0x08; move.OnMoveFromClient(); // airborne turning keeps the pending direction
            now = 12032; Assert.Equal(4, move.GetMoveState());
            now = 12100; move.MovementMaskField = 0x40; move.OnMoveFromClient();
            Assert.Equal((byte)0x40, move.GetLastMovementMask());
            Assert.Equal(now, move.GetLastPositionFromClientMillis());
            Assert.Equal(player.GetX(), move.GetLastPositionFromClient().GetX());
            now = 13431; Assert.Equal(4, move.GetMoveState());
            now = 13432; Assert.Equal(0, move.GetMoveState());
        }
        finally { SystemClock.UseSystemClock(); }
    }

    [Fact]
    public void PointRequestsAreForwardWhileTurningOrJumpingOnTheSpotHasNoDirection()
    {
        using var h = BossAiHarness.For().Build();
        var player = h.SpawnPlayer(100, 100, 200);
        var move = player.GetMoveController();
        long now = 10000;
        SystemClock.UseSource(() => now);
        try
        {
            move.SetNewDirection(80, 100, 200, 0); // behind us, but a clicked point is always forward
            move.MovementMaskField = 0xE0; move.OnMoveFromClient();
            now += 667; Assert.Equal(1, move.GetMoveState());
            now++;
            player.GetPosition().SetXYZH(110, 110, 200, 0);
            move.SetNewDirection(110.05f, 110, 220, 0); // vertical jump / less than MOVE_CHECK_OFFSET in XY
            move.MovementMaskField = 0xC8; move.OnMoveFromClient();
            now += 1332; Assert.Equal(0, move.GetMoveState());
            Assert.Equal(110, move.GetLastPositionFromClient().GetX());
        }
        finally { SystemClock.UseSystemClock(); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AbortOrRefusedStartedMovementStopsThePendingSlotWithoutCuttingItsFade(bool refused)
    {
        using var h = BossAiHarness.For().Build();
        var player = h.SpawnPlayer(100, 100, 200);
        var move = player.GetMoveController();
        long now = 10000;
        SystemClock.UseSource(() => now);
        try
        {
            move.SetNewDirection(120, 100, 200, 0); move.MovementMaskField = 0xC0; move.OnMoveFromClient();
            now += 667; Assert.Equal(1, move.GetMoveState());
            if (refused)
            {
                player.GetEffectController().SetAbnormal(AbnormalState.ROOT);
                Assert.False(player.CanPerformMove());
                var started = (AtomicBoolean)typeof(CreatureMoveController).GetField("Started", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(move)!;
                started.Set(true); move.SetInMove(true);
                move.MoveToDestination();
            }
            else { move.AbortMove(); }
            Assert.False(move.IsInMove());
            Assert.Equal(MovementMask.IMMEDIATE, move.GetMovementMask());
            now += 1331; Assert.Equal(1, move.GetMoveState());
            now++; Assert.Equal(0, move.GetMoveState());
        }
        finally { SystemClock.UseSystemClock(); }
    }

    [Theory]
    [InlineData(0, 100, 0.8f)]
    [InlineData(-100, 0, 0.6f)]
    public void ServerMovementUsesImmediateSpeedWithoutCreatingCombatArrows(float dx, float dy, float factor)
    {
        using var h = BossAiHarness.For().Build();
        var player = h.SpawnPlayer(300, 300, 200);
        var move = player.GetMoveController();
        long now = 10000;
        SystemClock.UseSource(() => now);
        try
        {
            move.SetNewDirection(300 + dx, 300 + dy, 200, 0);
            move.UpdateLastMove();
            float speed = player.GetGameStats().GetMovementSpeedFloat();
            Assert.True(speed > 0);
            now += 1000;
            move.MoveToDestination();
            float distance = (float)PositionUtil.GetDistance(300, 300, player.GetX(), player.GetY());
            Assert.InRange(distance, speed * factor - 0.0001f, speed * factor + 0.0001f);
            Assert.Equal(0, move.GetMoveState());
        }
        finally { SystemClock.UseSystemClock(); }
    }

    private static float Parse(JsonElement value) => float.Parse(value.GetString()!, CultureInfo.InvariantCulture);
    private static void AssertBits(float expected, float actual) => Assert.Equal(BitConverter.SingleToInt32Bits(expected), BitConverter.SingleToInt32Bits(actual));

    private static JsonDocument LoadFixture()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            var path = Path.Combine(directory.FullName, "parity-artifacts", "golden", "formulas", "MovementModifiers.json");
            if (File.Exists(path))
            {
                var fixture = JsonDocument.Parse(File.ReadAllText(path));
                Assert.Equal("e7a16c553baec87dc4d58df78cb67c6019f25674", fixture.RootElement.GetProperty("sourceJavaCommit").GetString());
                return fixture;
            }
            directory = directory.Parent;
        }
        throw new FileNotFoundException("Run scripts/parity/capture_movement_modifiers.py to capture the target Java implementation.");
    }
}
