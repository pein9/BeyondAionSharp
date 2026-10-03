namespace Aion.Bots.World;

/// <summary>Actual SM_INSTANCE_INFO row; remaining reuse time has Java's signed seconds semantics.</summary>
public sealed record BotInstanceEntry(int PlayerId, int CooldownId, int ReuseSeconds, int MaxEntries, int EntriesUsed, bool Shown);
