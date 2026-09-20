using System.Net;
using System.Net.Sockets;
using System.Text;
using Aion.Bots.Tracing;
using Aion.LiveBots;

namespace Aion.GameServer.Tests;

public sealed class LiveAdminConnectionReuseTests
{
	[Theory]
	[InlineData(false, 1)]
	[InlineData(true, 2)]
	public async Task OracleReadsReuseActiveConnectionsButRetireIdleOnes(bool idleBetweenReads, int expectedConnections)
	{
		string directory = Path.Combine(Path.GetTempPath(), "aion-admin-reuse-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(directory);
		try
		{
			using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
			using var listener = new TcpListener(IPAddress.Loopback, 0);
			listener.Start();
			var endpoint = (IPEndPoint)listener.LocalEndpoint;
			LiveBotOptions options = LiveBotOptions.Parse([
				"--run", "admin-reuse", "--output", directory, "--scenario", "connect", "--git-sha", "test",
				"--admin-port", endpoint.Port.ToString(), "--admin-token", "test-only-token"]);
			await using var problems = new LiveBotProblemWriter(Path.Combine(directory, "problems.jsonl"));
			using var trace = new BotActionTraceWriter(new MemoryStream(), "admin-reuse", "b01", "test-account");
			var session = new LiveBotSession(options, problems, trace, "b01", "test-account", "Testplayer");
			bool disposed = false;
			var idleConnectionClosed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
			Task<int> server = ServeAsync(listener, 8, deadline.Token, () => idleConnectionClosed.TrySetResult());
			try
			{
				for (int i = 0; i < 8; i++)
				{
					// Pool cleanup is periodic. Observe the actual client-initiated EOF,
					// not an assumed cleanup instant; the server never closes an idle peer.
					if (i == 4 && idleBetweenReads) await idleConnectionClosed.Task.WaitAsync(deadline.Token);
					Assert.Equal(7, await session.ReadCubeFreeSlotsAsync(deadline.Token));
				}
				Assert.Equal(expectedConnections, await server);
				await session.DisposeAsync();
				disposed = true;
				await Assert.ThrowsAsync<ObjectDisposedException>(() => session.ReadCubeFreeSlotsAsync(CancellationToken.None));
			}
			finally
			{
				await deadline.CancelAsync();
				if (!disposed) await session.DisposeAsync();
				try { await server; } catch (OperationCanceledException) when (deadline.IsCancellationRequested) { }
			}
		}
		finally { Directory.Delete(directory, recursive: true); } // Exact GUID-named directory owned by this test.
	}

	private static async Task<int> ServeAsync(TcpListener listener, int requests, CancellationToken token, Action? connectionClosed = null)
	{
		int connections = 0, served = 0;
		const string body = "{\"inventory\":{\"cubeFreeSlots\":7}}";
		byte[] response = Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Length: {body.Length}\r\nContent-Type: application/json\r\n\r\n{body}");
		while (served < requests)
		{
			using TcpClient peer = await listener.AcceptTcpClientAsync(token);
			connections++;
			using NetworkStream stream = peer.GetStream();
			using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
			while (served < requests)
			{
				string? line = await reader.ReadLineAsync(token);
				if (line == null)
				{
					connectionClosed?.Invoke();
					break;
				}
				Assert.Equal("GET /admin/player-storage-state?recipientCharacterId=0 HTTP/1.1", line);
				bool authorized = false;
				while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync(token)))
					authorized |= line == "X-Admin-Token: test-only-token";
				Assert.True(authorized);
				await stream.WriteAsync(response, token);
				served++;
			}
		}
		return connections;
	}
}
