using System.Collections;
using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Aion.Commons.Nio;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Network.Aion.Capture;

/// <summary>
/// C#-only session recorder (the Java 4.8 server has none). For each recorded connection it writes one JSONL
/// file holding every packet in both directions, in the order the server saw them:
/// <list type="bullet">
/// <item>client packets: the full decrypted payload (header and body, exactly what the client encrypted), the
/// decoded opcode and handler, every field the handler parsed, what the server did with it, and the server's
/// view of the character at that moment;</item>
/// <item>server packets: the full clear frame (length, header, body) before encryption, and every field of the
/// packet object that wrote it;</item>
/// <item>session events: open, account login, entering the world, leaving it, and close.</item>
/// </list>
/// The client's keystrokes never reach a server; the packets they produce do, and that is the level a replay
/// needs. A connection is buffered from its first packet until its account is known, then kept (and the buffer
/// written) when the account matches the filter, or dropped.
/// Enabled with <c>AION_RECORD=true</c>; <c>AION_RECORD_ACCOUNTS</c> (comma-separated, empty = every account)
/// selects accounts and <c>AION_RECORD_DIR</c> (default <c>./log/recordings</c>) the output folder.
/// </summary>
public sealed class SessionRecorder : IAsyncDisposable
{
	public const int FormatVersion = 1;
	private const int PreAccountBufferLimit = 20_000;
	private const int MaxCollectionItems = 512;
	private const int MaxStringLength = 4096;

	private static SessionRecorder? current;
	private static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);
	private static readonly ConcurrentDictionary<Type, FieldInfo[]> FieldCache = new();
	private static readonly JsonSerializerOptions Json = new() { WriteIndented = false };

	private readonly string directory;
	private readonly HashSet<string>? accounts;
	private readonly string? run;
	private readonly ConditionalWeakTable<AionConnection, Session> sessions = new();
	private readonly ConcurrentDictionary<Session, byte> open = new();
	private readonly ConcurrentBag<Task> finishing = [];
	private int disposed;

	public SessionRecorder(string directory, IEnumerable<string>? accounts = null, string? run = null)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(directory);
		this.directory = Path.GetFullPath(directory);
		Directory.CreateDirectory(this.directory);
		var names = accounts?.Select(a => a.Trim()).Where(a => a.Length > 0).ToArray() ?? [];
		this.accounts = names.Length == 0 ? null : new HashSet<string>(names, StringComparer.OrdinalIgnoreCase);
		this.run = run ?? Environment.GetEnvironmentVariable("AION_RUN_ID");
	}

	/// <summary>The active recorder, or null when recording is off.</summary>
	public static SessionRecorder? Current => Volatile.Read(ref current);

	public static void SetCurrent(SessionRecorder? recorder) => Volatile.Write(ref current, recorder);

	/// <summary>Reads <c>AION_RECORD</c>, <c>AION_RECORD_ACCOUNTS</c> and <c>AION_RECORD_DIR</c>; null when off.</summary>
	public static SessionRecorder? FromEnvironment()
	{
		string? setting = Environment.GetEnvironmentVariable("AION_RECORD");
		if (string.IsNullOrWhiteSpace(setting))
			return null;
		if (!bool.TryParse(setting, out bool enabled))
			throw new InvalidOperationException("AION_RECORD must be true or false.");
		if (!enabled)
			return null;
		string? dir = Environment.GetEnvironmentVariable("AION_RECORD_DIR");
		if (string.IsNullOrWhiteSpace(dir))
			dir = Path.Combine(Directory.GetCurrentDirectory(), "log", "recordings");
		return new SessionRecorder(dir, (Environment.GetEnvironmentVariable("AION_RECORD_ACCOUNTS") ?? "").Split(','));
	}

	/// <summary>
	/// A decrypted client payload, before the server decodes it. Returns the copy to pass back to
	/// <see cref="OnClientPacketHandled"/>, or null when this connection is not being recorded.
	/// </summary>
	public byte[]? OnClientPayload(AionConnection con, ByteBuffer data)
	{
		Session? session = SessionFor(con);
		if (session == null) return null;
		var payload = new byte[data.Remaining()];
		for (int i = 0; i < payload.Length; i++)
			payload[i] = data.Get(data.Position() + i);
		return payload;
	}

	/// <summary>Records one client packet after the server decoded it (or failed to).</summary>
	/// <param name="outcome">"executed", "read-failed", or "not-created" (unknown opcode or invalid state).</param>
	public void OnClientPacketHandled(AionConnection con, byte[] payload, AionClientPacket? packet, string outcome,
		string stateBefore)
	{
		Session? session = SessionFor(con);
		if (session == null) return;
		int rawOpcode = payload.Length >= 2 ? payload[0] | payload[1] << 8 : -1;
		var entry = new Dictionary<string, object?>
		{
			["dir"] = "C",
			["opcode"] = packet?.GetOpCode(),
			["opcodeHex"] = packet == null ? null : $"0x{packet.GetOpCode():X3}",
			["rawOpcode"] = rawOpcode,
			["packet"] = packet?.GetType().Name,
			["outcome"] = outcome,
			["state"] = stateBefore,
			["length"] = payload.Length,
			["payloadBase64"] = Convert.ToBase64String(payload),
			["bodyOffset"] = 5,
			["fields"] = packet == null ? null : Describe(packet, typeof(AionClientPacket)),
			["player"] = Snapshot(con.GetActivePlayer()),
		};
		session.Write(con, entry);
	}

	/// <summary>Records one server packet from its clear frame (called before in-place encryption).</summary>
	public void OnServerPacket(AionConnection con, AionServerPacket packet, ByteBuffer clearFrame)
	{
		Session? session = SessionFor(con);
		if (session == null) return;
		var frame = new byte[clearFrame.Limit()];
		for (int i = 0; i < frame.Length; i++)
			frame[i] = clearFrame.Get(i);
		var entry = new Dictionary<string, object?>
		{
			["dir"] = "S",
			["opcode"] = packet.GetOpCode(),
			["opcodeHex"] = $"0x{packet.GetOpCode():X3}",
			["packet"] = packet.GetType().Name,
			["length"] = frame.Length,
			["frameBase64"] = Convert.ToBase64String(frame),
			["bodyOffset"] = 7,
			["fields"] = Describe(packet, typeof(AionServerPacket)),
		};
		session.Write(con, entry);
	}

	/// <summary>Writes the close event and finishes the connection's file.</summary>
	public void OnDisconnect(AionConnection con)
	{
		if (!sessions.TryGetValue(con, out Session? session)) return;
		session.Close(con);
		if (open.TryRemove(session, out _))
			finishing.Add(session.Completion);
	}

	public async ValueTask DisposeAsync()
	{
		if (Interlocked.Exchange(ref disposed, 1) != 0) return;
		foreach (Session session in open.Keys)
			session.Close(null);
		// Closed sessions may still be writing their last lines; open ones were just closed above.
		await Task.WhenAll(open.Keys.Select(s => s.Completion).Concat(finishing));
	}

	private Session? SessionFor(AionConnection con)
	{
		if (Volatile.Read(ref disposed) != 0) return null;
		Session session = sessions.GetValue(con, c =>
		{
			var created = new Session(this, c);
			open[created] = 0;
			return created;
		});
		return session.Dropped ? null : session;
	}

	private bool Wants(string account) => accounts == null || accounts.Contains(account);

	/// <summary>The server's view of the character: where it is, its health and mana, and what it targets.</summary>
	private static Dictionary<string, object?>? Snapshot(Player? player)
	{
		if (player == null) return null;
		try
		{
			var life = player.GetLifeStats();
			VisibleObject? target = player.GetTarget();
			return new Dictionary<string, object?>
			{
				["objectId"] = player.GetObjectId(),
				["name"] = player.GetName(),
				["mapId"] = player.GetWorldId(),
				["x"] = player.GetX(),
				["y"] = player.GetY(),
				["z"] = player.GetZ(),
				["heading"] = player.GetHeading(),
				["level"] = player.GetLevel(),
				["hp"] = life.GetCurrentHp(),
				["maxHp"] = life.GetMaxHp(),
				["mp"] = life.GetCurrentMp(),
				["maxMp"] = life.GetMaxMp(),
				["dead"] = player.IsDead(),
				["targetObjectId"] = target?.GetObjectId(),
			};
		}
		catch (Exception error)
		{
			return new Dictionary<string, object?> { ["error"] = error.GetType().Name };
		}
	}

	/// <summary>Every instance field of a packet below <paramref name="stopAt"/>, as JSON-friendly values.</summary>
	internal static Dictionary<string, object?> Describe(object packet, Type stopAt)
	{
		var result = new Dictionary<string, object?>();
		foreach (FieldInfo field in FieldsOf(packet.GetType(), stopAt))
		{
			object? value;
			try { value = field.GetValue(packet); }
			catch (Exception error) { value = $"<{error.GetType().Name}>"; }
			result[CleanName(field.Name)] = Simplify(value, 0);
		}
		return result;
	}

	private static FieldInfo[] FieldsOf(Type type, Type stopAt) => FieldCache.GetOrAdd(type, t =>
	{
		var fields = new List<FieldInfo>();
		for (Type? current = t; current != null && current != stopAt && current != typeof(object); current = current.BaseType)
			fields.AddRange(current.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic |
				BindingFlags.DeclaredOnly));
		return fields.ToArray();
	});

	private static string CleanName(string name)
	{
		// Auto-property backing fields: "<Name>k__BackingField" -> "Name".
		int close = name.IndexOf('>', StringComparison.Ordinal);
		return name.StartsWith('<') && close > 1 ? name[1..close] : name;
	}

	private static object? Simplify(object? value, int depth)
	{
		switch (value)
		{
			case null: return null;
			case string text: return text.Length > MaxStringLength ? text[..MaxStringLength] + "…" : text;
			case bool or byte or sbyte or short or ushort or int or uint or long or ulong or char: return value;
			case float f: return float.IsFinite(f) ? f : f.ToString(CultureInfo.InvariantCulture);
			case double d: return double.IsFinite(d) ? d : d.ToString(CultureInfo.InvariantCulture);
			case decimal: return value;
			case Enum e: return e.ToString();
			case byte[] bytes: return new Dictionary<string, object?> { ["base64"] = Convert.ToBase64String(bytes) };
			case DateTime or DateTimeOffset or TimeSpan or Guid: return value.ToString();
		}
		if (value is ByteBuffer) return "<ByteBuffer>";
		Type type = value.GetType();
		if (value is VisibleObject visible)
			return new Dictionary<string, object?>
			{
				["type"] = type.Name,
				["objectId"] = visible.GetObjectId(),
				["name"] = SafeName(visible),
			};
		if (depth >= 2)
			return $"<{type.Name}>";
		if (value is IDictionary dictionary)
		{
			var map = new List<object?>();
			foreach (DictionaryEntry item in dictionary)
			{
				if (map.Count >= MaxCollectionItems) break;
				map.Add(new Dictionary<string, object?> { ["key"] = Simplify(item.Key, depth + 1), ["value"] = Simplify(item.Value, depth + 1) });
			}
			return map;
		}
		if (value is IEnumerable sequence)
		{
			var list = new List<object?>();
			foreach (object? item in sequence)
			{
				if (list.Count >= MaxCollectionItems) { list.Add("…"); break; }
				list.Add(Simplify(item, depth + 1));
			}
			return list;
		}
		// A small value object (a position, a vector, a record): its own fields, one level down.
		if (type.Namespace?.StartsWith("Aion", StringComparison.Ordinal) == true || type.IsValueType)
		{
			var nested = new Dictionary<string, object?> { ["type"] = type.Name };
			foreach (FieldInfo field in FieldsOf(type, typeof(object)).Take(32))
			{
				object? inner;
				try { inner = field.GetValue(value); }
				catch (Exception error) { inner = $"<{error.GetType().Name}>"; }
				nested[CleanName(field.Name)] = Simplify(inner, depth + 1);
			}
			return nested;
		}
		return $"<{type.Name}>";
	}

	private static string? SafeName(VisibleObject visible)
	{
		try { return visible.GetName(); }
		catch (Exception) { return null; }
	}

	// The server clock as UTC: real time on a live server, the harness clock under simulation.
	private static string RealUtcNow() =>
		SystemClock.UtcNow().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);

	/// <summary>One connection's recording: buffered until its account is known, then streamed to a file.</summary>
	private sealed class Session
	{
		private readonly SessionRecorder owner;
		private readonly string connectionId;
		private readonly object gate = new();
		private readonly List<string> pending = [];
		private Channel<string>? channel;
		private Task completion = Task.CompletedTask;
		private long sequence;
		private string? account;
		private int? playerObjectId;
		private bool closed;

		public Session(SessionRecorder owner, AionConnection con)
		{
			this.owner = owner;
			connectionId = $"{SystemClock.CurrentMillis():x}-{RuntimeHelpers.GetHashCode(con):x}";
			Enqueue(Line(new Dictionary<string, object?>
			{
				["dir"] = "E",
				["event"] = "session-open",
				["format"] = FormatVersion,
				["connection"] = connectionId,
				["ip"] = SafeIp(con),
				["run"] = owner.run,
			}));
		}

		public bool Dropped { get; private set; }

		public Task Completion => completion;

		public void Write(AionConnection con, Dictionary<string, object?> entry)
		{
			lock (gate)
			{
				if (Dropped || closed) return;
				ObserveIdentity(con);
				if (Dropped) return;
				Enqueue(Line(entry));
			}
		}

		public void Close(AionConnection? con)
		{
			lock (gate)
			{
				if (closed) return;
				if (con != null && !Dropped) ObserveIdentity(con);
				closed = true;
				if (Dropped || channel == null)
				{
					// Never matched an account: nothing is written for this connection.
					pending.Clear();
					Dropped = true;
					return;
				}
				Enqueue(Line(new Dictionary<string, object?> { ["dir"] = "E", ["event"] = "session-close" }));
				channel.Writer.TryComplete();
			}
		}

		private void ObserveIdentity(AionConnection con)
		{
			string? name = con.GetAccount()?.GetName();
			if (account == null && name != null)
			{
				account = name;
				if (!owner.Wants(name))
				{
					Dropped = true;
					pending.Clear();
					return;
				}
				Enqueue(Line(new Dictionary<string, object?>
				{
					["dir"] = "E",
					["event"] = "account",
					["account"] = name,
					["accountId"] = con.GetAccount()!.GetId(),
				}));
				Open(name);
			}
			var player = con.GetActivePlayer();
			int? objectId = player?.GetObjectId();
			if (objectId != playerObjectId)
			{
				Enqueue(Line(new Dictionary<string, object?>
				{
					["dir"] = "E",
					["event"] = player == null ? "leave-world" : "enter-world",
					["player"] = Snapshot(player),
				}));
				playerObjectId = objectId;
			}
		}

		private void Open(string accountName)
		{
			string safe = string.Concat(accountName.Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '_'));
			string stamp = RealUtcNow().Replace(":", "", StringComparison.Ordinal).Replace("-", "", StringComparison.Ordinal)
				.Replace(".", "", StringComparison.Ordinal);
			string path = Path.Combine(owner.directory, $"{safe}-{stamp}-{connectionId}.recording.jsonl");
			var writer = new StreamWriter(new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read),
				Utf8WithoutBom) { NewLine = "\n" };
			channel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
			foreach (string line in pending)
				channel.Writer.TryWrite(line);
			pending.Clear();
			completion = WriteAllAsync(channel.Reader, writer);
		}

		private void Enqueue(string line)
		{
			if (channel != null)
			{
				channel.Writer.TryWrite(line);
				return;
			}
			pending.Add(line);
			if (pending.Count > PreAccountBufferLimit)
			{
				// Far more packets than a login takes without an account: not a player session worth keeping.
				Dropped = true;
				pending.Clear();
			}
		}

		private string Line(Dictionary<string, object?> entry)
		{
			var line = new Dictionary<string, object?>
			{
				["seq"] = ++sequence,
				["t"] = SystemClock.CurrentMillis(),
				["wall"] = RealUtcNow(),
			};
			foreach (var (key, value) in entry)
				line[key] = value;
			return JsonSerializer.Serialize(line, Json);
		}

		private static async Task WriteAllAsync(ChannelReader<string> reader, StreamWriter writer)
		{
			await using (writer)
			{
				// Drain whatever is queued, then flush, so a crash loses at most the batch in flight.
				while (await reader.WaitToReadAsync())
				{
					while (reader.TryRead(out string? line))
						await writer.WriteLineAsync(line);
					await writer.FlushAsync();
				}
			}
		}

		private static string? SafeIp(AionConnection con)
		{
			try { return con.GetIP(); }
			catch (Exception) { return null; }
		}
	}
}
