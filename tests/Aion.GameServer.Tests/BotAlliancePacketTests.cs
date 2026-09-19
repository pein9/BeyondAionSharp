using System.Text;
using Aion.Bots.Api;
using Aion.Bots.Protocol;
using Aion.Bots.World;
using Aion.GameServer.Network.Aion.ClientPackets;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Tests;

public sealed class BotAlliancePacketTests
{
	private readonly BotServerPacketDecoder decoder = new();

	// Audited against SM_ALLIANCE_INFO / SM_ALLIANCE_MEMBER_INFO at Java ce54b7931.
	// Hand-built wire contracts, not Java-generated fixtures or Java execution.
	public static bool AssertAuditedWireContract(Type type)
	{
		var tests = new BotAlliancePacketTests();
		if (type == typeof(SM_ALLIANCE_INFO))
		{
			tests.AllianceInfoHandlesOptionalLeague(false); tests.AllianceInfoHandlesOptionalLeague(true);
		}
		else if (type == typeof(SM_ALLIANCE_MEMBER_INFO))
		{
			foreach (byte action in new byte[] { 0, 1, 3, 5, 7, 13, 65 })
				foreach (bool online in new[] { false, true }) tests.MemberEventsHaveExactBoundaries(action, online);
			tests.GroupChangeSharesJoinIdButHasNoEffectsBlock();
		}
		else return false;
		return true;
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public void AllianceInfoHandlesOptionalLeague(bool league)
	{
		var p = Checked(typeof(SM_ALLIANCE_INFO), Info(league));
		Assert.Equal((ushort)1, p.Get<ushort>("groupSize")); Assert.Equal(42, p.Get<int>("allianceId"));
		Assert.Equal(101, p.Get<int>("leaderId")); Assert.Equal(210010000, p.Get<int>("mapId"));
		Assert.Equal(new[] { 102, 0, 0, 0 }, p.Get<int[]>("viceCaptains"));
		Assert.Equal(Enumerable.Range(0, 8), p.Get<int[]>("lootRules"));
		var groups = p.Get<List<IReadOnlyDictionary<string, object?>>>("groups");
		Assert.Equal(4, groups.Count);
		for (int i = 0; i < groups.Count; i++) { Assert.Equal(i, groups[i]["number"]); Assert.Equal(1000 + i, groups[i]["groupId"]); }
		Assert.Equal(league ? 99 : 0, p.Get<int>("leagueId"));
		Assert.Equal(league ? 1400560 : 0, p.Get<int>("messageId")); Assert.Equal(league ? "Captain" : "", p.Get<string>("message"));
		var alliances = p.Get<List<IReadOnlyDictionary<string, object?>>>("alliances");
		Assert.Equal(league ? 2 : 0, alliances.Count);
		if (league)
		{
			Assert.Equal(Enumerable.Range(10, 8), p.Get<int[]>("leagueLootRules"));
			for (int i = 0; i < 2; i++)
			{
				Assert.Equal(42 + i, alliances[i]["allianceId"]); Assert.Equal(i, alliances[i]["position"]);
				Assert.Equal(4, alliances[i]["memberCount"]); Assert.Equal($"Captain{i}", alliances[i]["captainName"]);
				Assert.Equal(210010000, alliances[i]["mapId"]);
			}
		}
	}

	[Theory]
	[InlineData((byte)0, false)] [InlineData((byte)1, true)] [InlineData((byte)3, false)]
	[InlineData((byte)5, false)] [InlineData((byte)5, true)] [InlineData((byte)7, false)]
	[InlineData((byte)13, false)] [InlineData((byte)13, true)] [InlineData((byte)65, true)]
	public void MemberEventsHaveExactBoundaries(byte action, bool online)
	{
		var p = Checked(typeof(SM_ALLIANCE_MEMBER_INFO), Member(action, online));
		Assert.Equal(1000, p.Get<int>("allianceGroupId")); Assert.Equal(101, p.Get<int>("objectId"));
		Assert.Equal(online ? 600 : 0, p.Get<int>("maxHp")); Assert.Equal(online ? 500 : 0, p.Get<int>("currentHp"));
		Assert.Equal(210010001, p.Get<int>("instanceMapId")); Assert.Equal(1.25f, p.Get<float>("x"));
		Assert.Equal(action, p.Get<byte>("event"));
		if (action is 5 or 7 or 13)
		{
			Assert.Equal("Member", p.Get<string>("name")); Assert.Equal(online, p.Get<bool>("online"));
			Assert.False(p.Get<bool>("groupChange"));
		}
		if (action == 65 || (online && action is 5 or 7 or 13))
		{
			var effect = Assert.Single(p.Get<List<IReadOnlyDictionary<string, object?>>>("effects"));
			Assert.Equal(102, effect["effectorId"]); Assert.Equal((ushort)1282, effect["skillId"]);
			Assert.Equal((byte)3, effect["level"]); Assert.Equal((byte)7, effect["slotOrdinal"]);
			Assert.Equal(3500, effect["remainingMillis"]);
		}
	}

	[Fact]
	public void GroupChangeSharesJoinIdButHasNoEffectsBlock()
	{
		var p = Checked(typeof(SM_ALLIANCE_MEMBER_INFO), Member(5, false, groupChange: true));
		Assert.True(p.Get<bool>("groupChange")); Assert.False(p.Fields.ContainsKey("online"));
		Assert.Equal("Member", p.Get<string>("name"));
	}

	[Fact]
	public void ClientModelTracksAllianceLeagueAndCleanup()
	{
		var world = new BotWorldModel();
		world.Apply(decoder.Decode(typeof(SM_ALLIANCE_INFO), Info(false)));
		world.Apply(decoder.Decode(typeof(SM_ALLIANCE_MEMBER_INFO), Member(5, true)));
		Assert.Equal(42, world.AllianceId); Assert.Equal(101, world.AllianceLeaderId);
		Assert.Equal(new[] { 102 }, world.AllianceViceCaptains); Assert.Null(world.LeagueId);
		Assert.True(Assert.Single(world.AllianceMembers).Value.Online);
		world.Apply(decoder.Decode(typeof(SM_ALLIANCE_MEMBER_INFO), Member(3, false)));
		Assert.False(world.AllianceMembers[101].Online);
		world.Apply(decoder.Decode(typeof(SM_ALLIANCE_MEMBER_INFO), Member(5, false, groupChange: true)));
		Assert.False(world.AllianceMembers[101].Online); // A slot move must not reconnect an offline member.
		world.Apply(decoder.Decode(typeof(SM_ALLIANCE_MEMBER_INFO), Member(13, true)));
		Assert.True(world.AllianceMembers[101].Online);
		world.Apply(decoder.Decode(typeof(SM_ALLIANCE_INFO), Info(true)));
		Assert.Equal(99, world.LeagueId); Assert.Equal(2, world.LeagueAlliances.Count);
		Assert.Equal(new BotLeagueAlliance(42, 0, 4, "Captain0", 210010000), world.LeagueAlliances[42]);
		world.Apply(decoder.Decode(typeof(SM_ALLIANCE_INFO), Info(false)));
		Assert.Null(world.LeagueId); Assert.Empty(world.LeagueAlliances); Assert.Single(world.AllianceMembers);
		world.Apply(decoder.Decode(typeof(SM_ALLIANCE_MEMBER_INFO), Member(0, true)));
		Assert.Empty(world.AllianceMembers);
		world.Apply(decoder.Decode(typeof(SM_ALLIANCE_MEMBER_INFO), Member(13, true)));
		world.Apply(decoder.Decode(typeof(SM_LEAVE_GROUP_MEMBER), Convert.FromHexString("00000000003F000000000000000000")));
		Assert.Null(world.AllianceId); Assert.Null(world.AllianceLeaderId); Assert.Empty(world.AllianceMembers);
		Assert.Empty(world.AllianceViceCaptains); Assert.Null(world.LeagueId); Assert.Empty(world.LeagueAlliances);
	}

	[Fact]
	public void ApiUsesAllianceAndLeagueClientCommands()
	{
		var api = new BotApi();
		Assert.Equal(GameClientPackets.InviteToGroup(12, "Captain").Body, api.InviteToAlliance("Captain").Body);
		Assert.Equal(GameClientPackets.InviteToGroup(28, "Captain").Body, api.InviteToLeague("Captain").Body);
		foreach (var (packet, code, selected) in new[] { (api.SetAllianceLeader(101), 17, 101), (api.LeaveAlliance(), 14, 0), (api.LeaveLeague(), 29, 0) })
		{
			Assert.Equal(typeof(CM_PLAYER_STATUS_INFO), packet.PacketType);
			Assert.Equal(GameClientPackets.TeamCommand((byte)code, selected).Body, packet.Body);
		}
	}

	private DecodedBotServerPacket Checked(Type type, byte[] body)
	{
		var result = decoder.Decode(type, body);
		Assert.Throws<InvalidDataException>(() => decoder.Decode(type, body[..^1]));
		Assert.Throws<InvalidDataException>(() => decoder.Decode(type, [.. body, 0]));
		return result;
	}
	private static byte[] Info(bool league) => Body(w =>
	{
		w.Write((ushort)1); w.Write(42); w.Write(101); w.Write(210010000); w.Write(102); w.Write(new byte[12]);
		foreach (int rule in Enumerable.Range(0, 8)) w.Write(rule);
		w.Write(2); w.Write((byte)0); w.Write(1); w.Write(0); w.Write(league ? 99 : 0);
		for (int i = 0; i < 4; i++) { w.Write(i); w.Write(1000 + i); }
		w.Write(league ? 1400560 : 0); S(w, league ? "Captain" : "");
		if (!league) return;
		w.Write((ushort)2);
		foreach (int rule in Enumerable.Range(10, 8)) w.Write(rule);
		w.Write(2);
		for (int i = 0; i < 2; i++) { w.Write(i); w.Write(42 + i); w.Write(4); S(w, $"Captain{i}"); w.Write(210010000); }
	});
	private static byte[] Member(byte action, bool online, bool groupChange = false) => Body(w =>
	{
		w.Write(1000); w.Write(101);
		foreach (int stat in new[] { 600, 500, 400, 300, 200, 100 }) w.Write(online ? stat : 0);
		w.Write(0); w.Write(210010000); w.Write(210010001); w.Write(1.25f); w.Write(2.5f); w.Write(3.75f);
		w.Write((byte)3); w.Write((byte)0); w.Write((byte)10); w.Write(action); w.Write((byte)1); w.Write((byte)0); w.Write((byte)0);
		if (action is 5 or 7 or 13) S(w, "Member");
		if (groupChange || action is 0 or 1 or 3) return;
		w.Write(0L);
		if (!online && action != 65) { w.Write((ushort)0); return; }
		w.Write((byte)127); w.Write((ushort)1);
		w.Write(102); w.Write((ushort)1282); w.Write((byte)3); w.Write((byte)7); w.Write(3500); w.Write(new byte[32]);
	});
	private static byte[] Body(Action<BinaryWriter> write)
	{
		using var stream = new MemoryStream();
		using (var writer = new BinaryWriter(stream, Encoding.Unicode, leaveOpen: true)) write(writer);
		return stream.ToArray();
	}
	private static void S(BinaryWriter writer, string text) { writer.Write(Encoding.Unicode.GetBytes(text)); writer.Write((ushort)0); }
}
