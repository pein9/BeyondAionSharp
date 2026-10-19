using System.Reflection;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Tests.Ai;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class OverTimeTickCountTests
{
    [Theory]
    [InlineData(10000, 1000, 11000)]
    [InlineData(10500, 1000, 11000)]
    [InlineData(3500, 2000, 3000)]
    [InlineData(1000, 1000, 2000)]
    [InlineData(10000, 0, 10000)]
    [InlineData(0, 1000, 0)]
    [InlineData(-5, 1000, -5)]
    public void UpstreamDurationVectors(int duration, int interval, long expected)
        => Assert.Equal(expected, new TickProbe { checktime = interval }.RoundDurationToTicks(duration));

    [Theory]
    [InlineData(1000, 1000, 1)]
    [InlineData(2000, 1000, 2)]
    [InlineData(10000, 1000, 10)]
    [InlineData(10000, 2000, 5)]
    [InlineData(10000, 500, 21)]
    [InlineData(9000, 3000, 3)]
    [InlineData(10500, 1000, 10)]
    [InlineData(3500, 2000, 1)]
    public void UpstreamTickCountVectors(int duration, int interval, int expected)
    {
        var probe = new TickProbe { checktime = interval };
        Assert.Equal(expected, probe.GetTickCount((int)probe.RoundDurationToTicks(duration)));
    }

    [Theory]
    [InlineData(0, 1000)]
    [InlineData(-5, 1000)]
    [InlineData(10000, 0)]
    public void NoTickBudgetWithoutPositiveDurationAndInterval(int duration, int interval)
        => Assert.Equal(0, new TickProbe { checktime = interval }.GetTickCount(duration));

    [Theory]
    [InlineData(1000, 1000, 2000, 1)]
    [InlineData(10500, 1000, 11000, 10)]
    [InlineData(3500, 2000, 3000, 1)]
    [InlineData(10000, 500, 11000, 21)]
    [InlineData(9000, 3000, 10000, 3)]
    public void RealLifecycleCalculatesBeforeSchedulingAndTicksAtTheInterval(int rawDuration, int interval, int duration, int ticks)
    {
        using var harness = BossAiHarness.For().Build();
        var player = harness.SpawnPlayer();
        var probe = new TickProbe { Position = 1, Duration2 = rawDuration, checktime = interval,
            Now = () => harness.Clock.NowMillis };
        var effect = new Effect(player, player, Skill(probe), 1);
        effect.AddSuccessEffect(probe);
        effect.StartEffect();
        Assert.Equal(duration, probe.DurationAtStart);
        Assert.Equal(duration, effect.GetDuration());
        harness.Clock.Advance(TimeSpan.FromMilliseconds(interval - 1));
        Assert.Empty(probe.Ticks);
        harness.Clock.Advance(TimeSpan.FromMilliseconds(1));
        Assert.Equal(interval, Assert.Single(probe.Ticks));
        // The repeating task was queued before the end task. Its explicit budget must
        // suppress an endpoint tick even when both are due at the same instant.
        harness.Clock.Advance(TimeSpan.FromMilliseconds(duration + interval));
        Assert.Equal(Enumerable.Range(1, ticks).Select(i => (long)i * interval), probe.Ticks);
        Assert.Equal(1, probe.Ends);
        Assert.Empty(harness.Clock.Faults);
    }

    [Theory]
    [InlineData(50, 6000, 5)]
    [InlineData(150, 16000, 15)]
    public void PvpMultiplierPrecedesRounding(int percentage, int expectedDuration, int ticks)
    {
        using var harness = BossAiHarness.For().Build();
        var probe = new TickProbe { Position = 1, Duration2 = 10500, checktime = 1000 };
        var skill = Skill(probe);
        skill.pvpDuration = percentage;
        var effect = new Effect(harness.SpawnPlayer(), harness.SpawnPlayer(), skill, 1);
        effect.AddSuccessEffect(probe);
        effect.StartEffect();
        Assert.Equal(expectedDuration, effect.GetDuration());
        harness.Clock.Advance(TimeSpan.FromMilliseconds(expectedDuration + 2000));
        Assert.Equal(ticks, probe.Ticks.Count);
        Assert.Empty(harness.Clock.Faults);
    }

    [Fact]
    public void ExplicitDurationKeepsItsValueAndStillCapsTicksAtTheEndpoint()
    {
        using var harness = BossAiHarness.For().Build();
        var player = harness.SpawnPlayer();
        var probe = new TickProbe { Position = 1, Duration2 = 10500, checktime = 1000 };
        var effect = new Effect(player, player, Skill(probe), 1, 2000, null!);
        effect.AddSuccessEffect(probe);
        effect.StartEffect();
        Assert.Equal(2000, probe.DurationAtStart);
        harness.Clock.Advance(TimeSpan.FromSeconds(4));
        Assert.Single(probe.Ticks);
        Assert.Equal(1, probe.Ends);
        Assert.Empty(harness.Clock.Faults);
    }

    [Fact]
    public void PermanentToggleTicksUntilRemoved()
    {
        using var harness = BossAiHarness.For().Build();
        var player = harness.SpawnPlayer();
        var probe = new TickProbe { Position = 1, Duration2 = 1000, checktime = 1000 };
        var skill = Skill(probe);
        skill.activationAttribute = ActivationAttribute.TOGGLE;
        var effect = new Effect(player, player, skill, 1);
        effect.AddSuccessEffect(probe);
        effect.StartEffect();
        Assert.Equal(0, effect.GetDuration());
        harness.Clock.Advance(TimeSpan.FromSeconds(5));
        Assert.Equal(5, probe.Ticks.Count);
        effect.EndEffect(false);
        harness.Clock.Advance(TimeSpan.FromSeconds(5));
        Assert.Equal(5, probe.Ticks.Count);
        Assert.Equal(1, probe.Ends);
        Assert.Empty(harness.Clock.Faults);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OnlyTheFirstPositiveDurationDeterminesRounding(bool periodicFirst)
    {
        using var harness = BossAiHarness.For().Build();
        var player = harness.SpawnPlayer();
        var periodic = new TickProbe { Position = periodicFirst ? 1 : 2, Duration2 = periodicFirst ? 3500 : 12000, checktime = 2000 };
        var ordinary = new DurationProbe { Position = periodicFirst ? 2 : 1, Duration2 = periodicFirst ? 12000 : 3500 };
        var effect = new Effect(player, player, Skill(periodic, ordinary), 1);
        effect.AddSuccessEffect(periodic);
        effect.AddSuccessEffect(ordinary);
        Assert.Equal(1, effect.GetSuccessEffects().First().Position);
        effect.StartEffect();
        Assert.Equal(periodicFirst ? 3000 : 3500, effect.GetDuration());
        effect.EndEffect(false);
    }

    [Fact]
    public void DurationUsesLongArithmeticBeforeTheFinalIntClamp()
    {
        using var harness = BossAiHarness.For().Build();
        var player = harness.SpawnPlayer();
        var probe = new TickProbe { Position = 1, Duration2 = int.MaxValue, Duration1 = int.MaxValue, checktime = 1000 };
        var effect = new Effect(player, player, Skill(probe), 2);
        effect.AddSuccessEffect(probe);
        effect.StartEffect();
        Assert.Equal(int.MaxValue, effect.GetDuration());
        effect.EndEffect(false);
        Assert.Equal(unchecked(long.MaxValue + 1000 - long.MaxValue % 1000), probe.RoundDurationToTicks(long.MaxValue));
    }

    private static SkillTemplate Skill(params EffectTemplate[] templates)
    {
        var effects = new Effects { effects = templates.ToList() };
        typeof(Effects).GetField("effectTypes", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(effects, new HashSet<EffectType>());
        return new SkillTemplate { skillId = 1, stack = "OVERTIME_PORT_PROBE", activationAttribute = ActivationAttribute.ACTIVE, effects = effects };
    }

    private sealed class TickProbe : AbstractOverTimeEffect
    {
        public Func<long> Now = () => 0;
        public List<long> Ticks { get; } = new();
        public int DurationAtStart;
        public int Ends;
        public override void StartEffect(Effect effect)
        {
            DurationAtStart = effect.GetDuration();
            base.StartEffect(effect);
        }
        public override void OnPeriodicAction(Effect effect) => Ticks.Add(Now());
        public override void EndEffect(Effect effect) => Ends++;
    }

    private sealed class DurationProbe : EffectTemplate
    {
        public override void ApplyEffect(Effect effect) { }
    }
}
