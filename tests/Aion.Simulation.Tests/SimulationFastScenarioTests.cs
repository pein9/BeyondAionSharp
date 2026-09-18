using System.Diagnostics;
using System.Text.Json;
using Aion.Bots.Api;
using Aion.Bots.Movement;
using Aion.Bots.Navigation;
using Aion.Bots.Protocol;
using Aion.Bots.Scenarios;
using Aion.Bots.Timing;
using Aion.Bots.Transport;
using Aion.Bots.World;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Manager;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Controllers.Movement;
using Aion.GameServer.Dao;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Animations;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.Utils;

namespace Aion.Simulation.Tests;

[Collection(SimulationWorldCollection.Name)]
public sealed partial class SimulationFastScenarioTests(SimulationWorldFixture fixture)
{
	private readonly string allowlistPath = Path.Combine(
		Aion.GameServer.TestKit.RealStaticData.RepoRoot(), "parity-artifacts", "e2e", "log-allowlist.json");

	[SkippableFact]
	public async Task ManifestScenariosRunInFixedProcessOrder()
	{
		Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
		ScenarioTier tier = ReadTier();
		int shardCount = ReadPositiveInt("AION_SIM_SHARD_COUNT", 1);
		string processKey = Environment.GetEnvironmentVariable("AION_SIM_PROCESS_KEY") ?? "shard-00";
		ScenarioManifest manifest = ScenarioManifest.Load(ScenarioManifest.FindDefaultPath());
		IReadOnlyList<ScenarioExecution> plan = ScenarioIsolationPlanner.Plan(
			manifest.Scenarios,
			ScenarioMode.Sim,
			tier,
			shardCount,
			_ => TimeSpan.Zero);
		ScenarioExecution[] processPlan = plan
			.Where(execution => string.Equals(execution.ProcessKey, processKey, StringComparison.Ordinal))
			.ToArray();

		Assert.NotEmpty(processPlan);
		Assert.Equal(processPlan.OrderBy(execution => execution.Order), processPlan);
		var driver = new SimulationDriver(fixture.Clock);
		bool includeHistory = true;
		foreach (ScenarioExecution execution in processPlan)
		{
			await driver.PrepareScenarioAsync(execution);
			switch (execution.Scenario.Id)
			{
				case "S0":
					await RunS0Async(execution.Scenario, includeHistory);
					break;
				case "L0":
					await RunL0Async(execution, includeHistory);
					break;
				case "M1":
					await RunM1Async(execution, includeHistory);
					break;
				case "M2":
					await RunM2Async(execution.Scenario, includeHistory);
					break;
				case "M3":
					await RunM3Async(execution.Scenario, includeHistory);
					break;
				case "M4":
					await RunM4Async(execution.Scenario, includeHistory);
					break;
				case "M5":
					await RunM5Async(execution.Scenario, includeHistory);
					break;
				case "M6":
					await RunM6Async(execution.Scenario, includeHistory);
					break;
				case "M7":
					await RunM7Async(execution.Scenario, includeHistory);
					break;
				case "C1":
					await RunC1Async(execution.Scenario, includeHistory);
					break;
				case "C2":
					await RunC2Async(execution.Scenario, includeHistory);
					break;
				case "C3":
					await RunC3Async(execution.Scenario, includeHistory);
					break;
				case "C4":
					await RunC4Async(execution.Scenario, includeHistory);
					break;
				case "C5":
					await RunC5Async(execution.Scenario, includeHistory);
					break;
				case "C6":
					await RunC6Async(execution.Scenario, includeHistory);
					break;
				case "C7":
					await RunC7Async(execution.Scenario, includeHistory);
					break;
				case "C8":
					await RunC8Async(execution.Scenario, includeHistory);
					break;
				case "C9":
					await RunC9Async(execution.Scenario, includeHistory);
					break;
				case "C10":
					await RunC10Async(execution.Scenario, includeHistory);
					break;
				case "C11":
					await RunC11Async(execution.Scenario, includeHistory);
					break;
				case "C12":
					await RunC12Async(execution.Scenario, includeHistory);
					break;
				case "C13":
					await RunC13Async(execution.Scenario, includeHistory);
					break;
				case "C14":
					await RunC14Async(execution.Scenario, includeHistory);
					break;
				case "C15":
					await RunC15Async(execution.Scenario, includeHistory);
					break;
				case "Q1":
					await RunQ1Async(execution.Scenario, includeHistory);
					break;
				default:
					throw new InvalidOperationException($"SIM scenario '{execution.Scenario.Id}' has no runner.");
			}
			includeHistory = false;
		}
	}

	private async Task RunM7Async(ScenarioDefinition scenario, bool includeHistory)
	{
		using var policy = NewPolicy(scenario.Id, includeHistory);
		using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
		CancellationToken token = timeout.Token;
		await using var session = new SimulationL0Session(
			fixture, policy, "b01", accountId: 16, "Asimmovg", Race.ASMODIANS);
		session.BeginStep("s01", "login-create-enter");
		await session.LoginAndAuthenticateAsync(token);
		await session.CreateCharacterAsync(token);
		await session.EnterWorldAsync(token);
		await session.WaitForPacketAsync(typeof(SM_PLAY_MOVIE), token);

		Player player = fixture.World.GetPlayer(session.CharacterId);
		int maxHp = player.GetLifeStats().GetMaxHp();
		player.GetLifeStats().SetCurrentHp(maxHp / 2);
		int injuredHp = player.GetLifeStats().GetCurrentHp();

		session.BeginStep("s02", "sit-and-regenerate");
		await session.SendPacketAsync(session.Api.Rest(sitting: true), token);
		await session.WaitForPacketAsync(typeof(SM_EMOTION), token,
			packet => packet.Get<int>("senderObjectId") == session.CharacterId &&
				packet.Get<byte>("emotionType") == (byte)EmotionType.SIT);
		Assert.True(player.IsInState(CreatureState.RESTING));
		await session.AdvanceAsync(TimeSpan.FromMilliseconds(1700), token);
		await session.WaitForPacketAsync(typeof(SmAttackStatus), token);
		Assert.True(player.GetLifeStats().GetCurrentHp() > injuredHp);

		session.BeginStep("s03", "stand-and-toggle-walk-run");
		await session.SendPacketAsync(session.Api.Rest(sitting: false), token);
		await session.WaitForPacketAsync(typeof(SM_EMOTION), token,
			packet => packet.Get<int>("senderObjectId") == session.CharacterId &&
				packet.Get<byte>("emotionType") == (byte)EmotionType.STAND);
		Assert.False(player.IsInState(CreatureState.RESTING));

		await session.SendPacketAsync(session.Api.Walk(walking: true), token);
		await session.WaitForPacketAsync(typeof(SM_EMOTION), token,
			packet => packet.Get<int>("senderObjectId") == session.CharacterId &&
				packet.Get<byte>("emotionType") == (byte)EmotionType.WALK);
		Assert.True(player.IsInState(CreatureState.WALK_MODE));
		await session.SendPacketAsync(session.Api.Walk(walking: false), token);
		await session.WaitForPacketAsync(typeof(SM_EMOTION), token,
			packet => packet.Get<int>("senderObjectId") == session.CharacterId &&
				packet.Get<byte>("emotionType") == (byte)EmotionType.RUN);
		Assert.False(player.IsInState(CreatureState.WALK_MODE));
		policy.AssertClean();
	}

	private async Task RunM6Async(ScenarioDefinition scenario, bool includeHistory)
	{
		using var policy = NewPolicy(scenario.Id, includeHistory);
		using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
		CancellationToken token = timeout.Token;
		await using var session = new SimulationL0Session(
			fixture, policy, "b01", accountId: 15, "Asimmovf", Race.ASMODIANS);
		session.BeginStep("s01", "login-create-enter");
		await session.LoginAndAuthenticateAsync(token);
		await session.CreateCharacterAsync(token);
		await session.EnterWorldAsync(token);
		await session.WaitForPacketAsync(typeof(SM_PLAY_MOVIE), token);
		Player player = fixture.World.GetPlayer(session.CharacterId);
		player.GetCommonData().SetDaeva(true);

		session.BeginStep("s02", "director-setup-reshanta-flight");
		await TeleportForSetupAsync(session, player, 400010000, 940f, 2695f, 1628.3f, token);
		int initialFlightTime = player.GetLifeStats().GetCurrentFp();
		session.BeginStep("s03", "fly-up-and-drain-flight-time");
		await session.SendPacketAsync(session.Api.Fly(), token);
		Assert.True(player.IsInFlyState(FlyState.FLYING));
		BotPosition flightStart = session.Api.World.Position!.Value;
		var flightEnd = flightStart with { Z = flightStart.Z + 10 };
		await session.ExecuteMovementAsync(new BotMover(session.Api.World, session.Api.Timing)
			.CreateFlightPlan([flightEnd]), token);
		await session.AdvanceAsync(TimeSpan.FromSeconds(1.1), token);
		DecodedBotServerPacket drained = await session.WaitForPacketAsync(typeof(SM_FLY_TIME), token,
			packet => packet.Get<int>("currentFp") < initialFlightTime);
		Assert.True(drained.Get<int>("currentFp") < drained.Get<int>("maxFp"));
		Assert.InRange(MathF.Abs(player.GetZ() - flightEnd.Z), 0, 0.05f);

		session.BeginStep("s04", "land");
		await session.SendPacketAsync(session.Api.Land(), token);
		await session.WaitForPacketAsync(typeof(SM_EMOTION), token,
			packet => packet.Get<int>("senderObjectId") == session.CharacterId &&
				packet.Get<byte>("emotionType") == (byte)EmotionType.LAND);
		Assert.False(player.IsFlying());
		await session.AdvanceAsync(TimeSpan.FromSeconds(10), token);

		// Cross the first Primum ring through its centre while descending. The endpoints are symmetric around
		// the Java fly-ring plane, so the ordinary CM_MOVE glide stream—not a position mutation—triggers it.
		var glideStart = new BotPosition(958.03f, 2703.21f, 1634.66f, 0);
		var glideEnd = new BotPosition(961.23f, 2687.78f, 1621.88f, 0);
		session.BeginStep("s05", "director-setup-ring-ledge");
		await TeleportForSetupAsync(session, player, 400010000, glideStart.X, glideStart.Y, glideStart.Z, token);
		Assert.False(player.GetEffectController().HasAbnormalEffect(265));
		session.BeginStep("s06", "glide-off-ledge-through-fly-ring");
		await session.ExecuteMovementAsync(new BotMover(session.Api.World, session.Api.Timing)
			.CreateGlidePlan([glideEnd], glideStart,
				session.Api.World.MovementSpeed ?? throw new InvalidOperationException("No movement speed.")), token);
		Assert.True(player.GetEffectController().HasAbnormalEffect(265));

		session.BeginStep("s07", "director-setup-gelkmaros-windstream");
		await TeleportForSetupAsync(session, player, 220070000, 1888.25f, 2847.27f, 554.99f, token);
		DecodedBotServerPacket announce = await session.WaitForPacketAsync(typeof(SM_WINDSTREAM_ANNOUNCE), token,
			packet => packet.Get<int>("streamId") == 1);
		Assert.Equal(220070000, announce.Get<int>("mapId"));
		session.BeginStep("s08", "ride-windstream");
		await session.SendPacketAsync(GameClientPackets.Windstream(1, 0, 0), token);
		DecodedBotServerPacket entered = await session.WaitForPacketAsync(typeof(SM_WINDSTREAM), token);
		Assert.Equal(0, entered.Get<int>("state"));
		Assert.True(player.IsUsingFlightTransporterOrWindstream());
		await session.SendPacketAsync(GameClientPackets.Windstream(1, 0, 1), token);
		await session.WaitForPacketAsync(typeof(SM_EMOTION), token,
			packet => packet.Get<int>("senderObjectId") == session.CharacterId &&
				packet.Get<byte>("emotionType") == (byte)EmotionType.WINDSTREAM);
		BotPosition streamStart = session.Api.World.Position!.Value;
		await session.ExecuteMovementAsync(new BotMover(session.Api.World, session.Api.Timing)
			.CreateFlightPlan([streamStart with { X = streamStart.X + 10 }]), token);
		await session.SendPacketAsync(GameClientPackets.Windstream(1, 10, 3), token);
		DecodedBotServerPacket exited = await session.WaitForPacketAsync(typeof(SM_WINDSTREAM), token);
		Assert.Equal(3, exited.Get<int>("state"));
		Assert.False(player.IsUsingFlightTransporterOrWindstream());
		policy.AssertClean();
	}

	private async Task TeleportForSetupAsync(SimulationL0Session session, Player player, int mapId,
		float x, float y, float z, CancellationToken token)
	{
		int instanceId = fixture.World.GetWorldMap(mapId).GetMainWorldMapInstance().GetInstanceId();
		bool reloadMap = player.GetWorldId() != mapId || player.GetInstanceId() != instanceId;
		TeleportService.TeleportTo(player, mapId, instanceId, x, y, z, 0, TeleportAnimation.NONE);
		await session.DrainServerPacketsAsync(token);
		try
		{
			if (reloadMap)
			{
				await session.WaitForPacketAsync(typeof(SM_PLAYER_SPAWN), token,
					packet => packet.Get<int>("worldId") == mapId);
			}
			else
			{
				await session.WaitForPacketAsync(typeof(SM_CHANNEL_INFO), token);
			}
		}
		catch (OperationCanceledException)
		{
			throw new TimeoutException($"Setup teleport to {mapId}/{instanceId} produced no spawn; " +
				$"server={player.GetWorldId()}/{player.GetInstanceId()} ({player.GetX():F2},{player.GetY():F2},{player.GetZ():F2}) " +
				$"spawned={player.IsSpawned()} packets={string.Join(',', session.PacketTypes.TakeLast(20))}");
		}
		await session.WaitForPacketAsync(typeof(SM_PLAYER_INFO), token,
			packet => packet.Get<int>("objectId") == session.CharacterId);
		session.AcceptTeleportPosition();
	}

	private async Task RunM5Async(ScenarioDefinition scenario, bool includeHistory)
	{
		using var policy = NewPolicy(scenario.Id, includeHistory);
		using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
		CancellationToken token = timeout.Token;
		await using var session = new SimulationL0Session(
			fixture, policy, "b01", accountId: 14, "Asimmove", Race.ASMODIANS);
		session.BeginStep("s01", "login-create-enter");
		await session.LoginAndAuthenticateAsync(token);
		await session.CreateCharacterAsync(token);
		await session.EnterWorldAsync(token);
		await session.WaitForPacketAsync(typeof(SM_PLAY_MOVIE), token);

		var statueApproach = new BotPosition(855.5f, 2218.0f, 265.56f, 30);
		session.BeginStep("s02", "walk-to-ishalgen-teleport-statue");
		await session.MoveToPositionAsync(statueApproach, token);
		int statue = await session.WaitForNpcAsync(730532, token);
		session.BeginStep("s03", "teleport-to-anturoon-crossing");
		await session.SendPacketAsync(session.Api.TalkTo(statue), token);
		await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token);
		await session.SendPacketAsync(session.Api.SelectDialog(statue, 10000), token);
		await session.WaitForPacketAsync(typeof(SM_CHANNEL_INFO), token);
		await session.WaitForPacketAsync(typeof(SM_PLAYER_INFO), token,
			packet => packet.Get<int>("objectId") == session.CharacterId);
		session.AcceptTeleportPosition();
		AssertPosition(session.Api.World.Position, 527.03973f, 2449.6787f, 281.59262f);

		int osmar = await session.WaitForNpcAsync(203679, token);
		session.BeginStep("s04", "level-one-teleporter-refused");
		await session.SendPacketAsync(session.Api.SelectDialog(osmar, 44), token);
		DecodedBotServerPacket refusal = await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token,
			packet => packet.Get<int>("targetObjectId") == osmar);
		Assert.Equal((ushort)27, refusal.Get<ushort>("dialogPageId"));

		session.BeginStep("s05", "walk-back-to-statue");
		await session.MoveToPositionAsync(statueApproach, token);
		statue = await session.WaitForNpcAsync(730532, token);
		session.BeginStep("s06", "teleport-to-aldelle-village");
		await session.SendPacketAsync(session.Api.TalkTo(statue), token);
		await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token);
		await session.SendPacketAsync(session.Api.SelectDialog(statue, 10001), token);
		await session.WaitForPacketAsync(typeof(SM_CHANNEL_INFO), token);
		await session.WaitForPacketAsync(typeof(SM_PLAYER_INFO), token,
			packet => packet.Get<int>("objectId") == session.CharacterId);
		session.AcceptTeleportPosition();
		AssertPosition(session.Api.World.Position, 940.7842f, 1707.3416f, 259.6728f);
		policy.AssertClean();
	}

	private static void AssertPosition(BotPosition? actual, float x, float y, float z)
	{
		BotPosition position = Assert.IsType<BotPosition>(actual);
		Assert.InRange(MathF.Abs(position.X - x), 0, 0.01f);
		Assert.InRange(MathF.Abs(position.Y - y), 0, 0.01f);
		Assert.InRange(MathF.Abs(position.Z - z), 0, 0.01f);
	}

	private async Task RunM4Async(ScenarioDefinition scenario, bool includeHistory)
	{
		using var policy = NewPolicy(scenario.Id, includeHistory);
		using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
		CancellationToken token = timeout.Token;
		await using var session = new SimulationL0Session(
			fixture, policy, "b01", accountId: 13, "Asimmovd", Race.ASMODIANS);
		session.BeginStep("s01", "login-create-enter");
		await session.LoginAndAuthenticateAsync(token);
		await session.CreateCharacterAsync(token);
		await session.EnterWorldAsync(token);
		await session.WaitForPacketAsync(typeof(SM_PLAY_MOVIE), token);

		Player player = fixture.World.GetPlayer(session.CharacterId);
		const string routeId = "4D825485E3CEDF646EE35E7256D0F9F779717F8B";
		Npc walker = player.GetPosition().GetWorldMapInstance().GetNpcs(210363)
			.Single(npc => string.Equals(npc.GetSpawn()?.GetWalkerId(), routeId, StringComparison.Ordinal));
		var start = new BotPosition(player.GetX(), player.GetY(), player.GetZ(), player.GetHeading());
		float dx = walker.GetX() - start.X;
		float dy = walker.GetY() - start.Y;
		float length = MathF.Sqrt(dx * dx + dy * dy);
		var observationPoint = new BotPosition(
			walker.GetX() - dx / length * 10,
			walker.GetY() - dy / length * 10,
			walker.GetZ(), start.Heading);
		var mover = new BotMover(session.Api.World, session.Api.Timing);
		await session.ExecuteMovementAsync(mover.CreateGroundPlan([observationPoint], start,
			session.Api.World.MovementSpeed ?? throw new InvalidOperationException("No movement speed.")), token);
		player.UpdateKnownlist();
		if (!session.Api.World.Objects.ContainsKey(walker.GetObjectId()))
			await session.WaitForPacketAsync(typeof(SM_NPC_INFO), token,
				packet => packet.Get<int>("objectId") == walker.GetObjectId());
		Assert.Equal(210363, session.Api.World.Objects[walker.GetObjectId()].TemplateId);

		var ai = (NpcAI)walker.GetAi();
		WalkManager.StopWalking(ai);
		await session.WaitForPacketAsync(typeof(SM_MOVE), token,
			packet => packet.Get<int>("objectId") == walker.GetObjectId());
		session.BeginStep("s02", "observe-enabled-walker-route");
		Assert.True(WalkManager.StartWalking(ai));
		await session.AdvanceAsync(TimeSpan.FromSeconds(1), token);
		DecodedBotServerPacket move = await session.WaitForPacketAsync(typeof(SM_MOVE), token,
			packet => packet.Get<int>("objectId") == walker.GetObjectId());
		BotPosition observed = session.Api.World.Objects[walker.GetObjectId()].Position;
		Assert.Equal(move.Get<float>("x"), observed.X);
		Assert.True(MathF.Abs(observed.X - 530.26f) > 0.01f || MathF.Abs(observed.Y - 2748.19f) > 0.01f);

		session.BeginStep("s03", "disable-npc-movement");
		WalkManager.StopWalking(ai);
		await session.WaitForPacketAsync(typeof(SM_MOVE), token,
			packet => packet.Get<int>("objectId") == walker.GetObjectId());
		float stoppedX = walker.GetX();
		float stoppedY = walker.GetY();
		bool previous = AIConfig.ACTIVE_NPC_MOVEMENT;
		try
		{
			AIConfig.ACTIVE_NPC_MOVEMENT = false;
			Assert.False(WalkManager.StartWalking(ai));
			await session.AdvanceAsync(TimeSpan.FromSeconds(2), token);
			Assert.InRange(MathF.Abs(walker.GetX() - stoppedX), 0, 0.001f);
			Assert.InRange(MathF.Abs(walker.GetY() - stoppedY), 0, 0.001f);
		}
		finally
		{
			AIConfig.ACTIVE_NPC_MOVEMENT = previous;
		}
		policy.AssertClean();
	}

	private async Task RunM3Async(ScenarioDefinition scenario, bool includeHistory)
	{
		using var policy = NewPolicy(scenario.Id, includeHistory);
		using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
		CancellationToken token = timeout.Token;
		await using var session = new SimulationL0Session(
			fixture, policy, "b01", accountId: 12, "Asimmovc", Race.ASMODIANS);
		session.BeginStep("s01", "login-create-enter");
		await session.LoginAndAuthenticateAsync(token);
		await session.CreateCharacterAsync(token);
		await session.EnterWorldAsync(token);
		await session.WaitForPacketAsync(typeof(SM_PLAY_MOVIE), token);

		Player player = fixture.World.GetPlayer(session.CharacterId);
		var bind = new BotPosition(player.GetX(), player.GetY(), player.GetZ(), player.GetHeading());
		var ledge = bind with { Z = bind.Z + 60 };
		player.GetPosition().SetXYZH(ledge.X, ledge.Y, ledge.Z, ledge.Heading);
		var mover = new BotMover(session.Api.World, session.Api.Timing);
		BotMovementPlan fall = mover.CreateFallPlan([bind], ledge,
			session.Api.World.MovementSpeed ?? throw new InvalidOperationException("No movement speed."));
		session.BeginStep("s02", "fall-sixty-metres");
		await session.ExecuteMovementAsync(fall, token);
		if (!player.IsDead())
			throw new InvalidDataException(
				$"Sixty-metre fall did not kill player: hp={player.GetLifeStats().GetCurrentHp()}/" +
				$"{player.GetLifeStats().GetMaxHp()} pos=({player.GetX():F2},{player.GetY():F2},{player.GetZ():F2}) " +
				$"spawned={player.IsSpawned()} packets={string.Join(",", session.PacketTypes.TakeLast(8))}");
		await session.AdvanceAsync(TimeSpan.FromMilliseconds(500), token);
		await session.WaitForPacketAsync(typeof(SM_DIE), token);
		Assert.True(player.IsDead());

		session.BeginStep("s03", "bind-revive");
		await session.SendPacketAsync(session.Api.Revive(), token);
		await session.WaitForPacketAsync(typeof(SM_CHANNEL_INFO), token);
		Assert.False(player.IsDead());
		Assert.InRange(MathF.Abs(player.GetX() - bind.X), 0, 0.1f);
		Assert.InRange(MathF.Abs(player.GetY() - bind.Y), 0, 0.1f);
		policy.AssertClean();
	}

	private async Task RunM2Async(ScenarioDefinition scenario, bool includeHistory)
	{
		using var policy = NewPolicy(scenario.Id, includeHistory, new SimulationLogPolicyOptions { FailOnWarnings = true });
		using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
		CancellationToken token = timeout.Token;
		await using var session = new SimulationL0Session(
			fixture, policy, "b01", accountId: 11, "Asimmovb", Race.ASMODIANS);
		session.BeginStep("s01", "login-create-enter");
		await session.LoginAndAuthenticateAsync(token);
		await session.CreateCharacterAsync(token);
		await session.EnterWorldAsync(token);
		await session.WaitForPacketAsync(typeof(SM_PLAY_MOVIE), token);

		Player player = fixture.World.GetPlayer(session.CharacterId);
		var start = new BotPosition(player.GetX(), player.GetY(), player.GetZ(), player.GetHeading());
		session.BeginStep("s02", "move-outside-map-region");
		await session.SendMovementAsync(new MovementPacketData(
			99_999, 99_999, 99_999, 0, MovementMask.POSITION | MovementMask.ABSOLUTE), token);
		Assert.False(player.IsSpawned());
		Assert.InRange(MathF.Abs(player.GetX() - start.X), 0, 0.01f);
		Assert.InRange(MathF.Abs(player.GetY() - start.Y), 0, 0.01f);

		session.BeginStep("s03", "move-ignored-until-relog");
		await session.SendMovementAsync(new MovementPacketData(
			start.X + 1, start.Y, start.Z, start.Heading, MovementMask.POSITION | MovementMask.ABSOLUTE), token);
		Assert.False(player.IsSpawned());
		Assert.InRange(MathF.Abs(player.GetX() - start.X), 0, 0.01f);
		policy.AssertClean();
	}

	private async Task RunM1Async(ScenarioExecution execution, bool includeHistory)
	{
		using var policy = NewPolicy(execution.Scenario.Id, includeHistory);
		using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
		CancellationToken token = timeout.Token;
		await using var session = new SimulationL0Session(
			fixture, policy, "b01", accountId: 10, "Asimmova", Race.ASMODIANS);
		session.BeginStep("s01", "login-game-auth");
		await session.LoginAndAuthenticateAsync(token);
		session.BeginStep("s02", "create-asmodian-warrior");
		await session.CreateCharacterAsync(token);
		session.BeginStep("s03", "enter-world-and-finish-prologue");
		await session.EnterWorldAsync(token);
		try
		{
			await session.WaitForPacketAsync(typeof(SM_PLAY_MOVIE), token);
		}
		catch (OperationCanceledException) when (timeout.IsCancellationRequested)
		{
			throw new TimeoutException($"M1 did not receive SM_PLAY_MOVIE; packets: {string.Join(", ", session.PacketTypes)}");
		}
		int asak = await session.WaitForNpcAsync(203500, token);
		Assert.Contains("SM_PLAY_MOVIE", session.PacketTypes);

		BotNavigationGraph graph = BotNavigationGraphFactory.Build(fixture.DataManager.StaticData, [203500, 203504]);
		session.BeginStep("s04", "walk-to-asak");
		await session.MoveToNpcAsync(graph, asak, token);
		session.BeginStep("s05", "accept-quest-2101");
		await session.StartQuestAsync(asak, 2101, token);
		Assert.True(session.Api.World.Quests.ContainsKey(2101));

		int vandar = await session.WaitForNpcAsync(203504, token);
		session.BeginStep("s06", "walk-to-vandar");
		await session.MoveToNpcAsync(graph, vandar, token);
		session.BeginStep("s07", "report-to-vandar");
		await session.FinishQuestAsync(vandar, 2101, token);
		Assert.Equal(5, session.Api.World.Quests[2000].Status);
		Assert.Equal(5, session.Api.World.Quests[2101].Status);
		policy.AssertClean();
	}

	private async Task RunS0Async(ScenarioDefinition scenario, bool includeHistory)
	{
		using var policy = NewPolicy(scenario.Id, includeHistory);
		Assert.True(fixture.Bootstrap.IsStarted);
		Assert.True(fixture.World.IsInitialized);
		Assert.True(fixture.World.ObjectCount > 0);
		Assert.NotNull(fixture.World.GetWorldMap(scenario.Map!.Value).GetMainWorldMapInstance().GetNpc(210119));

		IReadOnlyList<string> first = await RunS0ProbeAsync(scenario.VirtualDuration);
		IReadOnlyList<string> second = await RunS0ProbeAsync(scenario.VirtualDuration);

		Assert.Equal(first, second);
		Assert.Equal([nameof(SM_KEY)], first);
		policy.AssertClean();
	}

	private async Task<IReadOnlyList<string>> RunS0ProbeAsync(TimeSpan duration)
	{
		Rnd.SetProcessSeed(fixture.Seed);
		await using var transport = new InProcessBotTransport(elapsed => fixture.Clock.Advance(elapsed));
		await using var packets = transport.ReceiveAsync().GetAsyncEnumerator();
		Assert.True(await packets.MoveNextAsync());
		var stream = new List<string> { packets.Current.PacketType.Name };
		await transport.AdvanceAsync(duration);
		return stream;
	}

	private async Task RunL0Async(ScenarioExecution execution, bool includeHistory)
	{
		using var policy = NewPolicy(execution.Scenario.Id, includeHistory);
		var actors = Enumerable.Range(1, execution.Scenario.Bots)
			.Select(index => new SimulationL0Actor(fixture, policy, index))
			.ToArray();
		long started = Stopwatch.GetTimestamp();
		try
		{
			await L0Scenario.RunAsync(actors, execution.Channel, includeChat: false);
			WriteL0PacketArtifact(actors);
			TimeSpan elapsed = Stopwatch.GetElapsedTime(started);
			Assert.True(elapsed < SimulationDriver.DefaultWallTimeBudget,
				$"L0 scenario body took {elapsed.TotalSeconds:F3}s; budget is {SimulationDriver.DefaultWallTimeBudget.TotalSeconds:F0}s.");
			policy.AssertClean();
		}
		finally
		{
			foreach (SimulationL0Actor actor in actors)
				await actor.DisposeAsync();
		}
	}

	private static void WriteL0PacketArtifact(IReadOnlyList<SimulationL0Actor> actors)
	{
		string? runDirectory = Environment.GetEnvironmentVariable("AION_E2E_RUN_DIR");
		if (string.IsNullOrWhiteSpace(runDirectory))
			return;
		string path = Path.Combine(Path.GetFullPath(runDirectory), "l0-packets.json");
		var artifact = actors.Select(actor => new
		{
			bot = actor.Bot,
			characterId = actor.Session.CharacterId,
			packets = actor.Session.PacketObservations,
		});
		File.WriteAllText(path, JsonSerializer.Serialize(artifact, new JsonSerializerOptions { WriteIndented = true }) + "\n");
	}

	private SimulationLogPolicy NewPolicy(string scenario, bool includeHistory,
		SimulationLogPolicyOptions? options = null) => new(
		"sim-fast",
		scenario,
		fixture.Clock,
		allowlistPath,
		options,
		captureProvider: fixture.LogCapture,
		loggerFactory: fixture.LoggerFactory,
		includeHistory: includeHistory);

	private static ScenarioTier ReadTier() =>
		Enum.TryParse(Environment.GetEnvironmentVariable("AION_SIM_TIER") ?? nameof(ScenarioTier.Fast),
			ignoreCase: true,
			out ScenarioTier tier) && tier is ScenarioTier.Fast or ScenarioTier.Full
			? tier
			: throw new InvalidOperationException("AION_SIM_TIER must be Fast or Full.");

	private static int ReadPositiveInt(string name, int defaultValue) =>
		int.TryParse(Environment.GetEnvironmentVariable(name) ?? defaultValue.ToString(), out int value) && value > 0
			? value
			: throw new InvalidOperationException($"{name} must be a positive integer.");

	private sealed class SimulationL0Actor : IL0ScenarioActor, IAsyncDisposable
	{
		private readonly SimulationLogPolicy policy;
		private int stepNumber;

		public SimulationL0Actor(SimulationWorldFixture fixture, SimulationLogPolicy policy, int index)
		{
			this.policy = policy;
			Bot = $"b{index:D2}";
			Session = new SimulationL0Session(fixture, policy, Bot, index, $"Aesim{(char)('a' + index - 1)}a");
		}

		public string Bot { get; }
		public SimulationL0Session Session { get; }
		IL0ScenarioSession IL0ScenarioActor.Session => Session;

		public async Task StepAsync(
			string action,
			Func<IL0ScenarioSession, CancellationToken, Task> operation,
			CancellationToken cancellationToken)
		{
			string step = $"s{++stepNumber:D2}";
			Session.BeginStep(step, action);
			using (policy.BeginBotStep(Bot, step))
				await operation(Session, cancellationToken);
		}

		public ValueTask DisposeAsync() => Session.DisposeAsync();
	}

	private sealed class SimulationL0Session : IL0ScenarioSession, IAsyncDisposable
	{
		private readonly SimulationWorldFixture fixture;
		private readonly SimulationLogPolicy policy;
		private readonly string bot;
		private readonly int accountId;
		private readonly string accountName;
		private readonly string characterName;
		private readonly string macAddress;
		private readonly Race race;
		private readonly BotApi api;
		private InProcessBotTransport? transport;
		private IAsyncEnumerator<DecodedBotServerPacket>? packets;
		private AionConnection.State state = AionConnection.State.CONNECTED;
		private string currentStep = "startup";
		private string currentAction = "startup";
		private int characterId;
		private PersistedPosition? expectedPosition;
		private BotPosition? currentPosition;

		public SimulationL0Session(
			SimulationWorldFixture fixture,
			SimulationLogPolicy policy,
			string bot,
			int accountId,
			string characterName,
			Race race = Race.ELYOS)
		{
			this.fixture = fixture;
			this.policy = policy;
			this.bot = bot;
			this.accountId = accountId;
			accountName = $"sim-player-{accountId}";
			this.characterName = characterName;
			this.race = race;
			macAddress = $"02-00-00-00-00-{accountId:X2}";
			api = new BotApi(timing: new BotTimingContract(new SimulationTimeProvider()));
		}

		public List<string> PacketTypes { get; } = [];
		public List<DecodedBotServerPacket> PacketHistory { get; } = [];
		public List<SimulationPacketObservation> PacketObservations { get; } = [];
		public int CharacterId => characterId;
		public BotApi Api => api;

		public void BeginStep(string step, string action)
		{
			currentStep = step;
			currentAction = action;
		}

		public async Task LoginAndAuthenticateAsync(CancellationToken cancellationToken)
		{
			await OpenAsync(cancellationToken);
			RequirePacket(await ReadNextAsync(cancellationToken), typeof(SM_KEY));
			await SendAsync(GameClientPackets.VersionCheck(207, 0, 65001, 10, 0, 2), cancellationToken);
			DecodedBotServerPacket version = await WaitForAsync(typeof(SM_VERSION_CHECK), cancellationToken);
			if (version.Get<byte>("answerId") != 0)
				throw new InvalidDataException("Game server rejected client version 207.");

			await SendAsync(GameClientPackets.L2AuthLoginCheck(2000 + accountId, 1000 + accountId, accountId, 3000 + accountId), cancellationToken);
			await SendAsync(GameClientPackets.MacAddress(macAddress, $"SIM-{bot.ToUpperInvariant()}"), cancellationToken);
			DecodedBotServerPacket auth = await WaitForAsync(typeof(SM_L2AUTH_LOGIN_CHECK), cancellationToken);
			if (!auth.Get<bool>("ok"))
				throw new InvalidDataException("Game-server simulation authentication failed.");
			state = AionConnection.State.AUTHED;
			await SendAsync(api.ListCharacters(2000 + accountId), cancellationToken);
			DecodedBotServerPacket list = await WaitForAsync(typeof(SM_CHARACTER_LIST), cancellationToken);
			if (list.Get<byte>("characterCount") != 0)
				throw new InvalidDataException($"Fresh simulation account {accountName} already has a character.");
		}

		public Task CreateCharacterAsync(CancellationToken cancellationToken) =>
			CreateCharacterAsync(cancellationToken, PlayerClass.WARRIOR);

		public async Task CreateCharacterAsync(CancellationToken cancellationToken, PlayerClass playerClass)
		{
			await SendAsync(api.CreateCharacter(new CharacterCreationData
			{
				AccountId = accountId,
				AccountName = accountName,
				CharacterName = characterName,
				Gender = 0,
				Race = (int)race,
				PlayerClass = (int)playerClass,
				Height = 1,
			}), cancellationToken);
			DecodedBotServerPacket response = await WaitForAsync(typeof(SM_CREATE_CHARACTER), cancellationToken);
			if (response.Get<int>("responseCode") != 0)
				throw new InvalidDataException($"Character creation failed with response {response.Get<int>("responseCode")}.");
			IReadOnlyDictionary<string, object?> character = response.Get<IReadOnlyDictionary<string, object?>>("character");
			characterId = Get<int>(character, "objectId");
			if (!string.Equals(Get<string>(character, "name"), characterName, StringComparison.Ordinal))
				throw new InvalidDataException("SM_CREATE_CHARACTER returned a different simulation character name.");
		}

		public async Task EnterWorldAsync(CancellationToken cancellationToken)
		{
			await SendAsync(api.EnterWorld(characterId), cancellationToken);
			state = AionConnection.State.IN_GAME;
			DecodedBotServerPacket spawn = await WaitForAsync(typeof(SM_PLAYER_SPAWN), cancellationToken);
			expectedPosition = Position(spawn);
			currentPosition = new BotPosition(
				spawn.Get<float>("x"), spawn.Get<float>("y"), spawn.Get<float>("z"), spawn.Get<byte>("heading"));
			await WaitForAsync(typeof(SM_PLAYER_INFO), cancellationToken);
		}

		public async Task<int> WaitForNpcAsync(int templateId, CancellationToken cancellationToken)
		{
			BotKnownObject? known = api.World.Objects.Values.FirstOrDefault(
				candidate => candidate.Kind == BotKnownObjectKind.Npc && candidate.TemplateId == templateId);
			if (known != null)
				return known.ObjectId;
			DecodedBotServerPacket packet = await WaitForAsync(typeof(SM_NPC_INFO), cancellationToken,
				candidate => candidate.Get<int>("npcId") == templateId);
			return packet.Get<int>("objectId");
		}

		public Task<DecodedBotServerPacket> WaitForPacketAsync(Type packetType, CancellationToken cancellationToken) =>
			WaitForAsync(packetType, cancellationToken);

		public Task<DecodedBotServerPacket> WaitForPacketAsync(Type packetType, CancellationToken cancellationToken,
			Func<DecodedBotServerPacket, bool> predicate) => WaitForAsync(packetType, cancellationToken, predicate);

		public Task SendMovementAsync(MovementPacketData movement, CancellationToken cancellationToken) =>
			SendAsync(api.MoveTo(movement), cancellationToken);

		public Task SendPacketAsync(BotClientPacket packet, CancellationToken cancellationToken) =>
			SendAsync(packet, cancellationToken);

		public ValueTask AdvanceAsync(TimeSpan elapsed, CancellationToken cancellationToken) =>
			transport!.AdvanceAsync(elapsed, cancellationToken);

		public ValueTask DrainServerPacketsAsync(CancellationToken cancellationToken) =>
			transport!.DrainAsync(cancellationToken);

		public async Task ExecuteMovementAsync(BotMovementPlan plan, CancellationToken cancellationToken)
		{
			await BotMover.ExecuteAsync(plan,
				(packet, token) => new ValueTask(SendAsync(packet, token)),
				(delay, token) => transport!.AdvanceAsync(delay, token), cancellationToken);
			if (plan.Frames.Count > 0)
				currentPosition = plan.Frames[^1].Position;
		}

		public async Task MoveToPositionAsync(BotPosition destination, CancellationToken cancellationToken)
		{
			BotPosition start = currentPosition ?? api.World.Position
				?? throw new InvalidOperationException("Enter the world before moving.");
			float speed = api.World.MovementSpeed
				?? throw new InvalidOperationException("SM_PLAYER_INFO did not provide movement speed.");
			IReadOnlyList<BotPosition> route = SegmentRoute(start, destination);
			await ExecuteMovementAsync(new BotMover(api.World, api.Timing).CreateGroundPlan(route, start, speed),
				cancellationToken);
			if (currentPosition is BotPosition position && api.World.MapId is int mapId)
				expectedPosition = new PersistedPosition(mapId, position.X, position.Y, position.Z);
		}

		public void AcceptTeleportPosition()
		{
			BotPosition position = api.World.Position
				?? throw new InvalidOperationException("The teleport response did not provide a destination.");
			currentPosition = position;
			if (api.World.MapId is int mapId)
				expectedPosition = new PersistedPosition(mapId, position.X, position.Y, position.Z);
		}

		public async Task MoveToNpcAsync(BotNavigationGraph graph, int objectId, CancellationToken cancellationToken)
		{
			BotPosition start = currentPosition ?? api.World.Position
				?? throw new InvalidOperationException("Enter the world before moving.");
			float speed = api.World.MovementSpeed
				?? throw new InvalidOperationException("SM_PLAYER_INFO did not provide movement speed.");
			var navigator = new BotWorldNavigator(graph);
			IReadOnlyList<BotPosition> route = navigator.FindPathToNpc(api.World, objectId, start);
			if (route.Count == 0)
			{
				BotPosition destination = api.World.Objects[objectId].Position;
				route = SegmentRoute(start, destination);
			}
			var mover = new BotMover(api.World, api.Timing);
			BotMovementPlan plan = mover.CreateGroundPlan(route, start, speed);
			await BotMover.ExecuteAsync(plan,
				(packet, token) => new ValueTask(SendAsync(packet, token)),
				(delay, token) => transport!.AdvanceAsync(delay, token), cancellationToken);
			if (plan.Frames.Count > 0)
				currentPosition = plan.Frames[^1].Position;
			if (currentPosition is BotPosition position && api.World.MapId is int mapId)
				expectedPosition = new PersistedPosition(mapId, position.X, position.Y, position.Z);
		}

		private static IReadOnlyList<BotPosition> SegmentRoute(BotPosition start, BotPosition destination)
		{
			float distance = MathF.Sqrt(
				MathF.Pow(destination.X - start.X, 2) + MathF.Pow(destination.Y - start.Y, 2) +
				MathF.Pow(destination.Z - start.Z, 2));
			int segments = Math.Max(1, (int)MathF.Ceiling(distance / 15f));
			return Enumerable.Range(1, segments)
				.Select(index => new BotPosition(
					start.X + (destination.X - start.X) * index / segments,
					start.Y + (destination.Y - start.Y) * index / segments,
					start.Z + (destination.Z - start.Z) * index / segments,
					destination.Heading))
				.ToArray();
		}

		public async Task StartQuestAsync(int npcObjectId, int questId, CancellationToken cancellationToken)
		{
			await SendAsync(api.TalkTo(npcObjectId), cancellationToken);
			await WaitForAsync(typeof(SM_DIALOG_WINDOW), cancellationToken);
			await SendAsync(api.SelectDialog(npcObjectId, 31, questId: questId), cancellationToken);
			await WaitForAsync(typeof(SM_DIALOG_WINDOW), cancellationToken);
			await SendAsync(api.SelectDialog(npcObjectId, 1002, questId: questId), cancellationToken);
			await WaitForAsync(typeof(SM_QUEST_ACTION), cancellationToken,
				packet => packet.Get<int>("questId") == questId);
			await WaitForAsync(typeof(SM_DIALOG_WINDOW), cancellationToken,
				packet => packet.Get<int>("targetObjectId") == npcObjectId);
		}

		public async Task FinishQuestAsync(int npcObjectId, int questId, CancellationToken cancellationToken)
		{
			await SendAsync(api.TalkTo(npcObjectId), cancellationToken);
			DecodedBotServerPacket opened = await WaitForAsync(typeof(SM_DIALOG_WINDOW), cancellationToken);
			await SendAsync(api.SelectDialog(npcObjectId, 31, questId: questId), cancellationToken);
			using (var responseTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
			{
				responseTimeout.CancelAfter(TimeSpan.FromSeconds(2));
				try
				{
					await WaitForAsync(typeof(SM_DIALOG_WINDOW), responseTimeout.Token);
				}
				catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
				{
					Player player = fixture.World.GetPlayer(characterId);
					BotKnownObject target = api.World.Objects[npcObjectId];
					throw new TimeoutException(
						$"Quest {questId} selection at NPC {npcObjectId} produced no dialog; opened page " +
						$"{opened.Get<ushort>("dialogPageId")} quest {opened.Get<int>("questId")}; " +
						$"player=({player.GetX():F2},{player.GetY():F2},{player.GetZ():F2}) " +
						$"target=({target.Position.X:F2},{target.Position.Y:F2},{target.Position.Z:F2}); " +
						$"packets={string.Join(",", PacketTypes.TakeLast(12))}");
				}
			}
			await SendAsync(api.SelectDialog(npcObjectId, 1009, questId: questId), cancellationToken);
			await WaitForAsync(typeof(SM_DIALOG_WINDOW), cancellationToken);
			await SendAsync(api.SelectDialog(npcObjectId, 23, questId: questId), cancellationToken);
			await WaitForAsync(typeof(SM_QUEST_ACTION), cancellationToken,
				packet => packet.Get<int>("questId") == questId &&
					packet.Fields.TryGetValue("status", out object? status) && status is byte value && value == 5);
		}

		public async Task ChangeChannelAsync(int channel, CancellationToken cancellationToken)
		{
			await SendAsync(api.ChangeChannel(channel), cancellationToken);
			await WaitForAsync(typeof(SM_CHANNEL_INFO), cancellationToken);
			DecodedBotServerPacket spawn = await WaitForAsync(typeof(SM_PLAYER_SPAWN), cancellationToken);
			if (spawn.Get<int>("worldChannel") % 10 != channel)
				throw new InvalidDataException($"Expected channel {channel}, got world channel {spawn.Get<int>("worldChannel")}.");
			expectedPosition = Position(spawn);
		}

		public Task ConnectChatAsync(CancellationToken cancellationToken) =>
			throw new NotSupportedException("Chat steps are LIVE-only.");

		public Task SendChatMessageAsync(string message, CancellationToken cancellationToken) =>
			throw new NotSupportedException("Chat steps are LIVE-only.");

		public Task ReceiveChatMessageAsync(string expected, CancellationToken cancellationToken) =>
			throw new NotSupportedException("Chat steps are LIVE-only.");

		public async Task WalkTenMetersAsync(CancellationToken cancellationToken)
		{
			PersistedPosition start = expectedPosition ?? throw new InvalidOperationException("Enter the world before moving.");
			PersistedPosition target = start with { X = start.X + 10f };
			await SendAsync(api.MoveTo(new MovementPacketData(start.X, start.Y, start.Z, 0,
				MovementMask.POSITION | MovementMask.MANUAL | MovementMask.ABSOLUTE,
				X2: target.X, Y2: target.Y, Z2: target.Z)), cancellationToken);
			await transport!.AdvanceAsync(TimeSpan.FromSeconds(2), cancellationToken);
			await SendAsync(api.MoveTo(new MovementPacketData(target.X, target.Y, target.Z, 0, MovementMask.IMMEDIATE)), cancellationToken);
			expectedPosition = target;
		}

		public async Task PingAsync(CancellationToken cancellationToken)
		{
			await SendAsync(GameClientPackets.Ping(), cancellationToken);
			await WaitForAsync(typeof(SM_PONG), cancellationToken);
		}

		public async Task QuitAsync(CancellationToken cancellationToken)
		{
			await SendAsync(api.Quit(stayConnected: false), cancellationToken);
			await WaitForAsync(typeof(SM_QUIT_RESPONSE), cancellationToken);
			await CloseAsync(cancellationToken);
		}

		public Task VerifyOfflineAsync(CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (PlayerDAO.IsOnline(characterId))
				throw new InvalidDataException($"PlayerDAO still reports {characterName} online after CM_QUIT.");
			PlayerCommonData persisted = PlayerDAO.LoadPlayerCommonData(characterId)
				?? throw new InvalidDataException($"PlayerDAO could not reload {characterName}.");
			AssertPosition(persisted.GetMapId(), persisted.GetX(), persisted.GetY(), persisted.GetZ());
			return Task.CompletedTask;
		}

		public Task WaitForReentryAsync(CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();
			TimeSpan remaining = api.Timing.TimeUntilEnterWorld();
			if (remaining > TimeSpan.Zero)
				fixture.Clock.Advance(remaining + TimeSpan.FromMilliseconds(1));
			return Task.CompletedTask;
		}

		public async Task ReloginAndVerifyPersistenceAsync(CancellationToken cancellationToken)
		{
			await OpenAsync(cancellationToken);
			RequirePacket(await ReadNextAsync(cancellationToken), typeof(SM_KEY));
			await SendAsync(GameClientPackets.VersionCheck(207, 0, 65001, 10, 0, 2), cancellationToken);
			await WaitForAsync(typeof(SM_VERSION_CHECK), cancellationToken);
			await SendAsync(GameClientPackets.L2AuthLoginCheck(2000 + accountId, 1000 + accountId, accountId, 3000 + accountId), cancellationToken);
			await SendAsync(GameClientPackets.MacAddress(macAddress, $"SIM-{bot.ToUpperInvariant()}"), cancellationToken);
			DecodedBotServerPacket auth = await WaitForAsync(typeof(SM_L2AUTH_LOGIN_CHECK), cancellationToken);
			if (!auth.Get<bool>("ok"))
				throw new InvalidDataException("Game-server simulation reauthentication failed.");
			state = AionConnection.State.AUTHED;
			await SendAsync(api.ListCharacters(2000 + accountId), cancellationToken);
			DecodedBotServerPacket list = await WaitForAsync(typeof(SM_CHARACTER_LIST), cancellationToken);
			IReadOnlyDictionary<string, object?> character = list
				.Get<List<IReadOnlyDictionary<string, object?>>>("characters")
				.SingleOrDefault(entry => string.Equals(Get<string>(entry, "name"), characterName, StringComparison.Ordinal))
				?? throw new InvalidDataException($"Character list did not contain {characterName} after relogin.");
			if (Get<int>(character, "objectId") != characterId)
				throw new InvalidDataException("Character object id changed after relogin.");
			AssertPosition(Get<int>(character, "mapId"), Get<float>(character, "x"),
				Get<float>(character, "y"), Get<float>(character, "z"));
		}

		public async ValueTask DisposeAsync() => await CloseAsync(CancellationToken.None);

		private async Task OpenAsync(CancellationToken cancellationToken)
		{
			await CloseAsync(CancellationToken.None);
			transport = new InProcessBotTransport(elapsed => fixture.Clock.Advance(elapsed), ip: $"127.0.0.{accountId}");
			packets = transport.ReceiveAsync(cancellationToken).GetAsyncEnumerator(cancellationToken);
			state = AionConnection.State.CONNECTED;
		}

		private async Task CloseAsync(CancellationToken cancellationToken)
		{
			if (transport != null)
			{
				await transport.CloseAsync(cancellationToken);
				await transport.DisposeAsync();
				transport = null;
			}
			if (packets != null)
			{
				try
				{
					await packets.DisposeAsync();
				}
				catch (NotSupportedException)
				{
					// A canceled MoveNext on ChannelReader.ReadAllAsync can already have disposed its async iterator.
				}
				packets = null;
			}
		}

		private Task SendAsync(BotClientPacket packet, CancellationToken cancellationToken)
		{
			InProcessBotTransport active = transport ?? throw new InvalidOperationException("Simulation game connection is not open.");
			return active.SendAsync(packet.Encode(active.Codec, state), cancellationToken).AsTask();
		}

		private async Task<DecodedBotServerPacket> WaitForAsync(Type packetType, CancellationToken cancellationToken)
			=> await WaitForAsync(packetType, cancellationToken, null);

		private async Task<DecodedBotServerPacket> WaitForAsync(Type packetType, CancellationToken cancellationToken,
			Func<DecodedBotServerPacket, bool>? predicate)
		{
			while (true)
			{
				DecodedBotServerPacket packet = await ReadNextAsync(cancellationToken);
				BotClientPacket? response = api.Observe(packet);
				if (response != null)
					await SendAsync(response, cancellationToken);
				if (packet.PacketType == typeof(SM_ENTER_WORLD_CHECK) && packet.Get<byte>("msg") != 0)
					throw new InvalidDataException($"SM_ENTER_WORLD_CHECK refused entry with message {packet.Get<byte>("msg")}.");
				if (packet.PacketType == packetType && (predicate == null || predicate(packet)))
					return packet;
			}
		}

		private async Task<DecodedBotServerPacket> ReadNextAsync(CancellationToken cancellationToken)
		{
			IAsyncEnumerator<DecodedBotServerPacket> active = packets
				?? throw new EndOfStreamException("Simulation game transport ended before the expected packet.");
			if (!await active.MoveNextAsync().AsTask().WaitAsync(cancellationToken))
				throw new EndOfStreamException("Simulation game transport ended before the expected packet.");
			DecodedBotServerPacket packet = active.Current;
			PacketTypes.Add(packet.PacketType.Name);
			PacketHistory.Add(packet);
			int? objectId = packet.Fields.TryGetValue("objectId", out object? value) && value is int id ? id : null;
			PacketObservations.Add(new SimulationPacketObservation(currentAction, packet.PacketType.Name, objectId));
			policy.ObservePacket(bot, currentStep, packet);
			return packet;
		}

		private void AssertPosition(int mapId, float x, float y, float z)
		{
			PersistedPosition expected = expectedPosition ?? throw new InvalidOperationException("No expected position was recorded.");
			if (mapId != expected.MapId || Math.Abs(x - expected.X) > 0.05f ||
				Math.Abs(y - expected.Y) > 0.05f || Math.Abs(z - expected.Z) > 0.05f)
				throw new InvalidDataException(
					$"Persisted position ({mapId}, {x:F3}, {y:F3}, {z:F3}) did not match " +
					$"expected ({expected.MapId}, {expected.X:F3}, {expected.Y:F3}, {expected.Z:F3}).");
		}

		private static PersistedPosition Position(DecodedBotServerPacket packet) => new(
			packet.Get<int>("worldId"), packet.Get<float>("x"), packet.Get<float>("y"), packet.Get<float>("z"));

		private static void RequirePacket(DecodedBotServerPacket packet, Type expected)
		{
			if (packet.PacketType != expected)
				throw new InvalidDataException($"Expected {expected.Name}, received {packet.PacketType.Name}.");
		}

		private static T Get<T>(IReadOnlyDictionary<string, object?> fields, string name) =>
			fields.TryGetValue(name, out object? value) && value is T typed
				? typed
				: throw new InvalidDataException($"Decoded field '{name}' was missing or was not {typeof(T).Name}.");

		private sealed class SimulationTimeProvider : TimeProvider
		{
			public override DateTimeOffset GetUtcNow() => SystemClock.UtcNow();
		}

		private sealed record PersistedPosition(int MapId, float X, float Y, float Z);

		public sealed record SimulationPacketObservation(string Action, string Packet, int? ObjectId);
	}
}
