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
	private static readonly string[] CompanionCheckpoints = ["joined", "walk"];

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
		while (world.GroupId != null && world.GroupLeaderId == leader)
		{
			await session.SynchronizeAsync(token);
			if (world.IsDead) { await combat.RestAsync(token); continue; }
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
		session.TraceDiagnostic("companion-leaves", new Dictionary<string, object?>
		{
			["group"] = world.GroupId, ["leaderNow"] = world.GroupLeaderId, ["leaderWas"] = leader, ["elapsedMillis"] = runtime.NowMillis - entered,
			["deaths"] = combat.ReviveCount,
		});
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
		var support = (world.VisibleEffects ?? []).Where(effect => Others().Any(member => member.ObjectId == effect.EffectorId))
			.Select(effect => new { effect.SkillId, effect.EffectorId }).ToArray();
		runtime.AssertClean();
		var receipt = new { verified = true, CharacterId = session.CharacterId, until = options.CompanionUntil, asked, companions = joined,
			leader = world.GroupLeaderId, group = world.GroupId, lootRules = world.GroupLootRules, list, walks, support,
			elapsedMillis = runtime.NowMillis - started, map = world.MapId, position = session.CurrentPosition };

		session.BeginStep("stand-in", "send-them-away");
		string[] farewell = await SayAsync(".companion dismiss");
		await WaitAsync(() => world.GroupId == null, "the group did not end after the dismissal", 60);
		File.WriteAllText(Path.Combine(Path.GetDirectoryName(session.CombatTracePath!)!, "companion-completion.json"),
			JsonSerializer.Serialize(new { receipt.verified, receipt.CharacterId, receipt.until, receipt.asked, receipt.companions, receipt.leader,
				receipt.group, receipt.lootRules, receipt.list, receipt.walks, receipt.support, farewell, receipt.elapsedMillis, receipt.map, receipt.position },
				new JsonSerializerOptions { WriteIndented = true }));
		session.TraceDiagnostic("stand-in-reached", new Dictionary<string, object?> { ["receipt"] = receipt, ["farewell"] = farewell });
	}
}
