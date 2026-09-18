using Aion.Bots.Protocol;
using Aion.Bots.Scenarios;
using Aion.Bots.World;
using Aion.GameServer.Model;
using Aion.GameServer.Network.Aion.ClientPackets;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Tests;

public sealed class BotCapitalScenarioTests
{
	[Fact]
	public void ScriptedFlightUsesTimedMovementUpdatesUntilTheConfiguredLandingTime()
	{
		var start = CapitalAscensionScenario.Belpartan;
		var finish = new BotPosition(218.85f, 250.49f, 206.72f, 0);
		var plan = CapitalAscensionScenario.CreateQuestFlight(start, finish, 310020000, TimeSpan.FromSeconds(45));
		Assert.Equal(90, plan.Frames.Count);
		Assert.Equal(TimeSpan.FromSeconds(45), plan.Duration);
		Assert.Equal(plan.Duration.Ticks, plan.Frames.Sum(frame => frame.DelayBefore.Ticks));
		Assert.All(plan.Frames, frame =>
		{
			Assert.Equal(TimeSpan.FromMilliseconds(500), frame.DelayBefore);
			Assert.Equal(typeof(CM_MOVE_IN_AIR), frame.Packet.PacketType);
		});
		Assert.Equal(finish, plan.Frames[^1].Position);
		var packet = new PacketBodyReader(plan.Frames[^1].Packet.Body);
		Assert.Equal(310020000, packet.ReadInt32());
		Assert.Equal(finish.X, packet.ReadSingle()); Assert.Equal(finish.Y, packet.ReadSingle()); Assert.Equal(finish.Z, packet.ReadSingle());
		Assert.Equal(finish.Heading, packet.ReadByte());
		Assert.Equal((int)MathF.Round(plan.Distance), packet.ReadInt32());
		Assert.Equal(0, packet.Remaining);
	}

	[Fact]
	public void ScriptedFlightRejectsNonpositiveDuration() => Assert.Throws<ArgumentOutOfRangeException>(() =>
		CapitalAscensionScenario.CreateQuestFlight(default, default, 310020000, TimeSpan.Zero));

	[Fact]
	public void FlightStartEmotionExposesTheServerSelectedPath()
	{
		// SM_EMOTION.java START_FLYTELEPORT writes a D path id after its common D/C/H/F header.
		byte[] body = [1, 0, 0, 0, (byte)EmotionType.START_FLYTELEPORT, 0, 0, 0, 0, 192, 64, 233, 3, 0, 0];
		var packet = new BotServerPacketDecoder().Decode(typeof(SM_EMOTION), body);
		Assert.Equal(1001, packet.Get<int>("teleportId"));
		Assert.Equal(6f, packet.Get<float>("movementSpeed"));
		Assert.Throws<InvalidDataException>(() => new BotServerPacketDecoder().Decode(typeof(SM_EMOTION), body[..^1]));
	}
}
