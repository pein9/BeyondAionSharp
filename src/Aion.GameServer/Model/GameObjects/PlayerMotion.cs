using Aion.GameServer.Model.GameObjects.Players.Motion;

namespace Aion.GameServer.Model.GameObjects;

public sealed record PlayerMotion(int Id, int ExpireTimeSeconds, bool IsActive)
{
	// Java parity: model/gameobjects/player/motion/Motion.secondsUntilExpiration.
	public int SecondsUntilExpiration(DateTimeOffset now)
	{
		return ExpireTimeSeconds == 0 ? 0 : ExpireTimeSeconds - (int)now.ToUnixTimeSeconds();
	}

	public static int GetMotionType(int motionId) =>
        Aion.GameServer.Dataholders.DataManager.ITEM_DATA.GetMotionType(motionId)?.GetId() ?? 0;
}
