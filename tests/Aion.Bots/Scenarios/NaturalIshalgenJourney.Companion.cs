using System.Text.Json;
using Aion.Bots.Navigation;
using Aion.Bots.Navigation.NavMesh;
using Aion.Bots.Protocol;
using Aion.Bots.Reflexes;
using Aion.Bots.Scenarios.Classes;
using Aion.Bots.World;
using Aion.GameServer.Custom.Companion;
using Aion.GameServer.Model;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.Bots.Scenarios;

public sealed partial class NaturalIshalgenJourney
{
	/// <summary>How far the stand-in plays so far (docs/companion-bots.md, the C steps).</summary>
	private static readonly string[] CompanionCheckpoints = ["joined", "walk", "marks", "loot", "inside", "general"];
	/// <summary>The named monsters on the stand-in's way through the camp, in the order the Group NTC leg takes them.</summary>
	private static readonly int[] CompanionNtcNamed = [256690, 256689, 256692];
	/// <summary>The two marks the stand-in sets; a companion takes the lowest mark first whatever its number.</summary>
	private const int CompanionFirstMark = 1, CompanionSecondMark = 2;

	private static (int Npc, ushort Dialog, int LocId, string Race, int MinLevel)[]? companionPortalPaths;

	/// <summary>
	/// Every dialog portal of the shipped data: the NPC, the dialog choice, where it leads, and whom it takes. The server's
	/// own table of them (Portal2Data) keeps its entries to itself once it is loaded, as Java's does, so a bot reads the
	/// file the way it reads the other shipped data it plans from.
	/// </summary>
	private static (int Npc, ushort Dialog, int LocId, string Race, int MinLevel)[] CompanionPortalPaths(string repoRoot) =>
		companionPortalPaths ??= [.. System.Xml.Linq.XDocument.Load(Path.Combine(repoRoot, "game-server", "data", "static_data", "portals", "portal_template2.xml"))
			.Root!.Elements("portal_dialog").SelectMany(dialog => dialog.Elements("portal_path").Select(path => (
				Npc: (int)dialog.Attribute("npc_id")!, Dialog: checked((ushort)(int)path.Attribute("dialog")!), LocId: (int)path.Attribute("loc_id")!,
				Race: (string?)path.Attribute("race") ?? "PC_ALL", MinLevel: (int?)path.Attribute("min_level") ?? 0)))];

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
		combat.WaitForGroupResurrectionAsync = ct => WaitForCompanionResurrectionAsync(healer != null, following.Reset, ct);
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
		// The portal NPC of this map that leads to another map, by the data the client ships with (portal_template2.xml,
		// portal_loc.xml): the NPC, and the dialog choice that sends a player through.
		(int Npc, ushort Dialog)? PortalTo(int destination)
		{
			HashSet<int> here = [.. runtime.Data.SpawnsDh.GetSpawnsByWorldId(world.MapId!.Value).Select(group => group.GetNpcId())];
			foreach (var path in CompanionPortalPaths(runtime.RepoRoot))
				if (here.Contains(path.Npc) && path.Race is "PC_ALL" or "ASMODIANS" && path.MinLevel <= world.Level &&
					runtime.Data.PortalLocDataDh.GetPortalLoc(path.LocId)?.GetWorldId() == destination)
					return (path.Npc, path.Dialog);
			return null;
		}
		var noWay = new HashSet<int>();
		int portals = 0;
		// Its leader has gone to another map: the companion walks to the portal NPC that leads there and goes through.
		async Task<bool> ThroughPortalAsync(int destination)
		{
			if (PortalTo(destination) is not { } portal)
			{
				if (noWay.Add(destination))
				{
					session.TraceDiagnostic("companion-no-way", new Dictionary<string, object?> { ["from"] = world.MapId, ["to"] = destination });
					await session.SendPacketAsync(session.Api.Say("I cannot reach you.", 5), token);
				}
				return false;
			}
			session.BeginStep("companion", $"portal-npc{portal.Npc}");
			BotKnownObject? Gate() => world.Objects.Values.Where(known => known.TemplateId == portal.Npc && !known.IsCorpse)
				.OrderBy(known => NaturalServicePolicy.Distance(session.CurrentPosition, known.Position)).FirstOrDefault();
			BotPosition spot = Gate()?.Position ?? runtime.Data.SpawnsDh.GetSpawnsByWorldId(world.MapId!.Value).Where(group => group.GetNpcId() == portal.Npc)
				.SelectMany(group => group.GetSpawnTemplates()).Select(at => new BotPosition(at.GetX(), at.GetY(), at.GetZ(), at.GetHeading()))
				.OrderBy(at => NaturalServicePolicy.Distance(session.CurrentPosition, at)).First();
			if (NaturalServicePolicy.Distance(session.CurrentPosition, spot) > 4)
			{
				var route = await navigator.FindRouteAsync(session.CurrentPosition, spot, token);
				foreach (BotPosition point in route)
				{
					while (NaturalServicePolicy.Distance(session.CurrentPosition, spot) > 4 && NaturalServicePolicy.Distance(session.CurrentPosition, point) > .5f)
					{
						BotPosition from = session.CurrentPosition;
						float fraction = Math.Min(1, 6 / NaturalServicePolicy.Distance(from, point));
						await navigator.MoveAsync([from, new(from.X + (point.X - from.X) * fraction, from.Y + (point.Y - from.Y) * fraction,
							from.Z + (point.Z - from.Z) * fraction, point.Heading)], token);
						await session.SynchronizeAsync(token);
						if (world.IsDead || NaturalServicePolicy.Distance(session.CurrentPosition, from) < .1f) break;
					}
					if (world.IsDead) return false;
				}
			}
			if (Gate() is not { } gate || NaturalServicePolicy.Distance(session.CurrentPosition, gate.Position) > 6)
			{
				session.TraceDiagnostic("companion-portal-not-reached", new Dictionary<string, object?> { ["npc"] = portal.Npc, ["position"] = session.CurrentPosition });
				return false;
			}
			int fromMap = world.MapId!.Value;
			await NaturalCapitalSteps.PortalAsync(session, new NaturalCapitalPortal(portal.Npc, fromMap, destination, 0, portal.Dialog), gate.ObjectId, token);
			EnterMap();
			portals++;
			session.TraceDiagnostic("companion-through-portal", new Dictionary<string, object?>
			{ ["npc"] = portal.Npc, ["from"] = fromMap, ["map"] = world.MapId, ["instance"] = world.InstanceId, ["position"] = session.CurrentPosition });
			return true;
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
			if (world.GroupMembers.TryGetValue(leader, out var gone) && gone.Online && gone.MapId != 0 && gone.MapId != world.MapId)
			{
				if (!await ThroughPortalAsync(gone.MapId)) await session.AdvanceAsync(TimeSpan.FromSeconds(2), token);
				continue;
			}
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
			["deaths"] = combat.ReviveCount, ["fights"] = fights, ["portals"] = portals, ["map"] = world.MapId, ["itemsGained"] = gained, ["newQuests"] = newQuests,
			["kinahGained"] = world.Kinah - carriedAtStart.GetValueOrDefault(BotWorldModel.KinahItemId),
		});
		if (gained.Count > 0) throw new InvalidDataException($"The companion carries loot it did not have: {string.Join(", ", gained.Select(entry => $"{entry.Key}x{entry.Value}"))}.");
		// A journal entry that is new was written by the server (the missions of a level); the trace names it.
		runtime.AssertClean();
		await session.QuitAsync(token);
	}

	/// <summary>
	/// A fallen member waits a minute for the group's Cleric to offer a resurrection and takes it; with no Cleric alive,
	/// or none that offers, it revives at its bind (docs/natural-group-healer.md). The Cleric itself does not wait.
	/// </summary>
	private async Task<bool> WaitForCompanionResurrectionAsync(bool isTheHealer, Action risen, CancellationToken token)
	{
		BotWorldModel world = session.Api.World;
		if (isTheHealer) return false;
		long began = runtime.NowMillis;
		int death = session.PacketHistory.Count - 1;
		while (death >= 0 && session.PacketHistory[death].PacketType != typeof(SM_DIE)) death--;
		session.TraceDiagnostic("companion-wait-for-resurrection", new Dictionary<string, object?> { ["position"] = session.CurrentPosition });
		while (world.IsDead && runtime.NowMillis - began < 60_000)
		{
			var cleric = world.GroupMembers.Values.FirstOrDefault(member => member.PlayerClass == PlayerClass.CLERIC.GetClassId() && member.Online && !member.Dead);
			if (cleric == null) return false;
			var offer = session.PacketHistory.Skip(death + 1).LastOrDefault(packet => packet.PacketType == typeof(SM_RESURRECT) && packet.Get<string>("casterName") == cleric.Name);
			if (offer != null)
			{
				await session.SendPacketAsync(session.Api.Revive(BotReviveType.Skill), token);
				await session.SynchronizeAsync(token);
				if (!world.IsDead)
				{
					session.TraceDiagnostic("companion-resurrected", new Dictionary<string, object?> { ["by"] = cleric.Name, ["afterMillis"] = runtime.NowMillis - began });
					risen();
					return true;
				}
			}
			await session.AdvanceAsync(TimeSpan.FromMilliseconds(500), token);
			await session.SynchronizeAsync(token);
		}
		session.TraceDiagnostic("companion-resurrection-fallback", new Dictionary<string, object?> { ["reason"] = "No Cleric's offer within a minute; revive at the bind." });
		return false;
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
		object? inside = null, general = null;
		if (reach >= 4)
		{
			// C7: to Vallack and into Nochsana Training Camp, as a player goes in: it tells nobody and waits for nobody.
			session.BeginStep("stand-in", "into-the-camp");
			var geometry = runtime.CreateGeometry();
			var navigator = new NaturalJourneyNavigator(session, BotNavigationGraphFactory.Build(runtime.Data, null, geometry), geometry, runtime,
				stopOnDeath: false) { AvoidHostileAggro = true, Planner = BotTravelPlanner.For(world.MapId!.Value, geometry, runtime.Data) };
			BotPosition vallack = runtime.Data.SpawnsDh.GetSpawnsByWorldId(world.MapId!.Value).Where(group => group.GetNpcId() == GroupNtcVallack)
				.SelectMany(group => group.GetSpawnTemplates()).Select(spot => new BotPosition(spot.GetX(), spot.GetY(), spot.GetZ(), spot.GetHeading())).First();
			var route = await navigator.FindRouteAsync(session.CurrentPosition, vallack, token);
			if (route.Count == 0) throw new InvalidDataException($"The stand-in found no way to Vallack: {navigator.LastRouteDiagnostic}");
			foreach (BotPosition point in route)
			{
				while (NaturalServicePolicy.Distance(session.CurrentPosition, point) > .5f && NaturalServicePolicy.Distance(session.CurrentPosition, vallack) > 4)
				{
					BotPosition at = session.CurrentPosition;
					float fraction = Math.Min(1, 6 / NaturalServicePolicy.Distance(at, point));
					await navigator.MoveAsync([at, new(at.X + (point.X - at.X) * fraction, at.Y + (point.Y - at.Y) * fraction,
						at.Z + (point.Z - at.Z) * fraction, point.Heading)], token);
					await session.SynchronizeAsync(token);
					if (NaturalServicePolicy.Distance(session.CurrentPosition, at) < .1f) throw new InvalidDataException("The stand-in could not go on to Vallack.");
				}
			}
			int gate = world.Objects.Values.Where(known => known.TemplateId == GroupNtcVallack)
				.OrderBy(known => NaturalServicePolicy.Distance(session.CurrentPosition, known.Position)).First().ObjectId;
			long wentIn = runtime.NowMillis;
			await NaturalCapitalSteps.PortalAsync(session, new NaturalCapitalPortal(GroupNtcVallack, world.MapId!.Value, GroupNtcMap, 0, GroupNtcEnterDialog), gate, token);
			if (world.MapId != GroupNtcMap || world.InstanceId == null) throw new InvalidDataException("The stand-in is not inside the camp.");
			// A companion that this client sees stands in this client's instance.
			await WaitAsync(() => Others().All(member => member.Online && member.MapId == world.MapId && world.Objects.ContainsKey(member.ObjectId) && Away(member) <= 12),
				"the companions did not follow the stand-in into the camp", 180);
			var entered = new { map = world.MapId, instance = world.InstanceId, allInsideAfterMillis = runtime.NowMillis - wentIn,
				companions = Others().Select(member => new { member.Name, member.MapId, metres = Away(member) }).ToArray() };
			session.TraceDiagnostic("stand-in-inside", new Dictionary<string, object?> { ["inside"] = entered });
			inside = entered;

			if (reach >= 5)
			{
				// C8: through the camp to the General, on the way the Group NTC leg takes without its quests and chests. The
				// stand-in plays as a player who leads plays: it lets its group come up before a pull, puts its mark on what
				// it pulls, fights it, and opens the corpse. It says nothing to anybody.
				long began = runtime.NowMillis;
				geometry = runtime.CreateGeometry();
				NaturalJourneyNavigator Inside() => new(session, BotNavigationGraphFactory.Build(runtime.Data, null, geometry), geometry, runtime, stopOnDeath: false);
				navigator = Inside();
				var combat = new NaturalJourneyCombat(session, navigator, runtime, geometry, stopOnDeath: false,
					conservativeRangedHold: false, NaturalMauPolicyParameters.Baseline, ClassLine) { ApproachMapId = world.MapId!.Value };
				void EnterMap()
				{
					geometry = runtime.CreateGeometry();
					navigator = world.MapId == GroupNtcMap ? Inside() : new NaturalJourneyNavigator(session, BotNavigationGraphFactory.Build(runtime.Data, null, geometry),
						geometry, runtime, stopOnDeath: false) { AvoidHostileAggro = true, Planner = BotTravelPlanner.For(world.MapId!.Value, geometry, runtime.Data) };
					combat.EnterMap(navigator, geometry, world.MapId!.Value);
				}
				combat.AfterBindRevive = EnterMap;
				combat.AfterInstanceRevive = EnterMap;
				combat.WaitForGroupResurrectionAsync = ct => WaitForCompanionResurrectionAsync(false, () => { }, ct);
				var looter = new NaturalGroupLootDriver(session, runtime, _ => false);
				var refused = new Dictionary<int, int>();
				var killed = new HashSet<int>();
				var fallenKinds = new List<object>();
				var killedKinds = new HashSet<int>();
				int pulls = 0, corpsesOpened = 0, reentries = 0;
				int generalSince = 0;
				BotPosition Lair(int template) => runtime.Data.SpawnsDh.GetSpawnsByWorldId(world.MapId!.Value).Where(group => group.GetNpcId() == template)
					.SelectMany(group => group.GetSpawnTemplates()).Select(spot => new BotPosition(spot.GetX(), spot.GetY(), spot.GetZ(), spot.GetHeading()))
					.OrderBy(at => NaturalServicePolicy.Distance(session.CurrentPosition, at)).First();
				(BotKnownObject Seen, float Reach)[] Hostiles() => [.. world.Objects.Values
					.Where(known => known.Kind == BotKnownObjectKind.Npc && known.TemplateId != null && !known.IsCorpse && !killed.Contains(known.ObjectId))
					.Select(known => (known, template: runtime.Data.NpcDataDh.GetNpcTemplate(known.TemplateId!.Value)))
					.Where(entry => runtime.IsAggressive(entry.template) && entry.template!.GetAggroRange() > 0)
					.Select(entry => (entry.known, entry.template!.GetAggroRange() + 2f))];
				// The monsters that struck a member of the group a moment ago, as this client saw it.
				int? OnTheGroup((BotKnownObject Seen, float Reach)[] hostiles)
				{
					HashSet<int> striking = [.. session.PacketHistory.TakeLast(300)
						.SelectMany(packet => world.GroupMembers.Keys.Select(member => runtime.IncomingAttacker(packet, member))).OfType<int>()];
					return hostiles.Where(hostile => striking.Contains(hostile.Seen.ObjectId))
						.OrderBy(hostile => NaturalServicePolicy.Distance(session.CurrentPosition, hostile.Seen.Position)).Select(hostile => (int?)hostile.Seen.ObjectId).FirstOrDefault();
				}
				static float FromSegment(BotPosition a, BotPosition b, BotPosition point)
				{
					float dx = b.X - a.X, dy = b.Y - a.Y, dz = b.Z - a.Z, length = dx * dx + dy * dy + dz * dz;
					float t = length < .0001f ? 0 : Math.Clamp(((point.X - a.X) * dx + (point.Y - a.Y) * dy + (point.Z - a.Z) * dz) / length, 0, 1);
					return NaturalServicePolicy.Distance(new(a.X + dx * t, a.Y + dy * t, a.Z + dz * t, 0), point);
				}
				bool GoneOrKilled(int target) => !world.Objects.TryGetValue(target, out var seen) || seen.IsCorpse ||
					session.PacketHistory.TakeLast(4000).Any(packet => BotAttackStatus.CarriesHp(packet) &&
						packet.Get<int>("objectId") == target && packet.Get<byte>("hpOrMp") == 0);
				// Back outside after a revive at the bind: to Vallack and in again.
				async Task BackInAsync()
				{
					session.BeginStep("stand-in", "back-into-the-camp");
					EnterMap();
					BotPosition door = Lair(GroupNtcVallack);
					var way = await navigator.FindRouteAsync(session.CurrentPosition, door, token);
					if (way.Count == 0) throw new InvalidDataException($"The stand-in found no way back to Vallack: {navigator.LastRouteDiagnostic}");
					foreach (BotPosition point in way)
						while (NaturalServicePolicy.Distance(session.CurrentPosition, point) > .5f && NaturalServicePolicy.Distance(session.CurrentPosition, door) > 4)
						{
							BotPosition at = session.CurrentPosition;
							float fraction = Math.Min(1, 6 / NaturalServicePolicy.Distance(at, point));
							await navigator.MoveAsync([at, new(at.X + (point.X - at.X) * fraction, at.Y + (point.Y - at.Y) * fraction,
								at.Z + (point.Z - at.Z) * fraction, point.Heading)], token);
							await session.SynchronizeAsync(token);
							if (NaturalServicePolicy.Distance(session.CurrentPosition, at) < .1f) throw new InvalidDataException("The stand-in could not go back to Vallack.");
						}
					int keeper = world.Objects.Values.Where(known => known.TemplateId == GroupNtcVallack)
						.OrderBy(known => NaturalServicePolicy.Distance(session.CurrentPosition, known.Position)).First().ObjectId;
					await NaturalCapitalSteps.PortalAsync(session, new NaturalCapitalPortal(GroupNtcVallack, world.MapId!.Value, GroupNtcMap, 0, GroupNtcEnterDialog), keeper, token);
					EnterMap();
					reentries++;
				}
				// Before a pull and before a walk: every companion alive, near, and with half its mana. A monster on a
				// member ends the wait at once.
				async Task<bool> GatheredAsync()
				{
					long waited = runtime.NowMillis;
					while (true)
					{
						await session.SynchronizeAsync(token);
						if (world.IsDead || world.MapId != GroupNtcMap || OnTheGroup(Hostiles()) != null) return false;
						if (Others().All(member => member.Online && !member.Dead && member.MapId == world.MapId &&
							(member.MaxMp <= 0 || member.CurrentMp * 100L / member.MaxMp >= 50) && Away(member) <= 12)) return true;
						if (runtime.NowMillis - waited > 600_000)
							throw new InvalidDataException("The companions did not gather inside the camp: " + string.Join(", ", Others().Select(member =>
								$"{member.Name} online {member.Online} dead {member.Dead} map {member.MapId} mp {member.CurrentMp}/{member.MaxMp} at {Away(member):F0} m")));
						await session.AdvanceAsync(TimeSpan.FromMilliseconds(250), token);
					}
				}
				async Task KillAsync(int target)
				{
					int? template = world.Objects.GetValueOrDefault(target)?.TemplateId;
					if (template == GroupNtcGeneral && generalSince == 0) generalSince = session.PacketHistory.Count;
					await session.SendPacketAsync(session.Api.Mark(CompanionFirstMark, target), token);
					pulls++;
					session.TraceDiagnostic("stand-in-pull", new Dictionary<string, object?> { ["target"] = target, ["npc"] = template, ["position"] = session.CurrentPosition });
					bool dead;
					try { dead = await combat.TryKillAsync(target, token) || GoneOrKilled(target); }
					catch (NaturalCombatApproachBlockedException blocked)
					{
						dead = GoneOrKilled(target);
						session.TraceDiagnostic("stand-in-pull-blocked", new Dictionary<string, object?> { ["target"] = target, ["deadOrGone"] = dead, ["reason"] = blocked.Message });
					}
					if (!dead)
					{
						refused[target] = refused.GetValueOrDefault(target) + 1;
						await session.AdvanceAsync(TimeSpan.FromSeconds(1), token);
						return;
					}
					killed.Add(target);
					if (template is int kind && killedKinds.Add(kind) && (CompanionNtcNamed.Contains(kind) || kind is GroupNtcGate or GroupNtcGeneral))
						fallenKinds.Add(new { npc = kind, atMillis = runtime.NowMillis - began });
					// The loot is free for all: the corpse is the stand-in's to open at once.
					for (int wait = 0; wait < 6 && !world.IsDead && world.LootStatuses.TryGetValue(target, out byte right) && right == 0; wait++)
					{
						if (await looter.StepAsync(token)) { corpsesOpened++; break; }
						await session.AdvanceAsync(TimeSpan.FromMilliseconds(250), token);
					}
				}
				async Task WalkStretchAsync(IReadOnlyList<BotPosition> stretchRoute, float stretch)
				{
					float walked = 0;
					foreach (var point in stretchRoute)
						while (NaturalServicePolicy.Distance(session.CurrentPosition, point) > .5f)
						{
							var at = session.CurrentPosition;
							float fraction = Math.Min(1, 6 / NaturalServicePolicy.Distance(at, point));
							await navigator.MoveAsync([at, new(at.X + (point.X - at.X) * fraction, at.Y + (point.Y - at.Y) * fraction,
								at.Z + (point.Z - at.Z) * fraction, point.Heading)], token);
							await session.SynchronizeAsync(token);
							float moved = NaturalServicePolicy.Distance(session.CurrentPosition, at);
							walked += moved;
							if (world.IsDead || moved < .1f || walked >= stretch || OnTheGroup(Hostiles()) != null) return;
						}
				}
				// To a place inside the camp, fighting what is in the way, until done says so. kind is the monster fought at the
				// place itself, when there is one.
				async Task AdvanceAsync(string what, BotPosition lair, int kind, Func<bool> done)
				{
					session.BeginStep("stand-in", "to-" + what);
					int idle = 0;
					while (!done())
					{
						await session.SynchronizeAsync(token);
						if (world.IsDead) { await combat.RestAsync(token); continue; }
						if (world.MapId != GroupNtcMap) { await BackInAsync(); continue; }
						int? pick = OnTheGroup(Hostiles());
						IReadOnlyList<BotPosition> way = [];
						if (pick == null)
						{
							await combat.RestAsync(token);
							if (!await GatheredAsync()) { await session.AdvanceAsync(TimeSpan.FromMilliseconds(250), token); continue; }
							var hostiles = Hostiles();
							if (NaturalServicePolicy.Distance(session.CurrentPosition, lair) > 3)
							{
								way = geometry.FindJourneyPath(world.MapId!.Value, session.CurrentPosition, lair);
								// The gate stands in its gateway, where the mesh has a seam: the way to within reach of it will do.
								if (way.Count == 0) way = geometry.FindInteractionPath(world.MapId!.Value, session.CurrentPosition, lair);
								if (way.Count == 0) throw new InvalidDataException($"No checked way inside the camp from {session.CurrentPosition} to {lair}.");
								// The stretch looked at is longer than the stretch walked, so no circle is entered unseen.
								float ahead = 0;
								BotPosition from = session.CurrentPosition;
								foreach (var point in way)
								{
									pick = hostiles.Where(hostile => refused.GetValueOrDefault(hostile.Seen.ObjectId) < 3 && FromSegment(from, point, hostile.Seen.Position) <= hostile.Reach)
										.OrderBy(hostile => NaturalServicePolicy.Distance(session.CurrentPosition, hostile.Seen.Position)).Select(hostile => (int?)hostile.Seen.ObjectId).FirstOrDefault();
									if (pick != null) break;
									ahead += NaturalServicePolicy.Distance(from, point); from = point;
									if (ahead >= 16) break;
								}
							}
							// At the place the named monster itself is the one to fight, wherever in sight it stands.
							else pick = world.Objects.Values.Where(known => known.Kind == BotKnownObjectKind.Npc && known.TemplateId == kind && kind != 0 &&
									!known.IsCorpse && !killed.Contains(known.ObjectId))
								.OrderBy(known => NaturalServicePolicy.Distance(session.CurrentPosition, known.Position)).Select(known => (int?)known.ObjectId).FirstOrDefault();
						}
						if (pick is int target) { idle = 0; await KillAsync(target); continue; }
						if (way.Count == 0)
						{
							if (++idle > 480) throw new InvalidDataException($"{what}: the stand-in stands at the place, npc {kind} is not there to fight, and it is not done.");
							await session.AdvanceAsync(TimeSpan.FromMilliseconds(250), token);
							continue;
						}
						idle = 0;
						await WalkStretchAsync(way, 8);
					}
					session.TraceDiagnostic("stand-in-step", new Dictionary<string, object?>
					{ ["done"] = what, ["npc"] = kind, ["pulls"] = pulls, ["deaths"] = combat.ReviveCount, ["atMillis"] = runtime.NowMillis - began, ["position"] = session.CurrentPosition });
				}
				await combat.BuffOurselfAsync(NaturalHelpTrigger.PrePull, token);
				foreach (int named in CompanionNtcNamed)
					await AdvanceAsync($"npc-{named}", Lair(named), named, () => killedKinds.Contains(named));
				await AdvanceAsync("the-gate", Lair(GroupNtcGate), GroupNtcGate, () => killedKinds.Contains(GroupNtcGate));
				await AdvanceAsync("inside-the-gateway", GroupNtcInside, 0, () => NaturalServicePolicy.Distance(session.CurrentPosition, GroupNtcInside) <= 3);
				await AdvanceAsync("the-general", Lair(GroupNtcGeneral), GroupNtcGeneral, () => killedKinds.Contains(GroupNtcGeneral));
				await session.SynchronizeAsync(token);
				var toTheGeneral = new { fallen = fallenKinds, pulls, kills = killed.Count, corpsesOpened, itemsTaken = looter.Collected,
					standInDeaths = combat.ReviveCount, reentries, elapsedMillis = runtime.NowMillis - began,
					generalStruckBy = Others().Where(member => session.PacketHistory.Skip(generalSince).Any(packet => world.Objects.Values
						.Where(known => known.TemplateId == GroupNtcGeneral).Any(boss => runtime.IncomingAttacker(packet, boss.ObjectId) == member.ObjectId)))
						.Select(member => member.Name).ToArray(),
					companions = Others().Select(member => new { member.Name, member.Online, member.Dead, member.MapId, metres = Away(member) }).ToArray() };
				session.TraceDiagnostic("stand-in-general", new Dictionary<string, object?> { ["general"] = toTheGeneral });
				general = toTheGeneral;
			}
		}
		var support = (world.VisibleEffects ?? []).Where(effect => Others().Any(member => member.ObjectId == effect.EffectorId))
			.Select(effect => new { effect.SkillId, effect.EffectorId }).ToArray();
		runtime.AssertClean();
		var receipt = new { verified = true, CharacterId = session.CharacterId, until = options.CompanionUntil, asked, companions = joined,
			leader = world.GroupLeaderId, group = world.GroupId, lootRules = world.GroupLootRules, list, walks, marks, loots, inside, general, support,
			elapsedMillis = runtime.NowMillis - started, map = world.MapId, position = session.CurrentPosition };

		// C9: the end, in one of the three ways a player's companions end. Sent away, or after the player left its
		// group, the server knows no companion of this player once each has left the game. A logout is the last thing
		// the stand-in does; what is left after it is the host's to check.
		string end = options.CompanionEnd ?? "dismiss";
		if (end is not ("dismiss" or "leave" or "logout")) throw new InvalidDataException($"The stand-in ends by dismiss, leave or logout, not by '{end}'.");
		session.BeginStep("stand-in", "the-end-by-" + end);
		string[] farewell = [], listAfter = [];
		long endedAt = runtime.NowMillis;
		long? goneAfterMillis = null;
		if (end != "logout")
		{
			if (end == "dismiss") farewell = await SayAsync(".companion dismiss");
			else await session.SendPacketAsync(session.Api.LeaveGroup(), token);
			await WaitAsync(() => world.GroupId == null, "the group did not end", 60);
			while (goneAfterMillis == null)
			{
				listAfter = await SayAsync(".companion list");
				if (listAfter.Any(line => line.Contains("You have no companion.", StringComparison.Ordinal))) goneAfterMillis = runtime.NowMillis - endedAt;
				else if (runtime.NowMillis - endedAt > 90_000) throw new InvalidDataException("The server still knows companions of the stand-in: " + string.Join(" / ", listAfter));
				else await session.AdvanceAsync(TimeSpan.FromSeconds(1), token);
			}
		}
		File.WriteAllText(Path.Combine(Path.GetDirectoryName(session.CombatTracePath!)!, "companion-completion.json"),
			JsonSerializer.Serialize(new { receipt.verified, receipt.CharacterId, receipt.until, receipt.asked, receipt.companions, receipt.leader,
				receipt.group, receipt.lootRules, receipt.list, receipt.walks, receipt.marks, receipt.loots, receipt.inside, receipt.general, receipt.support, end, farewell, goneAfterMillis, listAfter, receipt.elapsedMillis, receipt.map, receipt.position },
				new JsonSerializerOptions { WriteIndented = true }));
		session.TraceDiagnostic("stand-in-reached", new Dictionary<string, object?>
		{ ["receipt"] = receipt, ["end"] = end, ["farewell"] = farewell, ["goneAfterMillis"] = goneAfterMillis, ["listAfter"] = listAfter });
		if (end == "logout") await session.QuitAsync(token);
	}
}
