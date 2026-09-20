using Aion.Bots.Protocol;

namespace Aion.Bots.World;

public sealed partial class BotWorldModel
{
	public BotBindPoint? ObeliskBindPoint { get; private set; }
	public BotBindPoint? KiskBindPoint { get; private set; }
	/// <summary>Last observed update, not necessarily our Kisk. Consumers must match object/creator IDs
	/// and account for elapsed time. No optimistic countdown or unbounded history of nearby Kisks.</summary>
	public BotKiskUpdate? LastKiskUpdate { get; private set; }

	private void ApplyBindPoint(DecodedBotServerPacket packet)
	{
		var point = new BotBindPoint(packet.Get<int>("mapId"),
			new(packet.Get<float>("x"), packet.Get<float>("y"), packet.Get<float>("z"), 0), packet.Get<int>("kiskObjectId"));
		if (packet.Get<byte>("bindPointType") == 0) ObeliskBindPoint = point;
		else KiskBindPoint = point.KiskObjectId == 0 ? null : point;
	}
}

public sealed record BotBindPoint(int MapId, BotPosition Position, int KiskObjectId);
public sealed record BotKiskUpdate(int ObjectId, int CreatorId, int UseMask, int CurrentMembers, int MaxMembers,
	int RemainingResurrects, int MaxResurrects, int RemainingLifetimeSeconds);
