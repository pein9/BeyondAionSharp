using System.Xml;
using System.Xml.Serialization;
using Aion.Bots.Api;
using Aion.Bots.Protocol;
using Aion.Bots.Timing;
using Aion.Bots.World;
using Aion.GameServer.Model;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.Bots.Scenarios;

public interface IExtendedSocialDriver
{
	BotApi Api { get; }
	int CharacterId { get; }
	string CharacterName { get; }
	BotPosition CurrentPosition { get; }
	IReadOnlyList<DecodedBotServerPacket> PacketHistory { get; }
	Task StepAsync(string action, Func<CancellationToken, Task> operation, CancellationToken token);
	Task SendAsync(BotClientPacket packet, CancellationToken token);
	Task<DecodedBotServerPacket> WaitAsync(Type type, Func<DecodedBotServerPacket, bool> predicate, CancellationToken token);
	Task SynchronizeAsync(CancellationToken token);
	Task DelayAsync(TimeSpan duration, CancellationToken token);
	Task MoveAsync(BotPosition position, CancellationToken token);
	Task CompleteRecallTeleportAsync(CancellationToken token);
	Task LogoutAsync(CancellationToken token);
	Task ReenterAsync(CancellationToken token);
	Task VerifyExtendedSocialAsync(int legionId, CancellationToken token);
}

/// <summary>S7: ordinary group-finder, recall cast/accept and legion transactions after explicit director setup.</summary>
public static class ExtendedSocialScenario
{
	public const int MapId = 110010000;
	public const int RecallSkill = 3777;
	public const int RecallReagent = 169300011;
	public const int WarehouseNpc = 203751; // Pauton.
	public const int EmblemNpc = 203807; // Inofe.
	public const long SetupKinah = 6_000_000_000;
	public const long Deposit = 5_000_000_001;
	public const long Withdrawal = 3_000_000_001;
	public static readonly BotPosition WarehousePoint = new(1319.20f, 1405.43f, 575.27f, 97);
	public static readonly BotPosition EmblemPoint = new(1921.48f, 1396.94f, 590.381f, 55);
	private static readonly Lazy<ushort> RecallTiming = new(LoadRecallHitTime);
	public static ushort RecallHitTime => RecallTiming.Value;

	public static async Task RunAsync(IReadOnlyList<IExtendedSocialDriver> players, string legionName,
		Func<CancellationToken, Task> prepareLegionLevel, Func<CancellationToken, Task> prepareWarehousePosition,
		CancellationToken token = default)
	{
		Require(players.Count == 2 && players[0].CharacterId != players[1].CharacterId, "S7 requires two distinct subjects.");
		var leader = players[0]; var member = players[1];
		await SyncAsync(token);
		Require(players.All(p => p.Api.World.Level == 23 && p.Api.World.MapId == MapId && p.Api.World.GroupId == null), "S7 setup differs.");
		await StepAsync("group-finder-post-apply-update-remove", async ct =>
		{
			await leader.SendAsync(leader.Api.PostGroupRecruitment("S7 recruit"), ct);
			await leader.WaitAsync(typeof(SM_FIND_GROUP), p => p.Get<byte>("action") == 0, ct);
			await member.SendAsync(member.Api.PostGroupApplication("S7 apply", (byte)PlayerClass.SPIRIT_MASTER), ct);
			await member.WaitAsync(typeof(SM_FIND_GROUP), p => p.Get<byte>("action") == 4, ct);
			await CheckListingsAsync("S7 recruit", "S7 apply", ct);
			await leader.SendAsync(leader.Api.PostGroupRecruitment("S7 updated recruit", update: true), ct);
			await member.SendAsync(member.Api.PostGroupApplication("S7 updated apply", (byte)PlayerClass.SPIRIT_MASTER, update: true), ct);
			await SyncAsync(ct);
			await CheckListingsAsync("S7 updated recruit", "S7 updated apply", ct);
			byte serverId = leader.Api.World.GroupRecruitments[leader.CharacterId].ServerId;
			await leader.SendAsync(leader.Api.RemoveGroupListing(serverId: serverId), ct);
			await member.SendAsync(member.Api.RemoveGroupListing(application: true), ct);
			await SyncAsync(ct);
			foreach (var player in players)
			{
				Require(!player.Api.World.GroupRecruitments.ContainsKey(leader.CharacterId) && !player.Api.World.GroupApplications.ContainsKey(member.CharacterId), "Removal broadcasts left a listing.");
				foreach (bool applications in new[] { false, true }) await ListingsAsync(player, applications, ct);
				Require(!player.Api.World.GroupRecruitments.ContainsKey(leader.CharacterId) && !player.Api.World.GroupApplications.ContainsKey(member.CharacterId), "Removed listing returned on refresh.");
			}
		}, token);

		await StepAsync("form-party-and-cast-real-recall", async ct =>
		{
			await leader.SendAsync(leader.Api.InviteToGroup(member.CharacterName), ct);
			await member.WaitAsync(typeof(SM_QUESTION_WINDOW), p => p.Get<int>("code") == 60000, ct);
			await member.SendAsync(member.Api.Answer(1), ct);
			await member.WaitAsync(typeof(SM_GROUP_INFO), p => p.Get<int>("leaderId") == leader.CharacterId, ct);
			await SyncAsync(ct);
			foreach (var player in players) Require(player.Api.World.GroupMembers.Keys.Order().SequenceEqual(players.Select(p => p.CharacterId).Order()), "S7 party roster differs.");
			await member.MoveAsync(SocialBasicsScenario.Registrar with { X = SocialBasicsScenario.Registrar.X - 35 }, ct);
			await SyncAsync(ct);
			var destination = leader.CurrentPosition;
			Require(Distance(destination, member.CurrentPosition) > 25, "Recall must cover a meaningful distance.");
			Require(leader.Api.World.Skills.TryGetValue(RecallSkill, out var skill), "Spiritmaster did not learn Summon Group Member.");
			long reagentBefore = ItemCount(leader, RecallReagent);
			Require(reagentBefore >= 1, "Missing recall reagent.");
			int packetsBefore = member.PacketHistory.Count;
			await leader.SendAsync(leader.Api.Target(member.CharacterId), ct);
			await leader.SendAsync(leader.Api.Cast(new SpellCastData(RecallSkill, checked((byte)skill!.Level), 0)
				{ TargetObjectId = member.CharacterId, HitTime = RecallHitTime }), ct);
			var started = await leader.WaitAsync(typeof(SM_CASTSPELL), p => p.Get<int>("objectId") == leader.CharacterId && p.Get<ushort>("spellId") == RecallSkill, ct);
			await leader.DelayAsync(TimeSpan.FromMilliseconds(started.Get<ushort>("castDuration") + 1), ct);
			var result = await leader.WaitAsync(typeof(SM_CASTSPELL_RESULT), p => p.Get<int>("effectorId") == leader.CharacterId && p.Get<ushort>("skillId") == RecallSkill, ct);
			await leader.DelayAsync(TimeSpan.FromMilliseconds(result.Get<ushort>("hitTime") + 1), ct);
			await member.WaitAsync(typeof(SM_RECALLED_BY_OTHER), p => !p.Get<bool>("closed"), ct);
			Require(member.Api.World.RecallRequest == new BotRecallRequest(leader.CharacterName, RecallSkill, 30), "Wrong recall offer.");
			Require(Distance(destination, member.CurrentPosition) > 25, "Recall moved its target before consent.");
			await member.SendAsync(member.Api.AnswerRecall(true), ct);
			// Same-map NONE teleport is an immediate self-info refresh, not a fade-out/SM_TELEPORT_LOC handshake.
			await member.CompleteRecallTeleportAsync(ct);
			await SyncAsync(ct);
			Require(Distance(destination, member.CurrentPosition) < 0.1 && member.Api.World.MapId == MapId, "Accepted recall did not reach the caster.");
			Require(ItemCount(leader, RecallReagent) == reagentBefore - 1, "Recall did not consume exactly one reagent.");
			// Watch across the cancelled request's timeout, not just immediately after acceptance.
			for (int i = 0; i < 4; i++)
			{
				await leader.DelayAsync(TimeSpan.FromSeconds(8), ct); await SyncAsync(ct);
				Require(Distance(destination, member.CurrentPosition) < 0.1 && member.Api.World.RecallRequest == null, "Completed recall changed during timeout observation.");
				Require(ItemCount(leader, RecallReagent) == reagentBefore - 1, "Recall consumed another reagent after completion.");
				Require(member.PacketHistory.Skip(packetsBefore).Count(p => p.PacketType == typeof(SM_PLAYER_INFO) && p.Get<int>("objectId") == member.CharacterId) == 1, "Recall respawned its target more than once.");
			}
		}, token);

		int legionId = 0;
		await StepAsync("create-legion-and-invite-member", async ct =>
		{
			await leader.MoveAsync(SocialBasicsScenario.Registrar with { X = SocialBasicsScenario.Registrar.X - 1 }, ct);
			int npc = Npc(leader, SocialBasicsScenario.RegistrarId);
			await OpenDialogAsync(leader, npc, DialogAction.CREATE_LEGION, 2, ct);
			long before = leader.Api.World.Kinah;
			var oldEmblems = leader.Api.World.LegionEmblems.Keys.ToHashSet();
			await leader.SendAsync(leader.Api.CreateLegion(legionName), ct);
			await leader.WaitAsync(typeof(SM_SYSTEM_MESSAGE), p => p.Get<string?>("name") == "STR_GUILD_CREATED", ct);
			await SyncAsync(ct);
			Require(leader.Api.World.Kinah == before - SocialBasicsScenario.LegionFee && leader.Api.World.LegionName == legionName, "Legion create fee/name differs.");
			var created = leader.Api.World.LegionEmblems.Keys.Except(oldEmblems).ToArray();
			Require(created.Length == 1, "Creation must identify one new legion on the wire."); legionId = created[0];
			await leader.SendAsync(leader.Api.CloseDialog(npc), ct);
			await leader.SendAsync(leader.Api.InviteToLegion(member.CharacterName), ct);
			await member.WaitAsync(typeof(SM_QUESTION_WINDOW), p => p.Get<int>("code") == 80001, ct);
			await member.SendAsync(member.Api.Answer(1), ct);
			await member.WaitAsync(typeof(SM_LEGION_ADD_MEMBER), p => p.Get<int>("objectId") == member.CharacterId, ct);
			await SyncAsync(ct);
			foreach (var player in players) Require(player.Api.World.LegionName == legionName && player.Api.World.LegionMembers.Count == 2, "S7 legion roster differs.");
		}, token);
		await StepAsync("director-setup-legion-level-two", prepareLegionLevel, token);
		await SyncAsync(token);
		await StepAsync("change-emblem-and-read-legion-history", async ct =>
		{
			Require(players.All(p => p.Api.World.LegionLevel == 2), "Level-two setup not observed.");
			await leader.MoveAsync(EmblemPoint with { X = EmblemPoint.X - 1 }, ct);
			int npc = Npc(leader, EmblemNpc);
			await leader.SendAsync(leader.Api.TalkTo(npc), ct);
			await leader.WaitAsync(typeof(SM_DIALOG_WINDOW), p => p.Get<int>("targetObjectId") == npc, ct);
			long before = leader.Api.World.Kinah;
			long fee = (leader.Api.World.VendorPrices ?? throw new InvalidDataException("Missing prices packet.")).ServicePrice(800_000);
			await leader.SendAsync(leader.Api.SetLegionEmblem(legionId, 3, 0, 255, 250, 128, 64), ct);
			await leader.WaitAsync(typeof(SM_SYSTEM_MESSAGE), p => p.Get<string?>("name") == "STR_GUILD_CHANGE_EMBLEM", ct);
			await SyncAsync(ct);
			Require(leader.Api.World.Kinah == before - fee, "Emblem fee differs.");
			foreach (var player in players)
			{
				Require(player.Api.World.LegionEmblems[legionId] == new BotLegionEmblem(legionId, 3, 0, 255, 250, 128, 64), "Emblem broadcast differs.");
				var history = await HistoryAsync(player, 0, 0, ct);
				Require(history.TotalEntries == 4 && history.Entries.Count == 4, "Expected create, two joins and emblem modification.");
				Require(history.Entries.Select(e => $"{e.Action}:{e.Name}").Order().SequenceEqual(new[] { "0:", $"1:{leader.CharacterName}", $"1:{member.CharacterName}", "6:" }.Order()), "Legion history actions/actors differ.");
				Require(history.Entries.All(e => e.Timestamp > 0), "Legion history lacks timestamps.");
				var empty = await HistoryAsync(player, 1, 0, ct);
				Require(empty.TotalEntries == 4 && empty.Entries.Count == 0 && empty.Page == 1, "History pagination differs.");
			}
			await leader.SendAsync(leader.Api.CloseDialog(npc), ct);
		}, token);
		await StepAsync("director-setup-warehouse-location", prepareWarehousePosition, token);
		await SyncAsync(token);
		await StepAsync("legion-warehouse-deposit-withdraw-history-and-close", async ct =>
		{
			await leader.MoveAsync(WarehousePoint with { X = WarehousePoint.X - 1 }, ct);
			int npc = Npc(leader, WarehouseNpc);
			await OpenDialogAsync(leader, npc, DialogAction.OPEN_LEGION_WAREHOUSE, 25, ct);
			Require(leader.Api.World.LegionWarehouseKinah == 0, "Fresh legion warehouse is not empty.");
			long before = leader.Api.World.Kinah;
			Require(before >= Deposit, "Missing warehouse setup kinah.");
			await leader.SendAsync(leader.Api.DepositLegionKinah(Deposit), ct);
			await SyncAsync(ct);
			Require(leader.Api.World.LegionWarehouseKinah == Deposit && leader.Api.World.Kinah == before - Deposit, "Warehouse deposit failed conservation.");
			await leader.SendAsync(leader.Api.WithdrawLegionKinah(Withdrawal), ct);
			await SyncAsync(ct);
			Require(leader.Api.World.LegionWarehouseKinah == Deposit - Withdrawal && leader.Api.World.Kinah == before - Deposit + Withdrawal, "Warehouse withdrawal failed conservation.");
			foreach (var player in players)
			{
				var history = await HistoryAsync(player, 0, 2, ct);
				Require(history.TotalEntries == 2 && history.Entries.Count == 2 && history.Entries.All(e => e.Name == leader.CharacterName), "Warehouse history count/actor differs.");
				Require(history.Entries.Select(e => $"{e.Action}:{e.Description}").Order().SequenceEqual(new[] { $"17:{Deposit}", $"18:{Withdrawal}" }.Order()), "Warehouse history amounts differ.");
			}
			await leader.SendAsync(leader.Api.CloseDialog(npc), ct);
			await SyncAsync(ct);
			// Reopening proves the balance comes back from the warehouse, not just an optimistic local delta.
			await OpenDialogAsync(leader, npc, DialogAction.OPEN_LEGION_WAREHOUSE, 25, ct);
			Require(leader.Api.World.LegionWarehouseKinah == Deposit - Withdrawal, "Reopened warehouse lost its balance.");
			await leader.SendAsync(leader.Api.CloseDialog(npc), ct);
		}, token);
		await StepAsync("leave-party-and-verify-social-cleanup", async ct =>
		{
			await member.SendAsync(member.Api.LeaveGroup(), ct);
			await member.WaitAsync(typeof(SM_LEAVE_GROUP_MEMBER), _ => true, ct);
			await SyncAsync(ct);
			foreach (var player in players)
			{
				Require(player.Api.World.GroupId == null && player.Api.World.GroupMembers.Count == 0 && player.Api.World.RecallRequest == null, "S7 left party/recall state.");
			}
		}, token);
		await StepAsync("logout-reconnect-and-verify-legion-persistence", async ct =>
		{
			foreach (var player in players) await player.LogoutAsync(ct);
			foreach (var player in players)
			{
				int before = player.PacketHistory.Count;
				await player.ReenterAsync(ct);
				Require(player.PacketHistory.Skip(before).Any(p => p.PacketType == typeof(SM_LEGION_INFO) && p.Get<string>("name") == legionName), "Reconnect did not reload legion info.");
				var history = await HistoryAsync(player, 0, 2, ct);
				Require(history.TotalEntries == 2 && history.Entries.Count == 2, "Reconnect lost warehouse history.");
				await player.VerifyExtendedSocialAsync(legionId, ct);
			}
		}, token);

		Task StepAsync(string name, Func<CancellationToken, Task> action, CancellationToken ct) => leader.StepAsync(name, inner => member.StepAsync(name, action, inner), ct);
		async Task SyncAsync(CancellationToken ct) { foreach (var player in players) await player.SynchronizeAsync(ct); }
		async Task CheckListingsAsync(string recruitment, string application, CancellationToken ct)
		{
			foreach (var player in players)
			{
				await ListingsAsync(player, false, ct); await ListingsAsync(player, true, ct);
				Require(player.Api.World.GroupRecruitments.TryGetValue(leader.CharacterId, out var offer) && offer.Message == recruitment && offer.Name == leader.CharacterName && offer.SoloFlag == 16 && offer.Size == 1 && offer.MinLevel == 23 && offer.MaxLevel == 23, "Recruitment content differs.");
				Require(player.Api.World.GroupApplications.TryGetValue(member.CharacterId, out var apply) && apply.Message == application && apply.Name == member.CharacterName && apply.Level == 23 && apply.PlayerClass == (byte)PlayerClass.SPIRIT_MASTER, "Application content differs.");
			}
		}
	}

	private static async Task ListingsAsync(IExtendedSocialDriver player, bool applications, CancellationToken ct)
	{
		await player.SendAsync(player.Api.RequestGroupListings(applications), ct);
		await player.WaitAsync(typeof(SM_FIND_GROUP), p => p.Get<byte>("action") == (applications ? 4 : 0), ct);
	}
	private static async Task<BotLegionHistoryPage> HistoryAsync(IExtendedSocialDriver player, int page, byte type, CancellationToken ct)
	{
		await player.SynchronizeAsync(ct);
		await player.SendAsync(player.Api.RequestLegionHistory(page, type), ct);
		await player.WaitAsync(typeof(SM_LEGION_HISTORY), p => p.Get<int>("page") == page && p.Get<ushort>("historyType") == type, ct);
		return player.Api.World.LegionHistoryPages[type];
	}
	private static async Task OpenDialogAsync(IExtendedSocialDriver player, int npc, ushort action, ushort page, CancellationToken ct)
	{
		await player.SendAsync(player.Api.TalkTo(npc), ct);
		await player.WaitAsync(typeof(SM_DIALOG_WINDOW), p => p.Get<int>("targetObjectId") == npc, ct);
		await player.SendAsync(player.Api.SelectDialog(npc, action), ct);
		await player.WaitAsync(typeof(SM_DIALOG_WINDOW), p => p.Get<int>("targetObjectId") == npc && p.Get<ushort>("dialogPageId") == page, ct);
	}
	private static int Npc(IExtendedSocialDriver player, int templateId) => player.Api.World.Objects.Values.Single(o => o.TemplateId == templateId).ObjectId;
	private static long ItemCount(IExtendedSocialDriver player, int itemId) => player.Api.World.Inventory.Values.Where(i => i.ItemId == itemId).Sum(i => i.Count);
	private static double Distance(BotPosition a, BotPosition b) => Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2) + Math.Pow(a.Z - b.Z, 2));
	private static void Require(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
	private static ushort LoadRecallHitTime()
	{
		string directory = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ScenarioManifest.FindDefaultPath())!, "..", "..", "game-server", "data", "static_data", "skills"));
		using var reader = XmlReader.Create(Path.Combine(directory, "skill_templates.xml"));
		while (reader.Read())
		{
			if (reader.NodeType != XmlNodeType.Element || reader.LocalName != "skill_template" || reader.GetAttribute("skill_id") != "3777") continue;
			using var subtree = reader.ReadSubtree();
			var skill = (SkillTemplate?)new XmlSerializer(typeof(SkillTemplate), new XmlRootAttribute("skill_template")).Deserialize(subtree)
				?? throw new InvalidDataException("Missing recall timing template.");
			return checked((ushort)BotMotionTiming.Load(Path.Combine(directory, "motion_times.xml")).CalculateClientHitTime(skill,
				new BotMotionProfile(Race.ELYOS, Gender.MALE, BotWeaponMotionType.Book)));
		}
		throw new InvalidDataException("Missing recall skill 3777.");
	}
}
