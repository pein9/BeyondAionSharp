using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Aion.Bots.Scenarios;

namespace Aion.LiveBots;

internal sealed class LiveBotDashboardState
{
	private readonly ConcurrentDictionary<string, BotDashboardSnapshot> bots = new(StringComparer.Ordinal);
	private readonly ConcurrentDictionary<string, NaturalDecision[]> decisions = new(StringComparer.Ordinal);

	public void Publish(BotDashboardSnapshot snapshot) => bots[snapshot.Bot] = snapshot;

	public void PublishDecision(string bot, NaturalDecision decision) =>
		decisions.AddOrUpdate(bot, [decision], (_, prior) => [.. prior.TakeLast(19), decision]);

	public BotDashboardSnapshot[] Snapshot() => bots.Values.OrderBy(bot => bot.Bot, StringComparer.Ordinal)
		.Select(bot => bot with { Decisions = decisions.TryGetValue(bot.Bot, out NaturalDecision[]? history) ? history : [] })
		.ToArray();
}

internal sealed record BotDashboardSnapshot(
	string Bot,
	string Account,
	string CharacterName,
	int CharacterId,
	string Connection,
	int ConnectionGeneration,
	string Step,
	string Action,
	string ActionStatus,
	string? LastPacket,
	DateTimeOffset UpdatedAt,
	int? MapId,
	int? Channel,
	BotDashboardPosition? Position,
	ushort Level,
	long Experience,
	long ExperienceNeeded,
	int CurrentHp,
	int MaxHp,
	int CurrentMp,
	int MaxMp,
	ushort CurrentDp,
	ushort MaxDp,
	bool IsDead,
	long Kinah,
	int SkillCount,
	int CooldownCount,
	BotDashboardObjectCounts Nearby,
	BotDashboardQuest[] ActiveQuests,
	int[] CompletedQuestIds,
	BotDashboardItem[] Inventory,
	string? LastSystemMessage,
	NaturalDecision[]? Decisions = null);

internal sealed record BotDashboardPosition(float X, float Y, float Z, byte Heading);
internal sealed record BotDashboardObjectCounts(int Players, int Npcs, int Gatherables, int Statics);
internal sealed record BotDashboardQuest(int QuestId, byte Status, int StepAndFlags, byte CompleteCount, int? TimerSeconds);
internal sealed record BotDashboardItem(int ObjectId, int ItemId, string Description, long Count, ushort EquipmentSlot);

/// <summary>Loopback-only, read-only HTTP view of immutable bot state snapshots.</summary>
internal sealed class LiveBotDashboardHost : IAsyncDisposable
{
	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
	};
	private static readonly IReadOnlyDictionary<string, DashboardAsset> Assets = LoadAssets();

	private readonly string run;
	private readonly string[] scenarios;
	private readonly LiveBotDashboardState state;
	private readonly DateTimeOffset startedAt = DateTimeOffset.UtcNow;
	private readonly TcpListener? listener;
	private readonly CancellationTokenSource stop = new();
	private readonly Task acceptLoop;

	public LiveBotDashboardHost(string run, IReadOnlyList<string> scenarios, LiveBotDashboardState state, int port)
		: this(run, scenarios, state, port, enabled: port != 0)
	{
	}

	private LiveBotDashboardHost(
		string run, IReadOnlyList<string> scenarios, LiveBotDashboardState state, int port, bool enabled)
	{
		this.run = run;
		this.scenarios = scenarios.ToArray();
		this.state = state;
		if (!enabled)
		{
			acceptLoop = Task.CompletedTask;
			return;
		}

		listener = new TcpListener(IPAddress.Loopback, port);
		listener.Start();
		Port = ((IPEndPoint)listener.LocalEndpoint).Port;
		acceptLoop = AcceptLoopAsync(stop.Token);
	}

	internal static LiveBotDashboardHost StartForTest(
		string run, IReadOnlyList<string> scenarios, LiveBotDashboardState state) =>
		new(run, scenarios, state, 0, enabled: true);

	public int Port { get; }
	public bool Enabled => listener != null && !stop.IsCancellationRequested;
	public Uri Url => Enabled ? new Uri($"http://127.0.0.1:{Port.ToString(CultureInfo.InvariantCulture)}/")
		: throw new InvalidOperationException("The dashboard is disabled.");

	public async ValueTask DisposeAsync()
	{
		if (listener == null)
		{
			stop.Dispose();
			return;
		}
		await stop.CancelAsync();
		listener.Stop();
		try { await acceptLoop; }
		catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
		stop.Dispose();
	}

	private async Task AcceptLoopAsync(CancellationToken token)
	{
		while (!token.IsCancellationRequested)
		{
			TcpClient client;
			try { client = await listener!.AcceptTcpClientAsync(token); }
			catch (OperationCanceledException) when (token.IsCancellationRequested) { break; }
			_ = HandleAsync(client, token);
		}
	}

	private async Task HandleAsync(TcpClient client, CancellationToken token)
	{
		using (client)
		{
			try
			{
				await using NetworkStream stream = client.GetStream();
				using var reader = new StreamReader(stream, Encoding.ASCII, detectEncodingFromByteOrderMarks: false,
					bufferSize: 1024, leaveOpen: true);
				string? requestLine = await reader.ReadLineAsync(token);
				int headerBytes = requestLine?.Length ?? 0;
				while (await reader.ReadLineAsync(token) is { Length: > 0 } header)
				{
					headerBytes += header.Length;
					if (headerBytes > 16_384)
					{
						await WriteResponseAsync(stream, 431, "text/plain; charset=utf-8", "Request headers too large."u8.ToArray(), token);
						return;
					}
				}

				string[] parts = requestLine?.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries) ?? [];
				if (parts.Length < 2 || !string.Equals(parts[0], "GET", StringComparison.Ordinal))
				{
					await WriteResponseAsync(stream, 405, "text/plain; charset=utf-8", "Method not allowed."u8.ToArray(), token);
					return;
				}

				string path = parts[1].Split('?', 2)[0];
				if (string.Equals(path, "/api/state", StringComparison.Ordinal))
				{
					BotDashboardSnapshot[] bots = state.Snapshot();
					byte[] json = JsonSerializer.SerializeToUtf8Bytes(new
					{
						schemaVersion = 1,
						run,
						scenarios,
						startedAt,
						serverTime = DateTimeOffset.UtcNow,
						bots,
					}, JsonOptions);
					await WriteResponseAsync(stream, 200, "application/json; charset=utf-8", json, token);
					return;
				}

				string assetPath = path == "/" ? "/index.html" : path;
				if (Assets.TryGetValue(assetPath, out DashboardAsset? asset))
				{
					await WriteResponseAsync(stream, 200, asset.ContentType, asset.Body, token);
					return;
				}
				await WriteResponseAsync(stream, 404, "text/plain; charset=utf-8", "Not found."u8.ToArray(), token);
			}
			catch (Exception) when (token.IsCancellationRequested) { }
			catch (IOException) { }
			catch (SocketException) { }
		}
	}

	private static async Task WriteResponseAsync(
		NetworkStream stream, int status, string contentType, byte[] body, CancellationToken token)
	{
		string reason = status switch { 200 => "OK", 404 => "Not Found", 405 => "Method Not Allowed", 431 => "Request Header Fields Too Large", _ => "Error" };
		string header = $"HTTP/1.1 {status} {reason}\r\n" +
			$"Content-Type: {contentType}\r\nContent-Length: {body.Length.ToString(CultureInfo.InvariantCulture)}\r\n" +
			"Cache-Control: no-store\r\nX-Content-Type-Options: nosniff\r\nReferrer-Policy: no-referrer\r\n" +
			"Content-Security-Policy: default-src 'self'; connect-src 'self'; style-src 'self' 'unsafe-inline'; script-src 'self'; img-src 'none'\r\n" +
			"Connection: close\r\n\r\n";
		await stream.WriteAsync(Encoding.ASCII.GetBytes(header), token);
		await stream.WriteAsync(body, token);
	}

	private static IReadOnlyDictionary<string, DashboardAsset> LoadAssets()
	{
		Assembly assembly = typeof(LiveBotDashboardHost).Assembly;
		return new Dictionary<string, DashboardAsset>(StringComparer.Ordinal)
		{
			["/index.html"] = Load(assembly, "Aion.LiveBots.Dashboard.index.html", "text/html; charset=utf-8"),
			["/dashboard.css"] = Load(assembly, "Aion.LiveBots.Dashboard.dashboard.css", "text/css; charset=utf-8"),
			["/dashboard.js"] = Load(assembly, "Aion.LiveBots.Dashboard.dashboard.js", "text/javascript; charset=utf-8"),
		};
	}

	private static DashboardAsset Load(Assembly assembly, string name, string contentType)
	{
		using Stream stream = assembly.GetManifestResourceStream(name)
			?? throw new InvalidOperationException($"Missing embedded dashboard asset {name}.");
		using var memory = new MemoryStream();
		stream.CopyTo(memory);
		return new DashboardAsset(contentType, memory.ToArray());
	}

	private sealed record DashboardAsset(string ContentType, byte[] Body);
}
