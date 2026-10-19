using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Aion.Commons.Nio;
using Aion.GameServer.Model.Account;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.TestKit;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class CharacterAppearancePortTests
{
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;

    [Fact]
    public void RealReaderMatchesAllJavaFieldsAndFloatClampBoundaries()
    {
        using var fixture = Fixture(); var cases = fixture.RootElement.GetProperty("cases"); Assert.Equal(18, cases.GetArrayLength());
        foreach (var c in cases.EnumerateArray())
        {
            var appearance = Read(c);
            Assert.Equal(53, c.GetProperty("fields").EnumerateObject().Count());
            foreach (var field in c.GetProperty("fields").EnumerateObject())
            {
                object value = typeof(PlayerAppearance).GetField(field.Name, Fields)!.GetValue(appearance)!;
                int actual = value is float f ? BitConverter.SingleToInt32Bits(f) : (int)value;
                Assert.Equal(field.Value.GetInt32(), actual);
            }
        }
    }

    [Fact]
    public void ActualWorldPacketKeepsEveryNonzeroAppearanceByteInTheJavaPosition()
    {
        using var fixture = Fixture();
        using var full = JsonDocument.Parse(File.ReadAllText(Path.Combine(RealStaticData.RepoRoot(), "parity-artifacts/golden/packets/SM_PLAYER_INFO.a519ffc7b.json")));
        var first = full.RootElement.GetProperty("cases")[0];
        int offset = fixture.RootElement.GetProperty("worldAppearanceOffsets").GetProperty(first.GetProperty("name").GetString()!).GetInt32();
        foreach (var c in fixture.RootElement.GetProperty("cases").EnumerateArray())
        {
            var actual = GoldenPlayerInfoFixtureTests.CapturePlayerInfoWithAppearance(first.GetProperty("inputs"), Read(c));
            var expected = Convert.FromHexString(c.GetProperty("worldHex").GetString()!);
            Assert.Equal(expected, actual.AsSpan(offset, expected.Length).ToArray());
        }
    }

    [Fact]
    public void ActualAccountPacketKeepsEveryNonzeroAppearanceByteInTheJavaPosition()
    {
        using var s = new PvpExpPortTests.Scope(); using var fixture = Fixture(); var player = s.Player(65);
        int offset = fixture.RootElement.GetProperty("accountAppearanceOffset").GetInt32();
        foreach (var c in fixture.RootElement.GetProperty("cases").EnumerateArray())
        {
            var expected = Convert.FromHexString(c.GetProperty("accountHex").GetString()!);
            var account = new PlayerAccountData(player.GetCommonData(), Read(c), new CharacterBanInfo(0, long.MaxValue, "fixture"), []);
            // A test-only packet has no registered opcode; only the real base writer is exercised here.
            var packet = (AccountPrefix)RuntimeHelpers.GetUninitializedObject(typeof(AccountPrefix)); packet.Data = account;
            var buffer = ByteBuffer.Allocate(offset + expected.Length).Order(ByteOrder.LITTLE_ENDIAN); packet.SetBuf(buffer);
            // Stop the real writer immediately after appearance, before the later settings/mail/broker queries.
            var exception = Assert.Throws<InvalidOperationException>(packet.WritePrefix);
            Assert.Equal("BufferOverflow", exception.Message); Assert.Equal(offset + expected.Length, buffer.Position());
            var actual = new byte[buffer.Position()]; buffer.Flip(); buffer.Get(actual);
            Assert.Equal(expected, actual.AsSpan(offset, expected.Length).ToArray());
        }
    }

    [Theory]
    [InlineData(0.1f)]
    [InlineData(1.5f)]
    [InlineData(float.NaN)]
    public void ModelSetterKeepsExistingDatabaseAndPresetValuesOutsideTheIncomingSliderRange(float legacy)
    {
        var appearance = new PlayerAppearance(); appearance.SetHeightScale(legacy);
        if (float.IsNaN(legacy)) Assert.True(float.IsNaN(appearance.GetHeightScale()));
        else Assert.Equal(BitConverter.SingleToInt32Bits(legacy), BitConverter.SingleToInt32Bits(appearance.GetHeightScale()));
    }

    private static PlayerAppearance Read(JsonElement c)
    {
        var probe = new AppearanceReader(); probe.SetConnection(new PvpExpPortTests.Connection());
        probe.SetBuffer(ByteBuffer.Wrap(Convert.FromHexString(c.GetProperty("bodyHex").GetString()!)).Order(ByteOrder.LITTLE_ENDIAN));
        Assert.True(probe.Read()); Assert.Equal(0, probe.GetRemainingBytes()); return probe.Appearance;
    }
    private static JsonDocument Fixture()
    {
        var fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(RealStaticData.RepoRoot(), "parity-artifacts/golden/formulas/CharacterAppearance.a519ffc7b.json")));
        Assert.Equal("a519ffc7bcc3df94e713a72ef72349106687b0c3", fixture.RootElement.GetProperty("sourceJavaCommit").GetString()); return fixture;
    }
    private sealed class AppearanceReader() : AbstractCharacterEditPacket(0, new HashSet<AionConnection.State>())
    {
        internal PlayerAppearance Appearance => playerAppearance;
        protected override void ReadImpl() => ReadAppearance();
        protected override void RunImpl() { }
    }
    private sealed class AccountPrefix : AbstractPlayerInfoPacket
    {
        internal PlayerAccountData Data = null!;
        protected override void WriteImpl(AionConnection con) => WritePlayerInfo(Data, con);
        internal void WritePrefix() => WriteImpl(null!);
    }
}
