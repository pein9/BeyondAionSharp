using System.Reflection;
using System.Text.Json;
using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.SkillEngine.Condition;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Tests.Ai;
using Aion.GameServer.Utils;
using SkillProperties = Aion.GameServer.SkillEngine.Properties.Properties;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class ChainSkillsPortTests
{
    [Fact]
    public void ChainConditionsMatchAllSixtyCasesExecutedFromTheExactJavaCommit()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "AionServer.slnx"))) dir = dir.Parent;
        using var data = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir!.FullName,
            "parity-artifacts/golden/formulas/ChainSkills.b55aaf32c.json")));
        Assert.Equal("b55aaf32c86a41db8cac4806985531479f1b98d5", data.RootElement.GetProperty("sourceJavaCommit").GetString());
        using var h = Harness();
        var player = h.SpawnPlayer();
        var connection = Attach(player);
        long now = 1_000_000;
        SystemClock.UseSource(() => now);
        foreach (var row in data.RootElement.GetProperty("cases").EnumerateArray())
        {
            now = 1_000_000;
            var chain = player.GetChainSkills(); chain.ResetChain(); connection.Sent.Clear();
            string initial = row.GetProperty("initial").GetString()!;
            for (int i = 0; i < row.GetProperty("count").GetInt32(); i++) chain.UpdateChain(initial, row.GetProperty("hit").GetInt32());
            now += row.GetProperty("elapsed").GetInt64();
            var condition = new ChainCondition { category = row.GetProperty("category").GetString(),
                preCategory = row.GetProperty("pre").GetString(), time = row.GetProperty("time").GetInt32(),
                selfCount = row.GetProperty("selfCount").GetInt32(), preCount = row.GetProperty("preCount").GetInt32() };
            var skill = NewSkill(player, condition);
            Assert.Equal(row.GetProperty("accepted").GetBoolean(), condition.Validate(skill));
            Assert.Equal(row.GetProperty("resetOnStart").GetBoolean(), skill.IsChainResetOnCastStart());
            Assert.Equal(row.GetProperty("messages").GetInt32(), connection.Sent.Count);
            Assert.Equal(row.GetProperty("window").GetInt32(), condition.GetActivationWindow());
            Assert.Equal(row.GetProperty("countAfter").GetInt32(), chain.GetCurrentChainCount(initial));
        }
    }

    [Theory]
    [InlineData(0, 3000)]
    [InlineData(1, 1501)]
    [InlineData(500, 2000)]
    [InlineData(5000, 6500)]
    [InlineData(int.MaxValue, int.MinValue + 1499)]
    public void ActivationWindowUsesTheFollowUpsOwnTimeAndJavaOverflow(int time, int expected) =>
        Assert.Equal(expected, new ChainCondition { time = time }.GetActivationWindow());

    [Fact]
    public void MissingXmlTimeHasTheSameThreeSecondWindowAsJava()
    {
        var serializer = new XmlSerializer(typeof(ChainCondition));
        using var input = new StringReader("<ChainCondition category=\"NEXT\" precategory=\"OPEN\" />");
        Assert.Equal(3000, ((ChainCondition)serializer.Deserialize(input)!).GetActivationWindow());
    }

    [Fact]
    public void EachFollowUpChecksItsOwnWindowFromThePredecessorsHitAndExpiresAtEquality()
    {
        using var h = Harness();
        long now = 100_000;
        SystemClock.UseSource(() => now);
        var chain = new ChainSkills();
        chain.UpdateChain("OPEN", 750);
        now += 3749;
        Assert.True(chain.CanContinue("OPEN", 1, 3000));
        now++;
        Assert.False(chain.CanContinue("OPEN", 1, 3000));
        Assert.True(chain.CanContinue("OPEN", 1, 6500));
        now += 3500;
        Assert.False(chain.CanContinue("OPEN", 1, 6500));
        Assert.Equal(1, chain.GetCurrentChainCount("OPEN")); // expiration does not mutate the state
    }

    [Fact]
    public void OnlyTheCurrentConsecutiveCategoryCanContinueAndResetClearsEvenTheEmptyCategory()
    {
        using var h = Harness();
        var chain = new ChainSkills();
        chain.UpdateChain("A", 0); chain.UpdateChain("A", 0);
        Assert.True(chain.CanContinue("A", 2, 3000));
        Assert.False(chain.CanRepeat("A", 2, 3000));
        chain.UpdateChain("B", 0);
        Assert.False(chain.CanContinue("A", 1, 3000));
        Assert.Equal(1, chain.GetCurrentChainCount("B"));
        chain.UpdateChain("", 0);
        Assert.Equal(1, chain.GetCurrentChainCount(""));
        chain.ResetChain();
        Assert.Equal(0, chain.GetCurrentChainCount(""));
        Assert.False(chain.CanContinue("B", 1, 3000));
    }

    [Fact]
    public void RefusedFollowUpSends1300107AndValidationNeverConsumesThePredecessor()
    {
        using var h = Harness();
        var player = h.SpawnPlayer();
        var connection = Attach(player);
        var condition = new ChainCondition { category = "NEXT", preCategory = "OPEN", preCount = 2 };
        var skill = NewSkill(player, condition);
        player.GetChainSkills().UpdateChain("OPEN", 0);
        Assert.False(condition.Validate(skill));
        Assert.Equal(1300107, MessageId(Assert.Single(connection.Sent.OfType<SM_SYSTEM_MESSAGE>())));
        Assert.Equal(1, player.GetChainSkills().GetCurrentChainCount("OPEN"));
        Assert.False(skill.IsChainResetOnCastStart());
        player.GetChainSkills().UpdateChain("OPEN", 0);
        Assert.True(condition.Validate(skill));
        Assert.True(skill.IsChainResetOnCastStart());
        Assert.Equal(2, player.GetChainSkills().GetCurrentChainCount("OPEN"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AValidatedFollowUpConsumesTheChainOnlyAfterAllStartChecksPass(bool canPay)
    {
        using var h = Harness();
        var player = h.SpawnPlayer();
        var condition = new ChainCondition { category = "NEXT", preCategory = "OPEN" };
        var skill = NewSkill(player, condition, 1000);
        skill.GetSkillTemplate().endconditions = new Conditions { conditions = new() { new CostGate(canPay) } };
        player.GetChainSkills().UpdateChain("OPEN", 0);
        Assert.Equal(canPay, skill.UseNoAnimationSkill());
        Assert.Equal(canPay ? 0 : 1, player.GetChainSkills().GetCurrentChainCount("OPEN"));
    }

    [Fact]
    public void StartingAnOrdinaryCastDoesNotEndTheChainBeforeItsSuccessfulResult()
    {
        using var h = Harness();
        var player = h.SpawnPlayer();
        var skill = NewSkill(player, null, 1000);
        player.GetChainSkills().UpdateChain("OPEN", 0);
        Assert.True(skill.UseNoAnimationSkill());
        Assert.Same(skill, player.GetCastingSkill());
        Assert.Equal(1, player.GetChainSkills().GetCurrentChainCount("OPEN"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DisabledTriggerRateStillRequiresTheFirstEffectToLand(bool lands)
    {
        using var h = Harness();
        var player = h.SpawnPlayer();
        var skill = NewSkill(player, new ChainCondition { category = "NEXT" });
        skill.GetSkillTemplate().chainSkillProb = 0;
        skill.GetSkillTemplate().effects = ProbeEffects(new LandingProbe { Position = 1, Lands = lands });
        player.GetChainSkills().UpdateChain("BEFORE", 0);
        bool old = Aion.GameServer.Configs.Main.CustomConfig.SKILL_CHAIN_DISABLE_TRIGGERRATE;
        try
        {
            Aion.GameServer.Configs.Main.CustomConfig.SKILL_CHAIN_DISABLE_TRIGGERRATE = true;
            player.SetCasting(skill); skill.Finish();
        }
        finally { Aion.GameServer.Configs.Main.CustomConfig.SKILL_CHAIN_DISABLE_TRIGGERRATE = old; }
        Assert.Equal(lands ? 1 : 0, player.GetChainSkills().GetCurrentChainCount("NEXT"));
        Assert.Equal(lands ? 0 : 1, player.GetChainSkills().GetCurrentChainCount("BEFORE"));
    }

    [Fact]
    public void RepeatBypassesCooldownWithinItsOwnWindowThenExhaustionRestartsAtCastStart()
    {
        using var h = Harness();
        var player = h.SpawnPlayer();
        long now = SystemClock.CurrentMillis();
        SystemClock.UseSource(() => now);
        var condition = new ChainCondition { category = "REPEAT", selfCount = 2, time = 500 };
        var skill = NewSkill(player, condition);
        skill.GetSkillTemplate().cooldownId = 999091;
        player.SetSkillCoolDown(999091, now + 60_000);
        player.GetChainSkills().UpdateChain("REPEAT", 400);
        Assert.Equal(1, skill.GetMultiCastCount());
        now += 2399;
        Assert.False(player.IsSkillDisabled(skill.GetSkillTemplate()));
        Assert.True(condition.Validate(skill));
        Assert.False(skill.IsChainResetOnCastStart());
        now++;
        Assert.True(player.IsSkillDisabled(skill.GetSkillTemplate()));
        Assert.True(condition.Validate(skill));
        Assert.True(skill.IsChainResetOnCastStart());
        Assert.Equal(1, skill.GetMultiCastCount()); // obtained from the template, not validation side effects
    }

    [Theory]
    [InlineData("CAST", "CHAIN", true, 100, true)]
    [InlineData("CAST", "CHAIN", false, 100, false)]
    [InlineData("CAST", "CHAIN", true, 0, false)]
    [InlineData("CAST", null, true, 100, true)]
    [InlineData("CAST", null, false, 100, false)]
    [InlineData("PROVOKED", "CHAIN", true, 100, false)]
    [InlineData("PASSIVE", "CHAIN", true, 100, false)]
    public void EndCastCountsOnlyEligibleSuccessfulFirstEffectsAndLeavesFailedCastsUnchanged(string method, string? category, bool lands, int probability, bool advances)
    {
        using var h = Harness();
        var player = h.SpawnPlayer();
        var skill = NewSkill(player, category == null ? null : new ChainCondition { category = category });
        skill.Method(Enum.Parse<Skill.SkillMethod>(method));
        var probe = new LandingProbe { Position = 1, Lands = lands };
        skill.GetSkillTemplate().effects = ProbeEffects(probe);
        skill.GetSkillTemplate().chainSkillProb = probability;
        bool saved = Aion.GameServer.Configs.Main.CustomConfig.SKILL_CHAIN_DISABLE_TRIGGERRATE;
        Aion.GameServer.Configs.Main.CustomConfig.SKILL_CHAIN_DISABLE_TRIGGERRATE = false;
        player.GetChainSkills().UpdateChain("BEFORE", 0);
        SetHitTime(skill, 750);
        player.SetCasting(skill);
        try { skill.Finish(); }
        finally { Aion.GameServer.Configs.Main.CustomConfig.SKILL_CHAIN_DISABLE_TRIGGERRATE = saved; }
        Assert.Equal(1, probe.Calculations);
        Assert.Equal(advances ? 0 : 1, player.GetChainSkills().GetCurrentChainCount("BEFORE"));
        if (advances)
        {
            Assert.Equal(1, player.GetChainSkills().GetCurrentChainCount(category ?? ""));
            long now = SystemClock.CurrentMillis();
            SystemClock.UseSource(() => now);
            now += 3749;
            Assert.True(player.GetChainSkills().CanContinue(category ?? "", 1, 3000));
            now++;
            Assert.False(player.GetChainSkills().CanContinue(category ?? "", 1, 3000));
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void APointPlacementWithoutTargetsCountsButAnEmptyTargetedCastDoesNot(bool point, bool advances)
    {
        using var h = Harness();
        var player = h.SpawnPlayer();
        var skill = NewSkill(player, new ChainCondition { category = "POINT" });
        skill.GetSkillTemplate().properties = new SkillProperties { FirstTargetRaw = point ? "POINT" : "TARGET", TargetTypeRaw = "POINT" };
        // Control the post-validation target set at the unchanged use-condition boundary.
        skill.GetSkillTemplate().useconditions = new Conditions { conditions = new() { new DropTargets() } };
        skill.GetEffectedList().Clear();
        player.GetChainSkills().UpdateChain("BEFORE", 0);
        player.SetCasting(skill);
        skill.Finish();
        Assert.Equal(advances ? 0 : 1, player.GetChainSkills().GetCurrentChainCount("BEFORE"));
        Assert.Equal(advances ? 1 : 0, player.GetChainSkills().GetCurrentChainCount("POINT"));
    }

    private static CastProbe NewSkill(Player player, ChainCondition? condition, int duration = 0)
    {
        var skill = new CastProbe(new SkillTemplate { skillId = 999091, lvl = 1, duration = duration,
            activationAttribute = ActivationAttribute.ACTIVE, chainSkillProb = 100,
            startconditions = condition == null ? null : new Conditions { conditions = new() { condition } } }, player);
        skill.GetEffectedList().Add(player);
        return skill;
    }
    private static BossAiHarness Harness()
    {
        var h = BossAiHarness.For().Build();
        typeof(StaticData).GetProperty(nameof(StaticData.Motions))!.SetValue(DataManager.GetRegisteredInstance()!.StaticData, new MotionData());
        return h;
    }
    private static Effects ProbeEffects(params EffectTemplate[] probes)
    {
        var effects = new Effects { effects = probes.ToList() };
        typeof(Effects).GetField("effectTypes", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(effects, new HashSet<EffectType>());
        return effects;
    }
    private static void SetHitTime(Skill skill, int time) => typeof(Skill).GetField("hitTime", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(skill, time);
    private sealed class CastProbe(SkillTemplate template, Player player) : Skill(template, player, 1, player, null!)
    {
        public void Method(SkillMethod method) => skillMethod = method;
        public void Finish() => EndCast();
    }
    private sealed class CostGate(bool allowed) : Condition
    {
        public override bool Validate(Skill skill) => allowed;
        public override bool CanValidate(Skill skill) => allowed;
    }
    private sealed class DropTargets : Condition
    {
        public override bool Validate(Skill skill) { skill.GetEffectedList().Clear(); return true; }
    }
    private sealed class LandingProbe : EffectTemplate
    {
        public bool Lands;
        public int Calculations;
        public override void Calculate(Effect effect) { Calculations++; if (Lands) effect.AddSuccessEffect(this); }
        public override void ApplyEffect(Effect effect) { }
    }
    private static int MessageId(SM_SYSTEM_MESSAGE message) => (int)typeof(SM_SYSTEM_MESSAGE).GetField("msgId", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(message)!;
    private static Connection Attach(Player player)
    {
        var c = new Connection(); c.SetAccount(player.GetAccount()); player.SetClientConnection(c); Assert.True(c.SetActivePlayer(player)); return c;
    }
    private sealed class Connection() : AionConnection("127.0.0.1")
    {
        public List<AionServerPacket> Sent = new();
        protected override void EnqueuePacket(AionServerPacket packet, bool closing) => Sent.Add(packet);
    }
}
