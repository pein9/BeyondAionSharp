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

	/// <summary>
	/// NR-110c: true from the bot's order to attack until its order to guard. It is the bot's own word, sent by
	/// CM_SUMMON_COMMAND; the server keeps the mode and moves nothing by it. While it is true a walk of the bot leaves
	/// the spirit to its fight (BotMover.WeaveSpirit). A new spirit and a spirit that is gone have no order.
	/// </summary>
	public bool SummonFights { get; set; }

	/// <summary>NR-110c: how fast the spirit runs, by the client's own data of its kind; the bot's own walks drive it
	/// by this. No packet says it of a spirit that stands. Null until the journey has looked it up.</summary>
	public float? SummonSpeed { get; set; }

	private void ApplySummonPanel(DecodedBotServerPacket packet)
	{
		int objectId = packet.Get<int>("objectId");
		if (Summon?.ObjectId != objectId) (SummonFights, SummonSpeed) = (false, null);
		Summon = new(objectId, packet.Get<ushort>("level"), packet.Get<int>("currentHp"), packet.Get<int>("maxHp"));
	}

	private void ApplySummonOwnerRemove(DecodedBotServerPacket packet)
	{
		if (Summon?.ObjectId != packet.Get<int>("summonObjId")) return;
		Summon = null;
		(SummonFights, SummonSpeed) = (false, null);
	}
}
