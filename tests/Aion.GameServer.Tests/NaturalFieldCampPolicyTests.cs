using Aion.Bots.Navigation;
using Aion.Bots.World;

namespace Aion.GameServer.Tests;

public sealed class NaturalFieldCampPolicyTests
{
	[Fact]
	public void Q2005WalkedCampRemainsOutsideObservedScratcherAggro()
	{
		var camp = At(723.976f, 1484.345f);
		BotNavigationHazard[] observed =
		[
			new(At(735.67f, 1493.18f), 7),
			new(At(734.08f, 1510.96f), 6),
			new(At(746.50f, 1511.30f), 15),
		];
		Assert.True(NaturalFieldCampPolicy.CanRest(camp, observed));
	}

	[Fact]
	public void AggroCirclePlusMarginPreventsUnsafeRest()
	{
		Assert.False(NaturalFieldCampPolicy.CanRest(At(0, 0),
			[new BotNavigationHazard(At(10, 0), 7)]));
	}

	private static BotPosition At(float x, float y) => new(x, y, 285, 0);
}
