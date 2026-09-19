using System.Globalization;
using System.Xml.Linq;
using Aion.Bots.Movement;
using Aion.Bots.Protocol;
using Aion.Bots.Scenarios;
using Aion.Bots.World;
using Aion.Commons.Database;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Instance;
using Aion.GameServer.Services.Items;
using Aion.GameServer.TestKit;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.Simulation.Tests;

public sealed partial class SimulationFastScenarioTests
{
	private async Task RunBindSweepAsync(ScenarioDefinition scenario, bool includeHistory)
	{
		string root = RealStaticData.RepoRoot();
		int[] ids = XDocument.Load(Path.Combine(root, "game-server/data/static_data/bind_points/bind_points.xml"))
			.Root!.Elements("bind_point").Select(e => (int)e.Attribute("npcid")!).Order().ToArray();
		Assert.NotEmpty(ids); Assert.Equal(DataManager.BIND_POINT_DATA.Size(), ids.Length);
		var report = new DataSweepReport("bindpoints", ids.Select(Id));
		string reportPath = Path.Combine(Environment.GetEnvironmentVariable("AION_E2E_RUN_DIR")
			?? throw new InvalidOperationException("Sweep requires a run directory."), "data-sweeps", "bindpoints.json");
		report.Save(reportPath);
		var declaredIds = Directory.EnumerateFiles(Path.Combine(root, "game-server/data/static_data/spawns"), "*.xml", SearchOption.AllDirectories)
			.SelectMany(path => XDocument.Load(path).Descendants("spawn"))
			.Select(e => (int?)e.Attribute("npc_id") ?? 0).Where(ids.Contains).ToHashSet();
		int[] maps = DataManager.WORLD_MAPS_DATA.Select(m => m.GetMapId()).Order().ToArray();
		var instances = new Dictionary<int, WorldMapInstance>();
		using var policy = NewEconomyPolicy(scenario.Id, includeHistory);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(20));
		await using var elyos = new SimulationL0Session(fixture, policy, "b01", 125, "Elybindsweep");
		await using var asmodian = new SimulationL0Session(fixture, policy, "b02", 126, "Asmobindsweep", Race.ASMODIANS);
		foreach (var subject in new[] { elyos, asmodian })
		{
			subject.BeginStep("setup", "ordinary-character-and-gm-level-funds-setup");
			await subject.LoginAndAuthenticateAsync(timeout.Token); await subject.CreateCharacterAsync(timeout.Token);
			await subject.EnterWorldAsync(timeout.Token); await subject.SynchronizeAsync(timeout.Token);
			var actor = fixture.World.GetPlayer(subject.CharacterId);
			Assert.True(ClassChangeService.SetClass(actor, PlayerClass.GLADIATOR, validate: true, updateDaevaStatus: true));
			actor.GetCommonData().SetLevel(65);
			Assert.Equal(0, ItemService.AddItem(actor, BotWorldModel.KinahItemId, 100_000_000));
			Assert.Equal(0, actor.AccessLevel); Assert.False(actor.IsStaff());
		}
		var session = elyos;
		foreach (int id in ids)
		{
			using var rowTimeout = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token);
			rowTimeout.CancelAfter(TimeSpan.FromSeconds(45)); var token = rowTimeout.Token;
			using var rowPolicy = NewEconomyPolicy(scenario.Id, includeHistory: false);
			var details = new Dictionary<string, string>(); bool logChecked = false;
			try
			{
				var template = DataManager.BIND_POINT_DATA.GetBindPointTemplate(id); Assert.NotNull(template);
				var candidates = new List<Npc>();
				fixture.World.ForEachObject(o => { if (o is Npc npc && npc.IsSpawned() && npc.GetNpcId() == id) candidates.Add(npc); });
				int[] spawnMaps = maps.Where(map => DataManager.SPAWNS_DATA.GetSpawnsForNpc(map, id).Any(g => g.GetSpawnTemplates().Count != 0)).ToArray();
				details["declaredMapIds"] = string.Join(',', spawnMaps);
				if (candidates.Count == 0 && spawnMaps.Length == 0)
				{
					Assert.False(declaredIds.Contains(id), $"Bind point {id} has a shipped spawn not represented by loaded map groups.");
					logChecked = true; rowPolicy.AssertClean();
					report.Record(Id(id), DataSweepStatus.Unreachable, "No shipped spawn definition or runtime spawn", details);
					continue;
				}
				if (candidates.Count == 0)
					foreach (int mapId in spawnMaps)
					{
						if (!fixture.World.GetWorldMap(mapId).IsInstanceType()) continue;
						if (!instances.TryGetValue(mapId, out var instance))
							instances.Add(mapId, instance = InstanceService.GetNextAvailableInstance(mapId, 0, 0, 1, autoDestroy: false));
						candidates.AddRange(instance.GetNpcs(id).Where(n => n.IsSpawned()));
						if (candidates.Count > 0) break;
					}
				Assert.NotEmpty(candidates);
				var obelisk = candidates.OrderBy(n => n.GetWorldId()).ThenBy(n => n.GetInstanceId()).ThenBy(n => n.GetX()).First();
				session = obelisk.GetRace() == Race.ASMODIANS || obelisk.GetTribe() == TribeClass.FIELD_OBJECT_DARK ? asmodian : elyos;
				var player = fixture.World.GetPlayer(session.CharacterId);
				session.BeginStep($"bind-{id}", "normal-bind-persist-death-and-revive");
				details["mapId"] = Id(obelisk.GetWorldId()); details["instanceId"] = Id(obelisk.GetInstanceId());
				details["race"] = player.GetRace().ToString(); details["npcId"] = Id(id);
				await TeleportForSetupAsync(session, player, obelisk.GetWorldId(), obelisk.GetX() - 5, obelisk.GetY(), obelisk.GetZ(), token, obelisk.GetInstanceId());
				await session.MoveToPositionAsync(new BotPosition(obelisk.GetX() - 1, obelisk.GetY(), obelisk.GetZ(), 0), token);
				await session.SynchronizeAsync(token);
				long kinah = session.Api.World.Kinah;
				int start = session.PacketHistory.Count;
				await session.SendPacketAsync(session.Api.TalkTo(obelisk.GetObjectId()), token);
				await session.SynchronizeAsync(token);
				var question = Assert.Single(session.PacketHistory.Skip(start), p => p.PacketType == typeof(SM_QUESTION_WINDOW));
				await session.SendPacketAsync(GameClientPackets.QuestionResponse(question.Get<int>("code"), 1, question.Get<int>("senderId")), token);
				await session.SynchronizeAsync(token);
				Assert.Contains(session.PacketHistory.Skip(start), p => p.PacketType == typeof(SM_BIND_POINT_INFO));
				var bind = Assert.IsType<BindPointPosition>(player.GetBindPoint());
				Assert.Equal(obelisk.GetWorldId(), bind.GetMapId());
				Assert.InRange(PositionUtil.GetDistance(bind.GetX(), bind.GetY(), bind.GetZ(), player.GetX(), player.GetY(), player.GetZ()), 0, 0.1);
				Assert.Equal(kinah - Math.Max(0, template.GetPrice()), session.Api.World.Kinah);
				Assert.Equal(session.Api.World.Kinah, player.GetInventory().GetKinah());
				using (var connection = DatabaseFactory.GetConnection())
				{
					connection.Open(); using var command = connection.CreateCommand();
					command.CommandText = "SELECT map_id,x,y,z,heading FROM player_bind_point WHERE player_id=@player";
					command.Parameters.AddWithValue("@player", player.GetObjectId());
					using var reader = command.ExecuteReader(); Assert.True(reader.Read());
					Assert.Equal(bind.GetMapId(), reader.GetInt32(0));
					// MySQL FLOAT's text-protocol representation rounds the stored single-precision coordinates.
					Assert.InRange(Math.Abs(bind.GetX() - reader.GetFloat(1)), 0, 0.01f);
					Assert.InRange(Math.Abs(bind.GetY() - reader.GetFloat(2)), 0, 0.01f);
					Assert.InRange(Math.Abs(bind.GetZ() - reader.GetFloat(3)), 0, 0.01f);
					Assert.Equal(bind.GetHeading(), reader.GetByte(4)); Assert.False(reader.Read());
				}
				// GM sets up the ledge; honest fall movement causes death through the real movement handler.
				// Die away from the saved point so a missing return teleport cannot accidentally pass.
				var landing = new BotPosition(player.GetX() + 30, player.GetY(), player.GetZ(), player.GetHeading());
				var ledge = landing with { Z = landing.Z + 60 };
				player.GetPosition().SetXYZH(ledge.X, ledge.Y, ledge.Z, ledge.Heading);
				var mover = new BotMover(session.Api.World, session.Api.Timing);
				await session.ExecuteMovementAsync(mover.CreateFallPlan([landing], ledge,
					session.Api.World.MovementSpeed ?? throw new InvalidDataException("No movement speed observed.")), token);
				await session.AdvanceAsync(TimeSpan.FromMilliseconds(500), token); // Real delayed death notification, as in M3.
				await session.SynchronizeAsync(token);
				Assert.True(player.IsDead()); Assert.Contains(session.PacketHistory.Skip(start), p => p.PacketType == typeof(SM_DIE));
				Assert.True(PositionUtil.GetDistance(bind.GetX(), bind.GetY(), bind.GetZ(), player.GetX(), player.GetY(), player.GetZ()) >= 20);
				await session.SendPacketAsync(session.Api.Revive(), token);
				await session.WaitForPacketAsync(typeof(SM_CHANNEL_INFO), token);
				await session.SynchronizeAsync(token);
				Assert.False(player.IsDead()); Assert.True(player.GetLifeStats().GetCurrentHp() > 0);
				Assert.Equal(bind.GetMapId(), player.GetWorldId()); Assert.Equal(bind.GetMapId(), session.Api.World.MapId);
				Assert.InRange(PositionUtil.GetDistance(bind.GetX(), bind.GetY(), bind.GetZ(), player.GetX(), player.GetY(), player.GetZ()), 0, 1.5);
				details["bindPosition"] = FormattableString.Invariant($"{bind.GetX()},{bind.GetY()},{bind.GetZ()}");
				details["reviveMapId"] = Id(player.GetWorldId());
				details["revivePosition"] = FormattableString.Invariant($"{player.GetX()},{player.GetY()},{player.GetZ()}");
				details["persisted"] = "true"; details["deathObserved"] = "true"; details["bindObserved"] = "true";
				logChecked = true; rowPolicy.AssertClean();
				report.Record(Id(id), DataSweepStatus.Passed, "Normal bind dialog and fee, fresh SQL position, fall death and CM_REVIVE return verified", details);
				Console.WriteLine($"SWEEP-BIND {id}: passed at {bind.GetMapId()}");
				session.PacketHistory.Clear(); session.PacketObservations.Clear();
			}
			catch (Exception error)
			{
				details["recentPackets"] = string.Join('\n', session.PacketHistory.TakeLast(20).Select(p => p.PacketType.Name + " " + System.Text.Json.JsonSerializer.Serialize(p.Fields)));
				if (!logChecked) { try { rowPolicy.AssertClean(); } catch (Exception logError) { details["logFailure"] = logError.ToString(); } }
				report.Record(Id(id), DataSweepStatus.Failed, error.ToString(), details);
				Console.WriteLine($"SWEEP-BIND {id}: failed\n" + System.Text.Json.JsonSerializer.Serialize(details));
				throw;
			}
			finally { report.Save(reportPath); }
		}
		Assert.True(report.Complete);
		foreach (var subject in new[] { elyos, asmodian })
		{
			await subject.QuitAsync(timeout.Token); await subject.VerifyOfflineAsync(timeout.Token);
		}
		policy.AssertClean();
		static string Id(int value) => value.ToString(CultureInfo.InvariantCulture);
	}
}
