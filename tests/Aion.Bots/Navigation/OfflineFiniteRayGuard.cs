using Aion.GameServer.GeoEngine.Bounding;
using Aion.GameServer.GeoEngine.Collision;
using Aion.GameServer.GeoEngine.Math;
using Aion.GameServer.GeoEngine.Scene;

namespace Aion.Bots.Navigation;

/// <summary>Offline-only broad phase. Java's node bounds test an infinite ray before consulting
/// dynamic services. A short bot segment must not ask for server state at a distant shield.
/// Nearby dynamic nodes still use their original collision path and fail if state is unavailable.</summary>
public static class OfflineFiniteRayGuard
{
    public static void Apply(Node root)
    {
        for (int i = 0; i < root.GetChildren().Count; i++)
        {
            var child = root.GetChildren()[i];
            if (child is Guard) continue;
            if (child is Node node) Apply(node);
            if (child is not DespawnableNode dynamicNode) continue;
            root.DetachChildAt(i);
            root.AttachChildAt(new Guard(dynamicNode), i);
        }
    }

    private sealed class Guard : Node
    {
        private readonly DespawnableNode original;
        public Guard(DespawnableNode original) : base(original.GetName())
        {
            this.original = original;
            SetCollisionIntentions(original.GetCollisionIntentions());
            worldBound = original.GetWorldBound()?.Clone(null);
            AttachChild(original);
        }

        public override int CollideWith(Collidable other, CollisionResults results)
        {
            if (other is Ray ray && original.GetWorldBound() is BoundingBox box &&
                float.IsFinite(ray.limit) && ray.limit >= 0)
            {
                var center = box.GetCenter();
                if (Outside(ray.origin.X, ray.direction.X, center.X, box.GetXExtent()) ||
                    Outside(ray.origin.Y, ray.direction.Y, center.Y, box.GetYExtent()) ||
                    Outside(ray.origin.Z, ray.direction.Z, center.Z, box.GetZExtent())) return 0;

                bool Outside(float start, float direction, float middle, float extent)
                {
                    double end = start + (double)direction * ray.limit;
                    // Conservative padding keeps boundary/rounding cases on the original path.
                    return Math.Max(start, end) < (double)middle - extent - .01 ||
                        Math.Min(start, end) > (double)middle + extent + .01;
                }
            }
            return original.CollideWith(other, results);
        }
    }
}
