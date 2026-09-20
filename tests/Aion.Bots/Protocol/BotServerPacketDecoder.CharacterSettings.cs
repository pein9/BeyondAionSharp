namespace Aion.Bots.Protocol;

public sealed record BotMacro(byte Id, string Xml);
public sealed record BotTitle(int Id, int SecondsUntilExpiration);
public sealed record BotCharacterAppearance(int Voice, int SkinRgb, int HairRgb, int EyeRgb, int LipRgb, string FeaturesHex, float Height);

public sealed partial class BotServerPacketDecoder
{
	private static IReadOnlyDictionary<string, object?> DecodeReconnectKey(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		byte reserved = r.ReadByte(); int key = r.ReadInt32();
		if (reserved != 0 || r.Remaining != 0) throw new InvalidDataException("Invalid reconnect key response.");
		return Fields(("key", key));
	}

	private static IReadOnlyDictionary<string, object?> DecodeCharacterSelect(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		byte type = r.ReadByte();
		var fields = Fields(("type", type));
		if (type == 2)
		{
			short messageType = r.ReadInt16(); byte wrong = r.ReadByte();
			int wrongCount = r.ReadInt32(), maxWrongCount = r.ReadInt32();
			if (messageType is not (0 or 2 or 3) || wrongCount < 0 || maxWrongCount <= 0 || wrong != (wrongCount > 0 ? 1 : 0))
				throw new InvalidDataException("Invalid character-passkey result.");
			fields["messageType"] = messageType;
			fields["wrongCount"] = wrongCount;
			fields["maxWrongCount"] = maxWrongCount;
			fields["accepted"] = wrong == 0;
		}
		else if (type is not (0 or 1)) throw new InvalidDataException("Unknown character-passkey window.");
		if (r.Remaining != 0) throw new InvalidDataException("Character-passkey window has trailing bytes.");
		return fields;
	}

	private static IReadOnlyDictionary<string, object?> DecodeQuitResponse(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		int mode = r.ReadInt32(); byte reserved = r.ReadByte(); int sentinel = r.ReadInt32();
		if (mode is not (1 or 2) || reserved != 0 || sentinel != -1 || r.Remaining != 0)
			throw new InvalidDataException("Invalid quit response.");
		return Fields(("mode", mode), ("editMode", mode == 2));
	}

	private static IReadOnlyDictionary<string, object?> DecodeTitleInfo(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		byte action = r.ReadByte();
		var fields = Fields(("action", action));
		switch (action)
		{
			case 0:
				fields["reserved"] = r.ReadByte();
				var titles = new BotTitle[r.ReadUInt16()];
				for (int i = 0; i < titles.Length; i++) titles[i] = new(r.ReadInt32(), r.ReadInt32());
				fields["titles"] = titles;
				break;
			case 1:
			case 4:
				fields["titleId"] = r.ReadUInt16();
				break;
			case 3:
			case 5:
				fields["playerObjId"] = r.ReadInt32();
				fields["titleId"] = r.ReadUInt16();
				break;
			case 6:
				fields["bonusTitleId"] = r.ReadUInt16();
				break;
			default: throw new InvalidDataException($"Unsupported title-info action {action}.");
		}
		if (r.Remaining != 0) throw new InvalidDataException("Title information has unexpected trailing bytes.");
		return fields;
	}

	private static IReadOnlyDictionary<string, object?> DecodeMacroList(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		int playerObjectId = r.ReadInt32(); byte clear = r.ReadByte(); int count = -r.ReadInt16();
		if (clear > 1 || count < 0 || count > r.Remaining / 3) throw new InvalidDataException("Invalid macro-list flags or count.");
		var macros = new BotMacro[count];
		for (int i = 0; i < macros.Length; i++) macros[i] = new(r.ReadByte(), r.ReadString());
		if (r.Remaining != 0) throw new InvalidDataException("Macro list has unexpected trailing bytes.");
		return Fields(("playerObjectId", playerObjectId), ("clearList", clear == 1), ("macros", macros));
	}

	private static IReadOnlyDictionary<string, object?> DecodeMacroResult(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		var fields = Fields(("code", r.ReadByte()));
		if (r.Remaining != 0) throw new InvalidDataException("Macro result has unexpected trailing bytes.");
		return fields;
	}

	private static IReadOnlyDictionary<string, object?> DecodeUiSettings(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		byte type = r.ReadByte(); ushort size = r.ReadUInt16();
		if (size != 0x1C00 || r.Remaining < size) throw new InvalidDataException("Invalid UI-settings size or truncated padding.");
		// The server pads short settings to 0x1C00 bytes but writes longer data unchanged with that same header.
		return Fields(("type", type), ("declaredSize", size), ("paddedData", r.ReadBytes(r.Remaining)));
	}

	private static IReadOnlyDictionary<string, object?> DecodePlasticSurgery(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		int playerObjId = r.ReadInt32(); byte ticket = r.ReadByte(), genderSwitch = r.ReadByte();
		if (ticket is not (1 or 2) || genderSwitch > 1 || r.Remaining != 0) throw new InvalidDataException("Invalid plastic-surgery result.");
		return Fields(("playerObjId", playerObjId), ("hasTicket", ticket == 1), ("isGenderSwitch", genderSwitch == 1));
	}
}
