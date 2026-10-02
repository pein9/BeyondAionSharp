using Aion.Bots.Movement;
using Aion.Bots.Navigation;
using Aion.Bots.Scenarios;
using Aion.Bots.World;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;

namespace Aion.Simulation.Tests;

public sealed partial class SimulationFastScenarioTests
{
	/// <summary>
	/// AB-06 (docs/natural-altgard-leveling.md): Leg 4's other scripted quests on the live SIM server, with a level 16 probe
	/// Cleric, each through the contract's steps:
	/// <list type="bullet">
	/// <item>Q2231: Lamir, Karl, Gunmarson and Kaibech;</item>
	/// <item>Q2232: Tatural, then nine beehives (100% from var 1), then Gilungk;</item>
	/// <item>Q2239: Vovetirn's SETPRO1 (page 10, not a close), three Ampha Membranes, the check (var 1 -> 3 and the antidote),
	/// Gilungk;</item>
	/// <item>Q2289: five starved or fierce mosbears, Gefion's movie 62, Skanin's remedy, Komu's Horn at var 7, Gefion;</item>
	/// <item>Q24013 (the campaign, started): Nokir, Shania's Hunter's Poison, and the poison used inside its zone (var 2 -> 3,
	/// two Feral Black Claw Sharpeyes spawn).</item>
	/// </list>
	/// GM setup on the probe only: its level, Q2288 set COMPLETE (Q2289's prerequisite), targets set to 1 HP with their
	/// aggressive neighbours despawned (the fights are AB-07's), and setup teleports between the places (AB-02 walked them).
	/// </summary>
	[SkippableFact]
	public async Task AltgardBasfeltScriptedQuestsPlayThroughTheContractSteps()
	{
		Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
		const int altgard = 220030000, level = 16;
		using var policy = NewPolicy("AB06", includeHistory: false);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(20));
		CancellationToken token = timeout.Token;
		NaturalAltgardContract leg = NaturalAltgardContract.LoadLeg("l4");
		NaturalAltgardStep Step(string key) => leg.Steps.Single(step => step.Key == key);
		await using var session = new SimulationL0Session(fixture, policy, "b01", 148, "Asimbasscript", Race.ASMODIANS);
		session.BeginStep("s00", "login-create-enter-and-setup");
		await session.LoginAndAuthenticateAsync(token);
		await session.CreateCharacterAsync(token, PlayerClass.PRIEST);
		await session.EnterWorldAsync(token);
		await session.SynchronizeAsync(token);
		Player Server() => fixture.World.GetPlayer(session.CharacterId);
		Assert.True(ClassChangeService.SetClass(Server(), PlayerClass.CLERIC, validate: false, updateDaevaStatus: true));
		Server().GetCommonData().SetLevel(level);
		SkillLearnService.LearnNewSkills(Server(), 1, level);
		Assert.True(Server().GetQuestStateList().AddQuest(2288, new QuestState(2288, QuestStatus.COMPLETE)));
		foreach (int id in leg.Start.CompletedQuestIds.Where(id => Server().GetQuestStateList().GetQuestState(id) == null))
			Assert.True(Server().GetQuestStateList().AddQuest(id, new QuestState(id, QuestStatus.COMPLETE)));
		// Q24013 is started in the snapshot (AB-01): start it here as the campaign's own event does.
		var campaign = new QuestState(24013, QuestStatus.START);
		Assert.True(Server().GetQuestStateList().AddQuest(24013, campaign));
		// The client learns of a server-side quest only through SM_QUEST_ACTION (the AM-05 lesson).
		Aion.GameServer.Utils.PacketSendUtility.SendPacket(Server(),
			new Aion.GameServer.Network.Aion.ServerPackets.SM_QUEST_ACTION(Aion.GameServer.Network.Aion.ServerPackets.SM_QUEST_ACTION.ActionType.ADD, campaign));
		var instance = fixture.World.GetWorldMap(altgard).GetMainWorldMapInstance();
		BotNavigationGeometry geometry = BotNavigationGeometry.ForServerWorld(instance.GetInstanceId(), Race.ASMODIANS);
		var runtime = new NaturalJourneyRuntime(Aion.GameServer.TestKit.RealStaticData.RepoRoot(), "SIM-ab06", fixture.Seed, fixture.DataManager.StaticData,
			() => fixture.Clock.NowMillis, fixture.Epoch, () => geometry, _ => Task.FromResult(false), policy.AssertClean,
			() => policy.SnapshotProblems(), null!, new Aion.Bots.Dashboard.LiveBotDashboardState());
		(byte Status, int Var)? State(int id) => NaturalAltgardQuestSteps.State(session.Api.World, id);
		long ItemCount(int itemId) => session.Api.World.Inventory.Values.Where(owned => owned.ItemId == itemId).Sum(owned => owned.Count);
		var unusable = new HashSet<int>();

		async Task TeleportNearAsync(float x, float y, float z)
		{
			BotPosition ground = geometry.GroundAround(altgard, new BotPosition(x, y, z, 0), [2f, 3f, 5f, 8f]).First();
			await TeleportForSetupAsync(session, Server(), altgard, ground.X, ground.Y, ground.Z, token);
			session.AcceptTeleportPosition();
			await session.SynchronizeAsync(token);
		}
		async Task WalkToAsync(BotPosition target, float within)
		{
			if (NaturalGuardedTalkPolicy.Distance(session.CurrentPosition, target) <= within) return;
			BotPosition goal = geometry.GroundAround(altgard, target, [within * 0.6f, within * 0.8f, 2f, 3f])
				.OrderBy(point => NaturalGuardedTalkPolicy.Distance(point, session.CurrentPosition)).FirstOrDefault();
			if (goal == default) goal = target;
			IReadOnlyList<BotPosition> route = geometry.FindJourneyPath(altgard, session.CurrentPosition, goal);
			if (route.Count == 0) route = geometry.FindInteractionPath(altgard, session.CurrentPosition, target);
			if (route.Count == 0) return;
			float speed = session.Api.World.MovementSpeed ?? throw new InvalidOperationException("No movement speed.");
			await session.ExecuteMovementAsync(new BotMover(session.Api.World, session.Api.Timing).CreateGroundPlan(route, session.CurrentPosition, speed), token);
			await session.SynchronizeAsync(token);
		}
		async Task TalkAsync(string key)
		{
			NaturalAltgardStep step = Step(key);
			var npc = instance.GetNpcs().First(candidate => candidate.GetNpcId() == step.NpcId && !candidate.IsDead());
			await TeleportNearAsync(npc.GetX(), npc.GetY(), npc.GetZ());
			session.BeginStep($"s-{key}", key);
			int seen = await session.WaitForNpcAsync(step.NpcId, token);
			for (int attempt = 1; ; attempt++)
			{
				try { Console.WriteLine($"AB-06 {await NaturalAltgardQuestSteps.TalkAsync(session, step, seen, token)}"); return; }
				catch (NaturalDialogTooFarException) when (attempt < 4)
				{
					// Lamir and Shania walk their routes: follow to where the client sees them now.
					await session.SynchronizeAsync(token);
					await WalkToAsync(session.Api.World.Objects[seen].Position, 2f);
				}
			}
		}
		// One monster of the given kinds, set to 1 HP with its aggressive neighbours cleared, shot down with Smite. Returns its object.
		async Task<int> KillOneAsync(int[] kinds)
		{
			for (int tries = 0; tries < 16; tries++)
			{
				var next = instance.GetNpcs().Where(npc => kinds.Contains(npc.GetNpcId()) && !npc.IsDead() && !unusable.Contains(npc.GetObjectId()))
					.OrderBy(npc => MathF.Pow(npc.GetX() - session.CurrentPosition.X, 2) + MathF.Pow(npc.GetY() - session.CurrentPosition.Y, 2)).FirstOrDefault();
				// The shared Fast world keeps earlier probes' kills and despawns. Replenish only a shipped kind at a shipped
				// spot on this controlled probe, as AG-05 does, rather than requiring a particular test order.
				if (next == null)
				{
					var group = fixture.DataManager.StaticData.SpawnsDh.GetSpawnsByWorldId(altgard).First(spawn => kinds.Contains(spawn.GetNpcId()));
					var spot = group.GetSpawnTemplates().OrderBy(spawn => MathF.Pow(spawn.GetX() - session.CurrentPosition.X, 2) +
						MathF.Pow(spawn.GetY() - session.CurrentPosition.Y, 2)).First();
					next = Assert.IsType<Aion.GameServer.Model.GameObjects.Npc>(Aion.GameServer.SpawnEngine.SpawnEngine.SpawnObject(
						new Aion.GameServer.Model.Templates.Spawns.SpawnTemplate(new Aion.GameServer.Model.Templates.Spawns.SpawnGroup(
							altgard, group.GetNpcId(), 0, null), spot.GetX(), spot.GetY(), spot.GetZ(), spot.GetHeading(), 0, null, 0),
						instance.GetInstanceId()), exactMatch: false);
					Console.WriteLine($"AB-06 replenished {group.GetNpcId()} at its shipped spot for the shared probe world");
				}
				BotPosition near = geometry.GroundAround(altgard, new BotPosition(next.GetX(), next.GetY(), next.GetZ(), 0), [10f, 14f, 6f])
					.FirstOrDefault(point => geometry.HasLineOfSight(altgard, point, new BotPosition(next.GetX(), next.GetY(), next.GetZ() + 1, 0)));
				if (near == default) { unusable.Add(next.GetObjectId()); continue; }
				foreach (var npc in instance.GetNpcs().Where(npc => npc.GetObjectId() != next.GetObjectId() && !npc.IsDead() &&
					NaturalHostility.IsAggressive(npc.GetObjectTemplate(), fixture.DataManager.StaticData.TribeRelations, TribeClass.PC_DARK) &&
					MathF.Pow(npc.GetX() - next.GetX(), 2) + MathF.Pow(npc.GetY() - next.GetY(), 2) <= 25 * 25).ToArray())
					fixture.World.Despawn(npc);
				await TeleportNearAsync(near.X, near.Y, near.Z);
				next.GetLifeStats().SetCurrentHp(1);
				await session.SynchronizeAsync(token);
				if (!session.Api.World.Objects.ContainsKey(next.GetObjectId())) { unusable.Add(next.GetObjectId()); continue; }
				if (await NaturalAirCombat.ShootDownAsync(session, next.GetObjectId(), 0,
					(origin, skill, skillLevel, aim) => runtime.CreateSpellCast(session.Api.World, origin, skill, skillLevel, aim), token, maximumCasts: 10))
				{
					// The shoot-down can report a kill the server never made (run-fast: Komu stayed alive at full health,
					// so there was no horn to loot). A kill counts only when the server has the monster dead; else try it again.
					await session.SynchronizeAsync(token);
					if (next.IsDead()) return next.GetObjectId();
					continue;
				}
				unusable.Add(next.GetObjectId());
			}
			throw new InvalidDataException($"No {string.Join("/", kinds)} could be shot down.");
		}
		async Task LootAsync(int corpse, int item)
		{
			await WalkToAsync(session.Api.World.Objects.TryGetValue(corpse, out BotKnownObject? seen) ? seen.Position : session.CurrentPosition, 2f);
			await NaturalAltgardQuestSteps.LootItemAsync(session, corpse, item, token);
		}

		// Q2231 Sibling Rivalry: Lamir, Karl, Gunmarson, Kaibech.
		foreach (string key in new[] { "q2231-offer-lamir", "q2231-v0-karl", "q2231-v1-gunmarson", "q2231-v2-kaibech" })
			await TalkAsync(key);
		Assert.Contains(2231, session.Api.World.CompletedQuestIds);

		// Q2232 The Broken Honey Jar: Tatural, nine beehives, Gilungk.
		await TalkAsync("q2232-offer-gilungk");
		await TalkAsync("q2232-v0-tatural");
		NaturalAltgardObjectUse hives = leg.ObjectUseList.Single(use => use.QuestId == 2232);
		while (ItemCount(hives.LootItemId!.Value) < hives.Uses)
		{
			var hive = instance.GetNpcs().Where(npc => npc.GetNpcId() == hives.NpcId && !npc.IsDead() && !unusable.Contains(npc.GetObjectId()))
				.OrderBy(npc => MathF.Pow(npc.GetX() - session.CurrentPosition.X, 2) + MathF.Pow(npc.GetY() - session.CurrentPosition.Y, 2)).First();
			await TeleportNearAsync(hive.GetX(), hive.GetY(), hive.GetZ());
			unusable.Add(hive.GetObjectId());
			foreach (var npc in instance.GetNpcs().Where(npc => !npc.IsDead() &&
				NaturalHostility.IsAggressive(npc.GetObjectTemplate(), fixture.DataManager.StaticData.TribeRelations, TribeClass.PC_DARK) &&
				MathF.Pow(npc.GetX() - hive.GetX(), 2) + MathF.Pow(npc.GetY() - hive.GetY(), 2) <= 25 * 25).ToArray())
				fixture.World.Despawn(npc);
			if (!session.Api.World.Objects.ContainsKey(hive.GetObjectId())) continue;
			bool looted = await NaturalAltgardQuestSteps.UseContractObjectAsync(session, hives, hive.GetObjectId(), token);
			Console.WriteLine($"AB-06 beehive {hive.GetObjectId()}: looted {looted}, {ItemCount(hives.LootItemId.Value)} of {hives.Uses}");
		}
		await TalkAsync("q2232-v1-gilungk");
		Assert.Contains(2232, session.Api.World.CompletedQuestIds);

		// Q2239 Malodor Antidote: Vovetirn (page 10), three membranes, the check (var 1 -> 3), Gilungk.
		await TalkAsync("q2239-offer-gilungk");
		await TalkAsync("q2239-v0-vovetirn");
		NaturalAltgardCollectedItem membranes = leg.CollectionList.Single(collection => collection.QuestId == 2239).Items.Single();
		int kills = 0;
		while (ItemCount(membranes.ItemId) < membranes.Count)
		{
			Assert.True(++kills <= 12, $"{ItemCount(membranes.ItemId)} membranes after {kills - 1} kills");
			await LootAsync(await KillOneAsync([210482]), membranes.ItemId);
		}
		Console.WriteLine($"AB-06 Q2239: {membranes.Count} membranes from {kills} kills");
		await TalkAsync("q2239-v1-vovetirn");
		Assert.Equal(((byte)3, 3), State(2239));
		await TalkAsync("q2239-v3-gilungk");
		Assert.Contains(2239, session.Api.World.CompletedQuestIds);

		// Q2289 Rampaging Mosbears: five kills, Gefion's movie, Skanin, Komu's Horn at var 7, Gefion.
		await TalkAsync("q2289-offer-gefion");
		NaturalAltgardHunt hunt = leg.HuntList.Single(entry => entry.QuestId == 2289);
		while (State(2289) is (3, int var) && var < hunt.ToVar)
		{
			await KillOneAsync(hunt.NpcIds);
			await session.SynchronizeAsync(token);
		}
		Assert.Equal(((byte)3, hunt.ToVar), State(2289));
		await TalkAsync("q2289-v5-gefion");
		await TalkAsync("q2289-v6-skanin");
		NaturalAltgardAvoid komu = leg.AvoidList.Single();
		Assert.Equal(((byte)3, komu.UntilVar), State(2289));
		NaturalAltgardCollectedItem horn = leg.CollectionList.Single(collection => collection.QuestId == 2289).Items.Single();
		int komuKilled = await KillOneAsync([komu.NpcId]);
		int lootStart = session.PacketHistory.Count;
		await LootAsync(komuKilled, horn.ItemId);
		string dropList = string.Join(",", session.PacketHistory.Skip(lootStart).Where(packet => packet.PacketType == typeof(Aion.GameServer.Network.Aion.ServerPackets.SM_LOOT_ITEMLIST))
			.SelectMany(packet => packet.Get<List<IReadOnlyDictionary<string, object?>>>("items")).Select(item => item["itemId"]));
		Assert.True(ItemCount(horn.ItemId) == 1, $"No Komu's Horn from Komu {komuKilled} (live Komus {instance.GetNpcs().Count(npc => npc.GetNpcId() == komu.NpcId)}, " +
			$"Q2289 {State(2289)}); drop list [{dropList}]; Komus in the world: " +
			string.Join("; ", instance.GetNpcs().Where(npc => npc.GetNpcId() == komu.NpcId).Select(npc => $"{npc.GetObjectId()} dead {npc.IsDead()} at {npc.GetX():F0},{npc.GetY():F0}")));
		await TalkAsync("q2289-v7-gefion");
		Assert.Contains(2289, session.Api.World.CompletedQuestIds);

		// Q24013: Nokir, Shania's poison, and the poison used inside its zone.
		await TalkAsync("q24013-v0-nokir");
		await TalkAsync("q24013-v1-shania");
		NaturalAltgardItemUse poison = leg.RequiredItemUse;
		Assert.Equal(1, ItemCount(poison.ItemId));
		await TeleportNearAsync(1675f, 234f, 290f);
		int SharpeyesNear() => instance.GetNpcs().Count(npc => npc.GetNpcId() == poison.SpawnsNpcId && !npc.IsDead() &&
			MathF.Pow(npc.GetX() - Server().GetX(), 2) + MathF.Pow(npc.GetY() - Server().GetY(), 2) <= 20 * 20);
		int before = SharpeyesNear();
		session.BeginStep("s-q24013-poison", "use-the-hunters-poison-in-its-zone");
		await NaturalAltgardQuestSteps.UseQuestItemAsync(session, poison, runtime.Data.ItemDataDh.GetItemTemplate(poison.ItemId), token);
		await session.AdvanceAsync(TimeSpan.FromSeconds(4), token);
		await session.SynchronizeAsync(token);
		int after = SharpeyesNear();
		Console.WriteLine($"AB-06 Q24013 poison: var {State(24013)?.Var}; Feral Sharpeyes within 20 m {before} -> {after}");
		Assert.Equal(((byte)3, poison.NextVar), State(24013));
		Assert.Equal(before + poison.SpawnCount, after);
		policy.AssertClean();
	}
}
