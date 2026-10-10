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
/// NR-36: how a class kills in flight. With a skill it hovers inside the skill's reach and casts it; with none it flies
/// into its weapon's reach and swings (Java PlayerController.attackTarget has no flight condition; a skill's are its
/// template's start conditions).
/// </summary>
/// <param name="SkillId">The skill cast, or null for the weapon's swing.</param>
/// <param name="Reach">How far the skill reaches; for a swing, the weapon's attack range and the metre the server adds
/// to it (NR-53a; Java PlayerController.attackTarget 403-405). The two bodies' bound radii, which the server adds too
/// (PositionUtil.isInRange 243-251), are left as margin.</param>
/// <param name="SwingMillis">The weapon's attack speed, for a swing.</param>
public sealed record NaturalAirAttack(ushort? SkillId, float Reach, int SwingMillis = 0)
{
	/// <summary>Where the bot hovers: ten metres inside the reach, but no nearer than three metres; with a reach shorter
	/// than that, half a metre inside it. Smite's 25 m gives the 15 m the fungus fight has always used.</summary>
	public float HoverDistance => MathF.Max(MathF.Min(Reach - 0.5f, 3f), Reach - 10f);
}

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

	/// <param name="hoverDistance">NR-36: how near the class's air attack hovers; the Cleric's 15 m when not given.</param>
	public static NaturalAirCombatDecision Decide(NaturalAirCombatObservation state, float hoverDistance = HoverDistance)
	{
		if (state.Targets.Count == 0)
			return new("wait", null, "No fungus is visible; hold until one respawns (20 s).");
		(int id, BotPosition at) = state.Targets.OrderBy(target => NaturalFlightPolicy.Distance(state.Position, target.Position)).First();
		float toTarget = MathF.Max(0, NaturalFlightPolicy.Distance(state.Position, at) - hoverDistance);
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
	/// <summary>Smite ranks, the highest learned first (AC-00: the Cleric's level 11 and 16 ranks). The shot of a caller
	/// that names no air attack: the Cleric's probes.</summary>
	private static readonly ushort[] SmiteIds = [4015, 4014, 4013, 4012];

	/// <summary>
	/// NR-36: the class's air attack. Of the roles its profile names (<see cref="Classes.NaturalClassProfile.AirAttackRoles"/>),
	/// one whose best learned skill may be cast in flight at any target and needs no earlier chain step. Failing that,
	/// the swing of the held weapon.
	/// <para>
	/// NR-R3b: of those skills, the one that is ready again soonest, by its cooldown and its cast time; the profile's
	/// order decides between equals. A fungus takes two or three casts, and a skill that is long in coming again spends
	/// the flight time waiting: with Infernal Blaze, the first of its list from range and ready every 24 s, the Chanter
	/// spent 56 FP on two fungi and had no way back to its landing. A profile that names one role shoots as before.
	/// </para>
	/// </summary>
	/// <param name="learned">Whether the client's skill list holds a skill id.</param>
	public static NaturalAirAttack AttackFor(Classes.NaturalClassProfile profile, Func<int, bool> learned, int? weaponAttackRangeMillis,
		int? weaponAttackSpeedMillis)
	{
		// OrderBy keeps the order of equals, which is the profile's.
		NaturalPriestSkill? shot = profile.AirAttackRoles
			.Select(role => profile.Skills.Where(skill => skill.Role == role && learned(skill.Id))
				.OrderByDescending(skill => skill.MinimumLevel).ThenByDescending(skill => skill.Id).FirstOrDefault())
			.OfType<NaturalPriestSkill>()
			.Where(best => best is { GroundOnly: false, TargetFlight: null, RequiresChainCategory: null })
			.OrderBy(best => best.CooldownDeciseconds * 100 + best.CastMillis)
			.FirstOrDefault();
		if (shot != null) return new(shot.Id, Classes.NaturalSkillCatalog.Reach(shot, weaponAttackRangeMillis));
		return new(null, (weaponAttackRangeMillis ?? 1500) / 1000f + 1f, weaponAttackSpeedMillis is > 0 and int speed ? speed : 2500);
	}

	/// <summary>
	/// Where to shoot a fungus from, and the flight there: a point within Smite range of it, in sight of it, inside the FLY
	/// zone, that <see cref="NaturalFlightProtocol.Plan"/> can fly to. The shortest flight wins. Fungus near the floating
	/// island's underside are seen from only a few angles.
	/// </summary>
	/// <param name="hoverDistance">NR-36: how near the class's air attack hovers; the Cleric's 15 m when not given. The
	/// search also tries two thirds and four thirds of it: 10 m and 20 m for the Cleric, as before.</param>
	/// <param name="reach">NR-53a: how far the attack reaches. A point farther from the fungus than that, less a quarter
	/// metre, is no hover point: the search goes 6 m and 12 m above and below, which Smite's 25 m covers and a weapon's
	/// swing does not. Not given: every point counts, as before.</param>
	public static (BotPosition Hover, NaturalFlightRoute Route)? FindHover(BotNavigationGeometry geometry, int mapId,
		IReadOnlyList<NaturalFlyZone> zones, BotPosition from, BotPosition fungus, float cruiseZ,
		float hoverDistance = NaturalAirCombatPolicy.HoverDistance, float? reach = null)
	{
		var candidates = new List<BotPosition>();
		// A hover nearer than 6 m also looks half its own distance above and below.
		float[] heights = hoverDistance < 6f ? [0f, 6f, -6f, -12f, 12f, hoverDistance / 2, -hoverDistance / 2] : [0f, 6f, -6f, -12f, 12f];
		foreach (float radius in new[] { hoverDistance, hoverDistance * 2 / 3, hoverDistance * 4 / 3 })
			foreach (float dz in heights)
				for (int sector = 0; sector < 16; sector++)
				{
					if (reach is float limit && MathF.Sqrt(radius * radius + dz * dz) > limit - 0.25f) continue;
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

	/// <summary>Shoot the fungus down from where the bot hovers. True when its death (0% HP) or, for a real quest, the
	/// quest's counter was observed. A target that only leaves view is not a kill, and quest id 0 (the SIM probes) has no
	/// counter: before, <c>QuestStatus(0)</c> was 0, so "status left START" held after the first cast and a live target
	/// (Komu, in run-fast) was reported killed.</summary>
	/// <param name="attack">NR-36: the class's air attack; the Cleric's Smite when not given.</param>
	public static async Task<bool> ShootDownAsync(INaturalJourneySession session, int target, int questId,
		Func<BotPosition, ushort, byte, int, SpellCastData> createCast, CancellationToken token, int maximumCasts = 12,
		NaturalAirAttack? attack = null)
	{
		int varBefore = QuestVar(session, questId);
		int watched = session.PacketHistory.Count;
		await session.SendPacketAsync(session.Api.Target(target), token);
		bool DeathSeen() => session.PacketHistory.Skip(watched).Any(packet =>
			BotAttackStatus.CarriesHp(packet) && packet.Get<int>("objectId") == target && packet.Get<byte>("hpOrMp") == 0);
		bool CounterMoved() => questId > 0 && (QuestVar(session, questId) != varBefore || QuestStatus(session, questId) != 3);
		if (attack is { SkillId: null })
		{
			// NR-36: no skill for the air: swing the weapon, at its own speed, three swings for each cast allowed.
			for (int swing = 0; swing < maximumCasts * 3; swing++)
			{
				if (session.Api.World.IsDead) return false;
				if (!session.Api.World.Objects.ContainsKey(target)) return DeathSeen() || CounterMoved();
				await session.SendPacketAsync(session.Api.Attack(target, attack.SwingMillis, (byte)(swing & 0xFF)), token);
				await session.AdvanceAsync(TimeSpan.FromMilliseconds(attack.SwingMillis + 100), token);
				await session.SynchronizeAsync(token);
				if (DeathSeen() || CounterMoved()) return true;
			}
			return false;
		}
		ushort skillId = attack?.SkillId ?? SmiteIds.First(id => session.Api.World.Skills.ContainsKey(id));
		byte level = checked((byte)session.Api.World.Skills[skillId].Level);
		for (int cast = 0; cast < maximumCasts; cast++)
		{
			if (session.Api.World.IsDead) return false;
			// Gone from view: a kill only if its death or the quest's counter was seen (it may just have left view).
			if (!session.Api.World.Objects.ContainsKey(target)) return DeathSeen() || CounterMoved();
			TimeSpan gate = session.Api.Timing.TimeUntilCast(skillId);
			if (gate > TimeSpan.Zero) await session.AdvanceAsync(gate + TimeSpan.FromMilliseconds(1), token);
			await session.SendPacketAsync(session.Api.Cast(createCast(session.CurrentPosition, skillId, level, target)), token);
			DecodedBotServerPacket started = await BotCastProtocol.WaitForStartAsync(
				(predicate, waitToken) => session.WaitForPacketAsync(packet => predicate(packet) || BotCastProtocol.IsStartRejection(packet) ||
					IsInvalidTarget(packet), waitToken),
				session.CharacterId, skillId, token);
			// A refused cast never starts on the server: release the local casting gate, as the journey's CastAsync does
			// (AC-08: without it the next cast threw). Out of range or sight, the caller has to move first.
			if (started.PacketType == typeof(SM_SYSTEM_MESSAGE))
			{
				session.Api.Timing.RecordCastCancelled();
				if (started.Get<object>("name") is "STR_SKILL_NOT_ENOUGH_DISTANCE" or "STR_SKILL_OBSTACLE") return false;
			}
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
			if (DeathSeen() || CounterMoved()) return true;
		}
		return false;
	}

	/// <summary>The quest's first variable as the client sees it (the low six bits of the step word).</summary>
	public static int QuestVar(INaturalJourneySession session, int questId) =>
		session.Api.World.Quests.TryGetValue(questId, out BotQuestState? quest) ? quest.StepAndFlags & 0x3F : -1;

	public static byte QuestStatus(INaturalJourneySession session, int questId) =>
		session.Api.World.Quests.TryGetValue(questId, out BotQuestState? quest) ? quest.Status : (byte)0;

	/// <summary>How an air-combat run went.</summary>
	public sealed record Outcome(int Kills, int Sorties, int Missed, IReadOnlyList<double> ShootingSeconds, int LandedFp);

	/// <summary>
	/// Shoot fungus down until the quest leaves START, refilling on <paramref name="landing"/> whenever the policy says so,
	/// and end landed there. The bot may start on the ground or on the landing; flight time is read from SM_FLY_TIME.
	/// </summary>
	public static async Task<Outcome> RunAsync(INaturalJourneySession session, BotNavigationGeometry geometry, int mapId,
		IReadOnlyList<NaturalFlyZone> zones, float waterLevel, NaturalLandingTarget landing, float cruiseZ, int questId,
		Func<BotPosition, ushort, byte, int, SpellCastData> createCast, Func<long> nowMillis, CancellationToken token,
		int maximumSorties = 6, NaturalAirAttack? attack = null)
	{
		BotWorldModel world = session.Api.World;
		// NR-36: the class's air attack decides how near it hovers; the Cleric's Smite when none is given.
		float hoverDistance = attack?.HoverDistance ?? NaturalAirCombatPolicy.HoverDistance;
		bool airborne = false;
		long? lastTakeoff = null;
		float speed = 0;
		int sorties = 0, kills = 0, missed = 0;
		var shooting = new List<double>();
		var shotDown = new HashSet<int>();
		// NR-53a: fungus no hover point reaches, left for another while another is in view.
		var outOfReach = new HashSet<int>();
		while (QuestStatus(session, questId) == 3)
		{
			if (world.IsDead) throw new InvalidDataException("The bot died during the air fight.");
			if (!airborne)
			{
				if (++sorties > maximumSorties) throw new InvalidDataException($"More than {maximumSorties} sorties for Q{questId}.");
				long wait = NaturalFlightPolicy.RestoreMillis(world.CurrentFlightTime, world.MaxFlightTime, world.MaxFlightTime);
				if (lastTakeoff is { } last) wait = Math.Max(wait, last + NaturalFlightPolicy.TakeoffReuseMillis - nowMillis());
				if (wait > 0) await session.AdvanceAsync(TimeSpan.FromMilliseconds(wait + 100), token);
				await session.SynchronizeAsync(token);
				NaturalFlightDecision ready = NaturalFlightPolicy.CanTakeOff(new NaturalTakeoffObservation(true, session.CurrentPosition, false,
					waterLevel, nowMillis(), lastTakeoff, false, false, false), zones);
				if (!ready.Allowed) throw new InvalidDataException($"Cannot take off for the air fight: {ready.Reason}");
				lastTakeoff = nowMillis();
				speed = await NaturalFlightProtocol.TakeOffAsync(session, token);
				airborne = true;
			}
			await session.SynchronizeAsync(token);
			var observation = new NaturalAirCombatObservation(session.CurrentPosition, world.CurrentFlightTime, speed,
				VisibleFungus(session, outOfReach.Count == 0 ? shotDown : shotDown.Concat(outOfReach).ToHashSet()), landing);
			NaturalAirCombatDecision decision = NaturalAirCombatPolicy.Decide(observation, hoverDistance);
			session.TraceDiagnostic("air-combat-decision", new Dictionary<string, object?>
			{
				["action"] = decision.Action, ["target"] = decision.Target, ["reason"] = decision.Reason, ["fp"] = observation.Fp,
				["var"] = QuestVar(session, questId),
			});
			if (decision.Action == "attack")
			{
				BotPosition fungus = observation.Targets.Single(target => target.ObjectId == decision.Target).Position;
				(BotPosition Hover, NaturalFlightRoute Route)? found = FindHover(geometry, mapId, zones, session.CurrentPosition, fungus, cruiseZ,
					hoverDistance, attack?.Reach);
				if (found == null)
				{
					if (observation.Targets.Count == 1)
						throw new InvalidDataException($"No hover point in sight of fungus {decision.Target} at {fungus}.");
					outOfReach.Add(decision.Target!.Value);
					session.TraceDiagnostic("air-combat-out-of-reach", new Dictionary<string, object?>
					{
						["target"] = decision.Target, ["position"] = fungus, ["hoverDistance"] = hoverDistance, ["reach"] = attack?.Reach,
						["others"] = observation.Targets.Count - 1,
					});
					continue;
				}
				NaturalFlightRoute route = found.Value.Route;
				await NaturalFlightProtocol.FlyAsync(session, mapId, session.CurrentPosition, route.Waypoints, speed, token);
				long started = nowMillis();
				bool killed = await ShootDownAsync(session, decision.Target!.Value, questId, createCast, token, attack: attack);
				shotDown.Add(decision.Target.Value);
				if (!killed)
				{
					if (++missed > 2) throw new InvalidDataException($"Fungus {decision.Target} was not shot down.");
					continue;
				}
				kills++;
				shooting.Add((nowMillis() - started) / 1000.0);
			}
			else
			{
				await LandAsync(session, geometry, mapId, landing, cruiseZ, speed, token);
				airborne = false;
			}
		}
		if (airborne) await LandAsync(session, geometry, mapId, landing, cruiseZ, speed, token);
		await session.SynchronizeAsync(token);
		return new(kills, sorties, missed, shooting, world.CurrentFlightTime);
	}

	private static async Task LandAsync(INaturalJourneySession session, BotNavigationGeometry geometry, int mapId,
		NaturalLandingTarget landing, float cruiseZ, float speed, CancellationToken token)
	{
		NaturalFlightRoute route = NaturalFlightProtocol.Plan(geometry, mapId, session.CurrentPosition, landing.Position, cruiseZ);
		if (!route.IsUsable) throw new InvalidDataException($"No flight to the landing: {route.Refusal}");
		await NaturalFlightProtocol.FlyAsync(session, mapId, session.CurrentPosition, route.Waypoints, speed, token);
		await NaturalFlightProtocol.LandAsync(session, token);
		if (session.Api.World.CurrentFlightTime < 1) throw new InvalidDataException("Flight time ran out before the landing.");
	}

	private static bool IsInvalidTarget(DecodedBotServerPacket packet) =>
		packet.PacketType == typeof(SM_SYSTEM_MESSAGE) && packet.Get<object>("name") is "STR_SKILL_TARGET_IS_NOT_VALID";

	/// <summary>Visible Abyss Fungus, leaving out the ones already shot down (a corpse stays visible until SM_DELETE).</summary>
	public static IReadOnlyList<(int ObjectId, BotPosition Position)> VisibleFungus(INaturalJourneySession session,
		IReadOnlySet<int> shotDown) =>
		session.Api.World.Objects.Values.Where(known => known.TemplateId == AbyssFungusNpcId && !shotDown.Contains(known.ObjectId))
			.Select(known => (known.ObjectId, known.Position)).ToArray();
}
