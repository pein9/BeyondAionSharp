using Aion.Bots.Protocol;

namespace Aion.Bots.World;

public sealed record BotVisibleEffect(int EffectorId, int SkillId, byte SkillLevel, byte TargetSlot, int RemainingMillis);
public sealed record BotAbyssRewardObservation(BotAbyssRank Before, BotAbyssRank After, IReadOnlyList<BotVisibleEffect>? Effects);

public sealed partial class BotWorldModel
{
	/// <summary>Latest complete visible-icon snapshot; null until observed for this world entry.</summary>
	public IReadOnlyList<BotVisibleEffect>? VisibleEffects { get; private set; }
	/// <summary>One immutable effect snapshot at the last AP/counter-changing rank packet, not leaderboard refreshes.</summary>
	public BotAbyssRewardObservation? LastAbyssReward { get; private set; }

	/// <summary>
	/// NR-70a: the toggle skills the server said are on and has not said are off (SM_SKILL_ACTIVATION; Java
	/// Effect.startEffect 670-678 and endEffect 735-737 tell the caster of every start and every end, a death included).
	/// A move to another map ends no toggle and tells nothing again, so a world reload keeps the set. A new login starts
	/// with none: Java PlayerLeaveWorldService 103-112 removes every effect and PlayerEffectsDAO stores no toggle.
	/// </summary>
	public IReadOnlySet<int> ActiveToggles => activeToggles;

	private readonly HashSet<int> activeToggles = [];

	private void ApplySkillActivation(DecodedBotServerPacket packet)
	{
		if (packet.Get<int>("kind") != 0) return; // a removed stigma, not a toggle
		int skillId = packet.Get<ushort>("skillId");
		if (packet.Get<bool>("active")) activeToggles.Add(skillId);
		else activeToggles.Remove(skillId);
	}

	/// <summary>
	/// NR-80a: a creature's abnormal states as the server last told them, as the bits of AbnormalState; 0 for one it told
	/// nothing of. Java EffectController.broadCastEffects 304-308 sends SM_ABNORMAL_EFFECT to everyone who sees the
	/// creature whenever one of its effects starts or ends, and PlayerController.see sends it for a creature that comes
	/// into sight with effects on it.
	/// </summary>
	public int AbnormalsOf(int objectId) => objectAbnormals.GetValueOrDefault(objectId);

	private readonly Dictionary<int, int> objectAbnormals = [];

	/// <summary>
	/// NR-90a: a creature's visible effects as the server last told them; none for one it told nothing of. The packet
	/// lists every effect of the slots it names (Java SM_ABNORMAL_EFFECT's constructor filters by the slot of the effect
	/// that started or ended; 127 is every slot): those are replaced and the others kept. A row's slot is the slot's
	/// place in Java's enum, and the packet's slots are its bits.
	/// </summary>
	public IReadOnlyList<BotVisibleEffect> EffectsOf(int objectId) => objectEffects.GetValueOrDefault(objectId) ?? [];

	private readonly Dictionary<int, BotVisibleEffect[]> objectEffects = [];

	private void ApplyCreatureEffects(DecodedBotServerPacket packet)
	{
		int objectId = packet.Get<int>("objectId");
		objectAbnormals[objectId] = packet.Get<int>("abnormals");
		int slots = packet.Get<byte>("slots");
		IEnumerable<BotVisibleEffect> told = packet.Get<List<IReadOnlyDictionary<string, object?>>>("effects")
			.Select(row => new BotVisibleEffect((int)row["effectorId"]!, (ushort)row["skillId"]!,
				(byte)row["skillLevel"]!, (byte)row["targetSlot"]!, (int)row["remainingMillis"]!));
		IEnumerable<BotVisibleEffect> kept = slots != 127 && objectEffects.TryGetValue(objectId, out BotVisibleEffect[]? before)
			? before.Where(effect => (slots & 1 << effect.TargetSlot) == 0) : [];
		objectEffects[objectId] = [.. kept, .. told];
	}

	private void ForgetEffectObservations()
	{
		objectAbnormals.Clear();
		objectEffects.Clear();
		VisibleEffects = null;
		LastAbyssReward = null;
	}

	private void ApplyVisibleEffects(DecodedBotServerPacket packet)
	{
		// PlayerEffectController.updatePlayerEffectIcons sends ALL visible effects,
		// even when slot identifies one changed slot. It is not a filtered slot delta.
		var effects = packet.Get<List<IReadOnlyDictionary<string, object?>>>("effects")
			.Select(row => new BotVisibleEffect((int)row["effectorId"]!, (ushort)row["skillId"]!,
				(byte)row["skillLevel"]!, (byte)row["targetSlot"]!, (int)row["remainingMillis"]!)).ToArray();
		if (effects.Length != packet.Get<ushort>("effectCount") || effects.Any(effect => effect.SkillId == 0 || effect.SkillLevel == 0 || effect.TargetSlot > 7) ||
			effects.Select(effect => (effect.EffectorId, effect.SkillId)).Distinct().Count() != effects.Length)
			throw new InvalidDataException("Invalid visible-effect snapshot.");
		// Duration is display evidence, not authority to remove an effect locally. Java
		// uses -1 for permanent effects and may serialize an expiry before its timer runs.
		VisibleEffects = Array.AsReadOnly(effects);
	}

	private void ObserveAbyssReward(BotAbyssRank? before, BotAbyssRank after)
	{
		if (before != null && (before.Ap != after.Ap || before.AllKills != after.AllKills ||
			before.Daily.Ap != after.Daily.Ap || before.Daily.Kills != after.Daily.Kills))
			LastAbyssReward = new(before, after, VisibleEffects);
	}
}
