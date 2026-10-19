using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.GameObjects.Players.Motion;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: explicit packet factories for learned, activated, removed and visible player motions.</summary>
public class SM_MOTION : AionServerPacket
{
    private enum Action { LIST = 1, ADD = 2, SET = 5, REMOVE = 6, PLAYER_MOTIONS = 7 }
    private readonly Action action;
    private readonly int motionId, remainingTime, playerId;
    private readonly MotionType? type;
    private readonly IDictionary<MotionType, Motion>? activeMotions;
    private readonly ICollection<Motion>? motions;

    private SM_MOTION(Action action, int motionId, int remainingTime, MotionType? type, int playerId,
        IDictionary<MotionType, Motion>? activeMotions, ICollection<Motion>? motions)
    {
        this.action = action; this.motionId = motionId; this.remainingTime = remainingTime; this.type = type;
        this.playerId = playerId; this.activeMotions = activeMotions; this.motions = motions;
    }

    public static SM_MOTION List(ICollection<Motion> motions) => new(Action.LIST, 0, 0, null, 0, null, motions);
    public static SM_MOTION Add(Motion motion) => new(Action.ADD, motion.GetId(), motion.SecondsUntilExpiration(), null, 0, null, null);
    public static SM_MOTION Set(int motionId, MotionType type) => new(Action.SET, motionId, 0, type, 0, null, null);
    public static SM_MOTION Remove(int motionId) => new(Action.REMOVE, motionId, 0, null, 0, null, null);
    public static SM_MOTION PlayerMotions(Player player) => new(Action.PLAYER_MOTIONS, 0, 0, null, player.GetObjectId(), player.GetMotions().GetActiveMotions(), null);

    protected override void WriteImpl(AionConnection con)
    {
        WriteC((int)action);
        switch (action)
        {
            case Action.LIST:
                WriteH(motions!.Count);
                foreach (Motion motion in motions)
                {
                    WriteH(motion.GetId()); WriteD(motion.SecondsUntilExpiration()); WriteC(motion.IsActive() ? 1 : 0);
                }
                break;
            case Action.ADD:
                WriteH(motionId); WriteD(remainingTime);
                break;
            case Action.SET:
                WriteH(motionId); WriteC(type!.Value.GetId());
                break;
            case Action.REMOVE:
                WriteH(motionId);
                break;
            case Action.PLAYER_MOTIONS:
                WriteD(playerId);
                foreach (MotionType slot in System.Enum.GetValues<MotionType>())
                    WriteH(activeMotions!.TryGetValue(slot, out Motion? motion) ? motion.GetId() : 0);
                break;
        }
    }
}
