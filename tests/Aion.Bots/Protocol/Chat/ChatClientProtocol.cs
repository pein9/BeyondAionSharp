using Aion.ChatServer.Network;
using Aion.ChatServer.Network.Packets;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.Bots.Protocol.Chat;

/// <summary>Client-side chat packet writers bound to the token received in game-server SM_CHAT_INIT.</summary>
public sealed class ChatClientProtocol
{
	private readonly byte[] token;

	private ChatClientProtocol(byte[] token)
	{
		if (token.Length == 0)
			throw new ArgumentException("The SM_CHAT_INIT token cannot be empty.", nameof(token));
		this.token = token;
	}

	public static ChatClientProtocol FromChatInit(DecodedBotServerPacket packet)
	{
		if (packet.PacketType != typeof(SM_CHAT_INIT))
			throw new ArgumentException($"Expected {nameof(SM_CHAT_INIT)}, received {packet.PacketType.Name}.", nameof(packet));
		if (!packet.Fields.TryGetValue("token", out var tokenField) || tokenField is not byte[] token)
			throw new InvalidDataException("Decoded SM_CHAT_INIT does not contain a byte-array token.");
		return new ChatClientProtocol(token.ToArray());
	}

	public byte[] CreateChatInitFrame()
	{
		var writer = new PacketBodyWriter();
		writer.C(ClientPacketFactory.CmChatIni);
		writer.C(0x40);
		writer.H(0);
		writer.D(0);
		writer.D(0);
		writer.D(0);
		return ChatPacketFrameCodec.CreateFrame(writer.ToArray());
	}

	public byte[] CreatePlayerAuthFrame(
		int playerId,
		string accountName,
		string characterName,
		string channelIdentifier)
	{
		if (!channelIdentifier.StartsWith('@'))
			throw new ArgumentException("The chat channel identifier must begin with '@'.", nameof(channelIdentifier));

		var writer = new PacketBodyWriter();
		writer.C(ClientPacketFactory.CmPlayerAuth);
		writer.UH('@');
		writer.C(0);
		writer.D(1);
		WriteUtf16Length(writer, "AION");
		writer.D(27);
		writer.D(1);
		writer.D(0);
		writer.D(playerId);
		writer.D(0);
		writer.D(0);
		writer.D(0);
		WriteUtf16Length(writer, characterName + channelIdentifier);
		WriteUtf16Length(writer, accountName);
		writer.UH(token.Length);
		writer.B(token);
		return ChatPacketFrameCodec.CreateFrame(writer.ToArray());
	}

	public byte[] CreateChannelRequestFrame(int requestId, string channelIdentifier)
	{
		var writer = new PacketBodyWriter();
		writer.C(ClientPacketFactory.CmChannelRequest);
		writer.C(0x40);
		writer.H(0);
		writer.D(requestId);
		writer.B(new byte[16]);
		WriteUtf16Length(writer, channelIdentifier);
		writer.D(0);
		return ChatPacketFrameCodec.CreateFrame(writer.ToArray());
	}

	public byte[] CreateChannelMessageFrame(int channelId, string message)
	{
		var writer = new PacketBodyWriter();
		writer.C(ClientPacketFactory.CmChannelMessage);
		writer.H(0);
		writer.C(0);
		writer.D(0);
		writer.D(0);
		writer.D(0);
		writer.D(0);
		writer.D(channelId);
		writer.C(0);
		WriteUtf16Length(writer, message);
		return ChatPacketFrameCodec.CreateFrame(writer.ToArray());
	}

	private static void WriteUtf16Length(PacketBodyWriter writer, string value)
	{
		writer.UH(value.Length);
		foreach (var character in value)
			writer.UH(character);
	}
}
