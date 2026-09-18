using Aion.Bots.Api;
using Aion.Bots.Protocol;
using Aion.Bots.Reflexes;
using Aion.Bots.Timing;
using Aion.Bots.World;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.Network.Aion.ClientPackets;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Tests;

public sealed class BotApiTests
{
	[Fact]
	public void FacadeMapsLifecycleMovementCombatAndInteractionIntentsToPackets()
	{
		var api = new BotApi();
		var creation = new CharacterCreationData { AccountId = 1, AccountName = "account", CharacterName = "Botone" };
		var movement = new MovementPacketData(1, 2, 3, 4, 0);

		AssertPacket<CM_CHARACTER_LIST>(api.ListCharacters(1));
		AssertPacket<CM_CREATE_CHARACTER>(api.CreateCharacter(creation));
		AssertPacket<CM_DELETE_CHARACTER>(api.DeleteCharacter(1, 2));
		AssertPacket<CM_RESTORE_CHARACTER>(api.RestoreCharacter(1, 2));
		AssertPacket<CM_ENTER_WORLD>(api.EnterWorld(2));
		AssertPacket<CM_CHANGE_CHANNEL>(api.ChangeChannel(3));
		AssertPacket<CM_MOVE>(api.MoveTo(movement));
		Assert.Equal([typeof(CM_EMOTION), typeof(CM_MOVE)], api.Jump(movement).Select(packet => packet.PacketType));
		AssertPacket<CM_EMOTION>(api.Fly());
		AssertPacket<CM_EMOTION>(api.Land());
		Assert.Equal([typeof(CM_EMOTION), typeof(CM_MOVE)], api.Glide(movement).Select(packet => packet.PacketType));
		AssertPacket<CM_EMOTION>(api.Rest(true));
		AssertPacket<CM_EMOTION>(api.Walk(true));
		AssertPacket<CM_EMOTION>(api.Emote(7));

		AssertPacket<CM_TARGET_SELECT>(api.Target(50));
		AssertPacket<CM_ATTACK>(api.Attack(50, 1000));
		AssertPacket<CM_CASTSPELL>(api.Cast(new SpellCastData(100, 1, 0) { TargetObjectId = 50 }));
		AssertPacket<CM_SUMMON_COMMAND>(api.SummonCommand(1, 50));
		AssertPacket<CM_SUMMON_ATTACK>(api.SummonAttack(60, 50));
		AssertPacket<CM_SUMMON_CASTSPELL>(api.SummonCast(60, 101, 1, 50));

		var itemTemplate = new ItemTemplate { itemId = 160000001, useLimits = new ItemUseLimits() };
		AssertPacket<CM_USE_ITEM>(api.UseItem(70, itemTemplate));
		AssertPacket<CM_EQUIP_ITEM>(api.Equip(1, 2, 70));
		AssertPacket<CM_START_LOOT>(api.Loot(80));
		AssertPacket<CM_LOOT_ITEM>(api.Loot(80, 1));
		AssertPacket<CM_SHOW_DIALOG>(api.TalkTo(90));
		AssertPacket<CM_DIALOG_SELECT>(api.SelectDialog(90, 39));
		AssertPacket<CM_CLOSE_DIALOG>(api.CloseDialog(90));
		AssertPacket<CM_TELEPORT_SELECT>(api.Teleport(90, 100));
	}

	[Fact]
	public void FacadeMapsGatherCraftEconomySocialAndDisconnectIntentsToPackets()
	{
		var api = new BotApi();
		Assert.Equal([typeof(CM_TARGET_SELECT), typeof(CM_GATHER)], api.Gather(10).Select(packet => packet.PacketType));
		Assert.Throws<InvalidOperationException>(() => api.MoveTo(new MovementPacketData(0, 0, 0, 0, 0)));
		AssertPacket<CM_GATHER>(Assert.Single(api.Gather(10, start: false)));
		AssertPacket<CM_CRAFT>(api.Craft(20, 21, 22, [(23, 2L)]));
		api.Timing.SetActivity(BotBlockingActivity.Crafting, false);
		AssertPacket<CM_BUY_ITEM>(api.Buy(30, [(31, 1L)]));
		AssertPacket<CM_BUY_ITEM>(api.Sell(30, [(32, 1L)]));
		AssertPacket<CM_EXCHANGE_REQUEST>(api.TradeRequest(40));
		AssertPacket<CM_EXCHANGE_ADD_ITEM>(api.TradeAddItem(41, 2));
		AssertPacket<CM_EXCHANGE_ADD_KINAH>(api.TradeAddKinah(100));
		AssertPacket<CM_EXCHANGE_LOCK>(api.TradeLock());
		AssertPacket<CM_EXCHANGE_OK>(api.TradeAccept());
		AssertPacket<CM_EXCHANGE_CANCEL>(api.TradeCancel());
		AssertPacket<CM_SEND_MAIL>(api.SendMail("Daeva", "Title", "Message", 41, 2, 100));
		AssertPacket<CM_CHECK_MAIL_LIST>(api.CheckMailList());
		AssertPacket<CM_READ_MAIL>(api.ReadMail(42));
		AssertPacket<CM_GET_MAIL_ATTACHMENT>(api.GetMailAttachment(42, 0));
		AssertPacket<CM_DELETE_MAIL>(api.DeleteMail(42));
		AssertPacket<CM_INVITE_TO_GROUP>(api.InviteToGroup("Daeva"));
		AssertPacket<CM_CHAT_MESSAGE_PUBLIC>(api.Say("hello"));
		AssertPacket<CM_CHAT_MESSAGE_WHISPER>(api.Whisper("Daeva", "hello"));
		AssertPacket<CM_DUEL_REQUEST>(api.Duel(50));
		AssertPacket<CM_REVIVE>(api.Revive());
		AssertPacket<CM_QUIT>(api.Quit(stayConnected: false));
		Assert.Equal(BotConnectionCommand.Crash, new BotApi().Crash());
	}

	[Theory]
	[InlineData("STR_GATHER_OUT_OF_SKILL_POINT")]
	[InlineData("STR_GATHER_TOO_FAR_FROM_GATHER_SOURCE")]
	[InlineData("STR_GATHER_INVENTORY_IS_FULL")]
	public void GatheringRefusalReleasesTheClientMovementGate(string message)
	{
		var api = new BotApi();
		api.Gather(10);
		api.Observe(Packet<SM_SYSTEM_MESSAGE>(("msgId", 0), ("name", message), ("params", Array.Empty<string>()),
			("specialParams", Array.Empty<string>()), ("senderObjectId", 0)));
		Assert.DoesNotContain(BotBlockingActivity.Gathering, api.Timing.BlockingActivities);
		AssertPacket<CM_MOVE>(api.MoveTo(new MovementPacketData(1, 2, 3, 0, 0)));
	}

	[Fact]
	public void UnrelatedSystemMessageDoesNotReleaseGathering()
	{
		var api = new BotApi();
		api.Gather(10);
		api.Observe(Packet<SM_SYSTEM_MESSAGE>(("msgId", 0), ("name", "STR_GET_EXP"), ("params", Array.Empty<string>()),
			("specialParams", Array.Empty<string>()), ("senderObjectId", 0)));
		Assert.Contains(BotBlockingActivity.Gathering, api.Timing.BlockingActivities);
	}

	[Theory]
	[InlineData(0, true)]
	[InlineData(1, true)]
	[InlineData(3, true)]
	[InlineData(4, false)]
	[InlineData(5, false)]
	[InlineData(6, false)]
	public void CraftingKeepsMovementBlockedUntilATerminalUpdate(byte action, bool blocked)
	{
		var api = new BotApi();
		api.Craft(150000009, 155004206, 123, [(182290205, 1L)]);
		api.Observe(Packet<SM_CRAFT_UPDATE>(("action", action)));
		Assert.Equal(blocked, api.Timing.BlockingActivities.Contains(BotBlockingActivity.Crafting));
	}

	[Fact]
	public void CraftDefaultsToNormalRatherThanConsumingABooster()
	{
		var api = new BotApi();
		var packet = api.Craft(150000009, 155004206, 123, [(182290205, 1L)]);
		Assert.Equal((byte)0, packet.Body[15]); // after unk, three ids and material count
	}

	[Fact]
	public void ObserveUpdatesWorldTimingAndProducesReflexes()
	{
		var world = new BotWorldModel();
		var api = new BotApi(world, reflexes: new BotReflexes(questionPolicy: _ => 1));
		var stats = Packet<SM_STATS_INFO>(
			("objectId", 100), ("level", (ushort)1), ("expNeeded", 1L), ("expRecoverable", 0L), ("expShown", 0L),
			("maxHp", 100), ("currentHp", 100), ("maxMp", 100), ("currentMp", 100),
			("maxDp", (ushort)4000), ("dp", (ushort)0), ("maxFp", 60), ("currentFp", 60));
		Assert.Null(api.Observe(stats));
		Assert.Equal(100, world.SelfObjectId);

		var spawnResponse = Assert.IsType<BotClientPacket>(api.Observe(Packet<SM_PLAYER_SPAWN>(
			("worldId", 210010000), ("x", 1f), ("y", 2f), ("z", 3f), ("heading", (byte)4))));
		AssertPacket<CM_LEVEL_READY>(spawnResponse);

		var question = Packet<SM_QUESTION_WINDOW>(
			("code", 60000), ("params", new[] { "Daeva", "", "" }), ("senderId", 20), ("rangeOrCooldownSeconds", 0));
		AssertPacket<CM_QUESTION_RESPONSE>(Assert.IsType<BotClientPacket>(api.Observe(question)));
		AssertPacket<CM_QUESTION_RESPONSE>(api.Answer(1));

		api.Gather(10);
		Assert.Contains(BotBlockingActivity.Gathering, api.Timing.BlockingActivities);
		api.Observe(Packet<SM_GATHER_UPDATE>(("action", (byte)6)));
		Assert.DoesNotContain(BotBlockingActivity.Gathering, api.Timing.BlockingActivities);
	}

	[Fact]
	public void FacadeExposesEveryPlannedIntentName()
	{
		var names = typeof(BotApi).GetMethods().Select(method => method.Name).ToHashSet(StringComparer.Ordinal);
		foreach (var expected in new[]
		{
			"Login", "ListCharacters", "CreateCharacter", "DeleteCharacter", "RestoreCharacter", "EnterWorld",
			"ChangeChannel", "Quit", "Crash", "MoveTo", "Jump", "Fly", "Land", "Glide", "Rest", "Emote",
			"Target", "Attack", "Cast", "SummonCommand", "SummonAttack", "SummonCast", "UseItem", "Equip",
			"Loot", "TalkTo", "SelectDialog", "CloseDialog", "Answer", "Teleport", "Gather", "Craft", "Buy",
			"Sell", "TradeRequest", "TradeAddItem", "TradeAddKinah", "TradeLock", "TradeAccept", "TradeCancel",
			"InviteToGroup", "Say", "Whisper", "Duel", "Revive",
		})
			Assert.Contains(expected, names);
	}

	private static void AssertPacket<T>(BotClientPacket packet) => Assert.Equal(typeof(T), packet.PacketType);

	private static DecodedBotServerPacket Packet<T>(params (string Name, object? Value)[] fields) =>
		new(typeof(T), fields.ToDictionary(field => field.Name, field => field.Value, StringComparer.Ordinal));
}
