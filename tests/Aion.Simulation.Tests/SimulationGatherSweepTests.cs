using System.Globalization;
using System.Xml.Linq;
using Aion.Bots.Protocol;
using Aion.Bots.Scenarios;
using Aion.Bots.World;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Dataholders;
using Aion.GameServer.GeoEngine.Collision;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Instance;
using Aion.GameServer.Services.Items;
using Aion.GameServer.TestKit;
using Aion.GameServer.World;
using Aion.GameServer.World.Geo;

namespace Aion.Simulation.Tests;

public sealed partial class SimulationFastScenarioTests
{
	private async Task RunGatherSweepAsync(ScenarioDefinition scenario, bool includeHistory)
	{
		string root = RealStaticData.RepoRoot();
		int[] ids = XDocument.Load(Path.Combine(root, "game-server/data/static_data/gatherables/gatherable_templates.xml"))
			.Root!.Elements("gatherable_template").Select(e => (int)e.Attribute("id")!).Order().ToArray();
		Assert.Equal(DataManager.GATHERABLE_DATA.Size(), ids.Length);
		Assert.Equal(ids, scenario.Consumes.Where(r => r.Kind == ScenarioResourceKind.Gatherable).Select(r => r.Id).Order());
		var report = new DataSweepReport("gatherables", ids.Select(Id));
		string reportPath = Path.Combine(Environment.GetEnvironmentVariable("AION_E2E_RUN_DIR")
			?? throw new InvalidOperationException("Sweep requires a run directory."), "data-sweeps", "gatherables.json");
		report.Save(reportPath);
		// This independent source inventory prevents a missing loaded group from masquerading as no content.
		var declaredIds = Directory.EnumerateFiles(Path.Combine(root, "game-server/data/static_data/spawns"), "*.xml", SearchOption.AllDirectories)
			.SelectMany(path => XDocument.Load(path).Descendants("spawn"))
			.Select(e => (int?)e.Attribute("npc_id") ?? 0).Where(id => ids.Contains(id)).ToHashSet();
		var maps = DataManager.WORLD_MAPS_DATA.Select(m => m.GetMapId()).Order().ToArray();
		var instances = new Dictionary<int, WorldMapInstance>();
		using var policy = NewEconomyPolicy(scenario.Id, includeHistory);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(20)); var setupToken = timeout.Token;
		await using var elyos = new SimulationL0Session(fixture, policy, "b01", 121, "Elygathersweep");
		await using var asmodian = new SimulationL0Session(fixture, policy, "b02", 122, "Asmogathersweep", Race.ASMODIANS);
		foreach (var subject in new[] { elyos, asmodian })
		{
			subject.BeginStep("setup", "ordinary-character-and-gm-level-skill-setup");
			await subject.LoginAndAuthenticateAsync(setupToken); await subject.CreateCharacterAsync(setupToken);
			await subject.EnterWorldAsync(setupToken); await subject.SynchronizeAsync(setupToken);
			var subjectPlayer = fixture.World.GetPlayer(subject.CharacterId);
			Assert.True(ClassChangeService.SetClass(subjectPlayer, PlayerClass.GLADIATOR, validate: true, updateDaevaStatus: true));
			subjectPlayer.GetCommonData().SetLevel(65);
			Assert.Equal(0, subjectPlayer.AccessLevel); Assert.False(subjectPlayer.IsStaff());
		}
		var session = elyos;
		var player = fixture.World.GetPlayer(session.CharacterId);
		int failureChance = CraftConfig.MAX_GATHER_FAILURE_CHANCE;
		CraftConfig.MAX_GATHER_FAILURE_CHANCE = 0;
		try
		{
			foreach (int id in ids)
			{
				using var rowTimeout = CancellationTokenSource.CreateLinkedTokenSource(setupToken);
				rowTimeout.CancelAfter(TimeSpan.FromSeconds(45)); var token = rowTimeout.Token;
				using var rowPolicy = NewEconomyPolicy(scenario.Id, includeHistory: false);
				var details = new Dictionary<string, string>();
				bool logChecked = false;
				try
				{
					session.BeginStep($"gather-{id}", "gather-shipped-template-once");
					Console.WriteLine($"SWEEP-GATHER {id}: starting");
					var template = DataManager.GATHERABLE_DATA.GetGatherableTemplate(id);
					Assert.NotNull(template);
					var candidates = new List<Gatherable>();
					fixture.World.ForEachObject(o => { if (o is Gatherable g && g.IsSpawned() && g.GetObjectTemplate().GetTemplateId() == id) candidates.Add(g); });
					var spawnMaps = maps.Where(map => DataManager.SPAWNS_DATA.GetSpawnsForNpc(map, id).Any(g => g.GetSpawnTemplates().Count != 0)).ToArray();
					details["declaredMapIds"] = string.Join(',', spawnMaps);
					if (candidates.Count == 0 && spawnMaps.Length == 0)
					{
						Assert.False(declaredIds.Contains(id), $"Gatherable {id} has a shipped spawn not represented by loaded map groups.");
						logChecked = true; rowPolicy.AssertClean();
						report.Record(Id(id), DataSweepStatus.Unreachable, "No shipped spawn definition or runtime spawn", details);
						continue;
					}
					if (candidates.Count == 0)
					{
						foreach (int mapId in spawnMaps)
						{
							var map = fixture.World.GetWorldMap(mapId);
							if (!map.IsInstanceType()) continue;
							if (!instances.TryGetValue(mapId, out var instance))
							{
								instance = InstanceService.GetNextAvailableInstance(mapId, 0, 0, 1, autoDestroy: false);
								instances.Add(mapId, instance);
							}
							candidates.AddRange(instance.OfType<Gatherable>().Where(g => g.IsSpawned() && g.GetObjectTemplate().GetTemplateId() == id));
							if (candidates.Count > 0) break;
						}
					}
					Assert.NotEmpty(candidates); // A declared but failed spawn is a failure, never "unreachable".
					// Prefer a safe shipped spot; picking arbitrary cross-faction guard areas causes real interruptions.
					var ordered = candidates.OrderBy(g => g.GetWorldId()).ThenBy(g => g.GetInstanceId()).ThenBy(g => g.GetX()).ThenBy(g => g.GetY()).ToArray();
					var enemies = new Dictionary<(int Map, int Instance), Npc[]>();
					float Safety(Gatherable g)
					{
						var actor = fixture.World.GetPlayer(SubjectFor(g.GetWorldId()).CharacterId);
						var key = (g.GetWorldId(), g.GetInstanceId());
						if (!enemies.TryGetValue(key, out var npcs))
							enemies[key] = npcs = g.GetPosition().GetWorldMapInstance().OfType<Npc>().Where(n => n.IsSpawned() && !n.IsDead() && n.IsEnemy(actor)).ToArray();
						return npcs.Select(n => MathF.Pow(n.GetX() - g.GetX(), 2) + MathF.Pow(n.GetY() - g.GetY(), 2) + MathF.Pow(n.GetZ() - g.GetZ(), 2))
							.DefaultIfEmpty(float.MaxValue).Min();
					}
					// A fixed west-side approach can be inside a rock. Select a visible, in-range
					// director setup point instead; keep the server's normal gather LOS check enabled.
					var visible = ordered.OrderBy(g => Safety(g) >= 50 * 50 ? 0 : 1)
						.ThenBy(g => Safety(g) >= 50 * 50 ? 0 : -Safety(g))
						.Select(g => (Node: g, Point: FindVisibleGatherApproach(g,
							fixture.World.GetPlayer(SubjectFor(g.GetWorldId()).CharacterId).GetRace())))
						.FirstOrDefault(candidate => candidate.Point != null);
					Assert.NotNull(visible.Node); Assert.NotNull(visible.Point);
					var node = visible.Node;
					var approach = visible.Point.Value;
					session = SubjectFor(node.GetWorldId()); player = fixture.World.GetPlayer(session.CharacterId);
					session.BeginStep($"gather-{id}", "gather-shipped-template-once");
					details["race"] = player.GetRace().ToString();
					details["nearestEnemyDistance"] = MathF.Sqrt(Safety(node)).ToString(CultureInfo.InvariantCulture);
					details["mapId"] = Id(node.GetWorldId()); details["instanceId"] = Id(node.GetInstanceId());
					details["position"] = FormattableString.Invariant($"{node.GetX()},{node.GetY()},{node.GetZ()}");
					details["skillId"] = Id(template.GetHarvestSkill());
					player.GetSkillList().AddSkill(player, template.GetHarvestSkill(), Math.Min(549, template.GetSkillLevel() + 41));
					Assert.True(player.GetSkillList().GetSkillLevel(template.GetHarvestSkill()) >= template.GetSkillLevel());
					if (template.GetEraseValue() > 0)
						Assert.Equal(0, ItemService.AddItem(player, template.GetRequiredItemId(), template.GetEraseValue()));
					details["approach"] = FormattableString.Invariant($"{approach.X},{approach.Y},{approach.Z}");
					await TeleportForSetupAsync(session, player, node.GetWorldId(), approach.X, approach.Y, approach.Z, token, node.GetInstanceId());
					await session.MoveToPositionAsync(approach, token);
					Assert.True(GeoService.GetInstance().CanSee(player, node), "Gather sweep setup must have ordinary line of sight.");
					await session.SynchronizeAsync(token);
					var before = Totals();
					int start = session.PacketHistory.Count;
					foreach (var packet in session.Api.Gather(node.GetObjectId())) await session.SendPacketAsync(packet, token);
					await session.SynchronizeAsync(token);
					Assert.NotNull(player.GetInteractionTask());
					long deadline = fixture.Clock.NowMillis + 60_000;
					while (player.GetInteractionTask() != null)
					{
						long next = fixture.Clock.NextDueMillis ?? throw new InvalidDataException("Gathering has no scheduled work.");
						Assert.InRange(next, fixture.Clock.NowMillis, deadline);
						await session.AdvanceAsync(TimeSpan.FromMilliseconds(next - fixture.Clock.NowMillis), token);
						if (fixture.Clock.Faults.Count > 0)
							throw new AggregateException("Gather sweep observed a virtual timer fault.", fixture.Clock.Faults.Select(f => f.Exception));
					}
					await session.SynchronizeAsync(token);
					var completion = Assert.Single(session.PacketHistory.Skip(start), p => p.PacketType == typeof(SM_GATHER_UPDATE) && p.Get<byte>("action") >= 5);
					Assert.Equal((byte)6, completion.Get<byte>("action"));
					int product = completion.Get<int>("itemId");
					var materials = template.GetCheckType() == 2 && template.GetRequiredItemId() > 0
						? template.GetExtraMaterials().GetMaterial() : template.GetMaterials().GetMaterial();
					Assert.NotEmpty(materials); Assert.Contains(materials, m => m.GetItemId() == product);
					int count = Rates.GATHERING_COUNT.CalcResult(player, 1);
					Assert.True(count > 0, "A successful gather must produce an item.");
					before[product] = before.GetValueOrDefault(product) + count;
					if (template.GetEraseValue() > 0)
					{
						int input = template.GetRequiredItemId(); before[input] -= template.GetEraseValue();
						if (before[input] == 0) before.Remove(input);
					}
					Assert.Equal(before.OrderBy(p => p.Key), Totals().OrderBy(p => p.Key));
					var server = player.GetInventory().GetItemsWithKinah().Concat(player.GetEquipment().GetEquippedItems())
						.GroupBy(i => i.GetItemId()).ToDictionary(g => g.Key, g => g.Sum(i => i.GetItemCount()));
					Assert.Equal(before.OrderBy(p => p.Key), server.OrderBy(p => p.Key));
					Assert.Null(player.GetInteractionTask()); Assert.Equal(0, node.GetController().GetGatheringPlayerId());
					details["productId"] = Id(product); details["productCount"] = Id(count);
					// GM-equivalent fixture cleanup after the product and the entire inventory delta were verified.
					Assert.True(player.GetInventory().DecreaseByItemId(product, count));
					await session.SynchronizeAsync(token);
					logChecked = true; rowPolicy.AssertClean();
					report.Record(Id(id), DataSweepStatus.Passed, "CM gathering completed; exact input/product and whole-inventory deltas verified against packets and server", details);
					Console.WriteLine($"SWEEP-GATHER {id}: passed -> {product} x{count}");
					session.PacketHistory.Clear(); session.PacketObservations.Clear();
				}
				catch (Exception error)
				{
					details["recentPackets"] = string.Join('\n', session.PacketHistory.TakeLast(20).Select(p => p.PacketType.Name + " " + System.Text.Json.JsonSerializer.Serialize(p.Fields)));
					if (!logChecked)
					{
						try { rowPolicy.AssertClean(); }
						catch (Exception logError) { details["logFailure"] = logError.ToString(); }
					}
					report.Record(Id(id), DataSweepStatus.Failed, error.ToString(), details);
					Console.WriteLine($"SWEEP-GATHER {id}: failed\n" + System.Text.Json.JsonSerializer.Serialize(details));
					throw;
				}
				finally { report.Save(reportPath); }
			}
			Assert.True(report.Complete);
			foreach (var subject in new[] { elyos, asmodian })
			{
				await subject.QuitAsync(setupToken); await subject.VerifyOfflineAsync(setupToken);
			}
			policy.AssertClean();
		}
		finally { CraftConfig.MAX_GATHER_FAILURE_CHANCE = failureChance; report.Save(reportPath); }
		Dictionary<int, long> Totals() => session.Api.World.Inventory.Values.GroupBy(i => i.ItemId).ToDictionary(g => g.Key, g => g.Sum(i => i.Count));
		SimulationL0Session SubjectFor(int map) => map / 10000000 == 22 || map == 710010000 ? asmodian : elyos;
		static string Id(int value) => value.ToString(CultureInfo.InvariantCulture);
	}

	// Director setup only, including aerial nodes: this does not claim grounded natural travel.
	// Java GeoService.canSee uses the target's height and static-id/race ignore properties.
	private static BotPosition? FindVisibleGatherApproach(Gatherable node, Race race)
	{
		float upper = node.GetObjectTemplate().GetBoundRadius().GetUpper();
		float targetOffset = upper > 2.5f ? upper / 2 : 1.25f;
		var geo = GeoService.GetInstance().GetMap(node.GetWorldId());
		for (int direction = 0; direction < 8; direction++)
		{
			double angle = Math.PI + direction * Math.PI / 4;
			var point = new BotPosition(node.GetX() + (float)Math.Cos(angle), node.GetY() + (float)Math.Sin(angle), node.GetZ(), 0);
			if (geo.CanSee(point.X, point.Y, point.Z + 1.25f, node.GetX(), node.GetY(), node.GetZ() + targetOffset,
				node.GetInstanceId(), IgnoreProperties.Of(race, node.GetSpawn()?.GetStaticId() ?? -1))) return point;
		}
		return null;
	}
}
