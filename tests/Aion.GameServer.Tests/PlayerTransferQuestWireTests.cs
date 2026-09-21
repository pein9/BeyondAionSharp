using System.Runtime.CompilerServices;
using Aion.Commons.Network;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.LoginServer.ServerPackets;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services.Transfers;

namespace Aion.GameServer.Tests;

public sealed class PlayerTransferQuestWireTests
{
    [Theory]
    [InlineData(null, null)]
    [InlineData(0L, null)]
    [InlineData(null, 1735689600123L)]
    [InlineData(1735689600123L, 1767225600456L)]
    [InlineData(-1L, 0L)]
    public void SourceQuestPacketPreservesNullableDatesAndEveryOtherField(long? complete, long? repeat)
    {
        var quest = new QuestState(20100, QuestStatus.START, 0x12345678, 0x33445566, 3,
            Date(repeat), null, Date(complete));
        // Only the quest list is consulted by this packet section; no world/DB fixture is required.
        var player = (Player)RuntimeHelpers.GetUninitializedObject(typeof(Player));
        var quests = new QuestStateList();
        quests.AddQuest(quest.GetQuestId(), quest);
        player.SetQuestStateList(quests);
        var transfer = new TransferablePlayer(500, 100, 101) { player = player, taskId = 0x11223344 };
        byte[] actual = new SM_PTRANSFER_CONTROL(SM_PTRANSFER_CONTROL.QUEST_INFORMATION, transfer).SerializePayload();
        using var expected = new PacketBuffer();
        expected.WriteC(13);
        expected.WriteC(9);
        expected.WriteD(0x11223344);
        expected.WriteD(1);
        WriteQuest(expected, complete, repeat);
        Assert.Equal(expected.ToArray(), actual);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData(0L, null)]
    [InlineData(null, 1735689600123L)]
    [InlineData(1735689600123L, 1767225600456L)]
    [InlineData(-1L, 0L)]
    public void TargetQuestReaderPreservesNullableDatesWithoutTurningNullIntoEpoch(long? complete, long? repeat)
    {
        using var buffer = new PacketBuffer();
        WriteQuest(buffer, complete, repeat);
        var reader = new CMT_CHARACTER_INFORMATION(buffer.ToArray());
        QuestState actual = Assert.IsType<QuestState>(reader.ReadQuestState());
        Assert.Equal(20100, actual.GetQuestId());
        Assert.Equal(QuestStatus.START, actual.GetStatus());
        Assert.Equal(0x12345678, actual.GetQuestVars().GetQuestVars());
        Assert.Equal(3, actual.GetCompleteCount());
        Assert.Null(actual.GetRewardGroup());
        Assert.Equal(Date(complete), actual.GetLastCompleteTime());
        Assert.Equal(Date(repeat), actual.GetNextRepeatTime());
        Assert.Equal(0x33445566, actual.GetFlags());
        Assert.Equal(0, reader.GetRemainingBytes());
    }

    [Fact]
    public void CloneReaderUsesJavaTransferLittleEndianOrderForAllNumericWidths()
    {
        using var buffer = new PacketBuffer();
        buffer.WriteD(0x12345678);
        buffer.WriteQ(0x0123456789abcdefL);
        buffer.WriteF(123.5f);
        var reader = new NumericReader(buffer.ToArray());
        Assert.Equal((0x12345678, 0x0123456789abcdefL, 123.5f), reader.ReadNumbers());
        Assert.Equal(0, reader.GetRemainingBytes());
    }

    [Fact]
    public void DisabledQuestTransferStillConsumesTheSectionWithoutParsingItsStatus()
    {
        using var buffer = new PacketBuffer();
        WriteQuest(buffer, null, null, "not-a-quest-status");
        var reader = new CMT_CHARACTER_INFORMATION(buffer.ToArray());
        Assert.Null(reader.ReadQuestState(includeQuest: false));
        Assert.Equal(0, reader.GetRemainingBytes());
    }

    [Fact]
    public void NonSentinelInvalidEpochIsNotSilentlyConvertedToNull()
    {
        using var buffer = new PacketBuffer();
        WriteQuest(buffer, long.MaxValue, null);
        var reader = new CMT_CHARACTER_INFORMATION(buffer.ToArray());
        Assert.Throws<ArgumentOutOfRangeException>(() => reader.ReadQuestState());
    }

    private static DateTime? Date(long? value) => value.HasValue
        ? DateTimeOffset.FromUnixTimeMilliseconds(value.Value).UtcDateTime : null;

    private static void WriteQuest(PacketBuffer buffer, long? complete, long? repeat, string status = "START")
    {
        buffer.WriteD(20100);
        buffer.WriteS(status);
        buffer.WriteD(0x12345678);
        buffer.WriteD(3);
        buffer.WriteD(-1);
        // D19 reserves a value outside DateTime's epoch range; epoch zero stays a real date.
        buffer.WriteQ(complete ?? long.MinValue);
        buffer.WriteQ(repeat ?? long.MinValue);
        buffer.WriteD(0x33445566);
    }

    private sealed class NumericReader(byte[] bytes) : CMT_CHARACTER_INFORMATION(bytes)
    {
        public (int, long, float) ReadNumbers() => (ReadD(), ReadQ(), ReadF());
    }
}
