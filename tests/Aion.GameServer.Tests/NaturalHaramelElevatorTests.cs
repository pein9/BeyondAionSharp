using Aion.Bots.Navigation;
using Aion.Bots.Scenarios;
using Aion.GameServer.Controllers.Movement;
using Aion.GameServer.GeoEngine.Collision;
using Aion.GameServer.GeoEngine.Models;
using Aion.GameServer.Network.Aion.ClientPackets;
using Aion.GameServer.TestKit;

namespace Aion.GameServer.Tests;

public sealed class NaturalHaramelElevatorTests
{
	private static readonly NaturalHaramelElevator Elevator = NaturalHaramelElevator.Load(RealStaticData.RepoRoot());

	[Fact]
	public void ClientTicksProduceTheRealTwentySecondCycleAndStops()
	{
		Assert.Equal(20000, Elevator.PeriodMillis);
		Assert.Equal(1700, Elevator.AscentStarts);
		Assert.Equal(8667, Elevator.AscentEnds);
		Assert.InRange(Elevator.Bottom.Z, 90.52f, 90.53f);
		Assert.InRange(Elevator.Top.Z, 144.15f, 144.17f);
		Assert.Equal(Elevator.Bottom, Elevator.At(1700));
		Assert.Equal(Elevator.Top, Elevator.At(10000));
		Assert.Equal(Elevator.Bottom, Elevator.At(19000));
		Assert.Equal(Elevator.At(6000), Elevator.At(26000));
		Assert.Equal(Elevator.At(19000), Elevator.At(-1000));
		Assert.InRange(Elevator.At(5183).Z, (Elevator.Bottom.Z + Elevator.Top.Z) / 2 - .02f,
			(Elevator.Bottom.Z + Elevator.Top.Z) / 2 + .02f);
	}

	[Fact]
	public void BoardingMissWaitsForNextCycleAndCarryUsesOnlyOrdinaryPositionPackets()
	{
		Assert.Equal(20000, Elevator.NextBottomCycle(20000));
		Assert.Equal(40000, Elevator.NextBottomCycle(21700));
		Assert.Throws<InvalidOperationException>(() => Elevator.Ascent(20000, 21700));
		Assert.Throws<InvalidOperationException>(() => Elevator.Ascent(20000, 19999));
		var plan = Elevator.Ascent(20000, 20600);
		Assert.Equal(TimeSpan.FromMilliseconds(8067), plan.Duration);
		Assert.Equal(plan.Duration, TimeSpan.FromTicks(plan.Frames.Sum(f => f.DelayBefore.Ticks)));
		Assert.Equal(Elevator.Top, plan.Frames[^1].Position);
		Assert.All(plan.Frames, frame =>
		{
			Assert.Equal(typeof(CM_MOVE), frame.Packet.PacketType);
			Assert.Equal((byte)(MovementMask.POSITION | MovementMask.ABSOLUTE), frame.Packet.Body[13]);
			Assert.InRange(frame.DelayBefore.TotalMilliseconds, 0, 100);
			Assert.InRange(frame.Position.Z, Elevator.Bottom.Z, Elevator.Top.Z);
		});
	}

	[Fact]
	public void UpperStopBoardsTheActualDescentAndLateBoardingIsRefused()
	{
		Assert.Throws<InvalidOperationException>(() => Elevator.Descent(20000,28666));
		Assert.Throws<InvalidOperationException>(() => Elevator.Descent(20000,31333));
		var plan = Elevator.Descent(20000,29000);
		Assert.Equal(TimeSpan.FromMilliseconds(9667),plan.Duration);
		Assert.Equal(plan.Duration,TimeSpan.FromTicks(plan.Frames.Sum(f => f.DelayBefore.Ticks)));
		Assert.Equal(Elevator.Top,plan.Frames[0].Position);
		Assert.Equal(Elevator.Bottom,plan.Frames[^1].Position);
		Assert.All(plan.Frames,frame => Assert.Equal(typeof(CM_MOVE),frame.Packet.PacketType));
		Assert.True(plan.Frames.Zip(plan.Frames.Skip(1)).All(pair => pair.First.Position.Z >= pair.Second.Position.Z));
	}

	[Fact]
	public void AnUnprovenUpperLandingCannotBecomeAWalkOrHopConnector()
	{
		var geometry = new BotNavigationGeometry(_ => new GeoMap(Elevator.MapId), 1, IgnoreProperties.ANY_RACE).WithNavMesh(null);
		var destination = Elevator.Top with { X = Elevator.Top.X - 3.75f };
		Assert.Empty(Elevator.Disembark(geometry, destination));
		Assert.Empty(Elevator.JumpDisembark(geometry, destination));
	}
}
