using System.Diagnostics;
using Aion.Bots.Reflexes;
using Aion.Bots.Movement;
using Aion.Bots.Scenarios;
using Aion.Bots.World;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.LiveBots;

public static partial class LiveBotRunner
{
	private sealed class SoakKiskState(Race race, ItemTemplate template)
	{
		public Race Race { get; } = race;
		public ItemTemplate Template { get; } = template;
		public int ObjectId { get; set; }
		public int Kills { get; set; }
		public long ObservedAt { get; set; }
		public int LifetimeSeconds { get; set; }
		public TimeSpan Remaining => TimeSpan.FromSeconds(LifetimeSeconds) - Stopwatch.GetElapsedTime(ObservedAt);
	}

	private static async Task SoakPvpAsync(L0Actor first, L0Actor second, SoakKiskState[] states, long cycle, CancellationToken token)
	{
		var actors = new[] { first, second };
		await Task.WhenAll(actors.Select(actor => actor.Session.SynchronizeAsync(token)));
		foreach (var actor in actors)
			if (actor.Session.Api.World.IsDead || actor.Session.Api.World.GroupId != null || actor.Session.Api.World.DuelOpponentId != null)
				throw new InvalidDataException("Repeatable PvP requires living solo subjects outside duels.");
		await SocialBasicsScenario.RecoverForDuelAsync(new LiveSocialDriver(first), new LiveSocialDriver(second), token);
		await SoakIndependentPairAsync(first, second, (actor, ct) => EnsureKiskAsync(actor, states[actor == first ? 0 : 1], ct), token);
		for (int i = 0; i < actors.Length; i++)
		{
			var session = actors[i].Session;
			// CM_MOVE only cancels spawn protection after actual coordinate movement, not a zero-distance turn.
			await SoakPvpWalkAsync(session, SoakPvpCamp.Home(states[i].Race), token);
			await SoakPvpWalkAsync(session, SoakPvpCamp.Encounter(session.Navigation!.Value.Geometry, states[i].Race), token);
		}
		int winnerIndex = (int)(cycle % 2), victimIndex = 1 - winnerIndex;
		var winner = actors[winnerIndex]; var victim = actors[victimIndex];
		var winnerState = states[winnerIndex]; var victimState = states[victimIndex];
		var beforeWinnerItems = SoakTotals(winner.Session.Api.World);
		var beforeVictimItems = SoakTotals(victim.Session.Api.World);
		var beforeVictimKisk = victim.Session.Api.World.OwnedKiskUpdate;
		if (beforeVictimKisk == null || beforeVictimKisk.ObjectId != victimState.ObjectId || beforeVictimKisk.RemainingResurrects < 1)
			throw new InvalidDataException("PvP victim has no observed charged owned Kisk.");
		await PvpFlightScenario.KillAsync(new LiveSocialDriver(winner), new LiveSocialDriver(victim), winnerState.Race, winnerState.Kills + 1, token);
		winnerState.Kills++;
		foreach (var actor in actors)
			actor.Trace.WriteAction(actor.LastStep, "soak:pvp-kill", new Dictionary<string, object?>
			{ ["winner"] = winner.Session.CharacterId, ["victim"] = victim.Session.CharacterId, ["opponentKills"] = winnerState.Kills,
				["ap"] = actor.Session.Api.World.AbyssRank!.Ap, ["level"] = actor.Session.Api.World.Level });

		var victimWorld = victim.Session.Api.World;
		if (victimWorld.KiskBindPoint?.KiskObjectId != victimState.ObjectId || victimWorld.ReviveOptions is not { RemainingKiskTimeSeconds: > 0 })
			throw new InvalidDataException("Actual PvP death did not offer resurrection at the subject's owned Kisk.");
		await victim.Session.SendPacketAsync(victim.Session.Api.Revive(BotReviveType.Kisk), token);
		var update = await victim.Session.WaitForPacketAsync(typeof(SM_KISK_UPDATE), token,
			packet => packet.Get<int>("objectId") == victimState.ObjectId && packet.Get<int>("creatorId") == victim.Session.CharacterId);
		if (update.Get<int>("remainingResurrects") != beforeVictimKisk.RemainingResurrects - 1)
			throw new InvalidDataException("Kisk resurrection did not consume exactly one charge.");
		victim.Session.Api.World.BeginWorldReload();
		await victim.Session.CompleteTeleportAsync(SoakPvpCamp.MapId, token);
		await Task.WhenAll(actors.Select(actor => actor.Session.SynchronizeAsync(token)));
		if (victimWorld.IsDead || victimWorld.CurrentHp <= 0 || QuestDistance(victim.Session.CurrentPosition, SoakPvpCamp.Home(victimState.Race)) > .15f)
			throw new InvalidDataException("Ordinary Kisk revival did not restore life at the observed camp position.");
		SoakAssertInventory(winner.Session.Api.World, beforeWinnerItems);
		SoakAssertInventory(victimWorld, beforeVictimItems);
		victim.Trace.WriteAction(victim.LastStep, "soak:pvp-revived", new Dictionary<string, object?>
		{ ["kisk"] = victimState.ObjectId, ["remaining"] = update.Get<int>("remainingResurrects"), ["hp"] = victimWorld.CurrentHp, ["mp"] = victimWorld.CurrentMp });
		await SocialBasicsScenario.RecoverForDuelAsync(new LiveSocialDriver(first), new LiveSocialDriver(second), token);
		await SoakPvpWalkAsync(victim.Session, SoakPvpCamp.Encounter(victim.Session.Navigation!.Value.Geometry, victimState.Race), token);
		foreach (var actor in actors)
		{
			if (actor.Session.Api.World.IsDead || actor.Session.Api.Timing.BlockingActivities.Count != 0)
				throw new InvalidDataException("PvP cycle left a dead subject or blocked client interaction.");
			actor.Trace.WriteAction(actor.LastStep, "soak:pvp-cycle-complete", new Dictionary<string, object?> { ["cycle"] = cycle + 1 });
		}
	}

	private static async Task EnsureKiskAsync(L0Actor actor, SoakKiskState state, CancellationToken token)
	{
		var session = actor.Session;
		var world = session.Api.World;
		await session.SynchronizeAsync(token);
		if (state.ObjectId != 0)
		{
			var observed = world.OwnedKiskUpdate;
			if (observed?.ObjectId != state.ObjectId) throw new InvalidDataException("Owned Kisk observation was lost or replaced unexpectedly.");
			if (world.OwnedKiskRemoval == null && observed.RemainingResurrects > 0 && observed.RemainingLifetimeSeconds > 0 && state.Remaining > TimeSpan.FromMinutes(5))
			{
				if (world.KiskBindPoint?.KiskObjectId != state.ObjectId) throw new InvalidDataException("Relog did not preserve the owned Kisk binding.");
				return;
			}
			// Never begin a fight near expiry, force-despawn a Kisk, or reset its real cooldown.
			// Java removal may send the old bind point before clearing the server reference: use the
			// removal notice joined to this Kisk's deletion. Its final update may still report one second.
			while (world.OwnedKiskRemoval is not { DeleteObserved: true } removal || removal.ObjectId != state.ObjectId)
			{
				if (state.Remaining < TimeSpan.FromSeconds(-30))
					throw new InvalidDataException("Owned Kisk retirement notice/deletion missing thirty seconds after its observed lifetime.");
				await Task.Delay(1000, token);
				await session.SynchronizeAsync(token);
				if (world.IsDead) throw new InvalidDataException("Subject died while waiting for natural Kisk expiry.");
			}
			if (world.OwnedKiskRemoval.Destroyed)
				throw new InvalidDataException("Owned Kisk was destroyed instead of retiring naturally.");
			actor.Trace.WriteAction(actor.LastStep, "soak:kisk-retired", new Dictionary<string, object?>
			{ ["kisk"] = state.ObjectId, ["source"] = "STR_BINDSTONE_IS_REMOVED+SM_DELETE",
				["finalReportedLifetimeSeconds"] = world.OwnedKiskUpdate!.RemainingLifetimeSeconds });
		}
		while (session.Api.Timing.TimeUntilItemUse(state.Template) > TimeSpan.Zero ||
			state.ObjectId != 0 && Stopwatch.GetElapsedTime(state.ObservedAt) < TimeSpan.FromMilliseconds(state.Template.GetUseLimits().GetDelayTime() + 1000))
		{
			await Task.Delay(1000, token);
			await session.SynchronizeAsync(token);
		}
		await SoakPvpWalkAsync(session, SoakPvpCamp.Home(state.Race), token);
		var expected = SoakTotals(world);
		var item = world.Inventory.Values.FirstOrDefault(value => value.ItemId == state.Template.GetTemplateId() && value.Count > 0)
			?? throw new InvalidDataException("Finite initial Kisk supply exhausted; no timed GM restocking is allowed.");
		await session.SendPacketAsync(session.Api.UseItem(item.ObjectId, state.Template), token);
		var started = await session.WaitForPacketAsync(typeof(SM_ITEM_USAGE_ANIMATION), token,
			packet => packet.Get<int>("playerObjId") == session.CharacterId && packet.Get<int>("itemObjId") == item.ObjectId);
		if (started.Get<byte>("animationId") != 0 || started.Get<int>("castTime") != 10000)
			throw new InvalidDataException("Medium Kisk did not begin its ordinary ten-second placement cast.");
		await Task.Delay(10001, token);
		var finished = await session.WaitForPacketAsync(typeof(SM_ITEM_USAGE_ANIMATION), token,
			packet => packet.Get<int>("playerObjId") == session.CharacterId && packet.Get<int>("itemObjId") == item.ObjectId);
		if (finished.Get<byte>("animationId") != 1) throw new InvalidDataException("Kisk placement failed or was interrupted.");
		var question = await session.WaitForPacketAsync(typeof(SM_QUESTION_WINDOW), token, packet => packet.Get<int>("code") == 160018);
		int id = question.Get<int>("senderId");
		if (!world.Objects.TryGetValue(id, out var kisk) || kisk.TemplateId != SoakPvpCamp.Npc(state.Race) ||
			world.OwnedKiskUpdate is not { } owned || owned.ObjectId != id || owned.CreatorId != session.CharacterId)
			throw new InvalidDataException("Kisk binding question was not for this subject's observed medium Kisk.");
		await session.SendPacketAsync(session.Api.Answer(1), token);
		var update = await session.WaitForPacketAsync(typeof(SM_KISK_UPDATE), token,
			packet => packet.Get<int>("objectId") == id && packet.Get<int>("creatorId") == session.CharacterId);
		if (update.Get<int>("remainingResurrects") != SoakPvpCamp.MaxResurrects || update.Get<int>("maxResurrects") != SoakPvpCamp.MaxResurrects ||
			update.Get<int>("currentMembers") != 1 || update.Get<int>("remainingLifetimeSeconds") is < 7100 or > 7200)
			throw new InvalidDataException("Fresh medium Kisk has unexpected members, charges or lifetime.");
		await session.WaitForPacketAsync(typeof(SM_BIND_POINT_INFO), token,
			packet => packet.Get<byte>("bindPointType") == 4 && packet.Get<int>("kiskObjectId") == id);
		await session.SynchronizeAsync(token);
		if (world.KiskBindPoint?.MapId != SoakPvpCamp.MapId || QuestDistance(world.KiskBindPoint.Position, SoakPvpCamp.Home(state.Race)) > .15f)
			throw new InvalidDataException("Kisk binding does not point to the subject's grounded camp.");
		SoakAdd(expected, state.Template.GetTemplateId(), -1);
		SoakAssertInventory(world, expected);
		state.ObjectId = id;
		// Server cooldown begins at successful use, not at the start of the ten-second cast.
		// Recording after the synchronization barrier conservatively includes processing latency.
		state.ObservedAt = Stopwatch.GetTimestamp();
		state.LifetimeSeconds = update.Get<int>("remainingLifetimeSeconds");
		actor.Trace.WriteAction(actor.LastStep, "soak:kisk-bound", new Dictionary<string, object?>
		{ ["kisk"] = id, ["item"] = state.Template.GetTemplateId(), ["lifetimeSeconds"] = state.LifetimeSeconds, ["charges"] = SoakPvpCamp.MaxResurrects });
	}

	private static async Task SoakPvpWalkAsync(LiveBotSession session, BotPosition destination, CancellationToken token)
	{
		var nav = session.Navigation ?? throw new InvalidDataException("PvP requires checked camp geometry.");
		var path = SoakPvpCamp.Walk(nav.Geometry, session.CurrentPosition, destination);
		await session.ExecuteMovementAsync(new BotMover(session.Api.World, session.Api.Timing).CreateGroundPlan(path,
			session.CurrentPosition, session.Api.World.MovementSpeed ?? throw new InvalidDataException("Missing PvP movement speed.")), token);
		await session.SynchronizeAsync(token);
		if (session.Api.World.IsDead) throw new InvalidDataException("PvP subject died while walking the camp edge.");
	}
}
