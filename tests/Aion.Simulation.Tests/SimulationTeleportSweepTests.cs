using System.Globalization;
using System.Xml.Linq;
using Aion.Bots.Protocol;
using Aion.Bots.Scenarios;
using Aion.Bots.World;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Animations;
using Aion.GameServer.Model.Base;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Siege;
using Aion.GameServer.Model.Templates.Spawns.Siegespawns;
using Aion.GameServer.Model.Templates.Spawns.Basespawns;
using Aion.GameServer.Model.Templates.Teleport;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Instance;
using Aion.GameServer.Services.Items;
using Aion.GameServer.TestKit;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.Simulation.Tests;

public sealed partial class SimulationFastScenarioTests
{
	private async Task RunTeleportSweepAsync(ScenarioDefinition scenario, bool includeHistory)
	{
		string root = RealStaticData.RepoRoot();
		int[] ids = XDocument.Load(Path.Combine(root, "game-server/data/static_data/teleport_location.xml"))
			.Root!.Elements("teleloc_template").Select(e => (int)e.Attribute("loc_id")!).Order().ToArray();
		Assert.NotEmpty(ids); Assert.Equal(DataManager.TELELOCATION_DATA.Size(), ids.Length);
		var teleporterXml = XDocument.Load(Path.Combine(root, "game-server/data/static_data/npc_teleporter.xml"));
		var templates = teleporterXml.Root!.Elements("teleporter_template").Select(e =>
			DataManager.TELEPORTER_DATA.GetTeleporterTemplateByTeleportId((int)e.Attribute("teleportId")!)).ToArray();
		Assert.Equal(DataManager.TELEPORTER_DATA.Size(), templates.Length); Assert.All(templates, t => Assert.NotNull(t));
		Assert.Empty(teleporterXml.Descendants("telelocation").Select(e => (int)e.Attribute("loc_id")!).Except(ids));
		var declaredIds = Directory.EnumerateFiles(Path.Combine(root, "game-server/data/static_data/spawns"), "*.xml", SearchOption.AllDirectories)
			.SelectMany(path => XDocument.Load(path).Descendants("spawn")).Select(e => (int?)e.Attribute("npc_id") ?? 0).ToHashSet();
		var report = new DataSweepReport("teleporters", ids.Select(Id));
		string reportPath = Path.Combine(Environment.GetEnvironmentVariable("AION_E2E_RUN_DIR")
			?? throw new InvalidOperationException("Sweep requires a run directory."), "data-sweeps", "teleporters.json");
		report.Save(reportPath);
		int[] maps = DataManager.WORLD_MAPS_DATA.Select(m => m.GetMapId()).Order().ToArray();
		var instances = new Dictionary<int, WorldMapInstance>();
		using var policy = NewEconomyPolicy(scenario.Id, includeHistory);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(30));
		await using var elyos = new SimulationL0Session(fixture, policy, "b01", 127, "Elyportsweep");
		await using var asmodian = new SimulationL0Session(fixture, policy, "b02", 128, "Asmoportsweep", Race.ASMODIANS);
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
			rowTimeout.CancelAfter(TimeSpan.FromSeconds(60)); var token = rowTimeout.Token;
			using var rowPolicy = NewEconomyPolicy(scenario.Id, includeHistory: false);
			var details = new Dictionary<string, string>(); bool logChecked = false;
			Action? restoreSourceSiege = null;
			Action? restoreSourceBase = null;
			try
			{
				var destination = DataManager.TELELOCATION_DATA.GetTelelocationTemplate(id); Assert.NotNull(destination);
				var routes = templates.Where(t => t.GetTeleLocIdData().GetTeleportLocation(id) != null).ToArray();
				var npcIds = routes.SelectMany(t => t.GetNpcIds()).ToHashSet();
				details["routeNpcIds"] = string.Join(',', npcIds.Order());
				var candidates = new List<Npc>();
				fixture.World.ForEachObject(o => { if (o is Npc n && n.IsSpawned() && npcIds.Contains(n.GetNpcId())) candidates.Add(n); });
				if (candidates.Count == 0)
				{
					// Siege peace spawns are loaded separately from ordinary map spawns. Materialize
					// an existing race-specific group as explicit director setup, not production boot.
					var sieges = SiegeService.GetInstance();
					var source = sieges.GetSiegeLocations().Keys.Order()
						.SelectMany(key => DataManager.SPAWNS_DATA.GetSiegeSpawnsByLocId(key) ?? [])
						.Where(g => npcIds.Contains(g.GetNpcId())).SelectMany(g => g.GetSpawnTemplates())
						.OfType<SiegeSpawnTemplate>().FirstOrDefault(s => s.GetSiegeModType() == SiegeModType.PEACE
							&& s.GetSiegeRace() is SiegeRace.ELYOS or SiegeRace.ASMODIANS);
					if (source != null)
					{
						int sourceId = source.GetSiegeId(); var location = sieges.GetSiegeLocation(sourceId);
						Assert.Empty(fixture.World.GetLocalSiegeNpcs(sourceId));
						var originalRace = location.GetRace();
						restoreSourceSiege = () => { sieges.DeSpawnNpcs(sourceId); location.SetRace(originalRace); };
						location.SetRace(source.GetSiegeRace());
						sieges.SpawnNpcs(sourceId, source.GetSiegeRace(), SiegeModType.PEACE);
						details["setupSourceSiegeId"] = Id(sourceId);
						details["setupSourceSiegeRace"] = source.GetSiegeRace().ToString();
						fixture.World.ForEachObject(o => { if (o is Npc n && n.IsSpawned() && npcIds.Contains(n.GetNpcId())) candidates.Add(n); });
					}
				}
				if (candidates.Count == 0)
				{
					var bases = BaseService.GetInstance();
					var source = bases.GetBaseLocations().OrderBy(b => b.GetId())
						.SelectMany(b => DataManager.SPAWNS_DATA.GetBaseSpawnsByLocId(b.GetId()) ?? [])
						.Where(g => npcIds.Contains(g.GetNpcId())).SelectMany(g => g.GetSpawnTemplates())
						.OfType<BaseSpawnTemplate>().FirstOrDefault(s => s.GetOccupier() is not (BaseOccupier.BALAUR or BaseOccupier.PEACE));
					if (source != null)
					{
						int sourceId = source.GetId(); var location = bases.GetBaseLocation(sourceId);
						var originalOccupier = location.GetOccupier(); bool wasActive = bases.IsActive(sourceId);
						var factionPlayers = new[] { fixture.World.GetPlayer(elyos.CharacterId), fixture.World.GetPlayer(asmodian.CharacterId) };
						var originalFactions = factionPlayers.Select(p => p.GetPanesterraFaction()).ToArray();
						restoreSourceBase = () =>
						{
							try
							{
								if (wasActive) bases.Capture(sourceId, originalOccupier);
								else { bases.Stop(sourceId); location.SetOccupier(originalOccupier); }
							}
							finally { for (int i = 0; i < factionPlayers.Length; i++) factionPlayers[i].SetPanesterraFaction(originalFactions[i]); }
						};
						if (source.GetOccupier().GetPanesterraFaction() is { } faction)
						{
							foreach (var actor in factionPlayers) actor.SetPanesterraFaction(faction);
							details["setupPanesterraFaction"] = faction.ToString();
						}
						if (wasActive) bases.Capture(sourceId, source.GetOccupier());
						else { location.SetOccupier(source.GetOccupier()); bases.Start(sourceId); }
						details["setupSourceBaseId"] = Id(sourceId);
						details["setupSourceBaseOccupier"] = source.GetOccupier().ToString();
						fixture.World.ForEachObject(o => { if (o is Npc n && n.IsSpawned() && npcIds.Contains(n.GetNpcId())) candidates.Add(n); });
					}
				}
				int[] spawnMaps = maps.Where(map => npcIds.Any(npc => DataManager.SPAWNS_DATA.GetSpawnsForNpc(map, npc).Any(g => g.GetSpawnTemplates().Count != 0))).ToArray();
				if (candidates.Count == 0 && spawnMaps.Length == 0)
				{
					Assert.False(npcIds.Any(declaredIds.Contains), $"Destination {id} has a shipped teleporter spawn absent from loaded groups.");
					logChecked = true; rowPolicy.AssertClean();
					report.Record(Id(id), DataSweepStatus.Unreachable, routes.Length == 0 ? "No incoming NPC teleporter route is shipped" : "No shipped spawn definition or runtime spawn for any incoming route NPC", details);
					continue;
				}
				if (candidates.Count == 0)
					foreach (int map in spawnMaps)
					{
						if (!fixture.World.GetWorldMap(map).IsInstanceType()) continue;
						if (!instances.TryGetValue(map, out var instance))
							instances.Add(map, instance = InstanceService.GetNextAvailableInstance(map, 0, 0, 1, autoDestroy: false));
						candidates.AddRange(instance.OfType<Npc>().Where(n => n.IsSpawned() && npcIds.Contains(n.GetNpcId())));
						if (candidates.Count > 0) break;
					}
				Assert.NotEmpty(candidates);
				var npc = candidates.OrderByDescending(n => IsFriendly(n, SubjectFor(n))).ThenBy(n => n.GetWorldId()).ThenBy(n => n.GetInstanceId()).ThenBy(n => n.GetObjectId()).First();
				session = SubjectFor(npc); var player = fixture.World.GetPlayer(session.CharacterId);
				Assert.True(IsFriendly(npc, session), $"No friendly spawned route NPC for destination {id}.");
				var route = DataManager.TELEPORTER_DATA.GetTeleporterTemplateByNpcId(npc.GetNpcId()).GetTeleLocIdData().GetTeleportLocation(id);
				Assert.NotNull(route);
				session.BeginStep($"teleport-{id}", "normal-teleport-selection-and-arrival");
				details["npcId"] = Id(npc.GetNpcId()); details["sourceMapId"] = Id(npc.GetWorldId());
				details["race"] = player.GetRace().ToString(); details["type"] = route.GetType_().ToString();
				details["teleportId"] = Id(route.GetTeleportId());
				if (route.GetRequiredQuest() is int questId && questId != 0 && !player.IsCompleteQuest(questId))
				{
					var quest = player.GetQuestStateList().GetQuestState(questId);
					if (quest == null) Assert.True(player.GetQuestStateList().AddQuest(questId, new QuestState(questId, QuestStatus.COMPLETE)));
					else quest.SetStatus(QuestStatus.COMPLETE);
				}
				details["setupRequiredQuest"] = Id(route.GetRequiredQuest());
				int siegeId = SiegeService.GetInstance().GetSiegeIdByLocId(id);
				SiegeLocation? siege = siegeId > 0 ? SiegeService.GetInstance().GetSiegeLocation(siegeId) : null;
				if (siegeId > 0) Assert.NotNull(siege);
				var oldRace = siege?.GetRace(); bool? oldTeleport = siege?.IsCanTeleport(null!);
				try
				{
					// GM-equivalent prerequisite setup only; no siege boot changes or security overrides.
					if (siege != null)
					{
						siege.SetRace(player.GetRace() == Race.ELYOS ? SiegeRace.ELYOS : SiegeRace.ASMODIANS);
						siege.SetCanTeleport(true); details["setupSiegeId"] = Id(siegeId);
					}
					await TeleportForSetupAsync(session, player, npc.GetWorldId(), npc.GetX() - 5, npc.GetY(), npc.GetZ(), token, npc.GetInstanceId());
					var approach = new BotPosition(npc.GetX() - 1, npc.GetY(), npc.GetZ(), 0);
					if (route.GetType_() == TeleportType.FLIGHT)
					{
						var path = DataManager.FLY_PATH.GetPathTemplate(route.GetTeleportId() / 1000); Assert.NotNull(path);
						float dx = path.GetStartX() - npc.GetX(), dy = path.GetStartY() - npc.GetY();
						float distance = MathF.Sqrt(dx * dx + dy * dy);
						// Some aircraft NPCs stand beside the departure point. Approach from that
						// side while remaining in ordinary talk range, using real ground movement.
						float offset = Math.Min(npc.GetObjectTemplate().GetTalkDistance(), distance);
						if (distance > 0) approach = new(npc.GetX() + dx / distance * offset, npc.GetY() + dy / distance * offset, npc.GetZ(), 0);
					}
					await session.MoveToPositionAsync(approach, token);
					Assert.True(PositionUtil.IsInTalkRange(player, npc));
					PacketSendUtility.SendPacket(player, new SM_PRICES()); await session.SynchronizeAsync(token);
					long fee = (session.Api.World.VendorPrices ?? throw new InvalidDataException("No service price packet.")).ServicePrice(route.GetPrice());
					long kinah = session.Api.World.Kinah;
					int start = session.PacketHistory.Count;
					await session.SendPacketAsync(session.Api.Teleport(npc.GetObjectId(), id), token);
					await session.SynchronizeAsync(token);
					BotPosition expected;
					int expectedMap;
					if (route.GetType_() == TeleportType.FLIGHT)
					{
						// Wire flight IDs encode the client path as pathId * 1000 + 1 (HiddenTeleportNpcAI too).
						Assert.Equal(1, route.GetTeleportId() % 1000);
						var path = DataManager.FLY_PATH.GetPathTemplate(route.GetTeleportId() / 1000); Assert.NotNull(path);
						Assert.Equal(path.GetStartWorldId(), player.GetWorldId());
						Assert.Equal(path.GetStartWorldId(), path.GetEndWorldId());
						Assert.InRange(PositionUtil.GetDistance(player, path.GetStartX(), path.GetStartY(), path.GetStartZ()), 0, 7);
						Assert.Contains(session.PacketHistory.Skip(start), p => p.PacketType == typeof(SM_EMOTION) && p.Get<byte>("emotionType") == (byte)EmotionType.START_FLYTELEPORT && p.Get<int>("teleportId") == route.GetTeleportId());
						expectedMap = path.GetEndWorldId(); expected = new(path.GetEndX(), path.GetEndY(), path.GetEndZ(), 0);
						details["flightMillis"] = Id(path.GetTimeInMs());
						await session.ExecuteMovementAsync(CapitalAscensionScenario.CreateQuestFlight(session.CurrentPosition, expected, expectedMap, TimeSpan.FromMilliseconds(path.GetTimeInMs())), token);
						await session.SendPacketAsync(GameClientPackets.Emotion((byte)EmotionType.LAND_FLYTELEPORT), token);
						await session.SynchronizeAsync(token);
						Assert.False(player.IsInFlyingState());
						// Refresh the session's persistence expectation at the completed client-flight position.
						await session.MoveToPositionAsync(expected, token);
					}
					else
					{
						expectedMap = destination.GetMapId(); expected = new(destination.GetX(), destination.GetY(), destination.GetZ(), (byte)destination.GetHeading());
						Assert.Contains(session.PacketHistory.Skip(start), p => p.PacketType == typeof(SM_TELEPORT_LOC));
						await session.WaitForPacketAsync(typeof(SM_PLAYER_INFO), token, p => p.Get<int>("objectId") == session.CharacterId);
						await session.SynchronizeAsync(token); session.AcceptTeleportPosition();
					}
					Assert.Equal(expectedMap, destination.GetMapId()); Assert.Equal(expectedMap, player.GetWorldId());
					Assert.Equal(expectedMap, session.Api.World.MapId); Assert.True(player.IsSpawned()); Assert.False(player.IsDead());
					AssertPosition(route.GetType_() == TeleportType.FLIGHT ? session.CurrentPosition : session.Api.World.Position, expected.X, expected.Y, expected.Z);
					Assert.InRange(PositionUtil.GetDistance(player, expected.X, expected.Y, expected.Z), 0, 0.1);
					Assert.Equal(kinah - fee, session.Api.World.Kinah); Assert.Equal(session.Api.World.Kinah, player.GetInventory().GetKinah());
					details["mapId"] = Id(expectedMap); details["instanceId"] = Id(player.GetInstanceId());
					details["position"] = FormattableString.Invariant($"{player.GetX()},{player.GetY()},{player.GetZ()}");
					details["fee"] = fee.ToString(CultureInfo.InvariantCulture);
					logChecked = true; rowPolicy.AssertClean();
					report.Record(Id(id), DataSweepStatus.Passed, "CM_TELEPORT_SELECT, ordinary completion/flight movement, exact fee and packet/server destination verified", details);
					Console.WriteLine($"SWEEP-TELEPORT {id}: passed {route.GetType_()} -> {expectedMap}");
					session.PacketHistory.Clear(); session.PacketObservations.Clear();
				}
				finally { if (siege != null) { siege.SetRace(oldRace!.Value); siege.SetCanTeleport(oldTeleport!.Value); } }
			}
			catch (Exception error)
			{
				details["recentPackets"] = string.Join('\n', session.PacketHistory.TakeLast(20).Select(p => p.PacketType.Name + " " + System.Text.Json.JsonSerializer.Serialize(p.Fields)));
				if (!logChecked) { try { rowPolicy.AssertClean(); } catch (Exception logError) { details["logFailure"] = logError.ToString(); } }
				report.Record(Id(id), DataSweepStatus.Failed, error.ToString(), details);
				Console.WriteLine($"SWEEP-TELEPORT {id}: failed\n" + System.Text.Json.JsonSerializer.Serialize(details)); throw;
			}
			finally
			{
				try { restoreSourceBase?.Invoke(); }
				finally { try { restoreSourceSiege?.Invoke(); } finally { report.Save(reportPath); } }
			}
		}
		Assert.True(report.Complete);
		foreach (var subject in new[] { elyos, asmodian }) { await subject.QuitAsync(timeout.Token); await subject.VerifyOfflineAsync(timeout.Token); }
		policy.AssertClean();
		bool IsFriendly(Npc npc, SimulationL0Session subject) => npc.GetType_(fixture.World.GetPlayer(subject.CharacterId)) is CreatureType.FRIEND or CreatureType.SUPPORT;
		SimulationL0Session SubjectFor(Npc npc) => IsFriendly(npc, asmodian) && !IsFriendly(npc, elyos) ? asmodian : elyos;
		static string Id(int value) => value.ToString(CultureInfo.InvariantCulture);
	}
}
