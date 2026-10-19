using System.Collections;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Aion.Commons.Configuration;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Account;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Players;
using Aion.GameServer.Tests.Ai;
using Aion.GameServer.Utils;
using Msg = Aion.GameServer.Network.Aion.ServerPackets.SM_ENTER_WORLD_CHECK.Msg;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class MultiClientPortTests
{
    [Fact]
    public void ServiceAndBanPresentationMatchTheFortyEightScenariosExecutedFromJava()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "AionServer.slnx"))) dir = dir.Parent;
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir!.FullName,
            "parity-artifacts/golden/formulas/MultiClient.a485569df.json")));
        Assert.Equal("a485569dfa0bd56eaf5c9e6b8ffa46bc05d1562a", document.RootElement.GetProperty("sourceJavaCommit").GetString());
        foreach (var row in document.RootElement.GetProperty("cases").EnumerateArray())
        {
            using var f = new Fixture();
            SecurityConfig.MULTI_CLIENT_MAX_CLIENTS_PER_COMPUTER = row.GetProperty("limit").GetInt32();
            SecurityConfig.MULTI_CLIENT_ALLOW_BOTH_FACTIONS = row.GetProperty("both").GetBoolean();
            SecurityConfig.MULTI_CLIENT_FACTION_SWITCH_COOLDOWN_MINUTES = row.GetProperty("minutes").GetInt32();
            SecurityConfig.MULTI_CLIENT_IGNORED_MAC_ADDRESSES = row.GetProperty("ignored").EnumerateArray().Select(v => v.GetString()!).ToHashSet();
            var players = new Dictionary<int, Player>();
            foreach (var step in row.GetProperty("steps").EnumerateArray())
            {
                string actual = "-";
                string op = step.GetProperty("op").GetString()!;
                if (op == "T") f.Now += step.GetProperty("elapsed").GetInt64();
                else if (op == "L")
                {
                    var player = players[step.GetProperty("id").GetInt32()];
                    MultiClientService.OnLeaveWorld(player); player.SetClientConnection(null!);
                }
                else
                {
                    int id = step.GetProperty("id").GetInt32();
                    var race = Enum.Parse<Race>(step.GetProperty("race").GetString()!);
                    if (!players.TryGetValue(id, out var player)) players[id] = player = f.Player(race);
                    var con = f.Connection(player, step.GetProperty("ip").GetString()!, step.GetProperty("mac").GetString()!, step.GetProperty("hdd").GetString()!);
                    if (op == "E") actual = MultiClientService.TryEnterWorld(player, con)?.ToString() ?? "allowed";
                    else if (op == "O") player.SetClientConnection(con);
                    else if (op == "X") player.SetClientConnection(null!);
                    else if (op == "Q") actual = MultiClientService.CheckForFactionSwitchCooldownTime(race, con) == null ? "clear" : "blocked";
                    else if (op == "B")
                    {
                        player.GetAccount().SetAccessLevel((sbyte)step.GetProperty("access").GetInt32());
                        int duration = step.GetProperty("ban").GetInt32();
                        var real = duration == 0 ? null : new CharacterBanInfo(f.Now / 1000, duration, "existing punishment");
                        var ban = Ban(player, con, real);
                        actual = ban == null ? "none" : Convert.ToBase64String(Encoding.UTF8.GetBytes(ban.GetReason())) + ":" + (ban.GetEnd() - ban.GetStart());
                    }
                }
                Assert.True(actual == step.GetProperty("expected").GetString(), $"Java scenario {row} step {step}: got {actual}");
            }
        }
    }

    [Theory]
    [InlineData(0, 3, null)]
    [InlineData(-1, 3, null)]
    [InlineData(1, 0, null)]
    [InlineData(1, 1, Msg.CONNECTION_ERROR)]
    [InlineData(2, 1, null)]
    [InlineData(2, 2, Msg.CONNECTION_ERROR)]
    [InlineData(3, 2, null)]
    [InlineData(2, 3, Msg.CONNECTION_ERROR)]
    public void LimitCountsOnlineCharactersAndRejectsAtEquality(int limit, int online, Msg? expected)
    {
        using var f = new Fixture();
        SecurityConfig.MULTI_CLIENT_MAX_CLIENTS_PER_COMPUTER = limit;
        for (int i = 0; i < online; i++) f.Online("IP", "MAC");
        var player = f.Player();
        Assert.Equal(expected, MultiClientService.TryEnterWorld(player, f.Connection(player, "IP", "MAC")));
    }

    [Theory]
    [InlineData("IP", "MAC", "different", Msg.CONNECTION_ERROR)]
    [InlineData("OTHER", "MAC", "HDD", null)]
    [InlineData("IP", "OTHER", "HDD", null)]
    [InlineData("IP", "mac", "HDD", null)]
    [InlineData("IP", "", "different", null)]
    public void ComputerLimitRequiresBothIpAndExactMacAndNeverUsesHdd(string ip, string mac, string hdd, Msg? expected)
    {
        using var f = new Fixture();
        SecurityConfig.MULTI_CLIENT_MAX_CLIENTS_PER_COMPUTER = 1;
        f.Online("IP", "MAC", hdd: "HDD");
        var p = f.Player();
        Assert.Equal(expected, MultiClientService.TryEnterWorld(p, f.Connection(p, ip, mac, hdd)));
    }

    [Fact]
    public void EmptyMacAddressesCompareAsEqualAndSocketlessWorldPlayersAreSkipped()
    {
        using var f = new Fixture();
        SecurityConfig.MULTI_CLIENT_MAX_CLIENTS_PER_COMPUTER = 1;
        f.Player(); // a world player without a connection must not abort enumeration
        f.Online("IP", "");
        var p = f.Player();
        Assert.Equal(Msg.CONNECTION_ERROR, MultiClientService.TryEnterWorld(p, f.Connection(p, "IP", "")));
    }

    [Fact]
    public void ExistingBotIdentitiesAllowTheEightClassGroupBehindOneNatWithTheDefaultLimit()
    {
        using var f = new Fixture();
        for (int i = 1; i <= 8; i++)
        {
            var player = f.Player();
            string mac = Aion.Bots.Transport.BotIdentity.MacAddress(Aion.Bots.Transport.BotIdentity.MacBytes(i));
            var con = f.Connection(player, "127.0.0.1", mac);
            Assert.Null(MultiClientService.TryEnterWorld(player, con));
            player.SetClientConnection(con);
        }
    }

    [Fact]
    public void ComputerLimitWinsBeforeTheIndependentFactionRuleAndIgnoredMacBypassesBoth()
    {
        using var f = new Fixture();
        SecurityConfig.MULTI_CLIENT_MAX_CLIENTS_PER_COMPUTER = 1;
        SecurityConfig.MULTI_CLIENT_ALLOW_BOTH_FACTIONS = false;
        var source = f.Player(); var sourceCon = f.Connection(source, "IP", "MAC");
        Assert.Null(MultiClientService.TryEnterWorld(source, sourceCon));
        source.SetClientConnection(sourceCon);
        var other = f.Player(Race.ASMODIANS); var otherCon = f.Connection(other, "IP", "MAC");
        Assert.Equal(Msg.CONNECTION_ERROR, MultiClientService.TryEnterWorld(other, otherCon));
        SecurityConfig.MULTI_CLIENT_MAX_CLIENTS_PER_COMPUTER = 0;
        Assert.Equal(Msg.BOTH_FACTIONS, MultiClientService.TryEnterWorld(other, otherCon));
        SecurityConfig.MULTI_CLIENT_IGNORED_MAC_ADDRESSES.Add("MAC");
        Assert.Null(MultiClientService.TryEnterWorld(other, otherCon));
    }

    [Theory]
    [InlineData("IP", "OTHER", Msg.BOTH_FACTIONS)]
    [InlineData("OTHER", "MAC", Msg.BOTH_FACTIONS)]
    [InlineData("OTHER", "OTHER", null)]
    public void FactionRuleMatchesEitherIpOrMacInsteadOfTheComputerPair(string ip, string mac, Msg? expected)
    {
        using var f = new Fixture();
        SecurityConfig.MULTI_CLIENT_MAX_CLIENTS_PER_COMPUTER = 0;
        SecurityConfig.MULTI_CLIENT_ALLOW_BOTH_FACTIONS = false;
        var source = f.Player();
        Assert.Null(MultiClientService.TryEnterWorld(source, f.Connection(source, "IP", "MAC")));
        var other = f.Player(Race.ASMODIANS);
        Assert.Equal(expected, MultiClientService.TryEnterWorld(other, f.Connection(other, ip, mac)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(20)]
    public void FactionCooldownCountsOnlineSessionsAndEndsExactlyAtTheLogoutDeadline(int minutes)
    {
        using var f = new Fixture();
        SecurityConfig.MULTI_CLIENT_MAX_CLIENTS_PER_COMPUTER = 0;
        SecurityConfig.MULTI_CLIENT_ALLOW_BOTH_FACTIONS = false;
        SecurityConfig.MULTI_CLIENT_FACTION_SWITCH_COOLDOWN_MINUTES = minutes;
        var source = f.Player(); var con = f.Connection(source, "IP", "MAC");
        Assert.Null(MultiClientService.TryEnterWorld(source, con));
        var other = f.Player(Race.ASMODIANS); var otherCon = f.Connection(other, "IP", "MAC");
        f.Now += 10_000;
        Assert.Equal(Msg.BOTH_FACTIONS, MultiClientService.TryEnterWorld(other, otherCon));
        MultiClientService.OnLeaveWorld(source);
        if (minutes > 0)
        {
            f.Now += minutes * 60_000L - 1;
            Assert.Equal(Msg.BOTH_FACTIONS, MultiClientService.TryEnterWorld(other, otherCon));
            f.Now++;
        }
        Assert.Null(MultiClientService.TryEnterWorld(other, otherCon));
    }

    [Theory]
    [InlineData(Race.ELYOS, "Asmodian")]
    [InlineData(Race.ASMODIANS, "Elyos")]
    public void ZeroCooldownStillShowsTheHouseholdLogoutMessageForAnOnlineOpponent(Race selectedRace, string opposite)
    {
        using var f = new Fixture();
        SecurityConfig.MULTI_CLIENT_MAX_CLIENTS_PER_COMPUTER = 0;
        SecurityConfig.MULTI_CLIENT_ALLOW_BOTH_FACTIONS = false;
        SecurityConfig.MULTI_CLIENT_FACTION_SWITCH_COOLDOWN_MINUTES = 0;
        var source = f.Player(selectedRace == Race.ELYOS ? Race.ASMODIANS : Race.ELYOS);
        Assert.Null(MultiClientService.TryEnterWorld(source, f.Connection(source, "IP", "MAC")));
        var selected = f.Player(selectedRace);
        var ban = Ban(selected, f.Connection(selected, "IP", "MAC"));
        Assert.NotNull(ban);
        Assert.Equal(f.Now / 1000, ban.GetStart());
        Assert.Equal(61, ban.GetEnd() - ban.GetStart());
        Assert.Equal("\n\n\nPlease log out all " + opposite + " characters in your household\n\n\n\n\n\n\n", ban.GetReason());
    }

    [Fact]
    public void PositiveCooldownTextPreservesThePrivateUseGlyphAndStaffIsExempt()
    {
        using var f = new Fixture();
        SecurityConfig.MULTI_CLIENT_MAX_CLIENTS_PER_COMPUTER = 0;
        SecurityConfig.MULTI_CLIENT_ALLOW_BOTH_FACTIONS = false;
        var source = f.Player();
        Assert.Null(MultiClientService.TryEnterWorld(source, f.Connection(source, "IP", "MAC")));
        var selected = f.Player(Race.ASMODIANS); var con = f.Connection(selected, "IP", "MAC");
        Assert.Equal("\n\n\n\uE026 20 minute cooldown between switching factions\n\n\n\n\n\n\n", Ban(selected, con)!.GetReason());
        selected.GetAccount().SetAccessLevel(1);
        Assert.Null(Ban(selected, con));
    }

    [Fact]
    public void RealCharacterBanWinsUntilItsExactExpiryAndIgnoredMacHasNoSyntheticBan()
    {
        using var f = new Fixture();
        SecurityConfig.MULTI_CLIENT_MAX_CLIENTS_PER_COMPUTER = 0;
        SecurityConfig.MULTI_CLIENT_ALLOW_BOTH_FACTIONS = false;
        var source = f.Player(); Assert.Null(MultiClientService.TryEnterWorld(source, f.Connection(source, "IP", "MAC")));
        var selected = f.Player(Race.ASMODIANS); var con = f.Connection(selected, "IP", "MAC");
        var real = new CharacterBanInfo(f.Now / 1000, 1, "existing punishment");
        Assert.Same(real, Ban(selected, con, real));
        f.Now += 1000;
        Assert.NotSame(real, Ban(selected, con, real));
        SecurityConfig.MULTI_CLIENT_IGNORED_MAC_ADDRESSES.Add("MAC");
        Assert.Null(Ban(selected, con));
    }

    [Fact]
    public void NewConfigurationDefaultsAndOverridesBindWithoutHonoringRemovedModeKeys()
    {
        using var f = new Fixture();
        var props = new JavaProperties();
        props.SetProperty("gameserver.security.multi_client.max_clients_per_computer", "4");
        props.SetProperty("gameserver.security.multi_client.allow_both_factions", "false");
        props.SetProperty("gameserver.security.multi_client.ignored_mac_addresses", "AA, BB, AA");
        props.SetProperty("gameserver.security.multi_client.faction_switch_cooldown_minutes", "0");
        ConfigurableProcessor.Process(props, typeof(SecurityConfig));
        Assert.Equal(4, SecurityConfig.MULTI_CLIENT_MAX_CLIENTS_PER_COMPUTER);
        Assert.False(SecurityConfig.MULTI_CLIENT_ALLOW_BOTH_FACTIONS);
        Assert.Equal(new HashSet<string> { "AA", "BB" }, SecurityConfig.MULTI_CLIENT_IGNORED_MAC_ADDRESSES);
        Assert.Equal(0, SecurityConfig.MULTI_CLIENT_FACTION_SWITCH_COOLDOWN_MINUTES);
        var obsolete = new JavaProperties(); obsolete.SetProperty("gameserver.security.multi_clienting.restriction_mode", "FULL");
        obsolete.SetProperty("gameserver.security.multi_clienting.ignored_mac_addresses", "legacy");
        obsolete.SetProperty("gameserver.security.multi_clienting.faction_switch_cooldown_minutes", "7");
        ConfigurableProcessor.Process(obsolete, typeof(SecurityConfig));
        Assert.Equal(2, SecurityConfig.MULTI_CLIENT_MAX_CLIENTS_PER_COMPUTER);
        Assert.True(SecurityConfig.MULTI_CLIENT_ALLOW_BOTH_FACTIONS);
        Assert.Empty(SecurityConfig.MULTI_CLIENT_IGNORED_MAC_ADDRESSES);
        Assert.Equal(20, SecurityConfig.MULTI_CLIENT_FACTION_SWITCH_COOLDOWN_MINUTES);
    }

    private static CharacterBanInfo? Ban(Player player, AionConnection con, CharacterBanInfo? real = null)
    {
        var data = new PlayerAccountData(player.GetCommonData(), new PlayerAppearance());
        if (real != null) data.SetCharBanInfo(real);
        return (CharacterBanInfo?)typeof(AbstractPlayerInfoPacket).GetMethod("GetCharBanInfo", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(new SM_CHARACTER_LIST(0), new object[] { data, con });
    }
    private sealed class Connection(string ip, string mac, string hdd) : AionConnection(ip)
    {
        public void Identify() { SetMacAddress(mac); SetHddSerial(hdd); }
        protected override void EnqueuePacket(AionServerPacket packet, bool closing) { }
    }
    private sealed class Fixture : IDisposable
    {
        private readonly int max = SecurityConfig.MULTI_CLIENT_MAX_CLIENTS_PER_COMPUTER;
        private readonly bool both = SecurityConfig.MULTI_CLIENT_ALLOW_BOTH_FACTIONS;
        private readonly int minutes = SecurityConfig.MULTI_CLIENT_FACTION_SWITCH_COOLDOWN_MINUTES;
        private readonly ISet<string> ignored = SecurityConfig.MULTI_CLIENT_IGNORED_MAC_ADDRESSES;
        private readonly IDictionary sessions = (IDictionary)typeof(MultiClientService).GetField("sessionsByAccountId", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        private readonly DictionaryEntry[] oldSessions;
        private readonly Dictionary<FieldInfo, object?> oldConfig = typeof(SecurityConfig).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => !f.IsInitOnly && !f.IsLiteral).ToDictionary(f => f, f => f.GetValue(null));
        private readonly BossAiHarness h;
        private int id;
        public long Now = 1_700_000_000_000;
        public Fixture()
        {
            oldSessions = sessions.Keys.Cast<object>().Select(k => new DictionaryEntry(k, sessions[k]!)).ToArray(); sessions.Clear();
            SecurityConfig.MULTI_CLIENT_MAX_CLIENTS_PER_COMPUTER = 2;
            SecurityConfig.MULTI_CLIENT_ALLOW_BOTH_FACTIONS = true;
            SecurityConfig.MULTI_CLIENT_FACTION_SWITCH_COOLDOWN_MINUTES = 20;
            SecurityConfig.MULTI_CLIENT_IGNORED_MAC_ADDRESSES = new HashSet<string>();
            h = BossAiHarness.For().Build(); SystemClock.UseSource(() => Now);
        }
        public Player Player(Race race = Race.ELYOS)
        {
            var p = h.SpawnPlayer(race: race);
            typeof(Player).GetField("playerAccount", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(p, new Account(++id));
            return p;
        }
        public AionConnection Connection(Player p, string ip, string mac, string hdd = "HDD")
        {
            var con = new Connection(ip, mac, hdd); con.Identify(); con.SetAccount(p.GetAccount()); return con;
        }
        public Player Online(string ip, string mac, string hdd = "HDD")
        {
            var p = Player(); p.SetClientConnection(Connection(p, ip, mac, hdd)); return p;
        }
        public void Dispose()
        {
            sessions.Clear(); foreach (DictionaryEntry e in oldSessions) sessions[e.Key] = e.Value;
            SecurityConfig.MULTI_CLIENT_MAX_CLIENTS_PER_COMPUTER = max; SecurityConfig.MULTI_CLIENT_ALLOW_BOTH_FACTIONS = both;
            SecurityConfig.MULTI_CLIENT_FACTION_SWITCH_COOLDOWN_MINUTES = minutes; SecurityConfig.MULTI_CLIENT_IGNORED_MAC_ADDRESSES = ignored;
            foreach (var field in oldConfig) field.Key.SetValue(null, field.Value);
            h.Dispose();
        }
    }
}
