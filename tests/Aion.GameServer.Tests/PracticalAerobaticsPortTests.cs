using Aion.GameServer.Dataholders;
using Aion.GameServer.Dataholders.LoadingUtils;
using Aion.GameServer.GeoEngine.Math;
using Aion.GameServer.Handlers.Quest;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Flyring;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;
using Aion.GameServer.TestKit;
using Aion.GameServer.Tests.Ai;
using System.Reflection;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class PracticalAerobaticsPortTests
{
    private static readonly int[] Order = { 1, 4, 3, 6, 5, 2, 7 };
    private static string Ring(int number) => "ERACUS_TEMPLE_210020000_" + number;

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(8)]
    [InlineData(63)]
    public void LastRingCannotSkipEarlierStepsOrCancelTheCourseTimer(int step)
    {
        using var harness = BossAiHarness.For().Build();
        using var questData = new QuestDataScope();
        var player = harness.SpawnPlayer();
        var state = new QuestState(1354, QuestStatus.START, step, 0, 0, null, null, null);
        Assert.True(player.GetQuestStateList().AddQuest(1354, state));
        var env = new QuestEnv(null!, player, 1354);
        QuestService.QuestTimerStart(env, 120);
        player.GetLifeStats().SetCurrentFp(0);
        Assert.True(new _1354PraticalAerobatics().OnPassFlyingRingEvent(env, Ring(7)));
        Assert.Equal(step, state.GetQuestVarById(0));
        Assert.Equal(QuestStatus.START, state.GetStatus());
        Assert.True(player.GetController().HasTask(TaskId.QUEST_TIMER));
        Assert.Equal(7, player.GetLifeStats().GetCurrentFp()); // flight aid still applies on a wrong ring
        QuestService.QuestTimerEnd(env);
    }

    [Fact]
    public void OnlyTheExpectedRingAdvancesAndTheSeventhCorrectRingCompletesTheCourse()
    {
        using var harness = BossAiHarness.For().Build();
        using var questData = new QuestDataScope();
        var player = harness.SpawnPlayer();
        var state = new QuestState(1354, QuestStatus.START, 1, 0, 0, null, null, null);
        Assert.True(player.GetQuestStateList().AddQuest(1354, state));
        var env = new QuestEnv(null!, player, 1354);
        QuestService.QuestTimerStart(env, 120);
        var handler = new _1354PraticalAerobatics();
        Assert.True(handler.OnPassFlyingRingEvent(env, Ring(4)));
        Assert.Equal(1, state.GetQuestVarById(0));
        for (int i = 0; i < Order.Length; i++)
        {
            Assert.True(handler.OnPassFlyingRingEvent(env, Ring(Order[i])));
            Assert.Equal(i + 2, state.GetQuestVarById(0));
            Assert.Equal(i == 6 ? QuestStatus.REWARD : QuestStatus.START, state.GetStatus());
            Assert.Equal(i != 6, player.GetController().HasTask(TaskId.QUEST_TIMER));
        }
        player.GetLifeStats().SetCurrentFp(0);
        Assert.False(handler.OnPassFlyingRingEvent(env, Ring(7)));
        Assert.Equal(0, player.GetLifeStats().GetCurrentFp());
        Assert.Empty(harness.Clock.Faults);
    }

    [Theory]
    [InlineData(QuestStatus.REWARD)]
    [InlineData(QuestStatus.COMPLETE)]
    [InlineData(QuestStatus.LOCKED)]
    public void InactiveQuestDoesNotReceiveRingEffects(QuestStatus status)
    {
        using var harness = BossAiHarness.For().Build();
        using var questData = new QuestDataScope();
        var player = harness.SpawnPlayer();
        var state = new QuestState(1354, status, 7, 0, 0, null, null, null);
        Assert.True(player.GetQuestStateList().AddQuest(1354, state));
        player.GetLifeStats().SetCurrentFp(0);
        Assert.False(new _1354PraticalAerobatics().OnPassFlyingRingEvent(new QuestEnv(null!, player, 1354), Ring(7)));
        Assert.Equal(status, state.GetStatus());
        Assert.Equal(7, state.GetQuestVarById(0));
        Assert.Equal(0, player.GetLifeStats().GetCurrentFp());
    }

    [Theory]
    [InlineData(1, 395.2971f, 400.3083f, 284.2288f, 396.3625f, 400.4995f, 278.3212f)]
    [InlineData(2, 396.68726f, 272.0643f, 184.19234f, 396.68726f, 217.1849f, 174.19234f)]
    [InlineData(3, 121.85907f, 235.36667f, 189.87363f, 128.51076f, 235.36667f, 184.87363f)]
    [InlineData(4, 238.3586f, 370.1238f, 112.060295f, 236.3586f, 360.4345f, 106.995579f)]
    [InlineData(5, 331.80045f, 144.4999f, 131.75928f, 331.3166f, 138.44229f, 125.31162f)]
    [InlineData(6, 206.6613f, 135.5128f, 132.3650f, 207.8784f, 139.3262f, 127.6028f)]
    [InlineData(7, 264.9826f, 206.1993f, 298.4111f, 258.91852f, 206.88841f, 291.34375f)]
    public void AllSevenCorrectedOrientationsLoadExactly(int number, float x1, float y1, float z1, float x2, float y2, float z2)
    {
        var ring = Assert.Single(LoadRings().GetFlyRingTemplates(), r => r.GetName() == Ring(number));
        Assert.Equal(210020000, ring.GetMap());
        Assert.Equal(6f, ring.GetRadius());
        Assert.Equal(new[] { x1, y1, z1 }, new[] { ring.GetP1().GetX(), ring.GetP1().GetY(), ring.GetP1().GetZ() });
        Assert.Equal(new[] { x2, y2, z2 }, new[] { ring.GetP2().GetX(), ring.GetP2().GetY(), ring.GetP2().GetZ() });
    }

    [Fact]
    public void CorrectedThirdRingDetectsCrossingItsActualPlane()
    {
        using var harness = BossAiHarness.For(210020000).Build();
        var template = Assert.Single(LoadRings().GetFlyRingTemplates(), r => r.GetName() == Ring(3));
        var ring = new FlyRing(template, 1);
        var center = template.GetCenter();
        Assert.True(ring.IsCrossed(new Vector3f(center.x, center.y - 1, center.z), new Vector3f(center.x, center.y + 1, center.z)));
        Assert.False(ring.IsCrossed(new Vector3f(center.x - 1, center.y, center.z), new Vector3f(center.x + 1, center.y, center.z)));
        Assert.False(ring.IsCrossed(new Vector3f(center.x + 7, center.y - 1, center.z), new Vector3f(center.x + 7, center.y + 1, center.z)));
    }

    private static FlyRingData LoadRings() => JaxbHolderLoader.LoadFromFile<FlyRingData>(
        Path.Combine(RealStaticData.RepoRoot(), "game-server/data/static_data/fly_rings/fly_rings.xml"));

    private sealed class QuestDataScope : IDisposable
    {
        private readonly StaticData data = DataManager.GetRegisteredInstance()!.StaticData;
        private readonly PropertyInfo property = typeof(StaticData).GetProperty(nameof(StaticData.Quests))!;
        private readonly object? previous;
        public QuestDataScope()
        {
            previous = property.GetValue(data);
            property.SetValue(data, new QuestsData());
        }
        public void Dispose() => property.SetValue(data, previous);
    }
}
