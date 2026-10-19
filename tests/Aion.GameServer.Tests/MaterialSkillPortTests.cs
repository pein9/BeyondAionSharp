using System.Reflection;
using System.Runtime.CompilerServices;
using System.Xml.Serialization;
using Aion.GameServer.Controllers;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Dataholders.LoadingUtils;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Account;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Model.Templates.Materials;
using Aion.GameServer.Model.Templates.World;
using Aion.GameServer.Model.Templates.Zone;
using Aion.GameServer.Services;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.TestKit;
using Aion.GameServer.Tests.Ai;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.IdFactory;
using Aion.GameServer.Utils.Stats;
using Aion.GameServer.Utils.Time.Gametime;
using Aion.GameServer.World;
using Aion.GameServer.World.Knownlist;
using Aion.GameServer.World.Zone;
using TYPE = Aion.GameServer.Network.Aion.ServerPackets.SmAttackStatus.TYPE;
using LOG = Aion.GameServer.Network.Aion.ServerPackets.SmAttackStatus.LOG;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class MaterialSkillPortTests
{
    [Fact]
    public void UsageSurvivesReentryTracksSlotsSeparatelyAndResetsOnlyForAnotherMaterial()
    {
        long now = 0;
        SystemClock.UseSource(() => now);
        try
        {
            var controller = new NpcController();
            var usage = controller.GetOrCreateMaterialSkillUsage();
            Assert.Same(usage, controller.GetOrCreateMaterialSkillUsage());
            Assert.True(usage.TryUse(61, 0, 5));
            now = 4999;
            Assert.False(usage.TryUse(61, 0, 5));
            Assert.True(usage.TryUse(61, 1, 5));
            Assert.True(new NpcController().GetOrCreateMaterialSkillUsage().TryUse(61, 0, 5));
            now = 5000;
            Assert.True(usage.TryUse(61, 0, 5));
            Assert.True(usage.TryUse(62, 0, 5));
            Assert.True(usage.TryUse(61, 0, 5));
            Assert.True(usage.TryUse(61, 2, int.MaxValue));
            now += 1000;
            Assert.False(usage.TryUse(61, 2, int.MaxValue));
        }
        finally { SystemClock.UseSystemClock(); }
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("RAIN", WeatherState.RAIN)]
    [InlineData("SNOW", WeatherState.SNOW)]
    [InlineData("WIND", WeatherState.WIND)]
    public void MissingWeatherStateRemainsNullRatherThanDefaultingToRain(string? state, WeatherState? expected)
    {
        using var xml = new StringReader("<WeatherEntry name=\"RAIN_CLOUD\" before=\"true\"" + (state == null ? "" : " state=\"" + state + "\"") + " />");
        var entry = (WeatherEntry)new XmlSerializer(typeof(WeatherEntry)).Deserialize(xml)!;
        Assert.Equal(expected, entry.GetState());
        Assert.Null(WeatherEntry.NONE.GetState());
    }

    [Theory]
    [InlineData(3, true)]
    [InlineData(4, true)]
    [InlineData(6, true)]
    [InlineData(7, false)]
    [InlineData(19, false)]
    [InlineData(20, true)]
    [InlineData(21, true)]
    [InlineData(22, true)]
    public void MaterialNightUsesItsOwnHourRangeAndEveryConditionMustHold(int hour, bool night)
    {
        using var weather = new WeatherScope(hour, null);
        Assert.Equal(night, weather.Service.MatchesMaterialActConditions(weather.Creature, new() { MaterialActCondition.NIGHT }));
        Assert.Equal(night, weather.Service.MatchesMaterialActConditions(weather.Creature, new() { MaterialActCondition.SUNNY, MaterialActCondition.NIGHT }));
        weather.Entry.state = WeatherState.SNOW;
        Assert.False(weather.Service.MatchesMaterialActConditions(weather.Creature, new() { MaterialActCondition.NIGHT, MaterialActCondition.SUNNY }));
        Assert.True(weather.Service.MatchesMaterialActConditions(weather.Creature, new()));
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData(WeatherState.RAIN, false)]
    [InlineData(WeatherState.SNOW, false)]
    [InlineData(WeatherState.WIND, true)]
    public void SunnyUsesPhysicalWeatherStateEvenForLeadInEntries(WeatherState? state, bool expected)
    {
        using var weather = new WeatherScope(20, state);
        weather.Entry.weatherName = "RAIN";
        weather.Entry.isBefore = true;
        Assert.Equal(expected, weather.Service.MatchesMaterialActConditions(weather.Creature, new() { MaterialActCondition.SUNNY }));
    }

    [Fact]
    public void ShippedWeatherStatesAndTargetFilteringBindThroughTheRealLoader()
    {
        var data = JaxbHolderLoader.LoadFromFile<MapWeatherData>(Path.Combine(RealStaticData.RepoRoot(), "game-server/data/static_data/weather_table.xml"));
        Assert.Equal(WeatherState.SNOW, data.GetWeather(220030000).GetZoneData().Single(e => e.GetCode() == 1).GetState());
        Assert.Equal(WeatherState.WIND, data.GetWeather(210020000).GetZoneData().Single(e => e.GetCode() == 1).GetState());
        Assert.Null(data.GetWeather(210010000).GetZoneData().Single(e => e.GetCode() == 2).GetState());
        var template = new MaterialTemplate { skills = new() {
            new() { id = 1, target = MaterialTarget.PLAYER },
            new() { id = 2, target = MaterialTarget.ALL },
            new() { id = 3, target = MaterialTarget.PLAYER_WITH_PET } } };
        // An ownerless summon still matches the material's summon target, as Java now specifies.
        Assert.Equal(new[] { 2, 3 }, template.GetSkills(Bare<Summon>()).Select(s => s.GetId()));
        Assert.Equal(new[] { 2 }, template.GetSkills(Bare<Npc>()).Select(s => s.GetId()));
    }

    [Fact]
    public void SelfDamageBypassesPvpPveModifiersAndOwnMagicBoost()
    {
        var bare = Bare<Npc>();
        Assert.Equal(123.5f, StatFunctions.AdjustDamageByPvpOrPveModifiers(bare, bare, 123.5f, 500, false, SkillElement.FIRE));
        using var harness = BossAiHarness.For().Build();
        var caster = harness.SpawnPlayer(); var target = harness.SpawnPlayer();
        var skill = new SkillTemplate { skillId = 1, applyMagicalSkillBoostBonus = true };
        var damage = new DamageProbe();
        Assert.False(damage.ShouldApplyMagicalSkillBoostBonus(new Effect(caster, caster, skill, 1)));
        Assert.True(damage.ShouldApplyMagicalSkillBoostBonus(new Effect(caster, target, skill, 1)));
    }

    [Theory]
    [InlineData("hp-set", true)]
    [InlineData("mp-set", true)]
    [InlineData("hp-cost", true)]
    [InlineData("mp-cost", true)]
    [InlineData("hp-damage", false)]
    [InlineData("mp-damage", false)]
    [InlineData("no-loss", true)]
    public void ProtectionEndsOnlyForActualDamageAndNotAssignmentsOrSkillCosts(string source, bool protectedAfter)
    {
        using var harness = BossAiHarness.For().Build();
        var player = SpawnVulnerable(harness);
        player.GetController().StartProtectionActiveTask();
        Assert.True(player.IsProtectionActive());
        var stats = player.GetLifeStats();
        Assert.True(stats.GetCurrentHp() > 1); Assert.True(stats.GetCurrentMp() > 1);
        switch (source)
        {
            case "hp-set": stats.SetCurrentHp(stats.GetCurrentHp() - 1); break;
            case "mp-set": stats.SetCurrentMp(stats.GetCurrentMp() - 1); break;
            case "hp-cost": stats.ReduceHp(TYPE.USED_HP, 1, 0, LOG.REGULAR, player); break;
            case "mp-cost": stats.ReduceMp(TYPE.USED_MP, 1, 0, LOG.REGULAR); break;
            case "hp-damage": stats.ReduceHp(TYPE.DAMAGE, 1, 0, LOG.REGULAR, player); break;
            case "mp-damage": stats.ReduceMp(TYPE.DAMAGE_MP, 1, 0, LOG.REGULAR); break;
            case "no-loss": stats.ReduceHp(TYPE.DAMAGE, 0, 0, LOG.REGULAR, player); break;
        }
        Assert.Equal(protectedAfter, player.IsProtectionActive());
        Assert.Equal(protectedAfter, player.GetController().HasTask(TaskId.PROTECTION_ACTIVE));
        Assert.Equal(TYPE.HP.GetValue(), TYPE.DAMAGE.GetValue()); // Same wire ID, distinct Java enum identities.
    }

    [Fact]
    public void MaterialSelfAttackDamagesAProtectedPlayerThroughTheRealController()
    {
        using var harness = BossAiHarness.For().Build();
        var player = SpawnVulnerable(harness);
        player.GetController().StartProtectionActiveTask();
        int before = player.GetLifeStats().GetCurrentHp();
        player.GetController().OnAttack(player, null!, TYPE.DAMAGE, 1, false, LOG.REGULAR, null, null);
        Assert.Equal(before - 1, player.GetLifeStats().GetCurrentHp());
        Assert.False(player.IsProtectionActive());
    }

    [Fact]
    public async Task EveryMaterialSlotUsesItsOwnPeriodAcrossActorRecreationAndSkipsFlyingPlayers()
    {
        await EnsureWeatherReady();
        using var harness = BossAiHarness.For().Build();
        var player = harness.SpawnPlayer();
        player.GetController().StartProtectionActiveTask();
        var first = new SkillProbe { Position = 1 }; var second = new SkillProbe { Position = 1 };
        using var skills = new TemporarySkills(first, second);
        var actor = new Actor(player, new() { new() { id = 999981, level = 1, frequency = 2 }, new() { id = 999982, level = 1, frequency = 3 } });
        actor.Act(); harness.Clock.Advance(TimeSpan.Zero);
        Assert.Equal(1, first.Calls); Assert.Equal(1, second.Calls);
        Assert.Equal(Effect.ForceType.MATERIAL_SKILL, first.LastForce);
        actor.Abort();
        var reentered = new Actor(player, actor.Skills);
        reentered.Act(); harness.Clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(1, first.Calls); Assert.Equal(1, second.Calls);
        harness.Clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(2, first.Calls); Assert.Equal(1, second.Calls);
        harness.Clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(2, first.Calls); Assert.Equal(2, second.Calls);
        player.UnsetState(CreatureState.RESTING);
        player.SetState(CreatureState.FLYING);
        player.SetFlyState(FlyState.FLYING);
        Assert.True(player.IsInFlyingState());
        harness.Clock.Advance(TimeSpan.FromSeconds(4));
        Assert.Equal(2, first.Calls); Assert.Equal(2, second.Calls);
        player.UnsetFlyState(FlyState.FLYING);
        player.SetFlyState(FlyState.GLIDING);
        Assert.True(player.IsInGlidingState());
        harness.Clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(3, first.Calls); Assert.Equal(3, second.Calls);
        reentered.Abort();
    }

    private sealed class Actor(Creature creature, List<MaterialSkill> skills)
        : AbstractMaterialSkillActor(creature, null!, 0, CheckType.TOUCH, TaskId.ZONE_MATERIAL_ACTION, 61, skills)
    {
        public List<MaterialSkill> Skills => base.skills;
        public override void OnMoved(Aion.GameServer.GeoEngine.Collision.CollisionResults result) { }
    }
    private sealed class DamageProbe : DamageEffect { }
    private sealed class SkillProbe : EffectTemplate
    {
        public int Calls;
        public Effect.ForceType? LastForce;
        public override void ApplyEffect(Effect effect) { Calls++; LastForce = effect.GetForceType(); }
    }
    private sealed class TemporarySkills : IDisposable
    {
        private readonly Dictionary<int, SkillTemplate> data = (Dictionary<int, SkillTemplate>)typeof(SkillData)
            .GetField("skillTemplateById", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(DataManager.SKILL_DATA)!;
        public TemporarySkills(params SkillProbe[] probes)
        {
            for (int i = 0; i < probes.Length; i++)
            {
                var effects = new Effects { effects = new() { probes[i] } };
                typeof(Effects).GetField("effectTypes", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(effects, new HashSet<EffectType>());
                Assert.True(data.TryAdd(999981 + i, new SkillTemplate { skillId = 999981 + i, lvl = 1, activationAttribute = ActivationAttribute.ACTIVE, effects = effects }));
            }
        }
        public void Dispose() { data.Remove(999981); data.Remove(999982); }
    }

    private static Player SpawnVulnerable(BossAiHarness harness)
    {
        var common = new PlayerCommonData(IDFactory.GetInstance().NextId());
        common.SetName("Material"); common.SetRace(Race.ELYOS); common.SetPlayerClass(PlayerClass.WARRIOR);
        var player = new Player(new PlayerAccountData(common, new PlayerAppearance()), new Account(1));
        player.SetKnownlist(new KnownList(player));
        player.SetEffectController(new Aion.GameServer.Controllers.Effects.PlayerEffectController(player));
        player.SetPosition(harness.World.CreatePosition(BossAiHarness.DefaultMapId, 985, 135, 242, 0, 1));
        harness.World.StoreObject(player); harness.World.Spawn(player);
        return player;
    }

    private static async Task EnsureWeatherReady()
    {
        var real = await RealStaticData.LoadAsync();
        var previous = DataManager.GetRegisteredInstance();
        using var time = new TimeScope(20);
        try { DataManager.RegisterInstance(real); _ = WeatherService.GetInstance(); }
        finally { DataManager.RestoreInstance(previous); }
    }

    private sealed class TimeScope : IDisposable
    {
        private static readonly FieldInfo Instance = typeof(GameTimeService).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic)!;
        private readonly object? previous = Instance.GetValue(null);
        public TimeScope(int hour)
        {
            var service = Bare<GameTimeService>();
            typeof(GameTimeService).GetField("_gameTime", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(service, new GameTime(hour * 60 + 59));
            Instance.SetValue(null, service);
        }
        public void Dispose() => Instance.SetValue(null, previous);
    }

    private sealed class WeatherScope : IDisposable
    {
        private readonly TimeScope time;
        private readonly ZoneCleanupPortTests.DataScope data;
        public WeatherEntry Entry { get; } = new();
        public Creature Creature { get; } = Bare<Npc>();
        public WeatherService Service { get; } = Bare<WeatherService>();
        public WeatherScope(int hour, WeatherState? state)
        {
            var template = new ZoneTemplate { XmlName = "weather", Mapid = 1, ZoneType = ZoneClassName.WEATHER, AreaType = AreaType.Sphere, Sphere = new Sphere(0, 0, 0, 10) };
            data = new(template); time = new(hour); Entry.state = state;
            var zone = new ZoneInstance(1, new ZoneInfo(template));
            Creature.SetPosition(new WorldPosition(1, 0, 0, 0, 0, new MapRegion(1, null!, new[] { zone })));
            typeof(VisibleObject).GetField("_controller", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(Creature, new NpcController());
            zone.OnEnter(Creature);
            typeof(WeatherService).GetField("worldZoneWeathers", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(Service, new Dictionary<int, WeatherEntry[]> { [1] = new[] { Entry } });
        }
        public void Dispose() { time.Dispose(); data.Dispose(); }
    }
    private static T Bare<T>() => (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
}
