using Aion.Bots.Dashboard;
using Aion.Bots.Navigation;
using Aion.Bots.Scenarios;
using Aion.Bots.Tracing;
using Aion.Bots.World;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Spawns;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Items;

namespace Aion.Simulation.Tests;

public sealed partial class SimulationFastScenarioTests
{
	private const int Altgard = 220030000, Goon = 210715, GraveRobbingFencer = 210506;

	/// <summary>
	/// NA-23 (docs/natural-ascension-altgard.md): a focused, GM-prepared Cleric encounter, diagnostic only. A level 10
	/// Cleric with the Karmic Staff and the bridge endpoint's supplies fights outside Altgard Fortress, three times,
	/// on the journey's own pull, patrol, combat and rest code: one Lehpar goon (level 10, aggressive to Asmodians;
	/// the tentacled lobnites there are MONSTER tribe and never aggro), a pair, and a target with a random-walking
	/// goon beside it (Altgard has no aggressive level 9-12 walker near the fortress, and the only walker route there
	/// is the guards'). Deaths are recorded, not failed (OD-12).
	/// AC-00 (docs/natural-altgard-leveling.md): with AC00_CLERIC_LEVEL=15 the same encounter is fought by a Cleric of that
	/// level, with every skill the server auto-learns up to it, against Leg 3's grave robbing fencers (level 14), so the
	/// combat trace shows which ranks the rotation casts.
	/// AB-07: with AB07_STAGES=1 (level 16, or AC00_CLERIC_LEVEL) the stages are Leg 4's groups instead: a bigfoot mosbear with
	/// a grove malodor, Komu Silverclaw (SEASONED L17), Comrade Sumarhon (SEASONED L15) with two fencers, Infernus (EXPERT L13),
	/// and two Feral Black Claw Sharpeyes (SEASONED L17) with a black claw warrior (SEASONED L16).
	/// AK-07: with AK07_STAGES=1 (level 19, or AC00_CLERIC_LEVEL) the stages are Leg 5's groups, placed as the spawn file has
	/// them: a MuMu Village pull (a looklook pair with a lookout); Chieftain Manumumu (EXPERT L17) with his lookout 4 m away and
	/// two looklooks 11 m away; and a grave robbing sentry with two fencers (L14) from Sumarhon's ground.
	/// AG-06: with AG06_STAGES=1 (level 20, or AC00_CLERIC_LEVEL) the stages are Leg 6's: Minushan's Spirit (SEASONED L18) alone,
	/// as Q2252 raises it; three peckus (L18) placed as the three around the Bones of Minushan, walking as the spawn file has
	/// them (a pecku below 35% calls those within 15 m of its target); a blackened angolem (L17) with the shardling the
	/// spawn file puts 11.6 m from it; and the Spirit at the bones with those three peckus where the spawn file has them around
	/// the bones, the fight Q2252 brings.
	/// AE-05: AE05_STAGES=1 uses a free account at level 21 for warrior/arachna and warrior/sleekpaw mixes, Gabacha alone,
	/// and Gabacha's nearby warriors and sleekpaw. Relative placement comes from the shipped swamp spots.
	/// AO-03: AO03_STAGES=1 uses account 209 at level 21 for both pecku kinds, a bodyguard/shaman pair, Gattban alone,
	/// and Gattban with the nearest bodyguard and shaman, at the shipped relative spacing.
	/// AH-03: AH03_STAGES=1 uses account 212 at level 21 for both hero/sorcerer pairs, a mist/splash mix and a wild
	/// tayga pair, with shipped relative spacing and random walks.
	/// </summary>
	[SkippableFact]
	public async Task NaturalClericEncounterRunsOnce()
	{
		Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
		Skip.IfNot(Environment.GetEnvironmentVariable("NA23_CLERIC_ENCOUNTER") == "1",
			"Set NA23_CLERIC_ENCOUNTER=1 for the NA-23 Cleric encounter diagnostic.");
		string root = Aion.GameServer.TestKit.RealStaticData.RepoRoot();
		bool basfelt = Environment.GetEnvironmentVariable("AB07_STAGES") == "1";
		bool mumu = Environment.GetEnvironmentVariable("AK07_STAGES") == "1";
		bool berth = Environment.GetEnvironmentVariable("AG06_STAGES") == "1";
		bool eastGate = Environment.GetEnvironmentVariable("AE05_STAGES") == "1";
		bool observatory = Environment.GetEnvironmentVariable("AO03_STAGES") == "1";
		bool heart = Environment.GetEnvironmentVariable("AH03_STAGES") == "1";
		int level = int.TryParse(Environment.GetEnvironmentVariable("AC00_CLERIC_LEVEL"), out int asked) ? asked
			: heart || observatory || eastGate ? 21 : berth ? 20 : mumu ? 19 : basfelt ? 16 : 10;
		Assert.InRange(level, 10, 21);
		int monsterId = level == 10 ? Goon : GraveRobbingFencer;
		string item = heart ? "ah03" : observatory ? "ao03" : eastGate ? "ae05" : berth ? "ag06" : mumu ? "ak07" : basfelt ? "ab07" : level == 10 ? "na23" : "ac00";
		string run = Environment.GetEnvironmentVariable("AION_SIM_RUN_ID") ?? $"{item}-cleric-encounter-l{level}-s{fixture.Seed}";
		string directory = Path.Combine(root, "run", item, run);
		Directory.CreateDirectory(directory);
		string tracePath = Path.Combine(directory, $"{run}.trace.jsonl");
		if (File.Exists(tracePath)) throw new IOException($"Preserving existing NA-23 trace: {tracePath}");
		int accountId = heart ? 212 : observatory ? 209 : eastGate ? 206 : 41;
		using var trace = BotActionTraceWriter.Open(tracePath, run, "b01", $"sim-player-{accountId}",
			virtualTime: () => TimeSpan.FromMilliseconds(fixture.Clock.NowMillis));
		Console.WriteLine($"NA-23 trace: {tracePath}");
		using var policy = NewPolicy("NA23", includeHistory: true);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(20));
		CancellationToken token = timeout.Token;
		await using var session = new SimulationL0Session(fixture, policy, "b01", accountId,
			"Asimclric", Race.ASMODIANS, trace, tracePath);
		var dashboard = new LiveBotDashboardState();
		int dashboardPort = heart || observatory || eastGate ? int.Parse(Environment.GetEnvironmentVariable("AION_BOT_DASHBOARD_PORT") ?? "17880") : 0;
		await using var dashboardHost = new LiveBotDashboardHost(run, [heart ? "AH-03" : observatory ? "AO-03" : "AE-05"], dashboard, dashboardPort);
		session.Dashboard = dashboard;
		if (dashboardHost.Enabled) Console.WriteLine($"Cleric encounter dashboard: {dashboardHost.Url}");
		NaturalAscensionContract bridge = NaturalAscensionContract.LoadDefault();
		// The stage ground: open ground south-west of the fortress, 40 m east of a natural goon camp (cleared per stage).
		const float centerX = 1622.13f, centerY = 1947.95f;
		BotNavigationGeometry Geometry() => BotNavigationGeometry.ForServerWorld(
			fixture.World.GetPlayer(session.CharacterId).GetInstanceId(), Race.ASMODIANS);
		var runtime = new NaturalJourneyRuntime(root, "SIM-na23", fixture.Seed,
			fixture.DataManager.StaticData, () => fixture.Clock.NowMillis, fixture.Epoch,
			Geometry, EnterAsync, policy.AssertClean, () => policy.SnapshotProblems(), trace, dashboard)
		{
			PrepareCourseAsync = PrepareAsync,
			PrepareEncounterStageAsync = PrepareStageAsync,
			EncounterNpcIds = heart ? [210588, 210722, 210723, 210724, 210575, 210577, 210522, 210547] : null,
			EncounterStages = heart ? ["hero-sorcerer-l18", "hero-sorcerer-l19", "mist-splash", "wild-taygas"]
				: observatory ? ["observatory-peckus", "bodyguard-shaman", "gattban", "gattban-camp"]
				: eastGate ? ["warrior-arachnas", "warrior-sleekpaw", "gabacha", "gabacha-camp"]
				: berth ? ["minushan-spirit", "pecku-pack", "angolem-shardling", "minushan-bones"]
				: mumu ? ["mumu-pull", "manumumu", "sentry-fencers"]
				: basfelt ? ["mosbear-pair", "komu", "sumarhon-camp", "infernus", "sharpeyes"] : null,
			// NA-21: the approved help items (OD-13), unless NA_HELP_ITEMS=0.
			SupplyHelpItemAsync = NaturalHelpItemSupply.Enabled(Environment.GetEnvironmentVariable(NaturalHelpItemSupply.Switch))
				? SupplyHelpItemAsync : null,
		};
		await new NaturalIshalgenJourney(session, runtime, new NaturalJourneyOptions(ClericEncounter: true)).RunAsync(token);
		Assert.True(File.Exists(Path.Combine(directory, "na23-summary.json")));

		async Task SupplyHelpItemAsync(int itemId, long count, CancellationToken supplyToken)
		{
			NaturalHelpItemSupply.RequireApproved(itemId, count);
			Assert.Equal(0, ItemService.AddItem(fixture.World.GetPlayer(session.CharacterId), itemId, count, allowInventoryOverflow: true));
			await session.SynchronizeAsync(supplyToken);
		}

		async Task<bool> EnterAsync(CancellationToken enterToken)
		{
			session.BeginStep("na23-create", "create-fresh-character-for-the-cleric-encounter");
			await session.LoginAndAuthenticateAsync(enterToken);
			await session.CreateCharacterAsync(enterToken, PlayerClass.PRIEST);
			await session.EnterWorldAsync(enterToken);
			await session.WaitForPacketAsync(typeof(SM_PLAY_MOVIE), enterToken);
			await session.SynchronizeAsync(enterToken);
			return false;
		}

		// A level 10 Cleric as the bridge leaves it: Priest skills 1-10 and the Cleric's (ClassChangeService learns
		// 9..level), the Karmic Staff worn, the endpoint supplies, bound at Altgard Fortress, full HP and MP.
		async Task PrepareAsync(CancellationToken prepareToken)
		{
			Player player = fixture.World.GetPlayer(session.CharacterId);
			// Ascend first (it completes Q2008 and makes a Daeva, as SETPRO14 does); a non-Daeva is capped at level 9.
			Assert.True(ClassChangeService.SetClass(player, PlayerClass.CLERIC, validate: false, updateDaevaStatus: true));
			player.GetCommonData().SetLevel(level);
			SkillLearnService.LearnNewSkills(player, 1, level);
			(int Id, long Count)[] supplies =
			[
				(101500498, 1), (162000002, 32), (162000007, 106), (162000052, 10), (162000053, 12),
				(169300003, 30), (160002273, 5), (164002116, 50), (164002117, 50), (164002118, 50),
			];
			foreach ((int id, long count) in supplies)
				Assert.Equal(0, ItemService.AddItem(player, id, count, allowInventoryOverflow: true));
			var staff = player.GetInventory().GetItems().Last(item => item.GetItemId() == 101500498);
			Assert.NotNull(player.GetEquipment().EquipItem(staff.GetObjectId(), 1));
			player.SetBindPoint(new BindPointPosition(bridge.Bind.MapId, bridge.Bind.Position[0], bridge.Bind.Position[1],
				bridge.Bind.Position[2], 0));
			// AB-07: tell the client, as binding at the obelisk does; otherwise it still believes its bind is in Ishalgen and a
			// revive waits for a world reload that never comes.
			Aion.GameServer.Services.Teleport.TeleportService.SendObeliskBindPoint(player);
			session.Api.World.BeginWorldReload();
			await TeleportForSetupAsync(session, player, Altgard, bridge.Bind.Position[0] + 3, bridge.Bind.Position[1],
				bridge.Bind.Position[2], prepareToken);
			session.AcceptTeleportPosition();
			player.GetLifeStats().SetCurrentHp(player.GetLifeStats().GetMaxHp());
			player.GetLifeStats().SetCurrentMp(player.GetLifeStats().GetMaxMp());
			await session.SynchronizeAsync(prepareToken);
			Assert.Equal(level, session.Api.World.Level);
			Assert.Equal(Altgard, session.Api.World.MapId);
			session.TraceDiagnostic("na23-prepared", new Dictionary<string, object?>
			{
				["class"] = player.GetPlayerClass().ToString(), ["level"] = player.GetLevel(),
				["skills"] = session.Api.World.Skills.Keys.Order().ToArray(), ["maxHp"] = session.Api.World.MaxHp,
				["maxMp"] = session.Api.World.MaxMp, ["dp"] = session.Api.World.CurrentDp,
			});
		}

		// Per stage: clear aggressive monsters around the stage ground, spawn the stage's goons (no respawn), and put
		// the Cleric 28 m south of them on the ground, as it would arrive walking.
		async Task PrepareStageAsync(string stage, CancellationToken stageToken)
		{
			Player player = fixture.World.GetPlayer(session.CharacterId);
			var instance = fixture.World.GetWorldMap(Altgard).GetMainWorldMapInstance();
			foreach (var npc in instance.GetNpcs().Where(npc =>
				MathF.Sqrt(MathF.Pow(npc.GetX() - centerX, 2) + MathF.Pow(npc.GetY() - centerY, 2)) <= 60 &&
				NaturalHostility.IsAggressive(npc.GetObjectTemplate(), fixture.DataManager.StaticData.TribeRelations,
					TribeClass.PC_DARK)).ToArray())
				fixture.World.Despawn(npc);
			BotNavigationGeometry geometry = Geometry();
			(float Dx, float Dy, int RandomWalk, int NpcId)[] monsters = stage switch
			{
				"single" => [(0, 0, 0, monsterId)],
				"pair" => [(-2.5f, 0, 0, monsterId), (2.5f, 0, 0, monsterId)],
				"patrol" => [(0, 0, 0, monsterId), (8, 7, 10, monsterId)],
				// AB-07: Leg 4's groups.
				"mosbear-pair" => [(-2.5f, 0, 0, 210441), (2.5f, 0, 0, 210444)],
				"komu" => [(0, 0, 0, 210442)],
				"sumarhon-camp" => [(0, 0, 0, 210510), (-4, 2, 0, GraveRobbingFencer), (4, 2, 0, GraveRobbingFencer)],
				"infernus" => [(0, 0, 0, 211621)],
				"sharpeyes" => [(-3, 0, 0, 210457), (3, 0, 0, 210457), (0, 5, 0, 214039)],
				// AK-07: Leg 5's groups (looklooks 210451 L15 and 210452 SEASONED L16, lookout 210454 L16, sentry 210504).
				"mumu-pull" => [(-2.5f, 0, 0, 210452), (2.5f, 0, 0, 210451), (0, 5, 0, 210454)],
				"manumumu" => [(0, 0, 0, 210598), (0, 4, 0, 210454), (-7.8f, 7.8f, 0, 210451), (7.8f, 7.8f, 0, 210451)],
				"sentry-fencers" => [(0, 0, 0, 210504), (-4, 2, 0, GraveRobbingFencer), (4, 2, 0, GraveRobbingFencer)],
				// AG-06: Leg 6's groups (the peckus around the bones are spawn-file spots with random_walk 8, about their centre).
				"minushan-spirit" => [(0, 0, 0, 210634)],
				"pecku-pack" => [(-8.5f, -9.3f, 8, 212345), (-2.8f, 9.9f, 8, 212345), (11.3f, -0.6f, 8, 212345)],
				"angolem-shardling" => [(0, 0, 0, 210487), (-11.5f, -1.6f, 0, 210489)],
				"minushan-bones" => [(0, 0, 0, 210634), (4.7f, -7.4f, 8, 212345), (-9.4f, 3.1f, 8, 212345), (-15.1f, -16.0f, 8, 212345)],
				// AE-05: offsets from (1673.02,2151.74), (1640.96,2236.54) and Gabacha (1652.6,2225.49).
				"warrior-arachnas" => [(0, 0, 0, 210459), (9.6f, -15.6f, 0, 210754), (-8, -21.2f, 0, 210447)],
				"warrior-sleekpaw" => [(0, 0, 0, 210460), (-8.7f, 6, 0, 210502)],
				"gabacha" => [(0, 0, 0, 216893)],
				"gabacha-camp" => [(0, 0, 0, 216893), (4.9f, -13.5f, 0, 210459), (-11.6f, 11.1f, 0, 210460), (-20.3f, 17, 0, 210502)],
				// AO-03: closest mixed pecku spots (1601.33,2637.84)/(1629.26,2642.17); shared guard/shaman patrol origin
				// (1625.86,2592.8); Gattban (1721.12,2636.07) and the nearest guard (1717.69,2630.36)/shaman (1698.92,2639.26).
				"observatory-peckus" => [(0, 0, 0, 210526), (27.93f, 4.33f, 0, 210527)],
				"bodyguard-shaman" => [(0, 0, 0, 210528), (0, 0, 0, 210530)],
				"gattban" => [(0, 0, 0, 210532)],
				"gattban-camp" => [(0, 0, 0, 210532), (-3.43f, -5.71f, 0, 210528), (-22.2f, 3.19f, 0, 210530)],
				// AH-03: closest hero/sorcerer pairs at (2562.55,1608.04)/(2556.14,1620.43) and
				// (2814.34,1675.8)/(2816.96,1685.43); mist (2714.3,1686.3), splash L18 (2744.45,1697.57),
				// splash L19 (2716.74,1707.55); taygas (2399.19,2073.48)/(2413.86,2084.59).
				"hero-sorcerer-l18" => [(0, 0, 10, 210588), (-6.41f, 12.39f, 0, 210723)],
				"hero-sorcerer-l19" => [(0, 0, 0, 210722), (2.62f, 9.63f, 0, 210724)],
				"mist-splash" => [(0, 0, 10, 210575), (30.15f, 11.27f, 10, 210577), (2.44f, 21.25f, 10, 210522)],
				"wild-taygas" => [(0, 0, 0, 210547), (14.67f, 11.11f, 0, 210547)],
				_ => throw new ArgumentOutOfRangeException(nameof(stage)),
			};
			foreach ((float dx, float dy, int randomWalk, int npcId) in monsters)
			{
				BotPosition ground = geometry.SnapToGround(Altgard, new BotPosition(centerX + dx, centerY + dy, 258.4f, 0))
					?? throw new InvalidDataException($"No ground for the {stage} goon.");
				var template = new SpawnTemplate(new SpawnGroup(Altgard, npcId, 0, null), ground.X, ground.Y, ground.Z, 30,
					randomWalk, null, 0);
				Assert.True(Aion.GameServer.SpawnEngine.SpawnEngine.SpawnObject(template, instance.GetInstanceId())?.IsSpawned());
			}
			// Start as a Cleric arriving on foot: ground on the stage's own navmesh island, 28 m from the monsters, with line
			// of sight to them (a snapped point can land on a rock top the navmesh keeps apart). NA-23 found that a route
			// from the fortress obelisk out to here is rejected by the collision geometry (GeometryRejected; the gate).
			var stageCenter = geometry.SnapToGround(Altgard, new BotPosition(centerX, centerY, 258.4f, 0))
				?? throw new InvalidDataException("No ground at the NA-23 stage.");
			BotPosition start = geometry.GroundAround(Altgard, stageCenter, [28f], sectors: 24)
				.Where(point => geometry.HasLineOfSight(Altgard, point, stageCenter) &&
					geometry.FindJourneyPath(Altgard, point, stageCenter).Count > 0)
				.OrderBy(point => point.Y).FirstOrDefault();
			if (start == default) throw new InvalidDataException("No open ground 28 m from the NA-23 stage.");
			session.Api.World.BeginWorldReload();
			await TeleportForSetupAsync(session, player, Altgard, start.X, start.Y, start.Z, stageToken, heading: 30);
			session.AcceptTeleportPosition();
			await session.SynchronizeAsync(stageToken);
			session.TraceDiagnostic("na23-stage-prepared", new Dictionary<string, object?>
			{
				["stage"] = stage, ["start"] = start, ["monsters"] = monsters.Length,
				["hp"] = session.Api.World.CurrentHp, ["mp"] = session.Api.World.CurrentMp,
			});
		}
	}
}
