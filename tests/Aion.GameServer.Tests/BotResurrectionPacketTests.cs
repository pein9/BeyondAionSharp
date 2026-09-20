using Aion.Bots.Protocol;
using Aion.Bots.World;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Tests;

public sealed class BotResurrectionPacketTests
{
	private readonly BotServerPacketDecoder decoder = new();

	[Fact]
	public void KiskRemovalNoticeAndDeleteRetireTheOwnedKiskEvenWithOneSecondLeft()
	{
		var world = new BotWorldModel();
		world.Apply(new(typeof(SM_STATS_INFO), new Dictionary<string, object?>
		{
			["objectId"] = 5678, ["level"] = (ushort)10, ["expNeeded"] = 0L, ["expRecoverable"] = 0L, ["expShown"] = 0L,
			["maxHp"] = 100, ["currentHp"] = 100, ["maxMp"] = 100, ["currentMp"] = 100,
			["maxDp"] = (ushort)0, ["dp"] = (ushort)0, ["maxFp"] = 60, ["currentFp"] = 60,
		}));
		world.Apply(decoder.Decode(typeof(SM_KISK_UPDATE), Kisk(1234, 42, 1)));
		world.Apply(decoder.Decode(typeof(SM_BIND_POINT_INFO), Bind(4, 400010000, 1234)));
		Delete(9999); // A neighboring Kisk vanishing is not our retirement.
		Delete(1234); // Even our own out-of-range visibility deletion is not a retirement notice.
		Assert.Null(world.OwnedKiskRemoval);
		Notice("STR_BINDSTONE_IS_REMOVED");
		Assert.Equal(new BotKiskRemoval(1234, false, false), world.OwnedKiskRemoval);
		Delete(9999);
		Assert.False(world.OwnedKiskRemoval!.DeleteObserved);
		Delete(1234);
		Assert.Equal(new BotKiskRemoval(1234, false, true), world.OwnedKiskRemoval);
		Assert.Equal(1, world.OwnedKiskUpdate!.RemainingLifetimeSeconds); // Do not fabricate a zero update.
		Assert.Equal(1234, world.KiskBindPoint!.KiskObjectId); // Java sends the old binding before clearing it.
		world.BeginWorldReload();
		Assert.True(world.OwnedKiskRemoval.DeleteObserved);
		world.Apply(decoder.Decode(typeof(SM_KISK_UPDATE), Kisk(4321, 72, 7200)));
		Assert.Null(world.OwnedKiskRemoval);
		Notice("STR_BINDSTONE_IS_REMOVED"); // Old binding must not retire a new, different owned Kisk.
		Assert.Null(world.OwnedKiskRemoval);
		world.Apply(decoder.Decode(typeof(SM_BIND_POINT_INFO), Bind(4, 400010000, 4321)));
		Notice("STR_BINDSTONE_IS_DESTROYED");
		Delete(4321);
		Assert.Equal(new BotKiskRemoval(4321, true, true), world.OwnedKiskRemoval);

		void Notice(string name) => world.Apply(new(typeof(SM_SYSTEM_MESSAGE), new Dictionary<string, object?>
		{ ["msgId"] = 1300803, ["name"] = name, ["params"] = Array.Empty<string>(), ["specialParams"] = Array.Empty<string>(), ["senderObjectId"] = 0 }));
		void Delete(int id) => world.Apply(decoder.Decode(typeof(SM_DELETE), Body(w => { w.Write(id); w.Write((byte)1); })));
	}

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

	[Fact]
	public void NearbyUpdatesCannotOverwriteTheCreatorsOwnBoundedObservation()
	{
		var world = new BotWorldModel();
		world.Apply(new(typeof(SM_STATS_INFO), new Dictionary<string, object?>
		{
			["objectId"] = 5678, ["level"] = (ushort)10, ["expNeeded"] = 0L, ["expRecoverable"] = 0L, ["expShown"] = 0L,
			["maxHp"] = 100, ["currentHp"] = 100, ["maxMp"] = 100, ["currentMp"] = 100,
			["maxDp"] = (ushort)0, ["dp"] = (ushort)0, ["maxFp"] = 60, ["currentFp"] = 60,
		}));
		world.Apply(decoder.Decode(typeof(SM_KISK_UPDATE), Kisk(1234, 72, 7190)));
		byte[] neighbor = Kisk(9999, 72, 7200);
		System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(neighbor.AsSpan(4), 9876);
		world.Apply(decoder.Decode(typeof(SM_KISK_UPDATE), neighbor));
		Assert.Equal(9999, world.LastKiskUpdate!.ObjectId);
		Assert.Equal(1234, world.OwnedKiskUpdate!.ObjectId);
		Assert.Null(world.KiskBindPoint);
		world.BeginWorldReload();
		Assert.Equal(1234, world.OwnedKiskUpdate.ObjectId);
		world.Apply(decoder.Decode(typeof(SM_KISK_UPDATE), Kisk(1234, 0, 0)));
		Assert.Equal(0, world.OwnedKiskUpdate.RemainingResurrects);
		world.Apply(decoder.Decode(typeof(SM_KISK_UPDATE), Kisk(4321, 72, 7200)));
		Assert.Equal(4321, world.OwnedKiskUpdate.ObjectId);
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
