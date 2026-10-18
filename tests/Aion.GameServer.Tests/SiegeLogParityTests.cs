using Aion.Commons.Logging;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Dataholders.LoadingUtils;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model.Siege;
using Aion.GameServer.Services.Siege;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace Aion.GameServer.Tests;

/// <summary>
/// The siege log lines (Java's SIEGE_LOG, siege.log) as Java writes them. Found by the SIEGE scenario (P11-05), which reads
/// them from a whole siege; these pin the ones a unit can reach.
/// </summary>
public sealed class SiegeLogParityTests
{
	/// <summary>
	/// FortressAssault.calculateDifficultySettings: Java concatenates its floats, which prints them as Float.toString does,
	/// "1.0" where C# prints "1". Sulfur Fortress (1141) held by the Asmodians, no faction balance, and the influence this
	/// process has (none: SiegeServiceTestInit builds SiegeService with sieges off).
	/// </summary>
	[Fact]
	public void FortressAssaultLogsItsDifficultyAsJavaPrintsFloats()
	{
		FortressLocation fortress = LoadRealSiegeLocations().GetFortress()[1141];
		fortress.SetRace(SiegeRace.ASMODIANS);
		Assert.Equal(0, fortress.GetFactionBalance());
		Assert.Equal(0f, Influence.GetInstance().GetAsmodianInfluenceRate());
		using var capture = new CapturingLoggerProvider();
		using var factory = LoggerFactory.Create(builder => builder.AddProvider(capture));
		using (AionLog.OverrideFactory(factory))
			_ = new FortressAssault(new FortressSiege(fortress));

		CapturedLogEntry line = Assert.Single(capture.Entries, entry => entry.Category == "SIEGE_LOG");
		Assert.Equal("Initialized fortress assault on [locationID=1141] with [difficulty=0.33333334] [factionBalance=1.0] [influence=0.0] [difficultyMultiplier=1.0]",
			line.Message);
	}

	/// <summary>
	/// The siege AIs whose Java logger is LoggerFactory.getLogger("SIEGE_LOG") write to that category (siege.log), not to one
	/// named after the class: GateRepairAI's repair line, ArtifactAI's activation line and AhserionAI's kill line.
	/// </summary>
	[Theory]
	[InlineData(typeof(GateRepairAI))]
	[InlineData(typeof(ArtifactAI))]
	[InlineData(typeof(AhserionAI))]
	public void SiegeAisWriteToTheSiegeLog(Type ai)
	{
		var log = (ILogger)ai.GetField("log", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
		using var capture = new CapturingLoggerProvider();
		using var factory = LoggerFactory.Create(builder => builder.AddProvider(capture));
		using (AionLog.OverrideFactory(factory))
			log.LogInformation("probe");

		Assert.Equal("SIEGE_LOG", Assert.Single(capture.Entries).Category);
	}

	private static SiegeLocationData LoadRealSiegeLocations() =>
		JaxbHolderLoader.LoadFromFile<SiegeLocationData>(ResolveStaticDataFile("siege", "siege_locations.xml"));

	private static string ResolveStaticDataFile(params string[] relativeUnderStaticData)
	{
		for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
		{
			string candidate = Path.Combine([directory.FullName, "game-server", "data", "static_data", .. relativeUnderStaticData]);
			if (File.Exists(candidate))
				return candidate;
		}
		throw new FileNotFoundException(
			$"Could not locate game-server/data/static_data/{string.Join('/', relativeUnderStaticData)} from {AppContext.BaseDirectory}");
	}
}
