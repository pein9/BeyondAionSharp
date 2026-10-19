using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Serialization;
using Aion.Commons.Database;
using Aion.Commons.Nio;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Team.Legion;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Trade;
using MySqlConnector;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class LegionEmblemPortTests
{
    [Fact]
    public void ActualModelMatchesJavaTransitionsAndNeverReallocatesBetweenChunks()
    {
        using var fixture = Fixture();
        foreach (var sequence in fixture.RootElement.GetProperty("traces").EnumerateArray())
        {
            var emblem = new LegionEmblem();
            foreach (var state in sequence.EnumerateArray())
            {
                string action = state.GetProperty("action").GetString()!; var prior = emblem.GetUploadData();
                if (action.StartsWith("size:")) emblem.SetUploadSize(int.Parse(action[5..]));
                else if (action.StartsWith("data:"))
                {
                    Assert.Equal(state.GetProperty("accepted").GetBoolean(), emblem.AddUploadData(Convert.FromHexString(action[5..])));
                    Assert.Same(prior, emblem.GetUploadData());
                }
                else if (action == "on") emblem.SetUploading(true);
                else emblem.ResetUploadSettings();
                Assert.Equal(state.GetProperty("complete").GetBoolean(), emblem.IsUploadComplete());
                Assert.Equal(state.GetProperty("uploading").GetBoolean(), emblem.IsUploading());
                var expected = state.GetProperty("dataHex");
                if (expected.ValueKind == JsonValueKind.Null) Assert.Null(emblem.GetUploadData());
                else Assert.Equal(Convert.FromHexString(expected.GetString()!), emblem.GetUploadData());
            }
        }
        Assert.Equal(80000, LegionEmblem.MAX_CUSTOM_EMBLEM_SIZE);
    }

    [Fact]
    public void ProductionReaderMatchesExactJavaBodies()
    {
        using var fixture = Fixture();
        foreach (var vector in fixture.RootElement.GetProperty("reads").EnumerateArray())
        {
            var packet = Read(null, Convert.FromHexString(vector.GetProperty("bodyHex").GetString()!));
            Assert.Equal(Convert.FromHexString(vector.GetProperty("dataHex").GetString()!), Get(packet, "data"));
            Assert.Equal(0, packet.GetRemainingBytes());
        }
    }

    [Theory]
    [InlineData(-1)] [InlineData(int.MaxValue)] [InlineData(5)]
    public void MalformedChunkLengthsFailBeforeAllocatingOrDispatching(int length)
    {
        var packet = new CM_LEGION_UPLOAD_EMBLEM(0, new HashSet<AionConnection.State>());
        byte[] body = new byte[4]; System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(body, length);
        packet.SetBuffer(ByteBuffer.Wrap(body).Order(ByteOrder.LITTLE_ENDIAN));
        Assert.False(packet.Read());
    }

    [Theory]
    [InlineData(int.MinValue)] [InlineData(-1)] [InlineData(0)] [InlineData(80001)] [InlineData(int.MaxValue)]
    public void InvalidDeclaredSizeClearsPendingUploadWithoutChangingStoredEmblemOrMoney(int size)
    {
        using var s = new Scope(); var e = s.Emblem; var custom = new byte[] { 9, 8, 7 }; e.SetCustomEmblemData(custom);
        e.SetUploadSize(4); e.AddUploadData([1, 2]); e.SetUploading(true); long before = s.Player.GetInventory().GetKinah();
        s.Clear(); s.Info(size);
        Assert.False(e.IsUploading()); Assert.False(e.IsUploadComplete()); Assert.Null(e.GetUploadData());
        Assert.Same(custom, e.GetCustomEmblemData()); Assert.Equal(LegionEmblemType.CUSTOM, e.GetEmblemType());
        Assert.Equal(before, s.Player.GetInventory().GetKinah()); Assert.Empty(s.Packets);
    }

    [Theory]
    [InlineData(1)] [InlineData(80000)]
    public void AllowedDeclaredSizeAllocatesOnceAndResetsTheCounter(int size)
    {
        using var s = new Scope(); s.Info(size); var buffer = s.Emblem.GetUploadData();
        Assert.Equal(size, buffer.Length); Assert.True(s.Emblem.IsUploading()); Assert.False(s.Emblem.IsUploadComplete());
        Assert.Equal((byte)255, s.Emblem.GetColor_a()); Assert.Equal((byte)128, s.Emblem.GetColor_r());
        Assert.Equal(LegionEmblemType.DEFAULT, s.Emblem.GetEmblemType()); // CUSTOM without bytes falls back as in Java.
        Assert.Equal(10000000, s.Player.GetInventory().GetKinah()); Assert.Empty(s.Packets);
    }

    [Theory]
    [InlineData("rank")] [InlineData("level")] [InlineData("money")] [InlineData("active")]
    public void ExistingUploadRestrictionsStillRefuseAndResetTheUpload(string reason)
    {
        using var s = new Scope();
        if (reason == "rank") s.Player.GetLegionMember().SetRank(LegionRank.LEGIONARY);
        if (reason == "level") s.Legion.SetLegionLevel(2);
        if (reason == "money") s.Player.GetInventory().DecreaseKinah(s.Player.GetInventory().GetKinah());
        if (reason == "active") { s.Emblem.SetUploadSize(4); s.Emblem.SetUploading(true); }
        s.Info(4); Assert.False(s.Emblem.IsUploading()); Assert.Null(s.Emblem.GetUploadData());
    }

    [Fact]
    public void ExcessChunkResetsTheSessionAndDoesNotReplaceOrChargeForAnEmblem()
    {
        using var s = new Scope(); var custom = new byte[] { 7 }; s.Info(3); s.Emblem.SetCustomEmblemData(custom);
        s.Service.UploadEmblemData(s.Player, [1, 2]); var buffer = s.Emblem.GetUploadData(); Assert.Equal(new byte[] { 1, 2, 0 }, buffer);
        s.Clear(); Run(Read(s.Player, Body([3, 4])));
        Assert.Equal(SM_SYSTEM_MESSAGE.STR_GUILD_WARN_CORRUPT_EMBLEM_FILE().GetId(), Assert.IsType<SM_SYSTEM_MESSAGE>(Assert.Single(s.Packets)).GetId());
        Assert.False(s.Emblem.IsUploading()); Assert.Null(s.Emblem.GetUploadData()); Assert.Same(custom, s.Emblem.GetCustomEmblemData());
        Assert.Equal(10000000, s.Player.GetInventory().GetKinah()); Assert.Equal(new byte[] { 1, 2, 0 }, buffer);
        s.Clear(); s.Service.UploadEmblemData(s.Player, [3]); Assert.False(s.Emblem.IsUploading());
        Assert.All(s.Packets, p => Assert.Equal(SM_SYSTEM_MESSAGE.STR_GUILD_WARN_FAILURE_UPLOAD_EMBLEM().GetId(), Assert.IsType<SM_SYSTEM_MESSAGE>(p).GetId()));
    }

    [Fact]
    public void ZeroLengthChunksAndMissingPlayersRemainNoOpsAtThePacketBoundary()
    {
        using var s = new Scope(); s.Info(4); var buffer = s.Emblem.GetUploadData(); s.Clear();
        Run(Read(s.Player, Body([]))); Run(Read(null, Body([1])));
        Assert.Same(buffer, s.Emblem.GetUploadData()); Assert.Equal(new byte[4], buffer); Assert.Empty(s.Packets);
    }

    [Theory]
    [InlineData(int.MinValue)] [InlineData(-1)] [InlineData(0)] [InlineData(5)] [InlineData(255)] [InlineData(int.MaxValue)]
    public void InvalidRankCannotCreateAnotherBrigadeGeneralOrIndexOutsideTheEnum(int rank)
    {
        using var fixture = Fixture(); Assert.False(fixture.RootElement.GetProperty("ranks").EnumerateArray().Single(v => v.GetProperty("id").GetInt32() == rank).GetProperty("allowed").GetBoolean());
        using var s = new Scope(); s.Clear(); s.Service.AppointRank(s.Player, s.Target.GetName(), rank);
        Assert.Equal(LegionRank.VOLUNTEER, s.Target.GetLegionMember().GetRank()); Assert.Empty(s.Packets);
        Assert.True(s.Player.GetLegionMember().IsBrigadeGeneral());
    }

    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public void ValidRankOrdinalsRetainTheirRankAndNotification(int rank)
    {
        using var fixture = Fixture(); Assert.True(fixture.RootElement.GetProperty("ranks").EnumerateArray().Single(v => v.GetProperty("id").GetInt32() == rank).GetProperty("allowed").GetBoolean());
        using var s = new Scope(); s.Clear(); Assert.Same(s.Target, s.Common.H.World.GetPlayer(s.Target.GetName()));
        s.Service.AppointRank(s.Player, s.Target.GetName(), rank);
        Assert.Equal(Enum.GetValues<LegionRank>()[rank], s.Target.GetLegionMember().GetRank());
        int[] ids = [0, 1400902, 1300267, 1300268, 1400903];
        Assert.Equal(ids[rank], Get(Assert.Single(s.Packets), "msgId"));
    }

    [SkippableFact]
    public void ActualCompletionPersistsBytesChargesOnceAndBroadcastsThenClearsTheUpload()
    {
        string? testConnection = Environment.GetEnvironmentVariable("AION_LEGION_EMBLEM_PORT_TEST_CONNECTION");
        Skip.If(string.IsNullOrWhiteSpace(testConnection), "Set AION_LEGION_EMBLEM_PORT_TEST_CONNECTION to a disposable MySQL test server.");
        using var s = new Scope(); string db = "emblem_port_" + Guid.NewGuid().ToString("N");
        var builder = new MySqlConnectionStringBuilder(testConnection) { Database = "" };
        using var admin = new MySqlConnection(builder.ConnectionString); admin.Open(); Execute(admin, "CREATE DATABASE `" + db + "`");
        try
        {
            builder.Database = db; using var connection = new MySqlConnection(builder.ConnectionString); connection.Open();
            string ddl = File.ReadAllText(Path.Combine(PvpExpPortTests.Root(), "game-server/sql/aion_gs.sql"));
            foreach (string table in new[] { "legions", "legion_emblems", "legion_history" })
            {
                string sql = Regex.Match(ddl, "CREATE TABLE `" + table + @"` \([\s\S]*?\n\)[^;]*;").Value;
                Assert.NotEmpty(sql); Execute(connection, sql);
            }
            Execute(connection, "INSERT INTO legions(id,name) VALUES(" + s.Legion.GetLegionId() + ",'EmblemFixture')");
            using var source = new MySqlDataSourceBuilder(builder.ConnectionString).Build(); var slot = typeof(DatabaseFactory).GetField("_dataSource", BindingFlags.Static | BindingFlags.NonPublic)!; object? previous = slot.GetValue(null); slot.SetValue(null, source);
            try
            {
                foreach (int size in new[] { 4, 80000 })
                {
                    byte[] expected = Enumerable.Range(0, size).Select(i => unchecked((byte)(i * 17))).ToArray();
                    long before = s.Player.GetInventory().GetKinah(); long price = PricesService.GetPriceForService(LegionConfig.LEGION_EMBLEM_REQUIRED_KINAH, s.Player.GetRace());
                    s.Info(size); s.Clear(); Run(Read(s.Player, Body(expected[..2])));
                    Assert.Equal(before, s.Player.GetInventory().GetKinah()); Assert.Empty(s.Packets);
                    Run(Read(s.Player, Body(expected[2..])));
                    Assert.Equal(before - price, s.Player.GetInventory().GetKinah()); Assert.Equal(expected, s.Emblem.GetCustomEmblemData());
                    Assert.False(s.Emblem.IsUploading()); Assert.Null(s.Emblem.GetUploadData());
                    Assert.Equal(expected, Aion.GameServer.Dao.LegionDAO.LoadLegionEmblem(s.Legion.GetLegionId()).GetCustomEmblemData());
                    Assert.Contains(s.Packets, p => p is SM_LEGION_UPDATE_EMBLEM); Assert.Contains(s.Packets, p => p is SM_LEGION_SEND_EMBLEM_DATA);
                    Assert.Contains(s.Packets, p => p is SM_SYSTEM_MESSAGE m && m.GetId() == SM_SYSTEM_MESSAGE.STR_GUILD_WARN_SUCCESS_UPLOAD_EMBLEM().GetId());
                }
                using var count = new MySqlCommand("SELECT COUNT(*) FROM legion_history WHERE history_type='EMBLEM_REGISTER'", connection); Assert.Equal(2L, Convert.ToInt64(count.ExecuteScalar()));
            }
            finally { slot.SetValue(null, previous); }
        }
        finally { Execute(admin, "DROP DATABASE `" + db + "`"); }
    }

    private static JsonDocument Fixture() => JsonDocument.Parse(File.ReadAllText(Path.Combine(PvpExpPortTests.Root(), "parity-artifacts/golden/formulas/LegionEmblem.ba383f739.json")));
    private static object? Get(object obj, string name) => Field(obj.GetType(), name).GetValue(obj);
    private static void Set(object obj, string name, object? value) => Field(obj.GetType(), name).SetValue(obj, value);
    private static FieldInfo Field(Type t, string name) { for (Type? current = t; current != null; current = current.BaseType) { var f = current.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly); if (f != null) return f; } throw new MissingFieldException(t.Name, name); }
    private static byte[] Body(byte[] data) { byte[] result = new byte[data.Length + 4]; System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(result, data.Length); data.CopyTo(result, 4); return result; }
    private static CM_LEGION_UPLOAD_EMBLEM Read(Player? player, byte[] data) { var packet = new CM_LEGION_UPLOAD_EMBLEM(0, new HashSet<AionConnection.State>()); var con = player?.GetClientConnection() ?? new PvpExpPortTests.Connection(); if (player == null) con.SetActivePlayer(null!); packet.SetConnection(con); packet.SetBuffer(ByteBuffer.Wrap(data).Order(ByteOrder.LITTLE_ENDIAN)); Assert.True(packet.Read()); return packet; }
    private static void Run(AionClientPacket packet) => packet.GetType().GetMethod("RunImpl", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(packet, null);
    private static void Execute(MySqlConnection connection, string sql) { using var command = new MySqlCommand(sql, connection); command.ExecuteNonQuery(); }

    private sealed class Scope : IDisposable
    {
        internal PvpExpPortTests.Scope Common { get; } = new();
        internal Player Player { get; } internal Player Target { get; }
        internal LegionService Service { get; } = (LegionService)Activator.CreateInstance(typeof(LegionService), true)!;
        internal Legion Legion { get; } = new(987612, "EmblemFixture");
        internal LegionEmblem Emblem => Legion.GetLegionEmblem();
        internal AionServerPacket[] Packets => ((PvpExpPortTests.Connection)Player.GetClientConnection()).Packets;
        internal Scope()
        {
            Common.Now = 1792410000000;
            using var xml = new StringReader("<item_templates><item_template id='182400001' name='Kinah' level='1' quality='COMMON' max_stack_count='2147483647'/></item_templates>"); var data = (ItemData)new XmlSerializer(typeof(ItemData)).Deserialize(xml)!; data.AfterUnmarshal(null!); Set(Common.H.Data.StaticData, "<ItemDataDh>k__BackingField", data);
            Player = Common.Player(65); Target = Common.Player(65); Legion.SetLegionLevel(3); Legion.SetLegionEmblem(new LegionEmblem());
            var cache = (ConcurrentDictionary<int, LegionMember>)Get(Service, "legionMemberById")!;
            foreach (var player in new[] { Player, Target })
            {
                var member = new LegionMember(player.GetObjectId(), Legion); member.SetPlayerData(player); member.SetRank(player == Player ? LegionRank.BRIGADE_GENERAL : LegionRank.VOLUNTEER); Set(member, "online", true); Set(player, "legionMember", member); cache[player.GetObjectId()] = member; Legion.AddLegionMember(player.GetObjectId());
            }
            var kinah = new Item(987698, data.GetItemTemplate(182400001)); kinah.SetItemCount(10000000); Player.GetInventory().OnLoadHandler(kinah); Clear();
        }
        internal void Clear() { ((PvpExpPortTests.Connection)Player.GetClientConnection()).Clear(); ((PvpExpPortTests.Connection)Target.GetClientConnection()).Clear(); }
        internal void Info(int size) => Service.UploadEmblemInfo(Player, size, 255, 128, 127, 0, LegionEmblemType.CUSTOM);
        public void Dispose() => Common.Dispose();
    }
}
