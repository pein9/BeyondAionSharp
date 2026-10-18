using System.Linq;
using Aion.Commons.Logging;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.TestKit;
using Microsoft.Extensions.Logging;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// A retail despawn rung and the destruction of its instance. <c>InstanceService.destroyInstance</c> takes the instance out of its
/// map before it deletes the npcs in it, so Beshmundir Temple's lich keys (216586, 216590), whose <c>on_despawn</c> places an add
/// 281697 at (979, 130, 245), spawned into no instance: <c>World.CreatePosition</c> threw and the spawner logged "Error during
/// spawn" at every destruction of the temple (scenario INSTANCES, P11-01).
/// </summary>
[Collection("GoldenDataManager")]
public sealed class DestroyedInstanceDespawnTests
{
	private const int BeshmundirTemple = 300170000;
	private const int LichKey = 216586;
	private const int LichKeyAdd = 281697;

	[Fact]
	public void ADespawnRungPlacesItsAddInALiveInstance()
	{
		using BossAiHarness harness = BossAiHarness.For(BeshmundirTemple).WithWorldSize(2048).Build();
		Npc key = harness.Spawn(LichKey, 975f, 135f, 245f);

		key.GetController().Delete();

		Assert.Single(harness.LiveNpcs(), npc => npc.GetNpcId() == LichKeyAdd);
	}

	[Fact]
	public void ADestroyedInstanceRunsNoDespawnRung()
	{
		using BossAiHarness harness = BossAiHarness.For(BeshmundirTemple).WithWorldSize(2048).Build();
		Npc key = harness.Spawn(LichKey, 975f, 135f, 245f);
		using var capture = new CapturingLoggerProvider();
		using var factory = LoggerFactory.Create(builder => builder.AddProvider(capture));

		using (AionLog.OverrideFactory(factory))
		{
			// InstanceService.destroyInstance: the instance leaves its map, then its npcs are deleted.
			harness.World.GetWorldMap(BeshmundirTemple).RemoveWorldMapInstance(key.GetInstanceId());
			key.GetController().Delete();
		}

		Assert.DoesNotContain(capture.Entries, entry => entry.Level >= LogLevel.Warning);
		Assert.False(key.IsSpawned());
	}
}
