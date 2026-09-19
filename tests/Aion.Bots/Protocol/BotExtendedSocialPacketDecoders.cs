namespace Aion.Bots.Protocol;

public sealed partial class BotServerPacketDecoder
{
	private static IReadOnlyDictionary<string, object?> DecodeRecall(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		var fields = Fields(("closed", r.ReadByte() != 0), ("casterName", r.ReadString()),
			("skillId", r.ReadUInt16()), ("seconds", r.ReadUInt16()));
		RequireSocialEnd(r);
		return fields;
	}

	private static IReadOnlyDictionary<string, object?> DecodeLegionEmblem(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		var fields = Fields(("legionId", r.ReadInt32()), ("emblemId", r.ReadByte()), ("emblemType", r.ReadByte()),
			("alpha", r.ReadByte()), ("red", r.ReadByte()), ("green", r.ReadByte()), ("blue", r.ReadByte()));
		RequireSocialEnd(r);
		return fields;
	}

	private static IReadOnlyDictionary<string, object?> DecodeLegionEdit(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		byte type = r.ReadByte();
		var fields = Fields(("type", type));
		switch (type)
		{
			case 0: fields["level"] = r.ReadByte(); break;
			case 1: fields["ranking"] = r.ReadInt32(); break;
			case 2:
				fields["deputyPermission"] = r.ReadUInt16(); fields["centurionPermission"] = r.ReadUInt16();
				fields["legionaryPermission"] = r.ReadUInt16(); fields["volunteerPermission"] = r.ReadUInt16(); break;
			case 3: fields["contribution"] = r.ReadInt64(); break;
			case 4: fields["warehouseKinah"] = r.ReadInt64(); break;
			case 5: fields["announcement"] = r.ReadString(); fields["timestamp"] = r.ReadInt32(); break;
			case 6: fields["disbandTime"] = r.ReadInt32(); break;
			case 7: case 8: break;
			default: throw new InvalidDataException($"Unknown legion edit {type}.");
		}
		RequireSocialEnd(r);
		return fields;
	}

	private static IReadOnlyDictionary<string, object?> DecodeLegionHistory(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		var fields = Fields(("totalEntries", r.ReadInt32()), ("page", r.ReadInt32()));
		int count = r.ReadInt32();
		if (count is < 0 or > 8) throw new InvalidDataException($"Invalid legion history page size {count}.");
		var entries = new List<IReadOnlyDictionary<string, object?>>(count);
		for (int i = 0; i < count; i++)
		{
			var entry = Fields(("timestamp", r.ReadInt32()), ("action", r.ReadByte()));
			r.Skip(1);
			entry["name"] = ReadHistoryString(ref r); entry["description"] = ReadHistoryString(ref r);
			r.Skip(2);
			entries.Add(entry);
		}
		fields["entries"] = entries; fields["historyType"] = r.ReadUInt16();
		RequireSocialEnd(r);
		return fields;
	}

	private static string ReadHistoryString(ref PacketBodyReader r)
	{
		// WriteS(value, 32) writes 32 UTF-16 code units PLUS a terminator, even for empty strings.
		var bytes = r.ReadBytes(66);
		if (bytes[64] != 0 || bytes[65] != 0) throw new InvalidDataException("Unterminated legion history string.");
		var value = new PacketBodyReader(bytes);
		return value.ReadString();
	}

	private static IReadOnlyDictionary<string, object?> DecodeFindGroup(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		byte action = r.ReadByte();
		var fields = Fields(("action", action));
		var entries = new List<IReadOnlyDictionary<string, object?>>();
		fields["entries"] = entries;
		switch (action)
		{
			case 0: case 4: case 10: case 16:
				int count = r.ReadUInt16(); fields["totalEntries"] = r.ReadUInt16(); fields["lastUpdate"] = r.ReadInt32();
				for (int i = 0; i < count; i++)
				{
					if (action == 10) { entries.Add(ReadInstanceRecruitment(ref r)); continue; }
					if (action == 16)
					{
						r.Skip(4);
						var member = Fields(("mapId", r.ReadInt32()), ("objectId", r.ReadInt32()),
							("level", r.ReadInt32()), ("playerClass", r.ReadInt32()));
						r.Skip(4); member["name"] = r.ReadString(); entries.Add(member); continue;
					}
					var row = Fields(("objectId", r.ReadInt32()));
					if (action == 0)
					{
						row["serverId"] = r.ReadByte(); r.Skip(2); row["soloFlag"] = r.ReadByte();
					}
					row["groupType"] = r.ReadByte(); row["message"] = r.ReadString(); row["name"] = r.ReadString();
					if (action == 0)
					{
						row["size"] = r.ReadByte(); row["minLevel"] = r.ReadByte(); row["maxLevel"] = r.ReadByte();
					}
					else { row["playerClass"] = r.ReadByte(); row["level"] = r.ReadByte(); }
					row["lastUpdate"] = r.ReadInt32(); entries.Add(row);
				}
				break;
			case 1:
				fields["objectId"] = r.ReadInt32(); fields["serverId"] = r.ReadByte(); r.Skip(2);
				fields["soloFlag"] = r.ReadByte(); break;
			case 5: fields["objectId"] = r.ReadInt32(); break;
			case 11:
				fields["objectId"] = r.ReadInt32(); r.Skip(11); fields["playerClass"] = r.ReadByte();
				fields["level"] = r.ReadInt32(); fields["name"] = r.ReadString(); break;
			case 14:
				fields["packetNumber"] = r.ReadByte();
				while (r.Remaining != 0) entries.Add(ReadInstanceRecruitment(ref r));
				break;
			case 18: case 22: case 23: case 24:
				fields["groupId"] = r.ReadInt32(); fields["instanceMaskId"] = r.ReadInt32();
				if (action == 23) fields["showEnterMessage"] = r.ReadByte() != 0;
				if (action == 24)
				{
					int size = r.ReadByte();
					for (int i = 0; i < size; i++)
					{
						r.Skip(8);
						var member = Fields(("objectId", r.ReadInt32()), ("level", r.ReadInt32()), ("playerClass", r.ReadInt32()));
						r.Skip(2); member["ready"] = r.ReadByte() != 0; member["online"] = r.ReadByte() != 0;
						member["name"] = r.ReadString(); entries.Add(member);
					}
				}
				break;
			case 26:
				var masks = new int[r.ReadUInt16()];
				for (int i = 0; i < masks.Length; i++) masks[i] = r.ReadInt32();
				fields["instanceMaskIds"] = masks; break;
			default: throw new InvalidDataException($"Unknown find-group action {action}.");
		}
		RequireSocialEnd(r);
		return fields;
	}

	private static Dictionary<string, object?> ReadInstanceRecruitment(ref PacketBodyReader r)
	{
		var row = Fields(("groupId", r.ReadInt32()), ("instanceMaskId", r.ReadInt32()));
		r.Skip(4); row["size"] = r.ReadByte(); row["minMembers"] = r.ReadByte(); r.Skip(2);
		row["recruiterId"] = r.ReadInt32();
		// Actions 10 and 14 use different reserved values but the same eight-byte span.
		r.Skip(8); row["minLevel"] = r.ReadByte(); row["maxLevel"] = r.ReadByte(); r.Skip(2);
		row["lastUpdate"] = r.ReadInt32(); r.Skip(4); row["name"] = r.ReadString(); row["message"] = r.ReadString();
		return row;
	}
}
