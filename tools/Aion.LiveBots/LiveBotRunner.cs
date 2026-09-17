using System.Net.Sockets;
using System.Text.Json;
using Aion.Bots.Protocol;
using Aion.Bots.Reflexes;
using Aion.Bots.Tracing;
using Aion.Bots.Transport;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.LiveBots;

public static class LiveBotRunner
{
	public static async Task<int> RunAsync(LiveBotOptions options, CancellationToken cancellationToken = default)
	{
		Directory.CreateDirectory(options.OutputDirectory);
		Directory.CreateDirectory(Path.Combine(options.OutputDirectory, "bots"));
		await WriteRunMetadataAsync(options, cancellationToken);
		await using var problems = new LiveBotProblemWriter(Path.Combine(options.OutputDirectory, "bot.problems.jsonl"));
		var tasks = Enumerable.Range(1, options.BotCount)
			.Select(index => RunBotAsync(options, problems, index, cancellationToken))
			.ToArray();
		var results = await Task.WhenAll(tasks);
		var failed = results.Count(result => !result);
		Console.WriteLine($"LIVE bots completed: {results.Length - failed} passed, {failed} failed.");
		return failed == 0 ? 0 : 1;
	}

	private static async Task<bool> RunBotAsync(
		LiveBotOptions options,
		LiveBotProblemWriter problems,
		int index,
		CancellationToken cancellationToken)
	{
		var bot = $"b{index:D2}";
		var account = $"{bot}r{DateTimeOffset.Now:MMdd}";
		var tracePath = Path.Combine(options.OutputDirectory, "bots", $"{bot}.trace.jsonl");
		using var trace = BotActionTraceWriter.Open(tracePath, options.Run, bot, account);
		await using var session = new LiveBotSession(options, problems, trace, bot, account);
		var stepNumber = 0;

		try
		{
			foreach (var scenario in options.Scenarios)
			{
				var step = $"s{++stepNumber:D2}";
				trace.WriteAction(step, "scenario:start", new Dictionary<string, object?> { ["scenario"] = scenario });
				await RunStepAsync(options, problems, trace, bot, account, step, "connect", cancellationToken,
					async token => await session.ConnectAndReadKeyAsync(step, token));

				step = $"s{++stepNumber:D2}";
				await RunStepAsync(options, problems, trace, bot, account, step, "close", cancellationToken,
					async token => await session.CloseAsync(step, token));
				trace.WriteAction(step, "scenario:complete", new Dictionary<string, object?> { ["scenario"] = scenario });
			}
			return true;
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			throw;
		}
		catch (Exception ex)
		{
			Console.Error.WriteLine($"{bot} failed: {ex}");
			return false;
		}
	}

	private static async Task RunStepAsync(
		LiveBotOptions options,
		LiveBotProblemWriter problems,
		BotActionTraceWriter trace,
		string bot,
		string account,
		string step,
		string action,
		CancellationToken cancellationToken,
		Func<CancellationToken, Task> operation)
	{
		trace.WriteAction(step, action);
		using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		timeout.CancelAfter(options.StepTimeout);
		try
		{
			await operation(timeout.Token);
		}
		catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested && timeout.IsCancellationRequested)
		{
			await problems.WriteAsync(options.Run, bot, account, step, "timeout",
				$"Step '{action}' exceeded {options.StepTimeout.TotalSeconds:n0} seconds.", ex);
			throw new LiveBotFailureException($"{bot} {step} '{action}' timed out.", ex);
		}
		catch (TimeoutException ex)
		{
			await problems.WriteAsync(options.Run, bot, account, step, "timeout", ex.Message, ex);
			throw new LiveBotFailureException($"{bot} {step} '{action}' timed out.", ex);
		}
		catch (LiveBotFailureException)
		{
			throw;
		}
		catch (Exception ex)
		{
			await problems.WriteAsync(options.Run, bot, account, step, "step-failure", ex.Message, ex);
			throw;
		}
	}

	private static async Task WriteRunMetadataAsync(LiveBotOptions options, CancellationToken cancellationToken)
	{
		var metadata = new
		{
			run = options.Run,
			gitSha = options.GitSha,
			seed = options.Seed,
			virtualEpoch = (string?)null,
			timeZone = options.TimeZone,
			configProfile = options.Profile,
			scenarios = options.Scenarios,
			bots = options.BotCount,
			gameEndPoint = options.GameEndPoint.ToString(),
		};
		await using var output = File.Create(Path.Combine(options.OutputDirectory, "bots-run.json"));
		await JsonSerializer.SerializeAsync(output, metadata, new JsonSerializerOptions { WriteIndented = true }, cancellationToken);
	}
}

internal sealed class LiveBotSession : IAsyncDisposable
{
	private readonly LiveBotOptions options;
	private readonly LiveBotProblemWriter problems;
	private readonly BotActionTraceWriter trace;
	private readonly string bot;
	private readonly string account;
	private readonly SemaphoreSlim sendLock = new(1, 1);
	private TcpBotTransport? transport;
	private IAsyncEnumerator<DecodedBotServerPacket>? packets;
	private CancellationTokenSource? connectionLifetime;
	private Task? pingTask;
	private string currentStep = "startup";
	private AionConnection.State state = AionConnection.State.CONNECTED;
	private bool quitExpected;

	public LiveBotSession(
		LiveBotOptions options,
		LiveBotProblemWriter problems,
		BotActionTraceWriter trace,
		string bot,
		string account)
	{
		this.options = options;
		this.problems = problems;
		this.trace = trace;
		this.bot = bot;
		this.account = account;
	}

	public async Task ConnectAndReadKeyAsync(string step, CancellationToken cancellationToken)
	{
		currentStep = step;
		await OpenConnectionAsync(cancellationToken);
		var packet = await ReadNextAsync(cancellationToken);
		if (packet.PacketType != typeof(SM_KEY))
			throw new InvalidDataException($"Expected SM_KEY, received {packet.PacketType.Name}.");
	}

	public async Task CloseAsync(string step, CancellationToken cancellationToken)
	{
		currentStep = step;
		quitExpected = true;
		await CloseConnectionAsync(cancellationToken);
		quitExpected = false;
	}

	public void SetGameState(AionConnection.State newState)
	{
		state = newState;
	}

	public async ValueTask DisposeAsync()
	{
		quitExpected = true;
		await CloseConnectionAsync(CancellationToken.None);
		sendLock.Dispose();
	}

	private async Task<DecodedBotServerPacket> ReadNextAsync(CancellationToken cancellationToken)
	{
		try
		{
			if (packets == null || !await packets.MoveNextAsync().AsTask().WaitAsync(cancellationToken))
				throw new EndOfStreamException("Game transport ended before the expected packet.");
			var packet = packets.Current;
			trace.WriteReceived(currentStep, packet);
			if (packet.PacketType == typeof(SM_QUIT_RESPONSE) && !quitExpected)
			{
				await problems.WriteAsync(options.Run, bot, account, currentStep, "unexpected-quit-response",
					"Received SM_QUIT_RESPONSE before the scenario requested quit.");
				throw new LiveBotFailureException("Unexpected SM_QUIT_RESPONSE.");
			}
			return packet;
		}
		catch (Exception ex) when (ex is EndOfStreamException or IOException or SocketException)
		{
			await AttemptReconnectAsync(ex, cancellationToken);
			throw new LiveBotFailureException("Game connection ended unexpectedly.", ex);
		}
	}

	private async Task AttemptReconnectAsync(Exception failure, CancellationToken cancellationToken)
	{
		await problems.WriteAsync(options.Run, bot, account, currentStep, "unexpected-disconnect", failure.Message, failure);
		try
		{
			await CloseConnectionAsync(CancellationToken.None);
			await OpenConnectionAsync(cancellationToken);
			await problems.WriteAsync(options.Run, bot, account, currentStep, "automatic-reconnect",
				"Automatically reconnected after an unexpected disconnect; the run still fails.");
		}
		catch (Exception reconnectFailure)
		{
			await problems.WriteAsync(options.Run, bot, account, currentStep, "automatic-reconnect",
				"Automatic reconnect failed; the run still fails.", reconnectFailure);
		}
	}

	private async Task OpenConnectionAsync(CancellationToken cancellationToken)
	{
		if (transport != null)
			throw new InvalidOperationException("The bot already has an open game connection.");
		using var connectTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		connectTimeout.CancelAfter(options.ConnectTimeout);
		try
		{
			transport = await TcpBotTransport.ConnectAsync(options.GameEndPoint, connectTimeout.Token);
		}
		catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested && connectTimeout.IsCancellationRequested)
		{
			throw new TimeoutException(
				$"Connecting to {options.GameEndPoint} exceeded {options.ConnectTimeout.TotalSeconds:n0} seconds.", ex);
		}
		connectionLifetime = new CancellationTokenSource();
		packets = transport.ReceiveAsync(connectionLifetime.Token).GetAsyncEnumerator(connectionLifetime.Token);
		pingTask = RunPingLoopAsync(connectionLifetime.Token);
	}

	private async Task CloseConnectionAsync(CancellationToken cancellationToken)
	{
		if (transport == null && connectionLifetime == null)
			return;
		if (transport != null)
			await transport.CloseAsync(cancellationToken);
		if (connectionLifetime != null)
			await connectionLifetime.CancelAsync();
		if (pingTask != null)
		{
			try { await pingTask; }
			catch (OperationCanceledException) when (connectionLifetime?.IsCancellationRequested == true) { }
		}
		if (packets != null)
			await packets.DisposeAsync();
		if (transport != null)
			await transport.DisposeAsync();
		connectionLifetime?.Dispose();
		connectionLifetime = null;
		packets = null;
		pingTask = null;
		transport = null;
		state = AionConnection.State.CONNECTED;
	}

	private async Task RunPingLoopAsync(CancellationToken cancellationToken)
	{
		var scheduler = new LiveBotPingScheduler();
		while (!cancellationToken.IsCancellationRequested)
		{
			var delay = scheduler.NextDueAt - DateTimeOffset.UtcNow;
			if (delay > TimeSpan.Zero)
				await Task.Delay(delay, cancellationToken);
			var ping = scheduler.Poll();
			if (ping == null || state != AionConnection.State.IN_GAME || transport == null)
				continue;

			await sendLock.WaitAsync(cancellationToken);
			try
			{
				trace.WriteSent(currentStep, ping);
				await transport.SendAsync(transport.Codec.EncodeClientFrame(ping, state), cancellationToken);
			}
			finally
			{
				sendLock.Release();
			}
		}
	}
}

internal sealed class LiveBotFailureException : Exception
{
	public LiveBotFailureException(string message, Exception? innerException = null)
		: base(message, innerException)
	{
	}
}
