using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Aion.Commons.Logging;

public sealed record LogProblemAllowlistEntry
{
	[JsonPropertyName("fp")]
	[JsonRequired]
	public required string Fingerprint { get; init; }

	[JsonPropertyName("reason")]
	[JsonRequired]
	public required string Reason { get; init; }

	[JsonPropertyName("owner")]
	[JsonRequired]
	public required string Owner { get; init; }

	[JsonPropertyName("tracking")]
	[JsonRequired]
	public required string Tracking { get; init; }

	[JsonPropertyName("modes")]
	[JsonRequired]
	public required string[] Modes { get; init; }

	[JsonPropertyName("servers")]
	[JsonRequired]
	public required string[] Servers { get; init; }

	[JsonPropertyName("scenarios")]
	public string[]? Scenarios { get; init; }

	[JsonPropertyName("maxCount")]
	[JsonRequired]
	public required int MaxCount { get; init; }

	[JsonPropertyName("expires")]
	[JsonRequired]
	public required DateOnly Expires { get; init; }
}

/// <summary>Loads and validates the shared end-to-end problem allowlist.</summary>
public sealed class LogProblemAllowlist
{
	private static readonly Regex FingerprintPattern = new(
		"^[0-9a-f]{8}$",
		RegexOptions.Compiled | RegexOptions.CultureInvariant);
	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNameCaseInsensitive = false,
		UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
	};

	private LogProblemAllowlist(IReadOnlyList<LogProblemAllowlistEntry> entries) => Entries = entries;

	public IReadOnlyList<LogProblemAllowlistEntry> Entries { get; }

	public static LogProblemAllowlist Load(string path, DateOnly? today = null)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(path);
		try
		{
			using var stream = File.OpenRead(path);
			var entries = JsonSerializer.Deserialize<LogProblemAllowlistEntry[]>(stream, JsonOptions)
				?? throw new InvalidDataException($"Problem allowlist '{path}' must contain a JSON array.");
			ValidateEntries(entries, today ?? DateOnly.FromDateTime(DateTime.UtcNow));
			return new LogProblemAllowlist(entries);
		}
		catch (JsonException exception)
		{
			throw new InvalidDataException($"Problem allowlist '{path}' is not valid: {exception.Message}", exception);
		}
	}

	/// <summary>
	/// Rejects stale entries after a Full run. The caller supplies the fingerprints of allowlist entries that
	/// actually matched at least one problem during that run.
	/// </summary>
	public void ValidateFullRunMatches(IEnumerable<string> matchedFingerprints)
	{
		ArgumentNullException.ThrowIfNull(matchedFingerprints);
		var matched = matchedFingerprints.ToHashSet(StringComparer.Ordinal);
		var stale = Entries
			.Where(entry => !matched.Contains(entry.Fingerprint))
			.Select(entry => entry.Fingerprint)
			.Order(StringComparer.Ordinal)
			.ToArray();
		if (stale.Length != 0)
			throw new InvalidDataException($"Problem allowlist entries matched nothing in the Full run: {string.Join(", ", stale)}");
	}

	private static void ValidateEntries(IReadOnlyList<LogProblemAllowlistEntry> entries, DateOnly today)
	{
		var fingerprints = new HashSet<string>(StringComparer.Ordinal);
		foreach (var entry in entries)
		{
			if (string.IsNullOrEmpty(entry.Fingerprint) || !FingerprintPattern.IsMatch(entry.Fingerprint))
				throw new InvalidDataException($"Problem allowlist fingerprint '{entry.Fingerprint}' must be eight lowercase hexadecimal characters.");
			if (!fingerprints.Add(entry.Fingerprint))
				throw new InvalidDataException($"Problem allowlist fingerprint '{entry.Fingerprint}' is duplicated.");
			if (string.IsNullOrWhiteSpace(entry.Reason))
				throw new InvalidDataException($"Problem allowlist entry '{entry.Fingerprint}' has no reason.");
			if (string.IsNullOrWhiteSpace(entry.Owner))
				throw new InvalidDataException($"Problem allowlist entry '{entry.Fingerprint}' has no owner.");
			if (string.IsNullOrWhiteSpace(entry.Tracking))
				throw new InvalidDataException($"Problem allowlist entry '{entry.Fingerprint}' has no tracking reference.");
			if (entry.Modes == null || entry.Modes.Length == 0 || entry.Modes.Any(string.IsNullOrWhiteSpace))
				throw new InvalidDataException($"Problem allowlist entry '{entry.Fingerprint}' has no valid modes.");
			if (entry.Servers == null || entry.Servers.Length == 0 || entry.Servers.Any(string.IsNullOrWhiteSpace))
				throw new InvalidDataException($"Problem allowlist entry '{entry.Fingerprint}' has no valid servers.");
			if (entry.Scenarios != null && (entry.Scenarios.Length == 0 || entry.Scenarios.Any(string.IsNullOrWhiteSpace)))
				throw new InvalidDataException($"Problem allowlist entry '{entry.Fingerprint}' has no valid scenarios.");
			if (entry.MaxCount <= 0)
				throw new InvalidDataException($"Problem allowlist entry '{entry.Fingerprint}' must have a positive maxCount.");
			if (entry.Expires < today)
				throw new InvalidDataException($"Problem allowlist entry '{entry.Fingerprint}' expired on {entry.Expires:yyyy-MM-dd}.");
		}
	}
}
