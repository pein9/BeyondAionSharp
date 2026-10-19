using Aion.GameServer.Configs.Main;
using Aion.GameServer.Model.Geometry;

namespace Aion.GameServer.World.Zone;

/// <summary>
/// Axis-aligned region square used to test which zones intersect a map region.
/// Java parity: world/zone/RegionZone.
/// </summary>
public class RegionZone : RectangleArea
{
    // Java parity: spans one WORLD_REGION_SIZE square within the supplied height band.
    public RegionZone(float startX, float startY, float minZ, float maxZ)
        : base(startX, startY, startX + WorldConfig.WorldRegionSize, startY + WorldConfig.WorldRegionSize, minZ, maxZ)
    {
    }

    // Java parity: isInside(AbstractArea) — always true (region squares are containers).
    public bool IsInside(AbstractArea area) => true;
}
