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
	/// <summary>M6: every portal used as an object in the shipped data (portal_template2.xml's portal_use: Java PortalAI): the
	/// NPC, where it leads, whom it takes and the title it asks to be worn. Read from the file as the companion reads the
	/// dialog portals: the server's own table keeps its entries to itself once it is loaded.</summary>
	private static (int Npc, int LocId, string Race, int TitleId)[] GroupPortalUses(string repoRoot) =>
		groupPortalUses ??= [.. System.Xml.Linq.XDocument.Load(Path.Combine(repoRoot, "game-server", "data", "static_data", "portals", "portal_template2.xml"))
			.Root!.Elements("portal_use").SelectMany(use => use.Elements("portal_path").Select(path => (
				Npc: (int)use.Attribute("npc_id")!, LocId: (int)path.Attribute("loc_id")!,
				Race: (string?)path.Attribute("race") ?? "PC_ALL", TitleId: (int?)path.Attribute("title_id") ?? 0)))];
	private static (int Npc, int LocId, string Race, int TitleId)[]? groupPortalUses;

	/// <summary>M6: the mesh materials whose skill is lava (material_templates.xml; Lava Singe and Lava, 150 fire damage every
	/// three seconds): the crater floor south of Slag Bulwark, where the group fought for Q4311 and died to it.</summary>
	private static HashSet<int> LavaMaterials(NaturalJourneyRuntime runtime) =>
		lavaMaterials ??= [.. System.Xml.Linq.XDocument.Load(Path.Combine(runtime.RepoRoot, "game-server", "data", "static_data", "mesh_materials", "material_templates.xml"))
			.Root!.Elements("material").Where(material => material.Elements("skill").Any(skill =>
				runtime.Data.SkillDataDh.GetSkillTemplate((int)skill.Attribute("id")!)?.GetName()?.Contains("Lava", StringComparison.OrdinalIgnoreCase) == true))
			.Select(material => (int)material.Attribute("id")!)];
	private static HashSet<int>? lavaMaterials;

	/// <summary>M6: the monsters that blow themselves up (an NPC skill named Explosion in npc_skills.xml): the crater floor's
	/// flame shards killed all six at once, 2,600 each, while the group fought an ember spirit beside them.</summary>
	private static HashSet<int> Exploders(NaturalJourneyRuntime runtime) =>
		exploders ??= [.. System.Xml.Linq.XDocument.Load(Path.Combine(runtime.RepoRoot, "game-server", "data", "static_data", "npc_skills", "npc_skills.xml"))
			.Root!.Elements("npc_skills").Where(entry => entry.Elements("npc_skill").Any(skill =>
				runtime.Data.SkillDataDh.GetSkillTemplate((int)skill.Attribute("id")!)?.GetName() == "Explosion"))
			.SelectMany(entry => ((string)entry.Attribute("npc_ids")!).Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse))];
	private static HashSet<int>? exploders;

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
		// NL-2: a field group plays Brusthonin's group quests too, from the Settlers Campsite.
		if (world.MapId != NaturalAbyssEntry.Morheim && !(field && world.MapId == NaturalAltgardContract.Brusthonin))
			throw new InvalidDataException("The group starts in Morheim.");
		var geometry = runtime.CreateGeometry();
		// M6: a campaign quest of the field list has its own handler and no plan (Q24021, Q24022). The group does its kills
		// and collections for the members that have it, and each member's own leg does its talks: its collections come
		// from the server's quest data, its named kill from the round.
		IReadOnlyDictionary<int, QuestRunPlan> fieldPlans = field ? NaturalAltgardContract.LoadGroupFieldPlans() : new Dictionary<int, QuestRunPlan>();
		QuestRunPlan Campaign(int id) => new(1, id, runtime.Data.Quests.GetQuestById(id)?.GetName() ?? throw new InvalidDataException($"Q{id} has no quest data."),
			null, null, 0, [], "custom", new QuestRunTrigger("none", 0, 0, null, 0, []), [], [], [], false);
		QuestRunPlan[] quests = ntc ? [.. GroupNtcQuests.Select(id => NaturalAltgardContract.LoadGroupNtcPlans()[id])]
			: field ? [.. options.GroupQuests!.Select(id => fieldPlans.TryGetValue(id, out var plan) ? plan : Campaign(id))]
			: [NaturalAltgardContract.LoadMusterPlans().Values.Single()];
		bool IsCampaign(QuestRunPlan plan) => plan.Template == "custom";
		QuestRunOperation[] CampaignTasks(int id)
		{
			var data = runtime.Data.Quests.GetQuestById(id)!;
			var drops = data.GetQuestDrop() ?? [];
			QuestRunOperation[] collect = [.. (data.GetCollectItems()?.GetCollectItem() ?? [])
				.Select(item => (Item: item, Sources: drops.Where(drop => drop.GetItemId() == item.GetItemId() && drop.GetNpcId() is int)
					.Select(drop => new QuestRunSource("questDrop", drop.GetNpcId(), null, 0, [], null)).ToArray()))
				.Where(entry => entry.Sources.Length > 0 && entry.Item.GetItemId() is int && entry.Item.GetCount() is int)
				.Select(entry => new QuestRunOperation(QuestRunOperationKind.CollectQuestDrop, entry.Item.GetCount()!.Value, entry.Item.GetItemId()!.Value, Sources: entry.Sources))];
			QuestRunOperation[] named = [.. (options.GroupKills ?? []).Where(kill => kill.Quest == id)
				.Select(kill => new QuestRunOperation(QuestRunOperationKind.Kill, 1, Sequence: -1 - kill.Var, Npcs: [new QuestRunNpc(kill.Npc, null, [], false)]))];
			return [.. named, .. collect];
		}
		// What a journal counts for each quest: its kills, then the items it collects.
		var tasks = quests.ToDictionary(plan => plan.Id, plan => IsCampaign(plan) ? CampaignTasks(plan.Id) : QuestRunBook.Build(plan).Operations.Where(op => op.Kind is
			QuestRunOperationKind.Kill or QuestRunOperationKind.CollectQuestDrop or QuestRunOperationKind.UseQuestObject).ToArray());
		// The coin stage's one quest; the NTC stage's first, whose NPCs stand on the starting map.
		QuestRunPlan quest = quests[0];
		QuestRunOperation[] kills = [.. tasks[quest.Id].Where(op => op.Kind == QuestRunOperationKind.Kill)];
		var navigator = new NaturalJourneyNavigator(session, BotNavigationGraphFactory.Build(runtime.Data, [quest.Id], geometry),
			geometry, runtime, stopOnDeath: false) { AvoidHostileAggro = true, RouteFromNearestMesh = field,
			Planner = leads || field ? BotTravelPlanner.For(world.MapId.Value, geometry, runtime.Data) : null };
		var combat = new NaturalJourneyCombat(session, navigator, runtime, geometry, stopOnDeath: false,
			conservativeRangedHold: false, NaturalMauPolicyParameters.Baseline, ClassLine) { ApproachMapId = world.MapId.Value };
		// M6: in the field a follower also skips ahead (the companions' rule): a way straight to its leader replaces what is
		// left of the trail when it is shorter. The trail of before a Return led the Sorcerer 18 m from its leader back down
		// the hill below the garrison.
		var following = new NaturalGroupFollowDriver(session, runtime) { Field = field, SkipAhead = field };
		combat.HoldsGround = field;
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
		// M6: the targets the field leader saw fall. The server gives a freed object id to the next object it spawns, and a
		// named monster's respawn can carry the id of its own corpse (Millennium Eka was waited for 80 minutes, unseen).
		var fallenTargets = new HashSet<int>();
		// Of those, the ones whose corpse has gone from sight: a fresh corpse is not always seen as one yet.
		var vanishedTargets = new HashSet<int>();
		var retriedTargets = new Dictionary<int, int>();
		// M6: the members this leader saw die since the group last gathered; the attackers of members this fighter failed to kill.
		var fallenMembers = new HashSet<int>();
		// M6: campaign quests whose monster no way reaches, left for later.
		var campaignLeft = new HashSet<int>();
		var protectFailures = new Dictionary<int, int>();
		// M6: the field gatherings in a row that could not gather; where the leader last said it stands, when it last said so,
		// and when a member that found no safe way to it looks again.
		int missedGatherings = 0;
		BotPosition? leaderHere = null;
		// M6: where this field leader last stood alive, where it fell when it stood up at the bind, and its walk back there;
		// and whether this member stood up at the bind and has not seen its leader since.
		BotPosition? lastAlive = null, fellAt = null;
		// M10: and the maps of those two places: inside Fire Temple the leader stands up at the bind outside.
		int? lastAliveMap = null, fellMap = null;
		// walkingBack: the leader's own move must not start a gathering inside itself; safeOnly: the leader alone takes only the
		// way out of every monster's reach (its walk back to where it fell), never the way through.
		bool walkingBack = false, safeOnly = false, revivedAtBind = false, sayRevived = false, crossing = false;
		IReadOnlyList<NaturalFlyZone> flyZones = [];
		int? flyZonesMap = null;
		long? lastTakeoff = null;
		(int X, int Y, int Z)? flownTo = null;
		int flyTries = 0;
		long portalAgainAt = 0, offLavaAgainAt = 0;
		var badDryGround = new List<BotPosition>();
		var refusedFlights = new List<(BotPosition From, BotPosition To, long Until)>();
		var flightRings = new NaturalFlightRings(runtime.RepoRoot);
		long hereSaidAt = long.MinValue / 2, leaderHereAt = long.MinValue / 2, rejoinAgainAt = 0, plannedWayAt = 0;
		(BotPosition From, BotPosition To)? plannedNoWay = null;
		long backOnMeshAt = 0, leaderSeenAt = 0;
		BotPosition? lastLeaderAt = null;
		// M6: when this field leader last saw each member.
		var memberSeenAt = new Dictionary<int, long>();
		// M6: a member that has not seen its leader for a minute, or that stood up at the bind, is apart from its group.
		bool Apart() => revivedAtBind || runtime.NowMillis - leaderSeenAt > 60_000;
		// M6: the field targets the leader went in to through other monsters' reach, once each.
		var targetsGoneThrough = new HashSet<int>();
		using var healer = options.Role == "healer" ? new NaturalGroupHealDriver(session, runtime, combat.ClassProfile) { CatchUp = field } : null;
		var loot = new NaturalGroupLootDriver(session, runtime, item => world.Quests.Values.Where(entry => entry.Status == 3)
			.SelectMany(entry => runtime.Data.Quests.GetQuestById(entry.QuestId)?.GetCollectItems()?.GetCollectItem() ?? [])
			.Any(required => required.GetItemId() == item && ItemCount(world, item) < required.GetCount()));
		var rewardItems = new Dictionary<int, long>();

		async Task SayAsync(string value) => await session.SendPacketAsync(session.Api.Say(tag + ":" + value, 5), token);
		// M6: a field quest a member handed in before this login is in its completed list, not its journal: done. The helpers
		// of a second group (the Spirit Master's and the Assassin's) have done the quests it plays.
		int Status(int? id = null) => world.Quests.GetValueOrDefault(id ?? quest.Id)?.Status ??
			(field && world.CompletedQuests.ContainsKey(id ?? quest.Id) ? 5 : 0);
		bool GoneOrKilled(int target) => !world.Objects.TryGetValue(target, out var seen) || seen.IsCorpse ||
			session.PacketHistory.TakeLast(4000).Any(packet => BotAttackStatus.CarriesHp(packet) &&
				packet.Get<int>("objectId") == target && packet.Get<byte>("hpOrMp") == 0);
		int[] Counts(QuestRunPlan? of = null)
		{
			QuestRunPlan plan = of ?? quest;
			// A campaign quest's work is only for a member that has it; its named kill is done once the quest has passed the
			// step that needs it (the step is in the task's sequence: -1 - var).
			if (IsCampaign(plan))
				return tasks[plan.Id].Select(task => Status(plan.Id) != 3 ? task.Count
					: task.Kind == QuestRunOperationKind.Kill ? (world.Quests[plan.Id].StepAndFlags & 63) > -1 - task.Sequence ? 1 : 0
					: (int)Math.Min(task.Count, ItemCount(world, task.ItemId))).ToArray();
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
				else if (ledWhenItSpoke && words[1] == "here" && words.Length == 5 && int.TryParse(words[2], out int hereX) &&
					int.TryParse(words[3], out int hereY) && int.TryParse(words[4], out int hereZ))
				{
					leaderHere = new BotPosition(hereX, hereY, hereZ, 0);
					leaderHereAt = runtime.NowMillis;
				}
				else if (words[1] == "place" && words.Length == 4 && int.TryParse(words[2], out int map) && int.TryParse(words[3], out int instance))
					places[sender] = (map, instance);
				else if (ledWhenItSpoke && words[1] == "command")
				{
					command = words[2];
					arguments = [.. words.Skip(3).Select(value => int.TryParse(value, out int number) ? number : 0)];
				}
				else if (ledWhenItSpoke && words[1] == "assist" && int.TryParse(words[2], out int target)) assist = target;
				// M6: a member that stood up at the bind says so. The leader does not see a death while it fights, and a member
				// back on its feet at the bind is no longer dead in the group list.
				else if (field && words[1] == "revived" && sender != session.CharacterId) fallenMembers.Add(sender);
			}
			chatCursor = session.PacketHistory.Count;
			if (sayRevived && !world.IsDead) { sayRevived = false; await SayAsync("revived:1"); }
			if (world.GroupLeaderId is int leading) leaderSeen = leading;
			if (field && !world.IsDead) { lastAlive = session.CurrentPosition; lastAliveMap = world.MapId; }
			if (field && world.GroupLeaderId is int chiefSeen && world.Objects.TryGetValue(chiefSeen, out var chiefNow))
			{
				revivedAtBind = false;
				leaderSeenAt = runtime.NowMillis;
				// M6: a leader that moved more than 50 m at once took a Return: the trail it left is behind it, and the follower
				// that walked it went back down the hill below the garrison.
				if (!leads && lastLeaderAt is { } before && Distance(before, chiefNow.Position) > 50) following.Reset();
				lastLeaderAt = chiefNow.Position;
			}
			if (field && leads) foreach (int member in world.GroupMembers.Keys.Where(world.Objects.ContainsKey)) memberSeenAt[member] = runtime.NowMillis;
			foreach (QuestRunPlan plan in quests) progress[(session.CharacterId, plan.Id)] = (Status(plan.Id), Counts(plan));
			// M6: in the field the leader says where it stands every twenty seconds, for a member that stood up at the bind; not on
			// its own way back from the bind, where its group would come to meet it.
			if (field && leads && !walkingBack && fellAt == null && world.GroupId != null && runtime.NowMillis - hereSaidAt >= 20_000)
			{
				hereSaidAt = runtime.NowMillis;
				BotPosition at = session.CurrentPosition;
				await SayAsync($"here:{(int)at.X}:{(int)at.Y}:{(int)at.Z}");
			}
			string report = "progress:" + string.Join('|', quests.Select(plan => $"{plan.Id}={Status(plan.Id)}={string.Join(',', Counts(plan))}"));
			if (world.GroupId != null && report != lastReport)
			{
				await SayAsync(report); lastReport = report;
				session.TraceDiagnostic("group-quest-progress", new Dictionary<string, object?>
				{ ["quest"] = quest.Id, ["status"] = Status(), ["counts"] = Counts(), ["journal"] = report });
			}
			// Where each member stands, map and instance: inside the camp, and inside a field group's instance (M10).
			if (ntc || options.GroupInstance != null)
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
			// M6: the field group gathers by its own rules. The wait is also asked before each step of a fight's approach, and a
			// fight is not left to gather the group.
			if (field)
			{
				if (!combat.InCombat) await FieldGatherAsync(ct);
				return;
			}
			bool clericDead = world.GroupMembers.Values.Any(member => member.PlayerClass == (byte)PlayerClass.CLERIC && member.Dead);
			// The bind is a place on one map. Inside an instance nobody is at it, and recovery there is not played yet.
			bool atBind = world.ObeliskBindPoint is { } point && point.MapId == world.MapId && Distance(session.CurrentPosition, point.Position) < 30;
			bool memberAtBind = world.ObeliskBindPoint is { } common && common.MapId == world.MapId && world.GroupMembers.Values.Any(member =>
				member.MapId == common.MapId && Distance(member.Position, common.Position) < 30 && Distance(session.CurrentPosition, member.Position) > 150);
			if (world.ObeliskBindPoint?.MapId == world.MapId &&
				(clericDead || memberAtBind || atBind && world.GroupMembers.Values.Any(member => Distance(session.CurrentPosition, member.Position) > 150)))
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
		// M6 (docs/natural-morheim-leveling.md): how a field group gathers, and what it does after a death.
		// - A member that dies waits for the Cleric's resurrection; failing that it stands up at the bind and walks back to
		//   where the leader says it stands (the follower's rejoin), and the group does not wait for it.
		// - The leader that stood up at the bind walks back to where it fell, where its group is.
		// - Without its Cleric (dead, or on its way back from the bind) the group holds where it is, up to fifteen minutes.
		// - The whole group goes back to the bind when half of it stands up there, or after two gatherings in a row that
		//   could not gather; a member that is late there is not waited for.
		// The Sorcerer's death had sent five members 700 m home on foot (Return still cooling), where they stuck; the Templar's
		// sent them all back for him, and the wait for them ran out.
		async Task FieldGatherAsync(CancellationToken ct)
		{
			// Nor on the way to a takeoff: its members go there on their own when told to fly, and the leader that waited for them
			// halfway stayed behind while they flew (gs-c2).
			if (walkingBack || crossing || world.GroupId == null) return;
			// A fight can carry the leader off the mesh (knocked back, or a retreat across a ledge), where no way leads to it and
			// none from it (the Templar north of the servile kalbak): back onto the mesh within twelve metres first.
			if (!world.IsDead && runtime.NowMillis >= backOnMeshAt && world.MapId is int onMap && geometry.SnapToGround(onMap, session.CurrentPosition) == null &&
				geometry.SnapToGround(onMap, session.CurrentPosition, 12) is { } meshNear)
			{
				backOnMeshAt = runtime.NowMillis + 30_000;
				session.TraceDiagnostic("group-field-back-on-mesh", new Dictionary<string, object?> { ["position"] = session.CurrentPosition, ["to"] = meshNear });
				// The move asks this gathering again before it starts: it is not asked twice.
				walkingBack = true;
				try { await navigator.MoveAsync([session.CurrentPosition, meshNear], ct); }
				finally { walkingBack = false; }
			}
			IEnumerable<BotGroupMember> others = world.GroupMembers.Values.Where(member => member.ObjectId != session.CharacterId);
			// Out of sight, a member's place in the group list is the one it had when the group formed (at the bind): a member
			// stands at the bind only when the leader saw it die and no longer sees it.
			foreach (BotGroupMember member in others.Where(member => member.Dead)) fallenMembers.Add(member.ObjectId);
			bool RevivedAway(BotGroupMember member) => fallenMembers.Contains(member.ObjectId) && member.Online && !member.Dead &&
				!world.Objects.ContainsKey(member.ObjectId);
			bool IsCleric(BotGroupMember member) => member.PlayerClass == (byte)PlayerClass.CLERIC;
			BotPosition? Seen(BotGroupMember member) => world.Objects.GetValueOrDefault(member.ObjectId)?.Position;
			// On a slope a follower stands a little below its leader, and a healer a little behind the others: near is 15 m on
			// the flat and 12 m of height.
			bool Near(BotPosition at) => MathF.Sqrt(MathF.Pow(at.X - session.CurrentPosition.X, 2) + MathF.Pow(at.Y - session.CurrentPosition.Y, 2)) <= 15 &&
				MathF.Abs(at.Z - session.CurrentPosition.Z) <= 12;
			if (fellAt is { } fell && !world.IsDead)
			{
				// The members that stood up at the bind before it did have said so (the whole group fell in the Ice Claw village,
				// and the Templar walked back there alone).
				await ObserveAsync();
				fellAt = null;
				// Only to a group that is still out there: when it fell with its leader it stands up at the bind as well.
				bool groupOut = others.Any(member => member.Online && !member.Dead && Seen(member) == null && !RevivedAway(member));
				if (groupOut && Distance(session.CurrentPosition, fell) > 40)
				{
					session.TraceDiagnostic("group-field-back-to-group", new Dictionary<string, object?> { ["position"] = session.CurrentPosition, ["to"] = fell });
					// Alone it takes only the way out of every monster's reach (it died twice fighting its way back alone); with no
					// such way it calls its group to the bind.
					walkingBack = true;
					safeOnly = true;
					bool back = false;
					try
					{
						// M10: it fell inside the instance and stood up at the bind outside: through the entrance first.
						if (options.GroupInstance is { } fellInside && fellMap == fellInside.Map && world.MapId != fellInside.Map)
							await EnterAsync(fellInside.Portal, fellInside.Map);
						if (fellMap is int fellOn && fellOn != world.MapId) throw new IOException($"The leader fell on map {fellOn} and stands on {world.MapId}.");
						await WalkAsync(fell, 15);
						back = true;
					}
					catch (IOException none) { session.TraceDiagnostic("group-field-back-to-group-failed", new Dictionary<string, object?> { ["reason"] = none.Message }); }
					finally { walkingBack = false; safeOnly = false; }
					// From wherever the way stopped it (the pass west of the garrison, after twenty minutes back and forth).
					if (!back && !world.IsDead && world.ObeliskBindPoint is { } bound && bound.MapId == world.MapId)
					{
						await SayAsync("command:regroup");
						if (Distance(session.CurrentPosition, bound.Position) >= 30)
						{
							walkingBack = true;
							try { await ReturnToBindAsync(); }
							catch (IOException) { }
							finally { walkingBack = false; }
						}
						try
						{
							await WaitAsync(() => others.Where(member => member.Online).All(member => !member.Dead && Seen(member) is { } at && Near(at)),
								"group recovery at the common bind", 1500);
						}
						catch (IOException late) { session.TraceDiagnostic("group-field-recovery-late", new Dictionary<string, object?> { ["reason"] = late.Message }); }
						await SayAsync("command:hunt");
						fallenMembers.Clear();
					}
				}
			}
			// Waited for: every member online but one on its way back from the bind, the Cleric always. An offline member is a
			// bot that stopped (the Ranger's stop held the leader at every wait).
			// Nor one out of sight for a minute: it walks back on its own (the Sorcerer, wandering below the garrison, held every
			// gathering in the Ice Claw village to its five minutes).
			bool Waited(BotGroupMember member) => member.Online && (IsCleric(member) ||
				!RevivedAway(member) && runtime.NowMillis - memberSeenAt.GetValueOrDefault(member.ObjectId, runtime.NowMillis) <= 60_000);
			bool Together() => others.Where(Waited).All(member => !member.Dead && member.MapId == world.MapId && Seen(member) is { } at && Near(at) &&
				(member.MaxMp <= 0 || member.CurrentMp * 100L / member.MaxMp >= 50));
			// Nobody gathers on lava.
			if (field) await StepOffLavaAsync();
			if (Together()) { missedGatherings = 0; return; }
			BotPosition stop = session.CurrentPosition;
			// An interrupted movement plan must announce its stop before waiting for stationary followers.
			await session.SendPacketAsync(session.Api.MoveTo(new(stop.X, stop.Y, stop.Z, stop.Heading, 0)), ct);
			session.TraceDiagnostic("group-regroup", new Dictionary<string, object?>
			{ ["position"] = stop, ["members"] = others.Select(member => new { member.Name, Position = Seen(member), member.HpPercent }).ToArray() });
			long gathering = runtime.NowMillis;
			bool cameBack = false;
			while (!Together())
			{
				// A leader that only waits lets a pack kill its group (twice by Kimssi's elites): whatever strikes a member
				// while the group gathers is killed first, by all.
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
				foreach (BotGroupMember member in others.Where(member => member.Dead)) fallenMembers.Add(member.ObjectId);
				bool clericAway = others.Any(member => IsCleric(member) && (member.Dead || RevivedAway(member)));
				// The Cleric that stood up at the bind waits there for its group (a lone walk across the map left it below the
				// garrison): the group goes to it, as it goes to half of itself.
				bool many = others.Count(RevivedAway) * 2 >= Math.Max(1, others.Count(member => member.Online)) ||
					others.Any(member => IsCleric(member) && RevivedAway(member));
				bool stuck = missedGatherings >= 1 && runtime.NowMillis - gathering > 300_000;
				if (world.ObeliskBindPoint?.MapId == world.MapId && (many || stuck))
				{
					missedGatherings = 0;
					session.TraceDiagnostic("group-field-recovery", new Dictionary<string, object?>
					{
						["position"] = session.CurrentPosition, ["many"] = many, ["stuck"] = stuck,
						["members"] = others.Select(member => new { member.ObjectId, member.Name, member.Online, member.Dead, seen = Seen(member) != null }).ToArray(),
					});
					await SayAsync("command:regroup");
					// Return, or the walk to the bind while it cools: the walk does not stop to gather the group it is leaving.
					walkingBack = true;
					try { await ReturnToBindAsync(); }
					catch (IOException none) { session.TraceDiagnostic("group-field-recovery-no-way", new Dictionary<string, object?> { ["reason"] = none.Message }); }
					finally { walkingBack = false; }
					try
					{
						// Up to twenty-five minutes: a member's Return may be cooling its twenty.
						await WaitAsync(() => others.Where(member => member.Online).All(member => !member.Dead && Seen(member) is { } at && Near(at)),
							"group recovery at the common bind", 1500);
					}
					catch (IOException late) { session.TraceDiagnostic("group-field-recovery-late", new Dictionary<string, object?> { ["reason"] = late.Message }); }
					await SayAsync("command:hunt");
					fallenMembers.Clear();
					gathering = runtime.NowMillis;
					continue;
				}
				// A member in sight and 60 m off at most that stays out of reach for half a minute may be one that cannot come:
				// the leader stands on ground its followers' mesh does not join (a fight left the Templar 16 m from the Gladiator
				// and the Cleric). The leader goes to it, once a gathering; a member farther off is on its way.
				if (!cameBack && runtime.NowMillis - gathering > 30_000 && others.Where(member => member.Online && !member.Dead)
					.Select(Seen).OfType<BotPosition>().Where(at => !Near(at) && Distance(session.CurrentPosition, at) <= 60)
					.OrderBy(at => Distance(session.CurrentPosition, at)).Cast<BotPosition?>().FirstOrDefault() is { } apart)
				{
					cameBack = true;
					var back = await navigator.FindRouteAsync(session.CurrentPosition, apart, ct);
					if (back.Count == 0) back = PlannedWay(apart);
					// As a player walks: straight over ground the baked mesh leaves out, to a member near and level enough.
					if (back.Count == 0 && MathF.Abs(session.CurrentPosition.Z - apart.Z) <= 8) back = [session.CurrentPosition, apart];
					session.TraceDiagnostic("group-field-come-back", new Dictionary<string, object?> { ["position"] = session.CurrentPosition, ["to"] = apart, ["route"] = back.Count });
					if (back.Count > 0)
					{
						// The move asks for a gathering before each step; this one is not nested in itself.
						walkingBack = true;
						try { await navigator.MoveAsync(back, ct); }
						finally { walkingBack = false; }
					}
					continue;
				}
				// A member that takes longer than five minutes is not waited for: the group goes on with those near (the Cleric
				// circled 12 m off for three minutes, and the wait ran out three seconds before it arrived). The Cleric on its
				// way back from the bind is waited for fifteen.
				if (runtime.NowMillis - gathering > (clericAway ? 900_000 : 300_000))
				{
					session.TraceDiagnostic("group-field-not-gathered", new Dictionary<string, object?>
					{
						["position"] = session.CurrentPosition, ["clericAway"] = clericAway,
						["members"] = others.Select(member => new { member.Name, member.Dead, seen = Seen(member) != null,
							distance = Seen(member) is { } at ? Distance(session.CurrentPosition, at) : -1 }).ToArray(),
					});
					if (!clericAway) missedGatherings++;
					return;
				}
				await PauseAsync();
			}
			missedGatherings = 0;
		}
		navigator.BeforeMovementAsync = WaitForFollowersAsync;
		// The member changed maps: checked movement and combat are bound to the map the client stands on now.
		void EnterMap()
		{
			geometry = runtime.CreateGeometry();
			// In the open a member keeps out of other monsters' circles on its way to a target. Inside the camp the group
			// fights the packs, and a way around them does not exist: nobody plans one there.
			navigator = new NaturalJourneyNavigator(session, BotNavigationGraphFactory.Build(runtime.Data, [quest.Id], geometry),
				geometry, runtime, stopOnDeath: false) { AvoidHostileAggro = world.MapId != GroupNtcMap && world.MapId != options.GroupInstance?.Map, RouteFromNearestMesh = field,
				Planner = leads || field ? BotTravelPlanner.For(world.MapId!.Value, geometry, runtime.Data) : null, BeforeMovementAsync = WaitForFollowersAsync };
			combat.EnterMap(navigator, geometry, world.MapId!.Value);
			following.Reset();
		}
		combat.AfterBindRevive = () =>
		{
			if (field && leads) { fellAt = lastAlive; fellMap = lastAliveMap; }
			if (field) revivedAtBind = true;
			if (field && !leads) sayRevived = true;
			EnterMap();
		};
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
						BotKnownObject[] foes = [.. world.Objects.Values.Where(known => striking.Contains(known.ObjectId) && known.Kind == BotKnownObjectKind.Npc && !known.IsCorpse &&
							!GoneOrKilled(known.ObjectId))];
						if (foes.Length == 0) break;
						// The Cleric never fights: it heals itself until the blows stop. Alone, before its group forms, it steps out
						// of their reach first (its Returns were cut short three times at Deputy Lamipedon's spawn, and it walked
						// on into a route that did not exist).
						if (healer != null && world.GroupId == null && world.MapId is int escapeMap)
						{
							BotPosition[] foesAt = [.. foes.Select(foe => foe.Position)];
							BotPosition here = session.CurrentPosition;
							if (geometry.IslandGround(escapeMap, here, at => Distance(at, here) is > 30 and < 60 && foesAt.All(foe => Distance(at, foe) > 40))
								.OrderByDescending(at => foesAt.Min(foe => Distance(at, foe))).Cast<BotPosition?>().FirstOrDefault() is { } away &&
								await navigator.FindRouteAsync(here, away, token) is { Count: > 0 } path)
							{
								session.TraceDiagnostic("group-field-healer-steps-away", new Dictionary<string, object?> { ["position"] = here, ["to"] = away });
								await navigator.MoveAsync(path, token);
								break;
							}
						}
						if (healer != null) { await healer.StepAsync(token); await PauseAsync(); continue; }
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
			// M6: Return still cooling from the last recovery (twenty minutes): a field member waits it out where it stands, the
			// leader killing what strikes the group, and casts it then. The walks home it took instead failed on the slope above
			// the garrison, 77 m from the bind, and the group went home twenty-three times in a round without getting there.
			if (field && world.Skills.ContainsKey(skill) && session.Api.Timing.TimeUntilCast(skill) > TimeSpan.Zero)
			{
				long readyAt = runtime.NowMillis + (long)session.Api.Timing.TimeUntilCast(skill).TotalMilliseconds + 500;
				session.TraceDiagnostic("group-field-return-wait", new Dictionary<string, object?> { ["position"] = session.CurrentPosition, ["waitMillis"] = readyAt - runtime.NowMillis });
				// Not on lava: the leader waited out its Return on the crater floor.
				while (runtime.NowMillis < readyAt && !world.IsDead)
					if (!(leads && await GuardAsync()) && !await StepOffLavaAsync()) await PauseAsync();
				if (world.IsDead) { await combat.RestAsync(token); following.Reset(); return; }
				await session.SynchronizeAsync(token);
			}
			if (!world.Skills.ContainsKey(skill) || session.Api.Timing.TimeUntilCast(skill) > TimeSpan.Zero)
			{
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
			try
			{
				// In the crater a goal on other ground is flown to at once: the leader walked the lava floor four minutes toward
				// Embla before its walk failed at the floor's edge.
				bool flyFirst = field && !crossing && !walkingBack && InFlyZone(session.CurrentPosition) && Island(session.CurrentPosition) is int fromGround &&
					fromGround >= 0 && Island(destination, 8) is int toGround && toGround >= 0 && toGround != fromGround;
				try { if (!flyFirst) { await WalkOnAsync(destination, within); return; } throw new IOException("Other ground in the crater."); }
				// Ground closed by a portal's wall (Karhel's aetheric field in the Ice Claw village): the leader names the portal,
				// and every member goes through it.
				catch (IOException) when (field && leads && !crossing && GatesTo(destination).Length > 0)
				{
					(int Npc, int Title)? gate = await ReachableGateAsync(destination);
					if (gate == null || !await PortalAsync(gate.Value.Npc, gate.Value.Title)) throw new IOException($"No portal took the leader toward {destination} ({(gate == null ? "none in reach" : $"portal {gate.Value.Npc}")}).");
				}
				// The crater south of Slag Bulwark is a FLY zone on ground the navmesh does not join to the rest: the leader names
				// the goal, and every member flies there on its own, as a bot alone does.
				// A member walking on its own (to a quest NPC, after standing up at the obelisk) flies the same way: the Sorcerer,
				// left at Slag Bulwark, never reached Padinerk to hand in Q2376.
				catch (IOException) when (field && !crossing && (NearFlyZone(destination) || InFlyZone(session.CurrentPosition)))
				{
					if (leads) await SayAsync($"command:fly:{(int)MathF.Round(destination.X)}:{(int)MathF.Round(destination.Y)}:{(int)MathF.Round(destination.Z)}");
					if (!await CrossFlyZoneAsync(destination)) throw new IOException($"No walk nor flight reaches {destination}.");
				}
				await WalkOnAsync(destination, within);
			}
			finally { navigator.AvoidHostileAggro = keptOut; }
		}
		// M6: the crater south of Slag Bulwark (Sarinerk's and Padinerk's ledges, the floor, the ruins) is Morheim's second FLY
		// zone, ground the navmesh does not join to the rest. A field member crosses it as a bot alone does (the Morheim legs'
		// flyAcross): on foot to the zone's ground nearest the goal, a flight, and a landing beside the goal; and out the same way.
		IReadOnlyList<NaturalFlyZone> FlyZones()
		{
			if (flyZonesMap != world.MapId && world.MapId is int map)
			{
				flyZones = NaturalFlyZone.ForMap(runtime.RepoRoot, runtime.Data.WorldMaps2.GetTemplate(map), map);
				flyZonesMap = map;
			}
			return flyZones;
		}
		// Two looks, at the point and three metres under it: the navmesh can stand a point on a surface above the lava a bot then
		// stands in (a ledge east of the drakes, 89.8 m over a lava patch at 86.9).
		bool OnLava(BotPosition at) => world.MapId is int lavaMap && (Lava(lavaMap, at) || Lava(lavaMap, at with { Z = at.Z - 3 }));
		bool Lava(int lavaMap, BotPosition at) => geometry.GroundMaterial(lavaMap, at) is int material && LavaMaterials(runtime).Contains(material);
		// A member standing on lava out of a fight steps onto the nearest ground that is not, three to twenty metres off; a step
		// that left it where it stood is not taken again (the members on the crater floor stepped two metres forty times).
		async Task<bool> StepOffLavaAsync()
		{
			if (combat.InCombat || world.IsDead || world.MapId is not int map || runtime.NowMillis < offLavaAgainAt || !OnLava(session.CurrentPosition)) return false;
			offLavaAgainAt = runtime.NowMillis + 10_000;
			BotPosition here = session.CurrentPosition;
			if (geometry.IslandGround(map, here, at => Distance(at, here) is > 3 and <= 20 && !OnLava(at) &&
					!badDryGround.Any(bad => Distance(bad, at) < 1)).OrderBy(at => Distance(at, here))
				.Cast<BotPosition?>().FirstOrDefault() is not { } dry) return false;
			var path = await navigator.FindRouteAsync(here, dry, token);
			if (path.Count == 0) path = [here, dry];
			session.TraceDiagnostic("group-field-off-lava", new Dictionary<string, object?> { ["position"] = here, ["to"] = dry });
			// The leader's own step must not start a gathering inside itself.
			bool wasWalkingBack = walkingBack;
			walkingBack = true;
			try { await navigator.MoveAsync(path, token); }
			finally { walkingBack = wasWalkingBack; }
			if (Distance(session.CurrentPosition, here) < 1 || OnLava(session.CurrentPosition)) badDryGround.Add(dry);
			return true;
		}
		bool InFlyZone(BotPosition at) => world.MapId != GroupNtcMap && FlyZones().Any(zone => !zone.Forbids && zone.Contains(at.X, at.Y, at.Z + 2)) &&
			!FlyZones().Any(zone => zone.Forbids && zone.Contains(at.X, at.Y, at.Z + 2));
		// M10: places a few metres off a FLY zone's edge, at its height: Fire Temple's entrance stands 7 m south of the crater
		// zone's edge, and Flamyar 1.4 m; the group flies to the zone's ground nearest them and walks the rest.
		IEnumerable<BotPosition> AroundFlyEdge(BotPosition at) => from radius in new[] { 4f, 8f, 12f, 16f } from step in Enumerable.Range(0, 16)
			select new BotPosition(at.X + radius * MathF.Cos(step * MathF.PI / 8), at.Y + radius * MathF.Sin(step * MathF.PI / 8), at.Z, 0);
		bool NearFlyZone(BotPosition at) => InFlyZone(at) || AroundFlyEdge(at).Any(InFlyZone);
		// M6: Protector Karhel stands in his aetheric field, which only the chosen enter (the operator, 2026-10-16: "You must
		// have <Tayga Slayer> in order to enter this area"). Its wall is collision the navmesh leaves closed; the way in is the
		// statue of the Mau hero beside it, a portal (Java PortalAI, portal_template2: title 58, Q24022 grants it at Landver's
		// step), and the way out the statue inside. Java's PortalService wants the title worn.
		// A spawn or a portal's landing can stand a few metres off the mesh (Karhel 2.7 m): those are looked up wider.
		int Island(BotPosition at, float snap = 2) => world.MapId is int map && geometry.NavMesh?.NavMeshes.Get(map) is { } mesh
			? mesh.IslandOf(at, BotNavQuery.Default with { SnapHorizontal = snap }) : -1;
		(int Npc, int Title, int StatueIsland, BotPosition Statue)[] GatesTo(BotPosition goal)
		{
			if (world.MapId is not int map) return [];
			// As the journey reads it: the client's own object, or an Asmodian's when it carries no race.
			Race race = world.Objects.GetValueOrDefault(session.CharacterId)?.Race is byte raceId ? (Race)raceId : Race.ASMODIANS;
			int here = Island(session.CurrentPosition), there = Island(goal, 8);
			// A portal that lands on the goal's ground (in: Karhel's), or one standing on the leader's own ground that leads off it
			// (out); one within 1,500 m to go in (the leader looks from the garrison, 815 m from Karhel's statue), 300 m to go out. The leader can stand on a patch of mesh of its own (east of the garrison, where its walk to
			// Karhel stopped): its own walk takes the group to the statue.
			if (there < 0 || here == there) return [];
			BotPosition from = session.CurrentPosition;
			return GroupPortalUses(runtime.RepoRoot)
				.Where(path => path.Race == race.ToString() || path.Race == "PC_ALL")
				.Select(path => (Path: path, Loc: runtime.Data.PortalLocDataDh.GetPortalLoc(path.LocId)))
				.Where(entry => entry.Loc != null && entry.Loc.GetWorldId() == map &&
					runtime.Data.SpawnsDh.GetSpawnsByWorldId(map).Any(group => group.GetNpcId() == entry.Path.Npc) && Distance(from, Spawn(entry.Path.Npc)) <= 1500)
				.Select(entry => (entry.Path, Landing: new BotPosition(entry.Loc!.GetX(), entry.Loc.GetY(), entry.Loc.GetZ(), 0), Statue: Spawn(entry.Path.Npc)))
				.Select(entry => (entry.Path, entry.Landing, entry.Statue, LandingIsland: Island(entry.Landing, 8), StatueIsland: Island(entry.Statue, 8)))
				.Where(entry => entry.StatueIsland >= 0 && entry.StatueIsland != there && entry.LandingIsland >= 0 &&
					(entry.LandingIsland == there || here >= 0 && entry.StatueIsland == here && entry.LandingIsland != here && Distance(from, entry.Statue) <= 300))
				.OrderBy(entry => entry.LandingIsland == there ? 0 : 1).ThenBy(entry => Distance(entry.Landing, goal))
				.Select(entry => (entry.Path.Npc, entry.Path.TitleId, entry.StatueIsland, entry.Statue)).ToArray();
		}
		// A statue the leader can walk to: on its own ground, or one a route reaches (the statue inside Karhel's field was chosen
		// for the way back to the flags, from the garrison; the travel planner's road does not reach the statue outside it).
		async Task<(int Npc, int Title)?> ReachableGateAsync(BotPosition goal)
		{
			int here = Island(session.CurrentPosition);
			foreach (var gate in GatesTo(goal))
			{
				if (gate.StatueIsland == here || (await navigator.FindRouteAsync(session.CurrentPosition, gate.Statue, token)).Count > 0)
					return (gate.Npc, gate.Title);
				session.TraceDiagnostic("group-field-portal-out-of-reach", new Dictionary<string, object?> { ["npc"] = gate.Npc, ["statue"] = gate.Statue });
			}
			return null;
		}
		// On the ground a portal lands on: the statue beside it, across its wall, is not used again.
		bool PortalLanded(int npc) => GroupPortalUses(runtime.RepoRoot).Where(path => path.Npc == npc)
			.Select(path => runtime.Data.PortalLocDataDh.GetPortalLoc(path.LocId)).OfType<Aion.GameServer.Model.Templates.Portal.PortalLoc>()
			.Any(loc => new BotPosition(loc.GetX(), loc.GetY(), loc.GetZ(), 0) is var landing && (Distance(session.CurrentPosition, landing) <= 10 ||
				Island(landing, 8) is int landed && landed >= 0 && landed == Island(session.CurrentPosition)));
		async Task<bool> PortalAsync(int npc, int title)
		{
			if (world.MapId is not int map) return false;
			BotPosition statue = Spawn(npc), before = session.CurrentPosition;
			session.TraceDiagnostic("group-field-portal", new Dictionary<string, object?> { ["npc"] = npc, ["title"] = title, ["position"] = before, ["statue"] = statue });
			if (Distance(session.CurrentPosition, statue) > 4 && leads)
			{
				try { await WalkOnAsync(statue, 3); }
				catch (IOException far) { session.TraceDiagnostic("group-field-portal-unreached", new Dictionary<string, object?> { ["npc"] = npc, ["reason"] = far.Message }); return false; }
			}
			if (Distance(session.CurrentPosition, statue) > 4)
			{
				var path = await navigator.FindRouteAsync(session.CurrentPosition, statue, token);
				if (path.Count == 0) path = geometry.FindInteractionPath(map, session.CurrentPosition, statue);
				if (path.Count == 0) return false;
				await navigator.MoveAsync(path, token);
			}
			await session.SynchronizeAsync(token);
			// A blow breaks the use bar (Java ActionItemNpcAI), and two ice claw scratchers stand by the statue: the leader kills
			// what strikes the group first. The members go through after it, from beside the statue (told on the way, two walked
			// there alone from 480 m off).
			for (int guard = 0; leads && guard < 12 && !world.IsDead && await GuardAsync(); guard++) { }
			if (world.IsDead) return false;
			if (leads) await SayAsync($"command:portal:{npc}");
			if (world.Objects.Values.Where(known => known.TemplateId == npc && Distance(session.CurrentPosition, known.Position) <= 10)
				.OrderBy(known => Distance(session.CurrentPosition, known.Position)).FirstOrDefault() is not { } gate) return false;
			if (title != 0)
			{
				int asked = session.PacketHistory.Count;
				await session.SendPacketAsync(GameClientPackets.SetDisplayTitle((ushort)title), token);
				await session.AdvanceAsync(TimeSpan.FromMilliseconds(500), token);
				await session.SynchronizeAsync(token);
				if (!session.PacketHistory.Skip(asked).Any(packet => packet.PacketType == typeof(SM_TITLE_INFO) && packet.Get<byte>("action") == 1 &&
					packet.Get<ushort>("titleId") == title))
				{
					session.TraceDiagnostic("group-field-portal-title-refused", new Dictionary<string, object?> { ["npc"] = npc, ["title"] = title });
					return false;
				}
			}
			await NaturalAltgardQuestSteps.UseObjectAsync(session, gate.ObjectId, null, token);
			await session.SynchronizeAsync(token);
			session.AcceptTeleportPosition();
			following.Reset();
			// Through is where the portal lands, alive: two members that died on the way stood up at the obelisk, on other ground.
			bool through = !world.IsDead && PortalLanded(npc);
			session.TraceDiagnostic("group-field-portal-used", new Dictionary<string, object?> { ["npc"] = npc, ["through"] = through, ["position"] = session.CurrentPosition });
			return through;
		}
		async Task<bool> CrossFlyZoneAsync(BotPosition goal)
		{
			if (crossing || world.MapId is not int map || world.IsDead) return false;
			// Ground this member can walk to lies within a few metres of the goal: no flight.
			if (geometry.IslandGround(map, session.CurrentPosition, at =>
				MathF.Abs(at.X - goal.X) <= 8 && MathF.Abs(at.Y - goal.Y) <= 8 && MathF.Abs(at.Z - goal.Z) <= 6).Count > 0) return false;
			IReadOnlyList<NaturalFlyZone> zones = FlyZones();
			bool Open(NaturalFlyZone zone, BotPosition at) => zone.Contains(at.X, at.Y, at.Z + 2) &&
				!zones.Any(other => other.Forbids && other.Contains(at.X, at.Y, at.Z + 2));
			NaturalFlyZone? zone = zones.FirstOrDefault(entry => !entry.Forbids && Open(entry, goal))
				?? zones.FirstOrDefault(entry => !entry.Forbids && Open(entry, session.CurrentPosition))
				?? zones.FirstOrDefault(entry => !entry.Forbids && AroundFlyEdge(goal).Any(at => Open(entry, at)));
			if (zone == null) return false;
			bool Inside(BotPosition at) => Open(zone, at) && at.Z + 12 < zone.Top && !OnLava(at);
			// The landing: the zone's ground on the goal's island nearest it, or the goal's own static ground where the navmesh
			// has none (Sarinerk stands past the edge of his ledge's mesh).
			BotPosition? landing = geometry.IslandGround(map, goal, Inside).OrderBy(at => Distance(at, goal)).Cast<BotPosition?>().FirstOrDefault();
			if (landing is not { } near || Distance(near, goal) > 6) landing = Inside(goal) ? geometry.StaticGroundAt(map, goal) ?? landing : landing;
			if (landing is not { } land) return false;
			// The takeoff: here inside the zone, or dry ground within thirty metres, or the zone's ground on this island nearest the
			// landing (from the lava floor the leader chose ground 120 m back and stuck on its way to it).
			BotPosition here = session.CurrentPosition;
			BotPosition? takeoff = Inside(here) ? here
				: geometry.IslandGround(map, here, at => Inside(at) && Distance(at, here) <= 30).OrderBy(at => Distance(at, here)).Cast<BotPosition?>().FirstOrDefault()
					?? geometry.IslandGround(map, here, Inside).OrderBy(at => Distance(at, land)).Cast<BotPosition?>().FirstOrDefault();
			// A flight refused from here to there is not planned again for a minute: each plan searches some 1,800 columns, and
			// the leader asked five times in a second under the rock of a crater ledge.
			if (refusedFlights.Any(refused => Distance(refused.From, here) <= 3 && Distance(refused.To, land) <= 3 && runtime.NowMillis < refused.Until))
				return false;
			session.TraceDiagnostic("group-field-fly", new Dictionary<string, object?>
			{ ["zone"] = zone.Name, ["goal"] = goal, ["position"] = here, ["takeoff"] = takeoff, ["landing"] = land });
			if (takeoff is not { } off) return false;
			crossing = true;
			try
			{
				if (Distance(here, off) > 3)
				{
					var path = await navigator.FindRouteAsync(here, off, token);
					if (path.Count == 0) return false;
					await navigator.MoveAsync(path, token);
				}
				if (world.IsDead || !Inside(session.CurrentPosition)) return false;
				// Higher when the way across is blocked, up to the zone's ceiling: a ridge between the obscuras' ledge and Padinerk
				// shut the leg at 159 m, three times.
				NaturalFlightRoute PlanFrom(BotPosition start)
				{
					float height = MathF.Min(zone.Top - 6, MathF.Max(start.Z, land.Z) + 15);
					NaturalFlightRoute planned = NaturalFlightProtocol.Plan(geometry, map, start, land, height);
					for (float higher = height + 20; !planned.IsUsable && higher <= zone.Top - 6; higher += 20)
						planned = NaturalFlightProtocol.Plan(geometry, map, start, land, higher);
					return planned;
				}
				NaturalFlightRoute route = PlanFrom(session.CurrentPosition);
				NaturalFlightRings.Route? ringPreview = !route.IsUsable ? flightRings.Plan(geometry, map, session.CurrentPosition,
					land, 6f, world.MaxFlightTime, world.MaxFlightTime, zones) : null;
				// From under an overhang the climb itself is shut (the Templar by the drakes' ledge): the zone's ground nearby, as
				// a player steps out before taking off.
				if (!route.IsUsable && ringPreview == null)
				{
					BotPosition standing = session.CurrentPosition;
					// Where its leader last stood first, which a flight has just left (the Sorcerer on a rock top under the skyrock east
					// of the drakes, three metres from where its leader took off), then ten places within sixty metres.
					// Nearest the landing first, as the leader chooses its takeoff: out at the ledge's edge, not under the rock.
					IEnumerable<BotPosition> steps = geometry.IslandGround(map, standing, at => Inside(at) && Distance(at, standing) is > 4 and <= 60)
						.OrderBy(at => Distance(at, land)).Take(10);
					if (!leads && lastLeaderAt is { } leaderWas && Distance(leaderWas, standing) <= 60 && Inside(leaderWas)) steps = steps.Prepend(leaderWas);
					int looked = 0, plannable = 0;
					foreach (BotPosition step in steps)
					{
						looked++;
						if (Distance(step, standing) < 1 || !PlanFrom(step).IsUsable) continue;
						plannable++;
						var stepPath = await navigator.FindRouteAsync(standing, step, token);
						if (stepPath.Count == 0) continue;
						await navigator.MoveAsync(stepPath, token);
						route = PlanFrom(session.CurrentPosition);
						break;
					}
					// A rock top whose own ground is all under the rock above: off its edge onto its leader's ground, within twenty
					// metres, as a player steps down (the Sorcerer was refused three times there while its group flew on).
					if (!route.IsUsable && !leads && lastLeaderAt is { } below)
						foreach (BotPosition down in geometry.IslandGround(map, below, at => Inside(at) && Distance(at, standing) <= 20)
							.OrderBy(at => Distance(at, standing)).Take(6))
						{
							looked++;
							if (!PlanFrom(down).IsUsable) continue;
							plannable++;
							await navigator.MoveAsync([standing, down], token);
							route = PlanFrom(session.CurrentPosition);
							break;
						}
					session.TraceDiagnostic("group-field-fly-step-out", new Dictionary<string, object?>
					{ ["position"] = standing, ["looked"] = looked, ["plannable"] = plannable, ["usable"] = route.IsUsable });
				}
				if (!route.IsUsable && ringPreview == null)
				{
					session.TraceDiagnostic("group-field-fly-refused", new Dictionary<string, object?> { ["reason"] = route.Refusal });
					refusedFlights.Add((here, land, runtime.NowMillis + 60_000));
					return false;
				}
				// The flight time this flight needs, not a full bar: after four flights in the crater the members waited for theirs
				// to fill while their leader flew on, and the group did not gather. Six metres a second is slower than any flight.
				int needed = ringPreview != null ? world.MaxFlightTime : Math.Min(world.MaxFlightTime,
					(int)MathF.Ceiling(route.Meters / 6f) + NaturalFlightPolicy.LandingReserveFp);
				long wait = NaturalFlightPolicy.RestoreMillis(world.CurrentFlightTime, needed, world.MaxFlightTime);
				if (lastTakeoff is long last) wait = Math.Max(wait, last + NaturalFlightPolicy.TakeoffReuseMillis - runtime.NowMillis);
				if (wait > 0)
				{
					session.TraceDiagnostic("group-field-fly-wait", new Dictionary<string, object?>
					{ ["fp"] = world.CurrentFlightTime, ["maxFp"] = world.MaxFlightTime, ["needed"] = needed, ["waitMillis"] = wait });
					await session.AdvanceAsync(TimeSpan.FromMilliseconds(wait + 100), token);
				}
				await session.SynchronizeAsync(token);
				BotPosition from = session.CurrentPosition;
				lastTakeoff = runtime.NowMillis;
				float speed;
				try { speed = await NaturalFlightProtocol.TakeOffAsync(session, token); }
				catch (IOException refused)
				{
					session.TraceDiagnostic("group-field-fly-refused", new Dictionary<string, object?> { ["reason"] = refused.Message });
					return false;
				}
				NaturalFlightDecision go = route.IsUsable
					? NaturalFlightPolicy.CanFly(NaturalFlightProtocol.ToPlan(route, from, speed), world.CurrentFlightTime, zones)
					: new(false, route.Refusal ?? "No direct flight.");
				if (!go.Allowed)
				{
					if (flightRings.Plan(geometry, map, from, land, speed, world.CurrentFlightTime, world.MaxFlightTime, zones) is not { } ringRoute)
					{
						session.TraceDiagnostic("group-field-fly-short", new Dictionary<string, object?> { ["reason"] = go.Reason });
						await NaturalFlightProtocol.LandAsync(session, token);
						await session.SynchronizeAsync(token);
						return false;
					}
					if (!await NaturalFlightRings.FlyAsync(session, geometry, zones, ringRoute, token)) return false;
				}
				else await NaturalFlightProtocol.FlyAsync(session, map, from, route.Waypoints, speed, token);
				await NaturalFlightProtocol.LandAsync(session, token);
				await session.SynchronizeAsync(token);
				// A step's stop on the ground where it landed: the others saw each member where it took off, 189 m below at the
				// crater's floor, and the leader waited five minutes at every landing for a group standing beside it.
				BotPosition landed = session.CurrentPosition;
				await session.SendPacketAsync(session.Api.MoveTo(new(landed.X, landed.Y, landed.Z, landed.Heading, 0)), token);
				await session.SynchronizeAsync(token);
				following.Reset();
				return true;
			}
			finally { crossing = false; }
		}
		async Task WalkOnAsync(BotPosition destination, float within)
		{
			var approach = new NaturalApproachProgress();
			int clears = 0, replans = 0;
			// The group fights its way: the monster that would wake nearest the way, first.
			async Task<bool> ClearWayAsync(string why)
			{
				if (clears >= 12 || Blocker(destination) is not int blocker) return false;
				clears++;
				await WaitForFollowersAsync(token);
				await SayAsync($"assist:{blocker}");
				session.TraceDiagnostic("group-field-clear-way", new Dictionary<string, object?>
				{ ["target"] = blocker, ["template"] = world.Objects.GetValueOrDefault(blocker)?.TemplateId, ["destination"] = destination, ["clears"] = clears, ["why"] = why });
				try
				{
					if (await combat.TryKillAsync(blocker, token) || GoneOrKilled(blocker)) navigator.UnavailableObjects.Add(blocker);
					else { navigator.UnavailableObjects.Add(blocker); navigator.LeftForAnother.Add(blocker); }
				}
				catch (NaturalCombatApproachBlockedException) { navigator.UnavailableObjects.Add(blocker); }
				await combat.RestAsync(token);
				return true;
			}
			for (int attempt = 0; Distance(session.CurrentPosition, destination) > within && approach.CanRetry(attempt) && approach.StalledAttempts < 3; attempt++)
			{
				var from = session.CurrentPosition;
				var route = await navigator.FindRouteAsync(from, destination, token);
				if (route.Count == 0 && field && leads && !safeOnly && await ClearWayAsync("no route"))
				{
					attempt--;
					continue;
				}
				if (route.Count == 0 && field && leads && !safeOnly && navigator.AvoidHostileAggro)
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
						if (field && world.MapId is int ground)
						{
							if (geometry.SnapToGround(ground, next) is { } under && under.Z <= next.Z + 2 && geometry.OnSameIsland(ground, at, under))
								next = under with { Heading = point.Heading };
							// A step that cuts a corner of the mesh lands off it, where no way leads (north of the servile kalbak the
							// leader stepped off, back on and off again for half an hour): the route's own point is on the mesh.
							else if (geometry.SnapToGround(ground, next) == null) next = point;
						}
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
				// The leader alone on the safe road gives up at the same unsafe step twice: it went back and forth for twenty
				// minutes at the pass west of the garrison, where its group had fought through.
				if (replan && field && leads && safeOnly && ++replans >= 2)
					throw new IOException("The safe road is shut by a monster's reach.");
				// M6: the same unsafe step twice (a klaw beside the way up to Kimssi; the walk gave up after three): the group
				// fights the monster nearest the way, or takes the way through and fights what wakes.
				if (replan && field && leads && !safeOnly && ++replans >= 2)
				{
					replans = 0;
					if (await ClearWayAsync("unsafe step")) { attempt--; continue; }
					if (navigator.AvoidHostileAggro)
					{
						navigator.AvoidHostileAggro = false;
						session.TraceDiagnostic("group-field-through", new Dictionary<string, object?> { ["destination"] = destination, ["reason"] = "unsafe step" });
						attempt--;
						continue;
					}
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
			// M6: a field leader that cannot reach a quest NPC (the way up to Kimssi after a wipe) takes the group to the bind and
			// walks again from there, twice.
			for (int tried = 0; ; tried++)
			{
				try { await WalkAsync(at); break; }
				catch (IOException none) when (field && leads && tried < 2 && world.ObeliskBindPoint is { } home && home.MapId == world.MapId)
				{
					session.TraceDiagnostic("group-field-npc-unreached", new Dictionary<string, object?> { ["npc"] = template, ["tried"] = tried, ["reason"] = none.Message });
					await SayAsync("command:regroup");
					walkingBack = true;
					try { await ReturnToBindAsync(); }
					catch (IOException) { }
					finally { walkingBack = false; }
					await SayAsync("command:hunt");
				}
			}
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
		// M6: a field group meets at its obelisk. Its members come from where their last round left them: the four escorts of
		// the garrison's second group stood 530 m east at Deputy Lamipedon's spawn, the two they escorted at the obelisk.
		for (int tries = 0; field && Distance(session.CurrentPosition, obelisk) > 30; tries++)
		{
			session.TraceDiagnostic("group-field-to-obelisk", new Dictionary<string, object?> { ["position"] = session.CurrentPosition, ["obelisk"] = obelisk, ["tries"] = tries });
			try { await ReturnToBindAsync(); }
			catch (IOException failed) when (tries < 5)
			{
				session.TraceDiagnostic("group-field-to-obelisk-failed", new Dictionary<string, object?> { ["reason"] = failed.Message });
				await PauseAsync();
			}
		}
		session.TraceDiagnostic("group-start", new Dictionary<string, object?>
		{ ["name"] = name, ["position"] = session.CurrentPosition, ["map"] = world.MapId, ["bind"] = world.ObeliskBindPoint, ["repeatBefore"] = repeatBefore });
		// In the field the leader is met at the obelisk: a leader seen far off is where it stood before its Return (the
		// Gladiator walked 400 m back east to it).
		if (!leads && world.Objects.Values.FirstOrDefault(known => known.Name == leaderName) is { } rendezvous &&
			Distance(session.CurrentPosition, rendezvous.Position) > 20 && !(field && Distance(rendezvous.Position, obelisk) > 30))
			await WalkAsync(rendezvous.Position, 3);
		if (leads)
		{
			foreach (string invited in roster.Where(member => member != name))
			{
				await WaitAsync(() => world.Objects.Values.Any(known => known.Name == invited && Distance(session.CurrentPosition, known.Position) <= 20), $"{invited} at the rendezvous");
				// M6: a field member can be asked while it still rests at the obelisk after standing up there, and miss the
				// question (the Cleric never answered): the leader asks again every minute, five times.
				for (int asked = 1; ; asked++)
				{
					await session.SendPacketAsync(session.Api.InviteToGroup(invited), token);
					try
					{
						await WaitAsync(() => world.GroupMembers.Values.Any(member => member.Name == invited && member.Online), $"{invited} to accept", field ? 60 : 120);
						break;
					}
					catch (IOException) when (field && asked < 5) { session.TraceDiagnostic("group-field-invite-again", new Dictionary<string, object?> { ["invited"] = invited, ["asked"] = asked }); }
				}
			}
		}
		else
		{
			// M6: a field member asked while it stood up at the obelisk saw the Soul Healer's question take the invitation's place
			// in its window, and the server asks nothing again while its first question waits (the Cleric never answered five):
			// it answers the invitation it was sent.
			var invitation = () => session.PacketHistory.LastOrDefault(packet => packet.PacketType == typeof(SM_QUESTION_WINDOW) &&
				packet.Get<int>("code") == 60000 && packet.Get<string[]>("params").Contains(leaderName));
			bool Asked() => world.Question?.Code == 60000 && world.Question.Parameters.Contains(leaderName);
			await WaitAsync(() => Asked() || field && invitation() != null, "leader's invitation");
			if (Asked()) await session.SendPacketAsync(session.Api.Answer(1), token);
			else await session.SendPacketAsync(GameClientPackets.QuestionResponse(60000, 1, invitation()!.Get<int>("senderId")), token);
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
			// NL-2: a giver that walks (Vinduer, round the Settlers Campsite) is looked for farther than the follow distance and
			// met where it stops, as a player waits for it to turn round; one that cannot be met says so. The leader stood 8 m
			// from Vinduer's last place and the followers farther, every walk to him found no way to that old place, and each
			// gave up without a word: the group waited fifteen minutes for Q4017's hand-ins that nobody tried.
			BotKnownObject? Giver() => world.Objects.Values.Where(known => known.TemplateId == npc && !known.IsCorpse &&
				Distance(session.CurrentPosition, known.Position) <= 40).OrderBy(known => Distance(session.CurrentPosition, known.Position)).FirstOrDefault();
			var giver = Giver();
			for (int wait = 0; wait < 24 && giver?.MoveTarget != null; wait++)
			{
				await PauseAsync();
				giver = Giver();
			}
			if (giver == null)
			{
				session.TraceDiagnostic("group-quest-npc-absent", new Dictionary<string, object?>
				{ ["quest"] = quest.Id, ["npc"] = npc, ["finish"] = finish, ["position"] = session.CurrentPosition });
				return;
			}
			// Following stops a little behind F1. Each member still needs its own short NPC interaction approach.
			if (Distance(session.CurrentPosition, giver.Position) > 4)
			{
				var path = geometry.FindInteractionPath(world.MapId!.Value, session.CurrentPosition, giver.Position);
				if (path.Count == 0)
				{
					session.TraceDiagnostic("group-quest-npc-unreached", new Dictionary<string, object?>
					{ ["quest"] = quest.Id, ["npc"] = npc, ["finish"] = finish, ["position"] = session.CurrentPosition, ["giver"] = giver.Position });
					return;
				}
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
			ushort dialog = options.GroupInstance is { } instanced && destination == instanced.Map ? (ushort)instanced.Dialog : GroupNtcEnterDialog;
			await NaturalCapitalSteps.PortalAsync(session, new NaturalCapitalPortal(portal, world.MapId!.Value, destination, 0, dialog), gate.ObjectId, token);
			EnterMap();
			beenInside |= world.MapId == GroupNtcMap || world.MapId == options.GroupInstance?.Map;
			session.TraceDiagnostic("group-entered", new Dictionary<string, object?>
			{ ["map"] = world.MapId, ["instance"] = world.InstanceId, ["position"] = session.CurrentPosition });
			await ObserveAsync();
		}
		// M10: out of the field group's instance by its exit portal (Java PortalAI, a use bar), which puts an Asmodian beside
		// the entrance outside; a use a blow broke is tried again.
		async Task ExitAsync(int exitNpc, int inside)
		{
			for (int tries = 0; tries < 5 && world.MapId == inside && !world.IsDead; tries++)
			{
				BotKnownObject? Gate() => world.Objects.Values.Where(known => known.TemplateId == exitNpc && Distance(session.CurrentPosition, known.Position) <= 15)
					.OrderBy(known => Distance(session.CurrentPosition, known.Position)).FirstOrDefault();
				if (Gate() == null)
				{
					try { await WalkAsync(Spawn(exitNpc), 3); }
					catch (IOException far) { session.TraceDiagnostic("group-exit-unreached", new Dictionary<string, object?> { ["npc"] = exitNpc, ["reason"] = far.Message }); }
					await session.SynchronizeAsync(token);
				}
				if (Gate() is not { } gate) { await PauseAsync(); continue; }
				if (Distance(session.CurrentPosition, gate.Position) > 4)
				{
					var path = geometry.FindInteractionPath(inside, session.CurrentPosition, gate.Position);
					if (path.Count > 0) await navigator.MoveAsync(path, token);
				}
				for (int guard = 0; guard < 12 && !world.IsDead && await GuardAsync(); guard++) { }
				if (world.IsDead) return;
				session.BeginStep("group-exit", $"npc{exitNpc}");
				int start = session.PacketHistory.Count;
				if (!await NaturalAltgardQuestSteps.UseObjectAsync(session, gate.ObjectId, null, token, reloadWorld: true))
				{
					session.TraceDiagnostic("group-exit-interrupted", new Dictionary<string, object?> { ["npc"] = exitNpc, ["tries"] = tries });
					await session.SynchronizeAsync(token);
					continue;
				}
				if (!session.PacketHistory.Skip(start).Any(packet => packet.PacketType == typeof(SM_PLAYER_SPAWN)))
					await session.WaitForPacketAsync(typeof(SM_PLAYER_SPAWN), token);
				if (!session.PacketHistory.Skip(start).Any(packet => packet.PacketType == typeof(SM_PLAYER_INFO) && packet.Get<int>("objectId") == session.CharacterId))
					await session.WaitForPacketAsync(typeof(SM_PLAYER_INFO), token, packet => packet.Get<int>("objectId") == session.CharacterId);
				session.AcceptTeleportPosition();
				await session.SynchronizeAsync(token);
				EnterMap();
				session.TraceDiagnostic("group-exited", new Dictionary<string, object?> { ["map"] = world.MapId, ["position"] = session.CurrentPosition });
				await ObserveAsync();
			}
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
		// M6: the travel planner's way to a near point, which the navigator asks it for only past 100 m. Around Kellan's Cabin
		// the leader's road crossed onto mesh no local way joins, and leader and group stood 17 m apart with no way either way.
		IReadOnlyList<BotPosition> PlannedWay(BotPosition to) =>
			navigator.Planner?.Plan(session.CurrentPosition, to, world.Level) is { Route.Count: > 0 } plan ? plan.Route : [];
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
				// Revived at the bind while the group is inside the camp, or inside the field group's instance (M10): back through
				// the portal, whatever F1 said last.
				int? insideMap = ntc ? GroupNtcMap : options.GroupInstance?.Map;
				int insidePortal = ntc ? GroupNtcVallack : options.GroupInstance?.Portal ?? 0;
				if (beenInside && insideMap is int inside && world.MapId != inside && world.GroupLeaderId is int within &&
					world.GroupMembers.TryGetValue(within, out var itsLeader) && itsLeader.Online && itsLeader.MapId == inside)
				{
					await EnterAsync(insidePortal, inside);
					if (world.MapId != inside) await PauseAsync();
					continue;
				}
				if (command == "regroup" && !returnedForRegroup)
				{
					// M6: a field member with Return cooling walks to the bind on a way out of every monster's reach, or stays
					// where it is when there is none (it rejoins its leader later).
					try { await ReturnToBindAsync(); }
					catch (IOException none) when (field) { session.TraceDiagnostic("group-field-regroup-failed", new Dictionary<string, object?> { ["reason"] = none.Message }); }
					returnedForRegroup = true;
				}
				if (command != "regroup") returnedForRegroup = false;
				if (command is "accept" or "turn-in" && arguments is [int about, int npc] && quests.FirstOrDefault(plan => plan.Id == about) is { } asked)
				{
					if (command == "accept" && Status(about) is not (3 or 4 or 5)) await DialogQuestAsync(asked, npc, false);
					else if (command == "turn-in" && Status(about) is 3 or 4 && OwnTasksDone(asked)) await DialogQuestAsync(asked, npc, true);
				}
				else if (command == "enter" && arguments is [int portal, int destination] && world.MapId != destination) await EnterAsync(portal, destination);
				else if (field && command == "exit" && arguments is [int exitNpc, int fromMap] && world.MapId == fromMap && !world.IsDead) await ExitAsync(exitNpc, fromMap);
				// M6: the leader went through a portal: a member on the statue's ground goes through it too.
				else if (field && command == "portal" && arguments is [int gateNpc] && !world.IsDead && world.MapId is int gateMap &&
					runtime.Data.SpawnsDh.GetSpawnsByWorldId(gateMap).Any(group => group.GetNpcId() == gateNpc) &&
					Distance(session.CurrentPosition, Spawn(gateNpc)) <= 60 && !PortalLanded(gateNpc) &&
					runtime.NowMillis >= portalAgainAt)
				{
					int gateTitle = GroupPortalUses(runtime.RepoRoot).FirstOrDefault(path => path.Npc == gateNpc).TitleId;
					if (!await PortalAsync(gateNpc, gateTitle)) portalAgainAt = runtime.NowMillis + 30_000;
				}
				// M6: the leader flew to a goal in the crater's FLY zone: the member flies there too, three tries a goal.
				else if (field && command == "fly" && arguments is [int flyX, int flyY, int flyZ] && flownTo != (flyX, flyY, flyZ) && !world.IsDead)
				{
					if (await CrossFlyZoneAsync(new BotPosition(flyX, flyY, flyZ, 0)) || ++flyTries >= 3) { flownTo = (flyX, flyY, flyZ); flyTries = 0; }
				}
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
				if (field) await StepOffLavaAsync();
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
				// M6: a member that stood up at the bind and does not see its leader walks to where the leader last said it
				// stands, on the planner's road and out of every monster's reach: the Sorcerer, on its trail through the klaw
				// camp, died three times on the way back with no Cleric near.
				if (field && !combat.InCombat && !world.IsDead && world.GroupLeaderId is int chief && !world.Objects.ContainsKey(chief) &&
					// Only to a place said in the last minute: one said before the leader's own death sent the Cleric across the
					// klaw camp to where nobody stood.
					// Not alone across the map: a leader more than 150 m off is waited for where the member stands (at the bind),
					// for a lone walk of a kilometre stalled among monsters and left the Cleric and the Sorcerer below the garrison.
					leaderHere is { } there && runtime.NowMillis - leaderHereAt <= 60_000 && Apart() &&
					// M10: a place said inside the instance is not one on this map.
					!(places.TryGetValue(chief, out var chiefPlace) && chiefPlace.Map != (world.MapId ?? 0)) &&
					Distance(session.CurrentPosition, there) is > 40 and <= 150 &&
					runtime.NowMillis >= rejoinAgainAt)
				{
					session.TraceDiagnostic("group-field-rejoin", new Dictionary<string, object?> { ["position"] = session.CurrentPosition, ["to"] = there });
					bool rejoined = false;
					try { await WalkAsync(there, 20); rejoined = true; }
					catch (IOException none)
					{
						rejoinAgainAt = runtime.NowMillis + 30_000;
						session.TraceDiagnostic("group-field-rejoin-wait", new Dictionary<string, object?> { ["reason"] = none.Message });
					}
					// A leader in the crater's FLY zone is flown to.
					if (!rejoined && InFlyZone(there) && await CrossFlyZoneAsync(there)) rejoined = true;
					if (rejoined) following.Reset();
				}
				// M6: a follower that finds no way to the leader it sees takes the planner's (around Kellan's Cabin, 17 m apart).
				// The searches are the costliest there are, and one that failed held the world: not again from the same place to
				// the same place, and not more often than every twenty seconds.
				if (field && following.BlockedTurns >= 6 && !combat.InCombat && !world.IsDead && runtime.NowMillis >= plannedWayAt &&
					world.GroupLeaderId is int seenChief && world.Objects.TryGetValue(seenChief, out var chiefSeen) &&
					Distance(session.CurrentPosition, chiefSeen.Position) > 12 &&
					!(plannedNoWay is { } stuckAt && Distance(stuckAt.From, session.CurrentPosition) <= 3 && Distance(stuckAt.To, chiefSeen.Position) <= 3))
				{
					plannedWayAt = runtime.NowMillis + 20_000;
					// A follower a fight carried off the mesh finds no way to anything (the Gladiator 17 m from its leader, which
					// found a way to it): back onto the mesh within twelve metres first.
					if (world.MapId is int standing && geometry.SnapToGround(standing, session.CurrentPosition) == null &&
						geometry.SnapToGround(standing, session.CurrentPosition, 12) is { } meshNear)
					{
						session.TraceDiagnostic("group-follow-back-on-mesh", new Dictionary<string, object?> { ["position"] = session.CurrentPosition, ["to"] = meshNear });
						await navigator.MoveAsync([session.CurrentPosition, meshNear], token);
					}
					IReadOnlyList<BotPosition> way = PlannedWay(chiefSeen.Position);
					// A leader a fight carried off the mesh has no way to it at all: the ground of this member's island nearest it,
					// within the 14 m that count as near.
					if (way.Count == 0 && world.MapId is int here && geometry.IslandGround(here, session.CurrentPosition,
							at => Distance(at, chiefSeen.Position) <= 14).OrderBy(at => Distance(at, chiefSeen.Position)).Cast<BotPosition?>().FirstOrDefault() is { } nearLeader &&
						Distance(nearLeader, session.CurrentPosition) > 3)
						way = geometry.FindJourneyPath(here, session.CurrentPosition, nearLeader);
					// Then the navigator's own search, waypoint graph included: the one the leader's way back to it came from.
					if (way.Count == 0) way = await navigator.FindRouteAsync(session.CurrentPosition, chiefSeen.Position, token);
					// Last, as a player walks: straight over the ground to a leader near and in sight (60 m). The baked mesh leaves
					// some ground out (all five stood 33 m behind their leader east of Kimssi, and 43 m east of the cabin, where no
					// search found a way either way).
					if (way.Count == 0 && Distance(session.CurrentPosition, chiefSeen.Position) <= 60 &&
						MathF.Abs(session.CurrentPosition.Z - chiefSeen.Position.Z) <= 8)
						way = [session.CurrentPosition, chiefSeen.Position];
					session.TraceDiagnostic("group-follow-planned-way", new Dictionary<string, object?> { ["to"] = chiefSeen.Position, ["points"] = way.Count });
					plannedNoWay = way.Count == 0 ? (session.CurrentPosition, chiefSeen.Position) : null;
					if (way.Count > 0) { await navigator.MoveAsync(way, token); following.Reset(); }
				}
				// M6: a member on its way back from the bind does not walk its leader's trail through what lies on it (the
				// Sorcerer died four times on the trail, 120 to 215 m behind): only the safe road, or it waits where it is.
				// Nor one apart for a minute: the trail leads where its leader stood before a Return (the Sorcerer walked it off a
				// drop, 190 m from the garrison, while the group waited for it there).
				// Nor onto lava after a leader fighting on it, when near enough to fight and heal from dry ground.
				bool leaderInLava = field && world.GroupLeaderId is int lavaChief && world.Objects.TryGetValue(lavaChief, out var lavaLeader) &&
					Distance(session.CurrentPosition, lavaLeader.Position) <= 25 && !OnLava(session.CurrentPosition) && OnLava(lavaLeader.Position);
				if (!(field && Apart() && world.GroupLeaderId is int away && !world.Objects.ContainsKey(away)) && !leaderInLava)
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
				// NL-2: the leader tries again while its own journal has not moved, as the members do on every look (a walking giver).
				for (int tries = 0; tries < 8 && (finish ? Status(plan.Id) is 3 or 4 : Status(plan.Id) is not (3 or 4 or 5)); tries++)
				{
					if (tries > 0) await session.AdvanceAsync(TimeSpan.FromSeconds(2), token);
					await DialogQuestAsync(plan, npc, finish);
				}
				// Fifteen minutes: a member that stood up at the obelisk may have to fly to a giver in the crater.
				await WaitAsync(() => Everybody(entry => finish ? entry.Status == 5 : entry.Status is 3 or 4 or 5, plan.Id),
					$"every member to {(finish ? "turn in" : "accept")} Q{plan.Id}", 900);
				session.TraceDiagnostic("group-field-step", new Dictionary<string, object?>
				{ ["quest"] = plan.Id, ["npc"] = npc, ["done"] = finish ? "turned-in" : "accepted", ["members"] = world.GroupMembers.Count });
			}
			// A target is not fought beside a monster that blows itself up, as a player pulls the ember spirit away from the flame
			// shards or leaves it.
			bool BesideExploder(BotPosition at) => world.Objects.Values.Any(known => known.TemplateId is int other && Exploders(runtime).Contains(other) &&
				!known.IsCorpse && Distance(known.Position, at) <= 12);
			// While a drop is collected, not where a corpse could not be reached (a ruins caryatid on a ledge fifty metres under the
			// rest: the Gladiator never came near enough to loot it, and the leader killed it there again and again).
			bool collecting = false;
			var unlootable = new List<BotPosition>();
			bool Unlootable(BotPosition at) => collecting && unlootable.Any(spot => Distance(spot, at) <= 8);
			int LivingTarget(int[] kinds) => world.Objects.Values.Where(known => known.TemplateId is int kind && kinds.Contains(kind) && !known.IsCorpse &&
				!navigator.UnavailableObjects.Contains(known.ObjectId) && !(field && BesideExploder(known.Position)) && !Unlootable(known.Position))
				.OrderBy(known => Distance(session.CurrentPosition, known.Position)).FirstOrDefault()?.ObjectId ?? 0;
			BotPosition[] exploderSpawns = [];
			int? exploderSpawnsMap = null;
			BotPosition[] ExploderSpawns()
			{
				if (exploderSpawnsMap != world.MapId && world.MapId is int exploderMap)
				{
					exploderSpawns = [.. runtime.Data.SpawnsDh.GetSpawnsByWorldId(exploderMap)
						.Where(group => Exploders(runtime).Contains(group.GetNpcId())).SelectMany(group => group.GetSpawnTemplates())
						.Select(spot => new BotPosition(spot.GetX(), spot.GetY(), spot.GetZ(), 0))];
					exploderSpawnsMap = exploderMap;
				}
				return exploderSpawns;
			}
			// M10: a group that plays in an instance takes all its quests outside first, plays them inside, kills its bosses
			// there, and hands the quests in outside: Flamyar stands beside Fire Temple's entrance.
			NaturalGroupInstance? instance = options.GroupInstance;
			// The leader that stood up at the bind outside goes back in (its walk back to where it fell does so first).
			async Task BackInsideAsync()
			{
				if (instance == null || world.MapId == instance.Map || world.IsDead) return;
				session.TraceDiagnostic("group-field-back-inside", new Dictionary<string, object?> { ["position"] = session.CurrentPosition, ["map"] = world.MapId });
				await EnterAsync(instance.Portal, instance.Map);
				if (world.MapId != instance.Map) throw new IOException($"The group leader could not go back into map {instance.Map}.");
			}
			if (instance != null)
			{
				foreach (QuestRunPlan plan in quests.Where(plan => !IsCampaign(plan) && !Everybody(entry => entry.Status == 5, plan.Id)))
					await TogetherFieldAsync(plan, plan.StartNpcs.First().Id, false);
				await WalkAsync(Spawn(instance.Portal), 4);
				await WaitForFollowersAsync(token);
				await SayAsync($"command:enter:{instance.Portal}:{instance.Map}");
				await EnterAsync(instance.Portal, instance.Map);
				if (world.MapId != instance.Map) throw new IOException($"The group leader did not enter map {instance.Map}.");
				await WaitAsync(() => world.GroupMembers.Keys.All(id => places.TryGetValue(id, out var inside) && inside == places[session.CharacterId]),
					"every member inside the leader's instance", 600);
				session.TraceDiagnostic("group-field-step", new Dictionary<string, object?>
				{ ["done"] = "entered", ["map"] = world.MapId, ["instance"] = world.InstanceId, ["members"] = world.GroupMembers.Count });
			}
			foreach (QuestRunPlan plan in quests)
			{
				if (Everybody(entry => entry.Status == 5, plan.Id)) continue;
				int giver = IsCampaign(plan) ? 0 : plan.StartNpcs.First().Id, end = plan.EndNpcs.FirstOrDefault()?.Id ?? giver;
				if (!IsCampaign(plan) && instance == null) await TogetherFieldAsync(plan, giver, false);
				QuestRunOperation[] work = tasks[plan.Id];
				for (int index = 0; index < work.Length; index++)
				{
					await BackInsideAsync();
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
					bool unreachable = false;
					collecting = task.Kind == QuestRunOperationKind.CollectQuestDrop;
					while (!Done())
					{
						await combat.RestAsync(token);
						await BackInsideAsync();
						await combat.BuffOurselfAsync(NaturalHelpTrigger.PrePull, token);
						BotPosition[] candidates = [.. kinds.SelectMany(kind => runtime.Data.SpawnsDh.GetSpawnsByWorldId(world.MapId!.Value)
							.Where(group => group.GetNpcId() == kind).SelectMany(group => group.GetSpawnTemplates()))
							.Select(spot => new BotPosition(spot.GetX(), spot.GetY(), spot.GetZ(), spot.GetHeading()))
							.OrderBy(spot => ExploderSpawns().Any(shard => Distance(shard, spot) <= 15) || Unlootable(spot) ? 1 : 0)
							.ThenBy(spot => Distance(session.CurrentPosition, spot))];
						int target = LivingTarget(kinds);
						bool cameNear = false;
						foreach (BotPosition spot in candidates.Take(8))
						{
							if (target != 0) break;
							// Once a monster comes into sight, its class combat driver owns the checked approach and pull.
							try { await WalkAsync(spot, 60); cameNear = true; }
							catch (IOException direct)
							{
								// M6: no way reaches Q2355's servile kalbak's spawn (the leader's run ended on it): the leader goes to
								// the ground of its own island nearest the spawn, and a spawn it cannot come near is left for the next.
								BotPosition? near = world.MapId is int map ? geometry.IslandGround(map, session.CurrentPosition, at => Distance(at, spot) <= 50)
									.OrderBy(at => Distance(at, spot)).Cast<BotPosition?>().FirstOrDefault() : null;
								session.TraceDiagnostic("group-field-spawn-unreachable", new Dictionary<string, object?>
								{ ["quest"] = plan.Id, ["spot"] = spot, ["near"] = near, ["reason"] = direct.Message });
								if (near is not { } ground) continue;
								try { await WalkAsync(ground, 5); cameNear = true; }
								catch (IOException) { continue; }
							}
							target = LivingTarget(kinds);
						}
						// A campaign quest's monster that no way reaches is left, and the round goes on: Q24022's Protector Karhel
						// stands on ground closed on every side (navmesh island 206 east of the Ice Claw village; every step onto it
						// meets collision).
						if (target == 0 && !cameNear && IsCampaign(plan))
						{
							session.TraceDiagnostic("group-field-campaign-unreachable", new Dictionary<string, object?>
							{ ["quest"] = plan.Id, ["task"] = index, ["kinds"] = kinds, ["position"] = session.CurrentPosition });
							unreachable = true;
							break;
						}
						// A living monster of the kind under the id of one that fell, and whose corpse went from sight, is its respawn.
						vanishedTargets.UnionWith(fallenTargets.Where(id => !world.Objects.ContainsKey(id)));
						if (target == 0 && field && world.Objects.Values.FirstOrDefault(known => known.TemplateId is int kind && kinds.Contains(kind) &&
							!known.IsCorpse && vanishedTargets.Contains(known.ObjectId)) is { } respawned)
						{
							fallenTargets.Remove(respawned.ObjectId);
							vanishedTargets.Remove(respawned.ObjectId);
							navigator.UnavailableObjects.Remove(respawned.ObjectId);
							navigator.LeftForAnother.Remove(respawned.ObjectId);
							failedTargets.Remove(respawned.ObjectId);
							session.TraceDiagnostic("group-field-respawn-seen", new Dictionary<string, object?> { ["quest"] = plan.Id, ["target"] = respawned.ObjectId, ["template"] = respawned.TemplateId });
							continue;
						}
						// A living target set aside after a fight that went wrong (the leader fell to Protector Karhel and the group
						// then waited for him to come back while he stood there) is tried again, five times. So is one counted as
						// fallen that is seen alive and never left sight: its kill was another's (gdk-c1). Each try may go in
						// through the monsters around it once.
						if (target == 0 && field && world.Objects.Values.FirstOrDefault(known => known.TemplateId is int kind && kinds.Contains(kind) &&
							!known.IsCorpse && navigator.UnavailableObjects.Contains(known.ObjectId) &&
							retriedTargets.GetValueOrDefault(known.ObjectId) < 5) is { } setAside)
						{
							retriedTargets[setAside.ObjectId] = retriedTargets.GetValueOrDefault(setAside.ObjectId) + 1;
							fallenTargets.Remove(setAside.ObjectId);
							targetsGoneThrough.Remove(setAside.ObjectId);
							navigator.UnavailableObjects.Remove(setAside.ObjectId);
							navigator.LeftForAnother.Remove(setAside.ObjectId);
							failedTargets.Remove(setAside.ObjectId);
							session.TraceDiagnostic("group-field-target-again", new Dictionary<string, object?> { ["quest"] = plan.Id, ["target"] = setAside.ObjectId, ["tries"] = retriedTargets[setAside.ObjectId] });
							await PauseAsync();
							continue;
						}
						if (target == 0)
						{
							// A named monster the other group (or this one) killed a moment ago: wait at its spawn for it to come
							// back (Q2494's Sku, Paku and Seku; Q2328's Millennium Eka, half an hour).
							// A campaign monster is waited for fifteen minutes (Protector Karhel comes back in five), then left for later
							// and the round goes on: the Cleric, dead at both of his deaths, waited out its eighty in g76.
							if (++waits > (IsCampaign(plan) ? 15 : 80))
							{
								if (!IsCampaign(plan)) throw new IOException($"No living target of Q{plan.Id} ({string.Join("/", kinds)}) came back.");
								session.TraceDiagnostic("group-field-campaign-left", new Dictionary<string, object?> { ["quest"] = plan.Id, ["task"] = index, ["kinds"] = kinds });
								unreachable = true;
								break;
							}
							session.TraceDiagnostic("group-field-wait-for-spawn", new Dictionary<string, object?> { ["quest"] = plan.Id, ["kinds"] = kinds, ["wait"] = waits });
							await session.AdvanceAsync(TimeSpan.FromSeconds(60), token);
							await ObserveAsync();
							vanishedTargets.UnionWith(fallenTargets.Where(id => !world.Objects.ContainsKey(id)));
							continue;
						}
						waits = 0;
						await WaitForFollowersAsync(token);
						await SayAsync($"assist:{target}");
						session.TraceDiagnostic("group-field-pull", new Dictionary<string, object?>
						{ ["quest"] = plan.Id, ["target"] = target, ["template"] = world.Objects.GetValueOrDefault(target)?.TemplateId, ["position"] = session.CurrentPosition });
						try
						{
							bool fell;
							try { fell = await combat.TryKillAsync(target, token) || GoneOrKilled(target); }
							catch (NaturalCombatApproachBlockedException first) when (!GoneOrKilled(target) && targetsGoneThrough.Add(target))
							{
								// M6: no approach to Q2355's servile kalbak keeps out of every other monster's reach, and the group
								// waited an hour for "another" one beside it alive: it goes in through them once and fights what wakes.
								session.TraceDiagnostic("group-field-through-target", new Dictionary<string, object?> { ["target"] = target, ["reason"] = first.Message });
								bool keptOut = navigator.AvoidHostileAggro;
								navigator.AvoidHostileAggro = false;
								try { fell = await combat.TryKillAsync(target, token) || GoneOrKilled(target); }
								finally { navigator.AvoidHostileAggro = keptOut; }
							}
							if (fell) { navigator.UnavailableObjects.Add(target); fallenTargets.Add(target); }
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
							// M6: a member opens a corpse within 15 m. The leader that pulled Millennium Eka from 50 m off left him where
							// he fell, and the two that needed his item never came near it: the leader goes to the corpse of a quest
							// drop's source, and its members with it.
							if (field && task.Kind == QuestRunOperationKind.CollectQuestDrop && world.Objects.TryGetValue(target, out var corpse) &&
								Distance(session.CurrentPosition, corpse.Position) > 6)
							{
								// In the crater a corpse can lie a drop below on ground of its own: flown to, as a player would.
								try { await WalkAsync(corpse.Position, 3); }
								catch (IOException far)
								{
									unlootable.Add(corpse.Position);
									session.TraceDiagnostic("group-field-corpse-unreached", new Dictionary<string, object?> { ["target"] = target, ["reason"] = far.Message });
								}
							}
							// Each member opens the corpse for the items that drop for it (drop_each_member); hold while they do.
							for (int wait = 0; wait < 60 && world.Objects.TryGetValue(target, out _); wait++)
							{
								await loot.StepAsync(token);
								await PauseAsync();
							}
						}
						await PauseAsync();
					}
					if (unreachable) { campaignLeft.Add(plan.Id); break; }
				}
				if (!IsCampaign(plan) && instance == null) await TogetherFieldAsync(plan, end, true);
				else session.TraceDiagnostic("group-field-step", new Dictionary<string, object?>
				{ ["quest"] = plan.Id, ["done"] = "worked", ["members"] = world.GroupMembers.Count });
			}
			if (instance != null)
			{
				foreach (NaturalGroupBoss boss in options.GroupBosses ?? []) await KillBossAsync(boss);
				// Out by the exit portal, the group with it, and the quests handed in outside.
				await BackInsideAsync();
				await WalkAsync(Spawn(instance.Exit), 4);
				await WaitForFollowersAsync(token);
				await SayAsync($"command:exit:{instance.Exit}:{instance.Map}");
				await ExitAsync(instance.Exit, instance.Map);
				if (world.MapId == instance.Map) throw new IOException($"The group leader could not leave map {instance.Map}.");
				await WaitAsync(() => world.GroupMembers.Keys.All(id => places.TryGetValue(id, out var outside) && outside.Map == world.MapId),
					"every member out of the instance", 600);
				session.TraceDiagnostic("group-field-step", new Dictionary<string, object?> { ["done"] = "left", ["map"] = world.MapId, ["members"] = world.GroupMembers.Count });
				foreach (QuestRunPlan plan in quests.Where(plan => !IsCampaign(plan) && !Everybody(entry => entry.Status == 5, plan.Id)))
					await TogetherFieldAsync(plan, plan.EndNpcs.FirstOrDefault()?.Id ?? plan.StartNpcs.First().Id, true);
			}
			// M10: a boss with no quest, where the instance handler spawns it: the group walks there, fighting what stands on the
			// way, and kills it; a fight that went wrong (a death, the leader at the bind) is tried again, five times.
			async Task KillBossAsync(NaturalGroupBoss boss)
			{
				BotPosition lair = new(boss.At[0], boss.At[1], boss.At[2], 0);
				for (int tries = 1; tries <= 5; tries++)
				{
					await BackInsideAsync();
					await combat.RestAsync(token);
					try { await WalkAsync(lair, 25); }
					catch (IOException far) { session.TraceDiagnostic("group-field-boss-unreached", new Dictionary<string, object?> { ["npcs"] = boss.Npcs, ["reason"] = far.Message }); }
					await WaitForFollowersAsync(token);
					int target = world.Objects.Values.Where(known => known.TemplateId is int kind && boss.Npcs.Contains(kind) && !known.IsCorpse)
						.OrderBy(known => Distance(session.CurrentPosition, known.Position)).FirstOrDefault()?.ObjectId ?? 0;
					if (target == 0)
					{
						session.TraceDiagnostic("group-field-boss-absent", new Dictionary<string, object?> { ["npcs"] = boss.Npcs, ["position"] = session.CurrentPosition, ["tries"] = tries });
						if (Distance(session.CurrentPosition, lair) <= 40) return;
						continue;
					}
					await combat.BuffOurselfAsync(NaturalHelpTrigger.PrePull, token);
					await SayAsync($"assist:{target}");
					session.TraceDiagnostic("group-field-boss", new Dictionary<string, object?> { ["target"] = target, ["template"] = world.Objects.GetValueOrDefault(target)?.TemplateId, ["tries"] = tries });
					bool fell = false;
					bool keptOut = navigator.AvoidHostileAggro;
					navigator.AvoidHostileAggro = false;
					try { fell = await combat.TryKillAsync(target, token) || GoneOrKilled(target); }
					catch (NaturalCombatApproachBlockedException blocked)
					{
						session.TraceDiagnostic("group-field-boss-blocked", new Dictionary<string, object?> { ["target"] = target, ["reason"] = blocked.Message });
					}
					finally { navigator.AvoidHostileAggro = keptOut; }
					if (fell)
					{
						session.TraceDiagnostic("group-field-boss-killed", new Dictionary<string, object?> { ["target"] = target, ["tries"] = tries });
						if (options.GroupLoot) for (int wait = 0; wait < 60 && world.Objects.ContainsKey(target); wait++) { await loot.StepAsync(token); await PauseAsync(); }
						return;
					}
				}
				session.TraceDiagnostic("group-field-boss-left", new Dictionary<string, object?> { ["npcs"] = boss.Npcs });
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
			// A campaign quest is worked here, not handed in: its giver and its end are the leg's (decision 11).
			int[] notDone = [.. quests.Where(plan => !IsCampaign(plan) && Status(plan.Id) != 5).Select(plan => plan.Id)];
			if (notDone.Length > 0) throw new InvalidDataException($"The group member has not handed in Q{string.Join(", Q", notDone)}.");
			// Every member goes home before it leaves, so the next round's group forms at the obelisk (the garrison's group
			// ended at Deputy Lamipedon's spawn, among monsters that cut the next round's Returns short).
			if (world.ObeliskBindPoint is { } home && home.MapId == world.MapId && Distance(session.CurrentPosition, home.Position) > 30)
			{
				try { await ReturnToBindAsync(); }
				catch (IOException late) { session.TraceDiagnostic("group-field-end-home-failed", new Dictionary<string, object?> { ["reason"] = late.Message }); }
			}
			// M6: what the round did is the server's once it stores the character, which it does when the player leaves: Q2494's
			// hand-in, seconds before its round ended, was missing from the world captured after it. The member leaves and comes
			// back, and its quests are still handed in.
			await session.QuitAsync(token);
			await session.WaitForReentryAsync(token);
			await session.ReloginExistingCharacterAsync(token);
			await session.EnterWorldAsync(token);
			await session.SynchronizeAsync(token);
			int[] lost = [.. quests.Where(plan => !IsCampaign(plan) && Status(plan.Id) != 5).Select(plan => plan.Id)];
			if (lost.Length > 0) throw new InvalidDataException($"After a relog Q{string.Join(", Q", lost)} is not handed in.");
			runtime.AssertClean();
			var played = new { verified = true, CharacterId = session.CharacterId, name, leader = world.GroupLeaderId, roster,
				quests = quests.Select(plan => plan.Id).ToArray(),
				campaign = quests.Where(IsCampaign).Select(plan => new { plan.Id, status = Status(plan.Id), counts = Counts(plan), left = campaignLeft.Contains(plan.Id) }).ToArray(),
				elapsedMillis = runtime.NowMillis - started, position = session.CurrentPosition,
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
