using Aion.Bots.Scenarios;
using Aion.Bots.World;

namespace Aion.GameServer.Tests;

public sealed class SoakGatheringReturnTests
{
	[Fact]
	public void OneWayCheckedPathIsRejectedBeforeTravel()
	{
		var home = new BotPosition(0, 0, 0, 0);
		var target = new BotPosition(10, 10, 1, 0);
		Assert.Empty(SoakGatheringRoute.FindReturnablePath(home, target, home,
			(start, end) => start == home && end == target ? [target] : []));
	}

	[Fact]
	public void ReturnProbeUsesGroundNormalizedEndpointAndOriginalRendezvous()
	{
		var home = new BotPosition(0, 0, 0, 0);
		var start = new BotPosition(5, 0, 0, 0); // An exploration leg can begin away from home.
		var target = new BotPosition(10, 0, 1, 0);
		var grounded = target with { Z = 0.98f };
		var calls = new List<(BotPosition, BotPosition)>();
		var path = SoakGatheringRoute.FindReturnablePath(start, target, home, (from, to) =>
		{
			calls.Add((from, to));
			return calls.Count == 1 ? [grounded] : from == grounded && to == home ? [home] : [];
		});
		Assert.Equal(new[] { grounded }, path);
		Assert.Equal(new[] { (start, target), (grounded, home) }, calls);
	}

	[Fact]
	public void MissingOutboundDoesNotAttemptReturnOrInventMovement()
	{
		var home = new BotPosition(0, 0, 0, 0);
		int calls = 0;
		Assert.Empty(SoakGatheringRoute.FindReturnablePath(home, home with { X = 2 }, home,
			(_, _) => { calls++; return []; }));
		Assert.Equal(1, calls);
	}
}
