using System.Buffers.Binary;
using System.Reflection;
using Aion.Bots.Protocol;
using Aion.Commons.Nio;
using Aion.GameServer.Controllers.Movement;
using Aion.GameServer.Model;
using Aion.GameServer.Network;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Tests;

public sealed partial class BotGameClientPacketWriterTests
{
	[Theory]
	[MemberData(nameof(PacketCases))]
	public void WriterRoundTripsThroughProductionParser(string caseName, BotClientPacket outbound,
		AionConnection.State state, IReadOnlyDictionary<string, object?> expectedFields)
	{
		var liveCrypt = new Crypt();
		var codec = new GamePacketCodec();
		codec.RecoverKeyFromSmKeyFrame(CreateKeyFrame(liveCrypt.EnableKey()));
		var frame = outbound.Encode(codec, state);
		var payload = frame[2..];
		Assert.True(liveCrypt.Decrypt(ByteBuffer.Wrap(payload).Order(ByteOrder.LITTLE_ENDIAN)), caseName);

		var connection = new ParserConnection();
		connection.SetState(state);
		var parsed = AionClientPacketFactory.TryCreatePacket(ByteBuffer.Wrap(payload).Order(ByteOrder.LITTLE_ENDIAN), connection);

		Assert.NotNull(parsed);
		Assert.Equal(outbound.PacketType, parsed.GetType());
		Assert.True(parsed.Read(), caseName);
		Assert.Equal(0, parsed.GetRemainingBytes());
		foreach (var expected in expectedFields)
			Assert.Equal(expected.Value, GetFieldValue(parsed, expected.Key));
	}

	[Fact]
	public void L2AuthWriterIsExactlySixInt32Values() =>
		Assert.Equal(24, GameClientPackets.L2AuthLoginCheck(1, 2, 3, 4, 5, 6).Body.Length);

	public static IEnumerable<object[]> PacketCases()
	{
		var game = AionConnection.State.IN_GAME;
		var auth = AionConnection.State.AUTHED;
		var connected = AionConnection.State.CONNECTED;
		var allMove = (byte)(MovementMask.POSITION | MovementMask.MANUAL | MovementMask.ABSOLUTE | MovementMask.GLIDE | MovementMask.VEHICLE);
		var appearance = Enumerable.Range(0, CharacterCreationData.AppearanceFeatureLength).Select(i => (byte)i).ToArray();
		foreach (var row in ExtendedSocialPacketCases(game)) yield return row;
		foreach (var row in GearPacketCases(game)) yield return row;

		yield return C("version", GameClientPackets.VersionCheck(207, 9, 65001, 10, 11, 2), connected, "aionClientVersion", 207);
		yield return C("l2-auth", GameClientPackets.L2AuthLoginCheck(1, 2, 3, 4, 5, 6), connected, "accountId", 3);
		yield return C("mac", GameClientPackets.MacAddress("AA-BB-CC-DD-EE-FF", "SERIAL", 7, [8, 9]), connected, "macAddress", "AA-BB-CC-DD-EE-FF");
		yield return C("character-list", GameClientPackets.CharacterList(101), auth, "playOk2", 101);
		yield return C("create-character", GameClientPackets.CreateCharacter(new CharacterCreationData
		{
			AccountId = 1, AccountName = "account", CharacterName = "Botone", Gender = 1, Race = 0, PlayerClass = 0,
			Voice = 2, SkinRgb = 3, HairRgb = 4, EyeRgb = 5, LipRgb = 6, AppearanceFeatures = appearance, Height = 1.25f,
		}), auth, new Dictionary<string, object?> { ["characterName"] = "Botone", ["type"] = 0 });
		yield return C("enter-world", GameClientPackets.EnterWorld(102), auth, "objectId", 102);
		yield return C("level-ready", GameClientPackets.LevelReady(), game);
		yield return C("ui-settings", GameClientPackets.UiSettings(2, [1, 2, 3]), game, "settingsType", (byte)2);
		yield return C("chat-auth", GameClientPackets.ChatAuth(103, [1, 2, 3, 4, 5, 6]), game);
		yield return C("teleport-done", GameClientPackets.TeleportAnimationDone(), game);
		yield return C("movie-end", GameClientPackets.PlayMovieEnd(1, 104, 105, 106, true), game, "movieId", 106);
		yield return C("ping", GameClientPackets.Ping(7), auth);
		yield return C("time-check", GameClientPackets.TimeCheck(123456), game, "nanoTime", 123456);
		yield return C("team-command", GameClientPackets.TeamCommand(6, 123, 456, 789), game,
			new Dictionary<string, object?> { ["commandCode"] = 6, ["selectedObjectId"] = 123, ["allianceGroupId"] = 456, ["secondObjectId"] = 789 });
		yield return C("question", GameClientPackets.QuestionResponse(107, 1, 108), game, "questionid", 107);
		yield return C("move", GameClientPackets.Move(new MovementPacketData(1, 2, 3, 4, allMove,
			X2: 5, Y2: 6, Z2: 7, GlideFlag: GlideFlag.GEYSER, GeyserLocationId: 8,
			Unknown1: 9, Unknown2: 10, VehicleX: 11, VehicleY: 12, VehicleZ: 13)), game, "x2", 5f);
		yield return C("emotion-jump", GameClientPackets.Emotion((byte)EmotionType.JUMP), game, "emotionType", EmotionType.JUMP);
		yield return C("emotion-sit", GameClientPackets.Emotion((byte)EmotionType.SIT), game, "emotionType", EmotionType.SIT);
		yield return C("emotion-arbitrary", GameClientPackets.Emotion(0xff), game, "emotionType", EmotionType.NONE);
		yield return C("move-air", GameClientPackets.MoveInAir(109, 1, 2, 3, 4, 5), game, "worldId", 109);
		yield return C("windstream", GameClientPackets.Windstream(110, 111, 112), game, "teleportId", 110);
		yield return C("channel", GameClientPackets.ChangeChannel(3), game, "channel", 3);
		yield return C("target", GameClientPackets.TargetSelect(113, true), game, "targetObjectId", 113);
		yield return C("attack", GameClientPackets.Attack(114, 2, 300, 1), game, "targetObjectId", 114);
		yield return C("cast", GameClientPackets.CastSpell(new SpellCastData(115, 3, 2)
		{
			X = 1, Y = 2, Z = 3, TargetType2Unknowns = [4, 5, 6, 7, 8, 9, 10, 11], HitTime = 400, Unknown = 12,
		}), game, "spellid", 115);
		yield return C("charge-skill", GameClientPackets.UseChargeSkill(), game);
		yield return C("summon-command", GameClientPackets.SummonCommand(2, 116), game, "targetObjId", 116);
		yield return C("summon-attack", GameClientPackets.SummonAttack(117, 118, 1, 500, 2), game, "summonObjId", 117);
		yield return C("summon-cast", GameClientPackets.SummonCastSpell(119, 120, 2, 121), game, "skillId", 120);
		yield return C("summon-move", GameClientPackets.SummonMove(new MovementPacketData(1, 2, 3, 4, allMove,
			ObjectId: 122, X2: 5, Y2: 6, Z2: 7, GlideFlag: 1, Unknown1: 8, Unknown2: 9,
			VehicleX: 10, VehicleY: 11, VehicleZ: 12)), game, "objectId", 122);
		yield return C("use-item", GameClientPackets.UseItem(123, 2, 124), game, "targetItemId", 124);
		yield return C("equip", GameClientPackets.EquipItem(1, 125, 126), game, "itemObjId", 126);
		yield return C("move-item", GameClientPackets.MoveItem(127, 0, 1, 2), game, "itemObjId", 127);
		yield return C("split-item", GameClientPackets.SplitItem(128, 129, 0, 130, 1, 2), game, "itemAmount", 129L);
		yield return C("delete-item", GameClientPackets.DeleteItem(131), game, "itemObjectId", 131);
		yield return C("delete-quest", GameClientPackets.DeleteQuest(1098), game, "questId", 1098);
		yield return C("share-quest", GameClientPackets.ShareQuest(1112), game, "questId", 1112);
		yield return C("accept-shared-quest", GameClientPackets.DialogSelect(12345, 20000, 0, 0, 1112), game,
			new Dictionary<string, object?> { ["targetObjectId"] = 12345, ["dialogActionId"] = 20000, ["questId"] = 1112 });
		yield return C("start-loot", GameClientPackets.StartLoot(132, 1), game, "targetObjectId", 132);
		yield return C("loot-item", GameClientPackets.LootItem(133, 2), game, "index", 2);
		yield return C("show-dialog", GameClientPackets.ShowDialog(134), game, "targetObjectId", 134);
		yield return C("dialog-select", GameClientPackets.DialogSelect(135, 39, 2, 3, 1103), game, "dialogActionId", 39);
		yield return C("close-dialog", GameClientPackets.CloseDialog(136), game, "targetObjectId", 136);
		yield return C("teleport-select", GameClientPackets.TeleportSelect(137, 138), game, "locId", 138);
		yield return C("gather", GameClientPackets.Gather(-1), game, "actionId", -1);
		yield return C("craft", GameClientPackets.Craft(129, 139, 140, 141, 1, [(142, 3L)]), game, "recipeId", 140);
		yield return C("buy", GameClientPackets.BuyItem(143, 13, [(144, 2L)]), game, "sellerObjId", 143);
		yield return C("exchange-request", GameClientPackets.ExchangeRequest(145), game, "targetObjectId", 145);
		yield return C("exchange-item", GameClientPackets.ExchangeAddItem(146, 3), game, "itemObjId", 146);
		yield return C("exchange-kinah", GameClientPackets.ExchangeAddKinah(147), game, "kinahCount", 147L);
		yield return C("exchange-lock", GameClientPackets.ExchangeLock(), game);
		yield return C("exchange-ok", GameClientPackets.ExchangeOk(), game);
		yield return C("exchange-cancel", GameClientPackets.ExchangeCancel(), game);
		yield return C("mail-send", GameClientPackets.SendMail("Recipient", "Title", "Message", 149, 3, 125), game,
			new Dictionary<string, object?> { ["recipientName"] = "Recipient", ["title"] = "Title", ["message"] = "Message",
				["itemObjId"] = 149, ["itemCount"] = 3L, ["kinahCount"] = 125L, ["idLetterType"] = 0 });
		yield return C("mail-list", GameClientPackets.CheckMailList(true), game, "expressOnly", true);
		yield return C("mail-read", GameClientPackets.ReadMail(150), game, "mailObjId", 150);
		yield return C("mail-attachment", GameClientPackets.GetMailAttachment(150, 1), game,
			new Dictionary<string, object?> { ["mailObjId"] = 150, ["attachmentType"] = (byte)1 });
		yield return C("mail-delete", GameClientPackets.DeleteMail(150, 151), game, "mailObjIds", new[] { 150, 151 });
		yield return C("chat-public", GameClientPackets.ChatMessagePublic(0, "hello"), game, "message", "hello");
		yield return C("chat-whisper", GameClientPackets.ChatMessageWhisper("Target", "hello"), game, "name", "Target");
		yield return C("group-invite", GameClientPackets.InviteToGroup(0, "Target"), game, "playerName", "Target");
		yield return C("distribution-settings", GameClientPackets.DistributionSettings(2, 1, 2, 0, 2, 0, 2, 3, 1), game,
			new Dictionary<string, object?> { ["isLeague"] = 1, ["lootRule"] = 2, ["misc"] = 1, ["commonItemAbove"] = 2,
				["superiorItemAbove"] = 0, ["heroicItemAbove"] = 2, ["fabledItemAbove"] = 0, ["ethernalItemAbove"] = 2, ["mythicItemAbove"] = 3, ["unk"] = 2 });
		yield return C("group-roll", GameClientPackets.GroupLoot(123, 4, 162000031, 456, 2, true), game,
			new Dictionary<string, object?> { ["groupId"] = 123, ["index"] = 4, ["itemId"] = 162000031, ["npcObjId"] = 456,
				["distributionMode"] = 2, ["roll"] = 1, ["bid"] = 0L });
		yield return C("group-bid", GameClientPackets.GroupLoot(123, 4, 162000031, 456, 3, false, 5000000000L), game,
			new Dictionary<string, object?> { ["distributionMode"] = 3, ["roll"] = 0, ["bid"] = 5000000000L });
		yield return C("alliance-invite", GameClientPackets.InviteToGroup(12, "Captain"), game,
			new Dictionary<string, object?> { ["inviteType"] = 12, ["playerName"] = "Captain" });
		yield return C("league-invite", GameClientPackets.InviteToGroup(28, "Captain"), game,
			new Dictionary<string, object?> { ["inviteType"] = 28, ["playerName"] = "Captain" });
		yield return C("duel", GameClientPackets.DuelRequest(148), game, "objectId", 148);
		yield return C("legion", GameClientPackets.Legion(0x01, first: "Target"), game, "charName", "Target");
		yield return C("revive", GameClientPackets.Revive(0), game, "reviveId", 0);
		yield return C("delete-character", GameClientPackets.DeleteCharacter(149, 150), auth, "chaOid", 150);
		yield return C("restore-character", GameClientPackets.RestoreCharacter(151, 152), auth, "chaOid", 152);
		yield return C("quit", GameClientPackets.Quit(true), game, "stayConnected", true);
		yield return C("friend-status", GameClientPackets.FriendStatus(2), game, "status", (byte)2);
		yield return C("friend-list", GameClientPackets.ShowFriendList(), game);
		yield return C("friend-add", GameClientPackets.FriendAdd("Friend", "Hello"), game, new Dictionary<string, object?> { ["targetName"] = "Friend", ["message"] = "Hello" });
		yield return C("friend-delete", GameClientPackets.FriendDelete("Friend"), game, "targetName", "Friend");
		yield return C("friend-memo", GameClientPackets.FriendMemo("Friend", "A memo"), game, new Dictionary<string, object?> { ["targetName"] = "Friend", ["memo"] = "A memo" });
		yield return C("block-add", GameClientPackets.BlockAdd("Friend", "A reason"), game, new Dictionary<string, object?> { ["targetName"] = "Friend", ["reason"] = "A reason" });
		yield return C("block-delete", GameClientPackets.BlockDelete("Friend"), game, "targetName", "Friend");
		yield return C("block-reason", GameClientPackets.BlockReason("Friend", "Edited"), game, new Dictionary<string, object?> { ["targetName"] = "Friend", ["reason"] = "Edited" });
	}

	private static object[] C(string name, BotClientPacket packet, AionConnection.State state,
		string field, object? expected) => C(name, packet, state, new Dictionary<string, object?> { [field] = expected });
	private static object[] C(string name, BotClientPacket packet, AionConnection.State state,
		IReadOnlyDictionary<string, object?>? expected = null) => [name, packet, state, expected ?? new Dictionary<string, object?>()];

	private static object? GetFieldValue(object target, string fieldName)
	{
		for (var type = target.GetType(); type != null; type = type.BaseType)
		{
			var field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (field != null) return field.GetValue(target);
		}
		throw new MissingFieldException(target.GetType().FullName, fieldName);
	}

	private static byte[] CreateKeyFrame(int encodedKey)
	{
		var opcode = GamePacketRegistry.Instance.GetServer(typeof(SM_KEY)).Opcode;
		var frame = new byte[11];
		BinaryPrimitives.WriteUInt16LittleEndian(frame, (ushort)frame.Length);
		var encodedOpcode = GamePacketCodec.EncodeServerOpcode(opcode);
		BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(2), encodedOpcode);
		frame[4] = GamePacketCodec.ServerPacketCode;
		BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(5), unchecked((ushort)~encodedOpcode));
		BinaryPrimitives.WriteInt32LittleEndian(frame.AsSpan(7), encodedKey);
		return frame;
	}

	private sealed class ParserConnection : AionConnection
	{
		public ParserConnection() : base("127.0.0.1") { }
	}
}
