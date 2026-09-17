using System.Text.Json;
using Aion.Commons.Logging;
using Microsoft.Extensions.Logging;

namespace Aion.Commons.Tests;

public sealed class JsonLinesLoggerProviderTests
{
	private static readonly string[] ExpectedKeys =
	[
		"lvl", "ts", "srv", "run", "acct", "player", "op", "cat", "thr", "timer", "fp", "tpl",
		"msg", "exType", "exMsg", "frame", "stack",
	];

	[Fact]
	public void WritesFixedOrderJsonLinesWithScopesAndFlushesWarnings()
	{
		var directory = Path.Combine(Path.GetTempPath(), $"aion-jsonl-{Guid.NewGuid():N}");
		try
		{
			using var provider = new JsonLinesLoggerProvider(directory, "gs", "run-17");
			using var factory = LoggerFactory.Create(builder =>
			{
				builder.ClearProviders();
				builder.SetMinimumLevel(LogLevel.Trace);
				builder.AddProvider(provider);
			});
			var logger = factory.CreateLogger("PacketCategory");
			using (logger.BeginScope(new Dictionary<string, string>
			{
				["account"] = "bot-account",
				["player"] = "Botone",
				["packet"] = "CM_DIALOG_SELECT",
				["timer"] = "fixed-rate",
				["timerScheduledAt"] = "2026-09-17T12:34:00.0000000+00:00",
			}))
			{
				logger.LogInformation("Read packet {PacketId}", 39);
				logger.LogWarning(new InvalidOperationException("broken"), "Failed packet {PacketId}", 39);
			}

			var eventLines = ReadAllLinesWhileWriterIsOpen(Path.Combine(directory, "gs.events.jsonl"));
			var problemLines = ReadAllLinesWhileWriterIsOpen(Path.Combine(directory, "gs.problems.jsonl"));
			Assert.Equal(2, eventLines.Length);
			Assert.Single(problemLines);

			using var info = JsonDocument.Parse(eventLines[0]);
			using var warning = JsonDocument.Parse(problemLines[0]);
			Assert.Equal(ExpectedKeys, warning.RootElement.EnumerateObject().Select(property => property.Name));
			Assert.Equal("WARN", warning.RootElement.GetProperty("lvl").GetString());
			Assert.Equal("gs", warning.RootElement.GetProperty("srv").GetString());
			Assert.Equal("run-17", warning.RootElement.GetProperty("run").GetString());
			Assert.Equal("bot-account", warning.RootElement.GetProperty("acct").GetString());
			Assert.Equal("Botone", warning.RootElement.GetProperty("player").GetString());
			Assert.Equal("CM_DIALOG_SELECT", warning.RootElement.GetProperty("op").GetString());
			Assert.Equal("fixed-rate@2026-09-17T12:34:00.0000000+00:00", warning.RootElement.GetProperty("timer").GetString());
			Assert.Equal("Failed packet {PacketId}", warning.RootElement.GetProperty("tpl").GetString());
			Assert.Equal("Failed packet 39", warning.RootElement.GetProperty("msg").GetString());
			Assert.Equal(typeof(InvalidOperationException).FullName, warning.RootElement.GetProperty("exType").GetString());
			Assert.Equal("broken", warning.RootElement.GetProperty("exMsg").GetString());
			Assert.Matches("^[0-9a-f]{8}$", warning.RootElement.GetProperty("fp").GetString());
			Assert.Equal("<unknown>", warning.RootElement.GetProperty("frame").GetString());
			Assert.Equal("INFO", info.RootElement.GetProperty("lvl").GetString());
		}
		finally
		{
			if (Directory.Exists(directory))
				Directory.Delete(directory, recursive: true);
		}
	}

	private static string[] ReadAllLinesWhileWriterIsOpen(string path)
	{
		using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
		using var reader = new StreamReader(stream);
		var lines = new List<string>();
		while (reader.ReadLine() is { } line)
			lines.Add(line);
		return lines.ToArray();
	}
}
