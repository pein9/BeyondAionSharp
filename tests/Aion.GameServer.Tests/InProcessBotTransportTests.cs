using System.Runtime.CompilerServices;
using Aion.Bots.Protocol;
using Aion.Bots.Transport;
using Aion.GameServer.Model.Account;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Tests;

public sealed class InProcessBotTransportTests
{
	[Fact]
	public async Task InitializationSerializesSmKeyAndEncryptedCmPingReturnsDecodedPong()
	{
		await using var transport = new InProcessBotTransport();
		await using var received = transport.ReceiveAsync().GetAsyncEnumerator();

		Assert.True(await received.MoveNextAsync());
		Assert.Equal(typeof(SM_KEY), received.Current.PacketType);
		Assert.True(transport.Codec.HasKey);

		transport.ServerConnection.SetState(AionConnection.State.AUTHED);
		byte[] ping = GameClientPackets.Ping().Encode(transport.Codec, AionConnection.State.AUTHED);
		await transport.SendAsync(ping);

		Assert.True(await received.MoveNextAsync());
		Assert.Equal(typeof(SM_PONG), received.Current.PacketType);
		Assert.Equal(0, AionConnection.PacketQueueDepth);
	}

	[Fact]
	public async Task ClockAdvanceDrainsThroughSerializerBeforePublishingPacket()
	{
		InProcessBotTransport transport = null!;
		var player = (Player)RuntimeHelpers.GetUninitializedObject(typeof(Player));
		transport = new InProcessBotTransport(_ =>
			transport.ServerConnection.SendPacket(new SM_PLAY_MOVIE(true, 101, 202, 303, true)));
		await using (transport)
		await using (var received = transport.ReceiveAsync().GetAsyncEnumerator())
		{
			Assert.True(await received.MoveNextAsync()); // SM_KEY
			transport.ServerConnection.SetAccount(new Account(1));
			Assert.True(transport.ServerConnection.SetActivePlayer(player));

			await transport.AdvanceAsync(TimeSpan.FromSeconds(1));

			Assert.True(await received.MoveNextAsync());
			Assert.Equal(typeof(SM_PLAY_MOVIE), received.Current.PacketType);
			Assert.True(player.IsInCustomState(CustomPlayerState.WATCHING_CUTSCENE));
			transport.ServerConnection.SetActivePlayer(null!);
		}
	}

	[Fact]
	public async Task StrictSendRejectsUnreadClientBodyBytes()
	{
		await using var transport = new InProcessBotTransport();
		transport.ServerConnection.SetState(AionConnection.State.AUTHED);
		byte[] frame = transport.Codec.EncodeClientFrame(
			typeof(Aion.GameServer.Network.Aion.ClientPackets.CM_PING),
			AionConnection.State.AUTHED,
			[0, 0, 0xCC]);

		var error = await Assert.ThrowsAsync<InvalidDataException>(async () => await transport.SendAsync(frame));

		Assert.Contains("1 unread", error.Message, StringComparison.Ordinal);
		Assert.True(transport.ServerConnection.IsClosed());
	}

	[Fact]
	public async Task StrictSendRejectsBadFrameLengthBeforeServerProcessing()
	{
		await using var transport = new InProcessBotTransport();
		byte[] frame = new byte[7];
		frame[0] = 8;

		var error = await Assert.ThrowsAsync<InvalidDataException>(async () => await transport.SendAsync(frame));

		Assert.Contains("length mismatch", error.Message, StringComparison.OrdinalIgnoreCase);
	}

	[Fact]
	public async Task StrictSendRejectsCorruptEncryptedHeader()
	{
		await using var transport = new InProcessBotTransport();
		transport.ServerConnection.SetState(AionConnection.State.AUTHED);
		byte[] frame = GameClientPackets.Ping().Encode(transport.Codec, AionConnection.State.AUTHED);
		frame[4] ^= 0x7F;

		var error = await Assert.ThrowsAsync<InvalidDataException>(async () => await transport.SendAsync(frame));

		Assert.Contains("header", error.Message, StringComparison.OrdinalIgnoreCase);
		Assert.True(transport.ServerConnection.IsClosed());
	}

	[Fact]
	public async Task CrashDropsQueuedServerPacketsWithoutDrainingThem()
	{
		await using var transport = new InProcessBotTransport();
		await using var received = transport.ReceiveAsync().GetAsyncEnumerator();
		Assert.True(await received.MoveNextAsync()); // SM_KEY
		transport.ServerConnection.SendPacket(new SM_PONG());

		await transport.CrashAsync();

		Assert.False(await received.MoveNextAsync());
		Assert.True(transport.ServerConnection.IsClosed());
	}
}
