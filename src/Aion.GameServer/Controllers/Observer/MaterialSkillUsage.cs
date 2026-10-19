using Aion.GameServer.Utils;

namespace Aion.GameServer.Controllers.Observer;

/// <summary>Per-creature material skill periods survive leaving and re-entering the same material.</summary>
public class MaterialSkillUsage
{
    private readonly Dictionary<int, long> lastUseMillisBySlot = new();
    private int lastMaterialId;

    public bool TryUse(int materialId, int slot, int periodSeconds)
    {
        lock (this)
        {
            long now = SystemClock.CurrentMillis();
            if (lastMaterialId != materialId)
            {
                lastUseMillisBySlot.Clear();
                lastMaterialId = materialId;
            }
            else if (lastUseMillisBySlot.TryGetValue(slot, out long lastUseMillis) && now < unchecked(lastUseMillis + periodSeconds * 1000L))
                return false;
            lastUseMillisBySlot[slot] = now;
            return true;
        }
    }
}
