using System.Buffers.Binary;
using System.Reflection;
using System.Xml.Serialization;
using Aion.Commons.Nio;
using Aion.GameServer.Dataholders;
using Deleteskill = Aion.GameServer.Handlers.ConsoleCommands.Deleteskill;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Skill;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.SkillEngine.Condition;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.TestKit;
using Aion.GameServer.Tests.Ai;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class SkillGradeLevelPortTests
{
    [Theory]
    [InlineData(null, 1)]
    [InlineData("7", 7)]
    public void LearnTemplateLevelIsExplicitAndDefaultsToOne(string? level, int expected)
    {
        string xml = "<skill skillId=\"24\" minLevel=\"51\"" + (level == null ? "" : " skillLevel=\"" + level + "\"") + " />";
        var template = (SkillLearnTemplate)new XmlSerializer(typeof(SkillLearnTemplate)).Deserialize(new StringReader(xml))!;
        Assert.Equal(expected, template.GetSkillLevel());
        Assert.Null(template.GetClassId());
        Assert.Null(template.GetLearnSkill());
        Assert.Equal(Race.PC_ALL, template.GetRace());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ANewGradeSendsTheLearnMessageEvenWhenThePreviousGradeIsKnown(bool temporary)
    {
        var real = await RealStaticData.LoadAsync();
        using var harness = BossAiHarness.For().Build();
        var player = harness.SpawnPlayer();
        player.GetCommonData().SetPlayerClass(PlayerClass.CLERIC);
        player.GetCommonData().SetLevel(65);
        player.SetSkillList(new PlayerSkillList(new List<PlayerSkillEntry> {
            new(21, 1, 0, IPersistable.PersistentState.NOACTION) }));
        var connection = Attach(player);
        var previous = DataManager.GetRegisteredInstance();
        try
        {
            DataManager.RegisterInstance(real);
            Assert.True(temporary ? player.GetSkillList().AddTemporarySkill(player, 22, 1) : player.GetSkillList().AddSkill(player, 22, 1));
            var packet = Assert.Single(connection.Sent.OfType<SM_SKILL_LIST>());
            Assert.Equal(1300050, (int)typeof(SM_SKILL_LIST).GetField("messageId", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(packet)!);
            var learned = player.GetSkillList().GetSkillEntry(22);
            Assert.Equal(1, learned.GetSkillLevel());
            Assert.Equal(temporary ? IPersistable.PersistentState.NOACTION : IPersistable.PersistentState.NEW, learned.GetPersistentState());
            connection.Sent.Clear();
            Assert.False(player.GetSkillList().AddSkill(player, 22, 1));
            Assert.Empty(connection.Sent.OfType<SM_SKILL_LIST>());
            Assert.True(player.GetSkillList().AddSkill(player, 22, 2));
            packet = Assert.Single(connection.Sent.OfType<SM_SKILL_LIST>());
            Assert.Equal(0, (int)typeof(SM_SKILL_LIST).GetField("messageId", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(packet)!);
        }
        finally { DataManager.RestoreInstance(previous); }
    }

    [Fact]
    public async Task RealLearnedDivineMirrorKeepsGradeFourButUsesLevelOneAndCosts166Mp()
    {
        var real = await RealStaticData.LoadAsync();
        using var harness = BossAiHarness.For().Build();
        var player = harness.SpawnPlayer();
        var previous = DataManager.GetRegisteredInstance();
        try
        {
            DataManager.RegisterInstance(real);
            var learn = Assert.Single(DataManager.SKILL_TREE_DATA.GetTemplatesForSkill(24, PlayerClass.CLERIC, Race.ELYOS));
            var template = Assert.IsType<SkillTemplate>(DataManager.SKILL_DATA.GetSkillTemplate(24));
            Assert.Equal(4, template.GetLvl());
            Assert.Equal(1, learn.GetSkillLevel());
            player.SetSkillList(new PlayerSkillList(new List<PlayerSkillEntry> { new(24, learn.GetSkillLevel(), 0, IPersistable.PersistentState.NEW) }));
            var skill = new Skill(template, player, player);
            Assert.Equal(1, skill.GetSkillLevel());
            var mp = Assert.Single(Assert.IsType<Conditions>(template.GetEndConditions()).GetConditions().OfType<MpCondition>());
            int cost = (int)typeof(MpCondition).GetMethod("GetCost", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(mp, new object[] { skill })!;
            Assert.Equal(166, cost);
        }
        finally { DataManager.RestoreInstance(previous); }
    }

    [Theory]
    [InlineData(24, 1, 0)]
    [InlineData(1001, 5, 0)]
    [InlineData(1001, 7, 1)]
    [InlineData(30002, 350, 0)]
    [InlineData(40001, 450, 0)]
    public void SkillListWritesTheActualEntryLevelForEveryKindOfSkill(int id, int level, int type)
    {
        var entry = new PlayerSkillEntry(id, level, type, IPersistable.PersistentState.NOACTION);
        byte[] payload = Capture(new SM_SKILL_LIST(new List<PlayerSkillEntry> { entry }));
        Assert.Equal(id, BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(3)));
        Assert.Equal(level, BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(5)));
        if (id < 30000)
        {
            byte[] removal = Capture(new SM_SKILL_REMOVE(entry));
            Assert.Equal(level, removal[2]);
            Assert.Equal(type, removal[3]);
        }
    }

    [Fact]
    public void DeleteSkillHelpNamesItsOwnCommand()
    {
        using var harness = BossAiHarness.For().Build();
        var player = harness.SpawnPlayer();
        var connection = Attach(player);
        new Deleteskill().Execute(player);
        var packet = Assert.Single(connection.Sent.OfType<SM_MESSAGE>());
        string text = (string)typeof(SM_MESSAGE).GetField("message", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(packet)!;
        Assert.Equal("syntax ///deleteskill <skill name>", text);
    }

    [Fact]
    public void MigrationResetsOnlyCombatSkillLevelsAndSchemaKeepsPreviousColumns()
    {
        string root = RealStaticData.RepoRoot();
        Assert.Equal("UPDATE `player_skills` SET `skill_level` = 1 WHERE `skill_id` < 30000;",
            File.ReadAllLines(Path.Combine(root, "game-server/sql/update.sql")).Single(line => line.StartsWith("UPDATE")));
        string schema = File.ReadAllText(Path.Combine(root, "game-server/sql/aion_gs.sql"));
        Assert.Contains("`magical_criticals` tinyint NOT NULL DEFAULT '0'", schema);
        Assert.Contains("`rank_limit_expire_time` int NOT NULL DEFAULT '0'", schema);
        Assert.Contains("name=\"skillLevel\" type=\"xs:int\" default=\"1\"", File.ReadAllText(Path.Combine(root, "game-server/data/static_data/skill_tree/skill_tree.xsd")));
    }

    private static byte[] Capture(AionServerPacket packet)
    {
        var buffer = ByteBuffer.Allocate(8192).Order(ByteOrder.LITTLE_ENDIAN);
        packet.SetBuf(buffer);
        typeof(AionServerPacket).GetMethod("WriteImpl", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(packet, new object?[] { null });
        var payload = new byte[buffer.Position()];
        buffer.Flip();
        buffer.Get(payload);
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
}
