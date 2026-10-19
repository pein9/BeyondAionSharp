using System.Buffers.Binary;
using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Xml.Serialization;
using Aion.Commons.Database;
using Aion.Commons.Nio;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Account;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Team;
using Aion.GameServer.Model.Team.Group;
using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.Model.Stats.Calc.Functions;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.Tests.Ai;
using Aion.GameServer.Utils;
using Aion.GameServer.World.Knownlist;
using PlayerGroupMember = Aion.GameServer.Model.Team.Group.PlayerGroupMember;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class PvpExpPortTests
{
    private const BindingFlags Fields = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;
    internal static string Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "AionServer.slnx"))) dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException();
    }
    private static JsonDocument Oracle() => JsonDocument.Parse(File.ReadAllText(Path.Combine(Root(), "parity-artifacts/golden/formulas/PvpExp.d725054e1.json")));

    [Fact]
    public void AllLevelRowsAndFloatMatricesMatchTheExactJavaHolders()
    {
        using var scope = new Scope(); using var oracle = Oracle(); var root = oracle.RootElement;
        Assert.Equal("d725054e1431f29ee0d91f0ae85acfe4b9dc3085", root.GetProperty("sourceJavaCommit").GetString());
        foreach (var row in root.GetProperty("table").EnumerateArray())
        {
            int level = row.GetProperty("level").GetInt32(); var table = DataManager.PVP_EXP_TABLE;
            long[] actual = [table.GetExp(level), table.GetMaxFromAllUser(level), table.GetReduceAmount(level), table.GetReduceIntervalMillis(level), table.GetDelayTimeMillis(level), DataManager.MENTEE_EXP_LIMIT_TABLE.GetExpLimit(level)];
            Assert.Equal(row.GetProperty("values").EnumerateArray().Select(v => v.GetInt64()), actual);
        }
        var mods = Convert.FromBase64String(root.GetProperty("pvpMultipliers").GetString()!);
        var weights = Convert.FromBase64String(root.GetProperty("partyWeights").GetString()!);
        int n = root.GetProperty("matrixLevels").GetInt32();
        for (int a = 1; a <= n; a++) for (int b = 1; b <= n; b++)
        {
            int index = ((a - 1) * n + b - 1) * 4;
            Assert.Equal(BinaryPrimitives.ReadUInt32LittleEndian(mods.AsSpan(index)), BitConverter.SingleToUInt32Bits(DataManager.PVP_EXP_MOD_TABLE.GetMultiplier(a, b)));
            Assert.Equal(BinaryPrimitives.ReadUInt32LittleEndian(weights.AsSpan(index)), BitConverter.SingleToUInt32Bits(DataManager.PARTY_EXP_MOD_TABLE.GetWeight(a, b)));
        }
    }

    [Fact]
    public void FirstAttackerSharesAndWeightedMentorRewardsMatchJavaIncludingNarrowing()
    {
        using var scope = new Scope(); using var oracle = Oracle();
        foreach (var c in oracle.RootElement.GetProperty("shares").EnumerateArray())
            Assert.Equal(c.GetProperty("expectedBits").GetUInt32(), BitConverter.SingleToUInt32Bits(PvpService.CalculateXpShare(c.GetProperty("victimXp").GetInt32(), c.GetProperty("damage").GetInt32(), c.GetProperty("total").GetInt32(), c.GetProperty("first").GetBoolean())));
        var service = (PvpService)RuntimeHelpers.GetUninitializedObject(typeof(PvpService));
        foreach (var c in oracle.RootElement.GetProperty("rewards").EnumerateArray())
        {
            var members = c.GetProperty("members").EnumerateArray().Select((m, i) => BarePlayer(i + 1, m.GetProperty("level").GetInt32(), m.GetProperty("mentor").GetBoolean())).ToList();
            var result = (Dictionary<Player, int>)typeof(PvpService).GetMethod("CalculateXpRewards", Fields)!.Invoke(service, [members, c.GetProperty("team").GetBoolean(), BarePlayer(999, c.GetProperty("victim").GetInt32()), float.Parse(c.GetProperty("share").GetString()!, System.Globalization.CultureInfo.InvariantCulture)])!;
            Assert.Equal(c.GetProperty("expected").EnumerateArray().Select(v => v.GetInt32()), members.Select(m => result[m]));
        }
    }

    public static IEnumerable<object[]> TraceNames()
    {
        using var oracle = Oracle();
        return oracle.RootElement.GetProperty("traces").EnumerateArray().Select(t => new object[] {t.GetProperty("name").GetString()!}).ToArray();
    }

    [Theory]
    [MemberData(nameof(TraceNames))]
    public void LimitsAndMessagesMatchEveryJavaTrace(string name)
    {
        using var scope = new Scope(); using var oracle = Oracle();
        var trace = oracle.RootElement.GetProperty("traces").EnumerateArray().Single(t => t.GetProperty("name").GetString() == name);
        var service = NewLimitService(); var players = new Dictionary<int, Player>();
        Player Get(int id)
        {
            if (!players.TryGetValue(id, out var p)) players[id] = p = scope.Player(30);
            return p;
        }
        foreach (var a in trace.GetProperty("actions").EnumerateArray())
        {
            scope.Now = a.GetProperty("time").GetInt64(); var player = Get(a.GetProperty("id").GetInt32());
            var connection = (Connection)player.GetClientConnection(); connection.Clear(); bool allowed = false;
            var args = a.GetProperty("args").EnumerateArray().Select(v => v.GetInt64()).ToArray();
            switch (a.GetProperty("op").GetString())
            {
                case "set": player.GetCommonData().SetPvpExp(args[0]); break;
                case "enter":
                    if (scope.H.World.GetPlayer(player.GetObjectId()) == null) scope.H.World.StoreObject(player);
                    service.OnEnterWorld(player); break;
                case "leave": service.OnLeaveWorld(player); scope.H.World.RemoveObject(player); break;
                case "gain": allowed = service.TryGainPvpExp(player, Get((int)args[0]), args[1]); break;
                case "cleanup": scope.H.Clock.Advance(TimeSpan.FromMinutes(3)); break;
                default: throw new InvalidOperationException();
            }
            var map = (IDictionary)typeof(PvpExpLimitService).GetField("limitsByPlayer", Fields)!.GetValue(service)!;
            var limit = map[player.GetObjectId()];
            long last = limit == null ? long.MinValue : (long)limit.GetType().GetField("LastReduceTime", Fields)!.GetValue(limit)!;
            int victims = limit == null ? 0 : ((IDictionary)limit.GetType().GetField("CooldownEndByVictim", Fields)!.GetValue(limit)!).Count;
            Assert.Equal(a.GetProperty("allowed").GetBoolean(), allowed);
            Assert.Equal(a.GetProperty("counter").GetInt64(), player.GetCommonData().GetPvpExp());
            Assert.Equal(a.GetProperty("lastReduce").GetInt64(), last);
            Assert.Equal(a.GetProperty("victims").GetInt32(), victims);
            Assert.Equal(a.GetProperty("entries").GetInt32(), map.Count);
            Assert.Equal(a.GetProperty("messages").EnumerateArray().Select(v => v.GetString()), connection.Packets.OfType<SM_SYSTEM_MESSAGE>().Select(p =>
                Payload(p).SequenceEqual(Payload(SM_SYSTEM_MESSAGE.STR_CANNOT_GET_PVP_EXP_TIMEBASE_LIMIT())) ? "TB" :
                Payload(p).SequenceEqual(Payload(SM_SYSTEM_MESSAGE.STR_CANNOT_GET_PVP_EXP_TARGET_LIMIT())) ? "TG" : "unexpected"));
        }
    }

    [Fact]
    public void PositiveFirstHitsSurviveTeamAggregationAndRetailTauntStillSaturates()
    {
        using var scope = new Scope();
        var owner = scope.Player(30); var first = scope.Player(30); var second = scope.Player(30); var solo = scope.Player(30);
        var group = new PlayerGroup(new PlayerGroupMember(first), TeamType.GROUP, 100);
        first.SetPlayerGroup(group); second.SetPlayerGroup(group);
        owner.SetKnownlist(new DamageKnownList(owner, first, second, solo));
        var a = new AggroInfo(first); a.AddDamage(0, 1); a.AddDamage(-1, 2); a.AddDamage(30, 100); a.AddDamage(1, 99);
        Assert.Equal(31, a.GetDamage()); Assert.Equal(99, a.GetFirstDamageTime());
        a.AddHate(100); a.AddHate(int.MaxValue); Assert.Equal(int.MaxValue, a.GetHate());
        var b = new AggroInfo(second); b.AddDamage(60, 10);
        var c = new AggroInfo(solo); c.AddDamage(100, 20);
        var list = new DamageList([a, c, b], owner).ToTeamDamages();
        var firstDamage = list.GetFirstDamage(); Assert.NotNull(firstDamage);
        Assert.Same(group, firstDamage.GetAttacker()); Assert.Equal(10, firstDamage.GetFirstDamageTime());
        Assert.Same(second, list.GetMostDamageByTeam(group).GetAttacker()); Assert.Equal(10, list.GetMostDamageByTeam(group).GetFirstDamageTime());
        Assert.Same(solo, list.GetMostDamage().GetAttacker());
        Assert.Null(new DamageList([], owner).ToTeamDamages().GetFirstDamage());
    }

    [Fact]
    public void SummonDamageKeepsTheOriginalFirstTimeDuringBothMasterTransferBranches()
    {
        using var scope = new Scope(); var owner = scope.Player(30); var master = scope.Player(30);
        owner.SetKnownlist(new DamageKnownList(owner, master));
        // Only GetMaster/ObjectId are used here; use the real Summon ownership method without unrelated summon stats/AI.
        var summon = (Summon)RuntimeHelpers.GetUninitializedObject(typeof(Summon));
        typeof(Summon).GetField("master", Fields)!.SetValue(summon, master);
        var hit = new AggroInfo(summon); hit.AddDamage(25, 10);
        var direct = new AggroInfo(master); direct.AddDamage(50, 30);
        var damage = Assert.Single(new DamageList([direct, hit], owner).GetCreatureDamages());
        Assert.Same(master, damage.GetAttacker()); Assert.Equal(75, damage.GetDamage()); Assert.Equal(10, damage.GetFirstDamageTime());
        var aggro = new PlayerAggroList(owner);
        var entries = (IDictionary)typeof(AggroList).GetField("_aggroList", Fields)!.GetValue(aggro)!;
        entries[summon.ObjectId] = hit; aggro.Remove(summon, true);
        damage = Assert.Single(aggro.GetFinalDamageList().GetCreatureDamages());
        Assert.Equal(25, damage.GetDamage()); Assert.Equal(10, damage.GetFirstDamageTime());
        hit = new AggroInfo(summon); hit.AddDamage(7, 5);
        entries[summon.ObjectId] = hit; aggro.Remove(summon, true);
        damage = Assert.Single(aggro.GetFinalDamageList().GetCreatureDamages());
        Assert.Equal(32, damage.GetDamage()); Assert.Equal(5, damage.GetFirstDamageTime());
    }

    [Fact]
    public void RewardPathBoostsBeforeLimitAppliesRatesAfterLimitAndDoesNotGateXpOnDailyKills()
    {
        using var scope = new Scope(); scope.Now = 100_000;
        bool previousSiege = SiegeConfig.SIEGE_ENABLED; int previousKills = CustomConfig.MAX_DAILY_PVP_KILLS;
        float[] previousRates = RatesConfig.XP_PVP_RATES;
        var killer = scope.Player(30); var victim = scope.Player(30);
        var limit = PvpExpLimitService.GetInstance();
        var limits = (IDictionary)typeof(PvpExpLimitService).GetField("limitsByPlayer", Fields)!.GetValue(limit)!;
        var kills = (IDictionary)typeof(KillCounter).GetField("PVP_KILL_LISTS", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
        try
        {
            SiegeConfig.SIEGE_ENABLED = false; CustomConfig.MAX_DAILY_PVP_KILLS = 1; RatesConfig.XP_PVP_RATES = [2f];
            killer.GetCommonData().SetPlayerClass(PlayerClass.GLADIATOR); // Starting classes cannot earn DP.
            killer.GetCommonData().SetOnline(true);
            killer.SetAbyssRank(new AbyssRank(0,0,0,1,0,0,0,1,0,0,0,0,0,0,0));
            victim.SetAbyssRank(new AbyssRank(0,0,0,1,0,0,0,1,0,0,0,0,0,0,0));
            killer.GetGameStats().AddEffectOnly(null!, new IStatFunction[] {new StatSetFunction(StatEnum.BOOST_HUNTING_XP_RATE, 150)});
            long before = killer.GetCommonData().GetExp();
            var service = (PvpService)RuntimeHelpers.GetUninitializedObject(typeof(PvpService));
            var reward = typeof(PvpService).GetMethod("RewardPlayerTeam", Fields)!;
            object[] args = {new List<Player> {killer}, false, victim, 100, 100, 1f, 100f};
            Assert.True((bool)reward.Invoke(service, args)!);
            Assert.Equal(150, killer.GetCommonData().GetPvpExp()); // boost included, server XP rate excluded
            Assert.Equal(before + 300, killer.GetCommonData().GetExp());
            Assert.Equal(1, killer.GetAbyssRank().GetAp()); Assert.Equal(1, killer.GetCommonData().GetDp());
            Assert.True((bool)reward.Invoke(service, args)!);
            Assert.Equal(300, killer.GetCommonData().GetPvpExp()); // blocked attempt still increases accumulator
            Assert.Equal(before + 300, killer.GetCommonData().GetExp()); // no fallback minimum XP
            Assert.Equal(2, killer.GetAbyssRank().GetAp()); Assert.Equal(2, killer.GetCommonData().GetDp());
            Assert.Contains(((Connection)killer.GetClientConnection()).Packets.OfType<SM_SYSTEM_MESSAGE>(), p => Payload(p).SequenceEqual(Payload(SM_SYSTEM_MESSAGE.STR_CANNOT_GET_PVP_EXP_TARGET_LIMIT())));
        }
        finally
        {
            limits.Remove(killer.GetObjectId()); kills.Remove(killer.GetObjectId()); killer.GetCommonData().SetOnline(false);
            SiegeConfig.SIEGE_ENABLED = previousSiege; CustomConfig.MAX_DAILY_PVP_KILLS = previousKills; RatesConfig.XP_PVP_RATES = previousRates;
        }
    }

    internal static Player BarePlayer(int id, int level, bool mentor = false)
    {
        var pcd = new PlayerCommonData(id); typeof(PlayerCommonData).GetField("level", Fields)!.SetValue(pcd, level);
        var player = new Player(new PlayerAccountData(pcd, new PlayerAppearance()), new Account(id)); player.SetMentor(mentor); return player;
    }
    private static PvpExpLimitService NewLimitService() => (PvpExpLimitService)Activator.CreateInstance(typeof(PvpExpLimitService), true)!;
    private static byte[] Payload(AionServerPacket packet)
    {
        var buf = ByteBuffer.Allocate(8192).Order(ByteOrder.LITTLE_ENDIAN); packet.SetBuf(buf);
        typeof(AionServerPacket).GetMethod("WriteImpl", Fields)!.Invoke(packet, new object?[] {null});
        var bytes = new byte[buf.Position()]; buf.Flip(); buf.Get(bytes); return bytes;
    }
    private sealed class DamageKnownList : KnownList
    {
        public DamageKnownList(Creature owner, params Player[] players) : base(owner)
        { foreach (var p in players) KnownObjects[p.ObjectId] = new KnownObject(p); }
    }
    internal sealed class Connection() : AionConnection("127.0.0.1")
    {
        public AionServerPacket[] Packets => GetSendMsgQueue().ToArray();
        public void Clear() => GetSendMsgQueue().Clear();
    }
    internal sealed class Scope : IDisposable
    {
        internal BossAiHarness H {get;} = BossAiHarness.For().Build();
        internal long Now;
        private readonly FieldInfo source = typeof(DatabaseFactory).GetField("_dataSource", BindingFlags.Static | BindingFlags.NonPublic)!;
        private readonly object? previousSource;
        private readonly List<Player> players = new();
        public Scope()
        {
            previousSource = source.GetValue(null); source.SetValue(null, null);
            SystemClock.UseSource(() => Now);
            Load<PvpExpTable>("PvpExpTableDh", "pvp_exp_table");
            Load<PvpExpModTable>("PvpExpModTableDh", "pvp_exp_mod_table");
            Load<PartyExpModTable>("PartyExpModTableDh", "party_exp_mod_table");
            Load<MenteeExpLimitTable>("MenteeExpLimitTableDh", "mentee_exp_limit_table");
        }
        private void Load<T>(string property, string name)
        {
            using var file = File.OpenRead(Path.Combine(Root(), "game-server/data/static_data/exp_mod_table", name + ".xml"));
            var holder = (T)new XmlSerializer(typeof(T)).Deserialize(file)!;
            typeof(T).GetMethod("AfterUnmarshal")!.Invoke(holder, new object?[] {null});
            typeof(StaticData).GetField("<" + property + ">k__BackingField", Fields)!.SetValue(H.Data.StaticData, holder);
        }
        internal Player Player(int level)
        {
            var p = H.SpawnPlayer(); players.Add(p); typeof(PlayerCommonData).GetField("level", Fields)!.SetValue(p.GetCommonData(), level);
            typeof(PlayerCommonData).GetField("exp", Fields)!.SetValue(p.GetCommonData(), DataManager.PLAYER_EXPERIENCE_TABLE.GetStartExpForLevel(level));
            p.GetCommonData().SetDaeva(true);
            var con = new Connection(); con.SetActivePlayer(p); con.SetState(AionConnection.State.IN_GAME); p.SetClientConnection(con);
            return p;
        }
        public void Dispose()
        {
            foreach (var p in players) p.SetClientConnection(null!);
            source.SetValue(null, previousSource); H.Dispose();
        }
    }
}
