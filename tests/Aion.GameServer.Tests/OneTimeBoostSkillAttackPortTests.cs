using System.Reflection;
using System.Xml.Serialization;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.Model.Stats.Calc.Functions;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.TestKit;
using Aion.GameServer.Tests.Ai;
using Aion.GameServer.Utils.Stats;
using Rnd = Aion.GameServer.Commons.Utils.Rnd;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class OneTimeBoostSkillAttackPortTests
{
    [Theory]
    [MemberData(nameof(Eligibility))]
    public void OnlyUpstreamEligibleAttackEffectsGetBoosted(EffectTemplate template, SkillType? expected)
        => Assert.Equal(expected, OneTimeBoostSkillAttackEffect.GetBoostedSkillType(template));

    public static TheoryData<EffectTemplate, SkillType?> Eligibility => new()
    {
        { new SkillAttackInstantEffect(), SkillType.PHYSICAL },
        { new SkillAtkDrainInstantEffect(), SkillType.PHYSICAL },
        { new DashEffect(), SkillType.PHYSICAL },
        { new BackDashEffect(), SkillType.PHYSICAL },
        { new MoveBehindEffect(), SkillType.PHYSICAL },
        { new CarveSignetEffect(), SkillType.PHYSICAL },
        { new SpellAttackInstantEffect(), SkillType.MAGICAL },
        { new DelayedSpellAttackInstantEffect(), SkillType.MAGICAL },
        { new SpellAtkDrainInstantEffect(), SkillType.MAGICAL },
        { new ProcAtkInstantEffect(), null },
        { new SignetBurstEffect(), null },
        { new NoReduceSpellATKInstantEffect(), null },
        { new DispelBuffCounterAtkEffect(), null },
        { new PoisonEffect(), null },
        { new RootEffect(), null },
        { new HealEffect(), null },
    };

    [Fact]
    public void XmlParametersScaleDamageAccuracyAndChargesWithTheSkillLevel()
    {
        using var harness = BossAiHarness.For().Build();
        var player = harness.SpawnPlayer();
        var template = Deserialize("type=\"ALL\" value=\"10\" delta=\"5\" count=\"2\" count_delta=\"1\" " +
            "dmg_flat=\"7\" dmg_flat_delta=\"2\" acc_flat=\"11\" acc_flat_delta=\"4\" acc_percent=\"20\" acc_percent_delta=\"3\"");
        var effect = StartBoost(player, template, 3);
        var boost = Assert.IsType<OneTimeBoostSkillAttack>(player.GetObserveController().GetOneTimeBoostSkillAttack(SkillType.PHYSICAL));
        Assert.Same(effect, boost.GetEffect());
        Assert.Equal(138f, boost.CalculateDamage(100)); // 25% + 13 flat
        var accuracy = Accuracy();
        Assert.Equal(313, boost.CalculatePhysicalAccuracyBonus(accuracy)); // base only: 29% + 23
        Assert.Equal(2338, boost.CalculateMagicalAccuracy(accuracy, 50)); // includes existing bonuses again
        for (int i = 0; i < 4; i++)
        {
            boost.ConsumeCharge();
            Assert.Same(boost, player.GetObserveController().GetOneTimeBoostSkillAttack(SkillType.MAGICAL));
        }
        boost.ConsumeCharge();
        Assert.Same(boost, player.GetObserveController().GetOneTimeBoostSkillAttack(SkillType.MAGICAL));
        harness.Clock.Advance(TimeSpan.FromMilliseconds(9));
        Assert.Same(boost, player.GetObserveController().GetOneTimeBoostSkillAttack(SkillType.MAGICAL));
        harness.Clock.Advance(TimeSpan.FromMilliseconds(1));
        Assert.Null(player.GetObserveController().GetOneTimeBoostSkillAttack(SkillType.MAGICAL));
        Assert.Empty(harness.Clock.Faults);
    }

    [Theory]
    [InlineData(SkillType.PHYSICAL, true, false)]
    [InlineData(SkillType.MAGICAL, false, true)]
    [InlineData(SkillType.ALL, true, true)]
    [InlineData(SkillType.NONE, false, false)]
    public void BoostTypeControlsBothAccuracyAndDamageEligibility(SkillType type, bool physical, bool magical)
    {
        using var harness = BossAiHarness.For().Build();
        var player = harness.SpawnPlayer();
        var effect = StartBoost(player, new OneTimeBoostSkillAttackEffect { type = type, Value = 20, count = 1 });
        Assert.Equal(physical, OneTimeBoostSkillAttackEffect.GetActiveBoost(player, new SkillAttackInstantEffect()) != null);
        Assert.Equal(magical, OneTimeBoostSkillAttackEffect.GetActiveBoost(player, new SpellAttackInstantEffect()) != null);
        Assert.Null(OneTimeBoostSkillAttackEffect.GetActiveBoost(player, new PoisonEffect()));
        effect.EndEffect(false);
    }

    [Fact]
    public void MissingXmlTypeInstallsNoBoost()
    {
        using var harness = BossAiHarness.For().Build();
        var player = harness.SpawnPlayer();
        var effect = StartBoost(player, Deserialize("value=\"30\" count=\"5\""));
        Assert.Null(player.GetObserveController().GetOneTimeBoostSkillAttack(SkillType.PHYSICAL));
        effect.EndEffect(false);
    }

    [Fact]
    public void AccuracyOnlyBoostLastsForItsDurationWithoutConsumingCharges()
    {
        using var harness = BossAiHarness.For().Build();
        var player = harness.SpawnPlayer();
        StartBoost(player, new OneTimeBoostSkillAttackEffect { type = SkillType.ALL, accFlat = 500, count = 1, Duration2 = 5000 });
        var boost = player.GetObserveController().GetOneTimeBoostSkillAttack(SkillType.PHYSICAL)!;
        for (int i = 0; i < 10; i++) boost.ConsumeCharge();
        Assert.Same(boost, player.GetObserveController().GetOneTimeBoostSkillAttack(SkillType.PHYSICAL));
        harness.Clock.Advance(TimeSpan.FromSeconds(5));
        Assert.Null(player.GetObserveController().GetOneTimeBoostSkillAttack(SkillType.PHYSICAL));
        Assert.Empty(harness.Clock.Faults);
    }

    [Fact]
    public void LastBoostReplacesThePreviousOneAndItsExpiryCannotRemoveTheReplacement()
    {
        using var harness = BossAiHarness.For().Build();
        var player = harness.SpawnPlayer();
        var first = StartBoost(player, new OneTimeBoostSkillAttackEffect { type = SkillType.ALL, Value = 20, count = 1 });
        var second = StartBoost(player, new OneTimeBoostSkillAttackEffect { type = SkillType.PHYSICAL, Value = 30, count = 1 });
        first.EndEffect(false);
        var boost = player.GetObserveController().GetOneTimeBoostSkillAttack(SkillType.PHYSICAL)!;
        Assert.Same(second, boost.GetEffect());
        Assert.Equal(130f, boost.CalculateDamage(100));
        Assert.Null(player.GetObserveController().GetOneTimeBoostSkillAttack(SkillType.MAGICAL));
        player.GetObserveController().Clear();
        Assert.Null(player.GetObserveController().GetOneTimeBoostSkillAttack(SkillType.PHYSICAL));
        second.EndEffect(false);
    }

    [Fact]
    public void MagicalAccuracyUsesBoostedAccuracyAndRepeatsExistingBonuses()
    {
        var boost = new OneTimeBoostSkillAttack(null!, SkillType.ALL, 0, 0, 20, 30, 1);
        var accuracy = Accuracy(); // 1000 base + 200*1.5 bonus + 100 fixed = 1400
        Assert.Equal(230, boost.CalculatePhysicalAccuracyBonus(accuracy));
        Assert.Equal(2215, boost.CalculateMagicalAccuracy(accuracy, 50));
        Assert.Equal(0, new OneTimeBoostSkillAttack(null!, SkillType.ALL, 0, 0, -300, 0, 1).CalculateMagicalAccuracy(accuracy, 0));
        accuracy.SetBase(1_000_000_000);
        Assert.Equal(int.MaxValue, new OneTimeBoostSkillAttack(null!, SkillType.ALL, 0, 0, 2000, 0, 1).CalculateMagicalAccuracy(accuracy, 0));
        accuracy.SetBonus(float.NaN);
        Assert.Equal(0, boost.CalculateMagicalAccuracy(accuracy, 0));
    }

    [Fact]
    public void MagicalResistRateAcceptsTheResolvedBoostWithoutConsumingIt()
    {
        using var harness = BossAiHarness.For().Build();
        var attacker = harness.SpawnPlayer();
        var target = harness.SpawnPlayer();
        var effect = StartBoost(attacker, new OneTimeBoostSkillAttackEffect { type = SkillType.MAGICAL, accFlat = 500, count = 1 });
        var boost = attacker.GetObserveController().GetOneTimeBoostSkillAttack(SkillType.MAGICAL)!;
        int plain = StatFunctions.CalculateMagicalResistRate(attacker, target, 17, SkillElement.FIRE);
        int boosted = StatFunctions.CalculateMagicalResistRate(attacker, target, 17, SkillElement.FIRE, boost);
        int mAccuracy = attacker.GetGameStats().GetMAccuracy().GetCurrent() + 17;
        int mResist = target.GetGameStats().GetMResist().GetCurrent();
        Assert.Equal(Math.Min(500, mResist - mAccuracy), plain);
        Assert.Equal(Math.Min(500, mResist - boost.CalculateMagicalAccuracy(attacker.GetGameStats().GetMAccuracy(), 17)), boosted);
        Assert.True(boosted < plain);
        Assert.Same(boost, attacker.GetObserveController().GetOneTimeBoostSkillAttack(SkillType.MAGICAL));
        effect.EndEffect(false);
    }

    [Fact]
    public void RealSkillDamageGetsTheBonusForAllCalculationsBeforeTheDeferredExpiry()
    {
        using var harness = BossAiHarness.For().Build();
        var player = harness.SpawnPlayer();
        var template = new SpellAttackInstantEffect { Position = 1, Element = SkillElement.FIRE };
        int Damage()
        {
            var effect = new Effect(player, player, Skill(template), 1);
            AttackUtil.CalculateSkillResult(effect, 100, template, true);
            return effect.GetReserveds(1).GetValue();
        }
        int plain = Damage();
        StartBoost(player, new OneTimeBoostSkillAttackEffect { type = SkillType.MAGICAL, Value = 50, dmgFlat = 10, count = 1 });
        Assert.Equal((int)(plain * 1.5f + 10), Damage());
        Assert.Equal((int)(plain * 1.5f + 10), Damage());
        harness.Clock.Advance(TimeSpan.FromMilliseconds(10));
        Assert.Null(player.GetObserveController().GetOneTimeBoostSkillAttack(SkillType.MAGICAL));
        Assert.Equal(plain, Damage());
    }

    [Fact]
    public void PeriodicAndProcDamageDoNotReceiveOrConsumeTheBoost()
    {
        using var harness = BossAiHarness.For().Build();
        var player = harness.SpawnPlayer();
        var poison = new PoisonEffect { Position = 1, Element = SkillElement.FIRE };
        var proc = new ProcAtkInstantEffect { Position = 1, Element = SkillElement.FIRE };
        int Periodic() => AttackUtil.CalculateMagicalOverTimeSkillResult(new Effect(player, player, Skill(poison), 1), 100, poison, false);
        int Proc()
        {
            var effect = new Effect(player, player, Skill(proc), 1);
            AttackUtil.CalculateSkillResult(effect, 100, proc, true);
            return effect.GetReserveds(1).GetValue();
        }
        int periodic = Periodic(), procDamage = Proc();
        var boostEffect = StartBoost(player, new OneTimeBoostSkillAttackEffect { type = SkillType.ALL, Value = 500, dmgFlat = 500, count = 1 });
        Assert.Equal(periodic, Periodic());
        Assert.Equal(procDamage, Proc());
        Assert.Same(boostEffect, player.GetObserveController().GetOneTimeBoostSkillAttack(SkillType.MAGICAL)!.GetEffect());
        boostEffect.EndEffect(false);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InitialDodgeAndResistRollsUseTheBoost(bool magical)
    {
        using var harness = BossAiHarness.For().Build();
        var attacker = harness.SpawnPlayer();
        var target = harness.SpawnPlayer();
        EffectTemplate template = magical ? new SpellAttackInstantEffect { Element = SkillElement.FIRE } : new SkillAttackInstantEffect();
        var effect = new Effect(attacker, target, Skill(template), 1);
        target.GetGameStats().AddEffect(effect, new IStatFunction[] {
            new StatSetFunction(StatEnum.EVASION, 2000), new StatSetFunction(StatEnum.MAGICAL_RESIST, 2000) });
        var boostEffect = StartBoost(attacker, new OneTimeBoostSkillAttackEffect { type = SkillType.ALL, accFlat = 100_000 });
        var boost = attacker.GetObserveController().GetOneTimeBoostSkillAttack(SkillType.PHYSICAL)!;
        var check = typeof(EffectTemplate).GetMethod("CheckDodgeOrResistRate", BindingFlags.Instance | BindingFlags.NonPublic)!;
        int changed = 0;
        try
        {
            for (int seed = 0; seed < 16; seed++)
            {
                attacker.GetObserveController().RemoveOneTimeBoostSkillAttack(boostEffect);
                Rnd.UseSeed(seed);
                bool plain = (bool)check.Invoke(template, new object[] { effect })!;
                attacker.GetObserveController().SetOneTimeBoostSkillAttack(boost);
                Rnd.UseSeed(seed);
                bool boosted = (bool)check.Invoke(template, new object[] { effect })!;
                Assert.True(boosted);
                if (plain != boosted) changed++;
            }
            Assert.True(changed > 0);
        }
        finally { Rnd.UseProductionRandom(); boostEffect.EndEffect(false); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PhysicalStatusUsesAccuracyBonusUnlessTheAttackIsNoResist(bool noResist)
    {
        using var harness = BossAiHarness.For().Build();
        var attacker = harness.SpawnPlayer();
        var target = harness.SpawnPlayer();
        var template = new SkillAttackInstantEffect { NoResist = noResist, CritProbMod2 = 0 };
        var effect = new Effect(attacker, target, Skill(template), 1);
        target.GetGameStats().AddEffect(effect, new IStatFunction[] { new StatSetFunction(StatEnum.PARRY, 2000) });
        var boostEffect = StartBoost(attacker, new OneTimeBoostSkillAttackEffect { type = SkillType.PHYSICAL, accFlat = 100_000 });
        var boost = attacker.GetObserveController().GetOneTimeBoostSkillAttack(SkillType.PHYSICAL)!;
        var check = typeof(AttackUtil).GetMethod("CalculatePhysicalStatus", BindingFlags.Static | BindingFlags.NonPublic,
            null, new[] { typeof(Creature), typeof(Creature), typeof(EffectTemplate), typeof(Effect) }, null)!;
        int changed = 0;
        try
        {
            for (int seed = 0; seed < 16; seed++)
            {
                attacker.GetObserveController().RemoveOneTimeBoostSkillAttack(boostEffect);
                Rnd.UseSeed(seed);
                var plain = (AttackStatus)check.Invoke(null, new object[] { attacker, target, template, effect })!;
                attacker.GetObserveController().SetOneTimeBoostSkillAttack(boost);
                Rnd.UseSeed(seed);
                var boosted = (AttackStatus)check.Invoke(null, new object[] { attacker, target, template, effect })!;
                if (plain != boosted) changed++;
                if (!noResist) Assert.Equal(AttackStatus.NORMALHIT, boosted);
            }
            Assert.Equal(noResist, changed == 0);
        }
        finally { Rnd.UseProductionRandom(); boostEffect.EndEffect(false); }
    }

    [Fact]
    public async Task UpdatedDataLoadsTheAccuracyAndFlatDamageParameters()
    {
        var real = await RealStaticData.LoadAsync();
        var previous = DataManager.GetRegisteredInstance();
        try
        {
            DataManager.RegisterInstance(real);
            var physical = Assert.IsType<OneTimeBoostSkillAttackEffect>(Assert.IsType<SkillTemplate>(DataManager.SKILL_DATA.GetSkillTemplate(813)).GetEffects()!.GetEffects()[0]);
            Assert.Equal(500, physical.accFlat);
            foreach (int id in new[] { 13194, 13195, 13196, 13197, 13272 })
            {
                var effect = Assert.IsType<OneTimeBoostSkillAttackEffect>(Assert.IsType<SkillTemplate>(DataManager.SKILL_DATA.GetSkillTemplate(id)).GetEffects()!.GetEffects()[0]);
                Assert.Equal(1, effect.dmgFlat);
                Assert.Equal(1, effect.count);
            }
        }
        finally { DataManager.RestoreInstance(previous); }
    }

    private static AdditionStat Accuracy()
    {
        var accuracy = new AdditionStat(StatEnum.ACCURACY, 1000, null!);
        accuracy.SetBonus(200);
        accuracy.SetBonusRate(1.5f);
        accuracy.SetFixedBonusRate(0.1f);
        return accuracy;
    }

    private static OneTimeBoostSkillAttackEffect Deserialize(string attributes)
        => (OneTimeBoostSkillAttackEffect)new XmlSerializer(typeof(OneTimeBoostSkillAttackEffect))
            .Deserialize(new StringReader("<OneTimeBoostSkillAttackEffect " + attributes + " />"))!;

    private static Effect StartBoost(Player player, OneTimeBoostSkillAttackEffect template, int level = 1)
    {
        template.Position = 1;
        var effect = new Effect(player, player, Skill(template), level);
        effect.AddSuccessEffect(template);
        effect.StartEffect();
        return effect;
    }

    private static SkillTemplate Skill(EffectTemplate template)
    {
        var effects = new Effects { effects = new List<EffectTemplate> { template } };
        typeof(Effects).GetField("effectTypes", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(effects, new HashSet<EffectType>());
        return new SkillTemplate { skillId = 1, stack = "BOOST_PORT_PROBE", effects = effects,
            type = SkillType.MAGICAL, activationAttribute = ActivationAttribute.ACTIVE };
    }
}
