using Aion.Bots.Api;
using Aion.Bots.Protocol;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.Bots.Scenarios;

public interface IAllianceLeagueDriver
{
	BotApi Api { get; }
	int CharacterId { get; }
	string CharacterName { get; }
	Task StepAsync(string action, Func<CancellationToken, Task> operation, CancellationToken token);
	Task SendAsync(BotClientPacket packet, CancellationToken token);
	Task<DecodedBotServerPacket> WaitAsync(Type type, Func<DecodedBotServerPacket, bool> predicate, CancellationToken token);
	Task SynchronizeAsync(CancellationToken token);
	Task VerifyAllianceAsync(CancellationToken token);
}

/// <summary>S4: eight ordinary subjects, four two-player groups, two alliances and one league. No GM setup.</summary>
public static class AllianceLeagueScenario
{
	public static async Task RunAsync(IReadOnlyList<IAllianceLeagueDriver> players, CancellationToken token = default)
	{
		Require(players.Count == 8 && players.Select(p => p.CharacterId).Distinct().Count() == 8, "S4 requires eight distinct subjects.");
		await SyncAsync(token);
		await StepAsync("form-four-two-player-groups", async ct =>
		{
			for (int i = 0; i < 8; i += 2)
			{
				var leader = players[i]; var member = players[i + 1];
				await InviteAsync(leader, member, leader.Api.InviteToGroup(member.CharacterName), 60000, ct);
				foreach (var viewer in new[] { leader, member })
					await viewer.WaitAsync(typeof(SM_GROUP_INFO), p => p.Get<int>("leaderId") == leader.CharacterId, ct);
			}
			await SyncAsync(ct);
			for (int i = 0; i < 8; i += 2)
			{
				var first = players[i].Api.World; var second = players[i + 1].Api.World;
				Require(first.GroupId > 0 && first.GroupId == second.GroupId, "S4 group ids disagree.");
				var ids = new[] { players[i].CharacterId, players[i + 1].CharacterId }.Order().ToArray();
				Require(first.GroupMembers.Count == 2 && second.GroupMembers.Count == 2 &&
					first.GroupMembers.Keys.Order().SequenceEqual(ids) && second.GroupMembers.Keys.Order().SequenceEqual(ids),
					"S4 two-player group has the wrong roster (including self).");
			}
			Require(players.Where((_, i) => i % 2 == 0).Select(p => p.Api.World.GroupId).Distinct().Count() == 4,
				"S4 did not create four independent groups.");
		}, token);

		await StepAsync("merge-groups-into-two-alliances", async ct =>
		{
			for (int i = 0; i < 8; i += 4)
			{
				var leader = players[i]; var invited = players[i + 2];
				await InviteAsync(leader, invited, leader.Api.InviteToAlliance(invited.CharacterName), 70000, ct);
				foreach (var viewer in players.Skip(i).Take(4))
					await viewer.WaitAsync(typeof(SM_ALLIANCE_INFO), p => p.Get<int>("leaderId") == leader.CharacterId, ct);
			}
			await SyncAsync(ct);
			AssertAlliance(0, 0); AssertAlliance(4, 4);
			Require(players[0].Api.World.AllianceId != players[4].Api.World.AllianceId, "The two alliances must be distinct.");
		}, token);

		await StepAsync("transfer-alliance-captains", async ct =>
		{
			for (int i = 0; i < 8; i += 4)
			{
				await players[i].SendAsync(players[i].Api.SetAllianceLeader(players[i + 1].CharacterId), ct);
				foreach (var viewer in players.Skip(i).Take(4))
					await viewer.WaitAsync(typeof(SM_ALLIANCE_INFO), p => p.Get<int>("leaderId") == players[i + 1].CharacterId, ct);
			}
			await SyncAsync(ct);
			AssertAlliance(0, 1); AssertAlliance(4, 5);
			for (int i = 0; i < 8; i += 4)
				foreach (var viewer in players.Skip(i).Take(4))
					Require(viewer.Api.World.AllianceViceCaptains.SequenceEqual([players[i].CharacterId]),
						"Former captain was not demoted to vice-captain.");
		}, token);

		await StepAsync("join-two-alliances-into-league", async ct =>
		{
			await InviteAsync(players[1], players[5], players[1].Api.InviteToLeague(players[5].CharacterName), 902249, ct);
			foreach (var viewer in players)
				await viewer.WaitAsync(typeof(SM_ALLIANCE_INFO), p => p.Get<int>("leagueId") != 0 &&
					p.Get<List<IReadOnlyDictionary<string, object?>>>("alliances").Count == 2, ct);
			await SyncAsync(ct);
			AssertAlliance(0, 1); AssertAlliance(4, 5);
			int leagueId = players[0].Api.World.LeagueId ?? throw new InvalidDataException("S4 league is missing.");
			Require(leagueId > 0, "Invalid league identity.");
			foreach (var viewer in players)
			{
				Require(viewer.Api.World.LeagueId == leagueId && viewer.Api.World.LeagueAlliances.Count == 2,
					"Clients disagree on the two-alliance league.");
				for (int i = 0; i < 2; i++)
				{
					int id = players[i * 4].Api.World.AllianceId!.Value;
					Require(viewer.Api.World.LeagueAlliances.TryGetValue(id, out var row) && row.Position == i &&
						row.MemberCount == 4 && row.CaptainName == players[i * 4 + 1].CharacterName && row.MapId == 210010000,
						"League roster lost alliance identity, position, count or captain.");
				}
			}
		}, token);

		await StepAsync("leave-and-disband-league", async ct =>
		{
			await players[5].SendAsync(players[5].Api.LeaveLeague(), ct);
			foreach (var viewer in players)
				await viewer.WaitAsync(typeof(SM_ALLIANCE_INFO), p => p.Get<int>("leagueId") == 0, ct);
			await SyncAsync(ct);
			AssertAlliance(0, 1); AssertAlliance(4, 5);
			foreach (var viewer in players) Require(viewer.Api.World.LeagueId == null && viewer.Api.World.LeagueAlliances.Count == 0,
				"League was retained after its two alliances separated.");
		}, token);

		await StepAsync("leave-and-disband-alliances", async ct =>
		{
			// Keep each captain until last: the third departure disbands the remaining one-person alliance.
			foreach (int i in new[] { 0, 2, 3, 4, 6, 7 })
			{
				await players[i].SendAsync(players[i].Api.LeaveAlliance(), ct);
				await players[i].WaitAsync(typeof(SM_LEAVE_GROUP_MEMBER), _ => true, ct);
			}
			foreach (int i in new[] { 1, 5 }) await players[i].WaitAsync(typeof(SM_LEAVE_GROUP_MEMBER), _ => true, ct);
			await SyncAsync(ct);
			foreach (var viewer in players)
				Require(viewer.Api.World.AllianceId == null && viewer.Api.World.AllianceLeaderId == null &&
					viewer.Api.World.AllianceMembers.Count == 0 && viewer.Api.World.GroupId == null && viewer.Api.World.LeagueId == null,
					"S4 cleanup left a subject in a team.");
		}, token);

		async Task SyncAsync(CancellationToken ct)
		{
			foreach (var viewer in players) await viewer.SynchronizeAsync(ct);
		}
		async Task StepAsync(string action, Func<CancellationToken, Task> operation, CancellationToken ct)
		{
			await InStepAsync(0);
			async Task InStepAsync(int index)
			{
				if (index < players.Count) await players[index].StepAsync(action, _ => InStepAsync(index + 1), ct);
				else
				{
					await operation(ct);
					foreach (var viewer in players) await viewer.VerifyAllianceAsync(ct);
				}
			}
		}
		void AssertAlliance(int start, int leader)
		{
			var members = players.Skip(start).Take(4).ToArray();
			int? id = members[0].Api.World.AllianceId;
			Require(id > 0, "Alliance identity is missing.");
			foreach (var viewer in members)
			{
				var world = viewer.Api.World;
				Require(world.AllianceId == id && world.AllianceLeaderId == players[leader].CharacterId && world.GroupId == null,
					"Group-to-alliance conversion or captain update is incomplete.");
				Require(world.AllianceMembers.Count == 4 && world.AllianceMembers.Keys.Order().SequenceEqual(members.Select(p => p.CharacterId).Order()),
					"Alliance roster must contain exactly its four subjects, including self.");
				foreach (var member in members)
				{
					var row = world.AllianceMembers[member.CharacterId];
					Require(row.Name == member.CharacterName && row.Online && row.GroupId is >= 1000 and <= 1003,
						"Alliance member identity, status or subgroup is incorrect.");
				}
			}
		}
	}

	private static async Task InviteAsync(IAllianceLeagueDriver from, IAllianceLeagueDriver to, BotClientPacket packet,
		int question, CancellationToken token)
	{
		await from.SendAsync(packet, token);
		var prompt = await to.WaitAsync(typeof(SM_QUESTION_WINDOW), p => p.Get<int>("code") == question, token);
		Require(prompt.Get<string[]>("params")[0] == from.CharacterName, "Invitation lost the requester's name.");
		await to.SendAsync(to.Api.Answer(1), token);
	}
	private static void Require(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
}
