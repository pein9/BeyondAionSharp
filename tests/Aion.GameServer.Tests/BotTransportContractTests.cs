using Aion.Bots.Protocol;
using Aion.Bots.Transport;

namespace Aion.GameServer.Tests;

public sealed class BotTransportContractTests
{
	[Fact]
	public void Contract_SeparatesFramedSendDecodedReceiveGracefulCloseAndCrash()
	{
		Assert.True(typeof(IAsyncDisposable).IsAssignableFrom(typeof(IBotTransport)));

		var send = Assert.Single(typeof(IBotTransport).GetMethods(), method => method.Name == nameof(IBotTransport.SendAsync));
		Assert.Equal(typeof(ValueTask), send.ReturnType);
		Assert.Equal([typeof(ReadOnlyMemory<byte>), typeof(CancellationToken)], send.GetParameters().Select(parameter => parameter.ParameterType));

		var receive = Assert.Single(typeof(IBotTransport).GetMethods(), method => method.Name == nameof(IBotTransport.ReceiveAsync));
		Assert.Equal(typeof(IAsyncEnumerable<DecodedBotServerPacket>), receive.ReturnType);
		Assert.Equal([typeof(CancellationToken)], receive.GetParameters().Select(parameter => parameter.ParameterType));

		AssertOperation(nameof(IBotTransport.CloseAsync));
		AssertOperation(nameof(IBotTransport.CrashAsync));
	}

	private static void AssertOperation(string name)
	{
		var operation = Assert.Single(typeof(IBotTransport).GetMethods(), method => method.Name == name);
		Assert.Equal(typeof(ValueTask), operation.ReturnType);
		Assert.Equal([typeof(CancellationToken)], operation.GetParameters().Select(parameter => parameter.ParameterType));
	}
}
