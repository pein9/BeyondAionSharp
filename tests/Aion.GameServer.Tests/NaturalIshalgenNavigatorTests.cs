using Aion.Bots.Navigation;
using Aion.Bots.World;

namespace Aion.GameServer.Tests;

public sealed class NaturalIshalgenNavigatorTests
{
	[Fact]
	public async Task ExploringSpawnHintDoesNotInventGatherableObject()
	{
		var driver = new FakeDriver([]);
		NaturalNavigationResult result = await NaturalIshalgenNavigator.ExploreAnchorAsync(220010000, 400651,
			At(8), driver, "gatherable");
		Assert.True(result.Arrived);
		Assert.Null(result.TargetObjectId);
		Assert.Contains(driver.Events, item => item.Action == "anchor-observed" && item.Outcome == "completed");
	}

	[Fact]
	public async Task SegmentsCheckedRouteAndStopsAtObservedNpcWithoutInteracting()
	{
		var driver = new FakeDriver([new(77, 203500, At(20))]);
		NaturalNavigationResult result = await NaturalIshalgenNavigator.ApproachNpcAsync(220010000, 203500,
			At(20), driver);
		Assert.True(result.Arrived);
		Assert.Equal(77, result.TargetObjectId);
		Assert.Equal(3, result.Segments);
		Assert.All(driver.MovedSegments, segment => Assert.InRange(segment.Count, 1, 8));
		Assert.Equal(3, driver.Synchronizations);
		Assert.Equal(20, driver.Events.Single(item => item.Action == "route-to-observed-npc").PlannedRoute?.Length);
		Assert.Equal(220010000, driver.Events[0].MapId);
		Assert.Contains(driver.Events, item => item.Action == "navigation-arrived" && item.Outcome == "completed");
		Assert.DoesNotContain(driver.Events, item => item.Action.Contains("quest", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public async Task StaticAnchorOnlyGetsUsIntoSightThenObservedNpcTakesOver()
	{
		var driver = new FakeDriver([])
		{
			AfterMove = (self, moved) => { if (moved == 1) self.Targets = [new(91, 203500, At(19))]; },
		};
		NaturalNavigationResult result = await NaturalIshalgenNavigator.ApproachNpcAsync(220010000, 203500,
			At(16), driver);
		Assert.True(result.Arrived);
		Assert.Equal(91, result.TargetObjectId);
		Assert.Contains(driver.Events, item => item.Action == "route-to-anchor");
		Assert.Contains(driver.Events, item => item.Action == "target-reacquired");
	}

	[Fact]
	public async Task MovingTargetTriggersBoundedReplanFromLatestPacketPosition()
	{
		var driver = new FakeDriver([new(77, 203500, At(12))])
		{
			AfterMove = (self, moved) => { if (moved == 1) self.Targets = [new(77, 203500, At(20))]; },
		};
		NaturalNavigationResult result = await NaturalIshalgenNavigator.ApproachNpcAsync(220010000, 203500,
			At(12), driver);
		Assert.True(result.Arrived);
		Assert.True(result.RouteSearches >= 2);
		Assert.Contains(driver.Events, item => item.Action == "target-reacquired" && item.Destination.X == 20);
	}

	[Fact]
	public async Task NoRouteStallAndMissingTargetFailWithReproducibleReasons()
	{
		var noRoute = new FakeDriver([new(77, 203500, At(20))]) { NoRoute = true };
		NaturalNavigationResult unavailable = await NaturalIshalgenNavigator.ApproachNpcAsync(220010000, 203500,
			At(20), noRoute);
		Assert.False(unavailable.Arrived);
		Assert.Contains("No collision-checked route", unavailable.Reason);
		Assert.Equal(1, unavailable.RouteSearches);

		var stalled = new FakeDriver([new(77, 203500, At(20))]) { Stall = true };
		NaturalNavigationResult stuck = await NaturalIshalgenNavigator.ApproachNpcAsync(220010000, 203500,
			At(20), stalled);
		Assert.False(stuck.Arrived);
		Assert.Contains("bounded replans", stuck.Reason);
		Assert.Equal(4, stuck.Segments);

		var absent = new FakeDriver([]);
		NaturalNavigationResult missing = await NaturalIshalgenNavigator.ApproachNpcAsync(220010000, 203500,
			At(0), absent);
		Assert.False(missing.Arrived);
		Assert.Contains("no NPC was observed", missing.Reason);
		Assert.Equal(2, absent.Synchronizations);
	}

	[Fact]
	public async Task DespawnedTargetHasBoundedWaitAndCanBeReacquiredByTemplate()
	{
		var recovered = new FakeDriver([new(77, 203500, At(20))])
		{
			AfterMove = (self, moved) => { if (moved == 1) self.Targets = []; },
			AfterSynchronize = (self, count) => { if (count == 2) self.Targets = [new(88, 203500, At(20))]; },
		};
		NaturalNavigationResult arrived = await NaturalIshalgenNavigator.ApproachNpcAsync(220010000, 203500,
			At(20), recovered);
		Assert.True(arrived.Arrived);
		Assert.Equal(88, arrived.TargetObjectId);
		Assert.Contains(recovered.Events, item => item.Action == "target-lost");

		var lost = new FakeDriver([new(77, 203500, At(20))])
		{
			AfterMove = (self, moved) => { if (moved == 1) self.Targets = []; },
		};
		NaturalNavigationResult failed = await NaturalIshalgenNavigator.ApproachNpcAsync(220010000, 203500,
			At(20), lost);
		Assert.False(failed.Arrived);
		Assert.Contains("disappeared", failed.Reason);
		Assert.Equal(3, lost.Synchronizations);
	}

	private static BotPosition At(float x) => new(x, 0, 0, 0);

	private sealed class FakeDriver(List<NaturalNavigationObject> targets) : INaturalNavigationDriver
	{
		public List<NaturalNavigationObject> Targets { get; set; } = targets;
		public BotPosition Position { get; private set; } = At(0);
		public bool NoRoute { get; init; }
		public bool Stall { get; init; }
		public int Synchronizations { get; private set; }
		public Action<FakeDriver, int>? AfterMove { get; init; }
		public Action<FakeDriver, int>? AfterSynchronize { get; init; }
		public List<IReadOnlyList<BotPosition>> MovedSegments { get; } = [];
		public List<NaturalNavigationEvent> Events { get; } = [];

		public NaturalNavigationObservation Observe() => new(220010000, Position, false, Targets.ToArray());
		public Task<IReadOnlyList<BotPosition>> FindRouteAsync(BotPosition start, BotPosition destination, CancellationToken token)
		{
			if (NoRoute) return Task.FromResult<IReadOnlyList<BotPosition>>([]);
			var route = Enumerable.Range((int)start.X + 1, (int)destination.X - (int)start.X)
				.Select(x => At(x)).ToArray();
			return Task.FromResult<IReadOnlyList<BotPosition>>(route);
		}
		public Task MoveAsync(IReadOnlyList<BotPosition> segment, CancellationToken token)
		{
			MovedSegments.Add(segment);
			if (!Stall) Position = segment[^1];
			AfterMove?.Invoke(this, MovedSegments.Count);
			return Task.CompletedTask;
		}
		public Task SynchronizeAsync(CancellationToken token)
		{
			Synchronizations++;
			AfterSynchronize?.Invoke(this, Synchronizations);
			return Task.CompletedTask;
		}
		public void Record(NaturalNavigationEvent navigationEvent) => Events.Add(navigationEvent);
	}
}
