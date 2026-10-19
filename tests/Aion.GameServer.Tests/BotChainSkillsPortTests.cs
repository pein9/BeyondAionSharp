using System.Reflection;
using Aion.Bots.Protocol;
using Aion.Bots.Scenarios;
using Aion.Bots.Scenarios.Classes;
using Aion.Bots.World;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Tests;

public sealed class BotChainSkillsPortTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.FromUnixTimeMilliseconds(1_000_000);
    private static NaturalPriestSkill Row(string? category = "NEXT", string? pre = "OPEN", int self = 1, int time = 0) =>
        new(1, 1, "test", 0, 0, 1, 0, ChainCategory: category, RequiresChainCategory: pre, SelfCount: self, ChainWindowMillis: time, PreCount: 1);

    [Fact]
    public void RequestsAndFailedResultsKeepTheChainUntilAnAcceptedFollowUpStarts()
    {
        var state = new NaturalChainState("OPEN", null, 7, Now, 1);
        var plain = Row(null, null);
        Assert.Equal(state, state.CastSent(plain));
        Assert.Equal(state, state.CastStarted(plain, Now));
        Assert.Equal(state, state.CastCompleted(plain, 7, false, false, Now));
        Assert.Equal(NaturalChainState.None, state.CastCompleted(plain, 7, false, true, Now));
        Assert.Equal(NaturalChainState.None, state.CastStarted(Row(), Now));
    }

    [Fact]
    public void RepetitionUsesTheOwnWindowAndNoPreviousCategorySurvivesCompletion()
    {
        var state = new NaturalChainState("NEXT", "OPEN", 7, Now, 1);
        var skill = Row("NEXT", "OPEN", 3, 500);
        Assert.Equal(state, state.CastStarted(skill, Now.AddMilliseconds(1999)));
        Assert.Equal(NaturalChainState.None, state.CastStarted(skill, Now.AddMilliseconds(2000)));
        var result = state.CastCompleted(skill, 7, false, true, Now.AddMilliseconds(750));
        Assert.Null(result.Previous);
        Assert.Equal(2, result.UseCount);
        Assert.Equal(Now.AddMilliseconds(750), result.StepAt);
    }

    [Theory]
    [InlineData(0, 2999, true)]
    [InlineData(0, 3000, false)]
    [InlineData(500, 1999, true)]
    [InlineData(500, 2000, false)]
    [InlineData(5000, 6499, true)]
    [InlineData(5000, 6500, false)]
    public void PolicyUsesOwnWindowWithStrictHitDeadline(int time, int elapsed, bool expected)
    {
        var skill = Row(time: time);
        var state = Observation("OPEN", "OTHER", Now, 1);
        Assert.Equal(expected, Open(skill, state, Now.AddMilliseconds(elapsed)));
        Assert.False(Open(skill, state with { OpenChainCategory = "OTHER", PreviousChainCategory = "OPEN" }, Now));
        Assert.False(Open(skill with { PreCount = 2 }, state, Now));
    }

    [Fact]
    public void ChainRefusalIsARecognizedStartRejection()
    {
        var packet = new DecodedBotServerPacket(typeof(SM_SYSTEM_MESSAGE), new Dictionary<string, object?>
            { ["name"] = "STR_SKILL_CANT_CAST_IN_NONE_CHAINSKILL_STATUS", ["msgId"] = 1300107 });
        Assert.True(BotCastProtocol.IsStartRejection(packet));
    }

    private static NaturalCombatObservation Observation(string current, string previous, DateTimeOffset hit, int count) =>
        new(Level: 1, Hp: 100, MaxHp: 100, Mp: 100, MaxMp: 100, Dead: false, Aggro: false, TargetDistance: 1, TargetObjectId: 7,
            Learned: new Dictionary<int, BotSkill>(), Cooldowns: new Dictionary<int, DateTimeOffset>(),
            OpenChainCategory: current, PreviousChainCategory: previous, OpenChainTargetId: 7, ChainStepAt: hit, OpenChainUseCount: count);
    private static bool Open(NaturalPriestSkill skill, NaturalCombatObservation state, DateTimeOffset now) =>
        (bool)typeof(NaturalRotationCombatPolicy).GetMethod("ChainOpen", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, new object[] { skill, false, state, now })!;
}
