using System.Globalization;
using System.Text;
using System.Text.Json;
using Aion.Bots.Protocol;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.Bots.Tracing;

/// <summary>Writes one immediately visible JSONL record for each bot action and packet.</summary>
public sealed class BotActionTraceWriter : IDisposable
{
	public const string ActionDirection = "action";
	public const string SentDirection = ">";
	public const string ReceivedDirection = "<";

	private static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);
	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNamingPolicy = null,
	};

	private readonly StreamWriter writer;
	private readonly TimeProvider wallClock;
	private readonly Func<TimeSpan?> virtualTime;
	private readonly object writeLock = new();
	private bool disposed;

	public BotActionTraceWriter(Stream output, string run, string bot, string account,
		TimeProvider? wallClock = null, Func<TimeSpan?>? virtualTime = null, bool leaveOpen = false)
	{
		ArgumentNullException.ThrowIfNull(output);
		ArgumentException.ThrowIfNullOrWhiteSpace(run);
		ArgumentException.ThrowIfNullOrWhiteSpace(bot);
		ArgumentException.ThrowIfNullOrWhiteSpace(account);
		if (!output.CanWrite)
			throw new ArgumentException("The bot trace stream must be writable.", nameof(output));

		Run = run;
		Bot = bot;
		Account = account;
		this.wallClock = wallClock ?? TimeProvider.System;
		this.virtualTime = virtualTime ?? (() => null);
		writer = new StreamWriter(output, Utf8WithoutBom, bufferSize: 1024, leaveOpen)
		{
			NewLine = "\n",
		};
	}

	public string Run { get; }
	public string Bot { get; }
	public string Account { get; }

	public static BotActionTraceWriter Open(string path, string run, string bot, string account,
		TimeProvider? wallClock = null, Func<TimeSpan?>? virtualTime = null)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(path);
		var fullPath = Path.GetFullPath(path);
		Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
		var stream = new FileStream(fullPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
		return new BotActionTraceWriter(stream, run, bot, account, wallClock, virtualTime);
	}

	public void WriteAction(string step, string action, IReadOnlyDictionary<string, object?>? fields = null) =>
		Write(step, ActionDirection, action, fields ?? EmptyFields.Instance);

	public void WriteSent(string step, BotClientPacket packet, IReadOnlyDictionary<string, object?>? fields = null)
	{
		ArgumentNullException.ThrowIfNull(packet);
		Write(step, SentDirection, packet.PacketType.Name,
			fields ?? new Dictionary<string, object?>(StringComparer.Ordinal) { ["bodyHex"] = Convert.ToHexString(packet.Body) });
	}

	public void WriteReceived(string step, DecodedBotServerPacket packet)
	{
		ArgumentNullException.ThrowIfNull(packet);
		if (packet.PacketType == typeof(SM_SYSTEM_MESSAGE) &&
			(!packet.Fields.ContainsKey("name") || !packet.Fields.ContainsKey("params")))
			throw new InvalidDataException("Decoded system-message traces require the STR_ name and parameters.");
		Write(step, ReceivedDirection, packet.PacketType.Name, packet.Fields);
	}

	public void Dispose()
	{
		lock (writeLock)
		{
			if (disposed)
				return;
			disposed = true;
			writer.Dispose();
		}
	}

	private void Write(string step, string direction, string packet,
		IReadOnlyDictionary<string, object?> fields)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(step);
		ArgumentException.ThrowIfNullOrWhiteSpace(packet);

		using var output = new MemoryStream();
		using (var json = new Utf8JsonWriter(output))
		{
			json.WriteStartObject();
			json.WriteString("ts", FormatTimestamp(wallClock.GetUtcNow()));
			var virtualNow = virtualTime();
			if (virtualNow == null)
				json.WriteNull("vt");
			else
				json.WriteString("vt", FormatVirtualTime(virtualNow.Value));
			json.WriteString("run", Run);
			json.WriteString("bot", Bot);
			json.WriteString("account", Account);
			json.WriteString("step", step);
			json.WriteString("dir", direction);
			json.WriteString("packet", packet);
			json.WritePropertyName("fields");
			JsonSerializer.Serialize(json, fields, JsonOptions);
			json.WriteEndObject();
		}

		var line = Utf8WithoutBom.GetString(output.GetBuffer(), 0, checked((int)output.Length));
		lock (writeLock)
		{
			ObjectDisposedException.ThrowIf(disposed, this);
			writer.WriteLine(line);
			writer.Flush();
		}
	}

	private static string FormatTimestamp(DateTimeOffset timestamp) =>
		timestamp.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);

	private static string FormatVirtualTime(TimeSpan elapsed)
	{
		if (elapsed < TimeSpan.Zero)
			throw new ArgumentOutOfRangeException(nameof(elapsed), "Virtual elapsed time cannot be negative.");
		var totalHours = checked((long)elapsed.TotalHours);
		return string.Create(CultureInfo.InvariantCulture,
			$"{totalHours:00}:{elapsed.Minutes:00}:{elapsed.Seconds:00}.{elapsed.Milliseconds:000}");
	}

	private sealed class EmptyFields : Dictionary<string, object?>
	{
		public static readonly EmptyFields Instance = new();
	}
}
