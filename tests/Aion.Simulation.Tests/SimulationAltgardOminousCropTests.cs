using Aion.Bots.Dashboard;
using Aion.Bots.Movement;
using Aion.Bots.Navigation;
using Aion.Bots.Navigation.NavMesh;
using Aion.Bots.Protocol;
using Aion.Bots.Scenarios;
using Aion.Bots.Tracing;
using Aion.Bots.World;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;

namespace Aion.Simulation.Tests;

public sealed partial class SimulationFastScenarioTests
{
	/// <summary>
	/// AM-05 (docs/natural-altgard-leveling.md): the Q24012 campaign "An Ominous Crop". A level 13 Cleric (GM setup, with
	/// Q24012 at START var 0 as in the <c>altgard-l12</c> snapshot) talks to Loriniah (movie 61), walks into the MuMu Farmland
	/// (var 2), uses three MuMu Carts (var 5, each cart gone once used), then collects 5 Waist Bands from MuMu farmers and
	/// gatherers and 3 Hairpins from MuMu patrols (drops only from var 5), and hands them in to Loriniah for the hauberk.
	/// Each target is set to 1 HP and killed with one Smite, and the farmland monsters around it are despawned (GM setup on
	/// the monsters, never on the bot: this probe is about the quest mechanics and the var-5 drops; the fights are AM-06's).
	/// </summary>
	[SkippableFact]
	public async Task AltgardOminousCropUsesTheCartsCollectsAndIsRewarded()
	{
		Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
		const int altgard = 220030000, quest = 24012;
		using var policy = NewPolicy("AM05", includeHistory: false);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(20));
		CancellationToken token = timeout.Token;
		string root = Aion.GameServer.TestKit.RealStaticData.RepoRoot();
		NaturalAltgardContract leg = NaturalAltgardContract.LoadLeg("l2");
		NaturalAltgardObjectUse carts = leg.ObjectUseList.Single(use => use.QuestId == quest);
		NaturalAltgardCollection collection = leg.CollectionList.Single(entry => entry.QuestId == quest);
		string directory = Path.Combine(root, "run", "am05");
		Directory.CreateDirectory(directory);
		string tracePath = Path.Combine(directory, $"am05-s{fixture.Seed}-{DateTime.UtcNow:yyyyMMddHHmmss}.trace.jsonl");
		using var trace = BotActionTraceWriter.Open(tracePath, "am05", "b01", "sim-player-142",
			virtualTime: () => TimeSpan.FromMilliseconds(fixture.Clock.NowMillis));
		await using var session = new SimulationL0Session(fixture, policy, "b01", 142, "Asimcrop", Race.ASMODIANS, trace, tracePath);
		session.BeginStep("s00", "login-create-enter-and-setup");
		await session.LoginAndAuthenticateAsync(token);
		await session.CreateCharacterAsync(token, PlayerClass.PRIEST);
		await session.EnterWorldAsync(token);
		await session.SynchronizeAsync(token);
		Player player = fixture.World.GetPlayer(session.CharacterId);
		Assert.True(ClassChangeService.SetClass(player, PlayerClass.CLERIC, validate: false, updateDaevaStatus: true));
		player.GetCommonData().SetLevel(13);
		SkillLearnService.LearnNewSkills(player, 1, 13);
		// The bridge's Karmic Staff, as NA-23 gave its Cleric (a fresh character wears nothing).
		Assert.Equal(0, Aion.GameServer.Services.Items.ItemService.AddItem(player, 101500498, 1));
		Assert.NotNull(player.GetEquipment().EquipItem(player.GetInventory().GetItems().Last(owned => owned.GetItemId() == 101500498).GetObjectId(), 1));
		var crop = new QuestState(quest, QuestStatus.START, 0, 0, 0, null, null, null);
		Assert.True(player.GetQuestStateList().AddQuest(quest, crop));
		Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, new SM_QUEST_ACTION(SM_QUEST_ACTION.ActionType.ADD, crop));
		var instance = fixture.World.GetWorldMap(altgard).GetMainWorldMapInstance();
		BotNavigationGeometry geometry = BotNavigationGeometry.ForServerWorld(instance.GetInstanceId(), Race.ASMODIANS);
		BotTravelPlanner planner = BotTravelPlanner.For(altgard, geometry, fixture.DataManager.StaticData)
			?? throw new InvalidDataException("Altgard has no travel planner.");
		var runtime = new NaturalJourneyRuntime(root, "SIM-am05", fixture.Seed, fixture.DataManager.StaticData,
			() => fixture.Clock.NowMillis, fixture.Epoch, () => geometry, _ => Task.FromResult(false), policy.AssertClean,
			() => policy.SnapshotProblems(), trace, new LiveBotDashboardState());
		NaturalAltgardStep start = leg.Steps.Single(step => step.Key == "q24012-v0-loriniah");
		NaturalAltgardStep handIn = leg.Steps.Single(step => step.Key == "q24012-v5-loriniah");
		BotPosition loriniah = geometry.GroundAround(altgard, new BotPosition(start.Position[0], start.Position[1], start.Position[2], 0), [2f, 3f]).First();
		await TeleportForSetupAsync(session, player, altgard, loriniah.X, loriniah.Y, loriniah.Z, token);
		await session.SynchronizeAsync(token);

		session.BeginStep("s01", start.Key);
		Console.WriteLine($"AM-05 {await NaturalAltgardQuestSteps.TalkAsync(session, start, await session.WaitForNpcAsync(start.NpcId, token), token)}");

		// The carts: walk to the nearest one still standing; entering the farmland on the way is the zone step.
		int step = 1;
		var usedCarts = new HashSet<int>();
		while (NaturalAltgardQuestSteps.State(session.Api.World, quest) is (3, int var) && var < carts.ToVar)
		{
			session.BeginStep($"s{++step:00}", $"cart-at-var-{var}");
			Assert.True(usedCarts.Count < carts.Uses + 2, "too many cart attempts");
			// A used cart dies and leaves the server's list, so the nearest living cart is a fresh one.
			BotPosition cartSpawn = CartSpawns().OrderBy(at => NaturalGuardedTalkPolicy.Distance(at, session.CurrentPosition)).First();
			await WalkToAsync(cartSpawn, 2f);
			if (var == carts.FromVar - 1)
			{
				Assert.Equal(((byte)3, carts.FromVar), NaturalAltgardQuestSteps.State(session.Api.World, quest));
				Console.WriteLine($"AM-05 entered the farmland: var {carts.FromVar - 1} -> {carts.FromVar}");
				continue;
			}
			int cart = session.Api.World.Objects.Values.Where(known => known.TemplateId == carts.NpcId && !usedCarts.Contains(known.ObjectId))
				.OrderBy(known => NaturalGuardedTalkPolicy.Distance(known.Position, session.CurrentPosition)).Select(known => known.ObjectId).First();
			Assert.True(await NaturalAltgardQuestSteps.UseObjectAsync(session, cart, null, token), $"cart {cart} use interrupted");
			usedCarts.Add(cart);
			await session.AdvanceAsync(TimeSpan.FromSeconds(1), token);
			await session.SynchronizeAsync(token);
			Console.WriteLine($"AM-05 cart {cart}: var {var} -> {NaturalAltgardQuestSteps.State(session.Api.World, quest)?.Var}; still visible: {session.Api.World.Objects.ContainsKey(cart)}");
			Assert.Equal(var + 1, NaturalAltgardQuestSteps.State(session.Api.World, quest)?.Var);
		}
		Assert.Equal(((byte)3, carts.ToVar), NaturalAltgardQuestSteps.State(session.Api.World, quest));

		// The collections, from var 5.
		var unseen = new HashSet<int>();
		foreach (NaturalAltgardCollectedItem item in collection.Items)
		{
			int kills = 0;
			while (ItemCount(item.ItemId) < item.Count)
			{
				session.BeginStep($"s{++step:00}", $"collect-{item.ItemId}");
				Assert.True(++kills <= item.Count * 4, $"item {item.ItemId}: {ItemCount(item.ItemId)} of {item.Count} after {kills - 1} kills");
				await HealIfLowAsync();
				BotKnownObject? target = Nearest(item.SourceNpcIds);
				if (target == null)
				{
					BotPosition spawn = SpawnsOf(item.SourceNpcIds).OrderBy(at => NaturalGuardedTalkPolicy.Distance(at, session.CurrentPosition)).First();
					await WalkToAsync(spawn, 15f);
					target = Nearest(item.SourceNpcIds) ?? throw new InvalidDataException($"No source of {item.ItemId} in view near {spawn}.");
				}
				ClearAggressiveAround(target.Position, target.ObjectId);
				// GM setup on the monster, never on the bot: one Smite kills, so the probe tests the var-5 drops and
				// the loot, not the fight (AM-06's).
				var victim = instance.GetNpcs().Single(npc => npc.GetObjectId() == target.ObjectId);
				victim.GetLifeStats().SetCurrentHp(1);
				// The hairpin sources are walking patrols: in a long shared SIM world one may stand out of sight
				// (STR_SKILL_OBSTACLE) or walk off (AC-08), so follow it, closer each time, and shoot again, as a player does.
				bool killed = false;
				float[] reach = [18f, 8f, 3f];
				for (int chase = 0; chase < reach.Length && !killed; chase++)
				{
					BotPosition now = session.Api.World.Objects.TryGetValue(target.ObjectId, out BotKnownObject? seen) ? seen.Position : target.Position;
					await WalkToAsync(now, reach[chase], target.ObjectId);
					killed = await NaturalAirCombat.ShootDownAsync(session, target.ObjectId, quest,
						(origin, skill, level, victim) => runtime.CreateSpellCast(session.Api.World, origin, skill, level, victim), token, maximumCasts: 20);
				}
				if (!killed)
				{
					// Out of sight from every ground spot below it (a MuMu highsitter on its platform): a player picks
					// another source, and so does the probe.
					Assert.True(unseen.Add(target.ObjectId) && unseen.Count <= 3, $"{target.TemplateId} {target.ObjectId} was not killed");
					Console.WriteLine($"AM-05 {target.TemplateId} {target.ObjectId}: out of sight from the ground, another source instead");
					kills--;
					continue;
				}
				await WalkToAsync(session.Api.World.Objects.TryGetValue(target.ObjectId, out BotKnownObject? corpse) ? corpse.Position : target.Position, 2f, target.ObjectId);
				bool looted = await NaturalAltgardQuestSteps.LootItemAsync(session, target.ObjectId, item.ItemId, token);
				Console.WriteLine($"AM-05 {target.TemplateId}: {(looted ? "dropped" : "no drop")}, {ItemCount(item.ItemId)} of {item.Count}; HP {session.Api.World.CurrentHp}/{session.Api.World.MaxHp}");
				Assert.False(player.IsDead(), "died in the farmland");
			}
		}

		session.BeginStep($"s{++step:00}", handIn.Key);
		await HealIfLowAsync();
		BotPosition back = geometry.GroundAround(altgard, new BotPosition(handIn.Position[0], handIn.Position[1], handIn.Position[2], 0), [2f, 3f]).First();
		await WalkToAsync(back, 1f);
		Console.WriteLine($"AM-05 {await NaturalAltgardQuestSteps.TalkAsync(session, handIn, await session.WaitForNpcAsync(handIn.NpcId, token), token)}");
		await session.SynchronizeAsync(token);
		Assert.Contains(quest, session.Api.World.CompletedQuestIds);
		Assert.Contains(session.Api.World.Inventory.Values, owned => owned.ItemId == leg.RequiredRewardChoice.ItemId);
		Assert.All(collection.Items, item => Assert.Equal(0, ItemCount(item.ItemId)));
		Assert.False(player.IsDead());
		policy.AssertClean();

		long ItemCount(int itemId) => session.Api.World.Inventory.Values.Where(owned => owned.ItemId == itemId).Sum(owned => owned.Count);

		// Living ones only: the client keeps a corpse in view until it decays.
		BotKnownObject? Nearest(int[] templates) => session.Api.World.Objects.Values
			.Where(known => known.Kind == BotKnownObjectKind.Npc && known.TemplateId is int id && templates.Contains(id) && !known.IsCorpse &&
				!unseen.Contains(known.ObjectId) &&
				instance.GetNpcs().Any(npc => npc.GetObjectId() == known.ObjectId && !npc.IsDead()))
			.OrderBy(known => NaturalGuardedTalkPolicy.Distance(known.Position, session.CurrentPosition)).FirstOrDefault();

		IEnumerable<BotPosition> CartSpawns() => SpawnsOf([carts.NpcId]);

		IEnumerable<BotPosition> SpawnsOf(int[] templates) => instance.GetNpcs().Where(npc => templates.Contains(npc.GetNpcId()) && !npc.IsDead())
			.Select(npc => new BotPosition(npc.GetX(), npc.GetY(), npc.GetZ(), 0));

		// Every other monster that could join: aggressive ones, and the farmland tribes that support each other
		// (MuMu farmers help patrols, black claws help farmers; AM-03's findings).
		void ClearAggressiveAround(BotPosition at, int keep)
		{
			TribeClass[] farmland = [TribeClass.RATMAN, TribeClass.RATMANWORKER, TribeClass.LYCAN, TribeClass.TOWERMAN];
			foreach (var npc in instance.GetNpcs().Where(npc => npc.GetObjectId() != keep && !npc.IsDead() &&
				(NaturalHostility.IsAggressive(npc.GetObjectTemplate(), fixture.DataManager.StaticData.TribeRelations, TribeClass.PC_DARK) &&
					MathF.Pow(npc.GetX() - at.X, 2) + MathF.Pow(npc.GetY() - at.Y, 2) <= 40 * 40 ||
				 farmland.Contains(npc.GetObjectTemplate().GetTribe()) &&
					MathF.Pow(npc.GetX() - at.X, 2) + MathF.Pow(npc.GetY() - at.Y, 2) <= 25 * 25)).ToArray())
				fixture.World.Despawn(npc);
		}

		async Task WalkToAsync(BotPosition target, float within, int keep = 0)
		{
			if (NaturalGuardedTalkPolicy.Distance(session.CurrentPosition, target) <= within) return;
			BotPosition goal = geometry.GroundAround(altgard, target, [within * 0.6f, within * 0.8f, 2f, 3f])
				.OrderBy(point => NaturalGuardedTalkPolicy.Distance(point, session.CurrentPosition)).FirstOrDefault();
			if (goal == default) goal = target;
			IReadOnlyList<BotPosition> route = (NaturalGuardedTalkPolicy.Distance(session.CurrentPosition, goal) >= BotTravelPlanner.MinimumJourneyDistance
				? planner.PlanJourney(altgard, session.CurrentPosition, goal, 13, [])?.Route : null)
				?? geometry.FindJourneyPath(altgard, session.CurrentPosition, goal);
			if (route.Count == 0) route = geometry.FindInteractionPath(altgard, session.CurrentPosition, target);
			Assert.True(route.Count > 0, $"no route to {target}");
			// Never a corpse: despawning one would take its loot with it.
			foreach (var npc in instance.GetNpcs().Where(npc => npc.GetObjectId() != keep && !npc.IsDead() &&
				NaturalHostility.IsAggressive(npc.GetObjectTemplate(), fixture.DataManager.StaticData.TribeRelations, TribeClass.PC_DARK) &&
				route.Any(point => MathF.Pow(npc.GetX() - point.X, 2) + MathF.Pow(npc.GetY() - point.Y, 2) <= 25 * 25)).ToArray())
				fixture.World.Despawn(npc);
			float speed = session.Api.World.MovementSpeed ?? throw new InvalidOperationException("No movement speed.");
			await session.ExecuteMovementAsync(new BotMover(session.Api.World, session.Api.Timing).CreateGroundPlan(route, session.CurrentPosition, speed), token);
			await session.SynchronizeAsync(token);
		}

		async Task HealIfLowAsync()
		{
			for (int cast = 0; cast < 6 && session.Api.World.CurrentHp * 100 < session.Api.World.MaxHp * 70; cast++)
			{
				ushort heal = new ushort[] { 1839, 1838 }.First(id => session.Api.World.Skills.ContainsKey(id));
				TimeSpan gate = session.Api.Timing.TimeUntilCast(heal);
				if (gate > TimeSpan.Zero) await session.AdvanceAsync(gate + TimeSpan.FromMilliseconds(1), token);
				await session.SendPacketAsync(session.Api.Target(session.CharacterId), token);
				await session.SendPacketAsync(session.Api.Cast(runtime.CreateSpellCast(session.Api.World, session.CurrentPosition, heal,
					checked((byte)session.Api.World.Skills[heal].Level), session.CharacterId)), token);
				DecodedBotServerPacket started = await BotCastProtocol.WaitForStartAsync(
					(predicate, waitToken) => session.WaitForPacketAsync(packet => predicate(packet) || BotCastProtocol.IsStartRejection(packet), waitToken),
					session.CharacterId, heal, token);
				if (started.PacketType == typeof(SM_CASTSPELL))
				{
					await session.AdvanceAsync(TimeSpan.FromMilliseconds(started.Get<ushort>("castDuration") + 1), token);
					DecodedBotServerPacket result = await BotCastProtocol.WaitForCompletionAsync(session.WaitForPacketAsync, session.CharacterId, heal, token);
					await session.AdvanceAsync(BotCastProtocol.RecoveryDelay(result), token);
				}
				await session.SynchronizeAsync(token);
			}
		}
	}
}
