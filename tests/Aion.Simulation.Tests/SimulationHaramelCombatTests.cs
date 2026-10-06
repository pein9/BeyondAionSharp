using System.Text.Json;
using Aion.Bots.Dashboard;
using Aion.Bots.Navigation;
using Aion.Bots.Protocol;
using Aion.Bots.Scenarios;
using Aion.Bots.Tracing;
using Aion.Bots.World;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Items;
using Aion.GameServer.TestKit;

namespace Aion.Simulation.Tests;

public sealed partial class SimulationFastScenarioTests
{
	/// <summary>HM-04: unused access-0 account 225, actual Haramel HP/AI, incoming CG gear and regular skills.</summary>
	[SkippableFact]
	public async Task HaramelNormalBossAndNeighbourCombatProducesTheClericChestLootMovieAndExit()
	{
		Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
		using var policy = NewPolicy("HM04", includeHistory: false);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(20));
		CancellationToken token = timeout.Token;
		string root = RealStaticData.RepoRoot();
		string run = Environment.GetEnvironmentVariable("AION_SIM_RUN_ID") ?? "hm04-combat";
		string path = Path.Combine(root, "run", $"{run}.hm04.trace.jsonl");
		using var trace = BotActionTraceWriter.Open(path, run, "b01", "sim-player-225", virtualTime: () => TimeSpan.FromMilliseconds(fixture.Clock.NowMillis));
		await using var session = new SimulationL0Session(fixture, policy, "b01", 225, "Asimharfight", Race.ASMODIANS, trace, path);
		var dashboard = new LiveBotDashboardState();
		await using var host = new LiveBotDashboardHost(run, ["HM-04"], dashboard,
			int.Parse(Environment.GetEnvironmentVariable("AION_BOT_DASHBOARD_PORT") ?? "17880"));
		session.Dashboard = dashboard;
		if (host.Enabled) Console.WriteLine($"HM-04 dashboard: {host.Url}");
		var travel = new HaramelTravelProbe(this, fixture, session, token);
		await travel.InitializeAsync();
		Aion.GameServer.Model.GameObjects.Players.Player Server() => fixture.World.GetPlayer(session.CharacterId);
		// Labelled probe-only copies of the verified CG snapshot. The natural character is never modified.
		foreach ((int item, long slot) in new (int, long)[]
		{
			(125004139,4), (110551139,8), (111501065,16), (114501726,32), (112501015,2048), (113501074,4096),
			(120001521,64), (120001132,128), (122001664,256), (122000871,512), (121000751,1024), (123001109,65536),
		})
		{
			Assert.Equal(0, ItemService.AddItem(Server(), item, 1, allowInventoryOverflow: true));
			var owned = Server().GetInventory().GetItems().Single(i => i.GetItemId() == item);
			Assert.NotNull(Server().GetEquipment().EquipItem(owned.GetObjectId(), slot));
		}
		foreach ((int item, long count) in new (int, long)[] { (111101650,1), (188053787,1), (186000006,19), (169000004,75) })
			Assert.Equal(0, ItemService.AddItem(Server(), item, count, allowInventoryOverflow: true));
		var shards = Server().GetInventory().GetItems().Single(i => i.GetItemId() == 169000004);
		Assert.NotNull(Server().GetEquipment().EquipItem(shards.GetObjectId(), 8192));
		foreach (NaturalHelpTopUp supply in NaturalHelpItemSupply.Plan(24, new Dictionary<int, long>()))
		{
			NaturalHelpItemSupply.RequireApproved(supply.ItemId, supply.Count);
			Assert.Equal(0, ItemService.AddItem(Server(), supply.ItemId, supply.Count, allowInventoryOverflow: true));
		}
		await travel.SetupFortressAsync();
		await travel.FlyHubAsync(toHeart: true);
		await travel.BindAsync(700067, 813);
		await travel.PillarAsync(upper: false);
		await travel.PortalAsync(730319, 300200000);
		int instanceId = Server().GetInstanceId();
		int staff = session.Api.World.Inventory.Values.Single(i => i.ItemId == 101501357).ObjectId;
		Assert.DoesNotContain(11504, session.Api.World.Skills.Keys);
		BotNavigationGeometry Geometry() => BotNavigationGeometry.ForServerWorld(Server().GetInstanceId(), Race.ASMODIANS);
		BotPosition At(Npc npc) => new(npc.GetX(), npc.GetY(), npc.GetZ(), 0);
		Npc[] bosses = new[] { 216897,216907,216915,216922 }.Select(id => Server().GetWorldMapInstance().GetNpcs(id).Single()).ToArray();
		var packs = bosses.ToDictionary(b => b.GetNpcId(), b => Server().GetWorldMapInstance().GetNpcs()
			.Where(n => !bosses.Contains(n) && !n.IsDead() && Math.Abs(n.GetZ() - b.GetZ()) < 4 &&
				NaturalHostility.IsAggressive(n.GetObjectTemplate(), fixture.DataManager.StaticData.TribeRelations, TribeClass.PC_DARK) &&
				NaturalFlightPolicy.Distance(At(n), At(b)) < 35)
			.OrderBy(n => NaturalFlightPolicy.Distance(At(n), At(b))).Take(2).ToArray());
		Npc[] participants = bosses.Concat(packs.Values.SelectMany(p => p)).Distinct().ToArray();
		var original = participants.Select(n => new { npc = n.GetNpcId(), obj = n.GetObjectId(), hp = n.GetLifeStats().GetCurrentHp(), maxHp = n.GetLifeStats().GetMaxHp(), position = At(n) }).ToArray();
		foreach (Npc npc in participants) Assert.Equal(npc.GetLifeStats().GetMaxHp(), npc.GetLifeStats().GetCurrentHp());
		Assert.Equal(new[] { 2030,2138,2223,2223 }, bosses.Select(b => b.GetLifeStats().GetMaxHp()).ToArray());
		Assert.True(packs.Values.Sum(p => p.Length) >= 4, "Preserve actual shipped neighbours for the normal pulls.");
		int setupClears = 0, setupMoves = 0;
		async Task SetupAtAsync(BotPosition at)
		{
			Npc[] clear = Server().GetWorldMapInstance().GetNpcs().Where(n => !participants.Contains(n) && !n.IsDead() &&
				NaturalHostility.IsAggressive(n.GetObjectTemplate(), fixture.DataManager.StaticData.TribeRelations, TribeClass.PC_DARK) &&
				MathF.Pow(n.GetX() - at.X, 2) + MathF.Pow(n.GetY() - at.Y, 2) <= 900).ToArray();
			foreach (Npc npc in clear) fixture.World.Despawn(npc);
			setupClears += clear.Length; setupMoves++;
			Console.WriteLine($"HM-04 labelled setup teleport in native copy {instanceId}; clear {clear.Length} other aggressive neighbours, preserve all boss/pull participants; no HP/damage/quest changes.");
			session.Api.World.BeginWorldReload();
			await TeleportForSetupAsync(session, Server(), 300200000, at.X, at.Y, at.Z, token, instanceId);
			session.AcceptTeleportPosition();
			await session.SynchronizeAsync(token);
		}
		var runtime = new NaturalJourneyRuntime(root, "SIM-hm04", fixture.Seed, fixture.DataManager.StaticData,
			() => fixture.Clock.NowMillis, fixture.Epoch, Geometry, _ => Task.FromResult(false), policy.AssertClean,
			() => policy.SnapshotProblems(), trace, dashboard);
		var fights = new List<object>();
		int deaths = 0, retreats = 0, sequence = 0, pairPulls = 0;
		foreach (Npc boss in bosses)
		{
			foreach (Npc victim in new[] { boss }.Concat(packs[boss.GetNpcId()]))
			{
				if (victim.IsDead()) { fights.Add(new { npc = victim.GetNpcId(), obj = victim.GetObjectId(), collateral = true }); continue; }
				for (int attempt = 0; attempt < 4 && !victim.IsDead(); attempt++)
				{
					// Rest at the checked entry floor, then place only this disposable probe at the scripted fight.
					// The regular journey owns every heal, buff, attack, retreat and real bind revive thereafter.
					if (Server().GetWorldId() != 300200000)
					{
						int sickness = session.Api.World.VisibleEffects?.Where(e => e.SkillId == 8291).Select(e => e.RemainingMillis).DefaultIfEmpty(0).Max() ?? 0;
						if (sickness > 0) await session.AdvanceAsync(TimeSpan.FromMilliseconds(sickness + 100), token);
						await session.SynchronizeAsync(token);
						await travel.PillarAsync(upper: false);
						await travel.PortalAsync(730319, 300200000);
						Assert.Equal(instanceId, Server().GetInstanceId());
					}
					await SetupAtAsync(new(172,20,144.22548f,0));
					// A survivor of the last fight (the pair's partner, or this victim after a refused pull) walks home once the
					// probe is moved away, and Java refuses a pull on a returning npc. Whether the rest before this fight outlasted
					// that walk depended on the dice of the last one (D35's Fast run: a 13 s first fight and no rest). Wait it out.
					for (int wait = 0; wait < 60 && victim.GetAi().GetState() is not (Aion.GameServer.Ai.AIState.IDLE or Aion.GameServer.Ai.AIState.WALKING); wait++)
					{
						await session.AdvanceAsync(TimeSpan.FromSeconds(1), token);
						await session.SynchronizeAsync(token);
					}
					session.BeginStep($"normal-{++sequence}", $"actual-HP-{victim.GetNpcId()}-and-shipped-neighbours");
					long started = fixture.Clock.NowMillis;
					NaturalCombatDiagnosticResult result = await new NaturalIshalgenJourney(session, runtime, new()).RunObservedCombatAsync(async _ =>
					{
						BotPosition target = At(victim);
						bool pair = victim == boss && boss.GetNpcId() == 216897 && attempt == 0;
						Npc? partner = pair ? packs[boss.GetNpcId()].First() : null;
						BotPosition centre = partner == null ? target : new((target.X + partner.GetX()) / 2,
							(target.Y + partner.GetY()) / 2, (target.Z + partner.GetZ()) / 2, 0);
						BotPosition stand = Geometry().GroundAround(300200000, centre, pair ? [0f,2f,3f,5f] : [7f,6f,8f])
							.First(p => Geometry().HasLineOfSight(300200000, p with { Z = p.Z + 1.6f }, target with { Z = target.Z + 1 }));
						await SetupAtAsync(stand);
						Assert.True(session.Api.World.Objects.ContainsKey(victim.GetObjectId()));
						if (partner != null)
						{
							// Pull two actual shipped enemies by ordinary learned spell packets, not aggro/HP writes.
							foreach (Npc tagged in new[] { partner, victim })
								await NaturalAirCombat.ShootDownAsync(session, tagged.GetObjectId(), 0,
									(origin, skill, level, aim) => runtime.CreateSpellCast(session.Api.World, origin, skill, level, aim), token, maximumCasts: 1);
							// The diagnostic's private cooldown ledger starts after this callback. Finish the observed
							// lower-rank Smite cooldown before handing its shared group to the regular combat policy.
							ushort tagSkill = new ushort[] { 4015,4014,4013,4012 }.First(id => session.Api.World.Skills.ContainsKey(id));
							await session.AdvanceAsync(session.Api.Timing.TimeUntilCast(tagSkill) + TimeSpan.FromMilliseconds(1), token);
							pairPulls++;
						}
						return victim.GetObjectId();
					}, token);
					deaths += result.Deaths; retreats += result.Retreats;
					fights.Add(new { npc = victim.GetNpcId(), obj = victim.GetObjectId(), attempt, maxHp = victim.GetLifeStats().GetMaxHp(), result, started });
					Console.WriteLine($"HM-04 normal {victim.GetNpcId()} HP {victim.GetLifeStats().GetMaxHp()}: {JsonSerializer.Serialize(result)}.");
					await session.SynchronizeAsync(token);
				}
				Assert.True(victim.IsDead(), $"Normal combat did not yet prove {victim.GetNpcId()}; retained losses are outcomes, not completions.");
			}
		}
		await NaturalMovieGate.FinishAsync(session, token);
		await session.SynchronizeAsync(token);
		Assert.Contains(session.PacketHistory, p => p.PacketType == typeof(SM_PLAY_MOVIE) && p.Get<int>("cutsceneId") == 457);
		var summons = session.PacketHistory.Where(p => p.PacketType == typeof(SM_NPC_INFO) && p.Get<int>("npcId") is 282041 or 282042)
			.Select(p => p.Get<int>("npcId")).Distinct().Order().ToArray();
		// Java SummonerAI schedules the helpers, then refuses them if the boss died
		// before that task ran. A fast final spell can legitimately skip the phase.
		// HamerunSummonTests proves the nonlethal trigger and queued-death boundary.
		Assert.True(summons.Length == 0 || summons.SequenceEqual(new[] { 282041,282042 }),
			"An observed helper phase must contain both shipped add types.");
		string summonOutcome = summons.Length == 0 ? "no-helpers-observed-before-death" : "both-helpers-observed";
		Console.WriteLine($"HM-04 helper outcome: {summonOutcome}; retained as combat evidence.");
		Assert.DoesNotContain(Server().GetWorldMapInstance().GetNpcs(), n => n.GetNpcId() is 282041 or 282042);
		Assert.Single(Server().GetWorldMapInstance().GetNpcs(700832));
		Assert.Single(Server().GetWorldMapInstance().GetNpcs(700852));
		Assert.DoesNotContain(Server().GetWorldMapInstance().GetNpcs(), n => n.GetNpcId() is 700829 or 700830 or 700831);
		int chest = await travel.WalkNpcAsync(700832);
		int chestStart = session.PacketHistory.Count;
		Assert.True(await NaturalAltgardQuestSteps.UseObjectAsync(session, chest, null, token));
		await session.SynchronizeAsync(token);
		Assert.Contains(session.PacketHistory.Skip(chestStart), p => p.PacketType == typeof(SM_LOOT_ITEMLIST) && p.Get<int>("targetObjectId") == chest);
		BotLootItem[] offered = session.Api.World.Loot!.Items.ToArray();
		var loot = new List<object>();
		foreach (BotLootItem item in offered)
		{
			long before = session.Api.World.Inventory.Values.Where(i => i.ItemId == item.ItemId).Sum(i => i.Count);
			await session.AdvanceAsync(TimeSpan.FromMilliseconds(450), token);
			await session.SendPacketAsync(session.Api.Loot(chest, item.Index), token);
			await session.SynchronizeAsync(token);
			long after = session.Api.World.Inventory.Values.Where(i => i.ItemId == item.ItemId).Sum(i => i.Count);
			loot.Add(new { item, before, after, received = after - before });
		}
		await session.SendPacketAsync(session.Api.Loot(chest, close: true), token);
		await travel.PortalAsync(700852, 220030000);
		Assert.False(Server().IsDead());
		Assert.Equal((101501357,3L), (session.Api.World.Inventory[staff].ItemId, session.Api.World.Inventory[staff].Details.EquippedSlot));
		Assert.DoesNotContain(11504, session.Api.World.Skills.Keys);
		Assert.Single(session.Api.World.Inventory.Values, i => i.ItemId == 188053787 && i.Count == 1);
		int maxObservedAttackers = 0;
		using (var traceReader = new StreamReader(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
		{
			while (await traceReader.ReadLineAsync(token) is string line)
			{
				using JsonDocument entry = JsonDocument.Parse(line);
				if (entry.RootElement.GetProperty("packet").GetString() == "combat-decision")
					maxObservedAttackers = Math.Max(maxObservedAttackers, entry.RootElement.GetProperty("fields").GetProperty("observedAttackers").GetInt32());
			}
		}
		Assert.True(maxObservedAttackers >= 2, "The native pair pull must produce two actually observed attackers.");
		await session.QuitAsync(token);
		await session.WaitForReentryAsync(token);
		await session.ReloginAndVerifyPersistenceAsync(token);
		await session.EnterWorldAsync(token);
		await session.SynchronizeAsync(token);
		Assert.Equal((101501357,3L), (session.Api.World.Inventory[staff].ItemId, session.Api.World.Inventory[staff].Details.EquippedSlot));
		await File.WriteAllTextAsync(Path.ChangeExtension(path, ".summary.json"), JsonSerializer.Serialize(new
		{
			account = 225, level = 24, instanceId, entries = travel.EntriesUsed, original, fights, deaths, retreats,
			setupClears, setupMoves, pairPulls, maxObservedAttackers, packs = packs.ToDictionary(p => p.Key, p => p.Value.Select(n => n.GetNpcId()).ToArray()),
			summons, summonOutcome, movie = 457, chest = 700832, offered, loot, exit = 700852, staff, endpoint = session.CurrentPosition,
			naturalCharacterChanged = false, hpOrDamageEdited = false,
		}), token);
		Console.WriteLine($"HM-04 PASS: all four actual-HP bosses plus shipped neighbouring pulls, {deaths} deaths/{retreats} retreats, {summonOutcome}/movie/class loot/exit/relog; staff retained.");
		policy.AssertClean();
	}
}
