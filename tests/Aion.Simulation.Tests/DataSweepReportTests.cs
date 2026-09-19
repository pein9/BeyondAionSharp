using Aion.Bots.Scenarios;
using System.Text.Json;

namespace Aion.Simulation.Tests;

public sealed class DataSweepReportTests
{
	[Theory]
	[InlineData("skills")]
	[InlineData("recipes")]
	public void SuppliedPrerequisiteSweepsCannotClaimMissingSpawnExemptions(string sweep)
	{
		var report = new DataSweepReport(sweep, ["1"]);
		Assert.Throws<ArgumentException>(() => report.Record("1", DataSweepStatus.Unreachable, "No convenient target"));
		Assert.False(report.Complete);
		Assert.Equal(DataSweepStatus.Pending, Assert.Single(report.Rows).Status);
	}

	[Fact]
	public void InactiveIsDistinctFromSuccessfulTransactionsAndLimitedToTradeCatalogs()
	{
		var report = new DataSweepReport("tradelists", ["tradelist_template:203081"]);
		report.Record("tradelist_template:203081", DataSweepStatus.Inactive, "Shipped NPC has no BUY action");
		Assert.True(report.Complete); Assert.Equal(DataSweepStatus.Inactive, Assert.Single(report.Rows).Status);
		Assert.Throws<ArgumentException>(() => new DataSweepReport("recipes", ["1"]).Record("1", DataSweepStatus.Inactive, "Not allowed"));
		string path = Path.Combine(Path.GetTempPath(), $"aion-inactive-report-{Guid.NewGuid():N}.json");
		try
		{
			report.Save(path);
			using var saved = JsonDocument.Parse(File.ReadAllText(path));
			var counts = saved.RootElement.GetProperty("counts");
			Assert.Equal(1, counts.GetProperty("Inactive").GetInt32());
			Assert.Equal(0, counts.GetProperty("Passed").GetInt32());
			Assert.Equal(0, counts.GetProperty("Unreachable").GetInt32());
			Assert.Equal("Inactive", saved.RootElement.GetProperty("rows")[0].GetProperty("status").GetString());
		}
		finally { File.Delete(path); }
	}

	[Fact]
	public void InventoryMustBeNonemptyUniqueAndEntirelyAccountedFor()
	{
		Assert.Throws<ArgumentException>(() => new DataSweepReport("gather", []));
		Assert.Throws<ArgumentException>(() => new DataSweepReport("gather", ["1", "1"]));
		var report = new DataSweepReport("gather", ["2", "1"]);
		Assert.Equal(["1", "2"], report.Rows.Select(r => r.Id)); Assert.False(report.Complete);
		report.Record("1", DataSweepStatus.Passed, "Normal gather packet and exact inventory delta");
		Assert.False(report.Complete);
		report.Record("2", DataSweepStatus.Unreachable, "No shipped spawn definition or runtime spawn");
		Assert.True(report.Complete);
		Assert.Throws<InvalidOperationException>(() => report.Record("1", DataSweepStatus.Passed, "Duplicate"));
		Assert.Throws<ArgumentException>(() => report.Record("3", DataSweepStatus.Passed, "Outside inventory"));
	}

	[Fact]
	public void FailuresAndPendingRowsNeverProduceACompleteReport()
	{
		var report = new DataSweepReport("gather", ["1", "2"]);
		Assert.Throws<ArgumentException>(() => report.Record("1", DataSweepStatus.Unreachable, ""));
		Assert.Throws<ArgumentOutOfRangeException>(() => report.Record("1", DataSweepStatus.Pending, "Not a result"));
		report.Record("1", DataSweepStatus.Failed, "Missing product");
		report.Record("2", DataSweepStatus.Passed, "Verified");
		Assert.False(report.Complete);
	}
}
