using Aion.Bots.World;

namespace Aion.Bots.Scenarios;

/// <summary>One help scroll from the shipped item and skill data (docs/natural-ascension-altgard.md Appendix D).</summary>
/// <param name="Family">awakening, courage, running, movement-speed or anti-shock.</param>
/// <param name="EffectSlot">The Java effect id that decides replacement: 30184 (Courage/Awakening), 30182 (speed),
/// 154 (the Anti-Shock shields); 0 for an instant item.</param>
/// <param name="TierLevel">The tier the effect equals, for the tier rule (the item level, except for the veteran-reward
/// event scrolls: item level 30 with the Lesser tier's effect and no required level).</param>
public sealed record NaturalHelpItem(int ItemId, string Family, int ItemLevel, int SkillId, int UseDelayId,
	int UseDelayMillis, int DurationMillis, int EffectSlot, int? TierLevel = null)
{
	public int Tier => TierLevel ?? ItemLevel;
}

/// <summary>When the buff-ourself check runs.</summary>
public enum NaturalHelpTrigger { PrePull, AfterRest, AfterRevive, AfterRelog, TravelLeg, Combat }

/// <param name="Effects">The last visible-effect snapshot (null: not observed yet this world entry).</param>
/// <param name="EffectsAgeMillis">Game time since that snapshot; its remaining times are as of the snapshot.</param>
/// <param name="UseDelays">Use-delay group to the time it is ready again (the client timing contract).</param>
/// <param name="Casting">A cast is in progress: any item use would cancel it (Java CM_USE_ITEM).</param>
/// <param name="Disabled">Stunned or otherwise unable to act (Java PlayerRestrictions.canUseItem).</param>
/// <param name="PlannedTravelMeters">The planned length of the travel leg about to start, for the Running scroll.</param>
public sealed record NaturalHelpItemObservation(int Level, int Hp, int MaxHp, bool Dead, bool Casting, bool Cutscene,
	bool Flying, bool Disabled, IReadOnlyList<BotVisibleEffect>? Effects, long EffectsAgeMillis,
	IReadOnlyDictionary<int, long> ItemCounts, IReadOnlyDictionary<int, DateTimeOffset> UseDelays,
	float PlannedTravelMeters = 0, bool CrossMapTravel = false, int Dp = 0, bool SalvationLearned = false);

public sealed record NaturalHelpItemChoice(NaturalHelpItem? Item, string Reason, NaturalDecisionCheck[] Checks);

/// <summary>
/// NA-19: the buff-ourself check's help items. Pure: the caller supplies client-observed effects, inventory and
/// use-delay readiness; the policy names at most one scroll to use now, with a check per rule for the trace.
/// Awakening is kept up always (OD-15), never Courage (both fill effect slot 30184, so either replaces the other);
/// Running (never with another speed effect, slot 30182) only before a long leg; the Anti-Shock shield only in
/// combat at 50% HP, and the combat policy orders it. With nothing owned it chooses nothing.
/// </summary>
public static class NaturalHelpItemPolicy
{
	public const int AwakeningSlot = 30184, SpeedSlot = 30182, ShieldSlot = 154;
	/// <summary>Refresh a 5 min scroll when this little of it is left (the effect snapshot's time, aged).</summary>
	public const int RefreshWindowMillis = 20_000;
	public const float LongTravelMeters = 150;
	public const int ShieldHpPercent = 50;
	public const int SalvationDp = 2000;
	/// <summary>Anti-Shock tiers come from quests about ten levels early (Q2206 at 10 hands out the level 20 tier).</summary>
	public const int ShieldLevelAllowance = 10;

	public static readonly NaturalHelpItem[] All =
	[
		new(164000132, "awakening", 10, 9965, 34, 15_000, 300_000, AwakeningSlot),
		new(164000133, "awakening", 20, 9965, 34, 15_000, 300_000, AwakeningSlot),
		new(164000134, "awakening", 30, 9965, 34, 15_000, 300_000, AwakeningSlot),
		new(164000071, "courage", 10, 9959, 34, 15_000, 300_000, AwakeningSlot),
		new(164000072, "courage", 20, 9959, 34, 15_000, 300_000, AwakeningSlot),
		new(164000073, "courage", 30, 9959, 34, 15_000, 300_000, AwakeningSlot),
		new(164000074, "running", 10, 9960, 35, 15_000, 300_000, SpeedSlot),
		new(164000075, "running", 20, 9960, 35, 15_000, 300_000, SpeedSlot),
		new(164000076, "running", 30, 9960, 35, 15_000, 300_000, SpeedSlot),
		new(164000033, "movement-speed", 15, 9943, 35, 15_000, 600_000, SpeedSlot),
		new(164000067, "anti-shock", 20, 9953, 32, 60_000, 24_000, ShieldSlot),
		new(164000068, "anti-shock", 30, 9954, 32, 60_000, 24_000, ShieldSlot),
		new(164000069, "anti-shock", 40, 9955, 32, 60_000, 24_000, ShieldSlot),
		new(164000070, "anti-shock", 50, 9956, 32, 60_000, 24_000, ShieldSlot),
		new(164000131, "anti-shock", 60, 9964, 32, 60_000, 24_000, ShieldSlot),
		// NA-20a: veteran-reward event scrolls the natural character already owns (Java VeteranRewardService).
		new(164002118, "awakening", 30, 10467, 34, 1_000, 1_800_000, AwakeningSlot, TierLevel: 10),
		new(164002116, "running", 30, 10465, 35, 1_000, 1_800_000, SpeedSlot, TierLevel: 10),
		new(164002117, "courage", 30, 10466, 34, 1_000, 1_800_000, AwakeningSlot, TierLevel: 10),
		// NA-20a: DP for Salvation (Q2904 rewards five).
		new(160002273, "dp-jelly", 40, 10164, 23, 1_800_000, 0, 0),
	];

	/// <summary>
	/// CP-06: the scrolls of the level 1-9 manifest (docs/natural-class-profiles.md): the supplied Lesser Anti-Shock and
	/// Greater Running Scroll and the three event scrolls every starter owns. Below level 10 the policy picks among
	/// these and nothing else. The tier rule does not apply there: a consumable is gated by its restrict row, not by
	/// its item level, and none of the five has one. From level 10 on the tier rule decides, as before.
	/// </summary>
	public static readonly int[] StarterScrollIds = [164000067, 164000076, 164002116, 164002117, 164002118];

	/// <summary>Out of combat: the scroll to use now at this trigger, or none.</summary>
	public static NaturalHelpItemChoice DecideBuffs(NaturalHelpItemObservation state, DateTimeOffset now, NaturalHelpTrigger trigger)
	{
		var checks = new List<NaturalDecisionCheck>();
		if (Blocked(state, checks) is string blocked) return new(null, blocked, [.. checks]);
		if (trigger == NaturalHelpTrigger.Combat)
			return new(null, "In combat only the Anti-Shock shield is used, by the combat policy.", [.. checks]);

		// Awakening, always (OD-15).
		NaturalHelpItem? awakening = Best(state, "awakening", state.Level);
		BotVisibleEffect? slot = Active(state, AwakeningSlot);
		if (awakening == null) checks.Add(new("awakening", "skip", "No Awakening scroll at or below the character's level is owned."));
		else if (slot != null && Family(slot.SkillId) == "courage")
			checks.Add(new("awakening", "skip", "Courage fills the same slot; let it expire rather than waste a scroll swapping."));
		else if (slot != null && Remaining(state, slot) > RefreshWindowMillis)
			checks.Add(new("awakening", "pass", $"Awakening is active for {Remaining(state, slot) / 1000} s more."));
		else if (!Ready(state, awakening, now))
			checks.Add(new("awakening", "skip", $"Use-delay group {awakening.UseDelayId} is running."));
		else
		{
			checks.Add(new("awakening", "use", slot == null ? "Awakening is absent." : "Awakening is about to expire."));
			return new(awakening, $"Keep Awakening up (OD-15): {awakening.ItemId}, item level {awakening.ItemLevel}.", [.. checks]);
		}

		// Running, before a long leg only; never on top of another speed effect.
		bool longLeg = trigger == NaturalHelpTrigger.TravelLeg && (state.CrossMapTravel || state.PlannedTravelMeters >= LongTravelMeters);
		NaturalHelpItem? running = Best(state, "running", state.Level);
		BotVisibleEffect? speed = Active(state, SpeedSlot);
		if (!longLeg) checks.Add(new("running", "skip", $"Not a travel leg of {LongTravelMeters} m or to another map."));
		else if (running == null) checks.Add(new("running", "skip", "No Running scroll at or below the character's level is owned."));
		else if (speed != null && (Family(speed.SkillId) != "running" || Remaining(state, speed) > RefreshWindowMillis))
			checks.Add(new("running", "pass", "A speed effect is already active; speed effects replace each other."));
		else if (!Ready(state, running, now))
			checks.Add(new("running", "skip", $"Use-delay group {running.UseDelayId} is running."));
		else
		{
			checks.Add(new("running", "use", $"Travel leg of {state.PlannedTravelMeters:F0} m (cross-map: {state.CrossMapTravel})."));
			return new(running, $"Long journey: Running scroll {running.ItemId}.", [.. checks]);
		}

		// NA-20a: a Zeller Aether Jelly when Salvation is learned and observed DP cannot pay for it.
		NaturalHelpItem? jelly = Best(state, "dp-jelly", int.MaxValue);
		if (!state.SalvationLearned) checks.Add(new("dp-jelly", "skip", "Salvation is not learned."));
		else if (state.Dp >= SalvationDp) checks.Add(new("dp-jelly", "pass", $"Observed DP {state.Dp} pays for Salvation."));
		else if (jelly == null) checks.Add(new("dp-jelly", "skip", "No DP jelly is owned."));
		else if (!Ready(state, jelly, now)) checks.Add(new("dp-jelly", "skip", $"Use-delay group {jelly.UseDelayId} is running."));
		else
		{
			checks.Add(new("dp-jelly", "use", $"Observed DP {state.Dp} is below Salvation's {SalvationDp}."));
			return new(jelly, $"DP for Salvation: {jelly.ItemId}.", [.. checks]);
		}
		return new(null, "Nothing to apply.", [.. checks]);
	}

	/// <summary>In combat: the Anti-Shock tier to use now at 50% HP or below, or none. The combat policy orders it
	/// after the 30% retreat and before Salvation and the potion.</summary>
	public static NaturalHelpItemChoice DecideShield(NaturalHelpItemObservation state, DateTimeOffset now)
	{
		var checks = new List<NaturalDecisionCheck>();
		if (Blocked(state, checks) is string blocked) return new(null, blocked, [.. checks]);
		NaturalHelpItem? shield = Best(state, "anti-shock", state.Level + ShieldLevelAllowance);
		if (shield == null) return Skip("No Anti-Shock scroll at or below level + 10 is owned.");
		if (state.Hp * 100 > state.MaxHp * ShieldHpPercent) return Skip($"HP is above {ShieldHpPercent}%.");
		if (Active(state, ShieldSlot) != null) return Skip("A damage shield is active; a lower tier cannot replace a higher one.");
		if (!Ready(state, shield, now)) return Skip($"Use-delay group {shield.UseDelayId} is running.");
		checks.Add(new("anti-shock", "use", $"HP at or below {ShieldHpPercent}% with no shield."));
		return new(shield, $"Anti-Shock {shield.ItemId} (item level {shield.ItemLevel}) at {state.Hp * 100 / Math.Max(1, state.MaxHp)}% HP.", [.. checks]);

		NaturalHelpItemChoice Skip(string reason)
		{
			checks.Add(new("anti-shock", "skip", reason));
			return new(null, reason, [.. checks]);
		}
	}

	private static string? Blocked(NaturalHelpItemObservation state, List<NaturalDecisionCheck> checks)
	{
		string? reason = state.Dead ? "Dead." : state.Cutscene ? "A cutscene is playing." : state.Flying ? "In flight."
			: state.Casting ? "A cast is in progress; an item use would cancel it." : state.Disabled ? "Stunned or unable to act."
			: state.Effects == null ? "Effects are not observed yet." : null;
		checks.Add(new("can-use-items", reason == null ? "pass" : "skip", reason ?? "Alive, idle and effects observed."));
		return reason;
	}

	private static NaturalHelpItem? Best(NaturalHelpItemObservation state, string family, int maximumItemLevel) =>
		All.Where(item => item.Family == family && state.ItemCounts.GetValueOrDefault(item.ItemId) > 0 &&
				(state.Level <= NaturalHelpItemAllowlist.StarterMaxLevel ? StarterScrollIds.Contains(item.ItemId) : item.Tier <= maximumItemLevel))
			.OrderByDescending(item => item.Tier).ThenByDescending(item => item.DurationMillis).FirstOrDefault();

	private static BotVisibleEffect? Active(NaturalHelpItemObservation state, int effectSlot) =>
		state.Effects?.FirstOrDefault(effect => All.Any(item => item.SkillId == effect.SkillId && item.EffectSlot == effectSlot));

	private static string? Family(int skillId) => All.FirstOrDefault(item => item.SkillId == skillId)?.Family;

	/// <summary>Remaining time now; a permanent (-1) or unknown time counts as long.</summary>
	private static long Remaining(NaturalHelpItemObservation state, BotVisibleEffect effect) =>
		effect.RemainingMillis < 0 ? long.MaxValue : effect.RemainingMillis - state.EffectsAgeMillis;

	private static bool Ready(NaturalHelpItemObservation state, NaturalHelpItem item, DateTimeOffset now) =>
		!state.UseDelays.TryGetValue(item.UseDelayId, out DateTimeOffset readyAt) || readyAt <= now;
}
