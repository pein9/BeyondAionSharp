using Aion.Bots.Protocol;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.Bots.Timing;

/// <summary>Stateful client-side timing gates tied to Java commit ce54b7931.</summary>
public sealed class BotTimingContract
{
	public const string JavaSpecCommit = "ce54b7931546cddafb970d20c9f71fec6d48c83b";
	public const int AttackGraceMillis = 300;
	public const int MinimumCastIntervalMillis = 350;
	public const int ConfiguredReentrySeconds = 10;
	public const int JavaDefaultReentrySeconds = 20;
	public const int MaximumCrashLeaveDelaySeconds = 10;
	public static readonly BotTimingRuleSet Rules = new(JavaSpecCommit, AttackGraceMillis, MinimumCastIntervalMillis,
		ConfiguredReentrySeconds, JavaDefaultReentrySeconds, MaximumCrashLeaveDelaySeconds,
		new HashSet<BotBlockingActivity>
		{
			BotBlockingActivity.Casting,
			BotBlockingActivity.Gathering,
			BotBlockingActivity.Crafting,
		});

	private readonly TimeProvider timeProvider;
	private readonly TimeSpan reentryDelay;
	private readonly TimeSpan crashLeaveDelay;
	private readonly Dictionary<int, DateTimeOffset> skillCooldowns = [];
	private readonly Dictionary<int, DateTimeOffset> itemCooldowns = [];
	private readonly HashSet<BotBlockingActivity> blockingActivities = [];
	private DateTimeOffset nextAttackAt;
	private DateTimeOffset nextCastAt;
	private DateTimeOffset nextEnterWorldAt;

	public BotTimingContract(TimeProvider? timeProvider = null, TimeSpan? reentryDelay = null,
		TimeSpan? crashLeaveDelay = null)
	{
		this.timeProvider = timeProvider ?? TimeProvider.System;
		this.reentryDelay = reentryDelay ?? TimeSpan.FromSeconds(ConfiguredReentrySeconds);
		this.crashLeaveDelay = crashLeaveDelay ?? TimeSpan.FromSeconds(MaximumCrashLeaveDelaySeconds);
	}

	public int? SelectedTargetId { get; private set; }

	public IReadOnlySet<BotBlockingActivity> BlockingActivities => blockingActivities;

	public void RecordTargetSelection(int targetObjectId) => SelectedTargetId = targetObjectId;

	public TimeSpan TimeUntilAttack(int attackSpeedMillis) => Remaining(nextAttackAt);

	public void RecordAttack(int attackSpeedMillis)
	{
		EnsureReady(TimeUntilAttack(attackSpeedMillis), "attack");
		var minimumInterval = Math.Max(0, attackSpeedMillis - AttackGraceMillis);
		nextAttackAt = timeProvider.GetUtcNow() + TimeSpan.FromMilliseconds(minimumInterval);
	}

	public TimeSpan TimeUntilCast(int skillId)
	{
		var due = nextCastAt;
		if (skillCooldowns.TryGetValue(skillId, out var cooldown) && cooldown > due)
			due = cooldown;
		return Remaining(due);
	}

	public void RecordCastStarted(int skillId, int? targetObjectId)
	{
		if (targetObjectId != null && SelectedTargetId != targetObjectId)
			throw new InvalidOperationException(
				$"CM_TARGET_SELECT for object {targetObjectId} must precede CM_CASTSPELL; current target is {SelectedTargetId?.ToString() ?? "none"}.");
		if (blockingActivities.Contains(BotBlockingActivity.Casting))
			throw new InvalidOperationException("Cannot cast another skill before SM_CASTSPELL_RESULT or cancellation.");
		EnsureReady(TimeUntilCast(skillId), $"cast skill {skillId}");
		nextCastAt = timeProvider.GetUtcNow() + TimeSpan.FromMilliseconds(MinimumCastIntervalMillis);
		blockingActivities.Add(BotBlockingActivity.Casting);
	}

	public void RecordCastResult(int animationLastHitMillis)
	{
		var animationDue = timeProvider.GetUtcNow() + TimeSpan.FromMilliseconds(Math.Max(0, animationLastHitMillis));
		if (animationDue > nextCastAt)
			nextCastAt = animationDue;
		blockingActivities.Remove(BotBlockingActivity.Casting);
	}

	public void RecordCastCancelled() => blockingActivities.Remove(BotBlockingActivity.Casting);

	public void ApplySkillCooldowns(DecodedBotServerPacket packet)
	{
		if (packet.PacketType != typeof(SM_SKILL_COOLDOWN))
			throw new ArgumentException("Expected a decoded SM_SKILL_COOLDOWN packet.", nameof(packet));
		var now = timeProvider.GetUtcNow();
		foreach (var entry in packet.Get<List<IReadOnlyDictionary<string, object?>>>("cooldowns"))
		{
			var skillId = Get<ushort>(entry, "skillId");
			var remaining = Get<int>(entry, "remainingSeconds");
			if (remaining <= 0)
				skillCooldowns.Remove(skillId);
			else
				skillCooldowns[skillId] = now + TimeSpan.FromSeconds(remaining);
		}
	}

	public TimeSpan TimeUntilItemUse(ItemTemplate item)
	{
		var limits = item.GetUseLimits();
		return limits != null && itemCooldowns.TryGetValue(limits.GetDelayId(), out var due) ? Remaining(due) : TimeSpan.Zero;
	}

	public void RecordItemUse(ItemTemplate item)
	{
		EnsureReady(TimeUntilItemUse(item), $"use item {item.GetTemplateId()}");
		var limits = item.GetUseLimits();
		if (limits != null && limits.GetDelayTime() > 0)
			itemCooldowns[limits.GetDelayId()] = timeProvider.GetUtcNow() + TimeSpan.FromMilliseconds(limits.GetDelayTime());
	}

	public void SetActivity(BotBlockingActivity activity, bool active)
	{
		if (active)
			blockingActivities.Add(activity);
		else
			blockingActivities.Remove(activity);
	}

	public void EnsureCanMove()
	{
		if (blockingActivities.Count != 0)
			throw new InvalidOperationException(
				$"CM_MOVE is blocked while {string.Join(", ", blockingActivities.OrderBy(activity => activity))}.");
	}

	public void RecordLeftWorld(bool crashed)
	{
		nextEnterWorldAt = timeProvider.GetUtcNow() + reentryDelay + (crashed ? crashLeaveDelay : TimeSpan.Zero);
	}

	public TimeSpan TimeUntilEnterWorld() => Remaining(nextEnterWorldAt);

	public void EnsureCanEnterWorld() => EnsureReady(TimeUntilEnterWorld(), "enter world");

	private TimeSpan Remaining(DateTimeOffset due)
	{
		var remaining = due - timeProvider.GetUtcNow();
		return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
	}

	private static void EnsureReady(TimeSpan remaining, string action)
	{
		if (remaining > TimeSpan.Zero)
			throw new InvalidOperationException($"Cannot {action} for another {remaining.TotalMilliseconds:0} ms.");
	}

	private static T Get<T>(IReadOnlyDictionary<string, object?> fields, string name) =>
		fields.TryGetValue(name, out var value) && value is T typed
			? typed
			: throw new InvalidDataException($"Decoded packet field '{name}' was missing or was not {typeof(T).Name}.");
}

public enum BotBlockingActivity
{
	Casting,
	Gathering,
	Crafting,
}

public sealed record BotTimingRuleSet(string JavaCommit, int AttackGraceMillis, int MinimumCastIntervalMillis,
	int ConfiguredReentrySeconds, int JavaDefaultReentrySeconds, int MaximumCrashLeaveDelaySeconds,
	IReadOnlySet<BotBlockingActivity> MovementBlockingActivities);
