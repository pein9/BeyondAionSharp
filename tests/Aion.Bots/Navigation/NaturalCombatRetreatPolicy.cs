using Aion.Bots.Protocol;
using Aion.Bots.World;
using Aion.GameServer.Model;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.Bots.Navigation;

/// <summary>Retreat only toward client-estimated ground the bot previously walked.
/// The caller must recheck every proposed route against current geometry and mobs.</summary>
public static class NaturalCombatRetreatPolicy
{
	/// <summary>Allow short terrain detours but reject a route that first runs into the
	/// attackers' centre. The destination itself is already selected away from them.</summary>
	public static bool ClearsPackOnDeparture(BotPosition current, IReadOnlyList<BotPosition> route,
		IReadOnlyList<BotPosition> attackers)
	{
		if (attackers.Count == 0) return false;
		float centreX = attackers.Average(point => point.X), centreY = attackers.Average(point => point.Y);
		float startingClearance = MathF.Sqrt(MathF.Pow(current.X - centreX, 2) + MathF.Pow(current.Y - centreY, 2));
		float travelled = 0;
		BotPosition previous = current;
		foreach (BotPosition point in route)
		{
			travelled += Horizontal(previous, point);
			if (travelled > 12) break;
			float clearance = MathF.Sqrt(MathF.Pow(point.X - centreX, 2) + MathF.Pow(point.Y - centreY, 2));
			if (clearance + 2 < startingClearance) return false;
			previous = point;
		}
		return true;
	}

	/// <summary>Track the monsters still fighting this client. Java EmoteManager sends
	/// NEUTRALMODE_IN_MOVE when an NPC returns or idles; a later attack re-engages it.
	/// SM_DELETE also ends the client's observation of that pursuer.</summary>
	/// <param name="spirit">NR-110e: the bot's own spirit; a strike at it engages the striker as a strike at the bot does.</param>
	public static void ObserveEngagement(HashSet<int> attackers,
		IEnumerable<DecodedBotServerPacket> packets, int characterId, Func<int, string?>? localizedName = null,
		Func<int, bool>? hostileSkill = null, int? spirit = null)
	{
		foreach (DecodedBotServerPacket packet in packets)
		{
			if (IncomingAttacker(packet, characterId, hostileSkill, spirit) is int attacker)
				attackers.Add(attacker);
			else if (packet.PacketType == typeof(SM_EMOTION) &&
				packet.Get<byte>("emotionType") == (byte)EmotionType.NEUTRALMODE_IN_MOVE)
				attackers.Remove(packet.Get<int>("senderObjectId"));
			else if (packet.PacketType == typeof(SM_DELETE))
				attackers.Remove(packet.Get<int>("objectId"));
			else if (localizedName != null && packet.PacketType == typeof(SM_SYSTEM_MESSAGE))
				// Java EmoteManager addresses this message to the former player target. It carries
				// a localized NPC name, not an object ID; later actual hits restore any engagement.
				attackers.RemoveWhere(id => IsReturnMessage(packet, localizedName(id)));
		}
	}

	/// <summary>Stop attacking a monster that the client observed giving up. Never count this
	/// as a kill. A subsequent swing at this player cancels the earlier return observation.</summary>
	/// <param name="spirit">NR-110e: the bot's own spirit; a later swing at it cancels the return observation too.</param>
	public static bool TargetReturned(IEnumerable<DecodedBotServerPacket> packets, int target,
		int characterId, string? localizedName, Func<int, bool>? hostileSkill = null, int? spirit = null)
	{
		bool returned = false;
		foreach (DecodedBotServerPacket packet in packets)
		{
			if (packet.PacketType == typeof(SM_EMOTION) && packet.Get<int>("senderObjectId") == target &&
				packet.Get<byte>("emotionType") == (byte)EmotionType.NEUTRALMODE_IN_MOVE ||
				IsReturnMessage(packet, localizedName))
				returned = true;
			else if (IncomingAttacker(packet, characterId, hostileSkill, spirit) == target)
				returned = false;
		}
		return returned;
	}

	/// <summary>Java broadcasts targeted spell windups and results separately from SM_ATTACK.
	/// Only a shipped hostile skill aimed at this character is engagement; heals, buffs,
	/// self casts and ground targets are not evidence of an incoming attack.</summary>
	/// <param name="spirit">NR-110e: the bot's own spirit, or null. A monster keeps a spirit and its master as two
	/// enemies and strikes the one it hates more (Java AggroList.addDamage 37-52, AttackManager 75-80), and the server
	/// shows a strike at the spirit to everyone who sees it, as it shows one at the master. With a spirit named, a
	/// swing or a hostile skill aimed at the spirit names its attacker as one aimed at the bot does. What the bot and
	/// its spirit cast on each other names none: an order and the spirit's own answer to it are of a hostile kind.</param>
	public static int? IncomingAttacker(DecodedBotServerPacket packet, int characterId,
		Func<int, bool>? hostileSkill, int? spirit = null)
	{
		bool Aimed(int at) => at == characterId || at == spirit;
		bool Ours(int by) => by == characterId || by == spirit;
		if (packet.PacketType == typeof(SM_ATTACK) && Aimed(packet.Get<int>("targetObjId")) && !Ours(packet.Get<int>("attackerObjId")))
			return packet.Get<int>("attackerObjId");
		if (hostileSkill == null) return null;
		if (packet.PacketType == typeof(SM_CASTSPELL) && packet.Get<byte>("targetType") is 0 or 3 or 4 &&
			Aimed(packet.Get<int>("targetObjectId")) && !Ours(packet.Get<int>("objectId")) &&
			hostileSkill(packet.Get<ushort>("spellId")))
			return packet.Get<int>("objectId");
		if (packet.PacketType == typeof(SM_CASTSPELL_RESULT) && packet.Get<byte>("targetType") is 0 or 3 or 4 &&
			Aimed(packet.Get<int>("targetId")) && !Ours(packet.Get<int>("effectorId")) &&
			hostileSkill(packet.Get<ushort>("skillId")))
			return packet.Get<int>("effectorId");
		return null;
	}

	private static bool IsReturnMessage(DecodedBotServerPacket packet, string? localizedName) =>
		localizedName != null && packet.PacketType == typeof(SM_SYSTEM_MESSAGE) &&
		packet.Get<object>("name") is "STR_UI_COMBAT_NPC_RETURN" &&
		packet.Get<string[]>("params").Contains(localizedName, StringComparer.Ordinal);

	public static BotPosition[] SelectCheckpoints(BotPosition current, BotPosition refuge,
		IEnumerable<NaturalNavigationEvent> events, IReadOnlyList<BotPosition> observedAttackers)
	{
		ArgumentNullException.ThrowIfNull(events);
		ArgumentNullException.ThrowIfNull(observedAttackers);
		float currentClearance = Clearance(current);
		var selected = new List<BotPosition>();
		foreach (BotPosition checkpoint in events.Reverse()
			.Where(item => item.Action == "segment-progress" && item.Position != null)
			.Select(item => item.Position!.Value).Append(refuge))
		{
			float travel = Distance(current, checkpoint);
			if (travel is < 30 or > 150 || Clearance(checkpoint) < 35 ||
				Clearance(checkpoint) < currentClearance + 12 ||
				selected.Any(other => Distance(other, checkpoint) < 12)) continue;
			selected.Add(checkpoint);
			if (selected.Count == 8) break;
		}
		return selected.ToArray();

		float Clearance(BotPosition point) => observedAttackers.Count == 0 ? float.PositiveInfinity :
			observedAttackers.Min(attacker => Distance(point, attacker));
	}

	/// <summary>Java <c>AttackManager.checkGiveupDistance</c> with Ishalgen's default <c>ai_info</c>
	/// (chase_home 200): a chasing monster gives up once it is more than half that, 100 m, from home and
	/// has not been hit for 10 s. An equal-speed chaser cannot be outrun, so an escape has to drag
	/// the pack that far from home.</summary>
	public const float GiveUpDistanceFromHome = 100f;

	/// <summary>
	/// Escape destinations, best first. Every candidate lies ahead of the bot, away from the
	/// attackers' current centre: the angle between "candidate minus bot" and "bot minus attackers"
	/// is under about 80 degrees. None lies inside another observed hostile's circle. They are ranked
	/// by how far they are from the nearest attacker's home, since give-up is measured from home, and
	/// then by shorter travel. Unlike <see cref="SelectCheckpoints"/>, which re-measured clearance from
	/// chasers that move with the bot, this cannot choose a point back toward the pack.
	/// </summary>
	public static BotPosition[] SelectEscape(BotPosition current, IReadOnlyList<BotPosition> attackers,
		IReadOnlyList<BotPosition> attackerHomes, IEnumerable<BotPosition> candidates,
		IReadOnlyList<BotNavigationHazard> otherHazards, int maximum = 8)
	{
		ArgumentNullException.ThrowIfNull(attackers);
		ArgumentNullException.ThrowIfNull(attackerHomes);
		ArgumentNullException.ThrowIfNull(candidates);
		ArgumentNullException.ThrowIfNull(otherHazards);
		if (attackers.Count == 0) return candidates.Take(maximum).ToArray();
		float cx = attackers.Average(a => a.X), cy = attackers.Average(a => a.Y);
		float awayX = current.X - cx, awayY = current.Y - cy;
		float awayLength = MathF.Sqrt(awayX * awayX + awayY * awayY);
		if (awayLength < 0.5f)
		{
			// Standing on the pack's centre: away from the homes instead.
			awayX = current.X - attackerHomes.DefaultIfEmpty(current).Average(h => h.X);
			awayY = current.Y - attackerHomes.DefaultIfEmpty(current).Average(h => h.Y);
			awayLength = MathF.Max(0.5f, MathF.Sqrt(awayX * awayX + awayY * awayY));
		}
		IReadOnlyList<BotPosition> homes = attackerHomes.Count > 0 ? attackerHomes : attackers;
		var ranked = new List<(BotPosition Point, float Score)>();
		foreach (BotPosition candidate in candidates)
		{
			float dx = candidate.X - current.X, dy = candidate.Y - current.Y;
			float travel = MathF.Sqrt(dx * dx + dy * dy);
			if (travel < 20) continue;
			float cosine = (dx * awayX + dy * awayY) / (travel * awayLength);
			if (cosine < 0.17f) continue;
			if (otherHazards.Any(h => Horizontal(candidate, h.Position) < h.Radius + 2)) continue;
			float fromHome = homes.Min(h => Horizontal(candidate, h));
			float score = MathF.Min(fromHome, GiveUpDistanceFromHome + 30) * 2 + cosine * 20 - travel * 0.25f;
			ranked.Add((candidate, score));
		}
		var chosen = new List<BotPosition>();
		foreach (var (point, _) in ranked.OrderByDescending(r => r.Score).ThenBy(r => r.Point.X).ThenBy(r => r.Point.Y))
		{
			if (chosen.Any(other => Horizontal(other, point) < 10)) continue;
			chosen.Add(point);
			if (chosen.Count == maximum) break;
		}
		return chosen.ToArray();
	}

	private static float Horizontal(BotPosition a, BotPosition b) => MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

	private static float Distance(BotPosition a, BotPosition b) => MathF.Sqrt(
		MathF.Pow(a.X - b.X, 2) + MathF.Pow(a.Y - b.Y, 2) + MathF.Pow(a.Z - b.Z, 2));
}
