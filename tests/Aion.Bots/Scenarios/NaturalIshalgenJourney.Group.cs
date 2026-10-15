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
	/// <summary>The Group NTC leg (docs/natural-group-ntc.md): its Morheim quests in the order they are taken, their
	/// givers, and how far the leg is played so far.</summary>
	private static readonly int[] GroupNtcQuests = [4731, 4733, 4734];
	private const int GroupNtcAegir = 204301, GroupNtcKelmar = 800519, GroupNtcVallack = 800510, GroupNtcMap = 300030000;
	/// <summary>Vallack's dialog choice that sends a member into the camp (portal_template2.xml).</summary>
	private const ushort GroupNtcEnterDialog = 10000;
	private const int GroupNtcGate = 256694, GroupNtcChest = 700415, GroupNtcGeneral = 256693;
	/// <summary>A point just inside the gateway. The fortress is a navmesh island of its own (docs/bot-navigation.md):
	/// a way to an exact point crosses the seam in the gateway, so the leader goes to this point first.</summary>
	private static readonly BotPosition GroupNtcInside = new(346.4f, 350f, 379.1f, 0);
	private static readonly string[] GroupNtcCheckpoints = ["quests", "entrance", "protector", "field-guard", "gate", "chests", "general", "home"];

	/// <summary>
	/// A group plays quests together: the leader (F1) leads and speaks the commands in group chat, the others follow, do
	/// what each must do itself at an NPC, and report their journals. The coin stage plays the fortress's repeatable
	/// quest once (docs/natural-group-healer.md); the NTC stage plays the Group NTC leg; the field stage plays a list of
	/// Morheim quests in the open (M6, docs/natural-morheim-leveling.md).
	/// </summary>
	private async Task RunGroupQuestAsync(CancellationToken token)
	{
		BotWorldModel world = session.Api.World;
		bool ntc = options.GroupStage == "ntc", field = options.GroupStage == "field";
		if (!ntc && !field && options.GroupStage != null) throw new InvalidDataException($"Unknown group stage '{options.GroupStage}'.");
		if (ntc && !GroupNtcCheckpoints.Contains(options.GroupUntil))
			throw new InvalidDataException($"The Group NTC leg is played to one of [{string.Join(", ", GroupNtcCheckpoints)}] so far, not to '{options.GroupUntil}'.");
		string tag = ntc ? "group-ntc" : field ? "group-field" : "group-coin";
		// How far the NTC leg is played: the place of its checkpoint in the list.
		int reach = Array.IndexOf(GroupNtcCheckpoints, options.GroupUntil);
		string[] roster = options.GroupRoster!;
		string leaderName = options.GroupLeader!;
		string name = world.Objects[session.CharacterId].Name!;
		if (roster.Length is < 2 or > 6 || roster.Distinct().Count() != roster.Length || !roster.Contains(name) || !roster.Contains(leaderName))
			throw new InvalidDataException("The group needs two to six distinct existing characters, including self and its leader.");
		bool leads = name == leaderName;
		if (leads && options.Role == "healer") throw new InvalidDataException("This quest's bot leader must be a fighter.");
		if (world.MapId != NaturalAbyssEntry.Morheim) throw new InvalidDataException("The group starts in Morheim.");
		var geometry = runtime.CreateGeometry();
		QuestRunPlan[] quests = ntc ? [.. GroupNtcQuests.Select(id => NaturalAltgardContract.LoadGroupNtcPlans()[id])]
			: field ? [.. options.GroupQuests!.Select(id => NaturalAltgardContract.LoadGroupFieldPlans()[id])]
			: [NaturalAltgardContract.LoadMusterPlans().Values.Single()];
		// What a journal counts for each quest: its kills, then the items it collects.
		var tasks = quests.ToDictionary(plan => plan.Id, plan => QuestRunBook.Build(plan).Operations.Where(op => op.Kind is
			QuestRunOperationKind.Kill or QuestRunOperationKind.CollectQuestDrop or QuestRunOperationKind.UseQuestObject).ToArray());
		// The coin stage's one quest; the NTC stage's first, whose NPCs stand on the starting map.
		QuestRunPlan quest = quests[0];
		QuestRunOperation[] kills = [.. tasks[quest.Id].Where(op => op.Kind == QuestRunOperationKind.Kill)];
		var navigator = new NaturalJourneyNavigator(session, BotNavigationGraphFactory.Build(runtime.Data, [quest.Id], geometry),
			geometry, runtime, stopOnDeath: false) { AvoidHostileAggro = true,
			Planner = leads ? BotTravelPlanner.For(world.MapId.Value, geometry, runtime.Data) : null };
		var combat = new NaturalJourneyCombat(session, navigator, runtime, geometry, stopOnDeath: false,
			conservativeRangedHold: false, NaturalMauPolicyParameters.Baseline, ClassLine) { ApproachMapId = world.MapId.Value };
		var following = new NaturalGroupFollowDriver(session, runtime) { Field = field };
		int repeatBefore = Math.Max(world.Quests.GetValueOrDefault(quest.Id)?.CompleteCount ?? 0,
			world.CompletedQuests.GetValueOrDefault(quest.Id)?.CompleteCount ?? 0);
		long started = runtime.NowMillis;
		int chatCursor = 0;
		string command = "", lastReport = "", lastPlace = "";
		int[] arguments = [];
		// The leader as of this member's last look at the group.
		int? leaderSeen = null;
		// The chest this member was last told to open, so that a turn is taken once.
		int toldChest = 0;
		// The kinds of monster this member saw fall in a fight of its own, for a target no quest of the leg counts
		// (the gate, the General).
		var killedKinds = new HashSet<int>();
		// This member has been inside the camp: outside it again, it goes back in to its leader.
		bool beenInside = false;
		// Where each member says it stands: its map and the instance of it.
		var places = new Dictionary<int, (int Map, int Instance)>();
		int? assist = null;
		var progress = new Dictionary<(int Member, int Quest), (int Status, int[] Counts)>();
		var failedTargets = new Dictionary<int, int>();
		// M6: the members this leader saw die since the group last gathered; the attackers of members this fighter failed to kill.
		var fallenMembers = new HashSet<int>();
		var protectFailures = new Dictionary<int, int>();
		using var healer = options.Role == "healer" ? new NaturalGroupHealDriver(session, runtime, combat.ClassProfile) { CatchUp = field } : null;
		var loot = new NaturalGroupLootDriver(session, runtime, item => world.Quests.Values.Where(entry => entry.Status == 3)
			.SelectMany(entry => runtime.Data.Quests.GetQuestById(entry.QuestId)?.GetCollectItems()?.GetCollectItem() ?? [])
			.Any(required => required.GetItemId() == item && ItemCount(world, item) < required.GetCount()));
		var rewardItems = new Dictionary<int, long>();

		async Task SayAsync(string value) => await session.SendPacketAsync(session.Api.Say(tag + ":" + value, 5), token);
		int Status(int? id = null) => world.Quests.GetValueOrDefault(id ?? quest.Id)?.Status ?? 0;
		bool GoneOrKilled(int target) => !world.Objects.TryGetValue(target, out var seen) || seen.IsCorpse ||
			session.PacketHistory.TakeLast(4000).Any(packet => BotAttackStatus.CarriesHp(packet) &&
				packet.Get<int>("objectId") == target && packet.Get<byte>("hpOrMp") == 0);
		int[] Counts(QuestRunPlan? of = null)
		{
			QuestRunPlan plan = of ?? quest;
			return tasks[plan.Id].Select(task => Status(plan.Id) is 4 or 5 ? task.Count : Status(plan.Id) != 3 ? 0
				: task.Kind != QuestRunOperationKind.Kill ? (int)Math.Min(task.Count, ItemCount(world, task.ItemId))
				: NaturalQuestProgress.KillCount(world.Quests[plan.Id], plan.Steps.Single(step => step.Kind == "kill" && step.Sequence == task.Sequence)
					.Data.GetProperty("var").GetInt32(), task.Count)).ToArray();
		}
		async Task ObserveAsync()
		{
			await session.SynchronizeAsync(token);
			foreach (var packet in session.PacketHistory.Skip(chatCursor).Where(packet => packet.PacketType == typeof(SM_MESSAGE) && packet.Get<byte>("chatType") == 5))
			{
				int sender = packet.Get<int>("senderObjectId");
				// A member that was resting reads F1's words late. F1 may have left by then: it spoke as the leader this
				// member last saw, and is heard as that.
				bool ledWhenItSpoke = sender == world.GroupLeaderId || sender == leaderSeen;
				if (!world.GroupMembers.ContainsKey(sender) && !ledWhenItSpoke) continue;
				string[] words = packet.Get<string>("message").Split(':');
				if (words.Length < 3 || words[0] != tag) continue;
				// One line for the whole journal: quest=status=counts, a bar between quests.
				if (words[1] == "progress" && words.Length == 3)
				{
					foreach (string[] entry in words[2].Split('|').Select(part => part.Split('=')))
					{
						if (entry.Length != 3 || !int.TryParse(entry[0], out int about) || !int.TryParse(entry[1], out int status) ||
							!tasks.TryGetValue(about, out var wanted)) continue;
						int[] counts = entry[2].Length == 0 ? [] : entry[2].Split(',').Select(value => int.TryParse(value, out int count) ? count : -1).ToArray();
						if (counts.Length == wanted.Length && counts.All(count => count >= 0)) progress[(sender, about)] = (status, counts);
					}
				}
				else if (words[1] == "place" && words.Length == 4 && int.TryParse(words[2], out int map) && int.TryParse(words[3], out int instance))
					places[sender] = (map, instance);
				else if (ledWhenItSpoke && words[1] == "command")
				{
					command = words[2];
					arguments = [.. words.Skip(3).Select(value => int.TryParse(value, out int number) ? number : 0)];
				}
				else if (ledWhenItSpoke && words[1] == "assist" && int.TryParse(words[2], out int target)) assist = target;
			}
			chatCursor = session.PacketHistory.Count;
			if (world.GroupLeaderId is int leading) leaderSeen = leading;
			foreach (QuestRunPlan plan in quests) progress[(session.CharacterId, plan.Id)] = (Status(plan.Id), Counts(plan));
			string report = "progress:" + string.Join('|', quests.Select(plan => $"{plan.Id}={Status(plan.Id)}={string.Join(',', Counts(plan))}"));
			if (world.GroupId != null && report != lastReport)
			{
				await SayAsync(report); lastReport = report;
				session.TraceDiagnostic("group-quest-progress", new Dictionary<string, object?>
				{ ["quest"] = quest.Id, ["status"] = Status(), ["counts"] = Counts(), ["journal"] = report });
			}
			if (ntc)
			{
				places[session.CharacterId] = (world.MapId ?? 0, world.InstanceId ?? 0);
				string place = $"place:{world.MapId ?? 0}:{world.InstanceId ?? 0}";
				if (world.GroupId != null && place != lastPlace) { await SayAsync(place); lastPlace = place; }
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
			// Inside the camp F1 gathers the group before a pull and before a walk (RegroupedAsync). In a fight it moves as
			// the fight needs: it stood 88 seconds under the Teleporter's spells waiting for a caster 13 metres off.
			if (combat.InCombat && world.MapId == GroupNtcMap) return;
			// M6: the wait is also asked before each step of a fight's approach; a fight is not left to gather the group.
			if (field && combat.InCombat) return;
			bool clericDead = world.GroupMembers.Values.Any(member => member.PlayerClass == (byte)PlayerClass.CLERIC && member.Dead);
			// The bind is a place on one map. Inside an instance nobody is at it, and recovery there is not played yet.
			bool atBind = world.ObeliskBindPoint is { } point && point.MapId == world.MapId && Distance(session.CurrentPosition, point.Position) < 30;
			// M6: the leader's own entry in the group list keeps the place it had when the group formed (at the bind): it is
			// none of the members these rules look for, or a field group 150 m out would turn back for itself.
			IEnumerable<BotGroupMember> others = field ? world.GroupMembers.Values.Where(member => member.ObjectId != session.CharacterId) : world.GroupMembers.Values;
			// M6: out of sight, a member's place in the group list is the one it had when the group formed (at the bind), so a
			// field group knows a member stands at the bind only when it saw that member die and no longer sees it.
			foreach (BotGroupMember member in others.Where(member => member.Dead)) fallenMembers.Add(member.ObjectId);
			bool RevivedAway(BotGroupMember member) => fallenMembers.Contains(member.ObjectId) && member.Online && !member.Dead &&
				!world.Objects.ContainsKey(member.ObjectId);
			bool memberAtBind = field ? others.Any(RevivedAway) : world.ObeliskBindPoint is { } common && common.MapId == world.MapId && others.Any(member =>
				member.MapId == common.MapId && Distance(member.Position, common.Position) < 30 && Distance(session.CurrentPosition, member.Position) > 150);
			if (world.ObeliskBindPoint?.MapId == world.MapId &&
				(clericDead || memberAtBind || atBind && others.Any(member => Distance(session.CurrentPosition, member.Position) > 150)))
			{
				session.TraceDiagnostic("group-recovery", new Dictionary<string, object?>
				{
					["clericDead"] = clericDead, ["memberAtBind"] = memberAtBind, ["atBind"] = atBind, ["position"] = session.CurrentPosition,
					["members"] = others.Select(member => new { member.ObjectId, member.Name, member.Online, member.Dead, member.MapId, member.Position,
						seen = world.Objects.ContainsKey(member.ObjectId) }).ToArray(),
				});
				await SayAsync("command:regroup");
				if (!atBind) await ReturnToBindAsync();
				await WaitAsync(() => others.All(member => member.Online && !member.Dead && Distance(session.CurrentPosition, member.Position) <= 12), "group recovery at the common bind", 180);
				await SayAsync("command:hunt");
				fallenMembers.Clear();
			}
			// M6: on a slope in the field a follower stands a little below its leader, and a healer a little behind the others:
			// near is 15 m on the flat and 12 m of height.
			bool Near(BotPosition at) => field
				? MathF.Sqrt(MathF.Pow(at.X - session.CurrentPosition.X, 2) + MathF.Pow(at.Y - session.CurrentPosition.Y, 2)) <= 15 &&
					MathF.Abs(at.Z - session.CurrentPosition.Z) <= 12
				: Distance(session.CurrentPosition, at) <= 12;
			// M6: a member that is offline (its bot stopped) is not waited for: the Ranger's stop held the leader at every wait.
			bool Together() => world.GroupMembers.Values.Where(member => member.ObjectId != session.CharacterId && (!field || member.Online)).All(member =>
				member.Online && !member.Dead && member.MapId == world.MapId && Near(world.Objects.GetValueOrDefault(member.ObjectId)?.Position ?? member.Position) &&
				(combat.InCombat || member.MaxMp <= 0 || member.CurrentMp * 100L / member.MaxMp >= 50));
			if (!Together())
			{
				var at = session.CurrentPosition;
				// An interrupted movement plan must announce its stop before waiting for stationary followers.
				await session.SendPacketAsync(session.Api.MoveTo(new(at.X, at.Y, at.Z, at.Heading, 0)), ct);
				session.TraceDiagnostic("group-regroup", new Dictionary<string, object?>
				{ ["position"] = at, ["members"] = world.GroupMembers.Values.Select(member => new { member.Name, member.Position, member.HpPercent }).ToArray() });
			}
			if (field)
			{
				// M6: a leader that only waits lets a pack kill its group (twice by Kimssi's elites): whatever strikes a member
				// while the group gathers is killed first, by all.
				long gathering = runtime.NowMillis;
				bool cameBack = false;
				while (!Together())
				{
					HashSet<int> striking = [.. session.PacketHistory.TakeLast(300)
						.SelectMany(packet => world.GroupMembers.Keys.Select(member => runtime.IncomingAttacker(packet, member))).OfType<int>()];
					int? foe = world.Objects.Values.Where(known => striking.Contains(known.ObjectId) && known.Kind == BotKnownObjectKind.Npc && !known.IsCorpse &&
							!GoneOrKilled(known.ObjectId) && !navigator.UnavailableObjects.Contains(known.ObjectId))
						.OrderBy(known => Distance(session.CurrentPosition, known.Position)).Select(known => (int?)known.ObjectId).FirstOrDefault();
					if (foe is int attacker && !world.IsDead)
					{
						await SayAsync($"assist:{attacker}");
						session.TraceDiagnostic("group-field-defend", new Dictionary<string, object?> { ["target"] = attacker, ["position"] = session.CurrentPosition });
						try { if (await combat.TryKillAsync(attacker, ct) || GoneOrKilled(attacker)) navigator.UnavailableObjects.Add(attacker); }
						catch (NaturalCombatApproachBlockedException) { navigator.UnavailableObjects.Add(attacker); }
						continue;
					}
					if (world.IsDead) { await combat.RestAsync(ct); return; }
					// Members that died stand up at the bind, far behind: the group gathers there. A member only lagging behind
					// (the Cleric healing a fight 90 m back) is not one of them.
					foreach (BotGroupMember member in others.Where(member => member.Dead)) fallenMembers.Add(member.ObjectId);
					if (world.ObeliskBindPoint?.MapId == world.MapId && others.Any(RevivedAway))
					{
						session.TraceDiagnostic("group-field-recovery", new Dictionary<string, object?>
						{
							["position"] = session.CurrentPosition, ["self"] = session.CharacterId,
							["members"] = others.Select(member => new { member.ObjectId, member.Name, member.Online, member.Dead, member.MapId, member.Position,
								seen = world.Objects.ContainsKey(member.ObjectId) }).ToArray(),
						});
						await SayAsync("command:regroup");
						await ReturnToBindAsync();
						await WaitAsync(() => others.All(member => member.Online && !member.Dead && Near(world.Objects.GetValueOrDefault(member.ObjectId)?.Position ?? member.Position)),
							"group recovery at the common bind", 300);
						await SayAsync("command:hunt");
						fallenMembers.Clear();
						gathering = runtime.NowMillis;
						continue;
					}
					// M6: a member in sight and 40 m off at most that stays out of reach for half a minute may be one that cannot
					// come: the leader stands on ground its followers' mesh does not join (a fight left the Templar 16 m from the
					// Gladiator and the Cleric, and every gathering waited its five minutes). The leader goes to it, once a gathering;
					// a member farther off is on its way (or chasing a monster).
					if (!cameBack && runtime.NowMillis - gathering > 30_000 && others.Where(member => member.Online && !member.Dead)
						.Select(member => world.Objects.GetValueOrDefault(member.ObjectId)?.Position).OfType<BotPosition>()
						.Where(at => !Near(at) && Distance(session.CurrentPosition, at) <= 40).OrderBy(at => Distance(session.CurrentPosition, at))
						.Cast<BotPosition?>().FirstOrDefault() is { } apart)
					{
						cameBack = true;
						var back = await navigator.FindRouteAsync(session.CurrentPosition, apart, ct);
						session.TraceDiagnostic("group-field-come-back", new Dictionary<string, object?>
						{ ["position"] = session.CurrentPosition, ["to"] = apart, ["route"] = back.Count });
						if (back.Count > 0) await navigator.MoveAsync(back, ct);
						continue;
					}
					// A member that takes longer than five minutes is not waited for: the group goes on with those near (the Cleric
					// circled 12 m off for three minutes, and the wait ran out three seconds before it arrived).
					if (runtime.NowMillis - gathering > 300_000)
					{
						session.TraceDiagnostic("group-field-not-gathered", new Dictionary<string, object?>
						{
							["position"] = session.CurrentPosition,
							["members"] = others.Select(member => new { member.Name, member.Dead, seen = world.Objects.ContainsKey(member.ObjectId),
								distance = Distance(session.CurrentPosition, world.Objects.GetValueOrDefault(member.ObjectId)?.Position ?? member.Position) }).ToArray(),
						});
						return;
					}
					await PauseAsync();
				}
				return;
			}
			await WaitAsync(Together, "followers to regroup");
		}
		navigator.BeforeMovementAsync = WaitForFollowersAsync;
		// The member changed maps: checked movement and combat are bound to the map the client stands on now.
		void EnterMap()
		{
			geometry = runtime.CreateGeometry();
			// In the open a member keeps out of other monsters' circles on its way to a target. Inside the camp the group
			// fights the packs, and a way around them does not exist: nobody plans one there.
			navigator = new NaturalJourneyNavigator(session, BotNavigationGraphFactory.Build(runtime.Data, [quest.Id], geometry),
				geometry, runtime, stopOnDeath: false) { AvoidHostileAggro = world.MapId != GroupNtcMap,
				Planner = leads ? BotTravelPlanner.For(world.MapId!.Value, geometry, runtime.Data) : null, BeforeMovementAsync = WaitForFollowersAsync };
			combat.EnterMap(navigator, geometry, world.MapId!.Value);
			following.Reset();
		}
		combat.AfterBindRevive = EnterMap;
		combat.AfterInstanceRevive = EnterMap;
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
		// M6: in the field a Return is interrupted by whatever strikes the caster (the leader turning back for its Cleric among
		// Kimssi's elites): the attacker is killed and Return cast again, and after three tries the bind is walked to.
		async Task ReturnToBindAsync()
		{
			if (!field) { await ReturnOnceAsync(); return; }
			for (int attempt = 1; ; attempt++)
			{
				try { await ReturnOnceAsync(); return; }
				catch (IOException interrupted) when (interrupted.Message.StartsWith("Group Return was", StringComparison.Ordinal))
				{
					session.TraceDiagnostic("group-field-return-retry", new Dictionary<string, object?> { ["attempt"] = attempt, ["reason"] = interrupted.Message });
					// Every attacker, until none is left (a rest among them is a death: a rest does not fight back); then Return.
					for (int round = 0; round < 8 && !world.IsDead; round++)
					{
						HashSet<int> striking = [.. session.PacketHistory.TakeLast(300).Select(packet => runtime.IncomingAttacker(packet, session.CharacterId)).OfType<int>()];
						BotKnownObject[] foes = [.. world.Objects.Values.Where(known => striking.Contains(known.ObjectId) && known.Kind == BotKnownObjectKind.Npc && !known.IsCorpse)];
						if (foes.Length == 0) break;
						foreach (BotKnownObject foe in foes)
						{
							try { await combat.TryKillAsync(foe.ObjectId, token); }
							catch (NaturalCombatApproachBlockedException) { }
						}
						await ObserveAsync();
					}
					if (world.IsDead) { await combat.RestAsync(token); following.Reset(); return; }
					if (attempt >= 3 && world.ObeliskBindPoint is { } walkTo && walkTo.MapId == world.MapId)
					{
						await WalkAsync(walkTo.Position, 10);
						following.Reset();
						return;
					}
				}
			}
		}
		async Task ReturnOnceAsync()
		{
			if (world.ObeliskBindPoint is { } point && point.MapId == world.MapId && Distance(session.CurrentPosition, point.Position) < 20) { following.Reset(); return; }
			const ushort skill = 243;
			if (!world.Skills.ContainsKey(skill) || session.Api.Timing.TimeUntilCast(skill) > TimeSpan.Zero)
			{
				// M6: Return is still cooling down from the last recovery: a field group's member walks to the bind instead.
				if (!field || world.ObeliskBindPoint is not { } walkTo || walkTo.MapId != world.MapId)
					throw new IOException("Group recovery needs Return, but it is unavailable.");
				await WalkAsync(walkTo.Position, 10);
				following.Reset();
				return;
			}
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

		// M6: in the open a route that keeps out of every monster's reach can be shut (north-east of Kellan's Cabin, on the way
		// to Kimssi). A group fights its way then: the monster that would wake nearest the way first.
		int? Blocker(BotPosition destination)
		{
			BotPosition from = session.CurrentPosition;
			return world.Objects.Values.Where(known => known.Kind == BotKnownObjectKind.Npc && known.TemplateId is int template && !known.IsCorpse &&
					!navigator.UnavailableObjects.Contains(known.ObjectId) && Distance(from, known.Position) <= 60 &&
					runtime.IsAggressive(runtime.Data.NpcDataDh.GetNpcTemplate(template)))
				.OrderBy(known => Distance(from, known.Position) + Distance(known.Position, destination) - Distance(from, destination))
				.Select(known => (int?)known.ObjectId).FirstOrDefault();
		}
		async Task WalkAsync(BotPosition destination, float within = 4)
		{
			// M6: when nothing in sight shuts the way but the planner still keeps out of every monster's reach (Q2355's named
			// elite stands in its own, among others), a field group takes the way through them and fights what wakes.
			bool keptOut = navigator.AvoidHostileAggro;
			try { await WalkOnAsync(destination, within); }
			finally { navigator.AvoidHostileAggro = keptOut; }
		}
		async Task WalkOnAsync(BotPosition destination, float within)
		{
			var approach = new NaturalApproachProgress();
			int clears = 0;
			for (int attempt = 0; Distance(session.CurrentPosition, destination) > within && approach.CanRetry(attempt) && approach.StalledAttempts < 3; attempt++)
			{
				var from = session.CurrentPosition;
				var route = await navigator.FindRouteAsync(from, destination, token);
				if (route.Count == 0 && field && healer == null && clears < 12 && Blocker(destination) is int blocker)
				{
					clears++;
					await WaitForFollowersAsync(token);
					await SayAsync($"assist:{blocker}");
					session.TraceDiagnostic("group-field-clear-way", new Dictionary<string, object?>
					{ ["target"] = blocker, ["template"] = world.Objects.GetValueOrDefault(blocker)?.TemplateId, ["destination"] = destination, ["clears"] = clears });
					try
					{
						if (await combat.TryKillAsync(blocker, token) || GoneOrKilled(blocker)) navigator.UnavailableObjects.Add(blocker);
						else { navigator.UnavailableObjects.Add(blocker); navigator.LeftForAnother.Add(blocker); }
					}
					catch (NaturalCombatApproachBlockedException) { navigator.UnavailableObjects.Add(blocker); }
					await combat.RestAsync(token);
					attempt--;
					continue;
				}
				if (route.Count == 0 && field && navigator.AvoidHostileAggro)
				{
					navigator.AvoidHostileAggro = false;
					session.TraceDiagnostic("group-field-through", new Dictionary<string, object?> { ["destination"] = destination, ["reason"] = navigator.LastRouteDiagnostic });
					route = await navigator.FindRouteAsync(from, destination, token);
				}
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
						// M6: a straight stretch of the route can pass over a dip (east of Kellan's Cabin, 9 m): the step stands on
						// the walkable mesh under it, not in the air where the followers' trail could not reach it. Only the mesh the
						// leader stands on, the ground its followers walk: at a cliff's edge the ground below is the foot of the drop
						// (north of Kimssi, 8 m down), and static ground off the mesh left the leader 20 m from a group that had no way
						// to it, nor it to them.
						if (field && world.MapId is int ground && geometry.SnapToGround(ground, next) is { } under && under.Z <= next.Z + 2 &&
							geometry.OnSameIsland(ground, at, under))
							next = under with { Heading = point.Heading };
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
		// The field stage binds where its quests are (M6: Kellan's Cabin, the garrison, Slag Bulwark).
		int bindObelisk = field ? options.GroupBind ?? 700231 : 700231;
		BotPosition obelisk = Spawn(bindObelisk);
		if (world.ObeliskBindPoint is not { } bind || bind.MapId != world.MapId || Distance(bind.Position, obelisk) >= NaturalServicePolicy.SameObeliskRadius)
		{
			int objectId = await NpcAsync(bindObelisk);
			var bound = await new NaturalServiceSteps(session).BindAsync(objectId, obelisk, world.MapId.Value,
				runtime.Data.BindPointDataDh.GetBindPointTemplate(bindObelisk).GetPrice(), 5, token);
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

		bool Everybody(Func<(int Status, int[] Counts), bool> predicate, int? about = null) =>
			world.GroupMembers.Keys.All(id => progress.TryGetValue((id, about ?? quest.Id), out var entry) && predicate(entry));
		bool OwnTasksDone(QuestRunPlan plan) => Counts(plan).Select((count, index) => count >= tasks[plan.Id][index].Count).All(done => done);
		async Task DialogQuestAsync(QuestRunPlan quest, int npc, bool finish)
		{
			var giver = world.Objects.Values.Where(known => known.TemplateId == npc && !known.IsCorpse && Distance(session.CurrentPosition, known.Position) <= 15)
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
				int rewardAction = DialogAction.SELECTED_QUEST_NOREWARD;
				if (quest.HasSelectableReward)
				{
					// The class's own choice, by the rule of every leg (NaturalIshalgenInventoryPolicy.ChooseReward).
					int choice = NaturalIshalgenInventoryPolicy.Load(runtime.RepoRoot, world.Inventory.Values.Select(item => item.ItemId), ClassLine)
						.ChooseReward(quest.Id, world.Level, world.Inventory.Values, combat.ClassProfile.Gear);
					if (choice < 0) throw new InvalidDataException($"Q{quest.Id} offers no reward this class can choose.");
					rewardAction = DialogAction.SELECTED_QUEST_REWARD1 + choice;
				}
				if (quest.Template == "item_collecting" && Status(quest.Id) != 4)
					await FinishItemQuestAsync(session, giver.ObjectId, quest.Id, token, rewardAction);
				else await FinishStandardQuestAsync(session, giver.ObjectId, quest.Id, token, rewardAction);
				await session.SynchronizeAsync(token);
				foreach (var owned in world.Inventory.Values.GroupBy(item => item.ItemId))
					if (owned.Sum(item => item.Count) > before.GetValueOrDefault(owned.Key)) rewardItems[owned.Key] = owned.Sum(item => item.Count) - before.GetValueOrDefault(owned.Key);
			}
			else await session.StartQuestAsync(giver.ObjectId, quest.Id, token);
			await ObserveAsync();
		}

		// Through a portal NPC's dialog, every member for itself (Java PortalService: the first of a group makes the
		// instance, the others are sent into that same one).
		async Task EnterAsync(int portal, int destination)
		{
			BotKnownObject? Gate() => world.Objects.Values.Where(known => known.TemplateId == portal && !known.IsCorpse && Distance(session.CurrentPosition, known.Position) <= 15)
				.OrderBy(known => Distance(session.CurrentPosition, known.Position)).FirstOrDefault();
			var gate = Gate();
			if (gate == null && runtime.Data.SpawnsDh.GetSpawnsByWorldId(world.MapId!.Value).Any(group => group.GetNpcId() == portal))
			{
				// Back outside after the group went in (a revive at the bind): the member walks to the portal NPC by itself.
				await WalkAsync(Spawn(portal));
				await session.SynchronizeAsync(token);
				gate = Gate();
			}
			if (gate == null) return;
			if (Distance(session.CurrentPosition, gate.Position) > 4)
			{
				var path = geometry.FindInteractionPath(world.MapId!.Value, session.CurrentPosition, gate.Position);
				if (path.Count == 0) return;
				await navigator.MoveAsync(path, token);
			}
			session.BeginStep("group-enter", $"npc{portal}");
			await NaturalCapitalSteps.PortalAsync(session, new NaturalCapitalPortal(portal, world.MapId!.Value, destination, 0, GroupNtcEnterDialog), gate.ObjectId, token);
			EnterMap();
			beenInside |= world.MapId == GroupNtcMap;
			session.TraceDiagnostic("group-entered", new Dictionary<string, object?>
			{ ["map"] = world.MapId, ["instance"] = world.InstanceId, ["position"] = session.CurrentPosition });
			await ObserveAsync();
		}

		// The end of the leg. 4.8 has no word that ends a group: it ends when its members leave until fewer than two are
		// left (Java PlayerGroupLeavedEvent). Java moves a member that left its group inside an instance to the
		// instance's exit point thirty seconds later (PlayerLeavedEvent, InstanceService.moveToExitPoint), which for
		// this camp is beside Kelmar. There every member hands in its own quests.
		async Task LeaveAndGoHomeAsync()
		{
			int leftAt = session.PacketHistory.Count;
			session.BeginStep("group-disband", "leave-the-group");
			// A member that read the word late may be outside already: the instance put it out before it looked.
			bool alreadyOut = world.MapId != GroupNtcMap;
			if (world.GroupId != null) await session.SendPacketAsync(session.Api.LeaveGroup(), token);
			long began = runtime.NowMillis;
			while (!alreadyOut && !session.PacketHistory.Skip(leftAt).Any(packet =>
				packet.PacketType == typeof(SM_PLAYER_SPAWN) && packet.Get<int>("worldId") != GroupNtcMap))
			{
				if (runtime.NowMillis - began > 120_000) throw new IOException("The instance did not put the member out after it left the group.");
				await session.AdvanceAsync(TimeSpan.FromMilliseconds(500), token);
				await session.SynchronizeAsync(token);
			}
			int spawned = session.PacketHistory.Count - 1;
			while (spawned >= leftAt && session.PacketHistory[spawned].PacketType != typeof(SM_PLAYER_SPAWN)) spawned--;
			if (spawned >= leftAt && !session.PacketHistory.Skip(spawned).Any(packet => packet.PacketType == typeof(SM_PLAYER_INFO) && packet.Get<int>("objectId") == session.CharacterId))
				await session.WaitForPacketAsync(typeof(SM_PLAYER_INFO), token, packet => packet.Get<int>("objectId") == session.CharacterId);
			session.AcceptTeleportPosition();
			await session.SynchronizeAsync(token);
			EnterMap();
			session.TraceDiagnostic("group-ntc-home", new Dictionary<string, object?>
			{ ["map"] = world.MapId, ["position"] = session.CurrentPosition, ["group"] = world.GroupId, ["waitedMillis"] = runtime.NowMillis - began });
			foreach (QuestRunPlan plan in quests.Skip(1))
			{
				if (!world.Objects.Values.Any(known => known.TemplateId == GroupNtcKelmar && Distance(session.CurrentPosition, known.Position) <= 15))
					await WalkAsync(Spawn(GroupNtcKelmar));
				for (int attempt = 0; attempt < 3 && Status(plan.Id) != 5; attempt++) await DialogQuestAsync(plan, GroupNtcKelmar, true);
				if (Status(plan.Id) != 5) throw new IOException($"Q{plan.Id} was not handed in at Kelmar.");
			}
		}

		// A weapons chest gives its item to the member that opens it, and is gone (Java QuestItemNpcAI). A member opens
		// one on its turn, while it still lacks an item.
		async Task OpenChestAsync(int chest)
		{
			QuestRunPlan weapons = quests[2];
			QuestRunOperation collect = tasks[weapons.Id][0];
			if (!world.Objects.TryGetValue(chest, out var seen) || seen.IsCorpse || ItemCount(world, collect.ItemId) >= collect.Count) return;
			if (Distance(session.CurrentPosition, seen.Position) > 3)
			{
				var path = geometry.FindInteractionPath(world.MapId!.Value, session.CurrentPosition, seen.Position);
				if (path.Count == 0)
				{
					session.TraceDiagnostic("group-chest", new Dictionary<string, object?> { ["chest"] = chest, ["taken"] = false, ["reason"] = "no checked way to the chest" });
					return;
				}
				await navigator.MoveAsync(path, token);
			}
			session.BeginStep("group-chest", $"q{weapons.Id}");
			bool taken = await NaturalAltgardQuestSteps.UseObjectAsync(session, chest, collect.ItemId, token);
			await ObserveAsync();
			session.TraceDiagnostic("group-chest", new Dictionary<string, object?>
			{ ["chest"] = chest, ["taken"] = taken, ["count"] = ItemCount(world, collect.ItemId), ["position"] = session.CurrentPosition });
		}

		// M6: a quest object gives its item to the member that uses it (Q2353's amethysts): every member uses its own, the
		// nearest it sees of the task's kinds, until its journal holds what the quest asks. The leader takes the group to the
		// objects; a member uses only those near its leader (members that went off alone among Kimssi's elites died there).
		var usedObjects = new HashSet<int>();
		var objectTries = new Dictionary<int, int>();
		// M6: a blow breaks a quest object's use bar (Java ItemUseObserver), and Kellan's crystal ore stands among monsters: a
		// member first ends what strikes it, and the Cleric, which never fights, heals until the blows stop.
		async Task ClearStrikersAsync()
		{
			for (int turn = 0; turn < 12 && !world.IsDead; turn++)
			{
				DateTimeOffset recent = session.Api.Timing.Now - TimeSpan.FromSeconds(5);
				HashSet<int> striking = [.. session.PacketHistory.TakeLast(300).Where(packet => (packet.ReceivedAt ?? recent) >= recent)
					.Select(packet => runtime.IncomingAttacker(packet, session.CharacterId)).OfType<int>()];
				var foe = world.Objects.Values.Where(known => striking.Contains(known.ObjectId) && known.Kind == BotKnownObjectKind.Npc && !known.IsCorpse &&
					!GoneOrKilled(known.ObjectId) && Distance(session.CurrentPosition, known.Position) <= 30).OrderBy(known => Distance(session.CurrentPosition, known.Position)).FirstOrDefault();
				if (foe == null) return;
				session.TraceDiagnostic("group-field-object-strikers", new Dictionary<string, object?> { ["target"] = foe.ObjectId, ["template"] = foe.TemplateId });
				if (healer != null)
				{
					await healer.StepAsync(token);
					await PauseAsync();
					continue;
				}
				try { await combat.TryKillAsync(foe.ObjectId, token); }
				catch (NaturalCombatApproachBlockedException) { }
				await combat.RestAsync(token);
			}
		}
		// M6: while the members use what the quest asks of them, the leader kills what strikes one of them.
		async Task<bool> GuardAsync()
		{
			DateTimeOffset recent = session.Api.Timing.Now - TimeSpan.FromSeconds(5);
			HashSet<int> striking = [.. session.PacketHistory.TakeLast(300).Where(packet => (packet.ReceivedAt ?? recent) >= recent)
				.SelectMany(packet => world.GroupMembers.Keys.Select(member => runtime.IncomingAttacker(packet, member))).OfType<int>()];
			if (world.IsDead || world.Objects.Values.Where(known => striking.Contains(known.ObjectId) && known.Kind == BotKnownObjectKind.Npc && !known.IsCorpse &&
					!GoneOrKilled(known.ObjectId) && Distance(session.CurrentPosition, known.Position) <= 30).OrderBy(known => Distance(session.CurrentPosition, known.Position)).FirstOrDefault() is not { } foe)
				return false;
			await SayAsync($"assist:{foe.ObjectId}");
			session.TraceDiagnostic("group-field-guard", new Dictionary<string, object?> { ["target"] = foe.ObjectId, ["template"] = foe.TemplateId });
			try { await combat.TryKillAsync(foe.ObjectId, token); }
			catch (NaturalCombatApproachBlockedException) { }
			await combat.RestAsync(token);
			return true;
		}
		BotPosition[] ObjectSpots(int[] kinds) => [.. kinds.SelectMany(kind => runtime.Data.SpawnsDh.GetSpawnsByWorldId(world.MapId!.Value)
				.Where(group => group.GetNpcId() == kind).SelectMany(group => group.GetSpawnTemplates()))
			.Select(spot => new BotPosition(spot.GetX(), spot.GetY(), spot.GetZ(), 0))
			.OrderBy(at => Distance(session.CurrentPosition, at))];
		async Task UseObjectsAsync(QuestRunPlan plan, int index)
		{
			QuestRunOperation task = tasks[plan.Id][index];
			int[] kinds = [.. (task.Sources ?? []).Select(source => source.NpcId).OfType<int>()];
			for (int attempt = 0; attempt < 40 && Counts(plan)[index] < task.Count && !world.IsDead; attempt++)
			{
				BotPosition? leaderAt = !leads && world.GroupLeaderId is int chief && world.Objects.TryGetValue(chief, out var seenLeader) ? seenLeader.Position : null;
				if (!leads && leaderAt == null) return;
				var seen = world.Objects.Values.Where(known => known.TemplateId is int kind && kinds.Contains(kind) && !known.IsCorpse &&
					!usedObjects.Contains(known.ObjectId) && (leaderAt is not { } by || Distance(by, known.Position) <= 25))
					.OrderBy(known => Distance(session.CurrentPosition, known.Position)).FirstOrDefault();
				if (seen == null)
				{
					if (!leads) return;
					BotPosition[] spots = ObjectSpots(kinds);
					if (spots.Length == 0) break;
					BotPosition next = spots[Math.Min(attempt / 2, spots.Length - 1)];
					try { await WalkAsync(next, 10); }
					catch (IOException walk) { session.TraceDiagnostic("group-field-object-walk", new Dictionary<string, object?> { ["to"] = next, ["reason"] = walk.Message }); }
					await PauseAsync();
					continue;
				}
				if (Distance(session.CurrentPosition, seen.Position) > 3)
				{
					var path = geometry.FindInteractionPath(world.MapId!.Value, session.CurrentPosition, seen.Position);
					if (path.Count == 0) { usedObjects.Add(seen.ObjectId); continue; }
					await navigator.MoveAsync(path, token);
				}
				await ClearStrikersAsync();
				if (world.IsDead) break;
				session.BeginStep("group-field-object", $"q{plan.Id}");
				bool taken = await NaturalAltgardQuestSteps.UseObjectAsync(session, seen.ObjectId, task.ItemId, token);
				await ObserveAsync();
				// A use that a blow broke is tried again; an ore used, gone or tried three times is left.
				bool gone = !world.Objects.TryGetValue(seen.ObjectId, out var after) || after.IsCorpse;
				objectTries[seen.ObjectId] = objectTries.GetValueOrDefault(seen.ObjectId) + 1;
				if (taken || gone || objectTries[seen.ObjectId] >= 3) usedObjects.Add(seen.ObjectId);
				session.TraceDiagnostic("group-field-object", new Dictionary<string, object?>
				{ ["quest"] = plan.Id, ["object"] = seen.ObjectId, ["taken"] = taken, ["count"] = Counts(plan)[index] });
			}
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
				// Revived at the bind while the group is inside the camp: back through the portal, whatever F1 said last.
				if (beenInside && world.MapId != GroupNtcMap && world.GroupLeaderId is int within &&
					world.GroupMembers.TryGetValue(within, out var itsLeader) && itsLeader.Online && itsLeader.MapId == GroupNtcMap)
				{
					await EnterAsync(GroupNtcVallack, GroupNtcMap);
					if (world.MapId != GroupNtcMap) await PauseAsync();
					continue;
				}
				if (command == "regroup" && !returnedForRegroup) { await ReturnToBindAsync(); returnedForRegroup = true; }
				if (command != "regroup") returnedForRegroup = false;
				if (command is "accept" or "turn-in" && arguments is [int about, int npc] && quests.FirstOrDefault(plan => plan.Id == about) is { } asked)
				{
					if (command == "accept" && Status(about) is not (3 or 4 or 5)) await DialogQuestAsync(asked, npc, false);
					else if (command == "turn-in" && Status(about) is 3 or 4 && OwnTasksDone(asked)) await DialogQuestAsync(asked, npc, true);
				}
				else if (command == "enter" && arguments is [int portal, int destination] && world.MapId != destination) await EnterAsync(portal, destination);
				else if (command == "use" && arguments is [int usedFor, int usedTask] && quests.FirstOrDefault(plan => plan.Id == usedFor) is { } objectQuest &&
					Counts(objectQuest)[usedTask] < tasks[usedFor][usedTask].Count) await UseObjectsAsync(objectQuest, usedTask);
				else if (command == "chest" && arguments is [int chest, int opener] && opener == session.CharacterId && chest != toldChest)
				{
					toldChest = chest;
					await OpenChestAsync(chest);
				}
				else if (command == "disband")
				{
					await LeaveAndGoHomeAsync();
					break;
				}
				if (healer != null) await healer.StepAsync(token);
				else if (assist is int target && world.Objects.TryGetValue(target, out var enemy) && !enemy.IsCorpse &&
					Distance(session.CurrentPosition, enemy.Position) <= 35 && session.PacketHistory.TakeLast(400)
						.Any(packet => runtime.IncomingAttacker(packet, target) == world.GroupLeaderId))
				{
					try
					{
						if (await combat.TryKillAsync(target, token) && enemy.TemplateId is int fallen) killedKinds.Add(fallen);
					}
					catch (NaturalCombatApproachBlockedException blocked)
					{
						if (GoneOrKilled(target)) combat.NoteTargetTaken();
						else session.TraceDiagnostic("group-assist-blocked", new Dictionary<string, object?> { ["target"] = target, ["reason"] = blocked.Message });
					}
					assist = null;
					await combat.RestAsync(token);
				}
				else if (field && healer == null && !combat.InCombat && !world.IsDead)
				{
					// M6: a fighter turns on whatever strikes a member near it. The Cleric, which heals and never fights, was caught
					// alone behind the group by Kimssi's elites, and nobody came back for it.
					// Only a blow of the last five seconds, from a monster alive and near, and one this fighter has not failed twice.
					DateTimeOffset recent = session.Api.Timing.Now - TimeSpan.FromSeconds(5);
					HashSet<int> striking = [.. session.PacketHistory.TakeLast(300).Where(packet => (packet.ReceivedAt ?? recent) >= recent)
						.SelectMany(packet => world.GroupMembers.Keys.Where(member => member != session.CharacterId)
							.Select(member => runtime.IncomingAttacker(packet, member))).OfType<int>()];
					// Nor one seen falling: its corpse comes a moment after its last blow, and a cast at it went unanswered.
					if (world.Objects.Values.Where(known => striking.Contains(known.ObjectId) && known.Kind == BotKnownObjectKind.Npc && !known.IsCorpse &&
							!GoneOrKilled(known.ObjectId) && protectFailures.GetValueOrDefault(known.ObjectId) < 2 && Distance(session.CurrentPosition, known.Position) <= 30)
						.OrderBy(known => Distance(session.CurrentPosition, known.Position)).FirstOrDefault() is { } foe)
					{
						session.TraceDiagnostic("group-field-protect", new Dictionary<string, object?> { ["target"] = foe.ObjectId, ["template"] = foe.TemplateId });
						bool fell = false;
						try { fell = await combat.TryKillAsync(foe.ObjectId, token) || GoneOrKilled(foe.ObjectId); }
						catch (NaturalCombatApproachBlockedException blocked)
						{
							session.TraceDiagnostic("group-assist-blocked", new Dictionary<string, object?> { ["target"] = foe.ObjectId, ["reason"] = blocked.Message });
						}
						if (!fell) protectFailures[foe.ObjectId] = protectFailures.GetValueOrDefault(foe.ObjectId) + 1;
						await combat.RestAsync(token);
					}
				}
				if (options.GroupLoot) await loot.StepAsync(token);
				await following.StepAsync(token);
				await PauseAsync();
			}
		}
		else if (ntc)
		{
			// N2: the entry quest from Aegir to Kelmar, then Kelmar's two group quests, every member for itself.
			async Task TogetherAtAsync(QuestRunPlan plan, int npc, bool finish)
			{
				await NpcAsync(npc);
				await SayAsync($"command:{(finish ? "turn-in" : "accept")}:{plan.Id}:{npc}");
				await DialogQuestAsync(plan, npc, finish);
				await WaitAsync(() => Everybody(entry => finish ? entry.Status == 5 : entry.Status is 3 or 4, plan.Id),
					$"every member to {(finish ? "turn in" : "accept")} Q{plan.Id}");
				session.TraceDiagnostic("group-ntc-step", new Dictionary<string, object?>
				{ ["quest"] = plan.Id, ["npc"] = npc, ["done"] = finish ? "turned-in" : "accepted", ["members"] = world.GroupMembers.Count });
			}
			BotPosition landing = default;
			// The group was ended by its leader's word (N9), not by "done".
			bool gone = false;
			await TogetherAtAsync(quests[0], GroupNtcAegir, false);
			await TogetherAtAsync(quests[0], GroupNtcKelmar, true);
			foreach (QuestRunPlan plan in quests.Skip(1)) await TogetherAtAsync(plan, GroupNtcKelmar, false);
			if (options.GroupUntil != "quests")
			{
				// N3: nobody talks to Vallack until all six stand before him; then each goes in, into one instance.
				await NpcAsync(GroupNtcVallack);
				await WaitForFollowersAsync(token);
				await SayAsync($"command:enter:{GroupNtcVallack}:{GroupNtcMap}");
				await EnterAsync(GroupNtcVallack, GroupNtcMap);
				if (world.MapId != GroupNtcMap) throw new IOException("The group leader did not enter the camp.");
				landing = session.CurrentPosition;
				await WaitAsync(() => world.GroupMembers.Keys.All(id => places.TryGetValue(id, out var at) && at == places[session.CharacterId]),
					"every member inside the leader's instance", 180);
				session.TraceDiagnostic("group-ntc-step", new Dictionary<string, object?>
				{ ["done"] = "entered", ["map"] = world.MapId, ["instance"] = world.InstanceId, ["members"] = world.GroupMembers.Count });
			}

			// Inside the camp F1 walks the checked way to a named monster. Whatever fights the group, or stands where the
			// next stretch of the way would wake it, is killed first, by all: F1 names it and the others assist.
			var refused = new Dictionary<int, int>();
			var killed = new HashSet<int>();
			int named = 0;
			long namedAt = 0;
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
					.OrderBy(hostile => Distance(session.CurrentPosition, hostile.Seen.Position)).Select(hostile => (int?)hostile.Seen.ObjectId).FirstOrDefault();
			}
			static float FromSegment(BotPosition a, BotPosition b, BotPosition p)
			{
				float dx = b.X - a.X, dy = b.Y - a.Y, dz = b.Z - a.Z, length = dx * dx + dy * dy + dz * dz;
				float t = length < .0001f ? 0 : Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy + (p.Z - a.Z) * dz) / length, 0, 1);
				return Distance(new(a.X + dx * t, a.Y + dy * t, a.Z + dz * t, 0), p);
			}
			// Before F1 goes on: everyone alive, near and with mana. A monster on a member ends the wait at once.
			async Task<bool> RegroupedAsync()
			{
				long began = runtime.NowMillis;
				while (true)
				{
					await ObserveAsync();
					if (world.IsDead || OnTheGroup(Hostiles()) != null) return false;
					if (world.GroupMembers.Values.Where(member => member.ObjectId != session.CharacterId).All(member =>
						member.Online && !member.Dead && member.MapId == world.MapId && (member.MaxMp <= 0 || member.CurrentMp * 100L / member.MaxMp >= 50) &&
						Distance(session.CurrentPosition, world.Objects.GetValueOrDefault(member.ObjectId)?.Position ?? member.Position) <= 12)) return true;
					if (runtime.NowMillis - began > 600_000)
					{
						session.TraceDiagnostic("group-ntc-not-gathered", new Dictionary<string, object?>
						{
							["position"] = session.CurrentPosition,
							["members"] = world.GroupMembers.Values.Select(member => new
							{
								member.Name, member.Online, member.Dead, member.MapId, member.CurrentMp, member.MaxMp,
								Distance = Distance(session.CurrentPosition, world.Objects.GetValueOrDefault(member.ObjectId)?.Position ?? member.Position),
							}).ToArray(),
						});
						throw new IOException("Group wait timed out: the group to gather inside the camp.");
					}
					await session.AdvanceAsync(TimeSpan.FromMilliseconds(250), token);
				}
			}
			async Task GroupKillAsync(int target)
			{
				// The name of a target is spoken once, and again only when it has not been spoken for a while.
				if (named != target || runtime.NowMillis - namedAt > 5000)
				{
					await SayAsync($"assist:{target}");
					named = target; namedAt = runtime.NowMillis;
				}
				int? template = world.Objects.GetValueOrDefault(target)?.TemplateId;
				session.TraceDiagnostic("group-ntc-pull", new Dictionary<string, object?>
				{ ["target"] = target, ["template"] = template, ["position"] = session.CurrentPosition });
				bool dead = false;
				try
				{
					dead = await combat.TryKillAsync(target, token) || GoneOrKilled(target);
				}
				catch (NaturalCombatApproachBlockedException blocked)
				{
					// A member may finish it while F1 approaches; otherwise it is left until it comes at the group.
					dead = GoneOrKilled(target);
					session.TraceDiagnostic("group-target-skipped", new Dictionary<string, object?> { ["target"] = target, ["deadOrGone"] = dead, ["reason"] = blocked.Message });
				}
				if (!dead)
				{
					refused[target] = refused.GetValueOrDefault(target) + 1;
					await session.AdvanceAsync(TimeSpan.FromSeconds(1), token);
					return;
				}
				killed.Add(target);
				if (template is int kind) killedKinds.Add(kind);
				// Whoever has the right to the corpse takes its turn; F1 does not stand long for it, but for the General's
				// it stands until it is taken or a quarter of a minute is over.
				if (options.GroupLoot)
					for (int wait = 0; wait < (template == GroupNtcGeneral ? 60 : 12) && world.Objects.ContainsKey(target); wait++)
					{
						await loot.StepAsync(token);
						await PauseAsync();
					}
			}
			async Task WalkStretchAsync(IReadOnlyList<BotPosition> route, float stretch)
			{
				float walked = 0;
				foreach (var point in route)
					while (Distance(session.CurrentPosition, point) > .5f)
					{
						var at = session.CurrentPosition;
						float fraction = Math.Min(1, 6 / Distance(at, point));
						BotPosition next = new(at.X + (point.X - at.X) * fraction, at.Y + (point.Y - at.Y) * fraction,
							at.Z + (point.Z - at.Z) * fraction, point.Heading);
						await navigator.MoveAsync([at, next], token);
						await ObserveAsync();
						float moved = Distance(session.CurrentPosition, at);
						walked += moved;
						if (world.IsDead || moved < .1f || walked >= stretch || OnTheGroup(Hostiles()) != null) return;
					}
			}
			// To a place inside the camp, fighting what is in the way, until done says so. kind is the monster fought at
			// the place itself, when there is one.
			async Task AdvanceAsync(string what, BotPosition lair, int kind, Func<bool> done)
			{
				session.TraceDiagnostic("group-ntc-hunt", new Dictionary<string, object?> { ["what"] = what, ["npc"] = kind, ["lair"] = lair });
				int idle = 0;
				while (!done())
				{
					await ObserveAsync();
					if (world.IsDead) { await combat.RestAsync(token); continue; }
					if (world.MapId != GroupNtcMap)
					{
						await EnterAsync(GroupNtcVallack, GroupNtcMap);
						if (world.MapId != GroupNtcMap) throw new IOException("The group leader could not go back into the camp.");
						continue;
					}
					// A monster on a member is fought where it stands, at once. Nothing else is begun before the group has
					// rested and stands together.
					int? pick = OnTheGroup(Hostiles());
					IReadOnlyList<BotPosition> route = [];
					if (pick == null)
					{
						await combat.RestAsync(token);
						if (!await RegroupedAsync()) continue;
						var hostiles = Hostiles();
						if (Distance(session.CurrentPosition, lair) > 3)
						{
							route = geometry.FindJourneyPath(world.MapId!.Value, session.CurrentPosition, lair);
							// The gate stands in its gateway, where the mesh has a seam: the way to within reach of it will do.
							if (route.Count == 0) route = geometry.FindInteractionPath(world.MapId!.Value, session.CurrentPosition, lair);
							if (route.Count == 0) throw new IOException($"No checked way inside the camp from {session.CurrentPosition} to {lair}.");
							// The stretch looked at is longer than the stretch walked, so no circle is entered unseen.
							float ahead = 0;
							BotPosition from = session.CurrentPosition;
							foreach (var point in route)
							{
								pick = hostiles.Where(hostile => refused.GetValueOrDefault(hostile.Seen.ObjectId) < 3 && FromSegment(from, point, hostile.Seen.Position) <= hostile.Reach)
									.OrderBy(hostile => Distance(session.CurrentPosition, hostile.Seen.Position)).Select(hostile => (int?)hostile.Seen.ObjectId).FirstOrDefault();
								if (pick != null) break;
								ahead += Distance(from, point); from = point;
								if (ahead >= 16) break;
							}
						}
						// At the place the named monster itself is the one to fight, wherever in sight it stands, and whether or
						// not it would come at a player by itself (the gate does not).
						else pick = world.Objects.Values.Where(known => known.Kind == BotKnownObjectKind.Npc && known.TemplateId == kind && kind != 0 &&
								!known.IsCorpse && !killed.Contains(known.ObjectId))
							.OrderBy(known => Distance(session.CurrentPosition, known.Position)).Select(known => (int?)known.ObjectId).FirstOrDefault();
					}
					if (pick is int target) { idle = 0; await GroupKillAsync(target); continue; }
					if (route.Count == 0)
					{
						// At the place with nothing to fight: the named monster is not there, or a member lacks its count.
						if (++idle > 480) throw new IOException($"{what}: the group stands at the place, npc {kind} is not there to fight, and it is not done.");
						await PauseAsync();
						continue;
					}
					idle = 0;
					await WalkStretchAsync(route, 8);
				}
				session.TraceDiagnostic("group-ntc-step", new Dictionary<string, object?>
				{ ["done"] = what, ["npc"] = kind, ["members"] = world.GroupMembers.Count, ["kills"] = killed.Count, ["deaths"] = combat.ReviveCount,
					["position"] = session.CurrentPosition });
			}
			// A named monster of a kill quest: done when every member's journal has it.
			Task HuntNamedAsync(QuestRunPlan plan, int index)
			{
				QuestRunOperation kill = tasks[plan.Id][index];
				int kind = kill.Npcs!.Select(npc => npc.Id).First(id => runtime.Data.SpawnsDh.GetSpawnsByWorldId(world.MapId!.Value).Any(group => group.GetNpcId() == id));
				return AdvanceAsync($"Q{plan.Id} npc {kind}", Spawn(kind), kind, () => Everybody(entry => entry.Counts[index] >= kill.Count, plan.Id));
			}
			if (reach >= 2)
			{
				// N4: west through the lower camp to the Protector.
				await combat.BuffOurselfAsync(NaturalHelpTrigger.PrePull, token);
				await HuntNamedAsync(quests[1], 0);
			}
			if (reach >= 3)
			{
				// N5: north to the Guard, then the Aetheric Field Guard.
				await HuntNamedAsync(quests[1], 1);
				await HuntNamedAsync(quests[1], 2);
			}
			if (reach >= 4)
			{
				// N6: the gate is a monster in its gateway and does not fight. All strike it until it falls.
				await AdvanceAsync("the fortress gate", Spawn(GroupNtcGate), GroupNtcGate, () => killedKinds.Contains(GroupNtcGate));
			}
			if (reach >= 5)
			{
				// N7: every member takes four items for Q4734, each from a chest it opens itself; then the Teleporter.
				// The chests on the camp's own ground, outside the walls, come first, and then the ones inside: the
				// fortress holds 21 chests and the six need 24 items.
				QuestRunPlan weapons = quests[2];
				int need = tasks[weapons.Id][0].Count;
				BotPosition[] spots = [.. runtime.Data.SpawnsDh.GetSpawnsByWorldId(world.MapId!.Value).Where(group => group.GetNpcId() == GroupNtcChest)
					.SelectMany(group => group.GetSpawnTemplates()).Select(spot => new BotPosition(spot.GetX(), spot.GetY(), spot.GetZ(), spot.GetHeading()))];
				var visited = new HashSet<int>();
				int opened = 0;
				bool AllHaveTheirs() => Everybody(entry => entry.Counts[0] >= need, weapons.Id);
				int Has(int member) => progress.TryGetValue((member, weapons.Id), out var entry) ? entry.Counts[0] : 0;
				async Task ChestTourAsync(string part, Func<BotPosition, bool> ofThisPart)
				{
					while (!AllHaveTheirs())
					{
						int next = Enumerable.Range(0, spots.Length).Where(index => !visited.Contains(index) && ofThisPart(spots[index]))
							.OrderBy(index => Distance(session.CurrentPosition, spots[index])).DefaultIfEmpty(-1).First();
						if (next < 0) return;
						visited.Add(next);
						BotPosition spot = spots[next];
						if (geometry.FindJourneyPath(world.MapId!.Value, session.CurrentPosition, spot).Count == 0 &&
							geometry.FindInteractionPath(world.MapId!.Value, session.CurrentPosition, spot).Count == 0)
						{
							session.TraceDiagnostic("group-chest-skipped", new Dictionary<string, object?> { ["spot"] = spot, ["reason"] = "no checked way from here" });
							continue;
						}
						await AdvanceAsync($"{part} chest at {spot.X:F0}, {spot.Y:F0}, {spot.Z:F0}", spot, 0, () => Distance(session.CurrentPosition, spot) <= 4);
						await session.SynchronizeAsync(token);
						var chest = world.Objects.Values.Where(known => known.TemplateId == GroupNtcChest && !known.IsCorpse && Distance(known.Position, spot) <= 3)
							.OrderBy(known => Distance(known.Position, spot)).FirstOrDefault();
						if (chest == null) continue;
						while (!await RegroupedAsync())
							if (OnTheGroup(Hostiles()) is int attacker) await GroupKillAsync(attacker);
							else if (world.IsDead) await combat.RestAsync(token);
						// Whose turn: the member that lacks the most, the roster's order between equals.
						int opener = world.GroupMembers.Keys.Where(member => Has(member) < need)
							.OrderBy(member => Has(member)).ThenBy(member => Array.IndexOf(roster, world.GroupMembers[member].Name)).First();
						int before = Has(opener);
						session.TraceDiagnostic("group-chest-turn", new Dictionary<string, object?>
						{ ["chest"] = chest.ObjectId, ["spot"] = spot, ["opener"] = world.GroupMembers[opener].Name, ["has"] = before });
						opened++;
						if (opener == session.CharacterId) await OpenChestAsync(chest.ObjectId);
						else
						{
							await SayAsync($"command:chest:{chest.ObjectId}:{opener}");
							long began = runtime.NowMillis;
							while (Has(opener) == before && runtime.NowMillis - began < 40_000 && world.Objects.TryGetValue(chest.ObjectId, out var still) && !still.IsCorpse)
							{
								if (OnTheGroup(Hostiles()) is int attacker) await GroupKillAsync(attacker);
								await PauseAsync();
							}
							// The member's journal follows its loot by a moment.
							for (int wait = 0; wait < 12 && Has(opener) == before; wait++) await PauseAsync();
						}
					}
				}
				BotNavMesh? mesh = geometry.NavMesh?.NavMeshes.Get(world.MapId!.Value);
				// The camp's island is the landing's: F1 itself stands in the gateway now, on the seam.
				int camp = mesh?.IslandOf(landing, BotNavQuery.Default with { SnapHorizontal = 2 }) ?? -1;
				bool Outside(BotPosition spot) => mesh != null && mesh.IslandOf(spot, BotNavQuery.Default with { SnapHorizontal = 2 }) == camp;
				await ChestTourAsync("outer", Outside);
				if (!AllHaveTheirs())
				{
					if (Distance(session.CurrentPosition, Spawn(GroupNtcGate)) > 12)
						await AdvanceAsync("back before the gateway", Spawn(GroupNtcGate), 0, () => Distance(session.CurrentPosition, Spawn(GroupNtcGate)) <= 6);
					await AdvanceAsync("inside the gateway", GroupNtcInside, 0, () => Distance(session.CurrentPosition, GroupNtcInside) <= 3);
					await ChestTourAsync("inner", spot => !Outside(spot));
				}
				session.TraceDiagnostic("group-ntc-step", new Dictionary<string, object?>
				{ ["done"] = "chests", ["opened"] = opened, ["visited"] = visited.Count, ["all"] = AllHaveTheirs(),
					["has"] = world.GroupMembers.Values.ToDictionary(member => member.Name ?? member.ObjectId.ToString(), member => Has(member.ObjectId)) });
				if (!AllHaveTheirs()) throw new IOException("The chests of the camp are used up and not every member has its four items.");
				await HuntNamedAsync(quests[1], 3);
			}
			if (reach >= 6)
			{
				// N8: the end boss. No quest of the leg counts him until Q4732 has a handler; his fall is what F1 saw.
				await AdvanceAsync("the General", Spawn(GroupNtcGeneral), GroupNtcGeneral, () => killedKinds.Contains(GroupNtcGeneral));
			}
			if (reach >= 7)
			{
				// N9: the General is dead and looted. F1 gathers the group once more, speaks the word, and leaves last.
				while (!await RegroupedAsync())
					if (OnTheGroup(Hostiles()) is int attacker) await GroupKillAsync(attacker);
					else if (world.IsDead) await combat.RestAsync(token);
				await SayAsync("command:disband");
				session.TraceDiagnostic("group-ntc-step", new Dictionary<string, object?> { ["done"] = "disband spoken", ["members"] = world.GroupMembers.Count });
				long spoken = runtime.NowMillis;
				while (world.GroupId != null && world.GroupMembers.Count > 1 && runtime.NowMillis - spoken < 30_000) await PauseAsync();
				session.TraceDiagnostic("group-ntc-step", new Dictionary<string, object?>
				{ ["done"] = "the others left", ["group"] = world.GroupId, ["members"] = world.GroupMembers.Count, ["waitedMillis"] = runtime.NowMillis - spoken });
				await LeaveAndGoHomeAsync();
				// The word is spoken and the group is gone: no "done" follows.
				gone = true;
			}
			if (!gone)
			{
				await SayAsync("command:done");
				await session.AdvanceAsync(TimeSpan.FromSeconds(2), token);
			}
		}
		else if (field)
		{
			// M6: the field quests, one after another: accepted together at the giver, worked until every member's journal
			// holds what the quest asks, and handed in together at its end.
			async Task TogetherFieldAsync(QuestRunPlan plan, int npc, bool finish)
			{
				await NpcAsync(npc);
				await WaitForFollowersAsync(token);
				await SayAsync($"command:{(finish ? "turn-in" : "accept")}:{plan.Id}:{npc}");
				if (finish ? Status(plan.Id) is 3 or 4 : Status(plan.Id) is not (3 or 4 or 5)) await DialogQuestAsync(plan, npc, finish);
				await WaitAsync(() => Everybody(entry => finish ? entry.Status == 5 : entry.Status is 3 or 4 or 5, plan.Id),
					$"every member to {(finish ? "turn in" : "accept")} Q{plan.Id}", 300);
				session.TraceDiagnostic("group-field-step", new Dictionary<string, object?>
				{ ["quest"] = plan.Id, ["npc"] = npc, ["done"] = finish ? "turned-in" : "accepted", ["members"] = world.GroupMembers.Count });
			}
			int LivingTarget(int[] kinds) => world.Objects.Values.Where(known => known.TemplateId is int kind && kinds.Contains(kind) && !known.IsCorpse &&
				!navigator.UnavailableObjects.Contains(known.ObjectId)).OrderBy(known => Distance(session.CurrentPosition, known.Position))
				.FirstOrDefault()?.ObjectId ?? 0;
			foreach (QuestRunPlan plan in quests)
			{
				if (Everybody(entry => entry.Status == 5, plan.Id)) continue;
				int giver = plan.StartNpcs.First().Id, end = plan.EndNpcs.FirstOrDefault()?.Id ?? giver;
				await TogetherFieldAsync(plan, giver, false);
				QuestRunOperation[] work = tasks[plan.Id];
				for (int index = 0; index < work.Length; index++)
				{
					QuestRunOperation task = work[index];
					int at = index;
					bool Done() => Everybody(entry => entry.Status is 4 or 5 || entry.Counts.Length > at && entry.Counts[at] >= task.Count, plan.Id);
					// A drop from a quest object (Q2353's crystal ore, ai quest_use_item) comes from using it, not from a fight: the
					// plan calls it a drop, and the leader pulled ore twelve times for nothing (Java QuestItemNpcAI).
					bool fromObjects = task.Kind == QuestRunOperationKind.CollectQuestDrop && (task.Sources ?? []).Count > 0 &&
						(task.Sources ?? []).All(source => source.NpcId is int id && runtime.Data.NpcDataDh.GetNpcTemplate(id)?.GetAiName() == "quest_use_item");
					if (task.Kind == QuestRunOperationKind.UseQuestObject || fromObjects)
					{
						await SayAsync($"command:use:{plan.Id}:{index}");
						await UseObjectsAsync(plan, index);
						// The leader stays by the objects and guards the members that still use them; one that stood up at the bind
						// comes back to them alone.
						long began = runtime.NowMillis;
						while (!Done())
						{
							if (runtime.NowMillis - began > 1_800_000) throw new IOException($"Group wait timed out: every member to use Q{plan.Id}'s objects.");
							if (world.IsDead) { await combat.RestAsync(token); continue; }
							if (Counts(plan)[index] < task.Count) { await UseObjectsAsync(plan, index); await PauseAsync(); continue; }
							if (ObjectSpots([.. (task.Sources ?? []).Select(source => source.NpcId).OfType<int>()]).FirstOrDefault() is { } back &&
								Distance(session.CurrentPosition, back) > 40)
							{
								try { await WalkAsync(back, 10); }
								catch (IOException walk) { session.TraceDiagnostic("group-field-object-walk", new Dictionary<string, object?> { ["to"] = back, ["reason"] = walk.Message }); }
								await SayAsync($"command:use:{plan.Id}:{index}");
								await PauseAsync();
								continue;
							}
							if (!await GuardAsync()) await PauseAsync();
						}
						await SayAsync("command:hunt");
						continue;
					}
					int[] kinds = [.. (task.Kind == QuestRunOperationKind.Kill ? (task.Npcs ?? []).Select(npc => npc.Id)
						: (task.Sources ?? []).Select(source => source.NpcId).OfType<int>())
						.Where(id => runtime.Data.SpawnsDh.GetSpawnsByWorldId(world.MapId!.Value).Any(group => group.GetNpcId() == id))];
					if (kinds.Length == 0) throw new InvalidDataException($"Q{plan.Id} task {index} has no source on this map.");
					int waits = 0;
					while (!Done())
					{
						await combat.RestAsync(token);
						await combat.BuffOurselfAsync(NaturalHelpTrigger.PrePull, token);
						BotPosition[] candidates = [.. kinds.SelectMany(kind => runtime.Data.SpawnsDh.GetSpawnsByWorldId(world.MapId!.Value)
							.Where(group => group.GetNpcId() == kind).SelectMany(group => group.GetSpawnTemplates()))
							.Select(spot => new BotPosition(spot.GetX(), spot.GetY(), spot.GetZ(), spot.GetHeading()))
							.OrderBy(spot => Distance(session.CurrentPosition, spot))];
						int target = LivingTarget(kinds);
						foreach (BotPosition spot in candidates.Take(8))
						{
							if (target != 0) break;
							// Once a monster comes into sight, its class combat driver owns the checked approach and pull.
							await WalkAsync(spot, 60);
							target = LivingTarget(kinds);
						}
						if (target == 0)
						{
							// A named monster the other group (or this one) killed a moment ago: wait at its spawn for it to come
							// back (Q2494's Sku, Paku and Seku; Q2328's Millennium Eka, half an hour).
							if (++waits > 80) throw new IOException($"No living target of Q{plan.Id} ({string.Join("/", kinds)}) came back.");
							session.TraceDiagnostic("group-field-wait-for-spawn", new Dictionary<string, object?> { ["quest"] = plan.Id, ["kinds"] = kinds, ["wait"] = waits });
							await session.AdvanceAsync(TimeSpan.FromSeconds(60), token);
							await ObserveAsync();
							continue;
						}
						waits = 0;
						await WaitForFollowersAsync(token);
						await SayAsync($"assist:{target}");
						session.TraceDiagnostic("group-field-pull", new Dictionary<string, object?>
						{ ["quest"] = plan.Id, ["target"] = target, ["template"] = world.Objects.GetValueOrDefault(target)?.TemplateId, ["position"] = session.CurrentPosition });
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
							// Each member opens the corpse for the items that drop for it (drop_each_member); hold while they do.
							for (int wait = 0; wait < 60 && world.Objects.TryGetValue(target, out _); wait++)
							{
								await loot.StepAsync(token);
								await PauseAsync();
							}
						}
						await PauseAsync();
					}
				}
				await TogetherFieldAsync(plan, end, true);
			}
			await SayAsync("command:done");
			await session.AdvanceAsync(TimeSpan.FromSeconds(2), token);
		}
		else
		{
			await NpcAsync(204360);
			await SayAsync($"command:accept:{quest.Id}:204360");
			await DialogQuestAsync(quest, 204360, false);
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
			await SayAsync($"command:turn-in:{quest.Id}:204360");
			await DialogQuestAsync(quest, 204360, true);
			await WaitAsync(() => Everybody(entry => entry.Status == 5), "every member to turn in Q2318");
			await SayAsync("command:done");
			await session.AdvanceAsync(TimeSpan.FromSeconds(2), token);
		}
		await ObserveAsync();
		if (ntc)
		{
			// N2's end: the entry quest handed in and Kelmar's two in the journal. N3's: that, inside the camp.
			int[] wanted = [5, .. quests.Skip(1).Select(_ => reach >= 7 ? 5 : 3)];
			int[] journal = [.. quests.Select(plan => Status(plan.Id))];
			if (!journal.SequenceEqual(wanted))
				throw new InvalidDataException($"The group member's journal is [{string.Join(", ", journal)}], not [{string.Join(", ", wanted)}].");
			if (reach is >= 1 and < 7 && (world.MapId != GroupNtcMap || world.InstanceId == null))
				throw new InvalidDataException($"The group member stands on map {world.MapId}, not inside the camp.");
			// N9's: out of the camp, in no group, the quests handed in (the journal above).
			if (reach >= 7 && (world.MapId == GroupNtcMap || world.GroupId != null))
				throw new InvalidDataException($"The group member is on map {world.MapId} in group {world.GroupId}: not home, or not out of its group.");
			// N4's: the Protector in the journal of Q4733. N5's: the Guard and the Aetheric Field Guard beside him.
			int[] namedKills = Counts(quests[1]);
			if (reach >= 2 && namedKills[0] < 1) throw new InvalidDataException("The group member has no kill of the Protector in its journal.");
			if (reach >= 3 && (namedKills[1] < 1 || namedKills[2] < 1))
				throw new InvalidDataException("The group member lacks the Guard or the Aetheric Field Guard in its journal.");
			// N7's: its four chest items and the Teleporter.
			if (reach >= 5 && (namedKills[3] < 1 || Counts(quests[2])[0] < tasks[quests[2].Id][0].Count))
				throw new InvalidDataException("The group member lacks the Teleporter or its chest items.");
			runtime.AssertClean();
			var reached = new { verified = true, CharacterId = session.CharacterId, name, until = options.GroupUntil,
				leader = world.GroupLeaderId, group = world.GroupId, roster, quests = quests.ToDictionary(plan => plan.Id.ToString(), plan => Status(plan.Id)),
				counts = quests.ToDictionary(plan => plan.Id.ToString(), plan => Counts(plan)), lootCollected = loot.Collected,
				// The gate and the General, which no quest of the leg counts: whether this member saw them fall in a fight
				// of its own, and whether its client still shows the gate standing.
				sawFall = new { gate = killedKinds.Contains(GroupNtcGate), general = killedKinds.Contains(GroupNtcGeneral) },
				gateSeenStanding = world.Objects.Values.Any(known => known.TemplateId == GroupNtcGate && !known.IsCorpse),
				elapsedMillis = runtime.NowMillis - started, map = world.MapId, instance = world.InstanceId, position = session.CurrentPosition,
				deaths = combat.ReviveCount, rewardItems };
			File.WriteAllText(Path.Combine(Path.GetDirectoryName(session.CombatTracePath!)!, "group-ntc-completion.json"),
				JsonSerializer.Serialize(reached, new JsonSerializerOptions { WriteIndented = true }));
			session.TraceDiagnostic("group-ntc-reached", new Dictionary<string, object?> { ["receipt"] = reached });
			return;
		}
		if (field)
		{
			int[] notDone = [.. quests.Where(plan => Status(plan.Id) != 5).Select(plan => plan.Id)];
			if (notDone.Length > 0) throw new InvalidDataException($"The group member has not handed in Q{string.Join(", Q", notDone)}.");
			runtime.AssertClean();
			var played = new { verified = true, CharacterId = session.CharacterId, name, leader = world.GroupLeaderId, roster,
				quests = quests.Select(plan => plan.Id).ToArray(), elapsedMillis = runtime.NowMillis - started, position = session.CurrentPosition,
				bind = world.ObeliskBindPoint, deaths = combat.ReviveCount, lootCollected = loot.Collected, rewardItems };
			File.WriteAllText(Path.Combine(Path.GetDirectoryName(session.CombatTracePath!)!, "group-field-completion.json"),
				JsonSerializer.Serialize(played, new JsonSerializerOptions { WriteIndented = true }));
			session.TraceDiagnostic("group-field-completed", new Dictionary<string, object?> { ["receipt"] = played });
			return;
		}
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
