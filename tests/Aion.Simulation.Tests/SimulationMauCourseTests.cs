using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using Aion.Bots.Dashboard;
using Aion.Bots.Navigation;
using Aion.Bots.Protocol;
using Aion.Bots.Scenarios;
using Aion.Bots.Tracing;
using Aion.GameServer.Model;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services.Items;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.Services;
using Aion.GameServer.Utils;

namespace Aion.Simulation.Tests;

public sealed partial class SimulationFastScenarioTests
{
	/// <summary>Focused Mau course. A fresh deterministic SIM world is the course reset.</summary>
	[SkippableFact]
	public async Task NaturalMauCourseUsesFrozenPriestAndCurrentPolicy()
	{
		Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
		string? courseName = Environment.GetEnvironmentVariable("AION_MAU_COURSE");
		Skip.IfNot(Enum.TryParse(courseName, ignoreCase: true, out NaturalMauCourse course) &&
			Enum.IsDefined(course), "Set AION_MAU_COURSE=GeneratorToRae or RaeToHatata.");
		string? encounterName = Environment.GetEnvironmentVariable("AION_MAU_ENCOUNTER");
		NaturalMauEncounter? encounter = null;
		if (!string.IsNullOrWhiteSpace(encounterName))
		{
			Assert.True(Enum.TryParse(encounterName, ignoreCase: true, out NaturalMauEncounter parsed) &&
				Enum.IsDefined(parsed), $"Unknown Mau encounter {encounterName}.");
			encounter = parsed;
			Assert.Equal(encounter == NaturalMauEncounter.BlockedGeneratorRejoin
				? NaturalMauCourse.GeneratorToRae : NaturalMauCourse.RaeToHatata, course);
		}
		MauEncounterStart? encounterStart = encounter is { } selected ? StartFor(selected) : null;
		string root = Aion.GameServer.TestKit.RealStaticData.RepoRoot();
		using JsonDocument locked = JsonDocument.Parse(await File.ReadAllTextAsync(
			Path.Combine(root, "docs", "bot-learning-phase0-lock.json")));
		JsonElement definition = locked.RootElement.GetProperty("courses").EnumerateArray().Single(entry =>
			entry.GetProperty("id").GetString() == (course == NaturalMauCourse.GeneratorToRae ? "generator_to_rae" : "rae_to_hatata"));
		string run = Environment.GetEnvironmentVariable("AION_SIM_RUN_ID") ?? $"phase0-{course}-s{fixture.Seed}";
		string? requestedDirectory = Environment.GetEnvironmentVariable("AION_MAU_TRACE_DIR");
		string directory = requestedDirectory == null ? Path.Combine(root, "run", "bot-learning-phase0", "current", run) :
			Path.GetFullPath(Path.IsPathRooted(requestedDirectory) ? requestedDirectory : Path.Combine(root, requestedDirectory));
		Directory.CreateDirectory(directory);
		string? policyFile = Environment.GetEnvironmentVariable("AION_MAU_POLICY_FILE");
		NaturalMauPolicyParameters? candidatePolicy = string.IsNullOrWhiteSpace(policyFile) ? null :
			JsonSerializer.Deserialize<NaturalMauPolicyParameters>(await File.ReadAllTextAsync(policyFile))
			?? throw new InvalidDataException("Mau policy JSON was empty.");
		(candidatePolicy ?? NaturalMauPolicyParameters.Baseline).Validate();
		await File.WriteAllTextAsync(Path.Combine(directory, "policy.json"),
			JsonSerializer.Serialize(candidatePolicy ?? NaturalMauPolicyParameters.Baseline,
				new JsonSerializerOptions { WriteIndented = true }) + "\n");
		string tracePath = Path.Combine(directory, $"{run}.trace.jsonl");
		if (File.Exists(tracePath)) throw new IOException($"Preserving existing Mau course trace: {tracePath}");
		using var trace = BotActionTraceWriter.Open(tracePath, run, "b01", "sim-player-41",
			virtualTime: () => TimeSpan.FromMilliseconds(fixture.Clock.NowMillis));
		Console.WriteLine($"Mau course trace: {tracePath}");
		using var policy = NewPolicy("PHASE0", includeHistory: true);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(15));
		CancellationToken token = timeout.Token;
		await using var session = new SimulationL0Session(fixture, policy, "b01", accountId: 41,
			"Asimnjour", Race.ASMODIANS, trace, tracePath);
		var dashboard = new LiveBotDashboardState();
		int dashboardPort = int.Parse(Environment.GetEnvironmentVariable("AION_BOT_DASHBOARD_PORT") ?? "17880");
		await using var dashboardHost = new LiveBotDashboardHost(run, ["PHASE0"], dashboard, dashboardPort);
		session.Dashboard = dashboard;
		if (dashboardHost.Enabled) Console.WriteLine($"Mau course dashboard: {dashboardHost.Url}");
		NaturalJourneyRuntime? runtime = null;
		runtime = new NaturalJourneyRuntime(root, "SIM-mau-phase0", fixture.Seed,
			fixture.DataManager.StaticData, () => fixture.Clock.NowMillis, fixture.Epoch,
			() => BotNavigationGeometry.ForServerWorld(fixture.World.GetPlayer(session.CharacterId).GetInstanceId(),
				Race.ASMODIANS), EnterAsync, policy.AssertClean, () => policy.SnapshotProblems(), trace, dashboard)
		{
			PrepareCourseAsync = PrepareAsync,
		};
		await new NaturalIshalgenJourney(session, runtime,
			new NaturalJourneyOptions(Course: course, Encounter: encounter, MauPolicy: candidatePolicy)).RunAsync(token);

		async Task<bool> EnterAsync(CancellationToken enterToken)
		{
			session.BeginStep("phase0-create", "create-fresh-natural-priest-for-frozen-course");
			await session.LoginAndAuthenticateAsync(enterToken);
			await session.CreateCharacterAsync(enterToken, PlayerClass.PRIEST);
			await session.EnterWorldAsync(enterToken);
			await session.WaitForPacketAsync(typeof(SM_PLAY_MOVIE), enterToken);
			await session.SynchronizeAsync(enterToken);
			return false;
		}

		async Task PrepareAsync(CancellationToken prepareToken)
		{
			Player player = fixture.World.GetPlayer(session.CharacterId);
			Assert.Equal(PlayerClass.PRIEST, player.GetPlayerClass());
			// A fresh Priest has no saved obelisk bind; Java's revive path falls back to the
			// race's initial spawn. Check that effective revive location against the lock.
			JsonElement lockedBind = definition.GetProperty("bind");
			JsonElement lockedBindPosition = lockedBind.GetProperty("position");
			var savedBind = player.GetBindPoint();
			var initialSpawn = Aion.GameServer.Dataholders.DataManager.PLAYER_INITIAL_DATA.GetSpawnLocation(player.GetRace());
			Assert.Equal(lockedBind.GetProperty("mapId").GetInt32(), savedBind?.GetMapId() ?? initialSpawn.GetMapId());
			Assert.InRange(Math.Abs((savedBind?.GetX() ?? initialSpawn.GetX()) - lockedBindPosition.GetProperty("X").GetSingle()), 0, 0.01f);
			Assert.InRange(Math.Abs((savedBind?.GetY() ?? initialSpawn.GetY()) - lockedBindPosition.GetProperty("Y").GetSingle()), 0, 0.01f);
			Assert.InRange(Math.Abs((savedBind?.GetZ() ?? initialSpawn.GetZ()) - lockedBindPosition.GetProperty("Z").GetSingle()), 0, 0.01f);
			player.GetCommonData().SetLevel(9);
			foreach (var item in player.GetEquipment().GetEquippedItems().ToArray())
				Assert.NotNull(player.GetEquipment().UnEquipItem(item.GetObjectId()));
			foreach (var item in player.GetInventory().GetItems().ToArray())
				Assert.True(player.GetInventory().DecreaseByObjectId(item.GetObjectId(), item.GetItemCount()));
			foreach (var skill in player.GetSkillList().GetAllSkills().ToArray())
				SkillLearnService.RemoveSkill(player, skill.GetSkillId());
			foreach (JsonProperty skill in definition.GetProperty("skills").EnumerateObject())
				Assert.True(player.GetSkillList().AddSkill(player, int.Parse(skill.Name), skill.Value.GetInt32()));
			long desiredKinah = 0;
			foreach (JsonElement item in definition.GetProperty("inventory").EnumerateArray())
			{
				int id = item.GetProperty("itemId").GetInt32();
				long count = item.GetProperty("count").GetInt64();
				if (id == Aion.Bots.World.BotWorldModel.KinahItemId) { desiredKinah += count; continue; }
				Assert.Equal(0, ItemService.AddItem(player, id, count, allowInventoryOverflow: true));
				if (item.GetProperty("equippedSlot").ValueKind == JsonValueKind.Number)
				{
					int slot = item.GetProperty("equippedSlot").GetInt32();
					if (slot > 0)
					{
						var gear = player.GetInventory().GetItems().Last(entry => entry.GetItemId() == id);
						Assert.NotNull(player.GetEquipment().EquipItem(gear.GetObjectId(), slot));
					}
				}
			}
			long kinah = player.GetInventory().GetKinah();
			if (kinah > desiredKinah) player.GetInventory().DecreaseKinah(kinah - desiredKinah);
			else if (kinah < desiredKinah) player.GetInventory().IncreaseKinah(desiredKinah - kinah);
			JsonElement quest = definition.GetProperty("quest");
			int questId = quest.GetProperty("questId").GetInt32();
			QuestState state = player.GetQuestStateList().GetQuestState(questId) ??
				new QuestState(questId, QuestStatus.START);
			if (player.GetQuestStateList().GetQuestState(questId) == null)
				Assert.True(player.GetQuestStateList().AddQuest(questId, state));
			state.SetStatus(QuestStatus.START);
			int initialQuestVar = encounterStart?.QuestVar ?? quest.GetProperty("stepAndFlags").GetInt32();
			state.SetQuestVar(initialQuestVar);
			PacketSendUtility.SendPacket(player, new SM_QUEST_ACTION(SM_QUEST_ACTION.ActionType.ADD, state));
			await session.SynchronizeAsync(prepareToken);
			// The recorded max HP includes the Priest's client-visible Blessing of Guardianship.
			// Establish it through the ordinary cast protocol before applying the frozen HP/MP.
			await session.SendPacketAsync(session.Api.Target(session.CharacterId), prepareToken);
			await session.SendPacketAsync(session.Api.Cast(runtime!.CreateSpellCast(session.Api.World,
				session.CurrentPosition, 1684, 1, session.CharacterId)), prepareToken);
			await session.WaitForPacketAsync(typeof(SM_CASTSPELL_RESULT), prepareToken,
				packet => packet.Get<ushort>("skillId") == 1684);
			await session.SynchronizeAsync(prepareToken);
			if (encounterStart is { } start)
			{
				var instance = fixture.World.GetWorldMap(220010000).GetMainWorldMapInstance();
				foreach (MauSpawnKey key in start.Keep)
					Assert.Single(instance.GetNpcs().Where(npc => key.Matches(npc)));
				if (start.ClearRadius > 0)
				{
					foreach (var npc in instance.GetNpcs().Where(npc =>
						runtime!.IsAggressive(npc.GetObjectTemplate()) &&
						MathF.Sqrt(MathF.Pow(npc.GetX() - start.CenterX, 2) +
							MathF.Pow(npc.GetY() - start.CenterY, 2)) <= start.ClearRadius &&
						!start.Keep.Any(key => key.Matches(npc))).ToArray())
						fixture.World.Despawn(npc);
				}
				if (encounter == NaturalMauEncounter.HatataWithAdd)
				{
					var add = instance.GetNpcs().Single(npc =>
						new MauSpawnKey(211284, 656.619f, 894.842f).Matches(npc));
					fixture.World.Despawn(add);
					var nearHatata = Aion.GameServer.SpawnEngine.SpawnEngine.NewSingleTimeSpawn(
						220010000, 211284, 660f, 886f, 318.7f, 60);
					Assert.True(Aion.GameServer.SpawnEngine.SpawnEngine.SpawnObject(
						nearHatata, instance.GetInstanceId())?.IsSpawned());
				}
				if (start.WarmupSeconds > 0)
					fixture.Clock.Advance(TimeSpan.FromSeconds(start.WarmupSeconds));
			}
			JsonElement point = definition.GetProperty("position");
			float startX = encounterStart?.X ?? point.GetProperty("X").GetSingle();
			float startY = encounterStart?.Y ?? point.GetProperty("Y").GetSingle();
			float startZ = encounterStart?.Z ?? point.GetProperty("Z").GetSingle();
			byte startHeading = encounterStart?.Heading ?? point.GetProperty("Heading").GetByte();
			await TeleportForSetupAsync(session, player, 220010000,
				startX, startY, startZ, prepareToken, heading: startHeading);
			session.AcceptTeleportPosition();
			player.GetLifeStats().SetCurrentHp(definition.GetProperty("hp").GetInt32());
			player.GetLifeStats().SetCurrentMp(definition.GetProperty("mp").GetInt32());
			await session.SynchronizeAsync(prepareToken);
			Assert.Equal(9, session.Api.World.Level);
			Assert.Equal(initialQuestVar, session.Api.World.Quests[questId].StepAndFlags);
			Assert.Equal(definition.GetProperty("hp").GetInt32(), session.Api.World.CurrentHp);
			Assert.Equal(definition.GetProperty("mp").GetInt32(), session.Api.World.CurrentMp);
			Assert.Equal(definition.GetProperty("maxHp").GetInt32(), session.Api.World.MaxHp);
			Assert.Equal(definition.GetProperty("maxMp").GetInt32(), session.Api.World.MaxMp);
			Assert.Equal(startHeading, session.CurrentPosition.Heading);
			var expectedSkills = definition.GetProperty("skills").EnumerateObject()
				.Select(entry => (Id: int.Parse(entry.Name), Level: entry.Value.GetInt32())).OrderBy(entry => entry.Id).ToArray();
			var observedSkills = session.Api.World.Skills.Values.Select(skill => (Id: (int)skill.SkillId, Level: (int)skill.Level))
				.OrderBy(entry => entry.Id).ToArray();
			Assert.Equal(expectedSkills, observedSkills);
			var expectedItems = definition.GetProperty("inventory").EnumerateArray()
				.GroupBy(item => item.GetProperty("itemId").GetInt32())
				.Select(group => (Id: group.Key, Count: group.Sum(item => item.GetProperty("count").GetInt64())))
				.OrderBy(entry => entry.Id).ToArray();
			var observedItems = session.Api.World.Inventory.Values.GroupBy(item => item.ItemId)
				.Select(group => (Id: group.Key, Count: group.Sum(item => item.Count)))
				.OrderBy(entry => entry.Id).ToArray();
			Assert.Equal(expectedItems, observedItems);
			var expectedGear = definition.GetProperty("inventory").EnumerateArray()
				.Where(item => item.GetProperty("equippedSlot").ValueKind == JsonValueKind.Number &&
					item.GetProperty("equippedSlot").GetInt32() > 0)
				.Select(item => (Id: item.GetProperty("itemId").GetInt32(), Slot: item.GetProperty("equippedSlot").GetInt32()))
				.OrderBy(entry => entry.Slot).ToArray();
			var observedGear = session.Api.World.Inventory.Values
				.Where(item => item.Details.EquippedSlot is > 0)
				.Select(item => (Id: item.ItemId, Slot: checked((int)item.Details.EquippedSlot!.Value)))
				.OrderBy(entry => entry.Slot).ToArray();
			Assert.Equal(expectedGear, observedGear);
			// Bootstrap and character creation can consume a different number of process RNG
			// draws before the frozen course begins. Start each course's RNG stream here.
			Rnd.SetProcessSeed(fixture.Seed);
			Assert.Equal(0L, Rnd.ActiveSeedDrawCount);
			await CaptureCourseResetAsync(player, directory);
			session.TraceDiagnostic("phase0-course-rng-reset", new Dictionary<string, object?>
			{
				["seed"] = fixture.Seed, ["gameMillis"] = fixture.Clock.NowMillis,
				["drawCount"] = Rnd.ActiveSeedDrawCount,
			});
		}

		async Task CaptureCourseResetAsync(Player player, string outputDirectory)
		{
			var worldInstance = player.GetPosition().GetWorldMapInstance();
			var npcs = worldInstance.GetNpcs().OrderBy(npc => npc.GetObjectId()).Select(npc => new
			{
				objectId = npc.GetObjectId(), templateId = npc.GetNpcId(),
				spawnX = npc.GetSpawn().GetX(), spawnY = npc.GetSpawn().GetY(),
				spawnZ = npc.GetSpawn().GetZ(), spawnHeading = npc.GetSpawn().GetHeading(),
				walkerId = npc.GetSpawn().GetWalkerId(), randomWalkRange = npc.GetSpawn().GetRandomWalkRange(),
				x = npc.GetX(), y = npc.GetY(), z = npc.GetZ(), heading = npc.GetHeading(),
				hp = npc.GetLifeStats().GetCurrentHp(), dead = npc.IsDead(),
				aiState = npc.GetAi().GetState().ToString(), aiSubState = npc.GetAi().GetSubState().ToString(),
				walkStep = npc.GetMoveController().GetCurrentStep()?.GetStepIndex(),
				moveX = npc.GetMoveController().GetTargetX2(),
				moveY = npc.GetMoveController().GetTargetY2(),
				moveZ = npc.GetMoveController().GetTargetZ2(),
				lastMoveUpdate = npc.GetMoveController().GetLastMoveUpdate(),
			}).ToArray();
			var snapshot = new
			{
				schemaVersion = 1, course = course.ToString(), encounter = encounter?.ToString(),
				seed = fixture.Seed,
				gameMillis = fixture.Clock.NowMillis, epoch = fixture.Epoch,
				mapId = player.GetWorldId(), instanceId = player.GetInstanceId(),
				player = new
				{
					objectId = player.GetObjectId(), level = player.GetLevel(),
					hp = player.GetLifeStats().GetCurrentHp(), mp = player.GetLifeStats().GetCurrentMp(),
					items = player.GetInventory().GetItems().OrderBy(item => item.GetObjectId())
						.Select(item => new { objectId = item.GetObjectId(), itemId = item.GetItemId(), count = item.GetItemCount() }),
					skillCooldowns = player.GetSkillCoolDowns()?.OrderBy(entry => entry.Key)
						.Select(entry => new { id = entry.Key, expiresAtMillis = entry.Value }),
					itemCooldowns = player.GetItemCoolDowns().OrderBy(entry => entry.Key)
						.Select(entry => new { id = entry.Key, expiresAtMillis = entry.Value.GetReuseTime(),
							useDelay = entry.Value.GetUseDelay() }),
				},
				rng = new { seed = Rnd.ActiveSeed, drawCount = Rnd.ActiveSeedDrawCount },
				npcs, armedTimers = fixture.Clock.GetArmedTimers(),
			};
			string json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true }) + "\n";
			string path = Path.Combine(outputDirectory, "course-reset.json");
			await File.WriteAllTextAsync(path, json);
			string hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
			session.TraceDiagnostic("phase1-course-reset", new Dictionary<string, object?>
			{
				["path"] = path, ["sha256"] = hash, ["npcCount"] = npcs.Length,
				["armedTimerCount"] = fixture.Clock.ArmedTimerCount,
			});
		}
	}

	private sealed record MauSpawnKey(int TemplateId, float X, float Y)
	{
		public bool Matches(Aion.GameServer.Model.GameObjects.Npc npc) =>
			npc.GetNpcId() == TemplateId && npc.GetSpawn() is { } spawn &&
			MathF.Abs(spawn.GetX() - X) < 0.01f && MathF.Abs(spawn.GetY() - Y) < 0.01f;
	}

	private sealed record MauEncounterStart(float X, float Y, float Z, byte Heading,
		int QuestVar, float CenterX, float CenterY, float ClearRadius, int WarmupSeconds,
		MauSpawnKey[] Keep);

	private static MauEncounterStart StartFor(NaturalMauEncounter encounter) => encounter switch
	{
		NaturalMauEncounter.IsolatedStalker => new(669.08636f, 984.97394f, 309.64282f, 60,
			1, 657.303f, 966.940f, 55f, 0, [new(210750, 657.303f, 966.940f)]),
		NaturalMauEncounter.TwoAttackerPull => new(656f, 903f, 310.3f, 0,
			1, 669f, 910f, 42f, 0, [new(211284, 663.958f, 912.591f), new(211284, 674.597f, 908.080f)]),
		NaturalMauEncounter.MovingPatrol => new(625f, 882f, 311f, 0,
			1, 641.74f, 865.61f, 43f, 5, [new(210407, 641.74f, 865.61f)]),
		NaturalMauEncounter.BlockedGeneratorRejoin => new(628.25134f, 894.2491f, 309.9224f, 24,
			8, 0, 0, 0, 0, []),
		NaturalMauEncounter.HatataAlone => new(646f, 895f, 311f, 0,
			1, 662.601f, 882.597f, 43f, 0, [new(210409, 662.601f, 882.597f)]),
		NaturalMauEncounter.HatataWithAdd => new(660f, 884.5f, 318.7f, 0,
			1, 662.601f, 882.597f, 43f, 0, [new(210409, 662.601f, 882.597f),
				new(211284, 656.619f, 894.842f)]),
		_ => throw new ArgumentOutOfRangeException(nameof(encounter)),
	};
}
