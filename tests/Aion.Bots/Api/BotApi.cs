using Aion.Bots.Protocol;
using Aion.Bots.Protocol.Login;
using Aion.Bots.Reflexes;
using Aion.Bots.Scenarios;
using Aion.Bots.Timing;
using Aion.Bots.World;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.Bots.Api;

/// <summary>Intent-level bot API over the protocol writers, world model, reflexes and timing contract.</summary>
public sealed class BotApi
{
	private readonly BotMotionTiming? motionTiming;

	public BotApi(BotWorldModel? world = null, BotTimingContract? timing = null, BotReflexes? reflexes = null,
		BotMotionTiming? motionTiming = null, QuestDialogEchoDetector? questDialogEchoes = null)
	{
		World = world ?? new BotWorldModel();
		Timing = timing ?? new BotTimingContract();
		Reflexes = reflexes ?? new BotReflexes();
		QuestDialogEchoes = questDialogEchoes ?? new QuestDialogEchoDetector();
		this.motionTiming = motionTiming;
	}

	public BotWorldModel World { get; private set; }

	/// <summary>A new login must reconstruct observations from this connection, including empty journals.
	/// Keep timing restrictions: reconnecting does not erase a skill or item use delay.</summary>
	public void BeginLoginObservation()
	{
		World = new BotWorldModel();
		Timing.BeginLoginObservation();
		QuestDialogEchoes.BeginLoginObservation();
	}
	public BotTimingContract Timing { get; }
	public BotReflexes Reflexes { get; }
	public QuestDialogEchoDetector QuestDialogEchoes { get; }

	public async Task<BotLoginSession> Login(Stream stream, string username, string password,
		CancellationToken cancellationToken = default)
	{
		var protocol = await LoginClientProtocol.ReadInitAsync(stream, cancellationToken);
		var result = await protocol.LoginAsync(stream, username, password, cancellationToken);
		return new BotLoginSession(protocol, result);
	}

	public BotClientPacket ListCharacters(int playOk2) => GameClientPackets.CharacterList(playOk2);

	public BotClientPacket CreateCharacter(CharacterCreationData character) => GameClientPackets.CreateCharacter(character);

	public BotClientPacket DeleteCharacter(int playOk2, int characterObjectId) =>
		GameClientPackets.DeleteCharacter(playOk2, characterObjectId);

	public BotClientPacket RestoreCharacter(int playOk2, int characterObjectId) =>
		GameClientPackets.RestoreCharacter(playOk2, characterObjectId);

	public BotClientPacket EnterWorld(int characterObjectId)
	{
		Timing.EnsureCanEnterWorld();
		return GameClientPackets.EnterWorld(characterObjectId);
	}

	public BotClientPacket ChangeChannel(int channel) => GameClientPackets.ChangeChannel(channel);

	public BotClientPacket Quit(bool stayConnected)
	{
		Timing.RecordLeftWorld(crashed: false);
		return GameClientPackets.Quit(stayConnected);
	}

	public BotConnectionCommand Crash()
	{
		Timing.RecordLeftWorld(crashed: true);
		return BotConnectionCommand.Crash;
	}

	public BotClientPacket MoveTo(MovementPacketData movement)
	{
		Timing.EnsureCanMove();
		return GameClientPackets.Move(movement);
	}

	public IReadOnlyList<BotClientPacket> Jump(MovementPacketData movement)
	{
		Timing.EnsureCanMove();
		return [GameClientPackets.Emotion((byte)EmotionType.JUMP), GameClientPackets.Move(movement)];
	}

	public BotClientPacket Fly() => GameClientPackets.Emotion((byte)EmotionType.FLY);

	public IReadOnlyList<BotClientPacket> Fly(int worldId, float x, float y, float z, byte heading, int distance) =>
		[Fly(), GameClientPackets.MoveInAir(worldId, x, y, z, heading, distance)];

	public BotClientPacket Land() => GameClientPackets.Emotion((byte)EmotionType.LAND);

	public IReadOnlyList<BotClientPacket> Glide(MovementPacketData movement)
	{
		Timing.EnsureCanMove();
		return [GameClientPackets.Emotion((byte)EmotionType.START_GLIDE), GameClientPackets.Move(movement)];
	}

	public BotClientPacket Rest(bool sitting) =>
		GameClientPackets.Emotion((byte)(sitting ? EmotionType.SIT : EmotionType.STAND));

	public BotClientPacket Walk(bool walking) =>
		GameClientPackets.Emotion((byte)(walking ? EmotionType.WALK : EmotionType.RUN));

	public BotClientPacket Emote(ushort emotionId, int targetObjectId = 0) =>
		GameClientPackets.Emotion((byte)EmotionType.EMOTE, emotionId, targetObjectId);

	public BotClientPacket Target(int targetObjectId, bool selectTargetOfTarget = false)
	{
		Timing.RecordTargetSelection(targetObjectId);
		return GameClientPackets.TargetSelect(targetObjectId, selectTargetOfTarget);
	}

	public BotClientPacket Attack(int targetObjectId, int attackSpeedMillis, byte attackNumber = 0,
		ushort clientTime = 0, byte type = 0)
	{
		if (Timing.SelectedTargetId != targetObjectId)
			throw new InvalidOperationException($"Target({targetObjectId}) must precede Attack({targetObjectId}).");
		Timing.RecordAttack(attackSpeedMillis);
		return GameClientPackets.Attack(targetObjectId, attackNumber, clientTime, type);
	}

	public BotClientPacket Cast(SpellCastData cast, SkillTemplate? skill = null, BotMotionProfile? profile = null,
		int ammoTravelMillis = 0)
	{
		if (skill != null || profile != null)
		{
			if (skill == null || profile == null || motionTiming == null)
				throw new InvalidOperationException("Skill, motion profile and BotMotionTiming are all required to calculate client hit time.");
			var hitTime = motionTiming.CalculateClientHitTime(skill, profile, ammoTravelMillis);
			cast = cast with { HitTime = checked((ushort)hitTime) };
		}
		int? target = cast.TargetType is 0 or 3 or 4 ? cast.TargetObjectId : null;
		Timing.RecordCastStarted(cast.SkillId, target);
		return GameClientPackets.CastSpell(cast);
	}

	public BotClientPacket SummonCommand(byte mode, int targetObjectId, int unknown1 = 0, int unknown2 = 0) =>
		GameClientPackets.SummonCommand(mode, targetObjectId, unknown1, unknown2);

	public BotClientPacket SummonAttack(int summonObjectId, int targetObjectId, byte unknown1 = 0,
		ushort clientTime = 0, byte unknown3 = 0) =>
		GameClientPackets.SummonAttack(summonObjectId, targetObjectId, unknown1, clientTime, unknown3);

	public BotClientPacket SummonCast(int summonObjectId, ushort skillId, byte skillLevel, int targetObjectId) =>
		GameClientPackets.SummonCastSpell(summonObjectId, skillId, skillLevel, targetObjectId);

	public BotClientPacket UseItem(int itemObjectId, ItemTemplate template, byte type = 0, int argument = 0)
	{
		Timing.RecordItemUse(template);
		return GameClientPackets.UseItem(itemObjectId, type, argument);
	}

	public BotClientPacket Equip(byte action, long slot, int itemObjectId) =>
		GameClientPackets.EquipItem(action, slot, itemObjectId);

	public BotClientPacket EnchantItem(int itemObjectId, int stoneObjectId, int supplementObjectId = 0) =>
		BeginGearUse(GameClientPackets.EnchantItem(itemObjectId, stoneObjectId, supplementObjectId));
	public BotClientPacket SocketManastone(int itemObjectId, int stoneObjectId, bool fusion = false, int supplementObjectId = 0) =>
		BeginGearUse(GameClientPackets.SocketManastone(itemObjectId, stoneObjectId, fusion, supplementObjectId));
	public BotClientPacket SocketGodstone(int itemObjectId, int stoneObjectId) =>
		BeginGearUse(GameClientPackets.SocketGodstone(itemObjectId, stoneObjectId));
	public BotClientPacket UnwrapItem(int itemObjectId)
	{
		Timing.EnsureCanMove();
		return GameClientPackets.UnwrapItem(itemObjectId);
	}
	public BotClientPacket SelectDecomposable(int itemObjectId, byte index)
	{
		Timing.EnsureCanMove();
		return GameClientPackets.SelectDecomposable(itemObjectId, index);
	}
	public BotClientPacket PurifyItem(int itemObjectId, int resultItemId, params int[] materialObjectIds)
	{
		Timing.EnsureCanMove();
		return GameClientPackets.PurifyItem(World.SelfObjectId ?? throw new InvalidOperationException("Enter the world before purifying an item."),
			itemObjectId, resultItemId, materialObjectIds);
	}
	public BotClientPacket RemodelItem(int npcObjectId, int keepItemObjectId, int extractItemObjectId)
	{
		Timing.EnsureCanMove();
		return GameClientPackets.RemodelItem(npcObjectId, keepItemObjectId, extractItemObjectId);
	}
	public BotClientPacket TuneItem(int itemObjectId, int scrollObjectId = 0) =>
		BeginGearUse(GameClientPackets.TuneItem(itemObjectId, scrollObjectId));
	public BotClientPacket TuneResult(int itemObjectId, bool accepted)
	{
		Timing.EnsureCanMove();
		return GameClientPackets.TuneResult(itemObjectId, accepted);
	}
	public BotClientPacket ChargeItems(int npcObjectId, byte level, params int[] itemObjectIds)
	{
		Timing.EnsureCanMove();
		if (Timing.SelectedTargetId != npcObjectId) throw new InvalidOperationException("Select the conditioning NPC before charging items.");
		return GameClientPackets.ChargeItems(npcObjectId, level, itemObjectIds);
	}
	public BotClientPacket FuseWeapons(int npcObjectId, int mainWeaponObjectId, int secondaryWeaponObjectId)
	{
		EnsureArmsfusionTarget(npcObjectId);
		return GameClientPackets.FuseWeapons(npcObjectId, mainWeaponObjectId, secondaryWeaponObjectId);
	}
	public BotClientPacket BreakWeapons(int npcObjectId, int weaponObjectId)
	{
		EnsureArmsfusionTarget(npcObjectId);
		return GameClientPackets.BreakWeapons(npcObjectId, weaponObjectId);
	}
	private void EnsureArmsfusionTarget(int npcObjectId)
	{
		Timing.EnsureCanMove();
		if (Timing.SelectedTargetId != npcObjectId)
			throw new InvalidOperationException("Select the armsfusion officer before fusing or breaking weapons.");
	}
	public BotClientPacket RemoveManastone(int npcObjectId, int itemObjectId, byte slot, bool fusion = false)
	{
		Timing.EnsureCanMove(); // No outstanding cast, gathering, crafting or item-use operation.
		if (Timing.SelectedTargetId != npcObjectId)
			throw new InvalidOperationException("Select the manastone-removal NPC before requesting removal.");
		return GameClientPackets.RemoveManastone(npcObjectId, itemObjectId, slot, fusion);
	}
	private BotClientPacket BeginGearUse(BotClientPacket packet)
	{
		Timing.EnsureCanMove();
		Timing.SetActivity(BotBlockingActivity.ItemUse, true);
		return packet;
	}

	public BotClientPacket Loot(int targetObjectId, byte? itemIndex = null, bool close = false)
	{
		if (close && itemIndex != null)
			throw new ArgumentException("Closing a loot window cannot also select an item.", nameof(itemIndex));
		return itemIndex == null
			? GameClientPackets.StartLoot(targetObjectId, close ? (byte)1 : (byte)0)
			: GameClientPackets.LootItem(targetObjectId, itemIndex.Value);
	}

	public BotClientPacket TalkTo(int targetObjectId) => GameClientPackets.ShowDialog(targetObjectId);
	public BotClientPacket ShareQuest(int questId) => GameClientPackets.ShareQuest(questId);
	public BotClientPacket AcceptSharedQuest()
	{
		var share = World.PendingQuestShare ?? throw new InvalidOperationException("No quest share was received.");
		// Shared-quest acceptance targets the other player, not the original NPC or self.
		return SelectDialog(share.SharerId, DialogAction.QUEST_ACCEPT_SIMPLE, questId: share.QuestId);
	}

	public BotClientPacket SelectDialog(int targetObjectId, ushort actionId, ushort rewardIndex = 0,
		ushort lastPage = 0, int questId = 0)
	{
		QuestDialogEchoes.Record(targetObjectId, actionId, questId);
		return GameClientPackets.DialogSelect(targetObjectId, actionId, rewardIndex, lastPage, questId);
	}

	public BotClientPacket SelectDialogExpectRejection(int targetObjectId, ushort actionId, ushort rewardIndex = 0,
		ushort lastPage = 0, int questId = 0)
	{
		QuestDialogEchoes.Record(targetObjectId, actionId, questId, expectEcho: true);
		return GameClientPackets.DialogSelect(targetObjectId, actionId, rewardIndex, lastPage, questId);
	}

	public BotClientPacket CloseDialog(int targetObjectId) => GameClientPackets.CloseDialog(targetObjectId);
	public BotClientPacket DeleteQuest(int questId) => GameClientPackets.DeleteQuest(questId);

	public BotClientPacket Answer(byte response)
	{
		var question = World.Question ?? throw new InvalidOperationException("There is no open question window to answer.");
		return GameClientPackets.QuestionResponse(question.Code, response, question.SenderId);
	}

	public BotClientPacket Teleport(int targetObjectId, int locationId) =>
		GameClientPackets.TeleportSelect(targetObjectId, locationId);

	public IReadOnlyList<BotClientPacket> Gather(int targetObjectId, bool start = true)
	{
		if (!start)
		{
			Timing.SetActivity(BotBlockingActivity.Gathering, false);
			return [GameClientPackets.Gather(-1)];
		}
		Timing.RecordTargetSelection(targetObjectId);
		Timing.SetActivity(BotBlockingActivity.Gathering, true);
		return [GameClientPackets.TargetSelect(targetObjectId), GameClientPackets.Gather(0)];
	}

	public BotClientPacket Craft(int targetTemplateId, int recipeId, int targetObjectId,
		IReadOnlyList<(int ItemId, long Count)> materials, byte craftType = 0, byte unknown = 0)
	{
		Timing.SetActivity(BotBlockingActivity.Crafting, true);
		return GameClientPackets.Craft(unknown, targetTemplateId, recipeId, targetObjectId, craftType, materials);
	}

	public BotClientPacket Buy(int sellerObjectId, IReadOnlyList<(int ItemId, long Count)> items) =>
		GameClientPackets.BuyItem(sellerObjectId, 13, items);

	public BotClientPacket Sell(int sellerObjectId, IReadOnlyList<(int ItemId, long Count)> items) =>
		GameClientPackets.BuyItem(sellerObjectId, 1, items);

	public BotClientPacket TradeRequest(int targetObjectId) => GameClientPackets.ExchangeRequest(targetObjectId);
	public BotClientPacket TradeAddItem(int itemObjectId, int count) => GameClientPackets.ExchangeAddItem(itemObjectId, count);
	public BotClientPacket TradeAddKinah(long count) => GameClientPackets.ExchangeAddKinah(count);
	public BotClientPacket TradeLock() => GameClientPackets.ExchangeLock();
	public BotClientPacket TradeAccept() => GameClientPackets.ExchangeOk();
	public BotClientPacket TradeCancel() => GameClientPackets.ExchangeCancel();
	public BotClientPacket SendMail(string recipient, string title, string message, int itemObjectId, long itemCount, long kinah, byte letterType = 0) =>
		GameClientPackets.SendMail(recipient, title, message, itemObjectId, itemCount, kinah, letterType);
	public BotClientPacket CheckMailList(bool expressOnly = false) => GameClientPackets.CheckMailList(expressOnly);
	public BotClientPacket ReadMail(int letterId) => GameClientPackets.ReadMail(letterId);
	public BotClientPacket GetMailAttachment(int letterId, byte attachmentType) => GameClientPackets.GetMailAttachment(letterId, attachmentType);
	public BotClientPacket DeleteMail(int letterId) => GameClientPackets.DeleteMail(letterId);

	public BotClientPacket InviteToGroup(string playerName, byte inviteType = 0) =>
		GameClientPackets.InviteToGroup(inviteType, playerName);

	public BotClientPacket Say(string message, byte chatType = 0) => GameClientPackets.ChatMessagePublic(chatType, message);

	public BotClientPacket Whisper(string playerName, string message) =>
		GameClientPackets.ChatMessageWhisper(playerName, message);

	public BotClientPacket Duel(int targetObjectId) => GameClientPackets.DuelRequest(targetObjectId);
	public BotClientPacket LeaveGroup() => GameClientPackets.TeamCommand(6);
	public BotClientPacket InviteToAlliance(string playerName) => GameClientPackets.InviteToGroup(12, playerName);
	public BotClientPacket SetAllianceLeader(int objectId) => GameClientPackets.TeamCommand(17, objectId);
	public BotClientPacket LeaveAlliance() => GameClientPackets.TeamCommand(14);
	public BotClientPacket InviteToLeague(string playerName) => GameClientPackets.InviteToGroup(28, playerName);
	public BotClientPacket LeaveLeague() => GameClientPackets.TeamCommand(29);
	public BotClientPacket SetGroupLoot(byte lootRule, int distribution = 0) =>
		GameClientPackets.DistributionSettings(lootRule, 0, distribution, distribution, distribution, distribution, distribution, distribution);
	public BotClientPacket RollForLoot(int groupId, int index, int itemId, int corpseId, bool roll = true) =>
		GameClientPackets.GroupLoot(groupId, index, itemId, corpseId, 2, roll);
	public BotClientPacket CreateLegion(string name) => GameClientPackets.Legion(0, first: name);
	public BotClientPacket InviteToLegion(string playerName) => GameClientPackets.Legion(1, first: playerName);
	public BotClientPacket RequestGroupListings(bool applications = false) => GameClientPackets.FindGroupList(applications);
	public BotClientPacket PostGroupRecruitment(string message, byte groupType = 0, bool update = false, byte serverId = 1) =>
		GameClientPackets.FindGroupRecruitment(FindGroupObjectId, message, groupType, update, serverId, FindGroupSoloFlag);
	public BotClientPacket PostGroupApplication(string message, byte playerClass, byte groupType = 0, bool update = false) =>
		GameClientPackets.FindGroupApplication(World.SelfObjectId ?? throw new InvalidOperationException("Not in world."),
			message, playerClass, checked((byte)World.Level), groupType, update);
	public BotClientPacket RemoveGroupListing(bool application = false, byte serverId = 1) =>
		GameClientPackets.FindGroupRemove(application ? World.SelfObjectId ?? throw new InvalidOperationException("Not in world.") :
			FindGroupObjectId, application, serverId, FindGroupSoloFlag);
	private int FindGroupObjectId => World.AllianceId ?? World.GroupId ?? World.SelfObjectId ?? throw new InvalidOperationException("Not in world.");
	private byte FindGroupSoloFlag => (byte)(World.AllianceId == null && World.GroupId == null ? 16 : 0);
	public BotClientPacket AnswerRecall(bool accept)
	{
		if (World.RecallRequest == null) throw new InvalidOperationException("No recall request was received.");
		World.CloseRecallPrompt();
		return GameClientPackets.RecallAnswer(accept);
	}
	public BotClientPacket SetLegionEmblem(int legionId, byte emblemId, byte emblemType, byte alpha, byte red, byte green, byte blue) =>
		GameClientPackets.LegionEmblem(legionId, emblemId, emblemType, alpha, red, green, blue);
	public BotClientPacket RequestLegionHistory(int page = 0, byte type = 0) => GameClientPackets.LegionHistory(page, type);
	public BotClientPacket DepositLegionKinah(long amount) => GameClientPackets.LegionWarehouseKinah(amount, deposit: true);
	public BotClientPacket WithdrawLegionKinah(long amount) => GameClientPackets.LegionWarehouseKinah(amount, deposit: false);
	public BotClientPacket RequestFriendList() => GameClientPackets.ShowFriendList();
	public BotClientPacket AddFriend(string playerName, string message) => GameClientPackets.FriendAdd(playerName, message);
	public BotClientPacket DeleteFriend(string playerName) => GameClientPackets.FriendDelete(playerName);
	public BotClientPacket SetFriendMemo(string playerName, string memo) => GameClientPackets.FriendMemo(playerName, memo);
	public BotClientPacket BlockPlayer(string playerName, string reason) => GameClientPackets.BlockAdd(playerName, reason);
	public BotClientPacket UnblockPlayer(string playerName) => GameClientPackets.BlockDelete(playerName);
	public BotClientPacket SetBlockReason(string playerName, string reason) => GameClientPackets.BlockReason(playerName, reason);

	public BotClientPacket Revive(BotReviveType type = BotReviveType.Bind) => GameClientPackets.Revive((byte)type);

	public BotClientPacket? Observe(DecodedBotServerPacket packet, int animationLastHitMillis = 0)
	{
		World.Apply(packet);
		QuestDialogEchoes.Observe(packet);
		if (packet.PacketType == typeof(SM_SKILL_COOLDOWN))
			Timing.ApplySkillCooldowns(packet);
		else if (packet.PacketType == typeof(SM_CASTSPELL_RESULT) &&
			(World.SelfObjectId == null || packet.Get<int>("effectorId") == World.SelfObjectId))
			Timing.RecordCastResult(animationLastHitMillis);
		else if (packet.PacketType == typeof(SM_SKILL_CANCEL) &&
			(World.SelfObjectId == null || packet.Get<int>("objectId") == World.SelfObjectId))
			Timing.RecordCastCancelled();
		else if (packet.PacketType == typeof(SM_GATHER_UPDATE) && packet.Get<byte>("action") >= 5)
			Timing.SetActivity(BotBlockingActivity.Gathering, false);
		else if (packet.PacketType == typeof(SM_CRAFT_UPDATE) && packet.Get<byte>("action") >= 4)
			Timing.SetActivity(BotBlockingActivity.Crafting, false);
		else if (packet.PacketType == typeof(SM_ITEM_USAGE_ANIMATION) && packet.Get<int>("playerObjId") == World.SelfObjectId)
		{
			// Java ItemUseAnimation: each family has its own start, success and cancellation stages.
			byte animation = packet.Get<byte>("animationId");
			if (animation is 0 or 4 or 9 or 12)
				Timing.SetActivity(BotBlockingActivity.ItemUse, true);
			else if (animation is 1 or 2 or 3 or 6 or 8 or 10 or 11 or 13 or 14)
				Timing.SetActivity(BotBlockingActivity.ItemUse, false);
		}
		else if (packet.PacketType == typeof(SM_SYSTEM_MESSAGE) &&
			packet.Get<string>("name") is "STR_GATHER_OUT_OF_SKILL_POINT" or
				"STR_GATHER_TOO_FAR_FROM_GATHER_SOURCE" or "STR_GATHER_INVENTORY_IS_FULL")
			Timing.SetActivity(BotBlockingActivity.Gathering, false);
		return Reflexes.RespondTo(packet);
	}
}

public sealed record BotLoginSession(LoginClientProtocol Protocol, LoginHandshakeResult Result);

public enum BotConnectionCommand
{
	Crash,
}
