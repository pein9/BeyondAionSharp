using System.Reflection;
using System.Xml.Linq;
using System.Xml.Serialization;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Controllers.Effects;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Tests.Ai;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class SignetAlignmentPortTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [Theory]
    [InlineData(0, 100, 1, 1, 1)]
    [InlineData(2, 100, 3, 3, 1)]
    [InlineData(3, 100, 0, 3, 0)]
    [InlineData(5, 100, 0, 5, 0)]
    [InlineData(2, 0, 0, 2, 1)]
    [InlineData(0, 0, 0, 0, 1)]
    public void CalculateChoosesThePacketCarveAndApplyRefreshesWithoutAnotherRoll(int current, int chance, int carved, int applied, int draws)
    {
        using var h = BossAiHarness.For().Build();
        SetSignetData();
        var caster = h.SpawnPlayer(); var target = h.SpawnPlayer();
        var active = current == 0 ? null : ActiveSignet(caster, target, current);
        var probe = new ApplicationProbe { Position = 1 };
        using var skills = new TemporarySkills(probe, 5);
        var carve = new CarveProbe { Position = 1, signet = "SIGNET1", signetId = skills.First, signetCap = 3, prob = chance };
        var cast = new Effect(caster, target, Skill(1, carve), 2);
        Rnd.UseSeed(123);
        try
        {
            carve.Calculate(cast);
            Assert.Equal(carved, cast.GetCarvedSignet()); // the outgoing cast packet reads this before ApplyEffect
            Assert.Equal(draws, Rnd.ActiveSeedDrawCount);
            Assert.Empty(probe.Applications);
            carve.ApplyEffect(cast);
            Assert.Equal(draws, Rnd.ActiveSeedDrawCount);
            if (applied == 0) Assert.Empty(probe.Applications);
            else
            {
                var signet = Assert.Single(probe.Applications);
                Assert.Equal(applied, signet.GetSkillLevel());
                Assert.Equal(skills.First + applied - 1, signet.GetSkillId());
                Assert.Equal(0, signet.GetCarvedSignet()); // no longer stored on the signet itself
            }
            if (active != null)
                Assert.True(((Aion.GameServer.Utils.AtomicBoolean)typeof(Effect).GetField("hasEnded", PrivateInstance)!.GetValue(active)!).Get());
        }
        finally { Rnd.UseProductionRandom(); }
    }

    [Fact]
    public void ApplyDoesNotDowngradeASignetThatAdvancedSinceCalculation()
    {
        using var h = BossAiHarness.For().Build(); SetSignetData();
        var caster = h.SpawnPlayer(); var target = h.SpawnPlayer();
        var probe = new ApplicationProbe { Position = 1 }; using var skills = new TemporarySkills(probe, 5);
        ActiveSignet(caster, target, 4);
        var carve = new CarveProbe { Position = 1, signet = "SIGNET1", signetId = skills.First };
        var cast = new Effect(caster, target, Skill(1, carve), 2); cast.SetCarvedSignet(3);
        carve.ApplyEffect(cast);
        Assert.Equal(4, Assert.Single(probe.Applications).GetSkillLevel());
    }

    [Fact]
    public void RejectedDamageCalculationDoesNotRollOrCarve()
    {
        using var h = BossAiHarness.For().Build();
        var caster = h.SpawnPlayer(); var target = h.SpawnPlayer();
        var carve = new CarveProbe { Position = 1, Reject = true, signet = "SIGNET1", signetCap = 3 };
        var cast = new Effect(caster, target, Skill(1, carve), 2);
        Rnd.UseSeed(123);
        try { carve.Calculate(cast); Assert.Equal(0, cast.GetCarvedSignet()); Assert.Equal(0, Rnd.ActiveSeedDrawCount); }
        finally { Rnd.UseProductionRandom(); }
    }

    [Theory]
    [InlineData(0, 2, 24)]
    [InlineData(1, 2, 40)]
    [InlineData(0, 3, 33)]
    public void CarveChanceCombinesSkillLevelAndNextSignetLevelAndTruncates(int current, int level, int expected)
    {
        using var h = BossAiHarness.For().Build();
        SetSignetData(new SignetData { Level = 1, CarveProb = 60 });
        var player = h.SpawnPlayer(); var carve = new CarveProbe { signet = "SIGNET1", prob = 10, probDelta = 15 };
        var cast = new Effect(player, player, Skill(1, carve), level);
        Assert.Equal(expected, typeof(CarveSignetEffect).GetMethod("CalculateCarveChance", PrivateInstance)!.Invoke(carve, new object[] { cast, current }));
    }

    [Theory]
    [InlineData(false, 4, 100, 0, 1)]
    [InlineData(true, 4, 100, 0, 5)]
    [InlineData(true, 0, 100, 0, 1)]
    [InlineData(true, 4, 0, 50, 5)]
    [InlineData(true, 4, 0, 0, -1)]
    public void SubEffectUsesParentChanceLevelAndBaseOnePlusBurstedCount(bool burst, int count, int chance, int delta, int expected)
    {
        using var h = BossAiHarness.For().Build(); var caster = h.SpawnPlayer(); var target = h.SpawnPlayer();
        var probe = new ApplicationProbe { Position = 1 }; using var skills = new TemporarySkills(probe, 1);
        var parent = new ApplicationProbe { Position = 1, SubEffectField = new SubEffect { SkillId = skills.First, AddEffect = burst, Chance = chance, ChanceDelta = delta } };
        var cast = new Effect(caster, target, Skill(1, parent), 2);
        cast.SetSignetBurstedCount(count); cast.SetShieldDefense(4);
        parent.CalculateSubEffect(cast);
        if (expected < 0) { Assert.Null(cast.GetSubEffect()); Assert.Empty(probe.Calculations); }
        else
        {
            Assert.Equal(expected, Assert.Single(probe.Calculations).GetSkillLevel());
            Assert.Equal(expected, cast.GetSubEffect().GetSkillLevel());
            Assert.Equal(4, cast.GetSubEffect().GetShieldDefense());
            Assert.True(cast.GetSubEffect().IsSubEffect());
        }
    }

    [Theory]
    [InlineData(0f, false)]
    [InlineData(0.5f, true)]
    [InlineData(float.PositiveInfinity, true)]
    [InlineData(float.NegativeInfinity, false)]
    [InlineData(float.NaN, false)]
    public void BurstUsesTheLevelScaledFloatProbabilityAndConsumesTheExistingSignet(float delta, bool launches)
    {
        using var h = BossAiHarness.For().Build();
        SetSignetData(new SignetData { Level = 2, DamageMultiplier = 1, AddEffectProb = 100 });
        var caster = h.SpawnPlayer(); var target = h.SpawnPlayer(); ActiveSignet(caster, target, 2);
        var burst = new SignetBurstEffect { Position = 1, signet = "SIGNET1", signetlvl = 3, Value = 1, addEffectProbMultiplierDelta = delta };
        var cast = new Effect(caster, target, Skill(1, burst), 2);
        burst.CalculateDamage(cast);
        Assert.Equal(2, cast.GetSignetBurstedCount()); Assert.Equal(launches, cast.IsLaunchSubEffect());
        Assert.Null(target.GetEffectController().GetAbnormalEffect("SIGNET1"));
    }

    [Theory]
    [InlineData(1.99f, 1)]
    [InlineData(-1.99f, -1)]
    [InlineData(float.NaN, 0)]
    [InlineData(float.PositiveInfinity, int.MaxValue)]
    [InlineData(float.NegativeInfinity, int.MinValue)]
    [InlineData(float.MaxValue, int.MaxValue)]
    [InlineData(float.MinValue, int.MinValue)]
    public void ProbabilityNarrowingRetainsJavaTruncationAndSaturation(float value, int expected)
        => Assert.Equal(expected, Aion.GameServer.Utils.JavaMath.ToInt(value));

    [Fact]
    public void XmlDefaultsAndNewDeltaAttributesLoadAndTheEightSkillUpdatesArePresent()
    {
        Assert.Equal(100, Read<SignetData>("<signet_data lvl='1' dmg_multi='1' />").GetCarveProb());
        Assert.Equal(1, Read<SignetData>("<signet_data lvl='1' dmg_multi='1' />").GetAddEffectProb());
        Assert.Equal(27, Read<SignetData>("<signet_data carve_prob='27' />").GetCarveProb());
        Assert.Equal(100, Read<SubEffect>("<SubEffect skill_id='1' />").GetChance(50));
        Assert.Equal(6, Read<SubEffect>("<SubEffect chance='0' chance_delta='2' />").GetChance(3));
        Assert.Equal(15, Read<CarveSignetEffect>("<CarveSignetEffect prob_delta='15' />").probDelta);
        Assert.Equal(0.5f, Read<SignetBurstEffect>("<SignetBurstEffect add_effect_prob_multi_delta='0.5' />").addEffectProbMultiplierDelta);
        var root = RepositoryRoot();
        var skills = XDocument.Load(Path.Combine(root, "game-server/data/static_data/skills/skill_templates.xml"));
        foreach (int id in new[] { 2530, 2531, 2532, 2533, 2534, 2535, 19579, 19645 })
        {
            var skill = Assert.Single(skills.Root!.Elements("skill_template"), e => (int?)e.Attribute("skill_id") == id);
            var sub = Assert.Single(skill.Descendants("subeffect"));
            Assert.Equal(id == 19579 ? 8224 : id == 19645 ? 8676 : 8218, (int?)sub.Attribute("skill_id"));
            Assert.Equal(0, (int?)sub.Attribute("chance"));
            Assert.Equal(id == 19579 ? 2 : 100, (int?)sub.Attribute("chance_delta"));
        }
    }

    private static T Read<T>(string xml) => (T)new XmlSerializer(typeof(T)).Deserialize(new StringReader(xml))!;
    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "AionServer.slnx"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException();
    }
    private static void SetSignetData(params SignetData[] data)
    {
        var holder = new SignetDataTemplates { signetDataTemplateList = new() { new SignetDataTemplate { Signet = SignetEnum.SIGNET1, SignetDataList = data.ToList() } } };
        holder.AfterUnmarshal(null!);
        typeof(StaticData).GetField("<SignetDataTemplatesDh>k__BackingField", PrivateInstance)!.SetValue(DataManager.GetRegisteredInstance()!.StaticData, holder);
    }
    private static Effect ActiveSignet(Player caster, Player target, int level)
    {
        var active = new Effect(caster, target, new SkillTemplate { skillId = 999970 + level, lvl = level, stack = "SIGNET1" }, level);
        active.SetCarvedSignet(99); // deliberately stale: the actual stack level is the skill level
        ((Dictionary<string, Effect>)typeof(EffectController).GetField("abnormalEffectMap", PrivateInstance)!.GetValue(target.GetEffectController())!)["SIGNET1"] = active;
        return active;
    }
    private static SkillTemplate Skill(int id, EffectTemplate template, int level = 1)
    {
        var effects = new Effects { effects = new List<EffectTemplate> { template } };
        typeof(Effects).GetField("effectTypes", PrivateInstance)!.SetValue(effects, new HashSet<EffectType>());
        return new SkillTemplate { skillId = id, lvl = level, stack = "SIGNET_PORT_PROBE", effects = effects, type = SkillType.PHYSICAL, activationAttribute = ActivationAttribute.ACTIVE };
    }
    private sealed class CarveProbe : CarveSignetEffect
    {
        public bool Reject;
        protected override bool IsDodgedOrResisted(Effect effect, StatEnum? stat) => Reject;
        public override void CalculateDamage(Effect effect) { }
    }
    private sealed class ApplicationProbe : EffectTemplate
    {
        public List<Effect> Applications { get; } = new();
        public List<Effect> Calculations { get; } = new();
        public override void Calculate(Effect effect) { Calculations.Add(effect); effect.AddSuccessEffect(this); }
        public override void ApplyEffect(Effect effect) => Applications.Add(effect);
    }
    private sealed class TemporarySkills : IDisposable
    {
        public int First => 999980;
        private readonly Dictionary<int, SkillTemplate> skills;
        private readonly int count;
        public TemporarySkills(EffectTemplate probe, int count)
        {
            this.count = count;
            skills = (Dictionary<int, SkillTemplate>)typeof(SkillData).GetField("skillTemplateById", PrivateInstance)!.GetValue(DataManager.SKILL_DATA)!;
            for (int level = 1; level <= count; level++) Assert.True(skills.TryAdd(First + level - 1, Skill(First + level - 1, probe, level)));
        }
        public void Dispose() { for (int i = 0; i < count; i++) skills.Remove(First + i); }
    }
}
