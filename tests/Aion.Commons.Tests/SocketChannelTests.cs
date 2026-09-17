using System.Net;
using System.Net.Sockets;
using Aion.Commons.Nio;
using Aion.Commons.Nio.Channels;

namespace Aion.Commons.Tests;

public sealed class SocketChannelTests
{
	[Fact]
	public async Task NonBlockingChannelReturnsZeroAndDeliversOneMegabyteToSlowReader()
	{
		using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
		using var listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
		listener.Bind(new IPEndPoint(IPAddress.Loopback, 0));
		listener.Listen(1);

		using var client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
		var connect = client.ConnectAsync(listener.LocalEndPoint!, timeout.Token);
		using var server = await listener.AcceptAsync(timeout.Token);
		await connect;

		server.SendBufferSize = 4 * 1024;
		client.ReceiveBufferSize = 4 * 1024;
		var channel = SocketChannel.Open(server);
		channel.ConfigureBlocking(false);
		Assert.Equal(0, channel.Read(ByteBuffer.Allocate(1)));

		var fillBytes = Enumerable.Repeat((byte)0xA5, 1024 * 1024).ToArray();
		var fill = ByteBuffer.Wrap(fillBytes);
		var queuedPrefixBytes = 0;
		while (queuedPrefixBytes < 128 * 1024 * 1024)
		{
			var count = channel.Write(fill);
			if (count == 0)
				break;
			queuedPrefixBytes += count;
			if (!fill.HasRemaining())
				fill.SetPosition(0);
		}
		Assert.True(queuedPrefixBytes < 128 * 1024 * 1024, "Could not fill the nonblocking socket send window.");

		var sent = new byte[1024 * 1024];
		for (var i = 0; i < sent.Length; i++)
			sent[i] = (byte)(i * 31);
		var received = new byte[sent.Length];
		var receiveTask = ReadSlowlyAsync(client, queuedPrefixBytes, received, timeout.Token);
		var source = ByteBuffer.Wrap(sent);
		while (source.HasRemaining())
		{
			if (channel.Write(source) == 0)
				await Task.Delay(1, timeout.Token);
		}
		server.Shutdown(SocketShutdown.Send);

		Assert.Equal(sent.Length, await receiveTask.WaitAsync(timeout.Token));
		Assert.Equal(sent, received);
	}

	private static async Task<int> ReadSlowlyAsync(Socket socket, int prefixBytes, byte[] destination,
		CancellationToken cancellationToken)
	{
		await Task.Delay(100, cancellationToken);
		var total = 0;
		var payloadBytes = 0;
		var buffer = new byte[8 * 1024];
		while (payloadBytes < destination.Length)
		{
			var count = await socket.ReceiveAsync(buffer, SocketFlags.None, cancellationToken);
			if (count == 0)
				break;
			var payloadStart = Math.Min(count, Math.Max(0, prefixBytes - total));
			for (var i = 0; i < payloadStart; i++)
				Assert.Equal((byte)0xA5, buffer[i]);
			var payloadCount = count - payloadStart;
			if (payloadCount > 0)
			{
				buffer.AsSpan(payloadStart, payloadCount).CopyTo(destination.AsSpan(payloadBytes));
				payloadBytes += payloadCount;
			}
			total += count;
		}
		return payloadBytes;
	}
}
