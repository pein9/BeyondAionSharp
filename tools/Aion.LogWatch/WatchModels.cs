using System.Text.Json;

namespace Aion.LogWatch;

internal sealed record BotStep(
	string Bot,
	string Account,
	string Step,
	DateTimeOffset Timestamp,
	string Direction,
	string Packet);

internal sealed record LedgerEntry(string Fingerprint, string Status, string? Tracking, string? FixedIn);

internal sealed record WatchProblem(
	DateTimeOffset Timestamp,
	string Server,
	string Level,
	string Fingerprint,
	string Template,
	string Message,
	string? ExceptionType,
	string? Frame,
	string? Account,
	string? Timer,
	string? Operation = null,
	string? Category = null,
	string? Bot = null,
	string? Step = null,
	string? Kind = null,
	string? Stack = null)
{
	public static WatchProblem FromServerJson(JsonElement root)
	{
		return new WatchProblem(
			ReadTimestamp(root),
			RequiredString(root, "srv"),
			RequiredString(root, "lvl"),
			RequiredString(root, "fp"),
			RequiredString(root, "tpl"),
			RequiredString(root, "msg"),
			OptionalString(root, "exType"),
			OptionalString(root, "frame"),
			OptionalString(root, "acct"),
			OptionalString(root, "timer"),
			OptionalString(root, "op"),
			OptionalString(root, "cat"),
			Stack: OptionalString(root, "stack"));
	}

	public static DateTimeOffset ReadTimestamp(JsonElement root)
	{
		var raw = RequiredString(root, "ts");
		return DateTimeOffset.TryParse(raw, out var value)
			? value
			: throw new InvalidDataException($"Invalid timestamp '{raw}'.");
	}

	public static string RequiredString(JsonElement root, string name)
	{
		if (!root.TryGetProperty(name, out var property) || property.ValueKind != JsonValueKind.String)
			throw new InvalidDataException($"JSON record is missing string '{name}'.");
		return property.GetString()!;
	}

	public static string? OptionalString(JsonElement root, string name)
	{
		if (!root.TryGetProperty(name, out var property) || property.ValueKind == JsonValueKind.Null)
			return null;
		return property.ValueKind == JsonValueKind.String
			? property.GetString()
			: throw new InvalidDataException($"JSON record '{name}' must be a string or null.");
	}
}

internal sealed record DockerLine(string Source, string Service, string Line, bool IsErrorStream);
