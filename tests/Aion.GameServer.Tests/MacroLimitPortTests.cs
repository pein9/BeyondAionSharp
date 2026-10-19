using System.Buffers.Binary;
using System.Data;
using System.Reflection;
using Aion.Bots.Protocol;
using Aion.Commons.Configuration;
using Aion.Commons.Nio;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Configuration;
using Aion.GameServer.Dao;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Players;
using Aion.GameServer.TestKit;
using Aion.GameServer.Tests.Ai;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class MacroLimitPortTests
{
    [Theory]
    [InlineData(12, 0, false)]
    [InlineData(12, 1, true)]
    [InlineData(12, 12, true)]
    [InlineData(12, 13, false)]
    [InlineData(24, 24, true)]
    [InlineData(24, 25, false)]
    [InlineData(0, 1, false)]
    [InlineData(-1, 1, false)]
    [InlineData(300, 256, true)]
    public void IdValidityUsesTheConfiguredInclusiveLimit(int limit, int id, bool valid)
    {
        using var scope = new LimitScope(limit);
        Assert.Equal(valid, Macros.IsValidId(id));
        var macros = new Macros();
        if (valid) Assert.True(macros.Add(id, "<macro />"));
        else Assert.Throws<ArgumentException>(() => macros.Add(id, "<macro />"));
    }

    [Fact]
    public void MacrosAreSortedByIdAndReplacementKeepsItsSlot()
    {
        using var scope = new LimitScope(24);
        var macros = new Macros();
        foreach (int id in new[] { 24, 1, 13, 2 }) Assert.True(macros.Add(id, id.ToString()));
        Assert.False(macros.Add(13, "replacement"));
        var snapshot = macros.GetAll();
        Assert.Equal(new[] { 1, 2, 13, 24 }, snapshot.Select(m => m.Id));
        Assert.Equal("replacement", snapshot[2].Xml);
        snapshot.Clear();
        Assert.Equal(4, macros.GetAll().Count);
        Assert.True(macros.Remove(13));
        Assert.False(macros.Remove(13));
        Assert.Equal(new[] { 1, 2, 24 }, macros.GetAll().Select(m => m.Id));
    }

    [Theory]
    [InlineData(12)]
    [InlineData(24)]
    public void InvalidStoredRowsAreSkippedWithoutLosingLaterValidRows(int limit)
    {
        using var scope = new LimitScope(limit);
        var table = new DataTable();
        table.Columns.Add("order", typeof(int));
        table.Columns.Add("macro", typeof(string));
        table.Rows.Add(2, "second");
        table.Rows.Add(0, DBNull.Value); // must be skipped before even reading its XML
        table.Rows.Add(limit + 1, "over-limit");
        table.Rows.Add(1, "first");
        table.Rows.Add(-1, "negative");
        table.Rows.Add(limit, "last");
        var macros = new Macros();
        using var reader = table.CreateDataReader();
        PlayerMacrosDAO.ReadMacros(reader, macros, 700001);
        Assert.Equal(new[] { 1, 2, limit }, macros.GetAll().Select(m => m.Id));
        Assert.Equal(new[] { "first", "second", "last" }, macros.GetAll().Select(m => m.Xml));
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void PacketCountSignMarksOnlyTheFinalPart(bool clear, bool last)
    {
        var macros = new List<Macros.Macro> { new(1, "A"), new(2, "B") };
        byte[] payload = Capture(new SM_MACRO_LIST(700001, macros, clear, last));
        Assert.Equal(700001, BinaryPrimitives.ReadInt32LittleEndian(payload));
        Assert.Equal(clear ? 1 : 0, payload[4]);
        Assert.Equal(last ? -2 : 2, BinaryPrimitives.ReadInt16LittleEndian(payload.AsSpan(5)));
        Assert.Equal(1, payload[7]);
        Assert.Equal(2, payload[12]);
        Assert.Equal(7, SM_MACRO_LIST.DYNAMIC_BODY_PART_SIZE_CALCULATOR(new Macros.Macro(1, "\U0001F600")));
    }

    [Theory]
    [InlineData(0, 0, 1)]
    [InlineData(3, 3000, 3)]
    [InlineData(24, 20, 1)]
    public void WholeListReplacementIsSortedPagedAndClearsEvenWhenEmpty(int count, int xmlLength, int pages)
    {
        using var scope = new LimitScope(24);
        using var harness = BossAiHarness.For().Build();
        var player = harness.SpawnPlayer();
        player.SetMacros(new Macros());
        var connection = Attach(player);
        for (int id = count; id > 0; id--) player.GetMacros().Add(id, new string('x', xmlLength));
        PlayerService.SendMacroList(player);
        var packets = connection.Sent.OfType<SM_MACRO_LIST>().ToList();
        Assert.Equal(pages, packets.Count);
        var received = new List<int>();
        for (int i = 0; i < packets.Count; i++)
        {
            byte[] payload = Capture(packets[i]);
            Assert.True(payload.Length <= AionServerPacket.MAX_USABLE_PACKET_BODY_SIZE);
            Assert.Equal(i == 0 ? 1 : 0, payload[4]);
            var entries = (List<Macros.Macro>)typeof(SM_MACRO_LIST).GetField("macros", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(packets[i])!;
            Assert.Equal(i == packets.Count - 1 ? -entries.Count : entries.Count, BinaryPrimitives.ReadInt16LittleEndian(payload.AsSpan(5)));
            var decoded = new BotServerPacketDecoder().Decode(typeof(SM_MACRO_LIST), payload);
            Assert.Equal(i == packets.Count - 1, decoded.Get<bool>("isLast"));
            Assert.Equal(entries.Select(m => new BotMacro((byte)m.Id, m.Xml)), decoded.Get<BotMacro[]>("macros"));
            received.AddRange(entries.Select(m => m.Id));
        }
        Assert.Equal(Enumerable.Range(1, count), received);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    [InlineData(255)]
    public void InvalidClientSlotSendsFailureThenReplacesTheClientList(int id)
    {
        using var scope = new LimitScope(12);
        using var harness = BossAiHarness.For().Build();
        var player = harness.SpawnPlayer();
        player.SetMacros(new Macros());
        player.GetMacros().Add(12, "last");
        player.GetMacros().Add(1, "first");
        var connection = Attach(player);
        var packet = new CM_MACRO_CREATE(0, new HashSet<AionConnection.State>());
        packet.SetConnection(connection);
        packet.SetBuffer(ByteBuffer.Wrap(new byte[] { (byte)id, 0, 0 }).Order(ByteOrder.LITTLE_ENDIAN));
        Assert.True(packet.Read());
        typeof(CM_MACRO_CREATE).GetMethod("RunImpl", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(packet, null);
        Assert.Equal(2, connection.Sent.Count);
        var failure = Assert.IsType<SM_SYSTEM_MESSAGE>(connection.Sent[0]);
        Assert.Equal(MessageId(SM_SYSTEM_MESSAGE.STR_MACRO_MSG_CANNOT_REGIST()), MessageId(failure));
        var replacement = Assert.IsType<SM_MACRO_LIST>(connection.Sent[1]);
        byte[] payload = Capture(replacement);
        Assert.Equal(1, payload[4]);
        Assert.Equal(-2, BinaryPrimitives.ReadInt16LittleEndian(payload.AsSpan(5)));
        Assert.Equal(new[] { 1, 12 }, player.GetMacros().GetAll().Select(m => m.Id));
    }

    [Fact]
    public void MacroLimitBindsDefaultsAndOverridesInBothConfigurationPaths()
    {
        var fields = typeof(CustomConfig).GetFields(BindingFlags.Static | BindingFlags.Public).Where(f => !f.IsInitOnly).ToDictionary(f => f, f => f.GetValue(null));
        string? previousEnvironment = Environment.GetEnvironmentVariable("gameserver.macros.limit");
        try
        {
            Aion.GameServer.Configs.Config.LoadFrom(new JavaProperties(), typeof(CustomConfig));
            Assert.Equal(12, CustomConfig.MACROS_LIMIT);
            var properties = new JavaProperties();
            properties.SetProperty("gameserver.macros.limit", "24");
            Aion.GameServer.Configs.Config.LoadFrom(properties, typeof(CustomConfig));
            Assert.Equal(24, CustomConfig.MACROS_LIMIT);
            Assert.Equal(12, new GameServerCustomOptions().MacrosLimit);
            Environment.SetEnvironmentVariable("gameserver.macros.limit", "32");
            Assert.Equal(32, GameServerOptions.LoadFromJavaConfig(AppContext.BaseDirectory).Custom.MacrosLimit);
            string config = File.ReadAllText(Path.Combine(RealStaticData.RepoRoot(), "game-server/config/main/custom.properties"));
            Assert.Contains("gameserver.macros.limit = 12", config);
            Assert.Contains("gameserver.quest.random_bonus_rewards.enabled = true", config);
        }
        finally
        {
            foreach (var (field, value) in fields) field.SetValue(null, value);
            Environment.SetEnvironmentVariable("gameserver.macros.limit", previousEnvironment);
        }
    }

    private static int MessageId(SM_SYSTEM_MESSAGE message) => (int)typeof(SM_SYSTEM_MESSAGE)
        .GetField("msgId", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(message)!;

    private static byte[] Capture(AionServerPacket packet)
    {
        var buffer = ByteBuffer.Allocate(8192).Order(ByteOrder.LITTLE_ENDIAN);
        packet.SetBuf(buffer);
        typeof(AionServerPacket).GetMethod("WriteImpl", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(packet, new object?[] { null });
        var payload = new byte[buffer.Position()];
        buffer.Flip(); buffer.Get(payload);
        return payload;
    }

    private static Connection Attach(Player player)
    {
        var connection = new Connection();
        connection.SetAccount(player.GetAccount());
        player.SetClientConnection(connection);
        Assert.True(connection.SetActivePlayer(player));
        return connection;
    }

    private sealed class Connection() : AionConnection("127.0.0.1")
    {
        private readonly Queue<AionServerPacket> queue = new();
        public List<AionServerPacket> Sent { get; } = new();
        protected override Queue<AionServerPacket> GetSendMsgQueue() => queue;
        protected override void EnqueuePacket(AionServerPacket packet, bool closing) => Sent.Add(packet);
    }

    private sealed class LimitScope : IDisposable
    {
        private readonly int previous = CustomConfig.MACROS_LIMIT;
        public LimitScope(int limit) => CustomConfig.MACROS_LIMIT = limit;
        public void Dispose() => CustomConfig.MACROS_LIMIT = previous;
    }
}
