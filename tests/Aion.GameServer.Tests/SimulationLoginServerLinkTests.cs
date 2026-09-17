using System.Net;
using System.Runtime.CompilerServices;
using Aion.GameServer.Configuration;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.LoginServer;
using Aion.GameServer.Network.LoginServer.ServerPackets;
using Microsoft.Extensions.Logging.Abstractions;
using GameChatServer = Aion.GameServer.Network.ChatServer.ChatServer;
using GameLoginServer = Aion.GameServer.Network.LoginServer.LoginServer;

namespace Aion.GameServer.Tests;

public sealed class SimulationLoginServerLinkTests
{
	[Fact]
	public void AccountAuthResponsesAreSynchronousAndKeepEachBotsAccessLevel()
	{
		var responses = new List<SimulationAccountAuthenticationResponse>();
		var accounts = new Dictionary<int, SimulationLoginAccount>
		{
			[101] = new("sim-one", 1),
			[202] = new("sim-two", 7, Membership: 10),
		};
		var link = new SimulationLoginServerLink(accounts, responses.Add, gameServerCount: 2);

		Assert.True(link.SendPacket(new SmAccountAuth(101, 11, 12, 13)));
		Assert.Single(responses);
		Assert.True(link.SendPacket(new SmAccountAuth(202, 21, 22, 23)));
		Assert.True(link.SendPacket(new SmAccountAuth(303, 31, 32, 33)));

		Assert.True(link.IsAuthed);
		Assert.Equal(2, link.GetGameServerCount());
		Assert.Collection(
			responses,
			first =>
			{
				Assert.Equal(101, first.AccountId);
				Assert.Equal("sim-one", first.AccountName);
				Assert.Equal(1, first.AccessLevel);
				Assert.True(first.Accepted);
			},
			second =>
			{
				Assert.Equal(202, second.AccountId);
				Assert.Equal("sim-two", second.AccountName);
				Assert.Equal(7, second.AccessLevel);
				Assert.Equal(10, second.Membership);
				Assert.True(second.Accepted);
			},
			missing =>
			{
				Assert.Equal(303, missing.AccountId);
				Assert.False(missing.Accepted);
			});
		Assert.Equal(3, link.SentPackets.Count);
	}

	[Fact]
	public async Task LoginFacadeDelegatesToSimulationLinkAndChatSingletonIsConstructed()
	{
		var responses = new List<SimulationAccountAuthenticationResponse>();
		AionConnection? disconnected = null;
		SimulationLoginServerLink? simulationLink = null;
		var options = CreateOptions();
		await using var loginServer = new GameLoginServer(
			NullLogger<GameLoginServer>.Instance,
			options,
			characterSelectionRepository: null,
			_ => simulationLink = new SimulationLoginServerLink(
				new Dictionary<int, SimulationLoginAccount> { [404] = new("sim-four", 4) },
				responses.Add,
				connection => disconnected = connection,
				gameServerCount: 3));
		await using var chatServer = new GameChatServer(NullLogger<GameChatServer>.Instance, options);
		var connection = (AionConnection)RuntimeHelpers.GetUninitializedObject(typeof(AionConnection));

		Assert.True(loginServer.IsAuthed);
		Assert.Equal(3, loginServer.GetGameServerCount());
		Assert.True(loginServer.SendPacket(new SmAccountAuth(404, 1, 2, 3)));
		loginServer.OnDisconnect(connection);

		Assert.NotNull(simulationLink);
		Assert.Single(responses);
		Assert.Equal(4, responses[0].AccessLevel);
		Assert.Same(connection, disconnected);
		Assert.Same(loginServer, GameLoginServer.GetInstance());
		Assert.Same(chatServer, GameChatServer.GetInstance());
		Assert.Empty(chatServer.GetPublicIP());
	}

	[Theory]
	[InlineData("00-11-22-AA-BB-CC", true)]
	[InlineData("00-11-22-aa-bb-cc", false)]
	[InlineData("00:11:22:AA:BB:CC", false)]
	[InlineData("", false)]
	public void MacAddressUsesTheJavaProtocolShape(string value, bool expected)
	{
		Assert.Equal(expected, GameLoginServer.IsValidMacAddress(value));
	}

	private static GameServerOptions CreateOptions()
	{
		return new GameServerOptions
		{
			Network = new GameServerNetworkOptions
			{
				LoginEndPoint = new IPEndPoint(IPAddress.Loopback, 9014),
				ChatEndPoint = new IPEndPoint(IPAddress.Loopback, 9021),
				ClientConnectEndPoint = new IPEndPoint(IPAddress.Loopback, 7777),
				GameServerId = 1,
				LoginPassword = "1234",
				ChatPassword = "1234",
				MaxOnlinePlayers = 100,
			},
		};
	}
}
