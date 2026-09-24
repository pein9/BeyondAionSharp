using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Aion.Bots.Navigation.NavMesh;

namespace Aion.NavBake;

/// <summary>
/// Bakes, checks and renders the playtest bots' navmeshes (<c>game-server/data/nav</c>).
/// <code>
/// dotnet run --project tools/Aion.NavBake -- bake   [--maps 220010000,210010000|starter|all] [--out DIR] [--threads N]
/// dotnet run --project tools/Aion.NavBake -- check  [--maps ...] [--rebake]
/// dotnet run --project tools/Aion.NavBake -- render --maps 220010000 [--out DIR]
/// dotnet run --project tools/Aion.NavBake -- graph  [--maps ...]
/// </code>
/// Generated files are never hand-edited; change the inputs or settings and bake again.
/// </summary>
internal static class Program
{
	public static readonly int[] StarterMaps = [210010000, 220010000];

	public static async Task<int> Main(string[] args)
	{
		try
		{
			var options = Options.Parse(args);
			string root = options.RepoRoot ?? FindRepoRoot();
			string navDir = options.NavDir ?? (options.Command is "bake" or "check" ? options.Output : null) ?? BotNavMeshSet.DefaultDirectory(root);
			string cache = Path.Combine(root, "run", "navbake-cache");
			IReadOnlySet<int>? requested = options.Maps switch
			{
				null or "starter" => StarterMaps.ToHashSet(),
				"all" => null,
				"baked" => new BotNavMeshSet(navDir).AvailableMapIds().ToHashSet(),
				string list => list.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
					.Select(s => int.Parse(s, CultureInfo.InvariantCulture)).ToHashSet(),
			};
			var watch = Stopwatch.StartNew();
			BotNavWorld world = await BotNavWorld.LoadAsync(root, cache, requested, CancellationToken.None);
			Console.WriteLine($"Loaded static data and geodata in {watch.Elapsed.TotalSeconds:F1} s");
			int[] maps = (requested ?? world.MapIds.ToHashSet()).Where(world.HasGeometry).Order().ToArray();
			return options.Command switch
			{
				"bake" => Bake(world, maps, navDir, options),
				"check" => Check(world, maps, navDir, options),
				"render" => NavRender.Run(world, maps, navDir, options.Output ?? Path.Combine(root, "run", "nav-render")),
				"graph" => NavGraphCommand.Run(world, maps, navDir),
				"points" => NavDiag.Points(world, maps[0], navDir),
				"gap" => NavDiag.Gap(world, maps[0], navDir),
				"island" => NavDiag.Island(world, maps[0], navDir),
				"profile" => NavDiag.Profile(world, maps[0], navDir),
				"columns" => NavDiag.Columns(world, maps[0]),
				"diag" => NavDiag.Run(world, maps[0], navDir, options.Threads, int.Parse(options.Output!, CultureInfo.InvariantCulture)),
				"validate" => NavValidate.Run(world, maps[0], navDir, options.Bin ?? throw new ArgumentException("--bin"), options.Output),
				"hazards" => NavMeasure.Hazards(world, maps[0], navDir, options.Legacy, options.Threads > 0 ? options.Threads : 40),
				"measure" => NavMeasure.Run(world, maps, navDir, options.Legacy, options.Output, options.Threads > 0 ? options.Threads : int.MaxValue, options.Quick),
				_ => throw new ArgumentException("Unknown command " + options.Command),
			};
		}
		catch (Exception error)
		{
			Console.Error.WriteLine(error);
			return 1;
		}
	}

	public static BotNavMeshSettings Settings(Options options)
	{
		var settings = new BotNavMeshSettings();
		foreach (string pair in options.Set)
		{
			string[] kv = pair.Split('=');
			float v = float.Parse(kv[1], CultureInfo.InvariantCulture);
			settings = kv[0] switch
			{
				"cs" => settings with { CellSize = v },
				"ch" => settings with { CellHeight = v },
				"radius" => settings with { AgentRadius = v },
				"climb" => settings with { AgentMaxClimb = v },
				"height" => settings with { AgentHeight = v },
				"tile" => settings with { TileSizeCells = (int)v },
				"edgeError" => settings with { EdgeMaxError = v },
				"edgeLength" => settings with { EdgeMaxLength = v },
				"regionMin" => settings with { RegionMinSize = (int)v },
				"detailDist" => settings with { DetailSampleDistance = v },
				"merge" => settings with { FlagMergeVoxels = (int)v },
				_ => throw new ArgumentException("Unknown setting " + kv[0]),
			};
		}
		return settings;
	}

	private static int Bake(BotNavWorld world, int[] maps, string navDir, Options options)
	{
		var settings = Settings(options);
		foreach (int mapId in maps)
		{
			IReadOnlyList<BotNavVolume> roads = BotNavRoads.LoadVolumes(world.RepoRoot, mapId, settings.RoadHalfWidth);
			BotNavMeshBakeJob.Run(world, mapId, navDir, settings, roads, options.Threads, Console.WriteLine);
		}
		return 0;
	}

	/// <summary>Regeneration check: every checked-in navmesh must match current inputs and settings.
	/// With <c>--rebake</c> each map is rebaked in memory and compared byte for byte.</summary>
	private static int Check(BotNavWorld world, int[] maps, string navDir, Options options)
	{
		var settings = Settings(options);
		int stale = 0;
		foreach (int mapId in maps)
		{
			if (!BotNavMesh.Exists(navDir, mapId)) { Console.WriteLine($"{mapId}: MISSING"); stale++; continue; }
			var manifest = JsonSerializer.Deserialize<BotNavMeshManifest>(
				File.ReadAllText(BotNavMesh.ManifestPathFor(navDir, mapId)), BotNavMeshManifest.Json)!;
			IReadOnlyList<BotNavVolume> roads = BotNavRoads.LoadVolumes(world.RepoRoot, mapId, settings.RoadHalfWidth);
			var problems = new List<string>();
			if (manifest.SettingsFingerprint != settings.Fingerprint()) problems.Add("settings changed");
			if (manifest.SourceFingerprint != world.SourceFingerprint(mapId, roads)) problems.Add("inputs changed");
			string sha = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
				File.ReadAllBytes(BotNavMesh.PathFor(navDir, mapId)))).ToLowerInvariant();
			if (sha != manifest.NavMeshSha256) problems.Add("navmesh file edited");
			if (options.Rebake && problems.Count == 0)
			{
				string temp = Path.Combine(Path.GetTempPath(), "navbake-check-" + Environment.ProcessId);
				BotNavMeshManifest fresh = BotNavMeshBakeJob.Run(world, mapId, temp, settings, roads, options.Threads);
				if (fresh.NavMeshSha256 != manifest.NavMeshSha256) problems.Add("rebake differs");
				Directory.Delete(temp, true);
			}
			Console.WriteLine($"{mapId}: {(problems.Count == 0 ? "ok" : "STALE (" + string.Join(", ", problems) + ")")}");
			if (problems.Count > 0) stale++;
		}
		if (stale > 0) Console.WriteLine($"{stale} navmesh(es) need `dotnet run --project tools/Aion.NavBake -- bake --maps ...`.");
		return stale == 0 ? 0 : 1;
	}

	public static string FindRepoRoot()
	{
		for (string? dir = AppContext.BaseDirectory; dir != null; dir = Path.GetDirectoryName(dir))
			if (File.Exists(Path.Combine(dir, "AionServer.slnx"))) return dir;
		for (string? dir = Directory.GetCurrentDirectory(); dir != null; dir = Path.GetDirectoryName(dir))
			if (File.Exists(Path.Combine(dir, "AionServer.slnx"))) return dir;
		throw new InvalidOperationException("Run from inside the repository or pass --root.");
	}
}

internal sealed record Options(string Command, string? Maps, string? Output, string? RepoRoot, int Threads, bool Rebake, string? NavDir, bool Legacy, string? Bin, IReadOnlyList<string> Set, bool Quick)
{
	public static Options Parse(string[] args)
	{
		if (args.Length == 0) throw new ArgumentException("Usage: bake|check|render|graph [--maps ids|starter|all|baked] [--out DIR] [--threads N] [--rebake]");
		string? maps = null, output = null, root = null, navDir = null, bin = null;
		int threads = 0;
		bool rebake = false, legacy = false, quick = false;
		var set = new List<string>();
		for (int i = 1; i < args.Length; i++)
		{
			switch (args[i])
			{
				case "--maps": maps = args[++i]; break;
				case "--out": output = args[++i]; break;
				case "--root": root = args[++i]; break;
				case "--nav": navDir = args[++i]; break;
				case "--bin": bin = args[++i]; break;
				case "--set": set.Add(args[++i]); break;
				case "--quick": quick = true; break;
				case "--threads": threads = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
				case "--rebake": rebake = true; break;
				case "--legacy": legacy = true; break;
				default: throw new ArgumentException("Unknown option " + args[i]);
			}
		}
		return new Options(args[0], maps, output, root, threads, rebake, navDir, legacy, bin, set, quick);
	}
}
