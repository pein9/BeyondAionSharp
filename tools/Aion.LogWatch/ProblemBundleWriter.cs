using System.Text;
using System.Text.Json;

namespace Aion.LogWatch;

internal sealed class ProblemBundleWriter(string runDirectory, string run, RunProvenance provenance)
{
	public void Write(WatchProblem problem, IReadOnlyList<BotStep> steps)
	{
		var directory = Path.Combine(runDirectory, "problems", problem.Fingerprint);
		Directory.CreateDirectory(directory);
		File.WriteAllText(Path.Combine(directory, "stack.txt"),
			(problem.Stack ?? "<no stack captured>") + "\n", new UTF8Encoding(false));
		WriteServerContext(problem, Path.Combine(directory, "server-context.log"));
		WriteTraceContext(problem, steps, Path.Combine(directory, "bot-trace.jsonl"));
		File.WriteAllText(Path.Combine(directory, "metadata.json"), JsonSerializer.Serialize(new
		{
			run,
			fingerprint = problem.Fingerprint,
			server = problem.Server,
			level = problem.Level,
			timestamp = problem.Timestamp,
			bot = problem.Bot,
			step = problem.Step,
			seed = provenance.Seed,
			gitSha = provenance.GitSha,
			configProfile = provenance.ConfigProfile,
		}, new JsonSerializerOptions
		{
			WriteIndented = true,
			DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
		}) + "\n", new UTF8Encoding(false));
		File.WriteAllText(Path.Combine(directory, "draft-backlog.md"), $$"""
			### {{problem.Fingerprint}} — {{OneLine(problem.Message)}}

			- First reproduced: `{{provenance.GitSha}}`, run `{{run}}`, seed `{{provenance.Seed}}`, profile `{{provenance.ConfigProfile}}`
			- Server/level: `{{problem.Server}}` / `{{problem.Level}}`
			- Bot/step: `{{problem.Bot ?? "n/a"}}` / `{{problem.Step ?? "n/a"}}`
			- Tracking: TODO (`docs/Full-Parity-Backlog.md` id or issue URL)

			Evidence: `stack.txt`, `server-context.log`, `bot-trace.jsonl`, and `metadata.json` in this directory.
			""" + "\n", new UTF8Encoding(false));
	}

	private void WriteServerContext(WatchProblem problem, string destination)
	{
		var source = problem.Server switch
		{
			"gs" or "gs2" or "ls" or "cs" => Path.Combine(runDirectory, "logs", problem.Server, "server_console.log"),
			"bots" => Path.Combine(runDirectory, "bot.problems.jsonl"),
			_ => Path.Combine(runDirectory, "logs", "containers", $"{problem.Server}.log"),
		};
		if (!File.Exists(source))
		{
			File.WriteAllText(destination, $"<server log unavailable: {source}>\n", new UTF8Encoding(false));
			return;
		}
		var needle = OneLine(problem.Message);
		if (needle.Length > 160)
			needle = needle[..160];
		var preceding = new Queue<string>();
		var context = new List<string>(200);
		bool matched = false;
		// Share with active writers and stop at this poll's complete-record boundary.
		foreach (string line in new FileTail(source).ReadNewLines())
		{
			if (!matched && line.Contains(needle, StringComparison.Ordinal))
			{
				context.AddRange(preceding.TakeLast(100));
				matched = true;
			}
			if (matched)
			{
				context.Add(line);
				if (context.Count == 200) break;
			}
			else
			{
				preceding.Enqueue(line);
				if (preceding.Count > 101) preceding.Dequeue();
			}
		}
		File.WriteAllLines(destination, matched ? context : preceding, new UTF8Encoding(false));
	}

	private static void WriteTraceContext(WatchProblem problem, IReadOnlyList<BotStep> steps, string destination)
	{
		var selected = steps
			.Where(step => (problem.Account == null || step.Account == problem.Account) && step.Timestamp <= problem.Timestamp)
			.TakeLast(50)
			.Select(step => step.RawLine);
		File.WriteAllLines(destination, selected, new UTF8Encoding(false));
	}

	private static string OneLine(string value) => value.Replace('\r', ' ').Replace('\n', ' ');
}
