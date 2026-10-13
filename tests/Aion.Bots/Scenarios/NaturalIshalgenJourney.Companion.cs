using System.Text.Json;
using Aion.Bots.Navigation;
using Aion.Bots.Navigation.NavMesh;
using Aion.Bots.Scenarios.Classes;
using Aion.Bots.World;
using Aion.GameServer.Custom.Companion;
using Aion.GameServer.Model;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.Bots.Scenarios;

public sealed partial class NaturalIshalgenJourney
{
	/// <summary>How far the stand-in plays so far (docs/companion-bots.md, the C steps).</summary>
	private static readonly string[] CompanionCheckpoints = ["joined", "walk", "marks", "loot"];
	/// <summary>The two marks the stand-in sets; a companion takes the lowest mark first whatever its number.</summary>
	private const int CompanionFirstMark = 1, CompanionSecondMark = 2;

	/// <summary>
	/// Companion bots (docs/companion-bots.md). A companion is a character the server made for a player who asked for
	/// one: it is in that player's group from the moment it enters the world, and it leaves the game when it is no longer
	/// in that group. The stand-in is the test tool that plays the player's part in SIM; no bot leads in the product.
	/// </summary>
	private async Task RunCompanionAsync(CancellationToken token)
	{
		if (options.Companion == "stand-in")
		{
			await RunCompanionStandInAsync(token);
			return;
		}
		if (options.Companion != "companion") throw new InvalidDataException($"Unknown companion part '{options.Companion}'.");
		BotWorldModel world = session.Api.World;
		session.BeginStep("companion", "join-the-players-group");
		long entered = runtime.NowMillis;
		while (world.GroupId == null || world.GroupLeaderId == null)
		{
			if (runtime.NowMillis - entered > 30_000)
				throw new InvalidDataException("The companion entered the world and was put into no group.");
			await session.AdvanceAsync(TimeSpan.FromMilliseconds(250), token);
			await session.SynchronizeAsync(token);
		}
		int leader = world.GroupLeaderId.Value;
		if (leader == session.CharacterId) throw new InvalidDataException("The companion leads its group; the player always leads.");
		// A companion takes no loot and no quest: what it carries and what its journal holds as it joins is the measure.
		Dictionary<int, long> Carried() => world.Inventory.Values.GroupBy(item => item.ItemId).ToDictionary(group => group.Key, group => group.Sum(item => item.Count));
		HashSet<int> Journal() => [.. world.Quests.Keys, .. world.CompletedQuestIds];
		Dictionary<int, long> carriedAtStart = Carried();
		HashSet<int> journalAtStart = Journal();
		session.TraceDiagnostic("companion-joined", new Dictionary<string, object?>
		{
			["leader"] = leader, ["group"] = world.GroupId, ["members"] = world.GroupMembers.Keys.Order().ToArray(),
			["afterMillis"] = runtime.NowMillis - entered,
		});
		// Companion mode: it follows the leader it observes and plays its role. Nobody tells it anything, it takes no
		// quest and it opens no corpse. The Cleric heals, cleanses and blesses and never attacks.
		session.BeginStep("companion", "with-its-player");
		var geometry = runtime.CreateGeometry();
		NaturalJourneyNavigator Navigator() => new(session, BotNavigationGraphFactory.Build(runtime.Data, null, geometry), geometry, runtime, stopOnDeath: false);
		var navigator = Navigator();
		var combat = new NaturalJourneyCombat(session, navigator, runtime, geometry, stopOnDeath: false,
			conservativeRangedHold: false, NaturalMauPolicyParameters.Baseline, ClassLine) { ApproachMapId = world.MapId!.Value };
		var following = new NaturalGroupFollowDriver(session, runtime);
		using var healer = options.Role == "healer" ? new NaturalGroupHealDriver(session, runtime, combat.ClassProfile) : null;
		// The companion changed maps: checked movement and combat are bound to the map the client stands on now.
		void EnterMap()
		{
			geometry = runtime.CreateGeometry();
			navigator = Navigator();
			combat.EnterMap(navigator, geometry, world.MapId!.Value);
			following.Reset();
		}
		combat.AfterBindRevive = EnterMap;
		combat.AfterInstanceRevive = EnterMap;
		long scrollAskedMillis = long.MinValue / 2;
		// A monster this companion could not get at, and until when it leaves it alone.
		var leftAlone = new Dictionary<int, long>();
		// The monsters this client saw fall: its own kills, and every object whose health it saw reach nothing. A monster
		// that has just fallen is not a corpse to the client yet, and a mark stays on it.
		var fallen = new HashSet<int>();
		int fallenCursor = session.PacketHistory.Count;
		int fights = 0;
		bool Fightable(int id) => !fallen.Contains(id) && world.Objects.TryGetValue(id, out var seen) && seen.Kind == BotKnownObjectKind.Npc && !seen.IsCorpse &&
			leftAlone.GetValueOrDefault(id) <= runtime.NowMillis && NaturalServicePolicy.Distance(session.CurrentPosition, seen.Position) <= 40;
		// What a damage class attacks. The leader's marks come first, the lowest mark before the others. With no mark on
		// a living monster in view, it is what the leader is attacking; with neither, a monster that is attacking a
		// member of the group.
		(int Target, string Why)? FightTarget()
		{
			foreach ((int mark, int marked) in world.GroupBrands.OrderBy(entry => entry.Key))
				if (Fightable(marked)) return (marked, "mark-" + mark);
			int[] near = [.. world.Objects.Values.Where(known => Fightable(known.ObjectId)).Select(known => known.ObjectId)];
			if (near.Length == 0) return null;
			int[] members = [.. world.GroupMembers.Keys];
			DateTimeOffset since = session.Api.Timing.Now.AddSeconds(-10);
			int? defended = null;
			for (int index = session.PacketHistory.Count - 1, looked = 0; index >= 0 && looked < 400; index--, looked++)
			{
				var packet = session.PacketHistory[index];
				if (packet.ReceivedAt is { } at && at < since) break;
				foreach (int candidate in near)
					if (runtime.IncomingAttacker(packet, candidate) == leader) return (candidate, "leader");
				if (defended != null) continue;
				foreach (int member in members)
					if (runtime.IncomingAttacker(packet, member) is int attacker && Array.IndexOf(near, attacker) >= 0) { defended = attacker; break; }
			}
			return defended is int on ? (on, "defence") : null;
		}
		await combat.BuffOurselfAsync(NaturalHelpTrigger.PrePull, token);
		long turnAt = -1;
		int turnsAtOnce = 0;
		while (world.GroupId != null && world.GroupLeaderId == leader)
		{
			// Every turn of this loop takes game time. One that does not would hold the whole world still.
			if (runtime.NowMillis != turnAt) { turnAt = runtime.NowMillis; turnsAtOnce = 0; }
			else if (++turnsAtOnce > 200) throw new InvalidOperationException("The companion's loop turned 200 times without the clock moving.");
			await session.SynchronizeAsync(token);
			for (; fallenCursor < session.PacketHistory.Count; fallenCursor++)
				if (BotAttackStatus.CarriesHp(session.PacketHistory[fallenCursor]) && session.PacketHistory[fallenCursor].Get<byte>("hpOrMp") == 0)
					fallen.Add(session.PacketHistory[fallenCursor].Get<int>("objectId"));
			if (world.IsDead) { await combat.RestAsync(token); continue; }
			if (healer == null && FightTarget() is { } fight)
			{
				session.BeginStep("companion", "fight-" + fight.Why);
				session.TraceDiagnostic("companion-target", new Dictionary<string, object?>
				{
					["target"] = fight.Target, ["why"] = fight.Why, ["npc"] = world.Objects[fight.Target].TemplateId,
					["marks"] = world.GroupBrands.OrderBy(entry => entry.Key).Select(entry => $"{entry.Key}:{entry.Value}").ToArray(),
				});
				fights++;
				try
				{
					bool fell = await combat.TryKillAsync(fight.Target, token);
					if (fell) fallen.Add(fight.Target);
					else leftAlone[fight.Target] = runtime.NowMillis + 5_000;
					session.TraceDiagnostic("companion-fight-ended", new Dictionary<string, object?> { ["target"] = fight.Target, ["why"] = fight.Why, ["fell"] = fell });
				}
				catch (NaturalCombatApproachBlockedException blocked)
				{
					// Another member may have finished it on this one's way there; one it cannot get at is left for a while.
					leftAlone[fight.Target] = runtime.NowMillis + 20_000;
					session.TraceDiagnostic("companion-fight-blocked", new Dictionary<string, object?> { ["target"] = fight.Target, ["why"] = fight.Why, ["reason"] = blocked.Message });
				}
				session.BeginStep("companion", "with-its-player");
				await session.AdvanceAsync(TimeSpan.FromMilliseconds(250), token);
				if (!world.IsDead && FightTarget() == null) await combat.RestAsync(token);
				continue;
			}
			float gap = world.GroupMembers.TryGetValue(leader, out var f1)
				? NaturalServicePolicy.Distance(session.CurrentPosition, world.Objects.GetValueOrDefault(leader)?.Position ?? f1.Position) : 0;
			// Behind a leader that keeps walking it walks first. It runs no faster than its leader, so every cast on the
			// way is ground it does not make up until the leader stops: a blessing or an idle heal over time waits until
			// it stands beside its leader again. A member that is hurt cannot wait.
			bool hurt = world.CurrentHp * 100L <= world.MaxHp * 70L ||
				world.GroupMembers.Values.Any(member => member.Online && !member.Dead && member.MaxHp > 0 && member.HpPercent <= 70);
			if (healer != null && (hurt || gap <= 6)) await healer.StepAsync(token);
			// Left behind, it is on a long leg: the kit's Running scroll, by the rule every class has for one.
			if (gap > 12 && !combat.InCombat && runtime.NowMillis - scrollAskedMillis > 10_000)
			{
				scrollAskedMillis = runtime.NowMillis;
				await combat.BuffOurselfAsync(NaturalHelpTrigger.TravelLeg, token, NaturalHelpItemPolicy.LongTravelMeters);
			}
			// Far behind, it does not stop between steps.
			bool moved = await following.StepAsync(token);
			if (!moved || gap <= 6) await session.AdvanceAsync(TimeSpan.FromMilliseconds(250), token);
		}
		// Sent away, or the player left: the companion has nobody to follow and leaves the game. The server deletes it.
		session.BeginStep("companion", "leave-the-game");
		await session.SynchronizeAsync(token);
		var gained = Carried().Where(entry => entry.Key != BotWorldModel.KinahItemId && entry.Value > carriedAtStart.GetValueOrDefault(entry.Key))
			.ToDictionary(entry => entry.Key.ToString(), entry => entry.Value - carriedAtStart.GetValueOrDefault(entry.Key));
		int[] newQuests = [.. Journal().Except(journalAtStart).Order()];
		session.TraceDiagnostic("companion-leaves", new Dictionary<string, object?>
		{
			["group"] = world.GroupId, ["leaderNow"] = world.GroupLeaderId, ["leaderWas"] = leader, ["elapsedMillis"] = runtime.NowMillis - entered,
			["deaths"] = combat.ReviveCount, ["fights"] = fights, ["itemsGained"] = gained, ["newQuests"] = newQuests,
			["kinahGained"] = world.Kinah - carriedAtStart.GetValueOrDefault(BotWorldModel.KinahItemId),
		});
		if (gained.Count > 0) throw new InvalidDataException($"The companion carries loot it did not have: {string.Join(", ", gained.Select(entry => $"{entry.Key}x{entry.Value}"))}.");
		// A journal entry that is new was written by the server (the missions of a level); the trace names it.
		runtime.AssertClean();
		await session.QuitAsync(token);
	}

	/// <summary>
	/// The test's stand-in for the player: it asks for its companions in chat as a player would, plays as far as its
	/// checkpoint, writes what it saw, and sends them away.
	/// </summary>
	private async Task RunCompanionStandInAsync(CancellationToken token)
	{
		BotWorldModel world = session.Api.World;
		string[] asked = options.Companions is { Length: > 0 } wanted ? wanted : throw new InvalidDataException("The stand-in names no companion to ask for.");
		if (!CompanionCheckpoints.Contains(options.CompanionUntil))
			throw new InvalidDataException($"The stand-in plays to one of [{string.Join(", ", CompanionCheckpoints)}] so far, not to '{options.CompanionUntil}'.");
		if (world.GroupId != null) throw new InvalidDataException("The stand-in starts alone.");
		long started = runtime.NowMillis;

		async Task<string[]> SayAsync(string text)
		{
			int cursor = session.PacketHistory.Count;
			await session.SendPacketAsync(session.Api.Say(text), token);
			await session.SynchronizeAsync(token);
			return [.. session.PacketHistory.Skip(cursor).Where(packet => packet.PacketType == typeof(SM_MESSAGE)).Select(packet => packet.Get<string>("message"))];
		}
		async Task WaitAsync(Func<bool> ready, string reason, int seconds)
		{
			long began = runtime.NowMillis;
			while (!ready())
			{
				if (runtime.NowMillis - began > seconds * 1000L) throw new InvalidDataException("The stand-in waited in vain: " + reason);
				await session.AdvanceAsync(TimeSpan.FromMilliseconds(250), token);
				await session.SynchronizeAsync(token);
			}
		}

		var joined = new List<object>();
		foreach (string playerClass in asked)
		{
			session.BeginStep("stand-in", "ask-for-" + playerClass);
			HashSet<int> before = [.. world.GroupMembers.Keys];
			string[] answer = await SayAsync(".companion " + playerClass);
			if (!answer.Any(line => line.Contains("is on its way to you.", StringComparison.Ordinal)))
				throw new InvalidDataException($"The server made no {playerClass} companion: {string.Join(" / ", answer)}");
			long askedAt = runtime.NowMillis;
			await WaitAsync(() => world.GroupMembers.Keys.Any(member => member != session.CharacterId && !before.Contains(member)),
				$"no {playerClass} joined the group", 120);
			BotGroupMember member = world.GroupMembers.Values.Single(candidate => candidate.ObjectId != session.CharacterId && !before.Contains(candidate.ObjectId));
			var seen = new { member.ObjectId, member.Name, playerClass = ((PlayerClass)member.PlayerClass).ToString(), member.Level,
				member.MapId, afterMillis = runtime.NowMillis - askedAt };
			session.TraceDiagnostic("stand-in-companion-joined", new Dictionary<string, object?> { ["companion"] = seen, ["leader"] = world.GroupLeaderId });
			joined.Add(seen);
		}

		session.BeginStep("stand-in", "checkpoint-" + options.CompanionUntil);
		if (world.GroupLeaderId != session.CharacterId) throw new InvalidDataException("The stand-in does not lead its group.");
		if (world.GroupLootRules.Count == 0 || world.GroupLootRules.Any(rule => rule != 0))
			throw new InvalidDataException($"The group's loot is [{string.Join(", ", world.GroupLootRules)}], not free for all with every quality picked up as it is.");
		if (world.GroupMembers.Count != asked.Length + 1)
			throw new InvalidDataException($"The group has {world.GroupMembers.Count} members, not the stand-in and its {asked.Length} companions.");
		foreach ((BotGroupMember member, string playerClass) in world.GroupMembers.Values.Where(member => member.ObjectId != session.CharacterId)
			.OrderBy(member => member.ObjectId).Zip(asked))
		{
			if (!CompanionService.TryParseClass(playerClass, out PlayerClass wantedClass) || member.PlayerClass != wantedClass.GetClassId()
				|| member.Level != 26 || member.MapId != world.MapId)
				throw new InvalidDataException($"The companion {member.Name} is a level {member.Level} class {member.PlayerClass} on map {member.MapId}, not a level 26 {playerClass} here.");
		}
		string[] list = await SayAsync(".companion list");

		int reach = Array.IndexOf(CompanionCheckpoints, options.CompanionUntil);
		BotGroupMember[] Others() => [.. world.GroupMembers.Values.Where(member => member.ObjectId != session.CharacterId).OrderBy(member => member.ObjectId)];
		float Away(BotGroupMember member) => NaturalServicePolicy.Distance(session.CurrentPosition,
			world.Objects.GetValueOrDefault(member.ObjectId)?.Position ?? member.Position);
		var walks = new List<object>();
		HashSet<int> effectsBefore = [.. (world.VisibleEffects ?? []).Select(effect => effect.SkillId)];
		if (reach >= 1)
		{
			// C4: across the fortress to Vallack and back, as a player walks: it waits for nobody on its way.
			var geometry = runtime.CreateGeometry();
			var navigator = new NaturalJourneyNavigator(session, BotNavigationGraphFactory.Build(runtime.Data, null, geometry), geometry, runtime,
				stopOnDeath: false) { AvoidHostileAggro = true, Planner = BotTravelPlanner.For(world.MapId!.Value, geometry, runtime.Data) };
			BotPosition home = session.CurrentPosition;
			BotPosition vallack = runtime.Data.SpawnsDh.GetSpawnsByWorldId(world.MapId!.Value).Where(group => group.GetNpcId() == GroupNtcVallack)
				.SelectMany(group => group.GetSpawnTemplates()).Select(spot => new BotPosition(spot.GetX(), spot.GetY(), spot.GetZ(), spot.GetHeading())).First();
			foreach ((string name, BotPosition destination) in new[] { ("to-vallack", vallack), ("back", home) })
			{
				session.BeginStep("stand-in", "walk-" + name);
				long began = runtime.NowMillis;
				BotPosition from = session.CurrentPosition;
				var farthest = Others().ToDictionary(member => member.ObjectId, _ => 0f);
				var route = await navigator.FindRouteAsync(from, destination, token);
				if (route.Count == 0) throw new InvalidDataException($"The stand-in found no way to {name}: {navigator.LastRouteDiagnostic}");
				foreach (BotPosition point in route)
				{
					while (NaturalServicePolicy.Distance(session.CurrentPosition, point) > .5f && NaturalServicePolicy.Distance(session.CurrentPosition, destination) > 4)
					{
						BotPosition at = session.CurrentPosition;
						float fraction = Math.Min(1, 6 / NaturalServicePolicy.Distance(at, point));
						await navigator.MoveAsync([at, new(at.X + (point.X - at.X) * fraction, at.Y + (point.Y - at.Y) * fraction,
							at.Z + (point.Z - at.Z) * fraction, point.Heading)], token);
						await session.SynchronizeAsync(token);
						foreach (BotGroupMember member in Others()) farthest[member.ObjectId] = Math.Max(farthest.GetValueOrDefault(member.ObjectId), Away(member));
						if (NaturalServicePolicy.Distance(session.CurrentPosition, at) < .1f) throw new InvalidDataException($"The stand-in could not go on to {name}.");
					}
				}
				long arrived = runtime.NowMillis;
				await WaitAsync(() => Others().All(member => member.Online && member.MapId == world.MapId && Away(member) <= 6),
					$"the companions did not come up after the walk {name}", 60);
				var walk = new { name, metres = NaturalServicePolicy.Distance(from, session.CurrentPosition), walkMillis = arrived - began,
					cameUpMillis = runtime.NowMillis - arrived,
					farthest = Others().Select(member => new { member.Name, metres = farthest.GetValueOrDefault(member.ObjectId), atTheEnd = Away(member) }).ToArray() };
				session.TraceDiagnostic("stand-in-walk", new Dictionary<string, object?> { ["walk"] = walk });
				walks.Add(walk);
			}
			// The Cleric's support reaches its player: an effect on the stand-in that it did not have when it was alone.
			if (asked.Any(playerClass => CompanionService.TryParseClass(playerClass, out PlayerClass support) && support == PlayerClass.CLERIC))
				await WaitAsync(() => (world.VisibleEffects ?? []).Any(effect => !effectsBefore.Contains(effect.SkillId) &&
					Others().Any(member => member.ObjectId == effect.EffectorId)), "no support of the Cleric is on the stand-in", 60);
		}
		var marks = new List<object>();
		var loots = new List<object>();
		if (reach >= 2)
		{
			// C5: in the field outside the fortress, at the monsters of its repeatable coin quest. First a mark alone, which
			// the stand-in does not attack. Then two marks, the higher one set first. Then no mark: the stand-in attacks.
			var geometry = runtime.CreateGeometry();
			var navigator = new NaturalJourneyNavigator(session, BotNavigationGraphFactory.Build(runtime.Data, null, geometry), geometry, runtime,
				stopOnDeath: false) { AvoidHostileAggro = true, Planner = BotTravelPlanner.For(world.MapId!.Value, geometry, runtime.Data) };
			var combat = new NaturalJourneyCombat(session, navigator, runtime, geometry, stopOnDeath: false,
				conservativeRangedHold: false, NaturalMauPolicyParameters.Baseline, ClassLine) { ApproachMapId = world.MapId!.Value };
			int[] kinds = [.. QuestRunBook.Build(NaturalAltgardContract.LoadMusterPlans().Values.Single()).Operations
				.Where(op => op.Kind == QuestRunOperationKind.Kill).SelectMany(op => op.Npcs!).Select(npc => npc.Id)
				.Where(id => runtime.Data.SpawnsDh.GetSpawnsByWorldId(world.MapId!.Value).Any(group => group.GetNpcId() == id)).Distinct()];
			BotPosition[] grounds = [.. runtime.Data.SpawnsDh.GetSpawnsByWorldId(world.MapId!.Value).Where(group => kinds.Contains(group.GetNpcId()))
				.SelectMany(group => group.GetSpawnTemplates()).Select(spot => new BotPosition(spot.GetX(), spot.GetY(), spot.GetZ(), spot.GetHeading()))
				.OrderBy(at => NaturalServicePolicy.Distance(session.CurrentPosition, at))];
			var used = new HashSet<int>();
			BotKnownObject[] InView() => [.. world.Objects.Values.Where(known => known.Kind == BotKnownObjectKind.Npc && known.TemplateId is int kind &&
				kinds.Contains(kind) && !known.IsCorpse && !used.Contains(known.ObjectId) &&
				NaturalServicePolicy.Distance(session.CurrentPosition, known.Position) <= 30)
				.OrderBy(known => NaturalServicePolicy.Distance(session.CurrentPosition, known.Position))];
			// A monster has fallen when this client saw its health reach nothing. It is no corpse to the client yet then.
			int since = session.PacketHistory.Count;
			bool Fell(int target) => !world.Objects.TryGetValue(target, out var seen) || seen.IsCorpse || session.PacketHistory.Skip(since)
				.Any(packet => BotAttackStatus.CarriesHp(packet) && packet.Get<int>("objectId") == target && packet.Get<byte>("hpOrMp") == 0);
			// The stand-in walks towards the hunting ground until it has enough living monsters in view.
			async Task<BotKnownObject[]> FindAsync(int count)
			{
				foreach (BotPosition ground in grounds)
				{
					if (InView().Length >= count) break;
					var route = await navigator.FindRouteAsync(session.CurrentPosition, ground, token);
					foreach (BotPosition point in route)
					{
						while (InView().Length < count && NaturalServicePolicy.Distance(session.CurrentPosition, point) > .5f)
						{
							BotPosition at = session.CurrentPosition;
							float fraction = Math.Min(1, 6 / NaturalServicePolicy.Distance(at, point));
							await navigator.MoveAsync([at, new(at.X + (point.X - at.X) * fraction, at.Y + (point.Y - at.Y) * fraction,
								at.Z + (point.Z - at.Z) * fraction, point.Heading)], token);
							await session.SynchronizeAsync(token);
							if (NaturalServicePolicy.Distance(session.CurrentPosition, at) < .1f) break;
						}
						if (InView().Length >= count) break;
					}
				}
				BotKnownObject[] found = [.. InView().Take(count)];
				if (found.Length < count) throw new InvalidDataException($"The stand-in found {found.Length} of the {count} living monsters it wanted to mark.");
				foreach (BotKnownObject monster in found) used.Add(monster.ObjectId);
				// A player lets its group come up before a pull.
				await WaitAsync(() => Others().All(member => member.Online && Away(member) <= 12), "the companions did not come up before the marks", 90);
				return found;
			}
			// C6: the group's loot is free for all, so every corpse is the stand-in's to open at once, the one it never
			// struck too. It takes what is there, as a player would. Quest items are nobody's here.
			var looter = new NaturalGroupLootDriver(session, runtime, _ => false);
			async Task LootAsync(string test, params int[] corpses)
			{
				if (reach < 3) return;
				session.BeginStep("stand-in", "loot-" + test);
				await session.AdvanceAsync(TimeSpan.FromMilliseconds(500), token);
				await session.SynchronizeAsync(token);
				// 0: this client may open the corpse. No entry: the corpse holds nothing.
				var rights = corpses.Select(corpse => new { corpse, status = world.LootStatuses.TryGetValue(corpse, out byte status) ? status : -1 }).ToArray();
				int before = looter.Collected, opened = 0;
				// A player walks up to a corpse it wants to open.
				foreach (var right in rights.Where(right => right.status == 0))
				{
					if (!world.Objects.TryGetValue(right.corpse, out var body)) continue;
					var route = await navigator.FindRouteAsync(session.CurrentPosition, body.Position, token);
					foreach (BotPosition point in route)
					{
						while (NaturalServicePolicy.Distance(session.CurrentPosition, body.Position) > 3 && NaturalServicePolicy.Distance(session.CurrentPosition, point) > .5f)
						{
							BotPosition at = session.CurrentPosition;
							float fraction = Math.Min(1, 6 / NaturalServicePolicy.Distance(at, point));
							await navigator.MoveAsync([at, new(at.X + (point.X - at.X) * fraction, at.Y + (point.Y - at.Y) * fraction,
								at.Z + (point.Z - at.Z) * fraction, point.Heading)], token);
							await session.SynchronizeAsync(token);
							if (NaturalServicePolicy.Distance(session.CurrentPosition, at) < .1f) break;
						}
					}
					for (int turn = 0; turn < 8 && world.LootStatuses.TryGetValue(right.corpse, out byte still) && still == 0; turn++)
					{
						if (await looter.StepAsync(token)) { opened++; break; }
						await session.AdvanceAsync(TimeSpan.FromMilliseconds(250), token);
					}
				}
				var loot = new { test, rights, opened, items = looter.Collected - before, kinah = world.Kinah };
				session.TraceDiagnostic("stand-in-loot", new Dictionary<string, object?> { ["loot"] = loot });
				loots.Add(loot);
				if (rights.Any(right => right.status is not (0 or -1)))
					throw new InvalidDataException($"A corpse was not the stand-in's to open at once: {string.Join(", ", rights.Select(right => $"{right.corpse}:{right.status}"))}.");
				if (opened < rights.Count(right => right.status == 0)) throw new InvalidDataException("The stand-in could not open a corpse it had the right to.");
			}
			// Which companions struck a monster since a point of the packet history, as this client saw it.
			string[] Strikers(int target, int since) => [.. Others().Where(member => session.PacketHistory.Skip(since)
				.Any(packet => runtime.IncomingAttacker(packet, target) == member.ObjectId)).Select(member => member.Name!)];

			session.BeginStep("stand-in", "mark-one");
			BotKnownObject alone = (await FindAsync(1))[0];
			since = session.PacketHistory.Count;
			long markedAt = runtime.NowMillis;
			await session.SendPacketAsync(session.Api.Mark(CompanionFirstMark, alone.ObjectId), token);
			await WaitAsync(() => Fell(alone.ObjectId), "the marked monster did not fall", 180);
			var one = new { test = "a mark alone, not attacked by the stand-in", target = alone.ObjectId, npc = alone.TemplateId,
				fellAfterMillis = runtime.NowMillis - markedAt, strikers = Strikers(alone.ObjectId, since),
				seenMarks = world.GroupBrands.OrderBy(entry => entry.Key).Select(entry => $"{entry.Key}:{entry.Value}").ToArray() };
			session.TraceDiagnostic("stand-in-marks", new Dictionary<string, object?> { ["result"] = one });
			marks.Add(one);
			await session.SendPacketAsync(session.Api.Mark(CompanionFirstMark, 0), token);
			await LootAsync("the-mark-alone", alone.ObjectId);

			session.BeginStep("stand-in", "mark-two");
			BotKnownObject[] pair = await FindAsync(2);
			since = session.PacketHistory.Count;
			markedAt = runtime.NowMillis;
			// The higher mark is set first and on the nearer monster: only the mark's number can make the other fall first.
			await session.SendPacketAsync(session.Api.Mark(CompanionSecondMark, pair[0].ObjectId), token);
			await session.SendPacketAsync(session.Api.Mark(CompanionFirstMark, pair[1].ObjectId), token);
			long? lowFell = null, highFell = null;
			await WaitAsync(() =>
			{
				if (lowFell == null && Fell(pair[1].ObjectId)) lowFell = runtime.NowMillis - markedAt;
				if (highFell == null && Fell(pair[0].ObjectId)) highFell = runtime.NowMillis - markedAt;
				return lowFell != null && highFell != null;
			}, "the two marked monsters did not fall", 300);
			var two = new { test = "two marks, the higher set first and on the nearer monster", low = pair[1].ObjectId, high = pair[0].ObjectId,
				lowFellAfterMillis = lowFell, highFellAfterMillis = highFell, lowStrikers = Strikers(pair[1].ObjectId, since),
				highStrikers = Strikers(pair[0].ObjectId, since) };
			session.TraceDiagnostic("stand-in-marks", new Dictionary<string, object?> { ["result"] = two });
			marks.Add(two);
			if (lowFell > highFell) throw new InvalidDataException("The monster with the higher mark fell before the one with the lower mark.");
			await session.SendPacketAsync(session.Api.Mark(CompanionFirstMark, 0), token);
			await session.SendPacketAsync(session.Api.Mark(CompanionSecondMark, 0), token);
			await LootAsync("the-two-marks", pair[1].ObjectId, pair[0].ObjectId);

			session.BeginStep("stand-in", "no-mark");
			BotKnownObject unmarked = (await FindAsync(1))[0];
			await session.SynchronizeAsync(token);
			if (world.GroupBrands.Count != 0) throw new InvalidDataException("A mark is still set after the stand-in took its marks off.");
			since = session.PacketHistory.Count;
			markedAt = runtime.NowMillis;
			bool fell = false;
			try { fell = await combat.TryKillAsync(unmarked.ObjectId, token); }
			catch (NaturalCombatApproachBlockedException) { fell = Fell(unmarked.ObjectId); }
			await WaitAsync(() => Fell(unmarked.ObjectId), "the monster the stand-in attacked did not fall", 120);
			var none = new { test = "no mark: the stand-in attacks", target = unmarked.ObjectId, npc = unmarked.TemplateId, standInSawItFall = fell,
				fellAfterMillis = runtime.NowMillis - markedAt, strikers = Strikers(unmarked.ObjectId, since) };
			session.TraceDiagnostic("stand-in-marks", new Dictionary<string, object?> { ["result"] = none });
			marks.Add(none);
			await LootAsync("the-stand-ins-target", unmarked.ObjectId);
			// Every damage class struck each monster's turn; the Cleric struck none.
			string[] fighters = [.. Others().Where(member => member.PlayerClass != PlayerClass.CLERIC.GetClassId()).Select(member => member.Name!)];
			string[] healers = [.. Others().Where(member => member.PlayerClass == PlayerClass.CLERIC.GetClassId()).Select(member => member.Name!)];
			foreach ((string test, string[] strikers) in new[] { ("the mark alone", one.strikers), ("the lower mark", two.lowStrikers),
				("the higher mark", two.highStrikers), ("the stand-in's target", none.strikers) })
			{
				if (strikers.Intersect(healers).Any()) throw new InvalidDataException($"The Cleric struck {test}.");
				if (fighters.Length > 0 && !strikers.Intersect(fighters).Any()) throw new InvalidDataException($"No damage class struck {test}.");
			}
			await combat.RestAsync(token);
		}
		var support = (world.VisibleEffects ?? []).Where(effect => Others().Any(member => member.ObjectId == effect.EffectorId))
			.Select(effect => new { effect.SkillId, effect.EffectorId }).ToArray();
		runtime.AssertClean();
		var receipt = new { verified = true, CharacterId = session.CharacterId, until = options.CompanionUntil, asked, companions = joined,
			leader = world.GroupLeaderId, group = world.GroupId, lootRules = world.GroupLootRules, list, walks, marks, loots, support,
			elapsedMillis = runtime.NowMillis - started, map = world.MapId, position = session.CurrentPosition };

		session.BeginStep("stand-in", "send-them-away");
		string[] farewell = await SayAsync(".companion dismiss");
		await WaitAsync(() => world.GroupId == null, "the group did not end after the dismissal", 60);
		File.WriteAllText(Path.Combine(Path.GetDirectoryName(session.CombatTracePath!)!, "companion-completion.json"),
			JsonSerializer.Serialize(new { receipt.verified, receipt.CharacterId, receipt.until, receipt.asked, receipt.companions, receipt.leader,
				receipt.group, receipt.lootRules, receipt.list, receipt.walks, receipt.marks, receipt.loots, receipt.support, farewell, receipt.elapsedMillis, receipt.map, receipt.position },
				new JsonSerializerOptions { WriteIndented = true }));
		session.TraceDiagnostic("stand-in-reached", new Dictionary<string, object?> { ["receipt"] = receipt, ["farewell"] = farewell });
	}
}
