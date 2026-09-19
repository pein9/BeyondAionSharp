using System.Text;
using Aion.Bots.Api;
using Aion.Bots.Protocol;
using Aion.Bots.Scenarios;
using Aion.Bots.World;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Tests;

public sealed class BotExtendedSocialPacketTests
{
	private readonly BotServerPacketDecoder decoder = new();

	[Fact]
	public void RecallUsesTheStarterSpellbookClientMotionWithoutProjectileTravel()
	{
		Assert.Equal((ushort)667, ExtendedSocialScenario.RecallHitTime);
	}

	// Hand-built wire contracts audited against ce54b7931 SM_* writeImpl; no Java runtime required.
	public static bool AssertAuditedWireContract(Type type)
	{
		var tests = new BotExtendedSocialPacketTests();
		if (type == typeof(SM_FIND_GROUP))
		{
			tests.FindGroupListsReplaceAndRemovalOnlyRemovesTheMatchingRow();
			foreach (byte action in new byte[] { 10, 11, 14, 16, 18, 22, 23, 24, 26 }) tests.InstanceGroupBranchesHaveExactBoundaries(action);
		}
		else if (type == typeof(SM_RECALLED_BY_OTHER)) tests.RecallRequiresAnObservedOfferAndDoesNotTeleportOptimistically();
		else if (type == typeof(SM_LEGION_HISTORY)) tests.HistoryStringsAreFixedWidthAndPagesReplaceOnlyTheirOwnHistoryType();
		// Emblem and edit packets also have checked-in golden fixtures: keep the inventory loop testing those.
		else return false;
		return true;
	}

	[Fact]
	public void FindGroupListsReplaceAndRemovalOnlyRemovesTheMatchingRow()
	{
		var world = new BotWorldModel();
		world.Apply(Checked(typeof(SM_FIND_GROUP), Listings(0, 101, 102)));
		Assert.Equal(2, world.GroupRecruitments.Count);
		Assert.Equal(new BotGroupRecruitment(101, 5, 16, 2, "Post", "Leader", 1, 23, 23, 12345), world.GroupRecruitments[101]);
		world.Apply(Checked(typeof(SM_FIND_GROUP), Listings(4, 201, 202)));
		Assert.Equal(2, world.GroupApplications.Count);
		Assert.Equal(new BotGroupApplication(201, 2, "Post", "Leader", 7, 23, 12345), world.GroupApplications[201]);
		world.Apply(Checked(typeof(SM_FIND_GROUP), Body(w => { w.Write((byte)1); w.Write(101); w.Write(new byte[] { 5, 0, 0, 16 }); })));
		world.Apply(Checked(typeof(SM_FIND_GROUP), Body(w => { w.Write((byte)5); w.Write(201); })));
		Assert.Equal(102, Assert.Single(world.GroupRecruitments).Key);
		Assert.Equal(202, Assert.Single(world.GroupApplications).Key);
		world.Apply(Checked(typeof(SM_FIND_GROUP), Listings(0)));
		Assert.Empty(world.GroupRecruitments); Assert.Single(world.GroupApplications);
		world.Apply(Checked(typeof(SM_FIND_GROUP), Listings(4)));
		Assert.Empty(world.GroupApplications);
		Assert.Throws<InvalidDataException>(() => decoder.Decode(typeof(SM_FIND_GROUP), [255]));
	}

	[Theory]
	[InlineData((byte)10)] [InlineData((byte)11)] [InlineData((byte)14)] [InlineData((byte)16)]
	[InlineData((byte)18)] [InlineData((byte)22)] [InlineData((byte)23)] [InlineData((byte)24)] [InlineData((byte)26)]
	public void InstanceGroupBranchesHaveExactBoundaries(byte action)
	{
		var packet = Checked(typeof(SM_FIND_GROUP), Body(w =>
		{
			w.Write(action);
			if (action is 10 or 16) { w.Write((ushort)1); w.Write((ushort)1); w.Write(12345); }
			if (action == 14) w.Write((byte)1);
			switch (action)
			{
				case 10: case 14:
					w.Write(101); w.Write(2048); w.Write(1); w.Write((byte)2); w.Write((byte)6); w.Write((ushort)0);
					w.Write(201);
					if (action == 10) { w.Write(1); w.Write(0); }
					else { w.Write((byte)1); w.Write((byte)0); w.Write(1); w.Write((ushort)0); }
					w.Write((byte)23); w.Write((byte)65); w.Write((ushort)0); w.Write(12345); w.Write(0);
					S(w, "Leader"); S(w, "Instance"); break;
				case 11:
					w.Write(201); w.Write(new byte[11]); w.Write((byte)7); w.Write(23); S(w, "Applicant"); break;
				case 16:
					w.Write(0); w.Write(210010000); w.Write(201); w.Write(23); w.Write(7); w.Write((ushort)1);
					w.Write((ushort)0); S(w, "Member"); break;
				case 18: case 22: case 23: case 24:
					w.Write(101); w.Write(2048);
					if (action == 23) w.Write((byte)1);
					if (action == 24)
					{
						w.Write((byte)1); w.Write(0L); w.Write(201); w.Write(23); w.Write(7); w.Write((ushort)0);
						w.Write((byte)1); w.Write((byte)1); S(w, "Member");
					}
					break;
				case 26: w.Write((ushort)2); w.Write(2048); w.Write(4096); break;
			}
		}));
		Assert.Equal(action, packet.Get<byte>("action"));
		if (action is 10 or 14)
		{
			var row = Assert.Single(packet.Get<List<IReadOnlyDictionary<string, object?>>>("entries"));
			Assert.Equal(101, row["groupId"]); Assert.Equal(2048, row["instanceMaskId"]);
			Assert.Equal((byte)2, row["size"]); Assert.Equal((byte)6, row["minMembers"]); Assert.Equal(201, row["recruiterId"]);
			Assert.Equal((byte)23, row["minLevel"]); Assert.Equal((byte)65, row["maxLevel"]);
			Assert.Equal(12345, row["lastUpdate"]); Assert.Equal("Leader", row["name"]); Assert.Equal("Instance", row["message"]);
		}
		else if (action is 16 or 24)
		{
			var row = Assert.Single(packet.Get<List<IReadOnlyDictionary<string, object?>>>("entries"));
			Assert.Equal(201, row["objectId"]); Assert.Equal(23, row["level"]); Assert.Equal(7, row["playerClass"]); Assert.Equal("Member", row["name"]);
			if (action == 24) { Assert.Equal(true, row["online"]); Assert.Equal(true, row["ready"]); }
			else Assert.Equal(210010000, row["mapId"]);
		}
		else if (action == 11)
		{
			Assert.Equal(201, packet.Get<int>("objectId")); Assert.Equal((byte)7, packet.Get<byte>("playerClass"));
			Assert.Equal(23, packet.Get<int>("level")); Assert.Equal("Applicant", packet.Get<string>("name"));
		}
		else if (action == 26) Assert.Equal(new[] { 2048, 4096 }, packet.Get<int[]>("instanceMaskIds"));
		if (action is 18 or 22 or 23 or 24)
		{
			Assert.Equal(101, packet.Get<int>("groupId")); Assert.Equal(2048, packet.Get<int>("instanceMaskId"));
			if (action == 23) Assert.True(packet.Get<bool>("showEnterMessage"));
		}
	}

	[Fact]
	public void RecallRequiresAnObservedOfferAndDoesNotTeleportOptimistically()
	{
		var api = new BotApi();
		Assert.Throws<InvalidOperationException>(() => api.AnswerRecall(true));
		var offer = Checked(typeof(SM_RECALLED_BY_OTHER), Body(w => { w.Write((byte)0); S(w, "Caster"); w.Write((ushort)3777); w.Write((ushort)30); }));
		api.Observe(offer);
		Assert.Equal(new BotRecallRequest("Caster", 3777, 30), api.World.RecallRequest);
		Assert.Equal(new byte[] { 0 }, api.AnswerRecall(true).Body);
		Assert.Null(api.World.RecallRequest); Assert.Null(api.World.Position);
		Assert.Throws<InvalidOperationException>(() => api.AnswerRecall(true));
		api.Observe(offer);
		Assert.Equal(new byte[] { 1 }, api.AnswerRecall(false).Body);
		Assert.Null(api.World.RecallRequest); Assert.Null(api.World.Position);
		api.Observe(offer);
		api.Observe(Checked(typeof(SM_RECALLED_BY_OTHER), new byte[] { 1, 0, 0, 0, 0, 0, 0 }));
		Assert.Null(api.World.RecallRequest);
		Assert.Throws<InvalidOperationException>(() => api.AnswerRecall(false));
	}

	[Theory]
	[InlineData((byte)0)] [InlineData((byte)1)] [InlineData((byte)2)] [InlineData((byte)3)] [InlineData((byte)4)]
	[InlineData((byte)5)] [InlineData((byte)6)] [InlineData((byte)7)] [InlineData((byte)8)]
	public void LegionEditBranchesHaveExactBoundaries(byte type)
	{
		var packet = Checked(typeof(SM_LEGION_EDIT), Body(w =>
		{
			w.Write(type);
			switch (type)
			{
				case 0: w.Write((byte)2); break;
				case 1: case 6: w.Write(12345); break;
				case 2: foreach (ushort permission in new ushort[] { 65535, 128, 64, 32 }) w.Write(permission); break;
				case 3: case 4: w.Write(5_000_000_001L); break;
				case 5: S(w, "Announcement"); w.Write(12345); break;
			}
		}));
		Assert.Equal(type, packet.Get<byte>("type"));
		var world = new BotWorldModel(); world.Apply(packet);
		switch (type)
		{
			case 0: Assert.Equal((byte)2, world.LegionLevel); break;
			case 1: Assert.Equal(12345, packet.Get<int>("ranking")); break;
			case 2:
				Assert.Equal((ushort)65535, packet.Get<ushort>("deputyPermission")); Assert.Equal((ushort)128, packet.Get<ushort>("centurionPermission"));
				Assert.Equal((ushort)64, packet.Get<ushort>("legionaryPermission")); Assert.Equal((ushort)32, packet.Get<ushort>("volunteerPermission")); break;
			case 3: Assert.Equal(5_000_000_001L, packet.Get<long>("contribution")); break;
			case 4: Assert.Equal(5_000_000_001L, world.LegionWarehouseKinah); break;
			case 5: Assert.Equal("Announcement", packet.Get<string>("announcement")); Assert.Equal(12345, packet.Get<int>("timestamp")); break;
			case 6: Assert.Equal(12345, packet.Get<int>("disbandTime")); break;
		}
	}

	[Fact]
	public void EmblemsAreKeyedByLegionNotAssumedToBelongToSelf()
	{
		var world = new BotWorldModel();
		foreach (int id in new[] { 101, 102, 101 })
			world.Apply(Checked(typeof(SM_LEGION_UPDATE_EMBLEM), Body(w => { w.Write(id); w.Write(new byte[] { 3, 0, 255, 250, 128, 64 }); })));
		Assert.Equal(2, world.LegionEmblems.Count);
		Assert.Equal(new BotLegionEmblem(101, 3, 0, 255, 250, 128, 64), world.LegionEmblems[101]);
		Assert.Null(world.LegionName);
	}

	[Fact]
	public void HistoryStringsAreFixedWidthAndPagesReplaceOnlyTheirOwnHistoryType()
	{
		var body = Body(w =>
		{
			w.Write(10); w.Write(1); w.Write(2);
			foreach (var row in new[] { (17, "", "5000000001"), (18, new string('X', 32), "1") })
			{
				w.Write(12345); w.Write((byte)row.Item1); w.Write((byte)0);
				Fixed(w, row.Item2); Fixed(w, row.Item3); w.Write((ushort)0);
			}
			w.Write((ushort)2);
		});
		var world = new BotWorldModel(); world.Apply(Checked(typeof(SM_LEGION_HISTORY), body));
		var history = world.LegionHistoryPages[2];
		Assert.Equal(10, history.TotalEntries); Assert.Equal(1, history.Page); Assert.Equal((ushort)2, history.Type);
		Assert.Equal(new[] { new BotLegionHistoryEntry(12345, 17, "", "5000000001"), new BotLegionHistoryEntry(12345, 18, new string('X', 32), "1") }, history.Entries);
		world.Apply(Checked(typeof(SM_LEGION_HISTORY), Body(w => { w.Write(3); w.Write(9); w.Write(0); w.Write((ushort)0); })));
		Assert.Same(history, world.LegionHistoryPages[2]); Assert.Empty(world.LegionHistoryPages[0].Entries);
		world.Apply(Checked(typeof(SM_LEGION_HISTORY), Body(w => { w.Write(10); w.Write(9); w.Write(0); w.Write((ushort)2); })));
		Assert.Empty(world.LegionHistoryPages[2].Entries); Assert.Equal(9, world.LegionHistoryPages[2].Page);
		body[82] = 1; // First fixed name's dedicated terminator.
		Assert.Throws<InvalidDataException>(() => decoder.Decode(typeof(SM_LEGION_HISTORY), body));
	}

	private DecodedBotServerPacket Checked(Type type, byte[] body)
	{
		var packet = decoder.Decode(type, body);
		Assert.Throws<InvalidDataException>(() => decoder.Decode(type, body[..^1]));
		Assert.Throws<InvalidDataException>(() => decoder.Decode(type, [.. body, 255]));
		return packet;
	}
	private static byte[] Listings(byte action, params int[] ids) => Body(w =>
	{
		w.Write(action); w.Write((ushort)ids.Length); w.Write((ushort)ids.Length); w.Write(12345);
		foreach (int id in ids)
		{
			w.Write(id); if (action == 0) w.Write(new byte[] { 5, 0, 0, 16 });
			w.Write((byte)2); S(w, "Post"); S(w, "Leader");
			w.Write(action == 0 ? new byte[] { 1, 23, 23 } : new byte[] { 7, 23 }); w.Write(12345);
		}
	});
	private static byte[] Body(Action<BinaryWriter> write)
	{
		using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream, Encoding.Unicode, true);
		write(writer); writer.Flush(); return stream.ToArray();
	}
	private static void S(BinaryWriter w, string text) { w.Write(Encoding.Unicode.GetBytes(text)); w.Write((ushort)0); }
	private static void Fixed(BinaryWriter w, string text)
	{
		for (int i = 0; i < 32; i++) w.Write((ushort)(i < text.Length ? text[i] : 0));
		w.Write((ushort)0);
	}
}
