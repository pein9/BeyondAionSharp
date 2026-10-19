using Aion.ChatServer.Configuration;
using Aion.ChatServer.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.ChatServer.Tests;

public class GameServerServiceTests
{
    [Theory]
    [InlineData("", "", false, GsAuthResponse.NotAuthed)]
    [InlineData("", "", true, GsAuthResponse.Authed)]
    [InlineData("", "wrong", true, GsAuthResponse.NotAuthed)]
    [InlineData("secret", "secret", false, GsAuthResponse.Authed)]
    [InlineData("secret", "wrong", false, GsAuthResponse.NotAuthed)]
    public void PasswordAndLoopbackRulesAreAppliedBeforeRegistration(string expected, string received, bool loopback, GsAuthResponse response)
    {
        var service = new GameServerService(new ChatServerOptions { GameServerPassword = expected }, NullLogger<GameServerService>.Instance);
        Assert.Equal(response, service.RegisterGameServer(1, received, loopback));
        Assert.Equal(response == GsAuthResponse.Authed, service.IsOnline);
    }

    [Fact]
    public void Utf8ReplacementMatchesTheJavaPasswordComparison()
    {
        var service = new GameServerService(new ChatServerOptions { GameServerPassword = "?" }, NullLogger<GameServerService>.Instance);
        Assert.Equal(GsAuthResponse.Authed, service.RegisterGameServer(1, new string((char)0xD800, 1), fromLoopback: false));
    }

	[Fact]
	public void RegisterGameServer_TracksSingleOnlineServer()
	{
		var service = new GameServerService(new ChatServerOptions { GameServerPassword = "secret" }, NullLogger<GameServerService>.Instance);

		Assert.Equal(GsAuthResponse.Authed, service.RegisterGameServer(1, "secret", fromLoopback: true));
		Assert.True(service.IsOnline);
		Assert.Equal((byte)1, service.GameServerId);
		Assert.Equal(GsAuthResponse.AlreadyRegistered, service.RegisterGameServer(2, "secret", fromLoopback: true));

		service.SetOffline();

		Assert.False(service.IsOnline);
		Assert.Null(service.GameServerId);
	}
}
