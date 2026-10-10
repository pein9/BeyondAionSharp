using Aion.Bots.Navigation;
using Aion.Bots.Protocol;
using Aion.Bots.World;
using Aion.GameServer.Controllers.Movement;
using Aion.GameServer.Model.Templates.Npc;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;

namespace Aion.Bots.Scenarios.Classes;

/// <summary>
/// NR-110c: the bot's own spirit in a fight. The server swings for no spirit and walks none (NR-110a). Its owner's client
/// gives the order (CM_SUMMON_COMMAND; Java SummonsService.doMode 184-216 takes ATTACK only for a creature the spirit
/// knows and may attack, and attackMode 168-174 then keeps the mode and nothing more), walks it (CM_SUMMON_MOVE, NR-110b)
/// and reports each swing (CM_SUMMON_ATTACK). Java SummonController.attackTarget 77-91 refuses a swing that comes sooner
/// than the spirit's attack speed less 50 ms, and checks neither range nor sight. So the bot keeps both as a client does:
/// the spirit runs to its own reach of the target at its own speed and swings from there.
/// A monster keeps a spirit and its master as two enemies (Java AggroList.addDamage 37-52: the hate is ten times the
/// damage, of the one who dealt it) and turns on the one it hates more.
/// The spirit acts in beats, between two acts of the bot. Nothing here waits, the opening apart.
/// NR-110d: an order of the master (a skill with Java PetOrderUseUltraSkillEffect) makes the spirit cast nothing by
/// itself. The server queues the spirit's skill and tells the master's client with SM_SUMMON_USESKILL; the spirit casts
/// when the client answers with CM_SUMMON_CASTSPELL, which a beat does.
/// </summary>
internal sealed class NaturalSpiritDriver(INaturalJourneySession session, NaturalJourneyRuntime runtime, BotNavigationGeometry geometry)
{
	/// <summary>Java SummonMode: the ids CM_SUMMON_COMMAND carries.</summary>
	private const byte AttackMode = 0, GuardMode = 1;

	/// <summary>How long the bot holds its own first attack for the spirit's first hit.</summary>
	public const int OpeningMillis = 8000;

	/// <summary>NR-110f: a spirit that is held back from a target among other monsters is sent once the target is this
	/// near its master, so that it fights beside its master and outside the others' circles.</summary>
	public const float MeetMetres = 8f;
	private const int OpeningBeatMillis = 500;

	// The target the spirit is on, 0 for none, and the spirit that was sent at it.
	private int target, spiritId;
	private bool walking;
	private BotPosition place;
	private long walkedAtMillis, swungAtMillis = long.MinValue / 2;

	private BotWorldModel World => session.Api.World;

	private (BotOwnSummon Spirit, BotKnownObject Seen, NpcTemplate Kind)? Own() =>
		World.Summon is { } spirit && World.Objects.TryGetValue(spirit.ObjectId, out BotKnownObject? seen) && seen.TemplateId is int kind &&
		runtime.Data.NpcDataDh.GetNpcTemplate(kind) is { } template ? (spirit, seen, template) : null;

	/// <summary>NR-110f: the spirit has been sent at this target in this fight.</summary>
	public bool Sent(int prey) => target == prey && World.Summon?.ObjectId == spiritId;

	/// <summary>NR-110d: the spirit was sent at this target and stands in its own reach of it, where its skills reach.</summary>
	public bool At(int prey) => target == prey && !walking && World.Summon?.ObjectId == spiritId;

	/// <summary>NR-110d: answers every skill the server has asked the spirit for (SM_SUMMON_USESKILL) with
	/// CM_SUMMON_CASTSPELL, which is what makes the spirit cast it. Without a spirit the list is empty.</summary>
	public async Task AnswerOrdersAsync(CancellationToken token)
	{
		foreach (BotSummonSkillOrder order in World.TakeSummonSkillOrders())
		{
			await session.SendPacketAsync(GameClientPackets.SummonCastSpell(order.SummonObjectId, order.SkillId, order.SkillLevel,
				order.TargetObjectId), token);
			session.TraceDiagnostic("combat-spirit-order-answered", new Dictionary<string, object?>
			{
				["spiritObjectId"] = order.SummonObjectId, ["skillId"] = (int)order.SkillId, ["skillLevel"] = (int)order.SkillLevel,
				["targetObjectId"] = order.TargetObjectId,
			});
		}
	}

	/// <summary>Tells the bot's own walks how fast its spirit runs: the client's data of its kind. The server names no
	/// speed for it.</summary>
	public void Know()
	{
		if (World.SummonSpeed != null || Own() is not { } own || own.Kind.GetStatsTemplate() is not { } stats) return;
		float speed = stats.GetRunSpeedFight() > 0 ? stats.GetRunSpeedFight() : stats.GetRunSpeed();
		if (speed > 0) World.SummonSpeed = speed;
	}

	/// <summary>
	/// One beat of the spirit's fight: the order when the target is new, the stretch it ran since the beat before, and a
	/// swing when it stands in its reach of the target and its attack speed allows one. Without a spirit it does nothing.
	/// </summary>
	public async Task BeatAsync(int prey, CancellationToken token)
	{
		await AnswerOrdersAsync(token);
		if (Own() is not { } own || !World.Objects.TryGetValue(prey, out BotKnownObject? seen) || seen.IsCorpse) return;
		Know();
		long now = runtime.NowMillis;
		if (target != prey || spiritId != own.Spirit.ObjectId)
		{
			await session.SendPacketAsync(GameClientPackets.SummonCommand(AttackMode, prey), token);
			target = prey;
			spiritId = own.Spirit.ObjectId;
			walking = false;
			World.SummonFights = true;
			session.TraceDiagnostic("combat-spirit-sent", new Dictionary<string, object?>
			{
				["spiritObjectId"] = spiritId, ["spiritNpcId"] = own.Seen.TemplateId, ["targetObjectId"] = prey,
				["distance"] = Distance(own.Seen.Position, seen.Position), ["speed"] = World.SummonSpeed,
				["targetFromMaster"] = Distance(session.CurrentPosition, seen.Position),
			});
		}
		BotPosition at = walking ? place : own.Seen.Position;
		float reach = MathF.Max(1f, own.Kind.GetAttackRange());
		// Half a metre inside its reach, so that a step of the target does not put it out again.
		float stand = MathF.Max(0.5f, reach - 0.5f);
		float gap = Distance(at, seen.Position);
		if (walking)
		{
			float stride = (World.SummonSpeed ?? 6f) * (now - walkedAtMillis) / 1000f;
			bool arrives = gap - stand <= stride;
			at = Toward(at, seen.Position, MathF.Max(0, arrives ? gap - stand : stride));
			place = at;
			walkedAtMillis = now;
			walking = !arrives;
			await session.SendPacketAsync(GameClientPackets.SummonMove(new MovementPacketData(at.X, at.Y, at.Z, at.Heading,
				arrives ? MovementMask.IMMEDIATE : MovementMask.POSITION, ObjectId: spiritId)), token);
		}
		else if (gap > reach)
		{
			// The start names where the spirit will stand; the server drops a spirit's start that names no point
			// (Java CM_SUMMON_MOVE.runImpl 83-84).
			BotPosition end = Toward(at, seen.Position, gap - stand);
			place = at;
			walkedAtMillis = now;
			walking = true;
			await session.SendPacketAsync(GameClientPackets.SummonMove(new MovementPacketData(at.X, at.Y, at.Z, end.Heading,
				(byte)(MovementMask.POSITION | MovementMask.MANUAL | MovementMask.ABSOLUTE), ObjectId: spiritId,
				X2: end.X, Y2: end.Y, Z2: end.Z)), token);
		}
		int swing = own.Kind.GetAttackSpeed() > 0 ? own.Kind.GetAttackSpeed() : 2000;
		if (!walking && Distance(at, seen.Position) <= reach + 0.5f && now - swungAtMillis >= swing)
		{
			swungAtMillis = now;
			await session.SendPacketAsync(GameClientPackets.SummonAttack(spiritId, prey, 0, 0, 0), token);
		}
	}

	/// <summary>
	/// The opening: the spirit goes first. The bot holds its own first attack until the spirit's first hit on the target
	/// is seen, so that the monster turns on the spirit, and no longer than <see cref="OpeningMillis"/>. It holds
	/// nothing when it has no spirit and when something is on it already. True when time has passed: the fight then
	/// looks again before it acts.
	/// </summary>
	public async Task<bool> OpenAsync(int prey, bool attacked, CancellationToken token)
	{
		if (Own() is null) return false;
		int seenFrom = session.PacketHistory.Count;
		long started = runtime.NowMillis;
		await BeatAsync(prey, token);
		if (attacked || target != prey) return false;
		string ended = "time";
		while (runtime.NowMillis - started < OpeningMillis)
		{
			await session.AdvanceAsync(TimeSpan.FromMilliseconds(OpeningBeatMillis), token);
			await session.SynchronizeAsync(token);
			if (World.IsDead || Own() is null || !World.Objects.TryGetValue(prey, out BotKnownObject? seen) || seen.IsCorpse)
			{
				ended = "gone";
				break;
			}
			DecodedBotServerPacket[] since = session.PacketHistory.Skip(seenFrom).ToArray();
			if (since.Any(packet => packet.PacketType == typeof(SM_ATTACK) && packet.Get<int>("attackerObjId") == spiritId &&
				packet.Get<int>("targetObjId") == prey))
			{
				ended = "spirit-hit";
				break;
			}
			if (since.Any(packet => runtime.IncomingAttacker(packet, session.CharacterId) != null))
			{
				ended = "bot-attacked";
				break;
			}
			await BeatAsync(prey, token);
		}
		session.TraceDiagnostic("combat-spirit-opening", new Dictionary<string, object?>
		{
			["spiritObjectId"] = spiritId, ["targetObjectId"] = prey, ["ended"] = ended, ["millis"] = runtime.NowMillis - started,
		});
		return true;
	}

	/// <summary>The fight is over, or the bot runs: the spirit stops where it is and guards. The bot's next walk takes it
	/// along (BotMover.WeaveSpirit).</summary>
	public async Task StandDownAsync(CancellationToken token)
	{
		await AnswerOrdersAsync(token);
		if (target == 0) return;
		int was = target;
		target = 0;
		bool stops = walking;
		walking = false;
		// A spirit that is gone has taken its orders with it (BotWorldModel.ApplySummonOwnerRemove).
		if (World.Summon?.ObjectId != spiritId) return;
		World.SummonFights = false;
		if (stops)
			await session.SendPacketAsync(GameClientPackets.SummonMove(new MovementPacketData(place.X, place.Y, place.Z, place.Heading,
				MovementMask.IMMEDIATE, ObjectId: spiritId)), token);
		await session.SendPacketAsync(GameClientPackets.SummonCommand(GuardMode, 0), token);
		session.TraceDiagnostic("combat-spirit-called-back", new Dictionary<string, object?>
		{
			["spiritObjectId"] = spiritId, ["targetObjectId"] = was,
		});
	}

	/// <summary>The point <paramref name="metres"/> from one place towards another, on the ground when the map has
	/// ground there.</summary>
	private BotPosition Toward(BotPosition from, BotPosition to, float metres)
	{
		float span = Distance(from, to);
		float part = span <= 0 ? 0 : Math.Clamp(metres / span, 0, 1);
		byte heading = PositionUtil.GetHeadingTowards(from.X, from.Y, to.X, to.Y);
		var point = new BotPosition(from.X + (to.X - from.X) * part, from.Y + (to.Y - from.Y) * part, from.Z + (to.Z - from.Z) * part, heading);
		return World.MapId is int map && geometry.SnapToGround(map, point with { Z = point.Z + 2 }) is { } ground
			? ground with { Heading = heading } : point;
	}

	private static float Distance(BotPosition a, BotPosition b) =>
		MathF.Sqrt(MathF.Pow(a.X - b.X, 2) + MathF.Pow(a.Y - b.Y, 2) + MathF.Pow(a.Z - b.Z, 2));
}
