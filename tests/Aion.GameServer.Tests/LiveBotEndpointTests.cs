using Aion.LiveBots;

namespace Aion.GameServer.Tests;

public sealed class LiveBotEndpointTests
{
	[Theory]
	[InlineData("127.0.0.1")]
	[InlineData("::1")]
	public void DefaultEndpointsShareTheRequestedHost(string host)
	{
		var options = Parse("--host", host);
		Assert.Equal(host, options.LoginEndPoint.Address.ToString());
		Assert.Equal(host, options.GameEndPoint.Address.ToString());
		Assert.Equal(host, options.ChatEndPoint.Address.ToString());
		Assert.Equal(host, options.AdminBaseUri.Host.Trim('[', ']'));
	}

	[Fact]
	public void DockerServicesUseDistinctAddressesWithoutChangingGameAdminOrWorkload()
	{
		var options = Parse("--host", "172.23.0.4", "--login-host", "172.23.0.2", "--chat-host", "172.23.0.3",
			"--login-port", "2106", "--game-port", "7777", "--chat-port", "10241", "--admin-port", "7780");
		Assert.Equal("172.23.0.2:2106", options.LoginEndPoint.ToString());
		Assert.Equal("172.23.0.4:7777", options.GameEndPoint.ToString());
		Assert.Equal("172.23.0.3:10241", options.ChatEndPoint.ToString());
		Assert.Equal("http://172.23.0.4:7780/", options.AdminBaseUri.ToString());
		Assert.Equal(Parse().Scenarios, options.Scenarios);
		Assert.Equal(Parse().StepTimeout, options.StepTimeout);
	}

	private static LiveBotOptions Parse(params string[] options) => LiveBotOptions.Parse(
		["--run", "endpoints", "--output", "run/endpoints", "--scenario", "connect", .. options]);
}
