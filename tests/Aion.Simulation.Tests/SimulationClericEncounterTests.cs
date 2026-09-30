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
	/// </summary>
	[SkippableFact]
	public async Task NaturalClericEncounterRunsOnce()
	{
		Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
		Skip.IfNot(Environment.GetEnvironmentVariable("NA23_CLERIC_ENCOUNTER") == "1",
			"Set NA23_CLERIC_ENCOUNTER=1 for the NA-23 Cleric encounter diagnostic.");
		string root = Aion.GameServer.TestKit.RealStaticData.RepoRoot();
		bool basfelt = Environment.GetEnvironmentVariable("AB07_STAGES") == "1";
		int level = int.TryParse(Environment.GetEnvironmentVariable("AC00_CLERIC_LEVEL"), out int asked) ? asked : basfelt ? 16 : 10;
		Assert.InRange(level, 10, 20);
		int monsterId = level == 10 ? Goon : GraveRobbingFencer;
		string item = basfelt ? "ab07" : level == 10 ? "na23" : "ac00";
		string run = Environment.GetEnvironmentVariable("AION_SIM_RUN_ID") ?? $"{item}-cleric-encounter-l{level}-s{fixture.Seed}";
		string directory = Path.Combine(root, "run", item, run);
		Directory.CreateDirectory(directory);
		string tracePath = Path.Combine(directory, $"{run}.trace.jsonl");
		if (File.Exists(tracePath)) throw new IOException($"Preserving existing NA-23 trace: {tracePath}");
		using var trace = BotActionTraceWriter.Open(tracePath, run, "b01", "sim-player-41",
			virtualTime: () => TimeSpan.FromMilliseconds(fixture.Clock.NowMillis));
		Console.WriteLine($"NA-23 trace: {tracePath}");
		using var policy = NewPolicy("NA23", includeHistory: true);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(20));
		CancellationToken token = timeout.Token;
		await using var session = new SimulationL0Session(fixture, policy, "b01", accountId: 41,
			"Asimclric", Race.ASMODIANS, trace, tracePath);
		NaturalAscensionContract bridge = NaturalAscensionContract.LoadDefault();
		// The stage ground: open ground south-west of the fortress, 40 m east of a natural goon camp (cleared per stage).
		const float centerX = 1622.13f, centerY = 1947.95f;
		BotNavigationGeometry Geometry() => BotNavigationGeometry.ForServerWorld(
			fixture.World.GetPlayer(session.CharacterId).GetInstanceId(), Race.ASMODIANS);
		var runtime = new NaturalJourneyRuntime(root, "SIM-na23", fixture.Seed,
			fixture.DataManager.StaticData, () => fixture.Clock.NowMillis, fixture.Epoch,
			Geometry, EnterAsync, policy.AssertClean, () => policy.SnapshotProblems(), trace, new LiveBotDashboardState())
		{
			PrepareCourseAsync = PrepareAsync,
			PrepareEncounterStageAsync = PrepareStageAsync,
			EncounterStages = basfelt ? ["mosbear-pair", "komu", "sumarhon-camp", "infernus", "sharpeyes"] : null,
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
