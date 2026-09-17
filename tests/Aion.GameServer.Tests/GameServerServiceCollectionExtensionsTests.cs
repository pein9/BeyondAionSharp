using Aion.Commons.Configuration;
using Aion.Commons.Diagnostics;
using Aion.GameServer.Configuration;
using Aion.GameServer.Configs;
using Aion.GameServer.Data;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Admin;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Aion.GameServer.Tests;

public sealed class GameServerServiceCollectionExtensionsTests
{
	[Fact]
	public void AddGameServerRegistersTheReusableProductionGraphWithoutInitializingDatabaseOptions()
	{
		var gameOptions = new GameServerOptions();
		// DatabaseFactory.Initialize would reject this port. Registration must only retain the options for the host
		// lifecycle (or the SIM fixture) to initialize later.
		var databaseOptions = new DatabaseOptions { Port = -1 };
		var configLoadOptions = new GameServerConfigLoadOptions { ConfigRoot = "isolated-config" };
		var services = new ServiceCollection();

		IServiceCollection returned = services.AddGameServer(gameOptions, databaseOptions, configLoadOptions);

		Assert.Same(services, returned);
		Assert.Contains(services, descriptor =>
			descriptor.ServiceType == typeof(GameServerOptions)
			&& ReferenceEquals(descriptor.ImplementationInstance, gameOptions));
		Assert.Contains(services, descriptor =>
			descriptor.ServiceType == typeof(DatabaseOptions)
			&& ReferenceEquals(descriptor.ImplementationInstance, databaseOptions));
		Assert.Contains(services, descriptor =>
			descriptor.ServiceType == typeof(GameServerConfigLoadOptions)
			&& ReferenceEquals(descriptor.ImplementationInstance, configLoadOptions));
		Assert.Contains(services, descriptor =>
			descriptor.ServiceType == typeof(IStaticDataLoader)
			&& descriptor.ImplementationType == typeof(StaticDataService));
		Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(GameServerBootstrapService));

		ServiceDescriptor[] hosted = services.Where(descriptor => descriptor.ServiceType == typeof(IHostedService)).ToArray();
		Assert.Equal(5, hosted.Length);
		Assert.Contains(hosted, descriptor => descriptor.ImplementationFactory != null);
		Assert.Contains(hosted, descriptor => descriptor.ImplementationType == typeof(GameServerHostedService));
		Assert.Contains(hosted, descriptor => descriptor.ImplementationType == typeof(OutboundLinkHostedService));
		Assert.Contains(hosted, descriptor => descriptor.ImplementationType == typeof(ServerHeartbeatService));
		Assert.Contains(hosted, descriptor => descriptor.ImplementationType == typeof(AdminHttpService));
	}
}
