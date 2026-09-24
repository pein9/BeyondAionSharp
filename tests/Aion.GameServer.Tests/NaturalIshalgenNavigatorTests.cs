using Aion.Bots.Navigation;
using Aion.Bots.World;

namespace Aion.GameServer.Tests;

public sealed class NaturalIshalgenNavigatorTests
{
	[Fact]
	public async Task CheckedIngressReturnRetracesRecordedPositionsInReverse()
	{
		var driver = new FakeDriver([]) { Position = At(20) };
		NaturalNavigationResult result = await NaturalIshalgenNavigator.RetraceIngressAsync(
			220010000, At(0), [At(5), At(10), At(15), At(20)], driver);
		Assert.True(result.Arrived, result.Reason);
		Assert.Equal(0, driver.Position.X);
		Assert.Equal([15f, 10f, 5f, 0f],
			driver.MovedSegments.Select(segment => segment[^1].X).ToArray());
		Assert.Equal(4, result.RouteSearches);
	}

	[Fact]
	public async Task CheckedIngressReturnSkipsNewlyBlockedCheckpointOnlyWithSafeDetour()
	{
		var driver = new FakeDriver([]) { Position = At(20), BlockedDestinations = [15] };
		NaturalNavigationResult result = await NaturalIshalgenNavigator.RetraceIngressAsync(
			220010000, At(0), [At(5), At(10), At(15), At(20)], driver);
		Assert.True(result.Arrived, result.Reason);
		Assert.Equal(0, driver.Position.X);
		Assert.DoesNotContain(driver.MovedSegments, segment => segment[^1].X == 15);
		Assert.Contains(driver.Events, entry => entry.Action == "navigation-failed" &&
			entry.Destination.X == 15);
	}

	[Fact]
	public async Task CheckedIngressReturnCanSkipFiveNewlyBlockedCheckpointsWhenStartRemainsReachable()
	{
		var driver = new FakeDriver([])
		{
			Position = At(30),
			BlockedDestinations = [5, 10, 15, 20, 25],
		};
		NaturalNavigationResult result = await NaturalIshalgenNavigator.RetraceIngressAsync(
			220010000, At(0), [At(5), At(10), At(15), At(20), At(25), At(30)], driver);
		Assert.True(result.Arrived, result.Reason);
		Assert.Equal(0, driver.Position.X);
		Assert.Equal(5, driver.Events.Count(entry => entry.Action == "navigation-failed"));
	}

	[Fact]
	public async Task CheckedIngressReturnStopsWhenAReverseLegHasNoRoute()
	{
		var driver = new FakeDriver([]) { Position = At(20), NoRoute = true };
		NaturalNavigationResult result = await NaturalIshalgenNavigator.RetraceIngressAsync(
			220010000, At(0), [At(5), At(10), At(15), At(20)], driver);
		Assert.False(result.Arrived);
		Assert.Contains("checkpoint", result.Reason);
		Assert.Empty(driver.MovedSegments);
	}

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
	public async Task RangedSearchStopsNearAreaHintWithoutClaimingAVisibleTarget()
	{
		var driver = new FakeDriver([]) { MaxRoutePoints = 16 };
		NaturalNavigationResult result = await NaturalIshalgenNavigator.ExploreWithinRangeAsync(
			220010000, -1, At(100), 23, driver, "stalker-search-area");
		Assert.True(result.Arrived, result.Reason);
		Assert.Null(result.TargetObjectId);
		Assert.True(result.RouteSearches > 1); // The planner can return safe forward progress, not a full route.
		Assert.InRange(driver.Position.X, 77, 100);
		Assert.Contains(driver.Events, entry => entry.Action == "anchor-observed");
		Assert.DoesNotContain(driver.MovedSegments, segment => segment[^1].X > 80);
	}

	[Fact]
	public async Task ObservedCombatTargetStopsAtPriestSpellRangeInsteadOfMeleeRange()
	{
		var driver = new FakeDriver([new(77, 210402, At(30))]);
		NaturalNavigationResult result = await NaturalIshalgenNavigator.ExploreWithinRangeAsync(
			220010000, 210402, At(30), 23, driver, "priest-spell-range-target");
		Assert.True(result.Arrived, result.Reason);
		Assert.Equal(77, result.TargetObjectId);
		Assert.InRange(driver.Position.X, 7, 10);
		Assert.DoesNotContain(driver.MovedSegments, segment => segment[^1].X > 10);
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
	public async Task NewlyObservedHazardReplansBeforeEnteringNextSegment()
	{
		var driver = new FakeDriver([new(77, 203500, At(20))]) { RejectNextSegment = true };
		NaturalNavigationResult result = await NaturalIshalgenNavigator.ApproachNpcAsync(220010000,
			203500, At(20), driver);
		Assert.True(result.Arrived, result.Reason);
		Assert.True(result.RouteSearches >= 2);
		Assert.Contains(driver.Events, item => item.Action == "replan-hostile");
		Assert.Equal(1f, driver.MovedSegments[0][0].X); // The rejected segment was never sent.
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

	[Fact]
	public void ReturnBreadcrumbsUseOnlyWalkedProgressAndKeepTheLastPoint()
	{
		NaturalNavigationEvent Event(int sequence, string action, BotPosition? position) =>
			new(sequence, action, "completed", "test", 220010000, -1, position,
				At(100), null, 1, sequence, action == "route-to-anchor" ? [At(99)] : null);
		NaturalNavigationEvent[] events =
		[
			Event(1, "route-to-anchor", At(99)),
			Event(2, "segment-progress", At(10)),
			Event(3, "segment-progress", At(20)),
			Event(4, "segment-progress", At(30)),
			Event(5, "segment-progress", At(40)),
		];
		Assert.Equal([At(10), At(30), At(40)],
			NaturalIshalgenNavigator.SelectRetraceCheckpoints(events));
		Assert.Empty(NaturalIshalgenNavigator.SelectRetraceCheckpoints(
			[Event(1, "route-to-anchor", At(99))]));
	}

	private static BotPosition At(float x) => new(x, 0, 0, 0);

	private sealed class FakeDriver(List<NaturalNavigationObject> targets) : INaturalNavigationDriver
	{
		public List<NaturalNavigationObject> Targets { get; set; } = targets;
		public BotPosition Position { get; set; } = At(0);
		public bool NoRoute { get; init; }
		public HashSet<float> BlockedDestinations { get; init; } = [];
		public bool Stall { get; init; }
		public int MaxRoutePoints { get; init; } = int.MaxValue;
		public bool RejectNextSegment { get; set; }
		public int Synchronizations { get; private set; }
		public Action<FakeDriver, int>? AfterMove { get; init; }
		public Action<FakeDriver, int>? AfterSynchronize { get; init; }
		public List<IReadOnlyList<BotPosition>> MovedSegments { get; } = [];
		public List<NaturalNavigationEvent> Events { get; } = [];

		public NaturalNavigationObservation Observe() => new(220010000, Position, false, Targets.ToArray());
		public Task<IReadOnlyList<BotPosition>> FindRouteAsync(BotPosition start, BotPosition destination, CancellationToken token)
		{
			if (NoRoute || BlockedDestinations.Contains(destination.X))
				return Task.FromResult<IReadOnlyList<BotPosition>>([]);
			int step = Math.Sign(destination.X - start.X);
			var route = Enumerable.Range(1, Math.Abs((int)destination.X - (int)start.X))
				.Select(index => At(start.X + step * index)).ToArray();
			return Task.FromResult<IReadOnlyList<BotPosition>>(route.Take(MaxRoutePoints).ToArray());
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
		public bool IsSegmentSafe(IReadOnlyList<BotPosition> segment, int? targetObjectId)
		{
			if (!RejectNextSegment) return true;
			RejectNextSegment = false;
			return false;
		}
		public void Record(NaturalNavigationEvent navigationEvent) => Events.Add(navigationEvent);
	}
}
