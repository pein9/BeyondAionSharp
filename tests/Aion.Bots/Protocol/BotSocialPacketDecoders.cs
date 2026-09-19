namespace Aion.Bots.Protocol;

public sealed partial class BotServerPacketDecoder
{
	private static IReadOnlyDictionary<string, object?> DecodeGroupLoot(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		var fields = Fields(("groupId", r.ReadInt32()), ("index", r.ReadInt32()), ("itemCount", r.ReadInt32()), ("itemId", r.ReadInt32()));
		r.Skip(3);
		fields["lootCorpseId"] = r.ReadInt32(); fields["distributionId"] = r.ReadByte();
		fields["playerId"] = r.ReadInt32(); fields["luck"] = r.ReadInt32(); // -1 marks distribution completion.
		RequireSocialEnd(r);
		return fields;
	}

	private static IReadOnlyDictionary<string, object?> DecodeAllianceInfo(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		var fields = Fields(("groupSize", r.ReadUInt16()), ("allianceId", r.ReadInt32()),
			("leaderId", r.ReadInt32()), ("mapId", r.ReadInt32()));
		var viceCaptains = new int[4];
		for (int i = 0; i < viceCaptains.Length; i++) viceCaptains[i] = r.ReadInt32();
		fields["viceCaptains"] = viceCaptains;
		fields["lootRules"] = ReadAllianceLootRules(ref r);
		r.Skip(5);
		fields["teamType"] = r.ReadInt32(); fields["teamSubType"] = r.ReadInt32(); fields["leagueId"] = r.ReadInt32();
		var groups = new List<IReadOnlyDictionary<string, object?>>(4);
		for (int i = 0; i < 4; i++) groups.Add(Fields(("number", r.ReadInt32()), ("groupId", r.ReadInt32())));
		fields["groups"] = groups;
		fields["messageId"] = r.ReadInt32(); fields["message"] = r.ReadString();
		var alliances = new List<IReadOnlyDictionary<string, object?>>();
		// No count field is written at all when the league data is absent.
		if (r.Remaining != 0)
		{
			int count = r.ReadUInt16();
			if (count == 0) throw new InvalidDataException("An alliance league block must contain members.");
			fields["leagueLootRules"] = ReadAllianceLootRules(ref r);
			r.Skip(4);
			for (int i = 0; i < count; i++)
				alliances.Add(Fields(("position", r.ReadInt32()), ("allianceId", r.ReadInt32()),
					("memberCount", r.ReadInt32()), ("captainName", r.ReadString()), ("mapId", r.ReadInt32())));
		}
		fields["alliances"] = alliances;
		RequireSocialEnd(r);
		return fields;
	}

	private static int[] ReadAllianceLootRules(ref PacketBodyReader r)
	{
		var rules = new int[8];
		for (int i = 0; i < rules.Length; i++) rules[i] = r.ReadInt32();
		return rules;
	}

	private static IReadOnlyDictionary<string, object?> DecodeAllianceMember(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		var fields = Fields(("allianceGroupId", r.ReadInt32()), ("objectId", r.ReadInt32()), ("maxHp", r.ReadInt32()),
			("currentHp", r.ReadInt32()), ("maxMp", r.ReadInt32()), ("currentMp", r.ReadInt32()),
			("maxFp", r.ReadInt32()), ("currentFp", r.ReadInt32()));
		r.Skip(4);
		fields["mapId"] = r.ReadInt32(); fields["instanceMapId"] = r.ReadInt32();
		fields["x"] = r.ReadSingle(); fields["y"] = r.ReadSingle(); fields["z"] = r.ReadSingle();
		fields["playerClass"] = r.ReadByte(); fields["gender"] = r.ReadByte(); fields["level"] = r.ReadByte();
		byte action = r.ReadByte(); fields["event"] = action;
		r.Skip(1); fields["flyState"] = r.ReadByte(); r.Skip(1);
		if (action is 5 or 7 or 13)
		{
			fields["name"] = r.ReadString();
			// JOIN and MEMBER_GROUP_CHANGE share wire id 5, but the latter has only the name.
			fields["groupChange"] = action == 5 && r.Remaining == 0;
			if (r.Remaining != 0 || action != 5)
			{
				r.Skip(8);
				if (r.Remaining == 2)
				{
					if (r.ReadUInt16() != 0) throw new InvalidDataException("Offline alliance effects must be empty.");
					fields["online"] = false;
				}
				else { ReadAllianceEffects(ref r, fields); fields["online"] = true; }
			}
		}
		else if (action == 65) { r.Skip(8); ReadAllianceEffects(ref r, fields); }
		else if (action is not (0 or 1 or 3)) throw new InvalidDataException($"Unknown alliance event {action}.");
		RequireSocialEnd(r);
		return fields;
	}

	private static void ReadAllianceEffects(ref PacketBodyReader r, Dictionary<string, object?> fields)
	{
		fields["effectSlots"] = r.ReadByte();
		int count = r.ReadUInt16();
		var effects = new List<IReadOnlyDictionary<string, object?>>(count);
		for (int i = 0; i < count; i++)
			effects.Add(Fields(("effectorId", r.ReadInt32()), ("skillId", r.ReadUInt16()), ("level", r.ReadByte()),
				("slotOrdinal", r.ReadByte()), ("remainingMillis", r.ReadInt32())));
		fields["effects"] = effects;
		r.Skip(32);
	}

	private static IReadOnlyDictionary<string, object?> DecodeSocialResponse(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		var fields = Fields(("playerName", r.ReadString()), ("code", r.ReadByte()));
		RequireSocialEnd(r);
		return fields;
	}

	private static IReadOnlyDictionary<string, object?> DecodeFriendNotify(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		var fields = Fields(("name", r.ReadString()), ("code", r.ReadByte()));
		RequireSocialEnd(r);
		return fields;
	}

	private static Dictionary<string, object?> ReadFriend(ref PacketBodyReader r) => Fields(
		("name", r.ReadString()), ("level", r.ReadInt32()), ("playerClass", r.ReadInt32()), ("gender", r.ReadByte()),
		("mapId", r.ReadInt32()), ("lastOnline", r.ReadInt32()), ("note", r.ReadString()), ("status", r.ReadByte()));

	private static IReadOnlyDictionary<string, object?> DecodeFriendList(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		int signedCount = r.ReadInt16();
		if (signedCount > 0) throw new InvalidDataException("Friend list requires a nonpositive count.");
		r.Skip(1);
		var friends = new List<IReadOnlyDictionary<string, object?>>(-signedCount);
		for (int i = 0; i < -signedCount; i++)
		{
			int id = r.ReadInt32();
			var friend = ReadFriend(ref r);
			friend["objectId"] = id; friend["houseAddress"] = r.ReadInt32();
			friend["houseDoor"] = r.ReadByte(); friend["memo"] = r.ReadString();
			friends.Add(friend);
		}
		RequireSocialEnd(r);
		return Fields(("friends", friends));
	}

	private static IReadOnlyDictionary<string, object?> DecodeFriendUpdate(ReadOnlySpan<byte> body)
	{
		// Java serializes an empty body if the friend vanished before packet serialization.
		if (body.IsEmpty) return Fields(("empty", true));
		var r = new PacketBodyReader(body);
		var fields = ReadFriend(ref r);
		fields["empty"] = false;
		RequireSocialEnd(r);
		return fields;
	}

	private static IReadOnlyDictionary<string, object?> DecodeBlockList(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		int signedCount = r.ReadInt16();
		if (signedCount > 0) throw new InvalidDataException("Block list requires a nonpositive count.");
		r.Skip(1);
		var blocks = new List<IReadOnlyDictionary<string, object?>>(-signedCount);
		for (int i = 0; i < -signedCount; i++) blocks.Add(Fields(("name", r.ReadString()), ("reason", r.ReadString())));
		RequireSocialEnd(r);
		return Fields(("blocks", blocks));
	}

	private static IReadOnlyDictionary<string, object?> DecodeAbyssRank(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		var fields = Fields(("ap", r.ReadInt64()), ("currentGp", r.ReadInt32()), ("rank", r.ReadInt32()),
			("rankingListPosition", r.ReadInt32()));
		r.Skip(4); // Removed experience percentage.
		fields["allKill"] = r.ReadInt32(); fields["maxRank"] = r.ReadInt32();
		foreach (string period in new[] { "daily", "weekly", "last" })
		{
			fields[period + "Kill"] = r.ReadInt32(); fields[period + "Ap"] = r.ReadInt64(); fields[period + "Gp"] = r.ReadInt32();
		}
		r.Skip(1);
		RequireSocialEnd(r);
		return fields;
	}

	private static IReadOnlyDictionary<string, object?> DecodeDuel(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		byte type = r.ReadByte();
		var fields = Fields(("type", type));
		switch (type)
		{
			case 0: fields["requesterObjId"] = r.ReadInt32(); break;
			case 1:
				fields["resultId"] = r.ReadByte(); fields["msgId"] = r.ReadInt32(); fields["playerName"] = r.ReadString(); break;
			case 0xe0: break;
			default: throw new InvalidDataException($"Unknown duel event {type}.");
		}
		RequireSocialEnd(r);
		return fields;
	}

	private static IReadOnlyDictionary<string, object?> DecodeLeaveGroup(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		var fields = Fields(("groupId", r.ReadInt32()), ("reserved", r.ReadByte()), ("teamType", r.ReadInt32()),
			("teamSubType", r.ReadInt32()), ("reserved2", r.ReadUInt16()));
		RequireSocialEnd(r);
		return fields;
	}

	private static IReadOnlyDictionary<string, object?> DecodeGroupMember(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		var fields = Fields(("groupId", r.ReadInt32()), ("objectId", r.ReadInt32()), ("maxHp", r.ReadInt32()),
			("currentHp", r.ReadInt32()), ("maxMp", r.ReadInt32()), ("currentMp", r.ReadInt32()),
			("maxFp", r.ReadInt32()), ("currentFp", r.ReadInt32()));
		r.Skip(4);
		fields["mapId"] = r.ReadInt32(); fields["instanceMapId"] = r.ReadInt32();
		fields["x"] = r.ReadSingle(); fields["y"] = r.ReadSingle(); fields["z"] = r.ReadSingle();
		fields["playerClass"] = r.ReadByte(); fields["gender"] = r.ReadByte(); fields["level"] = r.ReadByte();
		byte action = r.ReadByte(); fields["event"] = action;
		r.Skip(1); fields["flyState"] = r.ReadByte(); fields["mentor"] = r.ReadByte() != 0;
		if (action is 5 or 7 or 13) fields["name"] = r.ReadString();
		if (action is 13 or 65)
		{
			r.Skip(8); fields["effectSlots"] = r.ReadByte();
			int count = r.ReadUInt16();
			var effects = new List<IReadOnlyDictionary<string, object?>>(count);
			for (int i = 0; i < count; i++)
				effects.Add(Fields(("effectorId", r.ReadInt32()), ("skillId", r.ReadUInt16()), ("level", r.ReadByte()),
					("slotOrdinal", r.ReadByte()), ("remainingMillis", r.ReadInt32())));
			fields["effects"] = effects;
			r.Skip(8 * 4); // One D per Java SkillTargetSlot, including NONE.
		}
		else if (action is not (0 or 1 or 3 or 5 or 7)) throw new InvalidDataException($"Unknown group event {action}.");
		RequireSocialEnd(r);
		return fields;
	}

	private static IReadOnlyDictionary<string, object?> DecodeLegionInfo(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		var fields = Fields(("name", r.ReadString()), ("level", r.ReadByte()), ("ranking", r.ReadInt32()),
			("deputyPermission", r.ReadUInt16()), ("centurionPermission", r.ReadUInt16()),
			("legionaryPermission", r.ReadUInt16()), ("volunteerPermission", r.ReadUInt16()), ("contribution", r.ReadInt64()));
		r.Skip(8);
		fields["disbandTime"] = r.ReadInt32(); fields["occupiedDominion"] = r.ReadInt32();
		fields["lastDominion"] = r.ReadInt32(); fields["currentDominion"] = r.ReadInt32();
		var announcements = new List<IReadOnlyDictionary<string, object?>>();
		for (int i = 0; i < 7; i++)
		{
			string message = r.ReadString();
			if (message.Length == 0) break;
			announcements.Add(Fields(("message", message), ("timestamp", r.ReadInt32())));
		}
		fields["announcements"] = announcements;
		RequireSocialEnd(r);
		return fields;
	}

	private static IReadOnlyDictionary<string, object?> DecodeLegionAddMember(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		var fields = Fields(("objectId", r.ReadInt32()), ("name", r.ReadString()), ("rank", r.ReadByte()),
			("isMember", r.ReadByte() != 0), ("playerClass", r.ReadByte()), ("level", r.ReadByte()),
			("mapId", r.ReadInt32()), ("serverId", r.ReadInt32()), ("messageId", r.ReadInt32()), ("text", r.ReadString()));
		RequireSocialEnd(r);
		return fields;
	}

	private static IReadOnlyDictionary<string, object?> DecodeLegionMembers(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		bool first = r.ReadByte() != 0;
		int signedCount = r.ReadInt16();
		var members = new List<IReadOnlyDictionary<string, object?>>(Math.Abs(signedCount));
		for (int i = 0; i < Math.Abs(signedCount); i++)
			members.Add(Fields(("objectId", r.ReadInt32()), ("name", r.ReadString()), ("playerClass", r.ReadByte()),
				("level", r.ReadInt32()), ("rank", r.ReadByte()), ("mapId", r.ReadInt32()), ("online", r.ReadByte() != 0),
				("selfIntro", r.ReadString()), ("nickname", r.ReadString()), ("lastOnline", r.ReadInt32()),
				("houseAddress", r.ReadInt32()), ("houseDoor", r.ReadInt32()), ("serverId", r.ReadInt32())));
		RequireSocialEnd(r);
		return Fields(("first", first), ("last", signedCount <= 0), ("members", members));
	}

	private static void RequireSocialEnd(PacketBodyReader reader)
	{
		if (reader.Remaining != 0) throw new InvalidDataException($"Social packet has {reader.Remaining} trailing bytes.");
	}
}
