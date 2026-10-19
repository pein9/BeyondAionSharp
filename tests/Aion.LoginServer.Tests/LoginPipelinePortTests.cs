using System.Text.Json;
using Aion.Commons.Network;
using Aion.LoginServer.Network;
using Aion.LoginServer.Network.Aion;
using Aion.LoginServer.Network.Aion.ClientPackets;
using Aion.LoginServer.Network.Crypto;
using Aion.LoginServer.Network.GameServer;

namespace Aion.LoginServer.Tests;

public sealed class LoginPipelinePortTests
{
    internal static readonly byte[] Key = { 1, 3, 5, 7, 9, 11, 13, 15, 2, 4, 6, 8, 10, 12, 14, 16 };

    private static JsonElement Fixture()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "AionServer.slnx"))) dir = dir.Parent;
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir!.FullName,
            "parity-artifacts/golden/formulas/LoginPipeline.bce89b2fc.json")));
        Assert.Equal("bce89b2fcf740d3b2bcd1b75dc62e7c62382aeca", document.RootElement.GetProperty("sourceJavaCommit").GetString());
        return document.RootElement.Clone();
    }

    internal static byte[] Golden(string name) => Convert.FromHexString(Fixture().GetProperty("packets").GetProperty(name).GetString()!);

    public static IEnumerable<object[]> CryptoCases() => Fixture().GetProperty("cryptoCases").EnumerateArray()
        .Where(c => c.GetProperty("sequence").GetInt32() == 0)
        .Select(c => new object[] { c.GetProperty("offset").GetInt32(), c.GetProperty("size").GetInt32() });

    [Theory]
    [MemberData(nameof(CryptoCases))]
    public void FirstAndSessionPacketsMatchJavaWithDirtyPaddingAndOffset(int offset, int size)
    {
        var engine = new LoginCryptEngine(() => 0x1234ABCD);
        engine.UpdateKey(Key);
        var rows = Fixture().GetProperty("cryptoCases").EnumerateArray()
            .Where(c => c.GetProperty("offset").GetInt32() == offset && c.GetProperty("size").GetInt32() == size);
        foreach (var row in rows)
        {
            var data = Enumerable.Repeat((byte)0x5A, 256).ToArray();
            for (int i = 0; i < size; i++) data[offset + i] = unchecked((byte)(i * 7 + 3));
            int length = engine.Encrypt(data, offset, size);
            Assert.Equal(Convert.FromHexString(row.GetProperty("cipherHex").GetString()!), data[offset..(offset + length)]);
            Assert.All(data[..offset], b => Assert.Equal(0x5A, b));
            Assert.All(data[(offset + length)..], b => Assert.Equal(0x5A, b));
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(7)]
    [InlineData(31)]
    public void ClientChecksumIgnoresFinalWordAndBufferOffsetButRejectsCorruption(int offset)
    {
        // Vectors pinned in Java CryptEngineTest. The final unused word is nonzero.
        var key = Enumerable.Range(0, 16).Select(i => (byte)(0x31 + i * 5)).ToArray();
        var expected = Convert.FromHexString("0B000000112233441A2233441A223344");
        foreach (bool corrupt in new[] { false, true })
        {
            var engine = new LoginCryptEngine(() => 0x1234ABCD);
            engine.UpdateKey(key);
            engine.Encrypt(new byte[32], 0, 1);
            var data = Enumerable.Repeat((byte)0x5A, offset + 32).ToArray();
            var cipher = Convert.FromHexString("F145B9B4084C3CFD7E5C1A0CB8CDDFD7");
            if (corrupt) cipher[3] ^= 0x7F;
            cipher.CopyTo(data, offset);
            Assert.Equal(!corrupt, engine.Decrypt(data, offset, cipher.Length));
            if (!corrupt) Assert.Equal(expected, data[offset..(offset + cipher.Length)]);
            Assert.All(data[..offset], b => Assert.Equal(0x5A, b));
            Assert.All(data[(offset + cipher.Length)..], b => Assert.Equal(0x5A, b));
        }
    }

    [Theory]
    [InlineData(128)]
    [InlineData(256)]
    public void LoginReadsMetadataAndBothCredentialShapesWithEveryPaddingLength(int credentials)
    {
        for (int padding = 0; padding < 8; padding++)
        {
            using var body = new PacketBuffer();
            body.WriteC(0);
            byte[] rsa = Enumerable.Range(0, credentials).Select(i => unchecked((byte)i)).ToArray();
            body.WriteB(rsa);
            body.WriteD(0x11223344);
            body.WriteB(new byte[16]);
            body.WriteD(32);
            body.WriteH(0xFEDC);
            body.WriteC(0xFE);
            byte[] md5 = Enumerable.Range(0, 16).Select(i => (byte)(255 - i)).ToArray();
            body.WriteB(md5);
            body.WriteB(Enumerable.Repeat((byte)0x5A, padding).ToArray());
            using var input = new PacketBuffer(body.ToArray());
            var packet = Assert.IsType<CmLogin>(AionClientPacketFactory.Create(input, LoginClientState.AuthedGameGuard, out bool failed));
            Assert.False(failed);
            Assert.Equal(rsa, packet.EncryptedLoginData);
            Assert.Equal(0x11223344, packet.SessionId);
            Assert.Equal(0xFEDC, packet.PackageId);
            Assert.Equal(0xFE, packet.OpenClientWindows);
            Assert.Equal(md5, packet.PcInfoMd5);
            Assert.Equal(0, input.Remaining);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(16)]
    public void OnlySubBlockPaddingIsConsumed(int padding)
    {
        using var body = new PacketBuffer();
        body.WriteC(7); body.WriteD(123); body.WriteB(new byte[16]);
        body.WriteB(Enumerable.Repeat((byte)0x5A, padding).ToArray());
        using var input = new PacketBuffer(body.ToArray());
        Assert.IsType<CmAuthGameGuard>(AionClientPacketFactory.Create(input, LoginClientState.Connected, out bool failed));
        Assert.False(failed);
        Assert.Equal(padding < 8 ? 0 : padding, input.Remaining);
    }

    [Theory]
    [InlineData(7, true)]
    [InlineData(8, true)]
    [InlineData(0x7F, false)]
    [InlineData(0, false)] // known in another state, ignored here
    public void LoginFactoryDistinguishesReadFailureFromUnknownOrWrongState(int opcode, bool expected)
    {
        Assert.Null(AionClientPacketFactory.Create(new PacketBuffer(new[] { (byte)opcode }), LoginClientState.Connected, out bool failed));
        Assert.Equal(expected, failed);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(0x7F, false)]
    public void BridgeFactoryDistinguishesReadFailureFromUnknown(int opcode, bool expected)
    {
        Assert.Null(GsClientPacketFactory.Create(new PacketBuffer(new[] { (byte)opcode }), GameServerConnectionState.Connected, out bool failed));
        Assert.Equal(expected, failed);
    }
}
