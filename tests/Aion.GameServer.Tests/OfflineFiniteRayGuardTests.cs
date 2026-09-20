using Aion.Bots.Navigation;
using Aion.GameServer.GeoEngine.Bounding;
using Aion.GameServer.GeoEngine.Collision;
using Aion.GameServer.GeoEngine.Math;
using Aion.GameServer.GeoEngine.Scene;

namespace Aion.GameServer.Tests;

public sealed class OfflineFiniteRayGuardTests
{
    [Fact]
    public void DistantDynamicStateIsNotQueriedButNearbyAndUnboundedQueriesStillFailClosed()
    {
        var root = new Node("offline");
        root.SetCollisionIntentions(CollisionIntention.ALL.GetId());
        root.AttachChild(new MissingStateNode());
        root.UpdateModelBound();
        OfflineFiniteRayGuard.Apply(root);
        OfflineFiniteRayGuard.Apply(root); // Idempotent; no wrapper accumulation.
        Assert.Single(root.GetChildren());
        var results = new CollisionResults(CollisionIntention.PHYSICAL.GetId(), 1, IgnoreProperties.ASMODIANS);
        var ray = new Ray(new Vector3f(0, 0, 0), new Vector3f(1, 0, 0));
        ray.SetLimit(2);
        Assert.Equal(0, root.CollideWith(ray, results));
        ray.SetLimit(99);
        Assert.Throws<InvalidOperationException>(() => root.CollideWith(ray, results));
        ray.SetLimit(float.PositiveInfinity);
        Assert.Throws<InvalidOperationException>(() => root.CollideWith(ray, results));
        ray = new Ray(new Vector3f(100, 0, 0), new Vector3f(1, 0, 0));
        ray.SetLimit(.1f);
        Assert.Throws<InvalidOperationException>(() => root.CollideWith(ray, results));
    }

    private sealed class MissingStateNode : DespawnableNode
    {
        public MissingStateNode()
        {
            worldBound = new BoundingBox(new Vector3f(100, 0, 0), 1, 1, 1);
            SetCollisionIntentions(CollisionIntention.PHYSICAL.GetId());
        }
        public override void UpdateModelBound() { }
        public override int CollideWith(Collidable other, CollisionResults results) =>
            throw new InvalidOperationException("Dynamic state is not hosted by the offline client.");
    }
}
