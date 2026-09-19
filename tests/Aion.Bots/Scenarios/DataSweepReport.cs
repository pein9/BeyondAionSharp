using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aion.Bots.Scenarios;

public enum DataSweepStatus { Pending, Passed, Unreachable, Failed, Inactive }

public sealed record DataSweepRow(string Id, DataSweepStatus Status, string Evidence,
	IReadOnlyDictionary<string, string>? Details = null);

/// <summary>Exhaustive inventory first, evidence second: interrupted or partial runs cannot look complete.</summary>
public sealed class DataSweepReport
{
	private readonly SortedDictionary<string, DataSweepRow> rows = new(StringComparer.Ordinal);
	public string Sweep { get; }
	public IReadOnlyCollection<DataSweepRow> Rows => rows.Values;
	public bool Complete => rows.Count > 0 && rows.Values.All(r => r.Status is DataSweepStatus.Passed or DataSweepStatus.Unreachable
		|| Sweep == "tradelists" && r.Status == DataSweepStatus.Inactive);

	public DataSweepReport(string sweep, IEnumerable<string> ids)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(sweep);
		Sweep = sweep;
		foreach (string id in ids)
		{
			ArgumentException.ThrowIfNullOrWhiteSpace(id);
			if (!rows.TryAdd(id, new(id, DataSweepStatus.Pending, "Not attempted")))
				throw new ArgumentException($"Duplicate sweep row {id}.", nameof(ids));
		}
		if (rows.Count == 0) throw new ArgumentException("A data sweep cannot have an empty inventory.", nameof(ids));
	}

	public void Record(string id, DataSweepStatus status, string evidence, IReadOnlyDictionary<string, string>? details = null)
	{
		if (status == DataSweepStatus.Pending || !Enum.IsDefined(status)) throw new ArgumentOutOfRangeException(nameof(status));
		if (status == DataSweepStatus.Inactive && Sweep != "tradelists") throw new ArgumentException("Inactive is approved only for trade catalogs.", nameof(status));
		if (status == DataSweepStatus.Unreachable && Sweep is "recipes" or "skills")
			throw new ArgumentException("Every recipe and skill requires execution with supplied prerequisites.", nameof(status));
		ArgumentException.ThrowIfNullOrWhiteSpace(evidence);
		if (!rows.TryGetValue(id, out var previous)) throw new ArgumentException($"Unknown sweep row {id}.", nameof(id));
		if (previous.Status != DataSweepStatus.Pending) throw new InvalidOperationException($"Sweep row {id} already has a result.");
		rows[id] = new(id, status, evidence, details);
	}

	public void Save(string path)
	{
		Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
		File.WriteAllText(path, JsonSerializer.Serialize(new
		{
			schemaVersion = 1, sweep = Sweep, complete = Complete,
			counts = Enum.GetValues<DataSweepStatus>().Where(s => s != DataSweepStatus.Inactive || Sweep == "tradelists")
				.ToDictionary(s => s.ToString(), s => rows.Values.Count(r => r.Status == s)),
			rows = rows.Values,
		}, JsonOptions) + "\n");
	}

	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true,
		Converters = { new JsonStringEnumConverter() },
	};
}
