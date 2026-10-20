using System.Runtime.CompilerServices;
using Aion.Bots.Protocol;
using Aion.Bots.Transport;
using Aion.GameServer.Model.Account;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.TestKit;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Tests;

public sealed class InProcessBotTransportTests
{
	[Fact]
	public async Task LongAdvancePumpsBothTheActiveAndParkedClientsAtTheirPacketTimes()
	{
		await using var clock = new VirtualThreadPool(strict: true);
		SystemClock.UseSource(() => clock.NowMillis);
		try
		{
			await using var active = new InProcessBotTransport(clock.Advance, registerClockPump: clock.RegisterAdvancePump);
			await using var parked = new InProcessBotTransport(
				waitForTurn: (_, _) => throw new InvalidOperationException("The parked client must not need a turn to receive."),
				registerClockPump: clock.RegisterAdvancePump);
			active.ServerConnection.SetState(AionConnection.State.AUTHED);
			parked.ServerConnection.SetState(AionConnection.State.AUTHED);
			await using var activePackets = active.ReceiveAsync().GetAsyncEnumerator();
			await using var parkedPackets = parked.ReceiveAsync().GetAsyncEnumerator();
			Assert.True(await activePackets.MoveNextAsync()); // SM_KEY
			Assert.True(await parkedPackets.MoveNextAsync());
			clock.ScheduleAtFixedRateTask(_ =>
			{
				active.ServerConnection.SendPacket(new SM_PONG());
				parked.ServerConnection.SendPacket(new SM_PONG());
				return ValueTask.CompletedTask;
			}, TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(20));

			await active.AdvanceAsync(TimeSpan.FromMinutes(3));

			Assert.False(active.ServerConnection.IsClosed());
			Assert.False(parked.ServerConnection.IsClosed());
			Assert.Equal(9, active.QueuedServerPackets);
			Assert.Equal(9, parked.QueuedServerPackets);
			for (int index = 1; index <= 9; index++)
			{
				Assert.True(await activePackets.MoveNextAsync());
				Assert.True(await parkedPackets.MoveNextAsync());
				Assert.Equal(typeof(SM_PONG), activePackets.Current.PacketType);
				Assert.Equal(typeof(SM_PONG), parkedPackets.Current.PacketType);
				Assert.Equal(DateTimeOffset.UnixEpoch.AddSeconds(index * 20), activePackets.Current.ReceivedAt);
				Assert.Equal(activePackets.Current.ReceivedAt, parkedPackets.Current.ReceivedAt);
			}
		}
		finally { SystemClock.UseSystemClock(); }
	}

	[Fact]
	public async Task AnUndrainedClientStillTriggersTheUnmodifiedSendStallGuard()
	{
		await using var clock = new VirtualThreadPool(strict: true);
		SystemClock.UseSource(() => clock.NowMillis);
		try
		{
			await using var transport = new InProcessBotTransport();
			transport.ServerConnection.SetState(AionConnection.State.AUTHED);
			clock.Schedule(() => transport.ServerConnection.SendPacket(new SM_PONG()), 1_000);
			clock.Schedule(() => transport.ServerConnection.SendPacket(new SM_PONG()), 62_000);

			clock.Advance(TimeSpan.FromSeconds(62));

			Assert.True(transport.ServerConnection.IsClosed());
		}
		finally { SystemClock.UseSystemClock(); }
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task ClosingOrCrashingUnregistersTheClockPump(bool crash)
	{
		await using var clock = new VirtualThreadPool(strict: true);
		await using var transport = new InProcessBotTransport(registerClockPump: clock.RegisterAdvancePump);
		if (crash) await transport.CrashAsync();
		else await transport.CloseAsync();

		clock.Advance(TimeSpan.FromMinutes(2));

		Assert.True(transport.ServerConnection.IsClosed());
		Assert.Empty(clock.Faults);
	}

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
