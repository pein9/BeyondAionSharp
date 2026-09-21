using System.Security.Cryptography;
using System.Text;
using Aion.ChatServer.Models;
using Aion.ChatServer.Models.Channels;
using Aion.ChatServer.Network.Handlers;
using Aion.ChatServer.Network.Packets;
using Aion.ChatServer.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.ChatServer.Tests;

public class ChatServiceTests
{
	[Theory]
	[InlineData(300000L)]
	[InlineData(60000L)]
	[InlineData(-30000L)]
	public void GagPlayer_ConvertsWireDurationToEpochExpiry(long durationMillis)
	{
		var service = CreateService();
		var client = service.RegisterPlayer(7, "PlayerOne", "Daeva", Race.Elyos, 0);
		long before = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
		service.GagPlayer(7, durationMillis);
		long after = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
		Assert.InRange(client.GagTime, before + durationMillis, after + durationMillis);
		Assert.Equal(durationMillis > 0, client.IsGagged());
	}

	[Fact]
	public void GagReplayReplacesRemainingDurationAndZeroImmediatelyClearsIt()
	{
		var service = CreateService();
		var client = service.RegisterPlayer(7, "PlayerOne", "Daeva", Race.Elyos, 0);
		service.GagPlayer(7, 300000);
		long initial = client.GagTime;
		long before = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
		service.GagPlayer(7, 60000);
		long after = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
		Assert.InRange(client.GagTime, before + 60000, after + 60000);
		Assert.True(client.GagTime < initial);
		Assert.True(client.IsGagged());
		service.GagPlayer(7, 0);
		Assert.Equal(0, client.GagTime);
		Assert.False(client.IsGagged());
	}

	[Fact]
	public void RegisterPlayer_GeneratesJavaShapedToken()
	{
		var service = CreateService();
		var accountName = "PlayerOne";

		var client = service.RegisterPlayer(7, accountName, "Daeva", Race.Elyos, accessLevel: 0);

		Assert.Equal(48, client.Token.Length);
		Assert.Equal(ExpectedJavaAccountToken(accountName), client.Token.Skip(16).ToArray());
	}

	[Fact]
	public void RegisterPlayerConnection_AttachesMatchingClient()
	{
		var service = CreateService();
		var client = service.RegisterPlayer(7, "PlayerOne", "Daeva", Race.Elyos, accessLevel: 0);
		var identifier = Encoding.Unicode.GetBytes("Daeva@\u0001public_ALL\u00011.0.AION.KOR");

		var attached = service.RegisterPlayerConnection(7, client.Token, identifier, "Daeva", "playerone", new FakeChatClientConnection());

		Assert.True(attached);
		Assert.Equal(identifier, client.Identifier);
		Assert.NotNull(client.Connection);
	}

	private static ChatService CreateService()
	{
		var channels = new ChatChannels(NullLogger<ChatChannels>.Instance);
		var broadcast = new BroadcastService(NullLogger<BroadcastService>.Instance);
		return new ChatService(channels, broadcast, NullLogger<ChatService>.Instance);
	}

	private static byte[] ExpectedJavaAccountToken(string accountName)
	{
		var bytes = Encoding.UTF8.GetBytes(accountName);
		var javaLength = Math.Min(accountName.Length, bytes.Length);
		return SHA256.HashData(bytes.AsSpan(0, javaLength));
	}

	private sealed class FakeChatClientConnection : IChatClientConnection
	{
		public Task SendPacketAsync(AbstractServerPacket packet, CancellationToken cancellationToken = default)
		{
			return Task.CompletedTask;
		}

		public Task CloseAsync(CancellationToken cancellationToken = default)
		{
			return Task.CompletedTask;
		}
	}
}
