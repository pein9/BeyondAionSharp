using Aion.Commons.Logging;
using Aion.GameServer.Configs.Schedule;
using Aion.GameServer.Services.Worldraid;
using Microsoft.Extensions.Logging;

namespace Aion.GameServer.Tests;

/// <summary>
/// The world raid log lines as Java writes them. Found by the RIFT scenario (P11-04), which reads them from the raids of the
/// 3rd; this pins the one a unit can reach without the world.
/// </summary>
public sealed class WorldRaidLogParityTests
{
	/// <summary>
	/// WorldRaidRunnable.run logs "Attempting to start world raid with ID: ... and location pool: ..."; Java concatenates the
	/// List&lt;Integer&gt;, which prints as AbstractCollection.toString does ("[]", "[4, 5, 6]"), where C# printed the list's type
	/// name. A schedule without locations asks the world raid service nothing.
	/// </summary>
	[Fact]
	public void WorldRaidRunnableLogsItsLocationPoolAsJavaPrintsAList()
	{
		var schedule = new WorldRaidSchedules.WorldRaidSchedule { id = "empty_pool", LocationsRaw = "" };
		Assert.Empty(schedule.GetLocations());
		using var capture = new CapturingLoggerProvider();
		using var factory = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Trace).AddProvider(capture));
		using (AionLog.OverrideFactory(factory))
			new WorldRaidRunnable(schedule).Run();

		CapturedLogEntry line = Assert.Single(capture.Entries, entry => entry.Category == nameof(WorldRaidRunnable));
		Assert.Equal("Attempting to start world raid with ID: empty_pool and location pool: []", line.Message);
	}
}
