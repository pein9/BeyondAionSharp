using System.Text.Json;
using Aion.Bots.Protocol;
using Aion.Bots.Reflexes;
using Aion.Bots.Scenarios;
using Aion.Bots.Tracing;
using Aion.Bots.World;
using Aion.GameServer.Dao;
using Aion.GameServer.Model;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.TestKit;

namespace Aion.Simulation.Tests;

public sealed partial class SimulationFastScenarioTests
{
	/// <summary>ND-04: controlled losses, actual recovery trips and retained-var recipients. Free account 219.</summary>
	[SkippableFact]
	public async Task DestinyLossesRecoverByBindReturnAndTheOrdinaryNornRoute()
	{
		Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
		using var policy = NewPolicy("ND04", includeHistory: false);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(25));
		CancellationToken token = timeout.Token;
		string run = Environment.GetEnvironmentVariable("AION_SIM_RUN_ID") ?? "nd04-recovery";
		string path = Path.Combine(RealStaticData.RepoRoot(), "run", $"{run}-nd04.trace.jsonl");
		using var trace = BotActionTraceWriter.Open(path, run, "b01", "sim-player-219", virtualTime: () => TimeSpan.FromMilliseconds(fixture.Clock.NowMillis));
		await using var session = new SimulationL0Session(fixture, policy, "b01", 219, "Asimdestloss", Race.ASMODIANS, trace, path);
		var probe = new DestinyProbe(this, fixture, session, token);
		await probe.InitializeAsync(100000);
		await BindDestinyProbeAsync(probe, session, token);
		await probe.SetupVarAsync(4); // Prepare only the first outdoor-Skuld prerequisite; entry/movie/equip below are real.
		await RecoverDestinyProbeAsync(probe);
		await TutorialAsync(equip: false);
		await DieAndReviveAsync("before-equip");
		await RecoverDestinyProbeAsync(probe);
		await TutorialAsync(equip: true);
		await DieAndReviveAsync("after-equip");
		await RecoverDestinyProbeAsync(probe);
		await TutorialAsync(equip: true);

		int instanceId = probe.Server.GetInstanceId(), beforeLogs = fixture.LogCapture.Entries.Count;
		await RelogAsync();
		Assert.Equal((320070000, 97), (probe.Server.GetWorldId(), probe.Var));
		Assert.True(probe.Server.GetWorldMapInstance().IsRegistered(session.CharacterId));
		// Java chooses the first registered instance, which can be an older still-live attempt after deaths.
		int relogInstance = probe.Server.GetInstanceId();
		Assert.Equal(1, Count(140000001));
		Assert.Empty(probe.Server.GetEquipment().GetEquippedItemsRegularStigma());
		Assert.DoesNotContain(11504, session.Api.World.Skills.Keys);
		Assert.Contains(fixture.LogCapture.Entries.Skip(beforeLogs), entry => entry.Category == "AUDIT_LOG" &&
			entry.Message.Contains("had more equipped stigmas on login than allowed", StringComparison.Ordinal));
		Console.WriteLine($"ND-04 live-instance relog: instance {instanceId} -> registered {relogInstance}, START/97 retained, tutorial stone unequipped/11504 removed; Java-shared slot-gate audit observed");
		await ReturnAndCheckAsync(); // Real outside entry resets the inconsistent live attempt.
		await RecoverDestinyProbeAsync(probe);
		Assert.NotEqual(instanceId, probe.Server.GetInstanceId());
		Assert.NotEqual(relogInstance, probe.Server.GetInstanceId());
		await TutorialAsync(equip: true);

		long beforeSpawn = fixture.Clock.NowMillis;
		await probe.TalkAsync("spawn");
		var instance = probe.Server.GetWorldMapInstance();
		var enemy = instance.GetNpcs(204263).Single(n => !n.IsDead());
		Assert.Equal(3169, enemy.GetLifeStats().GetCurrentHp());
		await session.QuitAsync(token); // A missed window during an interruption; no immunity or enemy edits.
		await session.AdvanceOfflineAsync(TimeSpan.FromMilliseconds(beforeSpawn + 290000 - fixture.Clock.NowMillis), token);
		Assert.True(enemy.IsSpawned());
		Assert.False(enemy.IsDead());
		await session.AdvanceOfflineAsync(TimeSpan.FromSeconds(20), token);
		Assert.False(enemy.IsSpawned());
		Assert.Empty(instance.GetNpcs(204263));
		await LoginAgainAsync();
		Assert.Equal((320070000, 98), (probe.Server.GetWorldId(), probe.Var));
		int skuld = await probe.WalkNpcAsync(204264);
		await NaturalDialogProtocol.OpenAsync(session, skuld, token);
		await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token);
		await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialogExpectRejection(skuld, DialogAction.SETPRO8, questId: 2900), token);
		await session.SynchronizeAsync(token);
		Assert.Equal(new PendingQuestDialogAction(skuld, DialogAction.SETPRO8, 2900), session.Api.QuestDialogEchoes.ConsumeExpectedRejection());
		Assert.Equal(98, probe.Var);
		Assert.Empty(instance.GetNpcs(204263));
		await session.SendPacketAsync(session.Api.CloseDialog(skuld), token);
		Console.WriteLine("ND-04 missed five-minute window: real Hellion alive at 290 s/deleted at 310 s; START/98 retained and SETPRO8 refused");
		await ReturnAndCheckAsync();
		await RecoverDestinyProbeAsync(probe);
		await TutorialAsync(equip: true);
		await ReturnAndCheckAsync(); // Outside exit with the temporary stone actually equipped.
		await RecoverDestinyProbeAsync(probe);
		Assert.Equal(95, probe.Var);
		await ReturnAndCheckAsync();

		// Each controlled saved var starts at the fortress; every trip and remaining talk is ordinary.
		foreach ((int var, string key) in new[] { (1, "munin"), (2, "urd"), (3, "verdandi"), (4, "enter"), (9, "skuld-return"), (10, "munin-return") })
		{
			await probe.SetupVarAsync(var);
			await RelogAsync();
			Assert.Equal(var, probe.Var);
			await probe.TransportAsync(120010000);
			await probe.TransportAsync(220010000);
			if (var is 1 or 4 or 9 or 10) await probe.FlyAsync(203513);
			await probe.TalkAsync(key);
			Assert.Equal(probe.Leg.Steps.Single(s => s.Key == "q2900-" + key).NextVar, probe.Var);
			Console.WriteLine($"ND-04 retained START/{var}: reached only {key}, resulting full var {probe.Var}");
			await ReturnAndCheckAsync(expectedVar: var == 4 ? 4 : probe.Var);
		}
		Assert.Equal(QuestStatus.REWARD, probe.Server.GetQuestStateList().GetQuestState(2900).GetStatus());
		await RelogAsync();
		Assert.Equal(4, session.Api.World.Quests[2900].Status);
		await probe.TransportAsync(120010000);
		await probe.TalkAsync("reward");
		Assert.Contains(2900, session.Api.World.CompletedQuestIds);
		Assert.Equal(144, session.Api.World.CompletedQuestIds.Count);
		Assert.Equal(1, Count(188053787));
		Assert.Equal(0, Count(140000098));
		await ReturnAndCheckAsync(expectedVar: 10);
		Console.WriteLine($"ND-04 recovery matrix complete: two controlled deaths, one missed window, live relogs, seven saved-state resumes; {probe.Walked} ground legs, sealed bundle and fortress bind retained");
		policy.AssertClean();

		long Count(int id) => session.Api.World.Inventory.Values.Where(item => item.ItemId == id).Sum(item => item.Count);
		async Task TutorialAsync(bool equip)
		{
			foreach (string key in new[] { "movie", "stone" }) await probe.TalkAsync(key);
			if (equip) await NaturalAltgardQuestSteps.EquipDestinyStigmaAsync(session, probe.Leg.Destiny!, await probe.WalkNpcAsync(204264), token);
			Assert.Equal(equip ? 97 : 99, probe.Var);
		}
		async Task DieAndReviveAsync(string label)
		{
			Assert.True(probe.Server.GetController().Die(probe.Server)); // Controlled death, real quest hook.
			await session.AdvanceAsync(TimeSpan.FromMilliseconds(501), token);
			var prompt = await session.WaitForPacketAsync(typeof(SM_DIE), token);
			Assert.False(prompt.Get<bool>("allowInstanceRevive"));
			await session.SynchronizeAsync(token);
			Assert.Equal(4, probe.Var);
			AssertDestinyCleanup(probe, session);
			session.Api.World.BeginWorldReload();
			await session.SendPacketAsync(session.Api.Revive(BotReviveType.Bind), token);
			await probe.AcceptTeleportAsync(220030000, true);
			Assert.False(probe.Server.IsDead());
			Assert.Equal(220030000, session.Api.World.ObeliskBindPoint!.MapId);
			if (session.Api.World.VisibleEffects?.FirstOrDefault(e => e.SkillId == 8291) is { } sickness)
			{
				await session.AdvanceAsync(TimeSpan.FromMilliseconds(sickness.RemainingMillis + 1000), token);
				await session.SynchronizeAsync(token);
			}
			Console.WriteLine($"ND-04 {label} death: START/4 cleanup, actual fortress bind revival, observed Soul Sickness wait");
		}
		async Task ReturnAndCheckAsync(int expectedVar = 4)
		{
			// Respect Return's shipped 1,200-second reuse. These waits occur safely without a live Hellion.
			await session.AdvanceAsync(TimeSpan.FromSeconds(1201), token);
			await session.SynchronizeAsync(token);
			await probe.ReturnAsync();
			if (session.Api.World.CompletedQuestIds.Contains(2900))
				Assert.Equal(QuestStatus.COMPLETE, probe.Server.GetQuestStateList().GetQuestState(2900).GetStatus());
			else Assert.Equal(expectedVar, probe.Var);
			AssertDestinyCleanup(probe, session);
		}
		async Task RelogAsync()
		{
			await session.QuitAsync(token);
			await session.WaitForReentryAsync(token);
			await LoginAgainAsync();
		}
		async Task LoginAgainAsync()
		{
			// ExecuteMovementAsync tracks the walked position, while the generic persistence helper's
			// expectation still holds the last teleport. Compare this probe's actual last ground position.
			BotPosition position = session.CurrentPosition;
			int map = session.Api.World.MapId!.Value, id = session.CharacterId;
			var list = await session.LoginCharacterListAsync(token);
			var character = Assert.Single(list.Get<List<IReadOnlyDictionary<string, object?>>>("characters"));
			Assert.Equal(id, (int)character["objectId"]!);
			Assert.Equal("Asimdestloss", character["name"]);
			AssertDestinyPosition(map, position, character);
			session.SelectCharacter(id, "Asimdestloss");
			await session.EnterWorldAsync(token);
			await session.SynchronizeAsync(token);
		}
	}

	/// <summary>ND-04: invoked twice by the owned runner, in two actual server/test-host processes.</summary>
	[SkippableFact]
	public async Task DestinyColdRestartFallsBackToTheFortressAndRecoversNormally()
	{
		Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
		string? phase = Environment.GetEnvironmentVariable("AION_SIM_DESTINY_RESTART_PHASE");
		Skip.If(phase is null, "Use scripts/sim/probe-destiny-restart.ps1 for the two-process ND-04 probe.");
		Assert.Contains(phase, new[] { "prepare", "resume" });
		Assert.StartsWith("aion_gs_sim_ni08_", Environment.GetEnvironmentVariable("AION_SIM_NI08_DATABASE") ?? "");
		string receiptPath = Environment.GetEnvironmentVariable("AION_SIM_DESTINY_RESTART_RECEIPT")!;
		Assert.False(string.IsNullOrWhiteSpace(receiptPath));
		using var policy = NewPolicy("ND04-cold", includeHistory: false);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(15));
		CancellationToken token = timeout.Token;
		string run = Environment.GetEnvironmentVariable("AION_SIM_RUN_ID") ?? "nd04-cold";
		string path = Path.Combine(RealStaticData.RepoRoot(), "run", $"{run}-nd04-cold.trace.jsonl");
		using var trace = BotActionTraceWriter.Open(path, run, "b01", "sim-player-219", virtualTime: () => TimeSpan.FromMilliseconds(fixture.Clock.NowMillis));
		await using var session = new SimulationL0Session(fixture, policy, "b01", 219, "Asimdestcold", Race.ASMODIANS, trace, path);
		var probe = new DestinyProbe(this, fixture, session, token);
		if (phase == "prepare")
		{
			await probe.InitializeAsync(100000);
			await BindDestinyProbeAsync(probe, session, token);
			await probe.SetupVarAsync(4);
			await RecoverDestinyProbeAsync(probe);
			foreach (string key in new[] { "movie", "stone" }) await probe.TalkAsync(key);
			await NaturalAltgardQuestSteps.EquipDestinyStigmaAsync(session, probe.Leg.Destiny!, await probe.WalkNpcAsync(204264), token);
			Assert.Equal(97, probe.Var);
			int instance = probe.Server.GetInstanceId();
			BotPosition position = session.CurrentPosition;
			await session.QuitAsync(token);
			Assert.False(PlayerDAO.IsOnline(session.CharacterId));
			var persisted = PlayerDAO.LoadPlayerCommonData(session.CharacterId)!;
			Assert.Equal(320070000, persisted.GetMapId());
			Assert.True(NaturalFlightPolicy.Distance(position, new(persisted.GetX(), persisted.GetY(), persisted.GetZ(), 0)) < 0.1f);
			File.WriteAllText(receiptPath, JsonSerializer.Serialize(new
			{
				CharacterId = session.CharacterId, Name = "Asimdestcold", ProcessId = Environment.ProcessId,
				MapId = 320070000, InstanceId = instance, Var = 97, ElapsedMillis = fixture.Clock.NowMillis,
				Position = position,
			}));
			Console.WriteLine($"ND-04 cold prepare: character {session.CharacterId}, real equipped START/97 saved inside instance {instance}, process {Environment.ProcessId}");
		}
		else
		{
			using JsonDocument receipt = JsonDocument.Parse(File.ReadAllText(receiptPath));
			JsonElement saved = receipt.RootElement;
			Assert.NotEqual(saved.GetProperty("ProcessId").GetInt32(), Environment.ProcessId);
			var list = await session.LoginCharacterListAsync(token);
			var character = Assert.Single(list.Get<List<IReadOnlyDictionary<string, object?>>>("characters"));
			Assert.Equal(saved.GetProperty("CharacterId").GetInt32(), (int)character["objectId"]!);
			Assert.Equal("Asimdestcold", character["name"]);
			Assert.Equal(320070000, (int)character["mapId"]!);
			JsonElement at = saved.GetProperty("Position");
			AssertDestinyPosition(320070000, new(at.GetProperty("X").GetSingle(), at.GetProperty("Y").GetSingle(), at.GetProperty("Z").GetSingle(), 0), character);
			session.SelectCharacter((int)character["objectId"]!, "Asimdestcold");
			await session.WaitForReentryAsync(token);
			int beforeLogs = fixture.LogCapture.Entries.Count;
			await session.EnterWorldAsync(token);
			await session.SynchronizeAsync(token);
			Assert.Equal((220030000, 4), (probe.Server.GetWorldId(), probe.Var));
			var spawns = session.PacketHistory.Where(p => p.PacketType == typeof(SM_PLAYER_SPAWN)).ToArray();
			Assert.Equal(2, spawns.Length);
			Assert.All(spawns, spawn => Assert.Equal(220030000, spawn.Get<int>("worldId")));
			AssertDestinyCleanup(probe, session);
			Assert.Contains(fixture.LogCapture.Entries.Skip(beforeLogs), entry => entry.Message.Contains("No instance exit found for race: ASMODIANS 320070000", StringComparison.Ordinal));
			Assert.Contains(fixture.LogCapture.Entries.Skip(beforeLogs), entry => entry.Category == "AUDIT_LOG" && entry.Message.Contains("had more equipped stigmas on login than allowed", StringComparison.Ordinal));
			Console.WriteLine($"ND-04 cold resume: same character {session.CharacterId}, new process {Environment.ProcessId}, missing-exit bind fallback/START4 cleanup and stigma-login audit observed");
			await RecoverDestinyProbeAsync(probe);
			Assert.Equal(95, probe.Var);
			Console.WriteLine("ND-04 cold recovery: ordinary Ukin/Doman/Aldelle flight/Skuld creates a new solo attempt");
		}
		policy.AssertClean();
	}

	private static async Task BindDestinyProbeAsync(DestinyProbe probe, SimulationL0Session session, CancellationToken token)
	{
		await probe.SetupAtFortressAsync();
		int obelisk = await probe.WalkNpcAsync(700065);
		Assert.True((await new NaturalServiceSteps(session).BindAsync(obelisk, session.Api.World.Objects[obelisk].Position,
			220030000, probe.Leg.Bind!.Price, 5, token)).IsDone);
	}

	private static async Task RecoverDestinyProbeAsync(DestinyProbe probe)
	{
		Assert.Equal((220030000, 4), (probe.Server.GetWorldId(), probe.Var));
		await probe.TransportAsync(120010000);
		await probe.TransportAsync(220010000);
		await probe.FlyAsync(203513);
		await probe.TalkAsync("enter");
		Assert.Equal(95, probe.Var);
	}

	private static void AssertDestinyCleanup(DestinyProbe probe, SimulationL0Session session)
	{
		Assert.DoesNotContain(session.Api.World.Inventory.Values, item => item.ItemId == 140000001);
		Assert.Empty(probe.Server.GetEquipment().GetEquippedItemsRegularStigma());
		Assert.DoesNotContain(11504, session.Api.World.Skills.Keys);
		Assert.False(probe.Server.GetSkillList().IsSkillPresent(11504));
		Assert.Contains(1842, session.Api.World.Skills.Keys);
	}

	private static void AssertDestinyPosition(int map, BotPosition position, IReadOnlyDictionary<string, object?> character)
	{
		Assert.Equal(map, (int)character["mapId"]!);
		Assert.True(NaturalFlightPolicy.Distance(position, new((float)character["x"]!, (float)character["y"]!, (float)character["z"]!, 0)) < 0.1f);
	}
}
