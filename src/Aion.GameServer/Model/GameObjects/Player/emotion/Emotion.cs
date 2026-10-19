using Aion.GameServer.Model;

namespace Aion.GameServer.Model.GameObjects.Players.Emotion;

/// <summary>Java parity: model/gameobjects/player/emotion/Emotion implements Expirable.</summary>
public class Emotion : IExpirable
{
    private readonly int id;
    private readonly int expireTime;

    public Emotion(int id, int expireTime)
    {
        this.id = id;
        this.expireTime = expireTime;
    }

    /// <summary>the id</summary>
    public int GetId()
    {
        return id;
    }

    public int GetExpireTime()
    {
        return expireTime;
    }

    public void OnExpire(Aion.GameServer.Model.GameObjects.Players.Player player)
    {
        player.GetEmotions().Remove(id);
    }
}
