using System.Reflection;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Tests.Ai;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class OneTimeBoostSkillAttackExpiryTests
{
    [Fact]
    public void AllTargetsAndEffectsOfTheLastAttackReceiveTheBonus()
    {
        using var harness = BossAiHarness.For().Build();
        var attacker = harness.SpawnPlayer();
        var firstTarget = harness.SpawnPlayer();
        var secondTarget = harness.SpawnPlayer();
        var spell = new SpellAttackInstantEffect { Position = 1, Element = SkillElement.FIRE };
        var delayed = new DelayedSpellAttackInstantEffect { Position = 2, Element = SkillElement.FIRE };
        int Damage(Player target, DamageEffect template)
        {
            var effect = new Effect(attacker, target, Skill(template), 1);
            AttackUtil.CalculateSkillResult(effect, 100, template, true);
            return effect.GetReserveds(template.Position).GetValue();
        }
        int plainFirst = Damage(firstTarget, spell), plainSecond = Damage(secondTarget, spell);
        int plainDelayed = Damage(secondTarget, delayed);
        var boost = new TrackingBoost { type = SkillType.MAGICAL, count = 1, dmgFlat = 100 };
        var active = Start(attacker, boost);
        // The existing PvP modifier scales the 100-point bonus to 42 damage.
        Assert.Equal(plainFirst + 42, Damage(firstTarget, spell));
        Assert.Equal(plainSecond + 42, Damage(secondTarget, spell));
        Assert.Equal(plainDelayed + 42, Damage(secondTarget, delayed));
        harness.Clock.Advance(TimeSpan.FromMilliseconds(9));
        Assert.Equal(0, boost.Ends);
        Assert.Same(active, attacker.GetObserveController().GetOneTimeBoostSkillAttack(SkillType.MAGICAL)!.GetEffect());
        harness.Clock.Advance(TimeSpan.FromMilliseconds(1));
        Assert.Equal(1, boost.Ends);
        Assert.Null(attacker.GetObserveController().GetOneTimeBoostSkillAttack(SkillType.MAGICAL));
        Assert.Equal(plainSecond, Damage(secondTarget, spell));
        Assert.Empty(harness.Clock.Faults);
    }

    [Fact]
    public void ExpiryIsTenMillisecondsAfterTheFinalChargeRatherThanTheFirstCharge()
    {
        using var harness = BossAiHarness.For().Build();
        var player = harness.SpawnPlayer();
        var template = new TrackingBoost { type = SkillType.ALL, count = 2, Value = 30 };
        Start(player, template);
        var boost = player.GetObserveController().GetOneTimeBoostSkillAttack(SkillType.PHYSICAL)!;
        boost.ConsumeCharge();
        harness.Clock.Advance(TimeSpan.FromMilliseconds(50));
        Assert.Equal(0, template.Ends);
        boost.ConsumeCharge();
        harness.Clock.Advance(TimeSpan.FromMilliseconds(9));
        boost.ConsumeCharge(); // another effect/target cannot move the original deadline
        Assert.Equal(0, template.Ends);
        harness.Clock.Advance(TimeSpan.FromMilliseconds(1));
        Assert.Equal(1, template.Ends);
        Assert.Null(player.GetObserveController().GetOneTimeBoostSkillAttack(SkillType.PHYSICAL));
        Assert.Empty(harness.Clock.Faults);
    }

    [Fact]
    public void DeferredOldExpiryCannotRemoveANewBoost()
    {
        using var harness = BossAiHarness.For().Build();
        var player = harness.SpawnPlayer();
        var first = new TrackingBoost { type = SkillType.ALL, count = 1, Value = 20 };
        Start(player, first);
        player.GetObserveController().GetOneTimeBoostSkillAttack(SkillType.PHYSICAL)!.ConsumeCharge();
        harness.Clock.Advance(TimeSpan.FromMilliseconds(2));
        var second = new TrackingBoost { type = SkillType.PHYSICAL, count = 1, Value = 30 };
        var replacement = Start(player, second);
        harness.Clock.Advance(TimeSpan.FromMilliseconds(8));
        Assert.Equal(1, first.Ends);
        Assert.Equal(0, second.Ends);
        var boost = player.GetObserveController().GetOneTimeBoostSkillAttack(SkillType.PHYSICAL)!;
        Assert.Same(replacement, boost.GetEffect());
        Assert.Equal(130f, boost.CalculateDamage(100));
        replacement.EndEffect(false);
        Assert.Empty(harness.Clock.Faults);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NonPositiveInitialCountsDoNotCrossTheZeroSchedulingTrigger(int count)
    {
        using var harness = BossAiHarness.For().Build();
        var player = harness.SpawnPlayer();
        var template = new TrackingBoost { type = SkillType.ALL, count = count, dmgFlat = 1 };
        var effect = Start(player, template);
        var boost = player.GetObserveController().GetOneTimeBoostSkillAttack(SkillType.PHYSICAL)!;
        boost.ConsumeCharge();
        harness.Clock.Advance(TimeSpan.FromMilliseconds(100));
        Assert.Equal(0, template.Ends);
        Assert.Same(boost, player.GetObserveController().GetOneTimeBoostSkillAttack(SkillType.PHYSICAL));
        effect.EndEffect(false);
    }

    private static Effect Start(Player player, OneTimeBoostSkillAttackEffect template)
    {
        template.Position = 1;
        var effect = new Effect(player, player, Skill(template), 1);
        effect.AddSuccessEffect(template);
        effect.StartEffect();
        return effect;
    }

    private static SkillTemplate Skill(EffectTemplate template)
    {
        var effects = new Effects { effects = new List<EffectTemplate> { template } };
        typeof(Effects).GetField("effectTypes", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(effects, new HashSet<EffectType>());
        return new SkillTemplate { skillId = 1, stack = "BOOST_EXPIRY_PROBE", effects = effects,
            type = SkillType.MAGICAL, activationAttribute = ActivationAttribute.ACTIVE };
    }

    private sealed class TrackingBoost : OneTimeBoostSkillAttackEffect
    {
        public int Ends;
        public override void EndEffect(Effect effect)
        {
            Ends++;
            base.EndEffect(effect);
        }
    }
}
