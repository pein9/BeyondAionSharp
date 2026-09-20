using System.Xml.Linq;
using Aion.Bots.World;

namespace Aion.Bots.Scenarios;

public readonly record struct SoakGatheringSpot(int MapId, int InstanceId, int TemplateId, float X, float Y, float Z)
{
	public BotPosition Position => new(X, Y, Z, 0);
}

/// <summary>One owner per physical node; state is bounded by shipped spots, not respawn object ids.</summary>
public sealed class SoakGatheringPool(IEnumerable<SoakGatheringSpot> spots)
{
	private readonly object gate = new();
	private readonly Dictionary<SoakGatheringSpot, Node> nodes = spots.ToDictionary(spot => spot, _ => new Node());
	public int Count => nodes.Count;

	public Lease? TryAcquire(SoakGatheringSpot spot, int objectId, TimeSpan elapsed)
	{
		if (objectId <= 0 || elapsed < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(objectId));
		lock (gate)
		{
			var node = nodes[spot];
			if (node.Owned || elapsed < node.AvailableAt) return null;
			if (node.Uses == GatheringTarget.HarvestCount) { node.Uses = 0; node.ObjectId = null; }
			if (node.ObjectId is { } previous && previous != objectId)
				throw new InvalidDataException("Gatherable changed identity before its expected depletion.");
			node.ObjectId = objectId;
			node.Owned = true;
			return new Lease(this, node, spot, objectId, node.Uses + 1);
		}
	}

	public static IReadOnlyList<SoakGatheringSpot> StarterSpots(int instanceId = 1)
	{
		if (instanceId <= 0) throw new ArgumentOutOfRangeException(nameof(instanceId));
		string root = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ScenarioManifest.FindDefaultPath())!, "../.."));
		var templates = XElement.Load(Path.Combine(root, "game-server/data/static_data/gatherables/gatherable_templates.xml"));
		var result = new List<SoakGatheringSpot>();
		foreach (var (file, target) in new[] { ("210010000_Poeta.xml", GatheringTarget.YoungAria), ("220010000_Ishalgen.xml", GatheringTarget.YoungAzpha) })
		{
			var template = templates.Elements("gatherable_template").Single(value => (int?)value.Attribute("id") == target.TemplateId);
			var material = template.Element("materials")!.Elements("material").Single();
			if ((int?)template.Attribute("harvestCount") != GatheringTarget.HarvestCount ||
				(int?)template.Attribute("skillLevel") != 1 || (int?)template.Attribute("harvestSkill") != 30001 ||
				(int?)material.Attribute("itemid") != target.ItemId || (int?)material.Attribute("rate") != 10000000 ||
				((int?)template.Attribute("eraseValue") ?? 0) != 0)
				throw new InvalidDataException("Starter gathering material/skill contract changed.");
			var map = XElement.Load(Path.Combine(root, "game-server/data/static_data/spawns/Gather", file)).Element("spawn_map")!;
			var spawn = map.Elements("spawn").Single(value => (int?)value.Attribute("npc_id") == target.TemplateId);
			if ((int?)map.Attribute("map_id") != target.MapId || (int?)spawn.Attribute("respawn_time") != GatheringTarget.RespawnDelay.TotalSeconds)
				throw new InvalidDataException("Starter gathering spawn contract changed.");
			result.AddRange(spawn.Elements("spot").Select(value => new SoakGatheringSpot(target.MapId, instanceId, target.TemplateId,
				(float)value.Attribute("x")!, (float)value.Attribute("y")!, (float)value.Attribute("z")!)));
		}
		if (result.Count == 0 || result.Distinct().Count() != result.Count) throw new InvalidDataException("Empty or duplicate starter gathering spots.");
		return result.AsReadOnly();
	}

	internal sealed class Node
	{
		public bool Owned;
		public int Uses;
		public int? ObjectId;
		public TimeSpan AvailableAt;
	}

	public sealed class Lease : IDisposable
	{
		private readonly SoakGatheringPool pool;
		private readonly Node node;
		private bool disposed, completed;
		internal Lease(SoakGatheringPool pool, Node node, SoakGatheringSpot spot, int objectId, int useNumber)
		{ this.pool = pool; this.node = node; Spot = spot; ObjectId = objectId; UseNumber = useNumber; }
		public SoakGatheringSpot Spot { get; }
		public int ObjectId { get; }
		public int UseNumber { get; }
		public bool Depletes => UseNumber == GatheringTarget.HarvestCount;

		// Java completeInteraction consumes a use on success, failure and abort. The soak treats aborts as failures.
		public void Complete(TimeSpan observedAt)
		{
			if (observedAt < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(observedAt));
			lock (pool.gate)
			{
				if (disposed || completed) throw new InvalidOperationException("Gathering lease completed twice or after release.");
				completed = true;
				node.Uses++;
				if (Depletes) node.AvailableAt = observedAt + GatheringTarget.RespawnDelay;
			}
		}

		public void Dispose()
		{
			lock (pool.gate)
			{
				if (disposed) return;
				disposed = true;
				node.Owned = false;
			}
		}
	}
}
