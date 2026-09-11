namespace Aion.GameServer.Dataholders;

public sealed class PlayerExperienceTable
{
	public PlayerExperienceTable(IReadOnlyList<long> experience)
	{
		Experience = experience;
	}

	public IReadOnlyList<long> Experience { get; }

	public int MaxLevel => Experience.Count;

	// Java parity: dataholders/PlayerExperienceTable.getMaxLevel().
	public int GetMaxLevel() => MaxLevel;

	public long GetStartExpForLevel(int level)
	{
		if (level > MaxLevel)
			throw new ArgumentException("The given level is higher than possible max");

		return level == 0 ? 0 : Experience[level - 1];
	}

	public int GetLevelForExp(long expValue)
	{
		for (var i = Experience.Count; i > 0; i--)
		{
			if (expValue >= Experience[i - 1])
			{
				return MaxLevel <= i ? MaxLevel - 1 : i;
			}
		}

		return 0;
	}
}
