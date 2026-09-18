using Aion.Bots.Protocol;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.Bots.World;

/// <summary>A client-side view of the world derived exclusively from decoded server packets.</summary>
public sealed class BotWorldModel
{
	public const int KinahItemId = 182_400_001;

	private readonly Dictionary<int, BotKnownObject> objects = [];
	private readonly Dictionary<int, BotInventoryItem> inventory = [];
	private readonly Dictionary<int, BotSkill> skills = [];
	private readonly Dictionary<int, BotSkillCooldown> cooldowns = [];
	private readonly Dictionary<int, BotQuestState> quests = [];
	private readonly Dictionary<int, BotCompletedQuest> completedQuests = [];
	private readonly List<BotSystemMessage> systemMessages = [];

	public IReadOnlyDictionary<int, BotKnownObject> Objects => objects;
	public IReadOnlyDictionary<int, BotInventoryItem> Inventory => inventory;
	public IReadOnlyDictionary<int, BotSkill> Skills => skills;
	public IReadOnlyDictionary<int, BotSkillCooldown> Cooldowns => cooldowns;
	public IReadOnlyDictionary<int, BotQuestState> Quests => quests;
	public IReadOnlyDictionary<int, BotCompletedQuest> CompletedQuests => completedQuests;
	public IReadOnlyList<BotSystemMessage> SystemMessages => systemMessages;

	public int? SelfObjectId { get; private set; }
	public int? MapId { get; private set; }
	public BotPosition? Position { get; private set; }
	public ushort Level { get; private set; }
	public int CurrentHp { get; private set; }
	public int MaxHp { get; private set; }
	public int CurrentMp { get; private set; }
	public int MaxMp { get; private set; }
	public ushort CurrentDp { get; private set; }
	public ushort MaxDp { get; private set; }
	public int CurrentFlightTime { get; private set; }
	public int MaxFlightTime { get; private set; }
	public float? MovementSpeed { get; private set; }
	public long CurrentExperience { get; private set; }
	public long RecoverableExperience { get; private set; }
	public long ExperienceNeeded { get; private set; }
	public bool IsDead { get; private set; }
	public BotReviveOptions? ReviveOptions { get; private set; }
	public long Kinah => inventory.Values.Where(item => item.ItemId == KinahItemId).Sum(item => item.Count);

	public BotDialogWindow? Dialog { get; private set; }
	public BotQuestionWindow? Question { get; private set; }
	public BotLootWindow? Loot { get; private set; }
	public BotTradeWindow? Trade { get; private set; }
	public BotQuestShare? PendingQuestShare { get; private set; }
	public string? ExchangeRequestFrom { get; private set; }

	/// <summary>Forget object ids that become invalid when the server rebuilds the player's visible world.</summary>
	public void BeginWorldReload()
	{
		objects.Clear();
		Dialog = null;
		Question = null;
		Loot = null;
		Trade = null;
	}

	public void Apply(DecodedBotServerPacket packet)
	{
		var type = packet.PacketType;
		if (type == typeof(SM_PLAYER_SPAWN))
			ApplyPlayerSpawn(packet);
		else if (type == typeof(SM_PLAYER_INFO))
			ApplyPlayerInfo(packet);
		else if (type == typeof(SM_EMOTION))
			ApplyEmotion(packet);
		else if (type == typeof(SM_NPC_INFO))
			ApplyNpcInfo(packet);
		else if (type == typeof(SM_GATHERABLE_INFO))
			ApplyGatherableInfo(packet);
		else if (type == typeof(SM_MOVE))
			ApplyMove(packet);
		else if (type == typeof(SM_DELETE))
			objects.Remove(packet.Get<int>("objectId"));
		else if (type == typeof(SM_TELEPORT_LOC))
			ApplyTeleport(packet);
		else if (type == typeof(SM_STATS_INFO))
			ApplyStats(packet);
		else if (type == typeof(SM_STATUPDATE_HP))
			ApplyHp(packet);
		else if (type == typeof(SM_STATUPDATE_MP))
			ApplyMp(packet);
		else if (type == typeof(SM_STATUPDATE_DP))
			CurrentDp = packet.Get<ushort>("currentDp");
		else if (type == typeof(SM_STATUPDATE_EXP))
			ApplyExperience(packet);
		else if (type == typeof(SM_FLY_TIME))
			ApplyFlightTime(packet);
		else if (type == typeof(SM_DIE))
			ApplyDeath(packet);
		else if (type == typeof(SM_INVENTORY_INFO))
			ApplyInventoryInfo(packet);
		else if (type == typeof(SM_INVENTORY_ADD_ITEM))
			UpsertInventoryItems(packet.Get<List<IReadOnlyDictionary<string, object?>>>("items"));
		else if (type == typeof(SM_INVENTORY_UPDATE_ITEM))
			ApplyInventoryUpdate(packet);
		else if (type == typeof(SM_DELETE_ITEM))
			inventory.Remove(packet.Get<int>("itemObjectId"));
		else if (type == typeof(SM_SKILL_LIST))
			ApplySkillList(packet);
		else if (type == typeof(SM_SKILL_COOLDOWN))
			ApplySkillCooldowns(packet);
		else if (type == typeof(SM_QUEST_LIST))
			ApplyQuestList(packet);
		else if (type == typeof(SM_QUEST_ACTION))
			ApplyQuestAction(packet);
		else if (type == typeof(SM_QUEST_COMPLETED_LIST))
			ApplyCompletedQuests(packet);
		else if (type == typeof(SM_DIALOG_WINDOW))
			ApplyDialog(packet);
		else if (type == typeof(SM_QUESTION_WINDOW))
			Question = new BotQuestionWindow(packet.Get<int>("code"), packet.Get<string[]>("params"),
				packet.Get<int>("senderId"), packet.Get<int>("rangeOrCooldownSeconds"));
		else if (type == typeof(SM_CLOSE_QUESTION_WINDOW))
			Question = null;
		else if (type == typeof(SM_LOOT_STATUS))
			ApplyLootStatus(packet);
		else if (type == typeof(SM_LOOT_ITEMLIST))
			ApplyLootItems(packet);
		else if (type == typeof(SM_TRADELIST))
			ApplyTrade(packet);
		else if (type == typeof(SM_SYSTEM_MESSAGE))
			ApplySystemMessage(packet);
		else if (type == typeof(SM_EXCHANGE_REQUEST))
			ExchangeRequestFrom = packet.Get<string>("receiver");
	}

	private void ApplyPlayerSpawn(DecodedBotServerPacket packet)
	{
		MapId = packet.Get<int>("worldId");
		Position = ReadPosition(packet.Fields);
		UpdateSelfObjectPosition();
	}

	private void ApplyPlayerInfo(DecodedBotServerPacket packet)
	{
		var objectId = packet.Get<int>("objectId");
		var position = ReadPosition(packet.Fields);
		var movementSpeed = GetNullableStruct<float>(packet.Fields, "movementSpeed");
		objects[objectId] = new BotKnownObject(objectId, BotKnownObjectKind.Player, position,
			Name: packet.Get<string>("name"), State: packet.Get<ushort>("state"), Race: packet.Get<byte>("race"),
			PlayerClass: packet.Get<byte>("playerClass"), MovementSpeed: movementSpeed);
		if (SelfObjectId == objectId)
		{
			Position = position;
			MovementSpeed = movementSpeed;
		}
	}

	private void ApplyEmotion(DecodedBotServerPacket packet)
	{
		var objectId = packet.Get<int>("senderObjectId");
		var movementSpeed = packet.Get<float>("movementSpeed");
		if (movementSpeed <= 0)
			return; // Some state-only SM_EMOTION constructors have no creature and therefore serialize speed 0.
		if (objects.TryGetValue(objectId, out var known))
			objects[objectId] = known with { MovementSpeed = movementSpeed };
		if (SelfObjectId == objectId)
			MovementSpeed = movementSpeed;
	}

	private void ApplyNpcInfo(DecodedBotServerPacket packet)
	{
		var objectId = packet.Get<int>("objectId");
		objects[objectId] = new BotKnownObject(objectId, BotKnownObjectKind.Npc, ReadPosition(packet.Fields, 0),
			TemplateId: packet.Get<int>("npcId"), VisualTemplateId: packet.Get<int>("visualNpcId"));
	}

	private void ApplyGatherableInfo(DecodedBotServerPacket packet)
	{
		var objectId = packet.Get<int>("objectId");
		var isStatic = packet.Get<bool>("isStatic");
		objects[objectId] = new BotKnownObject(objectId,
			isStatic ? BotKnownObjectKind.Static : BotKnownObjectKind.Gatherable,
			ReadPosition(packet.Fields), TemplateId: packet.Get<int>("templateId"),
			StaticId: packet.Get<int>("staticId"), State: packet.Get<ushort>("objectState"),
			IsOpen: GetNullableStruct<bool>(packet.Fields, "open"));
	}

	private void ApplyMove(DecodedBotServerPacket packet)
	{
		var objectId = packet.Get<int>("objectId");
		var position = ReadPosition(packet.Fields);
		if (objects.TryGetValue(objectId, out var known))
			objects[objectId] = known with { Position = position };
		if (SelfObjectId == objectId)
			Position = position;
	}

	private void ApplyTeleport(DecodedBotServerPacket packet)
	{
		MapId = packet.Get<int>("mapId");
		Position = ReadPosition(packet.Fields);
		UpdateSelfObjectPosition();
	}

	private void ApplyStats(DecodedBotServerPacket packet)
	{
		SelfObjectId = packet.Get<int>("objectId");
		if (objects.TryGetValue(SelfObjectId.Value, out var self))
			MovementSpeed = self.MovementSpeed;
		Level = packet.Get<ushort>("level");
		ExperienceNeeded = packet.Get<long>("expNeeded");
		RecoverableExperience = packet.Get<long>("expRecoverable");
		CurrentExperience = packet.Get<long>("expShown");
		MaxHp = packet.Get<int>("maxHp");
		CurrentHp = packet.Get<int>("currentHp");
		MaxMp = packet.Get<int>("maxMp");
		CurrentMp = packet.Get<int>("currentMp");
		MaxDp = packet.Get<ushort>("maxDp");
		CurrentDp = packet.Get<ushort>("dp");
		MaxFlightTime = packet.Get<int>("maxFp");
		CurrentFlightTime = packet.Get<int>("currentFp");
		IsDead = CurrentHp <= 0;
		if (!IsDead)
			ReviveOptions = null;
	}

	private void ApplyHp(DecodedBotServerPacket packet)
	{
		CurrentHp = packet.Get<int>("currentHp");
		MaxHp = packet.Get<int>("maxHp");
		if (CurrentHp > 0)
		{
			IsDead = false;
			ReviveOptions = null;
		}
	}

	private void ApplyMp(DecodedBotServerPacket packet)
	{
		CurrentMp = packet.Get<int>("currentMp");
		MaxMp = packet.Get<int>("maxMp");
	}

	private void ApplyExperience(DecodedBotServerPacket packet)
	{
		CurrentExperience = packet.Get<long>("currentExp");
		RecoverableExperience = packet.Get<long>("recoverableExp");
		ExperienceNeeded = packet.Get<long>("maxExp");
	}

	private void ApplyFlightTime(DecodedBotServerPacket packet)
	{
		CurrentFlightTime = packet.Get<int>("currentFp");
		MaxFlightTime = packet.Get<int>("maxFp");
	}

	private void ApplyDeath(DecodedBotServerPacket packet)
	{
		IsDead = true;
		CurrentHp = 0;
		ReviveOptions = new BotReviveOptions(packet.Get<bool>("allowReviveBySkill"),
			packet.Get<bool>("allowReviveByItem"), packet.Get<int>("remainingKiskTimeSeconds"),
			packet.Get<bool>("allowInstanceRevive"), packet.Get<bool>("invasion"));
	}

	private void ApplyInventoryInfo(DecodedBotServerPacket packet)
	{
		if (packet.Get<bool>("firstPacket"))
			inventory.Clear();
		UpsertInventoryItems(packet.Get<List<IReadOnlyDictionary<string, object?>>>("items"));
	}

	private void UpsertInventoryItems(IEnumerable<IReadOnlyDictionary<string, object?>> items)
	{
		foreach (var item in items)
		{
			var objectId = Get<int>(item, "objectId");
			inventory[objectId] = new BotInventoryItem(objectId, Get<int>(item, "itemId"), Get<string>(item, "desc"),
				Get<long>(item, "itemCount"), Get<ushort>(item, "itemMask"), Get<string>(item, "itemCreator"),
				Get<ushort>(item, "equipmentSlot"), Get<bool>(item, "cloth"));
		}
	}

	private void ApplyInventoryUpdate(DecodedBotServerPacket packet)
	{
		var objectId = packet.Get<int>("objectId");
		if (!inventory.TryGetValue(objectId, out var existing))
			return;
		inventory[objectId] = existing with
		{
			Description = packet.Get<string>("desc"),
			Count = GetNullableStruct<long>(packet.Fields, "itemCount") ?? existing.Count,
			ItemMask = GetNullableStruct<ushort>(packet.Fields, "itemMask") ?? existing.ItemMask,
			Creator = GetNullableString(packet.Fields, "itemCreator") ?? existing.Creator,
		};
	}

	private void ApplySkillList(DecodedBotServerPacket packet)
	{
		if (packet.Get<bool>("silentUpdate"))
			skills.Clear();
		foreach (var entry in packet.Get<List<IReadOnlyDictionary<string, object?>>>("skills"))
		{
			var skillId = Get<ushort>(entry, "skillId");
			skills[skillId] = new BotSkill(skillId, Get<ushort>(entry, "level"), Get<byte>(entry, "reserved"),
				Get<byte>(entry, "professionBarSize"), Get<int>(entry, "flag"), Get<byte>(entry, "skillType"));
		}
	}

	private void ApplySkillCooldowns(DecodedBotServerPacket packet)
	{
		foreach (var entry in packet.Get<List<IReadOnlyDictionary<string, object?>>>("cooldowns"))
		{
			var skillId = Get<ushort>(entry, "skillId");
			var remaining = Get<int>(entry, "remainingSeconds");
			if (remaining <= 0)
				cooldowns.Remove(skillId);
			else
				cooldowns[skillId] = new BotSkillCooldown(skillId, remaining, Get<int>(entry, "durationMillis"));
		}
	}

	private void ApplyQuestList(DecodedBotServerPacket packet)
	{
		quests.Clear();
		foreach (var entry in packet.Get<List<IReadOnlyDictionary<string, object?>>>("quests"))
		{
			var questId = Get<int>(entry, "questId");
			quests[questId] = new BotQuestState(questId, Get<byte>(entry, "status"),
				Get<int>(entry, "stepAndFlags"), Get<byte>(entry, "completeCount"), null);
		}
	}

	private void ApplyQuestAction(DecodedBotServerPacket packet)
	{
		if (packet.Fields.ContainsKey("suppressed"))
			return;
		var action = packet.Get<byte>("action");
		var questId = packet.Get<int>("questId");
		switch (action)
		{
			case 1:
			case 2:
				var completeCount = quests.TryGetValue(questId, out var existing) ? existing.CompleteCount : (byte)0;
				quests[questId] = new BotQuestState(questId, packet.Get<byte>("status"),
					packet.Get<int>("stepAndFlags"), completeCount, existing?.TimerSeconds);
				break;
			case 3:
				quests.Remove(questId);
				break;
			case 4:
				var state = quests.TryGetValue(questId, out var timed) ? timed : new BotQuestState(questId, 0, 0, 0, null);
				quests[questId] = state with { TimerSeconds = packet.Get<int>("timer") };
				break;
			case 5:
				PendingQuestShare = new BotQuestShare(questId, packet.Get<int>("sharerId"), packet.Get<bool>("shareInAlliance"));
				break;
		}
	}

	private void ApplyCompletedQuests(DecodedBotServerPacket packet)
	{
		if (packet.Get<byte>("updateMode") == 0)
			completedQuests.Clear();
		foreach (var entry in packet.Get<List<IReadOnlyDictionary<string, object?>>>("quests"))
		{
			var questId = Get<int>(entry, "questId");
			completedQuests[questId] = new BotCompletedQuest(questId, Get<byte>(entry, "completeCount"),
				Get<bool>(entry, "nonRepeatable"));
		}
	}

	private void ApplyDialog(DecodedBotServerPacket packet)
	{
		var pageId = packet.Get<ushort>("dialogPageId");
		if (pageId == 0)
		{
			Dialog = null;
			Trade = null;
			return;
		}
		Dialog = new BotDialogWindow(packet.Get<int>("targetObjectId"), pageId, packet.Get<int>("questId"));
	}

	private void ApplyLootStatus(DecodedBotServerPacket packet)
	{
		var status = packet.Get<byte>("status");
		if (status == 2)
			Loot = new BotLootWindow(packet.Get<int>("targetObjectId"), []);
		else if (status == 3)
			Loot = null;
	}

	private void ApplyLootItems(DecodedBotServerPacket packet)
	{
		var items = packet.Get<List<IReadOnlyDictionary<string, object?>>>("items")
			.Select(item => new BotLootItem(Get<byte>(item, "index"), Get<int>(item, "itemId"),
				Get<int>(item, "count"), Get<bool>(item, "requiresConfirmation")))
			.ToArray();
		Loot = new BotLootWindow(packet.Get<int>("targetObjectId"), items);
	}

	private void ApplyTrade(DecodedBotServerPacket packet)
	{
		var limited = packet.Get<List<IReadOnlyDictionary<string, object?>>>("limitedItems")
			.Select(item => new BotLimitedTradeItem(Get<int>(item, "itemId"), Get<ushort>(item, "buyCount"),
				Get<ushort>(item, "sellLimit")))
			.ToArray();
		Trade = new BotTradeWindow(packet.Get<int>("targetObjectId"), packet.Get<byte>("tradeNpcType"),
			packet.Get<int>("buyPriceModifier"), packet.Get<bool>("showBuyTab"), packet.Get<bool>("showSellTab"),
			packet.Get<int[]>("tabs"), limited);
	}

	private void ApplySystemMessage(DecodedBotServerPacket packet)
	{
		systemMessages.Add(new BotSystemMessage(packet.Get<int>("msgId"), GetNullableString(packet.Fields, "name"),
			packet.Get<string[]>("params"), packet.Get<string[]>("specialParams"), packet.Get<int>("senderObjectId")));
	}

	private void UpdateSelfObjectPosition()
	{
		if (SelfObjectId is int objectId && Position is BotPosition position && objects.TryGetValue(objectId, out var known))
			objects[objectId] = known with { Position = position };
	}

	private static BotPosition ReadPosition(IReadOnlyDictionary<string, object?> fields, byte? heading = null) =>
		new(Get<float>(fields, "x"), Get<float>(fields, "y"), Get<float>(fields, "z"),
			heading ?? Get<byte>(fields, "heading"));

	private static T Get<T>(IReadOnlyDictionary<string, object?> fields, string name) =>
		fields.TryGetValue(name, out var value) && value is T typed
			? typed
			: throw new InvalidDataException($"Decoded packet field '{name}' was missing or was not {typeof(T).Name}.");

	private static T? GetNullableStruct<T>(IReadOnlyDictionary<string, object?> fields, string name) where T : struct =>
		fields.TryGetValue(name, out var value) && value is T typed ? typed : null;

	private static string? GetNullableString(IReadOnlyDictionary<string, object?> fields, string name) =>
		fields.TryGetValue(name, out var value) ? value as string : null;
}

public enum BotKnownObjectKind
{
	Player,
	Npc,
	Gatherable,
	Static,
}

public readonly record struct BotPosition(float X, float Y, float Z, byte Heading);

public sealed record BotKnownObject(int ObjectId, BotKnownObjectKind Kind, BotPosition Position,
	int? TemplateId = null, int? VisualTemplateId = null, int? StaticId = null, string? Name = null,
	ushort? State = null, bool? IsOpen = null, byte? Race = null, byte? PlayerClass = null,
	float? MovementSpeed = null);

public sealed record BotInventoryItem(int ObjectId, int ItemId, string Description, long Count, ushort ItemMask,
	string Creator, ushort EquipmentSlot, bool Cloth);

public sealed record BotSkill(ushort SkillId, ushort Level, byte Reserved, byte ProfessionBarSize, int Flag, byte SkillType);

public sealed record BotSkillCooldown(ushort SkillId, int RemainingSeconds, int DurationMillis);

public sealed record BotQuestState(int QuestId, byte Status, int StepAndFlags, byte CompleteCount, int? TimerSeconds);

public sealed record BotCompletedQuest(int QuestId, byte CompleteCount, bool NonRepeatable);

public sealed record BotQuestShare(int QuestId, int SharerId, bool InAlliance);

public sealed record BotReviveOptions(bool BySkill, bool ByItem, int RemainingKiskTimeSeconds, bool InInstance, bool Invasion);

public sealed record BotDialogWindow(int TargetObjectId, ushort PageId, int QuestId);

public sealed record BotQuestionWindow(int Code, IReadOnlyList<string> Parameters, int SenderId, int RangeOrCooldownSeconds);

public sealed record BotLootItem(byte Index, int ItemId, int Count, bool RequiresConfirmation);

public sealed record BotLootWindow(int TargetObjectId, IReadOnlyList<BotLootItem> Items);

public sealed record BotLimitedTradeItem(int ItemId, ushort BuyCount, ushort SellLimit);

public sealed record BotTradeWindow(int TargetObjectId, byte NpcType, int BuyPriceModifier, bool ShowBuyTab,
	bool ShowSellTab, IReadOnlyList<int> Tabs, IReadOnlyList<BotLimitedTradeItem> LimitedItems);

public sealed record BotSystemMessage(int MessageId, string? Name, IReadOnlyList<string> Parameters,
	IReadOnlyList<string> SpecialParameters, int SenderObjectId);
