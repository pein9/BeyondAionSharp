using Aion.GameServer.Model;
using Aion.GameServer.Dataholders;

namespace Aion.GameServer.Model.GameObjects.Players.Motion;

/// <summary>Java parity: model/gameobjects/player/motion/Motion implements Expirable.</summary>
public class Motion : IExpirable
{
    private readonly int id;
    private readonly int deletionTime;
    private bool active = false;

    public Motion(int id, int deletionTime, bool isActive)
    {
        this.id = id;
        this.deletionTime = deletionTime;
        this.active = isActive;
    }

    /// <summary>the id</summary>
    public int GetId()
    {
        return id;
    }

    /// <summary>the active</summary>
    public bool IsActive()
    {
        return active;
    }

    // Java getType(); named explicitly to avoid hiding System.Object.GetType().
    public MotionType? GetMotionType() => DataManager.ITEM_DATA.GetMotionType(id);

    /// <param name="active">the active to set</param>
    public void SetActive(bool active)
    {
        this.active = active;
    }

    public int GetExpireTime()
    {
        return deletionTime;
    }

    public void OnExpire(Aion.GameServer.Model.GameObjects.Players.Player player)
    {
        player.GetMotions().Remove(id);
    }
}
