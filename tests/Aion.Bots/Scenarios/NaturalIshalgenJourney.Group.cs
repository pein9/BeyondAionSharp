using System.Text.Json;
using Aion.Bots.Navigation;
using Aion.Bots.Navigation.NavMesh;
using Aion.Bots.Protocol;
using Aion.Bots.Reflexes;
using Aion.Bots.Scenarios.Classes;
using Aion.Bots.World;
using Aion.GameServer.Model;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.Bots.Scenarios;

public sealed partial class NaturalIshalgenJourney
{
	private async Task RunGroupCoinAsync(CancellationToken token)
	{
		BotWorldModel world = session.Api.World;
		string[] roster = options.GroupRoster!;
		string leaderName = options.GroupLeader!;
		string name = world.Objects[session.CharacterId].Name!;
		if (roster.Length is < 2 or > 6 || roster.Distinct().Count() != roster.Length || !roster.Contains(name) || !roster.Contains(leaderName))
			throw new InvalidDataException("The group needs two to six distinct existing characters, including self and its leader.");
		bool leads = name == leaderName;
		if (leads && options.Role == "healer") throw new InvalidDataException("This quest's bot leader must be a fighter.");
		if (world.MapId != NaturalAbyssEntry.Morheim) throw new InvalidDataException("The group starts in Morheim.");
		var geometry = runtime.CreateGeometry();
		QuestRunPlan quest = NaturalAltgardContract.LoadMusterPlans().Values.Single();
		QuestRunOperation[] kills = [.. QuestRunBook.Build(quest).Operations.Where(op => op.Kind == QuestRunOperationKind.Kill)];
		var navigator = new NaturalJourneyNavigator(session, BotNavigationGraphFactory.Build(runtime.Data, [quest.Id], geometry),
			geometry, runtime, stopOnDeath: false) { AvoidHostileAggro = true,
			Planner = leads ? BotTravelPlanner.For(world.MapId.Value, geometry, runtime.Data) : null };
		var combat = new NaturalJourneyCombat(session, navigator, runtime, geometry, stopOnDeath: false,
			conservativeRangedHold: false, NaturalMauPolicyParameters.Baseline, ClassLine) { ApproachMapId = world.MapId.Value };
		var following = new NaturalGroupFollowDriver(session, runtime);
		int repeatBefore = Math.Max(world.Quests.GetValueOrDefault(quest.Id)?.CompleteCount ?? 0,
			world.CompletedQuests.GetValueOrDefault(quest.Id)?.CompleteCount ?? 0);
		long started = runtime.NowMillis;
		int chatCursor = 0;
		string command = "", lastReport = "";
		int? assist = null;
		var progress = new Dictionary<int, (int Status, int[] Counts)>();
		var failedTargets = new Dictionary<int, int>();
		using var healer = options.Role == "healer" ? new NaturalGroupHealDriver(session, runtime, combat.ClassProfile) : null;
		var loot = new NaturalGroupLootDriver(session, runtime, item => world.Quests.Values.Where(entry => entry.Status == 3)
			.SelectMany(entry => runtime.Data.Quests.GetQuestById(entry.QuestId)?.GetCollectItems()?.GetCollectItem() ?? [])
			.Any(required => required.GetItemId() == item && ItemCount(world, item) < required.GetCount()));
		var rewardItems = new Dictionary<int, long>();

		async Task SayAsync(string value) => await session.SendPacketAsync(session.Api.Say("group-coin:" + value, 5), token);
		int Status() => world.Quests.GetValueOrDefault(quest.Id)?.Status ?? 0;
		bool GoneOrKilled(int target) => !world.Objects.TryGetValue(target, out var seen) || seen.IsCorpse ||
			session.PacketHistory.TakeLast(4000).Any(packet => BotAttackStatus.CarriesHp(packet) &&
				packet.Get<int>("objectId") == target && packet.Get<byte>("hpOrMp") == 0);
		int[] Counts() => kills.Select(kill => Status() is 4 or 5 ? kill.Count : Status() == 3
			? NaturalQuestProgress.KillCount(world.Quests[quest.Id], quest.Steps.Single(step => step.Kind == "kill" && step.Sequence == kill.Sequence)
				.Data.GetProperty("var").GetInt32(), kill.Count) : 0).ToArray();
		async Task ObserveAsync()
		{
			await session.SynchronizeAsync(token);
			foreach (var packet in session.PacketHistory.Skip(chatCursor).Where(packet => packet.PacketType == typeof(SM_MESSAGE) && packet.Get<byte>("chatType") == 5))
			{
				int sender = packet.Get<int>("senderObjectId");
				if (!world.GroupMembers.ContainsKey(sender)) continue;
				string[] words = packet.Get<string>("message").Split(':');
				if (words.Length < 3 || words[0] != "group-coin") continue;
				if (words[1] == "progress" && words.Length == 4 && int.TryParse(words[2], out int status))
				{
					int[] counts = words[3].Split(',').Select(value => int.TryParse(value, out int count) ? count : -1).ToArray();
					if (counts.Length == kills.Length && counts.All(count => count >= 0)) progress[sender] = (status, counts);
				}
				else if (sender == world.GroupLeaderId && words[1] == "command") command = words[2];
				else if (sender == world.GroupLeaderId && words[1] == "assist" && int.TryParse(words[2], out int target)) assist = target;
			}
			chatCursor = session.PacketHistory.Count;
			progress[session.CharacterId] = (Status(), Counts());
			string report = $"progress:{Status()}:{string.Join(',', Counts())}";
			if (world.GroupId != null && report != lastReport)
			{
				await SayAsync(report); lastReport = report;
				session.TraceDiagnostic("group-quest-progress", new Dictionary<string, object?> { ["quest"] = quest.Id, ["status"] = Status(), ["counts"] = Counts() });
			}
		}
		async Task PauseAsync()
		{
			await session.AdvanceAsync(TimeSpan.FromMilliseconds(250), token);
			await ObserveAsync();
		}
		async Task WaitAsync(Func<bool> ready, string reason, int seconds = 120)
		{
			long began = runtime.NowMillis;
			while (!ready())
			{
				if (runtime.NowMillis - began > seconds * 1000L) throw new IOException($"Group wait timed out: {reason}.");
				await PauseAsync();
			}
		}
		async Task WaitForFollowersAsync(CancellationToken ct)
		{
			if (!leads || world.GroupId == null) return;
			await ObserveAsync();
			// Finish an existing fight before recovering a dead/out-of-range group. Never begin another pull without them.
			if (combat.InCombat && world.GroupMembers.Values.Any(member => member.Dead)) return;
			bool clericDead = world.GroupMembers.Values.Any(member => member.PlayerClass == (byte)PlayerClass.CLERIC && member.Dead);
			bool atBind = world.ObeliskBindPoint is { } point && Distance(session.CurrentPosition, point.Position) < 30;
			bool memberAtBind = world.ObeliskBindPoint is { } common && world.GroupMembers.Values.Any(member =>
				Distance(member.Position, common.Position) < 30 && Distance(session.CurrentPosition, member.Position) > 150);
			if (clericDead || memberAtBind || atBind && world.GroupMembers.Values.Any(member => Distance(session.CurrentPosition, member.Position) > 150))
			{
				await SayAsync("command:regroup");
				if (!atBind) await ReturnToBindAsync();
				await WaitAsync(() => world.GroupMembers.Values.All(member => member.Online && !member.Dead && Distance(session.CurrentPosition, member.Position) <= 12), "group recovery at the common bind", 180);
				await SayAsync("command:hunt");
			}
			bool Together() => world.GroupMembers.Values.Where(member => member.ObjectId != session.CharacterId).All(member =>
				member.Online && !member.Dead && member.MapId == world.MapId && Distance(session.CurrentPosition,
					world.Objects.GetValueOrDefault(member.ObjectId)?.Position ?? member.Position) <= 12 &&
				(combat.InCombat || member.MaxMp <= 0 || member.CurrentMp * 100L / member.MaxMp >= 50));
			if (!Together())
			{
				var at = session.CurrentPosition;
				// An interrupted movement plan must announce its stop before waiting for stationary followers.
				await session.SendPacketAsync(session.Api.MoveTo(new(at.X, at.Y, at.Z, at.Heading, 0)), ct);
				session.TraceDiagnostic("group-regroup", new Dictionary<string, object?>
				{ ["position"] = at, ["members"] = world.GroupMembers.Values.Select(member => new { member.Name, member.Position, member.HpPercent }).ToArray() });
			}
			await WaitAsync(Together, "followers to regroup");
		}
		navigator.BeforeMovementAsync = WaitForFollowersAsync;
		combat.WaitForGroupResurrectionAsync = async ct =>
		{
			if (healer != null) return false;
			long began = runtime.NowMillis;
			int death = session.PacketHistory.Count - 1;
			while (death >= 0 && session.PacketHistory[death].PacketType != typeof(SM_DIE)) death--;
			session.TraceDiagnostic("group-wait-for-resurrection", new Dictionary<string, object?> { ["position"] = session.CurrentPosition });
			while (world.IsDead && runtime.NowMillis - began < 60_000)
			{
				var cleric = world.GroupMembers.Values.FirstOrDefault(member => member.PlayerClass == (byte)PlayerClass.CLERIC && member.Online && !member.Dead);
				if (cleric == null) return false;
				var offer = session.PacketHistory.Skip(death + 1).LastOrDefault(packet => packet.PacketType == typeof(SM_RESURRECT) && packet.Get<string>("casterName") == cleric.Name);
				if (offer != null)
				{
					await session.SendPacketAsync(session.Api.Revive(BotReviveType.Skill), ct);
					await session.SynchronizeAsync(ct);
					if (!world.IsDead) { following.Reset(); return true; }
				}
				await session.AdvanceAsync(TimeSpan.FromMilliseconds(500), ct);
				await session.SynchronizeAsync(ct);
			}
			session.TraceDiagnostic("group-resurrection-fallback", new Dictionary<string, object?> { ["reason"] = "No usable Cleric offer within 60 seconds; revive at the common bind." });
			return false;
		};
		async Task ReturnToBindAsync()
		{
			if (world.ObeliskBindPoint is { } point && Distance(session.CurrentPosition, point.Position) < 20) { following.Reset(); return; }
			const ushort skill = 243;
			if (!world.Skills.ContainsKey(skill) || session.Api.Timing.TimeUntilCast(skill) > TimeSpan.Zero) throw new IOException("Group recovery needs Return, but it is unavailable.");
			await session.SendPacketAsync(session.Api.Target(session.CharacterId), token);
			await session.SendPacketAsync(session.Api.Cast(runtime.CreateSpellCast(world, session.CurrentPosition, skill, checked((byte)world.Skills[skill].Level), session.CharacterId)), token);
			var start = await BotCastProtocol.WaitForStartAsync((predicate, ct) => session.WaitForPacketAsync(packet => predicate(packet) || BotCastProtocol.IsStartRejection(packet), ct), session.CharacterId, skill, token);
			if (start.PacketType == typeof(SM_SYSTEM_MESSAGE)) { session.Api.Timing.RecordCastCancelled(); throw new IOException("Group Return was refused."); }
			await session.AdvanceAsync(TimeSpan.FromMilliseconds(start.Get<ushort>("castDuration") + 1), token);
			var result = await BotCastProtocol.WaitForCompletionAsync(session.WaitForPacketAsync, session.CharacterId, skill, token);
			if (result.PacketType != typeof(SM_CASTSPELL_RESULT)) throw new IOException("Group Return was interrupted.");
			await session.AdvanceAsync(BotCastProtocol.RecoveryDelay(result), token);
			await session.SynchronizeAsync(token);
			session.AcceptTeleportPosition(); following.Reset();
		}

		async Task WalkAsync(BotPosition destination, float within = 4)
		{
			var approach = new NaturalApproachProgress();
			for (int attempt = 0; Distance(session.CurrentPosition, destination) > within && approach.CanRetry(attempt) && approach.StalledAttempts < 3; attempt++)
			{
				var from = session.CurrentPosition;
				var route = await navigator.FindRouteAsync(from, destination, token);
				if (route.Count == 0) throw new IOException($"No checked group-leader route to {destination}: {navigator.LastRouteDiagnostic}");
				bool replan = false;
				// Bound each leader movement so casting followers can catch up before the next segment.
				foreach (var point in route)
				{
					while (Distance(session.CurrentPosition, point) > .5f)
					{
						var at = session.CurrentPosition;
						float fraction = Math.Min(1, 6 / Distance(at, point));
						BotPosition next = new(at.X + (point.X - at.X) * fraction, at.Y + (point.Y - at.Y) * fraction,
							at.Z + (point.Z - at.Z) * fraction, point.Heading);
						if (!navigator.IsSegmentSafe([at, next], null, destination))
						{
							session.TraceDiagnostic("group-leader-replan", new Dictionary<string, object?> { ["position"] = at, ["destination"] = destination });
							replan = true; break;
						}
						await navigator.MoveAsync([at, next], token);
						await ObserveAsync();
						if (world.IsDead) { await combat.RestAsync(token); break; }
						if (Distance(session.CurrentPosition, at) < .1f) break;
					}
					if (Distance(session.CurrentPosition, destination) <= within) return;
					if (replan) break;
				}
				approach.Observe(Distance(from, session.CurrentPosition), false);
			}
			if (Distance(session.CurrentPosition, destination) > within) throw new IOException("The group leader could not reach its destination.");
		}
		BotPosition Spawn(int template) => runtime.Data.SpawnsDh.GetSpawnsByWorldId(world.MapId!.Value)
			.Where(group => group.GetNpcId() == template).SelectMany(group => group.GetSpawnTemplates())
			.Select(spot => new BotPosition(spot.GetX(), spot.GetY(), spot.GetZ(), spot.GetHeading()))
			.OrderBy(at => Distance(session.CurrentPosition, at)).First();
		async Task<int> NpcAsync(int template)
		{
			BotPosition at = Spawn(template);
			await WalkAsync(at);
			await session.SynchronizeAsync(token);
			return world.Objects.Values.Where(known => known.TemplateId == template && !known.IsCorpse)
				.OrderBy(known => Distance(session.CurrentPosition, known.Position)).First().ObjectId;
		}

		// Preflight is before group travel: validate the common binding from the client, correcting it normally if needed.
		BotPosition obelisk = Spawn(700231);
		if (world.ObeliskBindPoint is not { } bind || bind.MapId != world.MapId || Distance(bind.Position, obelisk) >= NaturalServicePolicy.SameObeliskRadius)
		{
			int objectId = await NpcAsync(700231);
			var bound = await new NaturalServiceSteps(session).BindAsync(objectId, obelisk, world.MapId.Value,
				runtime.Data.BindPointDataDh.GetBindPointTemplate(700231).GetPrice(), 5, token);
			if (!bound.IsDone) throw new IOException(bound.Reason);
		}
		session.TraceDiagnostic("group-start", new Dictionary<string, object?>
		{ ["name"] = name, ["position"] = session.CurrentPosition, ["map"] = world.MapId, ["bind"] = world.ObeliskBindPoint, ["repeatBefore"] = repeatBefore });
		if (!leads && world.Objects.Values.FirstOrDefault(known => known.Name == leaderName) is { } rendezvous &&
			Distance(session.CurrentPosition, rendezvous.Position) > 20) await WalkAsync(rendezvous.Position, 3);
		if (leads)
		{
			foreach (string invited in roster.Where(member => member != name))
			{
				await WaitAsync(() => world.Objects.Values.Any(known => known.Name == invited && Distance(session.CurrentPosition, known.Position) <= 20), $"{invited} at the rendezvous");
				await session.SendPacketAsync(session.Api.InviteToGroup(invited), token);
				await WaitAsync(() => world.GroupMembers.Values.Any(member => member.Name == invited && member.Online), $"{invited} to accept");
			}
		}
		else
		{
			await WaitAsync(() => world.Question?.Code == 60000 && world.Question.Parameters.Contains(leaderName), "leader's invitation");
			await session.SendPacketAsync(session.Api.Answer(1), token);
		}
		await WaitAsync(() => world.GroupMembers.Count == roster.Length && world.GroupMembers.Values.All(member => roster.Contains(member.Name!)) &&
			world.GroupMembers.GetValueOrDefault(world.GroupLeaderId ?? 0)?.Name == leaderName, "the complete roster and leader");
		session.TraceDiagnostic("group-formed", new Dictionary<string, object?> { ["leader"] = world.GroupLeaderId, ["members"] = world.GroupMembers.Values.Select(member => member.Name).ToArray() });
		if (options.GroupLoot)
		{
			if (leads) await session.SendPacketAsync(session.Api.SetGroupLoot(1), token);
			await WaitAsync(() => world.GroupLootRules.FirstOrDefault(-1) == 1, "round-robin loot rules");
		}
		await combat.BuffOurselfAsync(NaturalHelpTrigger.AfterRelog, token);

		bool Everybody(Func<(int Status, int[] Counts), bool> predicate) => world.GroupMembers.Keys.All(id => progress.TryGetValue(id, out var entry) && predicate(entry));
		bool OwnKillsDone() => Counts().Select((count, index) => count >= kills[index].Count).All(done => done);
		async Task DialogQuestAsync(bool finish)
		{
			var giver = world.Objects.Values.Where(known => known.TemplateId == 204360 && !known.IsCorpse && Distance(session.CurrentPosition, known.Position) <= 15)
				.OrderBy(known => Distance(session.CurrentPosition, known.Position)).FirstOrDefault();
			if (giver == null) return;
			// Following stops a little behind F1. Each member still needs its own short NPC interaction approach.
			if (Distance(session.CurrentPosition, giver.Position) > 4)
			{
				var path = geometry.FindInteractionPath(world.MapId!.Value, session.CurrentPosition, giver.Position);
				if (path.Count == 0) return;
				await navigator.MoveAsync(path, token);
			}
			session.BeginStep(finish ? "group-quest-turn-in" : "group-quest-accept", $"q{quest.Id}");
			if (finish)
			{
				var before = world.Inventory.Values.GroupBy(item => item.ItemId).ToDictionary(group => group.Key, group => group.Sum(item => item.Count));
				await FinishStandardQuestAsync(session, giver.ObjectId, quest.Id, token);
				await session.SynchronizeAsync(token);
				foreach (var owned in world.Inventory.Values.GroupBy(item => item.ItemId))
					if (owned.Sum(item => item.Count) > before.GetValueOrDefault(owned.Key)) rewardItems[owned.Key] = owned.Sum(item => item.Count) - before.GetValueOrDefault(owned.Key);
			}
			else await session.StartQuestAsync(giver.ObjectId, quest.Id, token);
			await ObserveAsync();
		}

		if (!leads)
		{
			long? leaderMissingAt = null;
			bool returnedForRegroup = false;
			while (command != "done")
			{
				await ObserveAsync();
				if (world.GroupLeaderId == null || !world.GroupMembers.TryGetValue(world.GroupLeaderId.Value, out var currentLeader) || !currentLeader.Online ||
					world.GroupMembers.Values.All(member => member.Name != leaderName))
				{
					leaderMissingAt ??= runtime.NowMillis;
					if (runtime.NowMillis - leaderMissingAt > 120_000) throw new IOException("F1 is unavailable; the group quest cannot continue.");
				}
				else leaderMissingAt = null;
				if (world.IsDead) { await combat.RestAsync(token); continue; }
				if (command == "regroup" && !returnedForRegroup) { await ReturnToBindAsync(); returnedForRegroup = true; }
				if (command != "regroup") returnedForRegroup = false;
				if (command == "accept" && Status() is not (3 or 4)) await DialogQuestAsync(false);
				else if (command == "turn-in" && Status() is 3 or 4 && OwnKillsDone()) await DialogQuestAsync(true);
				if (healer != null) await healer.StepAsync(token);
				else if (assist is int target && world.Objects.TryGetValue(target, out var enemy) && !enemy.IsCorpse &&
					Distance(session.CurrentPosition, enemy.Position) <= 35 && session.PacketHistory.TakeLast(400)
						.Any(packet => runtime.IncomingAttacker(packet, target) == world.GroupLeaderId))
				{
					try { await combat.TryKillAsync(target, token); }
					catch (NaturalCombatApproachBlockedException blocked)
					{
						if (GoneOrKilled(target)) combat.NoteTargetTaken();
						else session.TraceDiagnostic("group-assist-blocked", new Dictionary<string, object?> { ["target"] = target, ["reason"] = blocked.Message });
					}
					assist = null;
					await combat.RestAsync(token);
				}
				if (options.GroupLoot) await loot.StepAsync(token);
				await following.StepAsync(token);
				await PauseAsync();
			}
		}
		else
		{
			await NpcAsync(204360);
			await SayAsync("command:accept");
			await DialogQuestAsync(false);
			await WaitAsync(() => Everybody(entry => entry.Status == 3), "every member to accept Q2318");
			await combat.BuffOurselfAsync(NaturalHelpTrigger.PrePull, token);
			foreach (var kill in kills)
			{
				int index = Array.IndexOf(kills, kill);
				while (!Everybody(entry => entry.Counts[index] >= kill.Count))
				{
					await combat.RestAsync(token);
					int[] kinds = [.. kill.Npcs!.Select(npc => npc.Id).Where(id => runtime.Data.SpawnsDh.GetSpawnsByWorldId(world.MapId!.Value).Any(group => group.GetNpcId() == id))];
					int kind = kinds.OrderBy(id => Distance(session.CurrentPosition, Spawn(id))).First();
					var candidates = runtime.Data.SpawnsDh.GetSpawnsByWorldId(world.MapId!.Value).Where(group => group.GetNpcId() == kind)
						.SelectMany(group => group.GetSpawnTemplates()).Select(spot => new BotPosition(spot.GetX(), spot.GetY(), spot.GetZ(), spot.GetHeading()))
						.OrderBy(at => Distance(session.CurrentPosition, at)).ToArray();
					int target = 0;
					foreach (var at in candidates)
					{
						// Once a monster comes into sight, its class combat driver owns the checked approach and pull.
						await WalkAsync(at, 80);
						target = world.Objects.Values.Where(known => known.TemplateId == kind && !known.IsCorpse && !navigator.UnavailableObjects.Contains(known.ObjectId))
							.OrderBy(known => Distance(session.CurrentPosition, known.Position)).FirstOrDefault()?.ObjectId ?? 0;
						if (target != 0) break;
					}
					if (target == 0) throw new IOException($"No living quest target of kind {kind} was reached.");
					await WaitForFollowersAsync(token);
					await SayAsync($"assist:{target}");
					try
					{
						if (await combat.TryKillAsync(target, token) || GoneOrKilled(target)) navigator.UnavailableObjects.Add(target);
						else
						{
							failedTargets[target] = failedTargets.GetValueOrDefault(target) + 1;
							if (failedTargets[target] >= 2) { navigator.UnavailableObjects.Add(target); navigator.LeftForAnother.Add(target); }
						}
					}
					catch (NaturalCombatApproachBlockedException blocked)
					{
						// A group member may finish the target while F1 is approaching. The journals decide whether more kills are needed.
						navigator.UnavailableObjects.Add(target);
						if (!GoneOrKilled(target)) navigator.LeftForAnother.Add(target);
						session.TraceDiagnostic("group-target-skipped", new Dictionary<string, object?> { ["target"] = target, ["deadOrGone"] = GoneOrKilled(target), ["reason"] = blocked.Message });
					}
					if (options.GroupLoot)
					{
						// Initial rights may belong to any member. Hold nearby while that client opens and closes the corpse.
						for (int wait = 0; wait < 60 && world.Objects.TryGetValue(target, out _); wait++)
						{
							await loot.StepAsync(token);
							await PauseAsync();
						}
					}
					await PauseAsync();
				}
			}
			await NpcAsync(204360);
			await SayAsync("command:turn-in");
			await DialogQuestAsync(true);
			await WaitAsync(() => Everybody(entry => entry.Status == 5), "every member to turn in Q2318");
			await SayAsync("command:done");
			await session.AdvanceAsync(TimeSpan.FromSeconds(2), token);
		}
		await ObserveAsync();
		if (Status() != 5 || world.Quests[quest.Id].CompleteCount != repeatBefore + 1 || rewardItems.Count == 0)
			throw new InvalidDataException("The group member has not completed exactly one new coin quest repeat.");
		runtime.AssertClean();
		var receipt = new { verified = true, CharacterId = session.CharacterId, name, quest = quest.Id,
			leader = world.GroupLeaderId, roster, repeatBefore, repeatAfter = world.Quests[quest.Id].CompleteCount,
			elapsedMillis = runtime.NowMillis - started, position = session.CurrentPosition, bind = world.ObeliskBindPoint,
			deaths = combat.ReviveCount, lootEnabled = options.GroupLoot, lootCollected = loot.Collected, rewardItems };
		File.WriteAllText(Path.Combine(Path.GetDirectoryName(session.CombatTracePath!)!, "group-coin-completion.json"),
			JsonSerializer.Serialize(receipt, new JsonSerializerOptions { WriteIndented = true }));
		session.TraceDiagnostic("group-quest-completed", new Dictionary<string, object?> { ["receipt"] = receipt });
	}
}
