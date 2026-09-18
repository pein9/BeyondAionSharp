using Aion.Commons.Logging;

namespace Aion.Commons.Tests;

public sealed class LogProblemAllowlistTests
{
	private static readonly DateOnly Today = new(2026, 9, 17);

	[Fact]
	public void CheckedInAllowlistIsValid()
	{
		var allowlist = LogProblemAllowlist.Load(RepoFile("parity-artifacts", "e2e", "log-allowlist.json"), Today);

		Assert.Equal(5, allowlist.Entries.Count);
		Assert.All(allowlist.Entries.Where(entry => entry.Fingerprint != "cf736122" && entry.Tracking == "P3-10"), entry =>
		{
			Assert.Equal("P3-10", entry.Tracking);
			Assert.Equal(["LIVE"], entry.Modes);
			Assert.Equal(1, entry.MaxCount);
			Assert.Equal(new DateOnly(2027, 9, 17), entry.Expires);
		});
		LogProblemAllowlistEntry m2 = allowlist.Entries.Single(entry => entry.Tracking == "P6-05/M2");
		Assert.Equal(["M2"], Assert.IsType<string[]>(m2.Scenarios));
		LogProblemAllowlistEntry glide = allowlist.Entries.Single(entry => entry.Fingerprint == "cf736122");
		Assert.Equal(["M6"], Assert.IsType<string[]>(glide.Scenarios));
		Assert.Contains("SIM", glide.Modes);
		Assert.Equal(["SIM"], m2.Modes);
		allowlist.ValidateFullRunMatches(allowlist.Entries.Select(entry => entry.Fingerprint));
	}

	[Theory]
	[InlineData("", "maintainer", "2026-10-01", "has no reason")]
	[InlineData("known transient", "", "2026-10-01", "has no owner")]
	[InlineData("known transient", "maintainer", "2026-09-16", "expired")]
	public void LoadRejectsUnownedUnexplainedOrExpiredEntries(string reason, string owner, string expires, string expected)
	{
		var path = WriteAllowlist(reason, owner, expires);
		try
		{
			var exception = Assert.Throws<InvalidDataException>(() => LogProblemAllowlist.Load(path, Today));
			Assert.Contains(expected, exception.Message, StringComparison.Ordinal);
		}
		finally
		{
			File.Delete(path);
		}
	}

	[Fact]
	public void FullRunCheckRejectsEntriesThatMatchedNothing()
	{
		var path = WriteAllowlist("known transient", "maintainer", "2026-10-01");
		try
		{
			var allowlist = LogProblemAllowlist.Load(path, Today);

			var exception = Assert.Throws<InvalidDataException>(() => allowlist.ValidateFullRunMatches([]));
			Assert.Contains("1234abcd", exception.Message, StringComparison.Ordinal);
			allowlist.ValidateFullRunMatches(["1234abcd"]);
		}
		finally
		{
			File.Delete(path);
		}
	}

	private static string WriteAllowlist(string reason, string owner, string expires)
	{
		var path = Path.Combine(Path.GetTempPath(), $"aion-log-allowlist-{Guid.NewGuid():N}.json");
		File.WriteAllText(
			path,
			$$"""
			[
			  {
			    "fp": "1234abcd",
			    "reason": "{{reason}}",
			    "owner": "{{owner}}",
			    "tracking": "FPB-A3",
			    "modes": ["SIM", "LIVE"],
			    "servers": ["gs"],
			    "maxCount": 1,
			    "expires": "{{expires}}"
			  }
			]
			""");
		return path;
	}

	private static string RepoFile(params string[] parts)
	{
		var directory = new DirectoryInfo(AppContext.BaseDirectory);
		while (directory != null && !File.Exists(Path.Combine(directory.FullName, "AionServer.slnx")))
			directory = directory.Parent;
		if (directory == null)
			throw new DirectoryNotFoundException("Could not find the repository root.");
		return Path.Combine([directory.FullName, .. parts]);
	}
}
