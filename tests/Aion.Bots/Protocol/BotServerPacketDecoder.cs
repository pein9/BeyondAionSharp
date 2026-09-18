using System.Collections.Frozen;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.Bots.Protocol;

/// <summary>Partial, bot-facing decoders for the server packets an honest client must perceive.</summary>
public sealed class BotServerPacketDecoder
{
	private delegate IReadOnlyDictionary<string, object?> DecodeBody(ReadOnlySpan<byte> body);

	private static readonly FrozenDictionary<Type, DecodeBody> Decoders =
		new Dictionary<Type, DecodeBody>
		{
			[typeof(SM_KEY)] = DecodeKey,
			[typeof(SM_VERSION_CHECK)] = DecodeVersionCheck,
			[typeof(SM_L2AUTH_LOGIN_CHECK)] = DecodeLoginCheck,
			[typeof(SM_CHARACTER_LIST)] = DecodeCharacterList,
			[typeof(SM_CREATE_CHARACTER)] = DecodeCreateCharacter,
			[typeof(SM_ENTER_WORLD_CHECK)] = DecodeEnterWorldCheck,
			[typeof(SM_PLAYER_SPAWN)] = DecodePlayerSpawn,
			[typeof(SM_PLAY_MOVIE)] = DecodePlayMovie,
			[typeof(SM_CHAT_INIT)] = DecodeChatInit,
			[typeof(SM_PLAYER_INFO)] = DecodePlayerInfo,
			[typeof(SM_EMOTION)] = DecodeEmotion,
			[typeof(SM_NPC_INFO)] = DecodeNpcInfo,
			[typeof(SM_GATHERABLE_INFO)] = DecodeGatherableInfo,
			[typeof(SM_MOVE)] = DecodeMove,
			[typeof(SM_DELETE)] = DecodeDelete,
			[typeof(SM_TELEPORT_LOC)] = DecodeTeleport,
			[typeof(SM_DIALOG_WINDOW)] = DecodeDialog,
			[typeof(SM_QUESTION_WINDOW)] = DecodeQuestion,
			[typeof(SM_CLOSE_QUESTION_WINDOW)] = DecodeCloseQuestion,
			[typeof(SM_MESSAGE)] = DecodeMessage,
			[typeof(SM_SYSTEM_MESSAGE)] = DecodeSystemMessage,
			[typeof(SM_STATS_INFO)] = DecodeStats,
			[typeof(SM_STATUPDATE_HP)] = DecodeHp,
			[typeof(SM_STATUPDATE_MP)] = DecodeMp,
			[typeof(SM_STATUPDATE_DP)] = DecodeDp,
			[typeof(SM_STATUPDATE_EXP)] = DecodeExp,
			[typeof(SM_FLY_TIME)] = DecodeFlyTime,
			[typeof(SM_ABNORMAL_STATE)] = DecodeAbnormalState,
			[typeof(SM_WINDSTREAM)] = DecodeWindstream,
			[typeof(SM_WINDSTREAM_ANNOUNCE)] = DecodeWindstreamAnnounce,
			[typeof(SM_DIE)] = DecodeDie,
			[typeof(SM_INVENTORY_INFO)] = DecodeInventoryInfo,
			[typeof(SM_INVENTORY_ADD_ITEM)] = DecodeInventoryAdd,
			[typeof(SM_INVENTORY_UPDATE_ITEM)] = DecodeInventoryUpdate,
			[typeof(SM_DELETE_ITEM)] = DecodeDeleteItem,
			[typeof(SM_CUBE_UPDATE)] = DecodeCubeUpdate,
			[typeof(SM_SKILL_LIST)] = DecodeSkillList,
			[typeof(SM_SKILL_COOLDOWN)] = DecodeSkillCooldown,
			[typeof(SM_CASTSPELL)] = DecodeCastSpell,
			[typeof(SM_CASTSPELL_RESULT)] = DecodeCastSpellResult,
			[typeof(SM_SKILL_CANCEL)] = DecodeSkillCancel,
			[typeof(SM_QUEST_LIST)] = DecodeQuestList,
			[typeof(SM_QUEST_ACTION)] = DecodeQuestAction,
			[typeof(SM_QUEST_COMPLETED_LIST)] = DecodeQuestCompletedList,
			[typeof(SM_LOOT_STATUS)] = DecodeLootStatus,
			[typeof(SM_LOOT_ITEMLIST)] = DecodeLootItemList,
			[typeof(SM_TRADELIST)] = DecodeTradeList,
			[typeof(SM_GROUP_INFO)] = DecodeGroupInfo,
			[typeof(SM_MAIL_SERVICE)] = DecodeMailService,
			[typeof(SM_EXCHANGE_REQUEST)] = DecodeExchangeRequest,
		}.ToFrozenDictionary();

	public IReadOnlyCollection<Type> PacketTypes => Decoders.Keys;

	public DecodedBotServerPacket Decode(DecodedGamePacket packet) => Decode(packet.PacketType, packet.Body);

	/// <summary>
	/// Decodes bot-facing packets and preserves known-but-uninteresting server packets as raw bodies so a live
	/// transport can keep the complete wire stream without pretending the packet is unknown to the server.
	/// </summary>
	public DecodedBotServerPacket DecodeOrRaw(DecodedGamePacket packet)
	{
		if (Decoders.TryGetValue(packet.PacketType, out var decoder))
			return new DecodedBotServerPacket(packet.PacketType, decoder(packet.Body));
		return new DecodedBotServerPacket(
			packet.PacketType,
			new Dictionary<string, object?>(StringComparer.Ordinal)
			{
				["bodyHex"] = Convert.ToHexString(packet.Body),
			});
	}

	public DecodedBotServerPacket Decode(Type packetType, ReadOnlySpan<byte> body)
	{
		if (!Decoders.TryGetValue(packetType, out var decoder))
			throw new NotSupportedException($"The bot does not decode {packetType.Name}.");
		return new DecodedBotServerPacket(packetType, decoder(body));
	}

	private static IReadOnlyDictionary<string, object?> DecodeKey(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		return Fields(("encodedKey", r.ReadInt32()));
	}

	private static IReadOnlyDictionary<string, object?> DecodeVersionCheck(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		return Fields(("answerId", r.ReadByte()));
	}

	private static IReadOnlyDictionary<string, object?> DecodeLoginCheck(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		var ok = r.ReadInt32() == 0;
		r.Skip(4 + 128 * 3 + 64 * 3);
		var maps = new List<IReadOnlyDictionary<string, object?>>();
		var mapCount = r.ReadUInt16();
		for (var i = 0; i < mapCount; i++)
			maps.Add(Fields(("mapId", r.ReadInt32()), ("twinCount", r.ReadUInt16())));
		return Fields(("ok", ok), ("worldMaps", maps), ("accountName", r.ReadString()));
	}

	private static IReadOnlyDictionary<string, object?> DecodeCharacterList(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		var playOk2 = r.ReadInt32();
		var characterCount = r.ReadByte();
		var characters = new List<IReadOnlyDictionary<string, object?>>(characterCount);
		for (var i = 0; i < characterCount; i++)
			characters.Add(DecodeCharacterSummary(ref r));
		return Fields(("playOk2", playOk2), ("characterCount", characterCount), ("characters", characters));
	}

	private static IReadOnlyDictionary<string, object?> DecodeCreateCharacter(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		var responseCode = r.ReadInt32();
		return responseCode == 0
			? Fields(("responseCode", responseCode), ("character", DecodeCharacterSummary(ref r)))
			: Fields(("responseCode", responseCode));
	}

	private static IReadOnlyDictionary<string, object?> DecodeCharacterSummary(ref PacketBodyReader r)
	{
		var objectId = r.ReadInt32();
		var nameBytes = r.ReadBytes((25 + 1) * sizeof(char));
		var name = System.Text.Encoding.Unicode.GetString(nameBytes).TrimEnd('\0');
		var gender = r.ReadInt32();
		var race = r.ReadInt32();
		var playerClass = r.ReadInt32();
		r.Skip(5 * sizeof(int)); // voice and four appearance colours
		r.Skip(52); // face/body appearance bytes, including the three reserved bytes
		r.Skip(sizeof(float)); // height
		var templateId = r.ReadInt32();
		var mapId = r.ReadInt32();
		var x = r.ReadSingle();
		var y = r.ReadSingle();
		var z = r.ReadSingle();
		var heading = r.ReadInt32();
		var level = r.ReadUInt16();
		r.Skip(sizeof(ushort)); // reserved
		r.Skip(2 * sizeof(int)); // title and legion id
		r.Skip((40 + 1) * sizeof(char)); // fixed legion name
		r.Skip(sizeof(ushort)); // legion membership flag
		var lastOnlineEpochSeconds = r.ReadInt32();
		r.Skip(16 * (sizeof(byte) + (3 * sizeof(int)))); // visible equipment
		r.Skip(6 * sizeof(int) + 68); // client-reserved blocks
		var deletionTimeSeconds = r.ReadInt32();
		r.Skip(2 * sizeof(ushort)); // helmet display and reserved
		r.Skip(4 * sizeof(int)); // mail counters
		r.Skip(sizeof(long)); // broker proceeds
		r.Skip(7 * sizeof(int)); // reserved values and character-ban timestamps
		_ = r.ReadString(); // character-ban reason
		return Fields(
			("objectId", objectId), ("name", name), ("gender", gender), ("race", race),
			("playerClass", playerClass), ("templateId", templateId), ("mapId", mapId),
			("x", x), ("y", y), ("z", z), ("heading", heading), ("level", level),
			("lastOnlineEpochSeconds", lastOnlineEpochSeconds), ("deletionTimeSeconds", deletionTimeSeconds));
	}

	private static IReadOnlyDictionary<string, object?> DecodeEnterWorldCheck(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		return Fields(("msg", r.ReadByte()));
	}

	private static IReadOnlyDictionary<string, object?> DecodePlayerSpawn(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		var worldChannel = r.ReadInt32();
		var worldId = r.ReadInt32();
		r.Skip(4);
		var personal = r.ReadByte() != 0;
		var fields = Fields(
			("worldChannel", worldChannel), ("worldId", worldId), ("personal", personal),
			("x", r.ReadSingle()), ("y", r.ReadSingle()), ("z", r.ReadSingle()), ("heading", r.ReadByte()));
		r.Skip(12);
		fields["beginner"] = r.ReadByte() != 0;
		return fields;
	}

	private static IReadOnlyDictionary<string, object?> DecodePlayMovie(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		var fields = Fields(
			("isMovie", r.ReadByte() != 0), ("objectId", r.ReadInt32()), ("questId", r.ReadInt32()),
			("cutsceneId", r.ReadInt32()));
		r.Skip(1);
		fields["canSkip"] = r.ReadByte() == 0;
		return fields;
	}

	private static IReadOnlyDictionary<string, object?> DecodeChatInit(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		return Fields(("token", r.ReadBytes(r.ReadInt32())));
	}

	private static IReadOnlyDictionary<string, object?> DecodeMessage(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		byte chatType = r.ReadByte();
		var fields = Fields(
			("chatType", chatType), ("senderRace", r.ReadByte()), ("senderObjectId", r.ReadInt32()),
			("senderName", r.ReadString()), ("message", r.ReadString()));
		if (chatType == (byte)Aion.GameServer.Model.ChatType.SHOUT)
		{
			fields["x"] = r.ReadSingle();
			fields["y"] = r.ReadSingle();
			fields["z"] = r.ReadSingle();
		}
		return fields;
	}

	private static IReadOnlyDictionary<string, object?> DecodePlayerInfo(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		var fields = Fields(("x", r.ReadSingle()), ("y", r.ReadSingle()), ("z", r.ReadSingle()), ("objectId", r.ReadInt32()));
		r.Skip(4 + 4 + 4 + 1 + 4 + 1);
		fields["race"] = r.ReadByte();
		fields["playerClass"] = r.ReadByte();
		fields["gender"] = r.ReadByte();
		fields["state"] = r.ReadUInt16();
		r.Skip(8);
		fields["heading"] = r.ReadByte();
		fields["name"] = r.ReadString();
		r.Skip(3 * sizeof(ushort)); // title, mentor flag, casting skill
		var legionId = r.ReadInt32();
		if (legionId == 0)
			r.Skip(8);
		else
		{
			r.Skip(6); // emblem id, type and ARGB
			_ = r.ReadString();
		}
		r.Skip(sizeof(byte) + sizeof(ushort) + sizeof(byte)); // hp%, dp, reserved
		var equipmentMask = unchecked((uint)r.ReadInt32());
		r.Skip(System.Numerics.BitOperations.PopCount(equipmentMask) * 16);
		r.Skip(4 * sizeof(int) + 51 + 3 * sizeof(float)); // colours, appearance bytes, height/scale/gravity
		fields["movementSpeed"] = r.ReadSingle();
		return fields;
	}

	private static IReadOnlyDictionary<string, object?> DecodeEmotion(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		var fields = Fields(
			("senderObjectId", r.ReadInt32()), ("emotionType", r.ReadByte()),
			("state", r.ReadUInt16()), ("movementSpeed", r.ReadSingle()));
		if (fields["emotionType"] is byte emotionType && emotionType == (byte)Aion.GameServer.Model.EmotionType.CHANGE_SPEED)
		{
			fields["baseAttackSpeed"] = r.ReadUInt16();
			fields["currentAttackSpeed"] = r.ReadUInt16();
			fields["reserved"] = r.ReadByte();
		}
		return fields;
	}

	private static IReadOnlyDictionary<string, object?> DecodeNpcInfo(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		return Fields(
			("x", r.ReadSingle()), ("y", r.ReadSingle()), ("z", r.ReadSingle()), ("objectId", r.ReadInt32()),
			("npcId", r.ReadInt32()), ("visualNpcId", r.ReadInt32()), ("creatureType", r.ReadByte()));
	}

	private static IReadOnlyDictionary<string, object?> DecodeGatherableInfo(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		var fields = Fields(
			("x", r.ReadSingle()), ("y", r.ReadSingle()), ("z", r.ReadSingle()), ("objectId", r.ReadInt32()),
			("staticId", r.ReadInt32()), ("templateId", r.ReadInt32()));
		var objectState = r.ReadUInt16();
		fields["objectState"] = objectState;
		fields["isStatic"] = objectState is 0x09 or 0x0A;
		fields["open"] = objectState switch
		{
			0x09 => true,
			0x0A => false,
			_ => null,
		};
		fields["heading"] = r.ReadByte();
		fields["l10nId"] = r.ReadInt32();
		return fields;
	}

	private static IReadOnlyDictionary<string, object?> DecodeMove(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		return Fields(
			("objectId", r.ReadInt32()), ("x", r.ReadSingle()), ("y", r.ReadSingle()), ("z", r.ReadSingle()),
			("heading", r.ReadByte()), ("movementMask", r.ReadByte()));
	}

	private static IReadOnlyDictionary<string, object?> DecodeWindstream(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		int state = r.ReadInt32();
		byte accepted = r.ReadByte();
		return Fields(("state", state), ("accepted", accepted), ("unk1", state), ("unk2", accepted));
	}

	private static IReadOnlyDictionary<string, object?> DecodeAbnormalState(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		int abnormals = r.ReadInt32();
		r.Skip(2 * sizeof(int));
		byte slot = r.ReadByte();
		ushort effectCount = r.ReadUInt16();
		var effects = new List<IReadOnlyDictionary<string, object?>>(effectCount);
		for (int index = 0; index < effectCount; index++)
		{
			effects.Add(Fields(
				("effectorId", r.ReadInt32()), ("skillId", r.ReadUInt16()), ("skillLevel", r.ReadByte()),
				("targetSlot", r.ReadByte()), ("remainingMillis", r.ReadInt32())));
		}
		return Fields(("abnormals", abnormals), ("slot", slot), ("effectCount", effectCount), ("effects", effects));
	}

	private static IReadOnlyDictionary<string, object?> DecodeWindstreamAnnounce(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		int flyPathType = r.ReadInt32();
		return Fields(("flyPathType", flyPathType), ("bidirectional", flyPathType), ("mapId", r.ReadInt32()),
			("streamId", r.ReadInt32()), ("state", r.ReadByte()));
	}

	private static IReadOnlyDictionary<string, object?> DecodeDelete(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		return Fields(("objectId", r.ReadInt32()), ("animationId", r.ReadByte()));
	}

	private static IReadOnlyDictionary<string, object?> DecodeTeleport(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		return Fields(
			("portAnimation", r.ReadByte()), ("mapId", r.ReadInt32()), ("destinationId", r.ReadInt32()),
			("x", r.ReadSingle()), ("y", r.ReadSingle()), ("z", r.ReadSingle()), ("heading", r.ReadByte()));
	}

	private static IReadOnlyDictionary<string, object?> DecodeDialog(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		return Fields(("targetObjectId", r.ReadInt32()), ("dialogPageId", r.ReadUInt16()), ("questId", r.ReadInt32()));
	}

	private static IReadOnlyDictionary<string, object?> DecodeQuestion(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		var code = r.ReadInt32();
		var parameters = new[] { r.ReadString(), r.ReadString(), r.ReadString() };
		r.Skip(5);
		return Fields(
			("code", code), ("params", parameters), ("senderId", r.ReadInt32()), ("rangeOrCooldownSeconds", r.ReadInt32()));
	}

	private static IReadOnlyDictionary<string, object?> DecodeCloseQuestion(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		r.Skip(4);
		return Fields(("messageId", r.ReadInt32()), ("params", new[] { r.ReadString(), r.ReadString(), r.ReadString() }));
	}

	private static IReadOnlyDictionary<string, object?> DecodeSystemMessage(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		var chatType = r.ReadByte();
		r.Skip(1);
		var senderObjectId = r.ReadInt32();
		var messageId = r.ReadInt32();
		var parameters = ReadStrings(ref r, r.ReadByte());
		var specialParameters = ReadStrings(ref r, r.ReadByte());
		return Fields(
			("chatType", chatType), ("senderObjectId", senderObjectId), ("msgId", messageId),
			("name", SystemMessageNames.GetNameOrNull(messageId)), ("params", parameters), ("specialParams", specialParameters));
	}

	private static IReadOnlyDictionary<string, object?> DecodeStats(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		var fields = Fields(("objectId", r.ReadInt32()));
		r.Skip(4 + 12 * 2);
		fields["level"] = r.ReadUInt16();
		r.Skip(3 * 2);
		fields["expNeeded"] = r.ReadInt64();
		fields["expRecoverable"] = r.ReadInt64();
		fields["expShown"] = r.ReadInt64();
		r.Skip(4);
		fields["maxHp"] = r.ReadInt32();
		fields["currentHp"] = r.ReadInt32();
		fields["maxMp"] = r.ReadInt32();
		fields["currentMp"] = r.ReadInt32();
		fields["maxDp"] = r.ReadUInt16();
		fields["dp"] = r.ReadUInt16();
		fields["maxFp"] = r.ReadInt32();
		fields["currentFp"] = r.ReadInt32();
		return fields;
	}

	private static IReadOnlyDictionary<string, object?> DecodeHp(ReadOnlySpan<byte> body) => DecodePair(body, "currentHp", "maxHp");

	private static IReadOnlyDictionary<string, object?> DecodeMp(ReadOnlySpan<byte> body) => DecodePair(body, "currentMp", "maxMp");

	private static IReadOnlyDictionary<string, object?> DecodePair(ReadOnlySpan<byte> body, string first, string second)
	{
		var r = new PacketBodyReader(body);
		return Fields((first, r.ReadInt32()), (second, r.ReadInt32()));
	}

	private static IReadOnlyDictionary<string, object?> DecodeDp(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		return Fields(("currentDp", r.ReadUInt16()));
	}

	private static IReadOnlyDictionary<string, object?> DecodeExp(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		return Fields(
			("currentExp", r.ReadInt64()), ("recoverableExp", r.ReadInt64()), ("maxExp", r.ReadInt64()),
			("rep1", r.ReadInt64()), ("rep2", r.ReadInt64()));
	}

	private static IReadOnlyDictionary<string, object?> DecodeFlyTime(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		return Fields(("currentFp", r.ReadInt32()), ("maxFp", r.ReadInt32()));
	}

	private static IReadOnlyDictionary<string, object?> DecodeDie(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		return Fields(
			("allowReviveBySkill", r.ReadByte() != 0), ("allowReviveByItem", r.ReadByte() != 0),
			("remainingKiskTimeSeconds", r.ReadInt32()), ("allowInstanceRevive", r.ReadByte() != 0),
			("invasion", r.ReadByte() == 0x80));
	}

	private static IReadOnlyDictionary<string, object?> DecodeInventoryInfo(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		var first = r.ReadByte() != 0;
		var npc = r.ReadByte();
		var quest = r.ReadByte();
		var item = r.ReadByte();
		var items = ReadInventoryItems(ref r, r.ReadUInt16());
		return Fields(("firstPacket", first), ("npcExpands", npc), ("questExpands", quest), ("itemExpands", item), ("items", items));
	}

	private static IReadOnlyDictionary<string, object?> DecodeInventoryAdd(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		var mask = r.ReadUInt16();
		return Fields(("addMask", mask), ("items", ReadInventoryItems(ref r, r.ReadUInt16())));
	}

	private static IReadOnlyDictionary<string, object?> DecodeInventoryUpdate(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		var objectId = r.ReadInt32();
		var description = r.ReadString();
		var blob = r.ReadLengthPrefixedBlob();
		var general = DecodeItemGeneralInfo(blob);
		return Fields(
			("objectId", objectId), ("desc", description), ("blob", blob),
			("itemMask", general?.ItemMask), ("itemCount", general?.ItemCount), ("itemCreator", general?.Creator),
			("updateMask", r.Remaining >= 2 ? r.ReadUInt16() : null));
	}

	private static IReadOnlyDictionary<string, object?> DecodeDeleteItem(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		return Fields(("itemObjectId", r.ReadInt32()), ("deleteType", r.ReadByte()));
	}

	private static IReadOnlyDictionary<string, object?> DecodeCubeUpdate(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		return Fields(("action", r.ReadByte()), ("actionValue", r.ReadByte()));
	}

	private static IReadOnlyDictionary<string, object?> DecodeSkillList(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		var count = r.ReadUInt16();
		var silent = r.ReadByte() != 0;
		var skills = new List<IReadOnlyDictionary<string, object?>>(count);
		for (var i = 0; i < count; i++)
		{
			skills.Add(Fields(
				("skillId", r.ReadUInt16()), ("level", r.ReadUInt16()), ("reserved", r.ReadByte()),
				("professionBarSize", r.ReadByte()), ("flag", r.ReadInt32()), ("skillType", r.ReadByte())));
		}
		return Fields(("silentUpdate", silent), ("skills", skills), ("messageId", r.ReadInt32()));
	}

	private static IReadOnlyDictionary<string, object?> DecodeSkillCooldown(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		var count = r.ReadUInt16();
		var notify = r.ReadByte() != 0;
		var entries = new List<IReadOnlyDictionary<string, object?>>(count);
		for (var i = 0; i < count; i++)
			entries.Add(Fields(("skillId", r.ReadUInt16()), ("remainingSeconds", r.ReadInt32()), ("durationMillis", r.ReadInt32())));
		return Fields(("notify", notify), ("cooldowns", entries));
	}

	private static IReadOnlyDictionary<string, object?> DecodeCastSpell(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		var fields = Fields(
			("objectId", r.ReadInt32()), ("spellId", r.ReadUInt16()), ("level", r.ReadByte()), ("targetType", r.ReadByte()));
		var targetType = (byte)fields["targetType"]!;
		if (targetType is 0 or 3 or 4)
			fields["targetObjectId"] = r.ReadInt32();
		else
		{
			fields["x"] = r.ReadSingle();
			fields["y"] = r.ReadSingle();
			fields["z"] = r.ReadSingle();
			if (targetType == 2)
				r.Skip(32);
		}
		fields["castDuration"] = r.ReadUInt16();
		r.Skip(1);
		fields["castSpeed"] = r.ReadSingle();
		fields["boost"] = r.ReadByte() != 0;
		return fields;
	}

	private static IReadOnlyDictionary<string, object?> DecodeCastSpellResult(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		var fields = Fields(("effectorId", r.ReadInt32()), ("targetType", r.ReadByte()));
		var targetType = (byte)fields["targetType"]!;
		if (targetType is 0 or 3 or 4)
			fields["targetId"] = r.ReadInt32();
		else
		{
			fields["x"] = r.ReadSingle();
			fields["y"] = r.ReadSingle();
			fields["z"] = r.ReadSingle();
			if (targetType == 2)
				r.Skip(32);
		}
		fields["skillId"] = r.ReadUInt16();
		fields["skillLevel"] = r.ReadByte();
		fields["cooldown"] = r.ReadInt32();
		fields["hitTime"] = r.ReadUInt16();
		r.Skip(1);
		fields["flags"] = r.ReadByte();
		return fields;
	}

	private static IReadOnlyDictionary<string, object?> DecodeSkillCancel(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		return Fields(("objectId", r.ReadInt32()), ("skillId", r.ReadUInt16()));
	}

	private static IReadOnlyDictionary<string, object?> DecodeQuestList(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		r.Skip(2);
		var count = unchecked((ushort)-r.ReadUInt16());
		var quests = new List<IReadOnlyDictionary<string, object?>>(count);
		for (var i = 0; i < count; i++)
			quests.Add(Fields(("questId", r.ReadInt32()), ("status", r.ReadByte()), ("stepAndFlags", r.ReadInt32()), ("completeCount", r.ReadByte())));
		return Fields(("quests", quests));
	}

	private static IReadOnlyDictionary<string, object?> DecodeQuestAction(ReadOnlySpan<byte> body)
	{
		if (body.IsEmpty)
			return Fields(("suppressed", true));
		var r = new PacketBodyReader(body);
		var action = r.ReadByte();
		var fields = Fields(("action", action), ("questId", r.ReadInt32()));
		switch (action)
		{
			case 1:
			case 2:
				fields["status"] = r.ReadByte();
				r.Skip(1);
				fields["stepAndFlags"] = r.ReadInt32();
				break;
			case 4:
				fields["timer"] = r.ReadInt32();
				break;
			case 5:
				fields["sharerId"] = r.ReadInt32();
				fields["shareInAlliance"] = r.ReadInt32() != 0;
				break;
		}
		return fields;
	}

	private static IReadOnlyDictionary<string, object?> DecodeQuestCompletedList(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		r.Skip(1);
		var updateMode = r.ReadByte();
		var count = unchecked((ushort)-r.ReadUInt16());
		var quests = new List<IReadOnlyDictionary<string, object?>>(count);
		for (var i = 0; i < count; i++)
			quests.Add(Fields(("questId", r.ReadInt32()), ("completeCount", r.ReadByte()), ("nonRepeatable", r.ReadByte() != 0)));
		return Fields(("updateMode", updateMode), ("quests", quests));
	}

	private static IReadOnlyDictionary<string, object?> DecodeLootStatus(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		return Fields(("targetObjectId", r.ReadInt32()), ("status", r.ReadByte()), ("lootEffectId", r.ReadInt32()));
	}

	private static IReadOnlyDictionary<string, object?> DecodeLootItemList(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		var target = r.ReadInt32();
		var count = r.ReadByte();
		var items = new List<IReadOnlyDictionary<string, object?>>(count);
		for (var i = 0; i < count; i++)
		{
			items.Add(Fields(
				("index", r.ReadByte()), ("itemId", r.ReadInt32()), ("count", r.ReadInt32()),
				("optionalSocket", r.ReadByte()), ("reserved1", r.ReadByte()), ("reserved2", r.ReadByte()),
				("requiresConfirmation", r.ReadByte() != 0)));
		}
		return Fields(("targetObjectId", target), ("items", items));
	}

	private static IReadOnlyDictionary<string, object?> DecodeTradeList(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		var fields = Fields(
			("targetObjectId", r.ReadInt32()), ("tradeNpcType", r.ReadByte()), ("buyPriceModifier", r.ReadInt32()));
		r.Skip(4);
		fields["showBuyTab"] = r.ReadByte() != 0;
		fields["showSellTab"] = r.ReadByte() != 0;
		var tabCount = r.ReadUInt16();
		var tabs = new int[tabCount];
		for (var i = 0; i < tabCount; i++)
			tabs[i] = r.ReadInt32();
		fields["tabs"] = tabs;
		var limitedCount = r.ReadUInt16();
		var limited = new List<IReadOnlyDictionary<string, object?>>(limitedCount);
		for (var i = 0; i < limitedCount; i++)
			limited.Add(Fields(("itemId", r.ReadInt32()), ("buyCount", r.ReadUInt16()), ("sellLimit", r.ReadUInt16())));
		fields["limitedItems"] = limited;
		return fields;
	}

	private static IReadOnlyDictionary<string, object?> DecodeGroupInfo(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		var fields = Fields(("groupId", r.ReadInt32()), ("leaderId", r.ReadInt32()), ("mapId", r.ReadInt32()));
		r.Skip(8 * 4 + 4 + 1);
		fields["teamType"] = r.ReadInt32();
		fields["teamSubType"] = r.ReadInt32();
		return fields;
	}

	private static IReadOnlyDictionary<string, object?> DecodeMailService(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		var service = r.ReadByte();
		var fields = Fields(("serviceId", service));
		if (service == 1)
			fields["messageId"] = r.ReadByte();
		return fields;
	}

	private static IReadOnlyDictionary<string, object?> DecodeExchangeRequest(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		return Fields(("receiver", r.ReadString()));
	}

	private static List<IReadOnlyDictionary<string, object?>> ReadInventoryItems(ref PacketBodyReader r, int count)
	{
		var items = new List<IReadOnlyDictionary<string, object?>>(count);
		for (var i = 0; i < count; i++)
		{
			var objectId = r.ReadInt32();
			var itemId = r.ReadInt32();
			var description = r.ReadString();
			var blob = r.ReadLengthPrefixedBlob();
			var general = DecodeItemGeneralInfo(blob);
			items.Add(Fields(
				("objectId", objectId), ("itemId", itemId), ("desc", description), ("blob", blob),
				("itemMask", general?.ItemMask), ("itemCount", general?.ItemCount), ("itemCreator", general?.Creator),
				("equipmentSlot", r.ReadUInt16()), ("cloth", r.ReadByte() != 0)));
		}
		return items;
	}

	private static ItemGeneralInfo? DecodeItemGeneralInfo(ReadOnlySpan<byte> blob)
	{
		var r = new PacketBodyReader(blob);
		while (r.Remaining > 0)
		{
			var entryId = r.ReadByte();
			if (entryId == 0x00)
			{
				var itemMask = r.ReadUInt16();
				var itemCount = r.ReadInt64();
				var creator = r.ReadString();
				r.Skip(21);
				return new ItemGeneralInfo(itemMask, itemCount, creator);
			}

			r.Skip(entryId switch
			{
				0x01 => 16,
				0x02 => 20,
				0x03 => 20,
				0x04 => 16,
				0x05 => 8,
				0x06 => 8,
				0x07 => 306,
				0x08 => 4,
				0x0A => 7,
				0x0B => 138,
				0x0D => 16,
				0x0E => 30,
				0x0F => 4,
				0x10 => 3,
				0x11 => 4,
				0x12 => 1,
				0x13 => 32,
				_ => throw new InvalidDataException($"Unknown item blob entry 0x{entryId:X2}."),
			});
		}
		return null;
	}

	private static string[] ReadStrings(ref PacketBodyReader r, int count)
	{
		var values = new string[count];
		for (var i = 0; i < count; i++)
			values[i] = r.ReadString();
		return values;
	}

	private static Dictionary<string, object?> Fields(params (string Name, object? Value)[] values) =>
		values.ToDictionary(value => value.Name, value => value.Value, StringComparer.Ordinal);

	private readonly record struct ItemGeneralInfo(ushort ItemMask, long ItemCount, string Creator);
}

public sealed record DecodedBotServerPacket(Type PacketType, IReadOnlyDictionary<string, object?> Fields)
{
	public T Get<T>(string name) => Fields.TryGetValue(name, out var value)
		? (T)value!
		: throw new KeyNotFoundException($"{PacketType.Name} did not decode a '{name}' field.");
}
