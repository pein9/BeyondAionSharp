using Aion.Bots.Protocol;

namespace Aion.Bots.World;

/// <summary>NR-110a: the bot's own spirit as SM_SUMMON_PANEL told it.</summary>
public sealed record BotOwnSummon(int ObjectId, int Level, int CurrentHp, int MaxHp);

public sealed partial class BotWorldModel
{
	/// <summary>
	/// NR-110a: the spirit the bot has out; null for none. Java SummonsService.createSummon 29-40 tells the master of a
	/// new spirit with SM_SUMMON_PANEL, and its ReleaseSummonTask tells it with SM_SUMMON_PANEL_REMOVE and
	/// SM_SUMMON_OWNER_REMOVE of every end: the spirit's death, the master's death, a release, a master that went out
	/// of the spirit's sight, a timer. A new login starts with none: PlayerLeaveWorldService 117 releases it. Which
	/// spirit it is, is the template of the object with this id.
	/// </summary>
	public BotOwnSummon? Summon { get; private set; }

	private void ApplySummonPanel(DecodedBotServerPacket packet) => Summon = new(packet.Get<int>("objectId"),
		packet.Get<ushort>("level"), packet.Get<int>("currentHp"), packet.Get<int>("maxHp"));

	private void ApplySummonOwnerRemove(DecodedBotServerPacket packet)
	{
		if (Summon?.ObjectId == packet.Get<int>("summonObjId")) Summon = null;
	}
}
