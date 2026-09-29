using Aion.Bots.Dashboard;
using Aion.Bots.Navigation;
using Aion.Bots.Scenarios;
using Aion.Bots.Tracing;
using Aion.Bots.World;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;

namespace Aion.Simulation.Tests;

public sealed partial class SimulationFastScenarioTests
{
	/// <summary>
	/// AF-06 (docs/natural-altgard-leveling.md): Q24011's air kills. A level 10 Daeva Cleric with Q24011 at var 2 (as after
	/// Borender's talk; GM setup, the talk itself is AF-07's) takes off beside the obelisk and shoots Abyss Fungus down
	/// with Smite from the air until the quest reaches its reward, landing on Borender's rock to refill flight time
	/// whenever <see cref="NaturalAirCombatPolicy"/> says the next fungus is not affordable.
	/// </summary>
	[SkippableFact]
	public async Task AltgardAirCombatCompletesTheFungusKills()
	{
		Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
		const int altgard = 220030000, fungusQuest = 24011;
		using var policy = NewPolicy("AF06", includeHistory: false);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(10));
		CancellationToken token = timeout.Token;
		string root = Aion.GameServer.TestKit.RealStaticData.RepoRoot();
		NaturalAltgardContract contract = NaturalAltgardContract.LoadDefault();
		IReadOnlyList<NaturalFlyZone> zones = NaturalFlyZone.Load(Path.Combine(root, "game-server/data/static_data/zones/zones_220030000.xml"));
		string directory = Path.Combine(root, "run", "af06");
		Directory.CreateDirectory(directory);
		string tracePath = Path.Combine(directory, $"af06-s{fixture.Seed}-{DateTime.UtcNow:yyyyMMddHHmmss}.trace.jsonl");
		using var trace = BotActionTraceWriter.Open(tracePath, "af06", "b01", "sim-player-47",
			virtualTime: () => TimeSpan.FromMilliseconds(fixture.Clock.NowMillis));
		await using var session = new SimulationL0Session(fixture, policy, "b01", 47, "Asimfungus", Race.ASMODIANS, trace, tracePath);
		session.BeginStep("s00", "login-create-enter-and-setup");
		await session.LoginAndAuthenticateAsync(token);
		await session.CreateCharacterAsync(token, PlayerClass.PRIEST);
		await session.EnterWorldAsync(token);
		await session.SynchronizeAsync(token);
		Player player = fixture.World.GetPlayer(session.CharacterId);
		Assert.True(ClassChangeService.SetClass(player, PlayerClass.CLERIC, validate: false, updateDaevaStatus: true));
		player.GetCommonData().SetLevel(10);
		SkillLearnService.LearnNewSkills(player, 1, 10);
		var fungusState = new QuestState(fungusQuest, QuestStatus.START, 2, 0, 0, null, null, null);
		Assert.True(player.GetQuestStateList().AddQuest(fungusQuest, fungusState));
		Aion.GameServer.Utils.PacketSendUtility.SendPacket(player,
			new Aion.GameServer.Network.Aion.ServerPackets.SM_QUEST_ACTION(Aion.GameServer.Network.Aion.ServerPackets.SM_QUEST_ACTION.ActionType.ADD, fungusState));
		BotNavigationGeometry geometry = BotNavigationGeometry.ForServerWorld(
			fixture.World.GetWorldMap(altgard).GetMainWorldMapInstance().GetInstanceId(), Race.ASMODIANS);
		BotPosition ground = geometry.SnapToGround(altgard,
			new BotPosition(contract.Hub.Anchor[0] - 3, contract.Hub.Anchor[1], contract.Hub.Anchor[2] + 1, 0))
			?? throw new InvalidDataException("No ground beside the Altgard obelisk.");
		await TeleportForSetupAsync(session, player, altgard, ground.X, ground.Y, ground.Z, token);
		await session.SynchronizeAsync(token);
		NaturalAltgardStep borender = contract.Steps.First(step => step.Area == "borender-rock");
		BotPosition rock = geometry.SnapToGround(altgard, new BotPosition(borender.Position[0] - 2.5f, borender.Position[1], borender.Position[2] + 3, 0))
			?? throw new InvalidDataException("No rock top beside Borender.");
		var landing = new NaturalLandingTarget("platform", rock);
		var runtime = new NaturalJourneyRuntime(root, "SIM-af06", fixture.Seed, fixture.DataManager.StaticData,
			() => fixture.Clock.NowMillis, fixture.Epoch, () => geometry, _ => Task.FromResult(false), policy.AssertClean,
			() => policy.SnapshotProblems(), trace, new LiveBotDashboardState());
		session.BeginStep("s01", "air-combat");
		NaturalAirCombat.Outcome outcome = await NaturalAirCombat.RunAsync(session, geometry, altgard, zones, contract.Flight.WaterLevel,
			landing, rock.Z + 8, fungusQuest,
			(origin, skill, level, target) => runtime.CreateSpellCast(session.Api.World, origin, skill, level, target),
			() => fixture.Clock.NowMillis, token);
		Assert.Equal(QuestStatus.REWARD, player.GetQuestStateList().GetQuestState(fungusQuest).GetStatus());
		Assert.False(player.IsDead());
		Assert.False(player.IsFlying());
		Assert.Equal(contract.AirKills.KillsAfterBorender, outcome.Kills);
		Console.WriteLine($"AF-06 done: {outcome.Kills} kills in {outcome.Sorties} sorties, {outcome.Missed} missed; shooting times " +
			$"{string.Join(", ", outcome.ShootingSeconds.Select(value => value.ToString("F1")))} s; lands with FP {outcome.LandedFp} at " +
			$"({player.GetX():F1}, {player.GetY():F1}, {player.GetZ():F1})");
		policy.AssertClean();
	}
}
