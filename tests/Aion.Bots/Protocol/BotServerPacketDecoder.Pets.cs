namespace Aion.Bots.Protocol;

public sealed record BotPetFunction(byte Id, byte[] Data);
public sealed record BotPetData(string Name, int TemplateId, int ObjectId, int MasterObjectId, int Birthday,
	int SecondsUntilExpiration, BotPetFunction[] Functions, int Decoration);

public sealed partial class BotServerPacketDecoder
{
	// SM_PET.writeImpl/writePetData at ce54b7931. Decode from bytes only; no static-data lookups.
	private static IReadOnlyDictionary<string, object?> DecodePet(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		ushort action = r.ReadUInt16(); var fields = Fields(("action", action));
		switch (action)
		{
			case 0:
				fields["reserved"] = r.ReadByte();
				int count = r.ReadUInt16();
				if (count > r.Remaining / 51) throw new InvalidDataException("Truncated pet list.");
				var pets = new BotPetData[count];
				for (int i = 0; i < count; i++) pets[i] = ReadPetData(ref r);
				fields["pets"] = pets;
				break;
			case 1: fields["pet"] = ReadPetData(ref r); break;
			case 2:
				fields["templateId"] = r.ReadInt32(); fields["petObjectId"] = r.ReadInt32(); r.Skip(8); break;
			case 3:
				fields["petName"] = r.ReadString(); fields["templateId"] = r.ReadInt32(); fields["petObjectId"] = r.ReadInt32();
				fields["x"] = r.ReadSingle(); fields["y"] = r.ReadSingle(); fields["z"] = r.ReadSingle();
				fields["targetX"] = r.ReadSingle(); fields["targetY"] = r.ReadSingle(); fields["targetZ"] = r.ReadSingle();
				fields["heading"] = r.ReadByte(); fields["masterObjectId"] = r.ReadInt32();
				fields["decoration"] = ReadPetAppearance(ref r); break;
			case 4: fields["petObjectId"] = r.ReadInt32(); fields["animationId"] = r.ReadByte(); break;
			case 9:
				if (r.ReadUInt16() != 1 || r.ReadByte() != 1) throw new InvalidDataException("Invalid pet feeding header.");
				byte feeding = r.ReadByte(); fields["subType"] = feeding;
				if (feeding is < 1 or > 8) throw new InvalidDataException("Unknown pet feeding result.");
				fields["feedProgress"] = r.ReadInt32(); fields["refeedSeconds"] = r.ReadInt32();
				if (feeding is 1 or 2 or 6 or 7 or 8) fields["itemObjectId"] = r.ReadInt32();
				if (feeding is 1 or 2 or 7 or 8) fields["count"] = r.ReadInt32();
				if (feeding is 2 or 6) fields["reserved"] = r.ReadByte();
				break;
			case 10: fields["petObjectId"] = r.ReadInt32(); fields["petName"] = r.ReadString(); break;
			case 12:
				byte mood = r.ReadByte(); fields["subType"] = mood;
				switch (mood)
				{
					case 0: fields["moodDelta"] = r.ReadInt32(); break;
					case 2:
						fields["reserved"] = r.ReadInt32(); fields["moodPoints"] = r.ReadInt32(); fields["emotionId"] = r.ReadInt32(); break;
					case 3: fields["rewardItemId"] = r.ReadInt32(); break;
					case 4:
						fields["moodPoints"] = r.ReadInt32(); fields["moodRemainingTime"] = r.ReadInt32(); fields["giftRemainingTime"] = r.ReadInt32(); break;
					default: throw new InvalidDataException("Unknown pet mood result.");
				}
				break;
			case 13:
				byte function = r.ReadByte(); fields["subType"] = function;
				byte operation = r.ReadByte();
				if (function == 2)
				{
					fields["dopeAction"] = operation;
					switch (operation)
					{
						case 0: fields["itemId"] = r.ReadInt32(); fields["slot"] = r.ReadInt32(); break;
						case 1: fields["slot"] = r.ReadInt32(); break;
						case 2: fields["slot"] = r.ReadInt32(); fields["itemId"] = r.ReadInt32(); break;
						case 3: fields["itemId"] = r.ReadInt32(); break;
						default: throw new InvalidDataException("Unknown pet doping result.");
					}
				}
				else if (function == 3 && operation is 1 or 2)
				{
					fields["active"] = operation == 1; fields["npcObjId"] = r.ReadInt32();
				}
				else if (function is 3 or 4 && operation == 0)
				{
					byte active = r.ReadByte(); if (active > 1) throw new InvalidDataException("Invalid pet function toggle.");
					fields["active"] = active == 1; fields["npcObjId"] = 0;
				}
				else throw new InvalidDataException("Unknown pet special function.");
				break;
			case 6: case 7: case 15: case 16: case 17: case 255: break; // Header-only Java constructors.
			default: throw new InvalidDataException($"Unknown pet action {action}.");
		}
		if (r.Remaining != 0) throw new InvalidDataException("Pet packet has trailing bytes.");
		return fields;
	}

	private static BotPetData ReadPetData(ref PacketBodyReader r)
	{
		string name = r.ReadString(); int template = r.ReadInt32(), id = r.ReadInt32(), master = r.ReadInt32();
		r.Skip(8); int birthday = r.ReadInt32(), expiry = r.ReadInt32();
		var functions = new BotPetFunction[2];
		for (int i = 0; i < functions.Length; i++)
		{
			byte function = r.ReadByte(), length = r.ReadByte();
			int expected = function switch { 0 or 6 => 0, 1 => 8, 2 => 32, 3 => 1, _ => -1 };
			if (length != expected) throw new InvalidDataException("Invalid pet function length.");
			functions[i] = new(function, r.ReadBytes(length));
		}
		return new(name, template, id, master, birthday, expiry, functions, ReadPetAppearance(ref r));
	}

	private static int ReadPetAppearance(ref PacketBodyReader r)
	{
		if (r.ReadUInt16() != 1) throw new InvalidDataException("Invalid pet appearance header.");
		r.Skip(3); int decoration = r.ReadInt32(); r.Skip(8); return decoration;
	}
}
