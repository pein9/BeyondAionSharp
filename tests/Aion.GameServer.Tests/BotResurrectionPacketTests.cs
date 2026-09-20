using Aion.Bots.Protocol;
using Aion.Bots.World;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Tests;

public sealed class BotResurrectionPacketTests
{
	private readonly BotServerPacketDecoder decoder = new();

	public static bool AssertAuditedWireContract(Type type)
	{
		var tests = new BotResurrectionPacketTests();
		if (type == typeof(SM_BIND_POINT_INFO)) tests.BindingKindsAreIndependentAndOnlyExplicitClearRemovesKiskBinding();
		else if (type == typeof(SM_KISK_UPDATE)) tests.KiskUpdateIsOneObservedSnapshotNotInferredBindingOrResurrection();
		else return false;
		return true;
	}

	[Fact]
	public void BindingKindsAreIndependentAndOnlyExplicitClearRemovesKiskBinding()
	{
		var world = new BotWorldModel();
		world.Apply(Checked(typeof(SM_BIND_POINT_INFO), Bind(0, 210010000, 0)));
		world.Apply(Checked(typeof(SM_BIND_POINT_INFO), Bind(4, 400010000, 1234)));
		Assert.Equal(new BotBindPoint(210010000, new(301.25f, 402.5f, 1450.75f, 0), 0), world.ObeliskBindPoint);
		Assert.Equal(new BotBindPoint(400010000, new(301.25f, 402.5f, 1450.75f, 0), 1234), world.KiskBindPoint);
		world.BeginWorldReload();
		Assert.Equal(1234, world.KiskBindPoint!.KiskObjectId);
		world.Apply(Checked(typeof(SM_BIND_POINT_INFO), Bind(0, 120010000, 0)));
		Assert.Equal(1234, world.KiskBindPoint.KiskObjectId);
		world.Apply(Checked(typeof(SM_BIND_POINT_INFO), Bind(4, 0, 0)));
		Assert.Null(world.KiskBindPoint);
		Assert.Equal(120010000, world.ObeliskBindPoint!.MapId);
		byte[] invalid = Bind(4, 400010000, 1234);
		invalid[0] = 2;
		Assert.Throws<InvalidDataException>(() => decoder.Decode(typeof(SM_BIND_POINT_INFO), invalid));
		invalid[0] = 4; invalid[1] = 0;
		Assert.Throws<InvalidDataException>(() => decoder.Decode(typeof(SM_BIND_POINT_INFO), invalid));
	}

	[Fact]
	public void KiskUpdateIsOneObservedSnapshotNotInferredBindingOrResurrection()
	{
		var world = new BotWorldModel();
		world.Apply(decoder.Decode(typeof(SM_DIE), new byte[8]));
		world.Apply(Checked(typeof(SM_KISK_UPDATE), Kisk(1234, 72, 7190)));
		Assert.Equal(new BotKiskUpdate(1234, 5678, 1, 2, 24, 72, 72, 7190), world.LastKiskUpdate);
		Assert.Null(world.KiskBindPoint);
		Assert.True(world.IsDead);
		world.Apply(Checked(typeof(SM_KISK_UPDATE), Kisk(1234, 71, 7000)));
		Assert.Equal(71, world.LastKiskUpdate!.RemainingResurrects);
		Assert.True(world.IsDead);
		// Nearby same-race Kisks also broadcast updates. Never infer ownership/binding or accumulate history.
		for (int id = 1; id <= 500; id++) world.Apply(decoder.Decode(typeof(SM_KISK_UPDATE), Kisk(id, 0, 0)));
		Assert.Equal(500, world.LastKiskUpdate!.ObjectId);
		Assert.Equal(0, world.LastKiskUpdate.RemainingLifetimeSeconds);
		world.BeginWorldReload();
		Assert.Null(world.LastKiskUpdate);
	}

	private DecodedBotServerPacket Checked(Type type, byte[] body)
	{
		for (int length = 0; length < body.Length; length++)
			Assert.Throws<InvalidDataException>(() => decoder.Decode(type, body[..length]));
		Assert.Throws<InvalidDataException>(() => decoder.Decode(type, [.. body, 0]));
		return decoder.Decode(type, body);
	}
	private static byte[] Bind(byte type, int map, int objectId) => Body(w =>
	{
		w.Write(type); w.Write((byte)1); w.Write(map);
		w.Write(map == 0 ? 0 : 301.25f); w.Write(map == 0 ? 0 : 402.5f); w.Write(map == 0 ? 0 : 1450.75f); w.Write(objectId);
	});
	private static byte[] Kisk(int id, int remaining, int seconds) => Body(w =>
	{
		w.Write(id); w.Write(5678); w.Write(1); w.Write(2); w.Write(24); w.Write(remaining); w.Write(72); w.Write(seconds);
	});
	private static byte[] Body(Action<BinaryWriter> write)
	{
		using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream);
		write(writer); return stream.ToArray();
	}
}
