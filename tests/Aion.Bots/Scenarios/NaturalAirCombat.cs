using Aion.Bots.Navigation;
using Aion.Bots.Protocol;
using Aion.Bots.World;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.Bots.Scenarios;

/// <summary>What the bot knows in the air between two air kills.</summary>
/// <param name="Targets">Visible, living air targets and where they float.</param>
/// <param name="Landing">Where it lands to refill flight time.</param>
public sealed record NaturalAirCombatObservation(BotPosition Position, int Fp, float SpeedMetersPerSecond,
	IReadOnlyList<(int ObjectId, BotPosition Position)> Targets, NaturalLandingTarget Landing);

/// <param name="Action">attack, land (refill flight time) or wait (nothing visible to attack).</param>
public sealed record NaturalAirCombatDecision(string Action, int? Target, string Reason);

/// <summary>
/// AF-06 (docs/natural-altgard-leveling.md): Q24011's Abyss Fungus float over Altgard Fortress. They never fight back
/// (<c>ai="noaction"</c>, 240 HP), so the only danger is the flight time. The bot takes a fungus only when it can fly to
/// it, kill it and still reach its landing with the landing reserve left; otherwise it lands and refills first.
/// </summary>
public static class NaturalAirCombatPolicy
{
	/// <summary>A fungus is shot from this far (Smite reaches 25 m).</summary>
	public const float HoverDistance = 15f;
	/// <summary>Seconds to kill one fungus: the AF-06 probe measured 5.7 s (two Smites); one missed cast is allowed for.</summary>
	public const int KillSeconds = 8;

	public static NaturalAirCombatDecision Decide(NaturalAirCombatObservation state)
	{
		if (state.Targets.Count == 0)
			return new("wait", null, "No fungus is visible; hold until one respawns (20 s).");
		(int id, BotPosition at) = state.Targets.OrderBy(target => NaturalFlightPolicy.Distance(state.Position, target.Position)).First();
		float toTarget = MathF.Max(0, NaturalFlightPolicy.Distance(state.Position, at) - HoverDistance);
		float targetToLanding = NaturalFlightPolicy.Distance(at, state.Landing.Position);
		int cost = (int)MathF.Ceiling(toTarget / state.SpeedMetersPerSecond) + KillSeconds +
			(int)MathF.Ceiling(targetToLanding / state.SpeedMetersPerSecond);
		return state.Fp - cost >= NaturalFlightPolicy.LandingReserveFp
			? new("attack", id, $"Fungus {id}: {cost} FP to kill and reach the landing, {state.Fp} left.")
			: new("land", null, $"The next fungus costs {cost} FP with the way to the landing; only {state.Fp} left: refill first.");
	}
}

/// <summary>The client side of AF-06: fly to a fungus, hover in range and shoot it down with Smite.</summary>
public static class NaturalAirCombat
{
	public const int AbyssFungusNpcId = 700092;
	/// <summary>Smite ranks: the Priest's level 6 rank first, then the level 1 rank.</summary>
	private static readonly ushort[] SmiteIds = [4013, 4012];

	/// <summary>
	/// Where to shoot a fungus from, and the flight there: a point within Smite range of it, in sight of it, inside the FLY
	/// zone, that <see cref="NaturalFlightProtocol.Plan"/> can fly to. The shortest flight wins. Fungus near the floating
	/// island's underside are seen from only a few angles.
	/// </summary>
	public static (BotPosition Hover, NaturalFlightRoute Route)? FindHover(BotNavigationGeometry geometry, int mapId,
		IReadOnlyList<NaturalFlyZone> zones, BotPosition from, BotPosition fungus, float cruiseZ)
	{
		var candidates = new List<BotPosition>();
		foreach (float radius in new[] { NaturalAirCombatPolicy.HoverDistance, 10f, 20f })
			foreach (float dz in new[] { 0f, 6f, -6f, -12f, 12f })
				for (int sector = 0; sector < 16; sector++)
				{
					float angle = sector * MathF.PI / 8;
					var hover = new BotPosition(fungus.X + radius * MathF.Cos(angle), fungus.Y + radius * MathF.Sin(angle), fungus.Z + dz, 0);
					if (zones.Any(zone => !zone.Forbids && zone.Contains(hover.X, hover.Y, hover.Z)) && geometry.HasLineOfSight(mapId, hover, fungus))
						candidates.Add(hover);
				}
		(BotPosition Hover, NaturalFlightRoute Route)? best = null;
		foreach (BotPosition hover in candidates.OrderBy(hover => NaturalFlightPolicy.Distance(from, hover)).Take(12))
		{
			NaturalFlightRoute route = NaturalFlightProtocol.Plan(geometry, mapId, from, hover, cruiseZ);
			if (route.IsUsable && (best == null || route.Meters < best.Value.Route.Meters))
				best = (hover, route);
		}
		return best;
	}

	/// <summary>Shoot the fungus down from where the bot hovers. True when its death or the quest's counter was observed.</summary>
	public static async Task<bool> ShootDownAsync(INaturalJourneySession session, int target, int questId,
		Func<BotPosition, ushort, byte, int, SpellCastData> createCast, CancellationToken token, int maximumCasts = 12)
	{
		ushort skillId = SmiteIds.First(id => session.Api.World.Skills.ContainsKey(id));
		byte level = checked((byte)session.Api.World.Skills[skillId].Level);
		int varBefore = QuestVar(session, questId);
		int watched = session.PacketHistory.Count;
		await session.SendPacketAsync(session.Api.Target(target), token);
		for (int cast = 0; cast < maximumCasts; cast++)
		{
			TimeSpan gate = session.Api.Timing.TimeUntilCast(skillId);
			if (gate > TimeSpan.Zero) await session.AdvanceAsync(gate + TimeSpan.FromMilliseconds(1), token);
			await session.SendPacketAsync(session.Api.Cast(createCast(session.CurrentPosition, skillId, level, target)), token);
			DecodedBotServerPacket started = await BotCastProtocol.WaitForStartAsync(
				(predicate, waitToken) => session.WaitForPacketAsync(packet => predicate(packet) || BotCastProtocol.IsStartRejection(packet) ||
					IsInvalidTarget(packet), waitToken),
				session.CharacterId, skillId, token);
			// The target is dead or gone (its corpse lingers until SM_DELETE): the attempt is over.
			if (IsInvalidTarget(started)) return false;
			if (started.PacketType == typeof(SM_CASTSPELL))
			{
				await session.AdvanceAsync(TimeSpan.FromMilliseconds(started.Get<ushort>("castDuration") + 1), token);
				DecodedBotServerPacket result = await BotCastProtocol.WaitForCompletionAsync(session.WaitForPacketAsync, session.CharacterId, skillId, token);
				await session.AdvanceAsync(BotCastProtocol.RecoveryDelay(result), token);
			}
			else
				await session.AdvanceAsync(TimeSpan.FromMilliseconds(BotCastProtocol.ReactionMillis), token);
			await session.SynchronizeAsync(token);
			bool dead = session.PacketHistory.Skip(watched).Any(packet =>
				packet.PacketType == typeof(SmAttackStatus) && packet.Get<int>("objectId") == target && packet.Get<byte>("hpOrMp") == 0) ||
				!session.Api.World.Objects.ContainsKey(target);
			if (dead || QuestVar(session, questId) != varBefore || QuestStatus(session, questId) != 3) return true;
		}
		return false;
	}

	/// <summary>The quest's first variable as the client sees it (the low six bits of the step word).</summary>
	public static int QuestVar(INaturalJourneySession session, int questId) =>
		session.Api.World.Quests.TryGetValue(questId, out BotQuestState? quest) ? quest.StepAndFlags & 0x3F : -1;

	public static byte QuestStatus(INaturalJourneySession session, int questId) =>
		session.Api.World.Quests.TryGetValue(questId, out BotQuestState? quest) ? quest.Status : (byte)0;

	private static bool IsInvalidTarget(DecodedBotServerPacket packet) =>
		packet.PacketType == typeof(SM_SYSTEM_MESSAGE) && packet.Get<object>("name") is "STR_SKILL_TARGET_IS_NOT_VALID";

	/// <summary>Visible Abyss Fungus, leaving out the ones already shot down (a corpse stays visible until SM_DELETE).</summary>
	public static IReadOnlyList<(int ObjectId, BotPosition Position)> VisibleFungus(INaturalJourneySession session,
		IReadOnlySet<int> shotDown) =>
		session.Api.World.Objects.Values.Where(known => known.TemplateId == AbyssFungusNpcId && !shotDown.Contains(known.ObjectId))
			.Select(known => (known.ObjectId, known.Position)).ToArray();
}
