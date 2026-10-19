using System.Collections.Generic;

namespace Aion.GameServer.Model.GameObjects.Players.Motion;

/// <summary>Java parity: model/gameobjects/player/motion/MotionList.</summary>
public class MotionList
{
    private readonly Aion.GameServer.Model.GameObjects.Players.Player owner;
    // Java EnumMap iteration follows enum declaration order.
    private SortedDictionary<MotionType, Motion> activeMotions;
    private Dictionary<int, Motion> motions;

    public MotionList(Aion.GameServer.Model.GameObjects.Players.Player owner)
    {
        this.owner = owner;
    }

    /// <summary>the activeMotions</summary>
    public IDictionary<MotionType, Motion> GetActiveMotions()
    {
        if (activeMotions == null)
            return new SortedDictionary<MotionType, Motion>();
        return activeMotions;
    }

    /// <summary>the motions</summary>
    public IDictionary<int, Motion> GetMotions()
    {
        if (motions == null)
            return new Dictionary<int, Motion>();
        return motions;
    }

    public void Add(Motion motion, bool persist)
    {
        if (motions == null)
            motions = new Dictionary<int, Motion>();
        if (motions.ContainsKey(motion.GetId()) && motion.GetExpireTime() == 0)
        {
            Remove(motion.GetId());
        }
        motions[motion.GetId()] = motion;
        if (motion.IsActive() && motion.GetMotionType() is { } type)
        {
            if (activeMotions == null)
                activeMotions = new SortedDictionary<MotionType, Motion>();
            activeMotions.TryGetValue(type, out Motion old);
            activeMotions[type] = motion;
            if (old != null)
            {
                old.SetActive(false);
                Aion.GameServer.Dao.MotionDAO.UpdateMotion(owner.GetObjectId(), old);
            }
        }
        if (persist)
        {
            Aion.GameServer.Taskmanager.Tasks.ExpireTimerTask.GetInstance().RegisterExpirable(motion, owner);
            Aion.GameServer.Dao.MotionDAO.StoreMotion(owner.GetObjectId(), motion);
        }
    }

    public bool Remove(int motionId)
    {
        if (!motions.TryGetValue(motionId, out Motion motion))
            motion = null;
        else
            motions.Remove(motionId);
        if (motion != null)
        {
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(owner, Aion.GameServer.Network.Aion.ServerPackets.SM_MOTION.Remove(motionId));
            Aion.GameServer.Dao.MotionDAO.DeleteMotion(owner.GetObjectId(), motionId);
            if (motion.IsActive())
            {
                if (activeMotions != null && motion.GetMotionType() is { } type &&
                    activeMotions.TryGetValue(type, out var current) && ReferenceEquals(current, motion))
                    activeMotions.Remove(type);
                return true;
            }
        }
        return false;
    }

    public void SetActive(int motionId, MotionType type)
    {
        if (motionId != 0)
        {
            if (!GetMotions().TryGetValue(motionId, out Motion motion))
                motion = null;
            if (motion == null || motion.GetMotionType() == null)
                return;
            if (activeMotions == null)
                activeMotions = new SortedDictionary<MotionType, Motion>();
            MotionType ownType = motion.GetMotionType()!.Value;
            activeMotions.TryGetValue(ownType, out Motion old);
            activeMotions[ownType] = motion;
            if (old != null && !ReferenceEquals(old, motion))
            {
                old.SetActive(false);
                Aion.GameServer.Dao.MotionDAO.UpdateMotion(owner.GetObjectId(), old);
            }
            motion.SetActive(true);
            Aion.GameServer.Dao.MotionDAO.UpdateMotion(owner.GetObjectId(), motion);
        }
        else if (activeMotions != null)
        {
            if (activeMotions.Remove(type, out Motion old))
            {
                old.SetActive(false);
                Aion.GameServer.Dao.MotionDAO.UpdateMotion(owner.GetObjectId(), old);
            }
        }
        Aion.GameServer.Utils.PacketSendUtility.SendPacket(owner, Aion.GameServer.Network.Aion.ServerPackets.SM_MOTION.Set(motionId, type));
        Aion.GameServer.Utils.PacketSendUtility.BroadcastPacket(owner, Aion.GameServer.Network.Aion.ServerPackets.SM_MOTION.PlayerMotions(owner), true);
    }
}
