using System.Text.Json;
using System.Text.Json.Serialization;
using Aion.GameServer.Model;

namespace Aion.Bots.Scenarios;

public sealed record QuestDialogProtocolTransition
{
	[JsonRequired]
	public required string Id { get; init; }

	[JsonRequired]
	public required string[] States { get; init; }

	[JsonRequired]
	public required string Target { get; init; }

	[JsonRequired]
	public required string[] Actions { get; init; }

	public string? Condition { get; init; }

	[JsonRequired]
	public required JsonElement Response { get; init; }

	[JsonRequired]
	public required string[] Effects { get; init; }
}

public sealed record QuestDialogHelperProtocol
{
	[JsonRequired]
	public required string JavaSource { get; init; }

	[JsonRequired]
	public required QuestDialogProtocolTransition[] Transitions { get; init; }
}

public sealed record QuestTemplateProtocol
{
	[JsonRequired]
	public required string JavaClass { get; init; }

	[JsonRequired]
	public required string JavaSource { get; init; }

	public string? StartableDefaultHelper { get; init; }

	[JsonRequired]
	public required QuestDialogProtocolTransition[] Transitions { get; init; }
}

public sealed record QuestTemplateDialogProtocolDocument
{
	[JsonRequired]
	public required int SchemaVersion { get; init; }

	[JsonRequired]
	public required string JavaCommit { get; init; }

	[JsonRequired]
	public required Dictionary<string, int> Actions { get; init; }

	[JsonRequired]
	public required int[] RewardPages { get; init; }

	[JsonRequired]
	public required Dictionary<string, QuestDialogHelperProtocol> Helpers { get; init; }

	[JsonRequired]
	public required Dictionary<string, QuestTemplateProtocol> Templates { get; init; }
}

public static class QuestTemplateDialogProtocol
{
	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNameCaseInsensitive = false,
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
	};

	private static readonly HashSet<string> RequiredTemplates =
	[
		"report_to",
		"monster_hunt",
		"item_collecting",
		"report_to_many",
		"item_order",
		"kill_in_world",
		"kill_in_zone",
		"kill_spawned",
		"work_order",
		"skill_use",
		"report_on_levelup",
	];

	private static readonly HashSet<string> States = ["startable", "start", "reward"];
	private static readonly HashSet<string> ResponseKinds =
	[
		"page",
		"startDialog",
		"endDialog",
		"selection",
		"close",
		"handled",
		"branch",
		"noPacket",
	];

	public static QuestTemplateDialogProtocolDocument Load(string path)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(path);
		try
		{
			using var input = File.OpenRead(path);
			QuestTemplateDialogProtocolDocument document = JsonSerializer.Deserialize<QuestTemplateDialogProtocolDocument>(input, JsonOptions)
				?? throw new InvalidDataException($"Quest template dialog protocol '{path}' is empty.");
			Validate(document);
			return document;
		}
		catch (JsonException exception)
		{
			throw new InvalidDataException($"Quest template dialog protocol '{path}' is invalid: {exception.Message}", exception);
		}
	}

	public static string FindDefaultPath()
	{
		foreach (string root in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory }.Distinct(StringComparer.OrdinalIgnoreCase))
		{
			for (DirectoryInfo? directory = new(root); directory != null; directory = directory.Parent)
			{
				string candidate = Path.Combine(directory.FullName, "parity-artifacts", "e2e", "quest-template-dialog-protocol.json");
				if (File.Exists(candidate))
					return candidate;
			}
		}
		throw new FileNotFoundException("Could not locate parity-artifacts/e2e/quest-template-dialog-protocol.json.");
	}

	private static void Validate(QuestTemplateDialogProtocolDocument document)
	{
		if (document.SchemaVersion != 1)
			throw new InvalidDataException($"Unsupported quest template dialog protocol schema {document.SchemaVersion}.");
		if (string.IsNullOrWhiteSpace(document.JavaCommit))
			throw new InvalidDataException("Quest template dialog protocol must name its Java commit.");
		if (!document.Templates.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(RequiredTemplates))
			throw new InvalidDataException("Quest template dialog protocol must contain exactly the eleven Phase 7 templates.");
		if (document.RewardPages is not [5, 6, 7, 8, 45, 46, 47, 48, 49, 50])
			throw new InvalidDataException("Quest reward page ids do not match DialogPage.getRewardPageByIndex.");

		foreach ((string action, int id) in document.Actions)
		{
			if (!string.Equals(DialogAction.NameOf(id), action, StringComparison.Ordinal))
				throw new InvalidDataException($"Dialog action {action}={id} does not match the production DialogAction table.");
		}

		var ids = new HashSet<string>(StringComparer.Ordinal);
		foreach ((string name, QuestDialogHelperProtocol helper) in document.Helpers)
			ValidateTransitions($"helper:{name}", helper.JavaSource, helper.Transitions, document.Actions, ids);
		foreach ((string name, QuestTemplateProtocol template) in document.Templates)
		{
			if (template.StartableDefaultHelper != null && !document.Helpers.ContainsKey(template.StartableDefaultHelper))
				throw new InvalidDataException($"Protocol template '{name}' names an unknown startable default helper.");
			ValidateTransitions(name, template.JavaSource, template.Transitions, document.Actions, ids);
		}
	}

	private static void ValidateTransitions(string owner, string source, IReadOnlyList<QuestDialogProtocolTransition> transitions,
		IReadOnlyDictionary<string, int> actions, HashSet<string> ids)
	{
		if (string.IsNullOrWhiteSpace(source) || transitions.Count == 0)
			throw new InvalidDataException($"Protocol owner '{owner}' has no Java source or transitions.");
		foreach (QuestDialogProtocolTransition transition in transitions)
		{
			if (string.IsNullOrWhiteSpace(transition.Id) || !ids.Add($"{owner}:{transition.Id}"))
				throw new InvalidDataException($"Protocol owner '{owner}' has an empty or duplicate transition id.");
			if (transition.States.Length == 0 || transition.States.Any(state => !States.Contains(state)))
				throw new InvalidDataException($"Protocol transition '{owner}:{transition.Id}' has an invalid state.");
			if (string.IsNullOrWhiteSpace(transition.Target) || transition.Actions.Length == 0 ||
				transition.Actions.Any(action => action != "*" && !actions.ContainsKey(action)))
				throw new InvalidDataException($"Protocol transition '{owner}:{transition.Id}' has an invalid target or action.");
			if (transition.Response.ValueKind != JsonValueKind.Object ||
				!transition.Response.TryGetProperty("kind", out JsonElement kind) ||
				kind.ValueKind != JsonValueKind.String || !ResponseKinds.Contains(kind.GetString()!))
				throw new InvalidDataException($"Protocol transition '{owner}:{transition.Id}' has an invalid response.");
			if (transition.Effects == null)
				throw new InvalidDataException($"Protocol transition '{owner}:{transition.Id}' must declare its effects.");
		}
	}
}
