using System.Reflection;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Tests.Ai;
using ForceType = Aion.GameServer.SkillEngine.Model.Effect.ForceType;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class DelayedSkillPortTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("MATERIAL_SKILL")]
    [InlineData("PORT_DELAYED_EVENT")]
    public void ExpiryCarriesTheExactParentForceTypeAndOnlyNormalCastsCheckResistance(string? forceName)
    {
        using var harness = BossAiHarness.For().Build();
        var caster = harness.SpawnPlayer();
        var target = harness.SpawnPlayer();
        var probe = new CalculationProbe { Position = 1, ResistNow = false };
        using var template = new TemporarySkill(probe);
        var force = forceName == null ? null : ForceType.GetInstance(forceName);
        var parent = new Effect(caster, target, new SkillTemplate { skillId = 1 }, 1, null, force!);
        typeof(Effect).GetField("endedByTime", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(parent, true);
        // Resistance changes after the parent was created. The normal calculation gate must run at trigger time.
        probe.ResistNow = true;
        new DelayedSkillEffect { skillId = template.Id }.EndEffect(parent);
        Assert.Same(force, Assert.Single(probe.Calculations).GetForceType());
        Assert.Equal(force == null ? 1 : 0, probe.ResistChecks);
        Assert.Equal(force == null ? 0 : 1, probe.Applications);
    }

    [Fact]
    public void RemovalBeforeExpiryDoesNotTriggerTheDelayedSkill()
    {
        using var harness = BossAiHarness.For().Build();
        var caster = harness.SpawnPlayer();
        var target = harness.SpawnPlayer();
        var probe = new CalculationProbe { Position = 1 };
        using var template = new TemporarySkill(probe);
        var parent = new Effect(caster, target, new SkillTemplate { skillId = 1 }, 1);
        new DelayedSkillEffect { skillId = template.Id }.EndEffect(parent);
        Assert.Empty(probe.Calculations);
        Assert.Equal(0, probe.Applications);
    }

    [Fact]
    public void AnUnforcedTriggeredEffectStillAppliesWhenItsCalculationSucceeds()
    {
        using var harness = BossAiHarness.For().Build();
        var caster = harness.SpawnPlayer();
        var target = harness.SpawnPlayer();
        var probe = new CalculationProbe { Position = 1 };
        using var template = new TemporarySkill(probe);
        var parent = new Effect(caster, target, new SkillTemplate { skillId = 1 }, 1);
        typeof(Effect).GetField("endedByTime", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(parent, true);
        new DelayedSkillEffect { skillId = template.Id }.EndEffect(parent);
        Assert.Null(Assert.Single(probe.Calculations).GetForceType());
        Assert.Equal(1, probe.ResistChecks);
        Assert.Equal(1, probe.Applications);
    }

    /// <summary>Controls only the dodge/resist outcome; real Effect.Initialize/Calculate/ApplyEffect and the forced bypass run.</summary>
    private sealed class CalculationProbe : EffectTemplate
    {
        public bool ResistNow;
        public int ResistChecks;
        public int Applications;
        public List<Effect> Calculations { get; } = new();
        public override void Calculate(Effect effect)
        {
            Calculations.Add(effect);
            base.Calculate(effect);
        }
        protected override bool IsDodgedOrResisted(Effect effect, StatEnum? stat)
        {
            ResistChecks++;
            return ResistNow;
        }
        public override void ApplyEffect(Effect effect) => Applications++;
    }

    private sealed class TemporarySkill : IDisposable
    {
        public int Id => 999991;
        private readonly Dictionary<int, SkillTemplate> skills;
        public TemporarySkill(EffectTemplate probe)
        {
            var effects = new Effects { effects = new List<EffectTemplate> { probe } };
            // The probe is not a serializable effect type; no production data or retail normalization is changed.
            typeof(Effects).GetField("effectTypes", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(effects, new HashSet<EffectType>());
            skills = (Dictionary<int, SkillTemplate>)typeof(SkillData)
                .GetField("skillTemplateById", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(DataManager.SKILL_DATA)!;
            Assert.True(skills.TryAdd(Id, new SkillTemplate { skillId = Id, lvl = 7,
                activationAttribute = ActivationAttribute.ACTIVE, effects = effects }));
        }
        public void Dispose() => skills.Remove(Id);
    }
}
