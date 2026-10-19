using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects.Players.Emotion;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: explicit emotion actions, unsigned-short IDs and signed-int remaining seconds.</summary>
public class SM_EMOTION_LIST : AionServerPacket
{
    public enum Action { LIST = 0, ADD = 1, REMOVE = 2 }
    private readonly Action action;
    private readonly ICollection<Emotion> emotions;

    public SM_EMOTION_LIST(Action action, ICollection<Emotion>? emotions)
    {
        this.action = action;
        this.emotions = emotions ?? System.Array.Empty<Emotion>();
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteC((int)action); WriteH(emotions.Count);
        foreach (Emotion emotion in emotions)
        {
            WriteH(emotion.GetId()); WriteD(action == Action.REMOVE ? 0 : emotion.SecondsUntilExpiration());
        }
    }
}
