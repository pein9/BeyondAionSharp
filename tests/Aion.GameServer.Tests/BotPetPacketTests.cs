using System.Text;
using Aion.Bots.Protocol;
using Aion.Bots.Scenarios;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Tests;

public sealed class BotPetPacketTests
{
	private readonly BotServerPacketDecoder decoder = new();
	[Fact]
	public void AdoptionAndLoginListPreserveOwnedPetAndFoodProgress()
	{
		byte[] row = PetRow();
		var adopted = decoder.Decode(typeof(SM_PET), [1, 0, .. row]).Get<BotPetData>("pet");
		PetLifecycleScenario.VerifyIdentity(adopted, 789);
		Assert.Equal(456, adopted.ObjectId); Assert.Equal(1234567890, adopted.Birthday);
		Assert.Equal(2 << 24, PetLifecycleScenario.FeedProgress(adopted));
		var list = decoder.Decode(typeof(SM_PET), [0, 0, 0, 1, 0, .. row]).Get<BotPetData[]>("pets");
		Assert.Single(list); Assert.Equal(adopted.ObjectId, list[0].ObjectId); Assert.Equal(adopted.Name, list[0].Name);
		foreach (byte[] packet in new byte[][] { [1, 0, .. row], [0, 0, 0, 1, 0, .. row] }) RejectBadLengths(packet);
		Assert.Throws<InvalidDataException>(() => PetLifecycleScenario.VerifyIdentity(adopted with { MasterObjectId = 790 }, 789));
		Assert.Throws<InvalidDataException>(() => PetLifecycleScenario.VerifyIdentity(adopted with { TemplateId = 900044 }, 789));
	}

	[Theory]
	[InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
	[InlineData(5)] [InlineData(6)] [InlineData(7)] [InlineData(8)]
	public void EveryFeedingSubtypeHasExactFieldsAndLength(byte subtype)
	{
		using var stream = new MemoryStream(); using var w = new BinaryWriter(stream);
		w.Write((ushort)9); w.Write((ushort)1); w.Write((byte)1); w.Write(subtype); w.Write(2 << 24); w.Write(0);
		if (subtype is 1 or 2 or 6 or 7 or 8) w.Write(456);
		if (subtype is 1 or 2 or 7 or 8) w.Write(1);
		if (subtype is 2 or 6) w.Write((byte)0);
		byte[] body = stream.ToArray(); var packet = decoder.Decode(typeof(SM_PET), body);
		Assert.True(PetLifecycleScenario.Feeding(packet, subtype)); Assert.Equal(2 << 24, packet.Get<int>("feedProgress"));
		if (subtype is 1 or 2 or 8)
		{
			PetLifecycleScenario.VerifyFeed(packet, 456, 1);
			Assert.Throws<InvalidDataException>(() => PetLifecycleScenario.VerifyFeed(packet, 455, 1));
			Assert.Throws<InvalidDataException>(() => PetLifecycleScenario.VerifyFeed(packet, 456, 0));
		}
		RejectBadLengths(body);
	}

	[Fact]
	public void SummonIdentifiesPetOwnerAndWorldPosition()
	{
		using var stream = new MemoryStream(); using var w = new BinaryWriter(stream, Encoding.Unicode);
		w.Write((ushort)3); w.Write(Encoding.Unicode.GetBytes("Mookie\0")); w.Write(900043); w.Write(456);
		foreach (float coordinate in new float[] { 1, 2, 3, 4, 5, 6 }) w.Write(coordinate);
		w.Write((byte)7); w.Write(789); Appearance(w);
		byte[] body = stream.ToArray(); var packet = decoder.Decode(typeof(SM_PET), body);
		Assert.Equal(900043, packet.Get<int>("templateId")); Assert.Equal(456, packet.Get<int>("petObjectId"));
		Assert.Equal(789, packet.Get<int>("masterObjectId")); Assert.Equal(1f, packet.Get<float>("x")); Assert.Equal(6f, packet.Get<float>("targetZ"));
		RejectBadLengths(body);
	}

	[Theory]
	[InlineData("0900010001000000000000000000")]
	[InlineData("0900020001050000000000000000")]
	[InlineData("000000FFFF")]
	[InlineData("0500")]
	[InlineData("0C0001")]
	[InlineData("0D00030002")]
	public void RejectsUnknownActionsMalformedCountsAndInvalidFlags(string hex) =>
		Assert.Throws<InvalidDataException>(() => decoder.Decode(typeof(SM_PET), Convert.FromHexString(hex)));

	private void RejectBadLengths(byte[] body)
	{
		for (int size = 0; size < body.Length; size++)
			Assert.Throws<InvalidDataException>(() => decoder.Decode(typeof(SM_PET), body.AsSpan(0, size)));
		Assert.Throws<InvalidDataException>(() => decoder.Decode(typeof(SM_PET), [.. body, 0]));
	}
	private static byte[] PetRow()
	{
		using var stream = new MemoryStream(); using var w = new BinaryWriter(stream, Encoding.Unicode);
		w.Write(Encoding.Unicode.GetBytes("Mookie\0")); w.Write(900043); w.Write(456); w.Write(789);
		w.Write(0); w.Write(0); w.Write(1234567890); w.Write(0);
		w.Write((byte)1); w.Write((byte)8); w.Write(2 << 24); w.Write(0); w.Write((ushort)6);
		Appearance(w); return stream.ToArray();
	}
	private static void Appearance(BinaryWriter w)
	{
		w.Write((ushort)1); w.Write(new byte[3]); w.Write(0); w.Write(0); w.Write(0);
	}
}
