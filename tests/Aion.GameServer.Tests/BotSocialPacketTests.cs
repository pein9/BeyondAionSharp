using System.Text;
using Aion.Bots.Protocol;
using Aion.Bots.Scenarios;
using Aion.Bots.World;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Tests;

public sealed class BotSocialPacketTests
{
	private readonly BotServerPacketDecoder decoder = new();

	[Fact]
	public void DuelHitTimeIncludesTheRealClientMotionAndProjectileTravel()
	{
		var point = SocialBasicsScenario.Registrar;
		Assert.Equal((ushort)467, SocialBasicsScenario.DuelHitTime(point, point with { X = point.X - 2 }));
		Assert.Equal((ushort)500, SocialBasicsScenario.DuelHitTime(point, point with { X = point.X - 3 }));
	}

	// These five packets have no checked-in Java-generated fixtures. These are hand-built contracts
	// audited against upstream 4.8 ce54b7931 SM_* writeImpl, not claimed to be Java execution results.
	public static bool AssertAuditedWireContract(Type packetType)
	{
		var tests = new BotSocialPacketTests();
		if (packetType == typeof(SM_GROUP_MEMBER_INFO))
			foreach (byte action in new byte[] { 0, 1, 3, 5, 7, 13, 65 }) tests.GroupMemberBranchesHaveExactBoundaries(action);
		else if (packetType == typeof(SM_LEGION_INFO))
			foreach (int count in new[] { 0, 1, 7 }) tests.LegionAnnouncementsStopAtEmptyStringOrSevenEntries(count);
		else if (packetType == typeof(SM_LEGION_ADD_MEMBER)) tests.LegionAddMemberCarriesIdentityAndRank();
		else if (packetType == typeof(SM_LEGION_MEMBERLIST))
			foreach (short count in new short[] { -1, 0, 1 }) tests.LegionListPreservesSignedChunkBoundary(count);
		else if (packetType == typeof(SM_FRIEND_UPDATE)) tests.FriendUpdatePreservesMemoAndHouseAndAcceptsMissingFriendBody();
		else return false;
		return true;
	}

	[Fact]
	public void FriendAndBlockListsDecodeNonemptyRowsAndReplaceClientState()
	{
		var world = new BotWorldModel();
		world.Apply(DecodeChecked(typeof(SM_FRIEND_LIST), FriendList()));
		Assert.Equal(new BotFriend(101, "Friend", 10, 3, 1, 210010000, 12345, "Note", 0, 123, 2, "Private memo"), Assert.Single(world.Friends).Value);
		world.Apply(DecodeChecked(typeof(SM_BLOCK_LIST), Body(w => { w.Write((short)-1); w.Write((byte)0); S(w, "Blocked"); S(w, "Reason"); })));
		Assert.Equal("Reason", Assert.Single(world.BlockedPlayers).Value);
		world.Apply(DecodeChecked(typeof(SM_FRIEND_LIST), [0, 0, 0]));
		world.Apply(DecodeChecked(typeof(SM_BLOCK_LIST), [0, 0, 0]));
		Assert.Empty(world.Friends); Assert.Empty(world.BlockedPlayers);
		Assert.Throws<InvalidDataException>(() => decoder.Decode(typeof(SM_FRIEND_LIST), [1, 0, 0]));
		Assert.Throws<InvalidDataException>(() => decoder.Decode(typeof(SM_BLOCK_LIST), [1, 0, 0]));
	}

	[Fact]
	public void FriendRelogRequiresFreshListPacketsNotOnlyRetainedState()
	{
		var friends = decoder.Decode(typeof(SM_FRIEND_LIST), [0, 0, 0]);
		var blocks = decoder.Decode(typeof(SM_BLOCK_LIST), [0, 0, 0]);
		FriendsAndBlocksScenario.AssertReloadPackets([friends, blocks]);
		Assert.Throws<InvalidDataException>(() => FriendsAndBlocksScenario.AssertReloadPackets([]));
		Assert.Throws<InvalidDataException>(() => FriendsAndBlocksScenario.AssertReloadPackets([friends]));
		Assert.Throws<InvalidDataException>(() => FriendsAndBlocksScenario.AssertReloadPackets([blocks]));
		Assert.Throws<InvalidDataException>(() => FriendsAndBlocksScenario.AssertReloadPackets([friends, blocks, friends]));
	}

	[Fact]
	public void FriendUpdatePreservesMemoAndHouseAndAcceptsMissingFriendBody()
	{
		var world = new BotWorldModel();
		world.Apply(decoder.Decode(typeof(SM_FRIEND_LIST), FriendList()));
		world.Apply(DecodeChecked(typeof(SM_FRIEND_UPDATE), Body(w => WriteFriend(w, online: true))));
		Assert.Equal(new BotFriend(101, "Friend", 10, 3, 1, 210010000, 0, "Note", 1, 123, 2, "Private memo"), Assert.Single(world.Friends).Value);
		var empty = decoder.Decode(typeof(SM_FRIEND_UPDATE), []);
		Assert.True(empty.Get<bool>("empty"));
		world.Apply(empty);
		Assert.Single(world.Friends);
		// Unknown updates do not fabricate ids or friendships.
		var blankWorld = new BotWorldModel();
		blankWorld.Apply(decoder.Decode(typeof(SM_FRIEND_UPDATE), Body(w => WriteFriend(w, online: true))));
		Assert.Empty(blankWorld.Friends);
	}

	[Theory]
	[InlineData(typeof(SM_FRIEND_RESPONSE), "playerName")]
	[InlineData(typeof(SM_BLOCK_RESPONSE), "playerName")]
	[InlineData(typeof(SM_FRIEND_NOTIFY), "name")]
	public void SocialResponsesHaveExactStringAndCodeBoundaries(Type type, string nameField)
	{
		var packet = DecodeChecked(type, Body(w => { S(w, "Friend"); w.Write((byte)2); }));
		Assert.Equal("Friend", packet.Get<string>(nameField)); Assert.Equal((byte)2, packet.Get<byte>("code"));
	}

	private static byte[] FriendList() => Body(w =>
	{
		w.Write((short)-1); w.Write((byte)0); w.Write(101); WriteFriend(w, online: false);
		w.Write(123); w.Write((byte)2); S(w, "Private memo");
	});
	private static void WriteFriend(BinaryWriter w, bool online)
	{
		S(w, "Friend"); w.Write(10); w.Write(3); w.Write((byte)1); w.Write(210010000);
		w.Write(online ? 0 : 12345); S(w, "Note"); w.Write((byte)(online ? 1 : 0));
	}

	[Theory]
	[InlineData((byte)0)]
	[InlineData((byte)1)]
	[InlineData((byte)3)]
	[InlineData((byte)5)]
	[InlineData((byte)7)]
	[InlineData((byte)13)]
	[InlineData((byte)65)]
	public void GroupMemberBranchesHaveExactBoundaries(byte action)
	{
		var packet = DecodeChecked(typeof(SM_GROUP_MEMBER_INFO), GroupMember(action));
		Assert.Equal(42, packet.Get<int>("groupId"));
		Assert.Equal(101, packet.Get<int>("objectId"));
		Assert.Equal(600, packet.Get<int>("maxHp"));
		Assert.Equal(500, packet.Get<int>("currentHp"));
		Assert.Equal(110010000, packet.Get<int>("mapId"));
		Assert.Equal(110010002, packet.Get<int>("instanceMapId"));
		Assert.Equal(1.25f, packet.Get<float>("x"));
		Assert.Equal((byte)10, packet.Get<byte>("level"));
		Assert.Equal(action, packet.Get<byte>("event"));
		Assert.True(packet.Get<bool>("mentor"));
		if (action is 5 or 7 or 13) Assert.Equal("Member", packet.Get<string>("name"));
		else Assert.False(packet.Fields.ContainsKey("name"));
		if (action is 13 or 65)
		{
			Assert.Equal((byte)127, packet.Get<byte>("effectSlots"));
			var effect = Assert.Single(packet.Get<List<IReadOnlyDictionary<string, object?>>>("effects"));
			Assert.Equal(102, effect["effectorId"]); Assert.Equal((ushort)1282, effect["skillId"]);
			Assert.Equal((byte)3, effect["level"]); Assert.Equal((byte)7, effect["slotOrdinal"]);
			Assert.Equal(3500, effect["remainingMillis"]);
		}
	}

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(7)]
	public void LegionAnnouncementsStopAtEmptyStringOrSevenEntries(int count)
	{
		var packet = DecodeChecked(typeof(SM_LEGION_INFO), LegionInfo(count));
		Assert.Equal("Social", packet.Get<string>("name"));
		Assert.Equal((byte)1, packet.Get<byte>("level"));
		Assert.Equal(5_000_000_000L, packet.Get<long>("contribution"));
		Assert.Equal((ushort)444, packet.Get<ushort>("volunteerPermission"));
		Assert.Equal(14, packet.Get<int>("currentDominion"));
		var announcements = packet.Get<List<IReadOnlyDictionary<string, object?>>>("announcements");
		Assert.Equal(count, announcements.Count);
		for (int i = 0; i < count; i++)
		{
			Assert.Equal($"Notice {i}", announcements[i]["message"]);
			Assert.Equal(123456 + i, announcements[i]["timestamp"]);
		}
	}

	[Fact]
	public void LegionAddMemberCarriesIdentityAndRank()
	{
		var packet = DecodeChecked(typeof(SM_LEGION_ADD_MEMBER), LegionAdd());
		Assert.Equal(102, packet.Get<int>("objectId"));
		Assert.Equal("Newmember", packet.Get<string>("name"));
		Assert.Equal((byte)4, packet.Get<byte>("rank"));
		Assert.False(packet.Get<bool>("isMember"));
		Assert.Equal((byte)3, packet.Get<byte>("playerClass"));
		Assert.Equal((byte)10, packet.Get<byte>("level"));
		Assert.Equal(110010000, packet.Get<int>("mapId"));
		Assert.Equal(1, packet.Get<int>("serverId"));
		Assert.Equal(1300260, packet.Get<int>("messageId"));
		Assert.Equal("Welcome", packet.Get<string>("text"));
	}

	[Theory]
	[InlineData((short)-1)]
	[InlineData((short)0)]
	[InlineData((short)1)]
	public void LegionListPreservesSignedChunkBoundary(short count)
	{
		var packet = DecodeChecked(typeof(SM_LEGION_MEMBERLIST), LegionList(count));
		Assert.True(packet.Get<bool>("first"));
		Assert.Equal(count <= 0, packet.Get<bool>("last"));
		var members = packet.Get<List<IReadOnlyDictionary<string, object?>>>("members");
		Assert.Equal(Math.Abs(count), members.Count);
		if (count == 0) return;
		var member = Assert.Single(members);
		Assert.Equal(101, member["objectId"]); Assert.Equal("Founder", member["name"]);
		Assert.Equal(10, member["level"]); Assert.Equal((byte)0, member["rank"]);
		Assert.Equal(true, member["online"]); Assert.Equal("Intro", member["selfIntro"]);
		Assert.Equal("Nick", member["nickname"]); Assert.Equal(8, member["houseAddress"]);
		Assert.Equal(9, member["houseDoor"]); Assert.Equal(1, member["serverId"]);
	}

	[Fact]
	public void SocialModelTracksMembershipChangesAndClearsTransientDuelAndGroupState()
	{
		var world = new BotWorldModel();
		world.Apply(new DecodedBotServerPacket(typeof(SM_GROUP_INFO), new Dictionary<string, object?> { ["groupId"] = 42, ["leaderId"] = 100, ["lootRules"] = new int[8] }));
		world.Apply(decoder.Decode(typeof(SM_GROUP_MEMBER_INFO), GroupMember(13)));
		Assert.Equal(42, world.GroupId); Assert.Equal(100, world.GroupLeaderId);
		Assert.Equal("Member", Assert.Single(world.GroupMembers).Value.Name);
		world.Apply(decoder.Decode(typeof(SM_GROUP_MEMBER_INFO), GroupMember(1)));
		Assert.Equal("Member", world.GroupMembers[101].Name);
		world.Apply(decoder.Decode(typeof(SM_GROUP_MEMBER_INFO), GroupMember(3)));
		Assert.False(world.GroupMembers[101].Online);
		world.Apply(decoder.Decode(typeof(SM_GROUP_MEMBER_INFO), GroupMember(0)));
		Assert.Empty(world.GroupMembers);
		world.Apply(decoder.Decode(typeof(SM_GROUP_MEMBER_INFO), GroupMember(5)));
		world.Apply(DecodeChecked(typeof(SM_LEAVE_GROUP_MEMBER), Convert.FromHexString("00000000003F000000000000000000")));
		Assert.Null(world.GroupId); Assert.Null(world.GroupLeaderId); Assert.Empty(world.GroupMembers);
		world.Apply(decoder.Decode(typeof(SM_LEGION_INFO), LegionInfo(0)));
		world.Apply(decoder.Decode(typeof(SM_LEGION_MEMBERLIST), LegionList(-1)));
		world.Apply(decoder.Decode(typeof(SM_LEGION_ADD_MEMBER), LegionAdd()));
		Assert.Equal("Social", world.LegionName); Assert.Equal(2, world.LegionMembers.Count);
		Assert.Equal((byte)0, world.LegionMembers[101].Rank); Assert.Equal((byte)4, world.LegionMembers[102].Rank);
		world.Apply(decoder.Decode(typeof(SM_LEGION_MEMBERLIST), LegionList(0)));
		Assert.Empty(world.LegionMembers);
		world.Apply(DecodeChecked(typeof(SM_DUEL), [0, 102, 0, 0, 0]));
		Assert.Equal(102, world.DuelOpponentId); Assert.Null(world.LastDuelResult);
		world.Apply(DecodeChecked(typeof(SM_DUEL), Body(w => { w.Write((byte)1); w.Write((byte)2); w.Write(1300098); S(w, "Opponent"); })));
		Assert.Null(world.DuelOpponentId); Assert.Equal(new BotDuelResult(2, 1300098, "Opponent"), world.LastDuelResult);
		DecodeChecked(typeof(SM_DUEL), [0xe0]);
		Assert.Throws<InvalidDataException>(() => decoder.Decode(typeof(SM_DUEL), [0xff]));
	}

	private DecodedBotServerPacket DecodeChecked(Type type, byte[] body)
	{
		var packet = decoder.Decode(type, body);
		Assert.Throws<InvalidDataException>(() => decoder.Decode(type, body[..^1]));
		Assert.Throws<InvalidDataException>(() => decoder.Decode(type, [.. body, 0]));
		return packet;
	}
	private static byte[] GroupMember(byte action) => Body(w =>
	{
		w.Write(42); w.Write(101); w.Write(600); w.Write(500); w.Write(400); w.Write(300); w.Write(200); w.Write(100);
		w.Write(0); w.Write(110010000); w.Write(110010002); w.Write(1.25f); w.Write(2.5f); w.Write(3.75f);
		w.Write((byte)3); w.Write((byte)0); w.Write((byte)10); w.Write(action); w.Write((byte)1); w.Write((byte)0); w.Write((byte)1);
		if (action is 5 or 7 or 13) S(w, "Member");
		if (action is 13 or 65)
		{
			w.Write(0L); w.Write((byte)127); w.Write((ushort)1);
			w.Write(102); w.Write((ushort)1282); w.Write((byte)3); w.Write((byte)7); w.Write(3500); w.Write(new byte[32]);
		}
	});
	private static byte[] LegionInfo(int count) => Body(w =>
	{
		S(w, "Social"); w.Write((byte)1); w.Write(123); w.Write((ushort)111); w.Write((ushort)222);
		w.Write((ushort)333); w.Write((ushort)444); w.Write(5_000_000_000L); w.Write(0L);
		w.Write(11); w.Write(12); w.Write(13); w.Write(14);
		for (int i = 0; i < count; i++) { S(w, $"Notice {i}"); w.Write(123456 + i); }
		if (count < 7) S(w, "");
	});
	private static byte[] LegionAdd() => Body(w =>
	{
		w.Write(102); S(w, "Newmember"); w.Write((byte)4); w.Write((byte)0); w.Write((byte)3); w.Write((byte)10);
		w.Write(110010000); w.Write(1); w.Write(1300260); S(w, "Welcome");
	});
	private static byte[] LegionList(short count) => Body(w =>
	{
		w.Write((byte)1); w.Write(count);
		for (int i = 0; i < Math.Abs(count); i++)
		{
			w.Write(101); S(w, "Founder"); w.Write((byte)3); w.Write(10); w.Write((byte)0); w.Write(110010000); w.Write((byte)1);
			S(w, "Intro"); S(w, "Nick"); w.Write(0); w.Write(8); w.Write(9); w.Write(1);
		}
	});
	private static byte[] Body(Action<BinaryWriter> write)
	{
		using var stream = new MemoryStream();
		using (var writer = new BinaryWriter(stream, Encoding.Unicode, leaveOpen: true)) write(writer);
		return stream.ToArray();
	}
	private static void S(BinaryWriter writer, string value) { writer.Write(Encoding.Unicode.GetBytes(value)); writer.Write((ushort)0); }
}
