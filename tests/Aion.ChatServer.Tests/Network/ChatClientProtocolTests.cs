using System.Buffers.Binary;
using System.Text;
using Aion.Bots.Protocol;
using Aion.Bots.Protocol.Chat;
using Aion.ChatServer.Network;
using Aion.ChatServer.Network.Packets;
using Aion.ChatServer.Network.Packets.Client;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.ChatServer.Tests.Network;

public sealed class ChatClientProtocolTests
{
	private const string ChannelIdentifier = "@\u0001public_ALL\u00011.0.AION.KOR";

	[Fact]
	public void Writers_RoundTripThroughProductionPacketFactory()
	{
		var token = Enumerable.Range(1, 48).Select(value => (byte)value).ToArray();
		var chatInitBody = new byte[sizeof(int) + token.Length];
		BinaryPrimitives.WriteInt32LittleEndian(chatInitBody, token.Length);
		token.CopyTo(chatInitBody, sizeof(int));
		var decoded = new BotServerPacketDecoder().Decode(typeof(SM_CHAT_INIT), chatInitBody);
		var protocol = ChatClientProtocol.FromChatInit(decoded);

		var chatIni = Assert.IsType<CmChatIni>(Parse(protocol.CreateChatInitFrame(), ChatClientConnectionState.Connected));
		Assert.Equal(0x40, chatIni.UnknownC);
		Assert.Equal(0, chatIni.UnknownH);

		var auth = Assert.IsType<CmPlayerAuth>(Parse(
			protocol.CreatePlayerAuthFrame(0x10203040, "account", "Daeva", ChannelIdentifier),
			ChatClientConnectionState.Connected));
		Assert.Equal(0x10203040, auth.PlayerId);
		Assert.Equal("AION", auth.GameName);
		Assert.Equal("account", auth.AccountName);
		Assert.Equal("Daeva", auth.CharacterName);
		Assert.Equal(token, auth.Token);

		var request = Assert.IsType<CmChannelRequest>(Parse(
			protocol.CreateChannelRequestFrame(13, ChannelIdentifier),
			ChatClientConnectionState.Authed));
		Assert.Equal(13, request.ChannelRequestId);
		Assert.Equal(ChannelIdentifier, request.ChannelIdentifier);

		var message = Assert.IsType<CmChannelMessage>(Parse(
			protocol.CreateChannelMessageFrame(0x55667788, "Hello"),
			ChatClientConnectionState.Authed));
		Assert.Equal(0x55667788, message.ChannelId);
		Assert.Equal("Hello", Encoding.Unicode.GetString(message.Content));
	}

	private static AbstractClientPacket? Parse(byte[] frame, ChatClientConnectionState state)
	{
		using var payload = ChatPacketFrameCodec.CreatePayloadBuffer(frame);
		return ClientPacketFactory.Create(payload, state);
	}
}
