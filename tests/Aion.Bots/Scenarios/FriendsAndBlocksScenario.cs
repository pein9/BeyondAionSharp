using Aion.Bots.Api;
using Aion.Bots.Protocol;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.Bots.Scenarios;

public interface IFriendsAndBlocksDriver
{
	BotApi Api { get; }
	int CharacterId { get; }
	string CharacterName { get; }
	Task StepAsync(string action, Func<CancellationToken, Task> operation, CancellationToken token);
	Task SendAsync(BotClientPacket packet, CancellationToken token);
	Task<DecodedBotServerPacket> WaitAsync(Type type, Func<DecodedBotServerPacket, bool> predicate, CancellationToken token);
	Task SynchronizeAsync(CancellationToken token);
	Task LogoutAsync(CancellationToken token);
	Task ReenterAsync(CancellationToken token);
	Task VerifyFriendsAndBlocksAsync(CancellationToken token);
}

/// <summary>S3: ordinary friend/block requests, reciprocal lifecycle notifications, and persistence across real reconnects.</summary>
public static class FriendsAndBlocksScenario
{
	public static void AssertReloadPackets(IEnumerable<DecodedBotServerPacket> packets)
	{
		var types = packets.Select(packet => packet.PacketType).ToArray();
		// Initial lists may precede SM_PLAYER_INFO during reentry. Check this connection's consumed stream,
		// not a retained client dictionary or a second wait for a packet already processed by EnterWorld.
		Require(types.Count(type => type == typeof(SM_FRIEND_LIST)) == 1 && types.Count(type => type == typeof(SM_BLOCK_LIST)) == 1,
			"Reconnect did not supply exactly one fresh friend list and block list.");
	}

	public static async Task RunAsync(IFriendsAndBlocksDriver first, IFriendsAndBlocksDriver second, CancellationToken token)
	{
		await SyncAsync(token);
		foreach (var actor in new[] { first, second }) Require(actor.Api.World.Friends.Count == 0 && actor.Api.World.BlockedPlayers.Count == 0, "S3 subjects must start with empty social lists.");
		await BothStepAsync("friend-request-and-accept", async ct =>
		{
			await first.SendAsync(first.Api.AddFriend(second.CharacterName, "S3 invitation"), ct);
			var question = await second.WaitAsync(typeof(SM_QUESTION_WINDOW), packet => packet.Get<int>("code") == 1401498, ct);
			Require(question.Get<int>("senderId") == first.CharacterId &&
				question.Get<string[]>("params").SequenceEqual(new[] { first.CharacterName, "S3 invitation", "" }), "Friend invitation lost its sender, message or empty third wire parameter.");
			await second.SendAsync(second.Api.Answer(1), ct);
			foreach (var (actor, other) in new[] { (first, second), (second, first) })
			{
				await ResponseAsync(actor, typeof(SM_FRIEND_RESPONSE), other.CharacterName, 0, ct);
				Require(actor.Api.World.Friends.Count == 1 && actor.Api.World.Friends.TryGetValue(other.CharacterId, out var friend) &&
					friend.Name == other.CharacterName && friend.Status == 1 && friend.Memo == "", "Friend addition did not create a reciprocal online entry.");
			}
		}, token);
		await BothStepAsync("set-independent-friend-memos", async ct =>
		{
			foreach (var (actor, other, memo) in new[] { (first, second, "S3 first memo"), (second, first, "S3 second memo") })
			{
				await actor.SendAsync(actor.Api.SetFriendMemo(other.CharacterName, memo), ct);
				await actor.WaitAsync(typeof(SM_FRIEND_LIST), _ => true, ct);
				Require(actor.Api.World.Friends[other.CharacterId].Memo == memo, "Friend memo was not applied.");
				await actor.VerifyFriendsAndBlocksAsync(ct);
			}
		}, token);
		await BothStepAsync("friend-logout-relogin-notifications", async ct =>
		{
			await second.LogoutAsync(ct);
			await NoticeAsync(first, second.CharacterName, 1, ct);
			Require(first.Api.World.Friends[second.CharacterId].Status == 0, "Logout did not update friend status.");
			// Java PlayerLeaveWorldService notifies friends before assigning lastOnline. On first logout
			// the early update may legitimately carry zero; ask for a fresh list after verified persistence.
			await first.SendAsync(first.Api.RequestFriendList(), ct);
			await first.WaitAsync(typeof(SM_FRIEND_LIST), _ => true, ct);
			Require(first.Api.World.Friends[second.CharacterId] is { Status: 0, LastOnline: > 0, Memo: "S3 first memo" }, "Persisted offline friend list lost its status, timestamp or memo.");
			await second.ReenterAsync(ct);
			await NoticeAsync(first, second.CharacterName, 0, ct);
			await SyncAsync(ct);
			// EnterWorld notifies friends before StoreObject; an eagerly serialized status update can
			// still say offline. The notification itself is mandatory; refresh after completed entry.
			await first.SendAsync(first.Api.RequestFriendList(), ct);
			await first.WaitAsync(typeof(SM_FRIEND_LIST), _ => true, ct);
			Require(first.Api.World.Friends[second.CharacterId] is { Status: 1, LastOnline: 0, Memo: "S3 first memo" }, "Login update lost the remaining player's private memo.");
			await second.SendAsync(second.Api.RequestFriendList(), ct);
			await second.WaitAsync(typeof(SM_FRIEND_LIST), _ => true, ct);
			Require(second.Api.World.Friends.Count == 1 && second.Api.World.Friends[first.CharacterId] is { Status: 1, Memo: "S3 second memo" }, "Relog did not restore the saved friendship and memo.");
			await second.VerifyFriendsAndBlocksAsync(ct);
		}, token);
		await BothStepAsync("delete-reciprocal-friendship", async ct =>
		{
			await first.SendAsync(first.Api.DeleteFriend(second.CharacterName), ct);
			await ResponseAsync(first, typeof(SM_FRIEND_RESPONSE), second.CharacterName, 6, ct);
			await NoticeAsync(second, first.CharacterName, 2, ct);
			Require(first.Api.World.Friends.Count == 0 && second.Api.World.Friends.Count == 0, "Deleting friendship did not remove both directions.");
			await first.VerifyFriendsAndBlocksAsync(ct); await second.VerifyFriendsAndBlocksAsync(ct);
		}, token);
		await BothStepAsync("block-edit-reason-and-relog", async ct =>
		{
			await first.SendAsync(first.Api.BlockPlayer(second.CharacterName, "S3 original reason"), ct);
			await ResponseAsync(first, typeof(SM_BLOCK_RESPONSE), second.CharacterName, 0, ct);
			Require(first.Api.World.BlockedPlayers.Count == 1 && first.Api.World.BlockedPlayers[second.CharacterName] == "S3 original reason", "Block list did not contain the new entry.");
			await first.SendAsync(first.Api.SetBlockReason(second.CharacterName, "S3 revised reason"), ct);
			await ResponseAsync(first, typeof(SM_BLOCK_RESPONSE), second.CharacterName, 5, ct);
			Require(first.Api.World.BlockedPlayers[second.CharacterName] == "S3 revised reason", "Block reason was not changed.");
			await first.VerifyFriendsAndBlocksAsync(ct);
			await first.LogoutAsync(ct);
			await first.ReenterAsync(ct);
			await SyncAsync(ct);
			Require(first.Api.World.BlockedPlayers.Count == 1 && first.Api.World.BlockedPlayers[second.CharacterName] == "S3 revised reason", "Block entry/reason did not survive relog.");
			await first.VerifyFriendsAndBlocksAsync(ct);
		}, token);
		await BothStepAsync("blocked-friend-request-and-unblock", async ct =>
		{
			await second.SendAsync(second.Api.AddFriend(first.CharacterName, "S3 must be blocked"), ct);
			// Java deliberately omits the target name on the TARGET_BLOCKED_YOU response.
			await ResponseAsync(second, typeof(SM_FRIEND_RESPONSE), "", 8, ct);
			await first.SendAsync(first.Api.UnblockPlayer(second.CharacterName), ct);
			await ResponseAsync(first, typeof(SM_BLOCK_RESPONSE), second.CharacterName, 1, ct);
			await SyncAsync(ct);
			foreach (var actor in new[] { first, second })
			{
				Require(actor.Api.World.Friends.Count == 0 && actor.Api.World.BlockedPlayers.Count == 0, "Social lists were not empty after cleanup.");
				await actor.VerifyFriendsAndBlocksAsync(ct);
			}
		}, token);

		Task BothStepAsync(string action, Func<CancellationToken, Task> operation, CancellationToken ct) => first.StepAsync(action, inner => second.StepAsync(action, operation, inner), ct);
		async Task SyncAsync(CancellationToken ct) { await first.SynchronizeAsync(ct); await second.SynchronizeAsync(ct); }
	}

	private static Task<DecodedBotServerPacket> NoticeAsync(IFriendsAndBlocksDriver actor, string name, byte code, CancellationToken token) =>
		actor.WaitAsync(typeof(SM_FRIEND_NOTIFY), packet => packet.Get<string>("name") == name && packet.Get<byte>("code") == code, token);
	private static async Task ResponseAsync(IFriendsAndBlocksDriver actor, Type type, string name, byte code, CancellationToken token)
	{
		var response = await actor.WaitAsync(type, _ => true, token);
		Require(response.Get<byte>("code") == code && response.Get<string>("playerName") == name, $"Unexpected {type.Name}: {response.Get<byte>("code")}, '{response.Get<string>("playerName")}'.");
	}
	private static void Require(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
}
