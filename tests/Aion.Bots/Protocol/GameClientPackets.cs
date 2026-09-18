using Aion.GameServer.Controllers.Movement;
using Aion.GameServer.Model;
using Aion.GameServer.Network.Aion.ClientPackets;

namespace Aion.Bots.Protocol;

public static class GameClientPackets
{
	public static BotClientPacket VersionCheck(ushort clientVersion, ushort npcVersion, int windowsEncoding,
		int windowsVersion, int windowsSubVersion, byte liteInfo) =>
		Create<CM_VERSION_CHECK>(w => { w.UH(clientVersion); w.UH(npcVersion); w.D(windowsEncoding); w.D(windowsVersion); w.D(windowsSubVersion); w.C(liteInfo); });

	public static BotClientPacket L2AuthLoginCheck(int playOk2, int playOk1, int accountId, int loginOk, int unknown1 = 0, int unknown2 = 0) =>
		Create<CM_L2AUTH_LOGIN_CHECK>(w => { w.D(playOk2); w.D(playOk1); w.D(accountId); w.D(loginOk); w.D(unknown1); w.D(unknown2); });

	public static BotClientPacket MacAddress(string macAddress, string hddSerial, int localIp = 0,
		IReadOnlyList<int>? routeIps = null, byte unknown = 0) => Create<CM_MAC_ADDRESS>(w =>
	{
		w.C(unknown);
		w.UH(routeIps?.Count ?? 0);
		if (routeIps != null)
			foreach (var routeIp in routeIps)
				w.D(routeIp);
		w.S(macAddress);
		w.S(hddSerial);
		w.D(localIp);
	});

	public static BotClientPacket CharacterList(int playOk2) => Create<CM_CHARACTER_LIST>(w => w.D(playOk2));

	public static BotClientPacket CreateCharacter(CharacterCreationData data) => Create<CM_CREATE_CHARACTER>(w =>
	{
		if (data.AppearanceFeatures.Length != CharacterCreationData.AppearanceFeatureLength)
			throw new ArgumentException($"AppearanceFeatures must contain {CharacterCreationData.AppearanceFeatureLength} bytes.", nameof(data));
		w.D(data.AccountId);
		w.S(data.AccountName);
		w.S(data.CharacterName, 25);
		w.D(data.Gender);
		w.D(data.Race);
		w.D(data.PlayerClass);
		w.D(data.Voice);
		w.D(data.SkinRgb);
		w.D(data.HairRgb);
		w.D(data.EyeRgb);
		w.D(data.LipRgb);
		w.B(data.AppearanceFeatures);
		w.F(data.Height);
		w.C(data.Type);
	});

	public static BotClientPacket EnterWorld(int objectId) => Create<CM_ENTER_WORLD>(w => w.D(objectId));
	public static BotClientPacket LevelReady() => Empty<CM_LEVEL_READY>();

	public static BotClientPacket UiSettings(byte settingsType, ReadOnlySpan<byte> data, short unknown = 0)
	{
		var copiedData = data.ToArray();
		return Create<CM_UI_SETTINGS>(w =>
		{
			w.C(settingsType); w.H(unknown); w.UH(copiedData.Length); w.B(copiedData);
		});
	}

	public static BotClientPacket ChatAuth(int objectId, ReadOnlySpan<byte> macAddress)
	{
		if (macAddress.Length != 6)
			throw new ArgumentException("Chat authentication requires exactly six MAC bytes.", nameof(macAddress));
		var copiedMacAddress = macAddress.ToArray();
		return Create<CM_CHAT_AUTH>(w => { w.D(objectId); w.B(copiedMacAddress); });
	}

	public static BotClientPacket TeleportAnimationDone() => Empty<CM_TELEPORT_ANIMATION_DONE>();
	public static BotClientPacket PlayMovieEnd(byte type, int targetObjectId, int questId, int movieId, bool canSkip, byte unknown = 0) =>
		Create<CM_PLAY_MOVIE_END>(w => { w.C(type); w.D(targetObjectId); w.D(questId); w.D(movieId); w.C(unknown); w.C(canSkip ? 0 : 1); });
	public static BotClientPacket Ping(short unknown = 0) => Create<CM_PING>(w => w.H(unknown));
	public static BotClientPacket TimeCheck(int clientMillis) => Create<CM_TIME_CHECK>(w => w.D(clientMillis));
	public static BotClientPacket QuestionResponse(int questionId, byte response, int senderId, byte unknownByte = 0,
		short unknownShort1 = 0, int unknownInt = 0, short unknownShort2 = 0) => Create<CM_QUESTION_RESPONSE>(w =>
	{
		w.D(questionId); w.C(response); w.C(unknownByte); w.H(unknownShort1); w.D(senderId); w.D(unknownInt); w.H(unknownShort2);
	});

	public static BotClientPacket Move(MovementPacketData data) => Create<CM_MOVE>(w => WriteMovement(w, data, includeObjectId: false));

	public static BotClientPacket Emotion(byte type, ushort emotion = 0, int targetObjectId = 0,
		float x = 0, float y = 0, float z = 0, byte heading = 0, byte strafeUnknown = 0, int sprintUnknown = 0) =>
		Create<CM_EMOTION>(w =>
		{
			w.C(type);
			switch ((EmotionType)type)
			{
				case EmotionType.WINDSTREAM_STRAFE: w.C(strafeUnknown); break;
				case EmotionType.START_SPRINT: w.D(sprintUnknown); break;
				case EmotionType.EMOTE: w.UH(emotion); w.D(targetObjectId); break;
				case EmotionType.CHAIR_SIT:
				case EmotionType.CHAIR_UP: w.F(x); w.F(y); w.F(z); w.C(heading); break;
			}
		});

	public static BotClientPacket MoveInAir(int worldId, float x, float y, float z, byte heading, int distance) =>
		Create<CM_MOVE_IN_AIR>(w => { w.D(worldId); w.F(x); w.F(y); w.F(z); w.C(heading); w.D(distance); });
	public static BotClientPacket Windstream(int teleportId, int distance, int state) =>
		Create<CM_WINDSTREAM>(w => { w.D(teleportId); w.D(distance); w.D(state); });
	public static BotClientPacket ChangeChannel(int channel) => Create<CM_CHANGE_CHANNEL>(w => w.D(channel));
	public static BotClientPacket TargetSelect(int targetObjectId, bool selectTargetOfTarget = false) =>
		Create<CM_TARGET_SELECT>(w => { w.D(targetObjectId); w.C(selectTargetOfTarget ? 1 : 0); });
	public static BotClientPacket Attack(int targetObjectId, byte attackNumber, ushort time, byte type) =>
		Create<CM_ATTACK>(w => { w.D(targetObjectId); w.C(attackNumber); w.UH(time); w.C(type); });

	public static BotClientPacket CastSpell(SpellCastData data) => Create<CM_CASTSPELL>(w =>
	{
		w.UH(data.SkillId); w.C(data.Level); w.C(data.TargetType);
		switch (data.TargetType)
		{
			case 0:
			case 3:
			case 4: w.D(data.TargetObjectId); break;
			case 1: w.F(data.X); w.F(data.Y); w.F(data.Z); break;
			case 2:
				w.F(data.X); w.F(data.Y); w.F(data.Z);
				foreach (var value in data.TargetType2Unknowns)
					w.F(value);
				break;
		}
		w.UH(data.HitTime); w.D(data.Unknown);
	});

	public static BotClientPacket UseChargeSkill() => Empty<CM_USE_CHARGE_SKILL>();
	public static BotClientPacket SummonCommand(byte mode, int targetObjectId, int unknown1 = 0, int unknown2 = 0) =>
		Create<CM_SUMMON_COMMAND>(w => { w.C(mode); w.D(unknown1); w.D(unknown2); w.D(targetObjectId); });
	public static BotClientPacket SummonAttack(int summonObjectId, int targetObjectId, byte unknown1, ushort time, byte unknown3) =>
		Create<CM_SUMMON_ATTACK>(w => { w.D(summonObjectId); w.D(targetObjectId); w.C(unknown1); w.UH(time); w.C(unknown3); });
	public static BotClientPacket SummonCastSpell(int summonObjectId, ushort skillId, byte skillLevel, int targetObjectId, int unknown = 0) =>
		Create<CM_SUMMON_CASTSPELL>(w => { w.D(summonObjectId); w.UH(skillId); w.C(skillLevel); w.D(targetObjectId); w.D(unknown); });
	public static BotClientPacket SummonMove(MovementPacketData data) => Create<CM_SUMMON_MOVE>(w => WriteMovement(w, data, includeObjectId: true));

	public static BotClientPacket UseItem(int uniqueItemId, byte type = 0, int argument = 0) => Create<CM_USE_ITEM>(w =>
	{
		w.D(uniqueItemId); w.C(type); if (type is 2 or 5 or 6) w.D(argument);
	});
	public static BotClientPacket EquipItem(byte action, long slot, int itemObjectId) =>
		Create<CM_EQUIP_ITEM>(w => { w.C(action); w.Q(slot); w.D(itemObjectId); });
	public static BotClientPacket MoveItem(int itemObjectId, byte source, byte destination, short slot) =>
		Create<CM_MOVE_ITEM>(w => { w.D(itemObjectId); w.C(source); w.C(destination); w.H(slot); });
	public static BotClientPacket SplitItem(int sourceItemObjectId, long amount, byte sourceStorageType,
		int destinationItemObjectId, byte destinationStorageType, short slot) => Create<CM_SPLIT_ITEM>(w =>
	{
		w.D(sourceItemObjectId); w.Q(amount); w.C(sourceStorageType); w.D(destinationItemObjectId); w.C(destinationStorageType); w.H(slot);
	});
	public static BotClientPacket DeleteItem(int itemObjectId) => Create<CM_DELETE_ITEM>(w => w.D(itemObjectId));
	public static BotClientPacket DeleteQuest(int questId) => Create<CM_DELETE_QUEST>(w => w.D(questId));
	public static BotClientPacket StartLoot(int targetObjectId, byte action) => Create<CM_START_LOOT>(w => { w.D(targetObjectId); w.C(action); });
	public static BotClientPacket LootItem(int targetObjectId, byte index) => Create<CM_LOOT_ITEM>(w => { w.D(targetObjectId); w.C(index); });
	public static BotClientPacket ShowDialog(int targetObjectId) => Create<CM_SHOW_DIALOG>(w => w.D(targetObjectId));
	public static BotClientPacket DialogSelect(int targetObjectId, ushort actionId, ushort rewardIndex, ushort lastPage, int questId, ushort unknown = 0) =>
		Create<CM_DIALOG_SELECT>(w => { w.D(targetObjectId); w.UH(actionId); w.UH(rewardIndex); w.UH(lastPage); w.D(questId); w.UH(unknown); });
	public static BotClientPacket CloseDialog(int targetObjectId) => Create<CM_CLOSE_DIALOG>(w => w.D(targetObjectId));
	public static BotClientPacket TeleportSelect(int targetObjectId, int locationId, short unknown = 0) =>
		Create<CM_TELEPORT_SELECT>(w => { w.D(targetObjectId); w.D(locationId); w.H(unknown); });
	public static BotClientPacket Gather(int actionId) => Create<CM_GATHER>(w => w.D(actionId));

	public static BotClientPacket Craft(byte unknown, int targetTemplateId, int recipeId, int targetObjectId,
		byte craftType, IReadOnlyList<(int ItemId, long Count)> materials) => Create<CM_CRAFT>(w =>
	{
		w.C(unknown); w.D(targetTemplateId); w.D(recipeId); w.D(targetObjectId); w.UH(materials.Count); w.C(craftType);
		foreach (var material in materials) { w.D(material.ItemId); w.Q(material.Count); }
	});

	public static BotClientPacket BuyItem(int sellerObjectId, short tradeActionId, IReadOnlyList<(int ItemId, long Count)> items) =>
		Create<CM_BUY_ITEM>(w =>
		{
			w.D(sellerObjectId); w.H(tradeActionId); w.UH(items.Count);
			foreach (var item in items) { w.D(item.ItemId); w.Q(item.Count); }
		});

	public static BotClientPacket ExchangeRequest(int targetObjectId) => Create<CM_EXCHANGE_REQUEST>(w => w.D(targetObjectId));
	public static BotClientPacket ExchangeAddItem(int itemObjectId, int count) => Create<CM_EXCHANGE_ADD_ITEM>(w => { w.D(itemObjectId); w.D(count); });
	public static BotClientPacket ExchangeAddKinah(long count) => Create<CM_EXCHANGE_ADD_KINAH>(w => w.Q(count));
	public static BotClientPacket ExchangeLock() => Empty<CM_EXCHANGE_LOCK>();
	public static BotClientPacket ExchangeOk() => Empty<CM_EXCHANGE_OK>();
	public static BotClientPacket ExchangeCancel() => Empty<CM_EXCHANGE_CANCEL>();
	public static BotClientPacket SendMail(string recipient, string title, string message, int itemObjectId, long itemCount, long kinah, byte letterType = 0) =>
		Create<CM_SEND_MAIL>(w => { w.S(recipient); w.S(title); w.S(message); w.D(itemObjectId); w.Q(itemCount); w.Q(kinah); w.C(letterType); });
	public static BotClientPacket CheckMailList(bool expressOnly = false) => Create<CM_CHECK_MAIL_LIST>(w => w.C(expressOnly ? 1 : 0));
	public static BotClientPacket ReadMail(int letterId) => Create<CM_READ_MAIL>(w => w.D(letterId));
	public static BotClientPacket GetMailAttachment(int letterId, byte attachmentType) => Create<CM_GET_MAIL_ATTACHMENT>(w => { w.D(letterId); w.C(attachmentType); });
	public static BotClientPacket DeleteMail(params int[] letterIds) => Create<CM_DELETE_MAIL>(w =>
	{
		w.UH(letterIds.Length);
		foreach (int id in letterIds) { w.D(id); w.C(0); }
	});
	public static BotClientPacket ChatMessagePublic(byte type, string message) => Create<CM_CHAT_MESSAGE_PUBLIC>(w => { w.C(type); w.S(message); });
	public static BotClientPacket ChatMessageWhisper(string name, string message) => Create<CM_CHAT_MESSAGE_WHISPER>(w => { w.S(name); w.S(message); });
	public static BotClientPacket InviteToGroup(byte inviteType, string playerName) => Create<CM_INVITE_TO_GROUP>(w => { w.C(inviteType); w.S(playerName); });
	public static BotClientPacket DuelRequest(int objectId) => Create<CM_DUEL_REQUEST>(w => w.D(objectId));

	public static BotClientPacket Legion(byte subOpcode, int value = 0, string first = "", string second = "",
		IReadOnlyList<short>? permissions = null) => Create<CM_LEGION>(w =>
	{
		w.C(subOpcode);
		switch (subOpcode)
		{
			case 0x00:
			case 0x01:
			case 0x04:
			case 0x05:
			case 0x09:
			case 0x0A: w.D(value); w.S(first); break;
			case 0x02:
			case 0x07:
			case 0x08:
			case 0x0E: w.D(value); w.H(0); break;
			case 0x06: w.D(value); w.S(first); break;
			case 0x0D:
				if (permissions == null || permissions.Count != 4)
					throw new ArgumentException("Legion permissions require four values.", nameof(permissions));
				foreach (var permission in permissions) w.H(permission);
				break;
			case 0x0F: w.S(first); w.S(second); break;
			case 0x10: w.D(value); break;
		}
	});

	public static BotClientPacket Revive(byte reviveId) => Create<CM_REVIVE>(w => w.C(reviveId));
	public static BotClientPacket DeleteCharacter(int playOk2, int characterObjectId) => Create<CM_DELETE_CHARACTER>(w => { w.D(playOk2); w.D(characterObjectId); });
	public static BotClientPacket RestoreCharacter(int playOk2, int characterObjectId) => Create<CM_RESTORE_CHARACTER>(w => { w.D(playOk2); w.D(characterObjectId); });
	public static BotClientPacket Quit(bool stayConnected) => Create<CM_QUIT>(w => w.C(stayConnected ? 1 : 0));
	public static BotClientPacket FriendStatus(byte status) => Create<CM_FRIEND_STATUS>(w => w.C(status));

	private static BotClientPacket Empty<T>() => new(typeof(T), []);

	private static BotClientPacket Create<T>(Action<PacketBodyWriter> write)
	{
		var writer = new PacketBodyWriter();
		write(writer);
		return new BotClientPacket(typeof(T), writer.ToArray());
	}

	private static void WriteMovement(PacketBodyWriter w, MovementPacketData data, bool includeObjectId)
	{
		if (includeObjectId) w.D(data.ObjectId);
		w.F(data.X); w.F(data.Y); w.F(data.Z); w.C(data.Heading); w.C(data.Type);
		if ((data.Type & MovementMask.POSITION) != 0 && (data.Type & MovementMask.MANUAL) != 0)
		{
			if ((data.Type & MovementMask.ABSOLUTE) != 0)
			{
				w.F(data.X2); w.F(data.Y2); w.F(data.Z2);
			}
			else if (!includeObjectId)
			{
				w.F(data.VectorX); w.F(data.VectorY); w.F(data.VectorZ);
			}
		}
		if ((data.Type & MovementMask.GLIDE) != 0)
		{
			w.C(data.GlideFlag);
			if (!includeObjectId && data.GlideFlag == GlideFlag.GEYSER) w.C(data.GeyserLocationId);
		}
		if ((data.Type & MovementMask.VEHICLE) != 0)
		{
			w.D(data.Unknown1); w.D(data.Unknown2); w.F(data.VehicleX); w.F(data.VehicleY); w.F(data.VehicleZ);
		}
	}
}

public sealed record CharacterCreationData
{
	public const int AppearanceFeatureLength = 52;
	public int AccountId { get; init; }
	public string AccountName { get; init; } = "";
	public string CharacterName { get; init; } = "";
	public int Gender { get; init; }
	public int Race { get; init; }
	public int PlayerClass { get; init; }
	public int Voice { get; init; }
	public int SkinRgb { get; init; }
	public int HairRgb { get; init; }
	public int EyeRgb { get; init; }
	public int LipRgb { get; init; }
	public byte[] AppearanceFeatures { get; init; } = new byte[AppearanceFeatureLength];
	public float Height { get; init; } = 1;
	public byte Type { get; init; }
}

public sealed record MovementPacketData(
	float X, float Y, float Z, byte Heading, byte Type,
	int ObjectId = 0, float X2 = 0, float Y2 = 0, float Z2 = 0,
	float VectorX = 0, float VectorY = 0, float VectorZ = 0,
	byte GlideFlag = 0, byte GeyserLocationId = 0,
	int Unknown1 = 0, int Unknown2 = 0, float VehicleX = 0, float VehicleY = 0, float VehicleZ = 0);

public sealed record SpellCastData(ushort SkillId, byte Level, byte TargetType)
{
	public int TargetObjectId { get; init; }
	public float X { get; init; }
	public float Y { get; init; }
	public float Z { get; init; }
	public float[] TargetType2Unknowns { get; init; } = new float[8];
	public ushort HitTime { get; init; }
	public int Unknown { get; init; }
}
