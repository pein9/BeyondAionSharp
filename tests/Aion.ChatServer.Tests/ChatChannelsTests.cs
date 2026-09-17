using Aion.ChatServer.Models;
using Aion.ChatServer.Models.Channels;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.ChatServer.Tests;

public class ChatChannelsTests
{
	[Fact]
	public void GetOrCreate_ParsesJavaIdentifierAndReusesMatchingRegionChannel()
	{
		var channels = new ChatChannels(NullLogger<ChatChannels>.Instance);
		var client = new ChatClient(1, new byte[48], "account", "Daeva", Race.Elyos, accessLevel: 0);
		var identifier = "@\u0001public_ALL\u00011.0.AION.KOR";

		var first = channels.GetOrCreate(client, identifier);
		var second = channels.GetOrCreate(client, identifier);

		var region = Assert.IsType<RegionChannel>(first);
		Assert.Same(first, second);
		Assert.Equal(ChannelType.Region, region.ChannelType);
		Assert.Equal(Race.Elyos, region.Race);
		Assert.Equal("ALL", region.MapIdentifier);
	}

	[Fact]
	public async Task GetOrCreate_ConcurrentRequestsReuseOneRegionChannel()
	{
		var channels = new ChatChannels(NullLogger<ChatChannels>.Instance);
		const string identifier = "@\u0001public_ALL\u00011.0.AION.KOR";
		using var start = new ManualResetEventSlim();
		var requests = Enumerable.Range(1, 32)
			.Select(id => Task.Run(() =>
			{
				var client = new ChatClient(id, new byte[48], $"account{id}", $"Daeva{id}", Race.Elyos, accessLevel: 0);
				start.Wait();
				return channels.GetOrCreate(client, identifier);
			}))
			.ToArray();

		start.Set();
		var results = await Task.WhenAll(requests);

		var channel = Assert.IsType<RegionChannel>(results[0]);
		Assert.All(results, result => Assert.Same(channel, result));
	}

	[Fact]
	public void GetOrCreate_RejectsOtherRaceForNormalAccessClient()
	{
		var channels = new ChatChannels(NullLogger<ChatChannels>.Instance);
		var client = new ChatClient(1, new byte[48], "account", "Daeva", Race.Elyos, accessLevel: 0);

		var channel = channels.GetOrCreate(client, "@\u0001public_ALL\u00011.1.AION.KOR");

		Assert.Null(channel);
	}

	[Theory]
	[InlineData("Gladiator")]
	[InlineData("Gladyatör")]
	[InlineData("Гладиатор")]
	[InlineData("剑星")]
	[InlineData("검성")]
	public void GetOrCreate_ReusesJobChannelForJavaLocalizedAliases(string alias)
	{
		var channels = new ChatChannels(NullLogger<ChatChannels>.Instance);
		var client = new ChatClient(1, new byte[48], "account", "Daeva", Race.Elyos, accessLevel: 0);

		var first = channels.GetOrCreate(client, "@\u0001job_Gladiator\u00011.0.AION.KOR");
		var aliasChannel = channels.GetOrCreate(client, $"@\u0001job_{alias}\u00011.0.AION.KOR");

		var job = Assert.IsType<JobChannel>(first);
		Assert.True(job.HasAliases);
		Assert.Same(first, aliasChannel);
	}
}
