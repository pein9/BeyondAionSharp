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

	private void ForgetEffectObservations()
	{
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
