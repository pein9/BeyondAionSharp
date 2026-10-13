using System.Text.Json;
using Aion.Bots.World;
using Aion.GameServer.Custom.Companion;
using Aion.GameServer.Model;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.Bots.Scenarios;

public sealed partial class NaturalIshalgenJourney
{
	/// <summary>How far the stand-in plays so far (docs/companion-bots.md, the C steps).</summary>
	private static readonly string[] CompanionCheckpoints = ["joined"];

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
		session.BeginStep("companion", "with-its-player");
		while (world.GroupId != null && world.GroupLeaderId == leader)
		{
			await session.AdvanceAsync(TimeSpan.FromMilliseconds(500), token);
			await session.SynchronizeAsync(token);
		}
		// Sent away, or the player left: the companion has nobody to follow and leaves the game. The server deletes it.
		session.BeginStep("companion", "leave-the-game");
		session.TraceDiagnostic("companion-leaves", new Dictionary<string, object?>
		{
			["group"] = world.GroupId, ["leaderNow"] = world.GroupLeaderId, ["leaderWas"] = leader, ["elapsedMillis"] = runtime.NowMillis - entered,
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
		runtime.AssertClean();
		var receipt = new { verified = true, CharacterId = session.CharacterId, until = options.CompanionUntil, asked, companions = joined,
			leader = world.GroupLeaderId, group = world.GroupId, lootRules = world.GroupLootRules, list,
			elapsedMillis = runtime.NowMillis - started, map = world.MapId, position = session.CurrentPosition };

		session.BeginStep("stand-in", "send-them-away");
		string[] farewell = await SayAsync(".companion dismiss");
		await WaitAsync(() => world.GroupId == null, "the group did not end after the dismissal", 60);
		File.WriteAllText(Path.Combine(Path.GetDirectoryName(session.CombatTracePath!)!, "companion-completion.json"),
			JsonSerializer.Serialize(new { receipt.verified, receipt.CharacterId, receipt.until, receipt.asked, receipt.companions, receipt.leader,
				receipt.group, receipt.lootRules, receipt.list, farewell, receipt.elapsedMillis, receipt.map, receipt.position },
				new JsonSerializerOptions { WriteIndented = true }));
		session.TraceDiagnostic("stand-in-reached", new Dictionary<string, object?> { ["receipt"] = receipt, ["farewell"] = farewell });
	}
}
