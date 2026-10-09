using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using Aion.Bots.Scenarios.Classes;
using Aion.Bots.World;
using Aion.GameServer.Model;

namespace Aion.Bots.Scenarios;

/// <summary>Static, shipped-item knowledge used with packet-observed inventory; no server-state oracle.</summary>
/// <param name="Restrict">The template's restrict row: the level each class may wear the item at, by class id.</param>
/// <param name="RestrictMax">The template's restrict_max row, or empty: the last level each class may wear it at.</param>
/// <param name="Bonuses">NR-32: the template's flat bonus lines without conditions, summed by stat name
/// (<c>MAXMP</c>, <c>BOOST_MAGICAL_SKILL</c>); none when not read.</param>
public sealed record NaturalItem(int Id, string Group, int[] Restrict, int[] RestrictMax, string Race,
	int Price, int Quality, int MinimumDamage, int MaximumDamage, int MagicBoost, int Mask,
	int ItemLevel = 0, int ExtraInventory = 0, int PhysicalAttack = 0, IReadOnlyDictionary<string, int>? Bonuses = null)
{
	/// <summary>The level this class may wear the item at; 0 when the row does not reach its column.</summary>
	public int RequiredLevelFor(PlayerClass playerClass) =>
		Restrict.Length > playerClass.GetClassId() ? Restrict[playerClass.GetClassId()] : 0;
	/// <summary>The last level this class may wear the item at; 0 for no limit.</summary>
	public int MaximumLevelFor(PlayerClass playerClass) =>
		RestrictMax.Length > playerClass.GetClassId() ? RestrictMax[playerClass.GetClassId()] : 0;
	// CP-22: the members below keep their names for their callers; each asks the Priest's or the Cleric's gear rules.
	public int RequiredLevel => RequiredLevelFor(PlayerClass.PRIEST);
	public int MaximumLevel => MaximumLevelFor(PlayerClass.PRIEST);
	public int ClericLevel => RequiredLevelFor(PlayerClass.CLERIC);
	public int ClericMaximumLevel => MaximumLevelFor(PlayerClass.CLERIC);
	/// <summary>AK-08: Java <c>ItemStorage.getCubeItems</c>: an item counts against the cube's limit unless its template names an
	/// extra inventory (<c>&lt;inventory id="2"/&gt;</c>, the quest tab).</summary>
	public bool InMainCube => ExtraInventory < 1;
	public bool Sellable => (Mask & 4) != 0;
	public bool IsPriestGear => NaturalGearRules.Priest.IsGear(this);
	public string? GearSlot => NaturalGearRules.Priest.Slot(this);
	public long EquipSlot => GearSlot switch
	{
		"WEAPON" => 1, "HEAD" => 4, "TORSO" => 8, "GLOVE" => 16, "SHOES" => 32,
		"SHOULDER" => 2048, "PANTS" => 4096, _ => 0,
	};
	public bool UsableAt(int level) => NaturalGearRules.Priest.Usable(this, level);
	public long GearScore => NaturalGearRules.Priest.Score(this);

	// NA-09: after Ascension the Cleric also wears chain and staves (masteries 49 and 89). CP-29a: both rule sets are
	// built from the class's gear table.
	public bool IsAccessory => Group is "RING" or "EARRING" or "NECKLACE" or "BELT";
	/// <summary>NR-33: the item is worn or held as gear: a weapon, a shield, a piece of armor, a hat or an accessory, by
	/// the slots its item group may take. A power shard, a stigma stone, wings and a plume take slots too and are not
	/// gear of a class's kind.</summary>
	public bool IsEquipment => Enum.TryParse(Group, out Aion.GameServer.Model.Templates.Items.Enums.ItemGroup group) &&
		group is not (Aion.GameServer.Model.Templates.Items.Enums.ItemGroup.POWER_SHARDS or Aion.GameServer.Model.Templates.Items.Enums.ItemGroup.STIGMA
			or Aion.GameServer.Model.Templates.Items.Enums.ItemGroup.WING or Aion.GameServer.Model.Templates.Items.Enums.ItemGroup.PLUME) &&
		Aion.GameServer.Model.Templates.Items.Enums.ItemGroupExtensions.GetValidEquipmentSlots(group) != 0;
	public bool IsClericGear => NaturalGearRules.Cleric.IsGear(this);
	public string? ClericGearSlot => NaturalGearRules.Cleric.Slot(this);
	public bool UsableByClericAt(int level) => NaturalGearRules.Cleric.Usable(this, level);
	/// <summary>A Cleric casts: a staff before a mace, each by magic boost, then damage; armor by item level, then type.</summary>
	public long ClericGearScore => NaturalGearRules.Cleric.Score(this);
}

public sealed record NaturalInventoryDecision(int ObjectId, int ItemId, string Action, string Reason, long Count);
public sealed record NaturalInventoryPlan(int Capacity, int Occupied, IReadOnlyList<NaturalInventoryDecision> Decisions)
{
	public const int QuestFreeSlotReserve = 3;
	public int FreeSlots => Capacity - Occupied;
	public bool CubePressure => FreeSlots < QuestFreeSlotReserve;
	public IReadOnlyList<NaturalInventoryDecision> Sales => Decisions.Where(d => d.Action == "sell").ToArray();
	public IReadOnlyList<NaturalInventoryDecision> Equips => Decisions.Where(d => d.Action == "equip").ToArray();
}

public sealed class NaturalIshalgenInventoryPolicy
{
	private readonly IReadOnlyDictionary<int, NaturalItem> items;
	private readonly HashSet<int> questItems;
	private readonly IReadOnlyDictionary<int, int[]> rewards;

	/// <summary>The Ascension bridge's protected items (its contract): supplies of every rule set from Ascension on.</summary>
	private readonly HashSet<int> bridgeSupplies;
	private readonly (int QuestId, int ItemId) ceremony;
	private readonly NaturalClassLine line;

	private NaturalIshalgenInventoryPolicy(IReadOnlyDictionary<int, NaturalItem> items, HashSet<int> questItems,
		IReadOnlyDictionary<int, int[]> rewards, HashSet<int> bridgeSupplies, (int, int) ceremony, NaturalClassLine line)
	{
		this.items = items;
		this.questItems = questItems;
		this.rewards = rewards;
		this.bridgeSupplies = bridgeSupplies;
		this.ceremony = ceremony;
		this.line = line;
	}

	/// <param name="line">CP-23: the class line of the character whose inventory is decided; the accepted line when not given.</param>
	public static NaturalIshalgenInventoryPolicy Load(string root, IEnumerable<int> observedItemIds, NaturalClassLine? line = null)
	{
		var contract = NaturalIshalgenContract.Load(Path.Combine(root, "parity-artifacts/e2e/natural-ishalgen-contract.json"));
		var questIds = contract.Quests.Select(quest => quest.Id).ToHashSet();
		var questItems = new HashSet<int>();
		var rewards = new Dictionary<int, int[]>();
		XElement quests = XDocument.Load(Path.Combine(root, "game-server/data/static_data/quest_data/quest_data.xml")).Root!;
		// NR-32: the list the server offers. A quest with use_class_reward takes the chosen index from the class's own list
		// (Java QuestService.getRewardItems 172-178, QuestTemplate.getSelectableRewardByClass; 2 means on the last repeat
		// only, and no quest of the route has it); every other quest takes it from the general list of its first reward
		// group, whatever class lists its data also carries. A starter class has no class list.
		var lineContract = NaturalClassLineContract.Load(Path.Combine(root, "parity-artifacts/e2e/natural-class-lines.json"));
		string? classList = (line ?? NaturalClassLine.Default).Second is { } listClass
			? lineContract.Second(listClass).CeremonyReward.SelectableList : null;
		int[] Offered(XElement quest) => (string?)quest.Attribute("use_class_reward") is "1" or "2"
			? classList == null ? [] : quest.Elements(classList).Select(element => (int)element.Attribute("item_id")!).ToArray()
			: quest.Elements("rewards").FirstOrDefault()?.Elements("selectable_reward_item")
				.Select(element => (int)element.Attribute("item_id")!).ToArray() ?? [];
		foreach (XElement quest in quests.Elements("quest").Where(q => questIds.Contains((int)q.Attribute("id")!)))
		{
			int id = (int)quest.Attribute("id")!;
			// All non-reward item references are protected, including starters and collection materials.
			foreach (XAttribute item in quest.DescendantsAndSelf().Where(node => !node.AncestorsAndSelf("rewards").Any())
				.Attributes("item_id"))
				questItems.Add((int)item);
			rewards[id] = Offered(quest);
		}
		// AB-08: every other Asmodian quest's selectable rewards, so a template hand-in beyond Ishalgen (Altgard's Q2225 and
		// Q2227) can choose one. Only the choice list is added; the protected quest items stay the contract's.
		foreach (XElement quest in quests.Elements("quest").Where(q => (string?)q.Attribute("race_permitted") is null or "ASMODIANS" or "PC_ALL"))
		{
			int id = (int)quest.Attribute("id")!;
			if (rewards.ContainsKey(id)) continue;
			int[] choices = Offered(quest);
			if (choices.Length > 0) rewards[id] = choices;
		}
		// NA-09: the Ascension bridge (docs/natural-ascension-altgard.md): its protected items and the ceremony
		// weapon the operator chose (OD-5), from the reviewed contract. CP-26: a line that takes another second class has
		// that pair's bridge, with its own class-reward list (quest_data.xml, priest_selectable_reward for the Cleric) and
		// dispatch quest; a line that takes none keeps the reviewed bridge's protected items and never reaches its quests.
		var bridge = NaturalAscensionContract.Load(Path.Combine(root, "parity-artifacts/e2e/natural-ascension-contract.json"));
		if ((line ?? NaturalClassLine.Default).Second is { } second)
			bridge = NaturalAscensionContract.ForChoice(bridge, lineContract,
				(line ?? NaturalClassLine.Default).Starter, second, line?.CeremonyItemId);
		XElement ceremonyQuest = quests.Elements("quest").Single(q => (int)q.Attribute("id")! == bridge.CeremonyReward.QuestId);
		rewards[bridge.CeremonyReward.QuestId] = ceremonyQuest.Elements(bridge.CeremonyReward.SelectableList)
			.Select(e => (int)e.Attribute("item_id")!).ToArray();
		foreach (int id in bridge.Quests.Select(quest => quest.Id).Where(id => id != bridge.CeremonyReward.QuestId)) rewards[id] = [];
		// The bridge quests' own item references (Destiny Cards, the dispatch work item) are quest items; the Priest
		// never carries them, so its rules are unaffected.
		foreach (XElement quest in quests.Elements("quest").Where(q => bridge.Quests.Any(b => b.Id == (int)q.Attribute("id")!)))
			foreach (XAttribute item in quest.DescendantsAndSelf().Where(node => !node.AncestorsAndSelf("rewards").Any() &&
				!node.Name.LocalName.EndsWith("selectable_reward", StringComparison.Ordinal)).Attributes("item_id"))
				questItems.Add((int)item);
		questItems.UnionWith([182203009, 182203010, 182203011]); // handed out by Q2008's handler, not its data
		// NA-21: the approved help items (OD-13), every help scroll NA-19 knows and every potion combat drinks are the
		// Cleric's supplies too; the NA-21 run found the shop stop selling the freshly supplied Anti-Shock and serums.
		HashSet<int> bridgeSupplies = bridge.ProtectedItemIds.ToHashSet();
		var needed = observedItemIds.Concat(rewards.Values.SelectMany(value => value))
			.Concat(bridgeSupplies).Concat(NaturalGearRules.Cleric.Supplies).ToHashSet();
		var catalog = new Dictionary<int, NaturalItem>();
		using XmlReader reader = XmlReader.Create(Path.Combine(root, "game-server/data/static_data/items/item_templates.xml"),
			new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit });
		while (reader.Read())
		{
			if (reader.NodeType != XmlNodeType.Element || reader.Name != "item_template" ||
				!int.TryParse(reader.GetAttribute("id"), out int id) || !needed.Contains(id)) continue;
			using XmlReader subtree = reader.ReadSubtree();
			XElement element = XElement.Load(subtree);
			// CP-22: the whole restrict row, a level for every class id. A template without one may be worn by all at 1.
			int[] levels = ((string?)element.Attribute("restrict"))?.Split(' ', StringSplitOptions.RemoveEmptyEntries)
				.Select(level => int.Parse(level, CultureInfo.InvariantCulture)).ToArray() ?? Enumerable.Repeat(1, 17).ToArray();
			int[] maximums = ((string?)element.Attribute("restrict_max"))?.Split(' ', StringSplitOptions.RemoveEmptyEntries)
				.Select(level => int.Parse(level, CultureInfo.InvariantCulture)).ToArray() ?? [];
			XElement? weapon = element.Element("weapon_stats");
			catalog[id] = new NaturalItem(id, (string?)element.Attribute("item_group") ?? "NONE", levels,
				maximums, (string?)element.Attribute("race") ?? "PC_ALL", (int?)element.Attribute("price") ?? 0,
				Quality((string?)element.Attribute("quality")), (int?)weapon?.Attribute("min_damage") ?? 0,
				(int?)weapon?.Attribute("max_damage") ?? 0, (int?)weapon?.Attribute("boost_magical_skill") ?? 0,
				(int?)element.Attribute("mask") ?? 0, (int?)element.Attribute("level") ?? 0,
				(int?)element.Element("inventory")?.Attribute("id") ?? 0,
				// CP-29: the flat physical-attack lines of the template, without conditions.
				element.Element("modifiers")?.Elements("add").Where(add => (string?)add.Attribute("name") == "PHYSICAL_ATTACK" &&
					add.Element("conditions") == null).Sum(add => (int?)add.Attribute("value") ?? 0) ?? 0,
				// NR-32: every flat bonus line without conditions, by stat.
				element.Element("modifiers")?.Elements("add").Where(add => add.Attribute("name") != null && add.Element("conditions") == null)
					.GroupBy(add => (string)add.Attribute("name")!).ToDictionary(group => group.Key, group => group.Sum(add => (int?)add.Attribute("value") ?? 0)));
			if (catalog.Count == needed.Count) break;
		}
		return new(catalog, questItems, rewards, bridgeSupplies, (bridge.CeremonyReward.QuestId, bridge.CeremonyReward.ItemId),
			line ?? NaturalClassLine.Default);
	}

	/// <param name="questNeeded">AK-08: items an open quest still needs (Q2292's rings): never sold.</param>
	public NaturalInventoryPlan Decide(BotWorldModel world, IReadOnlySet<int>? questNeeded = null, NaturalCoinGear? coinGear = null,
		NaturalHaramel? haramel = null) => Decide(world.Inventory.Values,
		world.Level, world.CubeExpansion?.Capacity ?? 27, GearRules(world), questNeeded, coinGear, haramel);

	/// <summary>CP-23: the gear rules of the class the client observes, by the policy's class line. An unobserved class
	/// is the line's starter.</summary>
	public NaturalGearRules GearRules(BotWorldModel world) => NaturalClassProfiles.For(
		world.SelfObjectId is int self && world.Objects.TryGetValue(self, out BotKnownObject? known) ? known.PlayerClass : null, line).Gear;

	/// <summary>The client-observed class of the player: Cleric after Ascension (D25), else the Priest rules.</summary>
	public static bool IsCleric(BotWorldModel world) => world.SelfObjectId is int self &&
		world.Objects.TryGetValue(self, out BotKnownObject? known) && known.PlayerClass == (byte)Aion.GameServer.Model.PlayerClass.CLERIC;

	public NaturalItem Item(int itemId) => items.TryGetValue(itemId, out NaturalItem? item) ? item
		: throw new InvalidDataException($"Shipped item template {itemId} was not found.");

	public NaturalInventoryPlan Decide(IEnumerable<BotInventoryItem> inventory, int level, int capacity, bool cleric = false,
		IReadOnlySet<int>? questNeeded = null, NaturalCoinGear? coinGear = null, NaturalHaramel? haramel = null) =>
		Decide(inventory, level, capacity, cleric ? NaturalGearRules.Cleric : NaturalGearRules.Priest, questNeeded, coinGear, haramel);

	/// <summary>
	/// CP-22: keep, wear or sell, by the class's gear rules. The best usable item of each slot is worn, gear for a later
	/// level is kept while it beats that, supplies are kept, and what is left and sellable is sold. A leg's protected items
	/// and retained weapon and an open quest's needs are honored whenever the leg gives them (NA-09), and the bridge's
	/// supplies are kept (Lesser Life Elixirs, mana elixirs, powder, Zeller jelly, Tea of Repose, Destiny Cards).
	/// Accessories are apart (AK-Q4 (b)): the equipment check wears them, so one still in the cube once the upgrades are
	/// worn is surplus and sold, unless the character is not yet the level to wear it. CP-29a: one path for every class.
	/// </summary>
	public NaturalInventoryPlan Decide(IEnumerable<BotInventoryItem> inventory, int level, int capacity, NaturalGearRules rules,
		IReadOnlySet<int>? questNeeded = null, NaturalCoinGear? coinGear = null, NaturalHaramel? haramel = null)
	{
		ArgumentNullException.ThrowIfNull(rules);
		BotInventoryItem[] observed = inventory.Where(item => item.ItemId != BotWorldModel.KinahItemId).ToArray();
		int occupied = Occupied(observed);
		var best = observed.Where(item => items.TryGetValue(item.ItemId, out var template) && rules.Usable(template, level))
			.GroupBy(item => rules.Slot(items[item.ItemId]))
			.ToDictionary(group => group.Key!, group => group.OrderByDescending(item => rules.Score(items[item.ItemId]))
				.ThenBy(item => item.ObjectId).First().ObjectId);
		// CP-29: what the best wearable item of a slot scores. A better item of that slot is held until the character has the
		// level for it.
		long BestScore(string? slot) => slot != null && best.TryGetValue(slot, out int objectId)
			? rules.Score(items[observed.First(item => item.ObjectId == objectId).ItemId]) : long.MinValue;
		// NR-03: a class that holds two weapons keeps its two best one-hand weapons. The second is the one a new weapon
		// has to beat (the operator, 2026-10-08: "replacing the worst one vs the new item"); with one weapon owned, any
		// second is welcome. The two need not be the same item.
		bool holdsTwo = best.TryGetValue("WEAPON", out int firstWeapon) &&
			rules.IsSecondWeapon(items[observed.First(item => item.ObjectId == firstWeapon).ItemId]);
		int? secondWeapon = !holdsTwo ? null : observed.Where(item => item.ObjectId != firstWeapon &&
				items.TryGetValue(item.ItemId, out var template) && rules.Usable(template, level) && rules.IsSecondWeapon(template))
			.OrderByDescending(item => rules.Score(items[item.ItemId])).ThenBy(item => item.ObjectId)
			.Select(item => (int?)item.ObjectId).FirstOrDefault();
		long ScoreToBeat(NaturalItem template) => holdsTwo && rules.IsSecondWeapon(template)
			? secondWeapon is int second ? rules.Score(items[observed.First(item => item.ObjectId == second).ItemId]) : long.MinValue
			: BestScore(rules.Slot(template));
		var decisions = new List<NaturalInventoryDecision>();
		foreach (BotInventoryItem item in observed.OrderBy(item => item.ObjectId))
		{
			string action, reason;
			if (haramel?.ProtectedItemIds.Contains(item.ItemId) == true) (action, reason) = ("hold", "haramel-retained-item");
			else if (coinGear?.ProtectedItemIds.Contains(item.ItemId) == true) (action, reason) = ("hold", "coin-gear-protected");
			else if (!items.TryGetValue(item.ItemId, out NaturalItem? template)) (action, reason) = ("hold", "unknown-static-item");
			else if ((coinGear != null || haramel != null) && rules.GoesInItsHands(template))
				(action, reason) = ("hold", "retained-staff-no-weapon-swap");
			else if (questItems.Contains(item.ItemId) || template.Group is "QUEST" or "KEY") (action, reason) = ("hold", "quest-protected");
			else if (questNeeded?.Contains(item.ItemId) == true) (action, reason) = ("hold", "quest-needed");
			else if (item.Details.EquippedSlot.GetValueOrDefault() != 0) (action, reason) = ("hold", "currently-equipped");
			else if (template.Group == "CL_MULTISLOT") (action, reason) = ("hold", "multi-slot-needs-separate-equip-review");
			else if (template.IsAccessory && template.RequiredLevelFor(rules.Class) > level) (action, reason) = ("hold", "accessory-for-later");
			else if (template.IsAccessory && (item.ItemMask & 4) != 0 && template.Sellable) (action, reason) = ("sell", "surplus-accessory");
			else if (best.TryGetValue(rules.Slot(template) ?? "", out int winner) && winner == item.ObjectId)
				(action, reason) = ("equip", rules.EquipReason);
			else if (secondWeapon == item.ObjectId) (action, reason) = ("hold", "second-weapon");
			// CP-29: gear for a later level is kept while it beats the slot's best.
			else if (template.RequiredLevelFor(rules.Class) > level && rules.UsableNowOrLater(template, level) &&
				rules.Score(template) > ScoreToBeat(template)) (action, reason) = ("hold", "gear-for-later");
			else if (rules.Supplies.Contains(item.ItemId) || bridgeSupplies.Contains(item.ItemId)) (action, reason) = ("hold", "combat-supply");
			else if ((item.ItemMask & 4) == 0 || !template.Sellable) (action, reason) = ("hold", "not-sellable");
			else (action, reason) = ("sell", rules.IsGear(template) ? "surplus-gear" : "unneeded-or-unusable");
			decisions.Add(new(item.ObjectId, item.ItemId, action, reason, item.Count));
		}
		return new(capacity, occupied, decisions);
	}

	/// <summary>The stacks in the cube that count against its limit: not worn, and not in the quest tab.</summary>
	private int Occupied(IEnumerable<BotInventoryItem> observed) => observed.Count(item => item.Details.EquippedSlot.GetValueOrDefault() == 0 &&
		(!items.TryGetValue(item.ItemId, out NaturalItem? template) || template.InMainCube));

	/// <summary>
	/// CP-29: the reward choice, by the class's gear rules. A weapon of the class's groups that beats the held one comes
	/// first, then armor of its types that beats the worn piece; an item for a later level counts, because the rules keep
	/// it. With no upgrade offered, the class's own gear is still preferred to another class's, then a consumable by the
	/// rules' order, then (NR-32) the piece whose bonus lines suit the class, then the sale price. The bonus lines decide
	/// between two pieces of one score, and among what the rules do not score: accessories and hats.
	/// NR-50c (NR-Q15): a class that holds a shield and owns none takes an offered shield before anything else. A weapon is
	/// offered again at later quests; on the route a shield is offered once (Q2100).
	/// </summary>
	/// <param name="rewardRules">CP-23: the rules the choices are scored by (the profile's
	/// <see cref="NaturalClassProfile.Gear"/>); the Priest's when not given.</param>
	public int ChooseReward(int questId, int level, IEnumerable<BotInventoryItem> inventory, NaturalGearRules? rewardRules = null)
	{
		// The ceremony weapon is the operator's choice (OD-5: the Karmic Staff), not a score.
		if (questId == ceremony.QuestId && rewards.TryGetValue(questId, out int[]? list))
			return Array.IndexOf(list, ceremony.ItemId);
		if (!rewards.TryGetValue(questId, out int[]? choices) || choices.Length == 0) return -1;
		NaturalGearRules rules = rewardRules ?? NaturalGearRules.Priest;
		BotInventoryItem[] held = inventory.ToArray();
		var owned = held.Where(item => items.TryGetValue(item.ItemId, out var template) && rules.UsableNowOrLater(template, level))
			.GroupBy(item => rules.Slot(items[item.ItemId]))
			.ToDictionary(group => group.Key!, group => group.Max(item => rules.Score(items[item.ItemId])));
		// NR-03: a class that holds two weapons compares a weapon with the worse of its two; with fewer than two, any
		// one-hand weapon of its groups is an upgrade.
		long[] twoHands = held.Where(item => items.TryGetValue(item.ItemId, out var template) && rules.UsableNowOrLater(template, level) &&
			rules.IsSecondWeapon(template)).Select(item => rules.Score(items[item.ItemId])).OrderDescending().ToArray();
		long ScoreToBeat(NaturalItem item) => rules.IsSecondWeapon(item)
			? twoHands.Length >= 2 ? twoHands[1] : long.MinValue
			: owned.GetValueOrDefault(rules.Slot(item)!, long.MinValue);
		bool Wearable(int id, out NaturalItem item) => items.TryGetValue(id, out item!) && rules.UsableNowOrLater(item, level);
		bool Upgrade(int id, out NaturalItem item) => Wearable(id, out item) && rules.Score(item) > ScoreToBeat(item);
		bool lacksShield = rules.OffHand == NaturalOffHand.Shield && !owned.ContainsKey("SHIELD");
		int ConsumablePlace(int id)
		{
			for (int index = 0; index < rules.ConsumableOrder.Count; index++)
				if (rules.ConsumableOrder[index] == id) return index;
			return int.MaxValue;
		}
		return choices.Select((id, index) => (id, index))
			.OrderByDescending(choice => lacksShield && Wearable(choice.id, out NaturalItem item) && rules.Slot(item) == "SHIELD")
			.ThenByDescending(choice => Upgrade(choice.id, out NaturalItem item) && rules.Slot(item) == "WEAPON")
			.ThenByDescending(choice => Upgrade(choice.id, out NaturalItem item) ? rules.Score(item) : long.MinValue)
			.ThenByDescending(choice => Wearable(choice.id, out NaturalItem item) ? rules.Score(item) : long.MinValue)
			.ThenBy(choice => ConsumablePlace(choice.id))
			.ThenByDescending(choice => items.TryGetValue(choice.id, out NaturalItem? item) ? rules.BonusFit(item) : [], NaturalGearRules.BonusFitOrder)
			.ThenByDescending(choice => items.TryGetValue(choice.id, out NaturalItem? item) && item.Sellable ? item.Price : 0)
			.ThenBy(choice => choice.index).First().index;
	}

	/// <summary>The list the server offers this policy's class at a quest; empty when it offers no choice.</summary>
	public IReadOnlyList<int> RewardList(int questId) => rewards.GetValueOrDefault(questId) ?? [];

	/// <summary>
	/// NR-32: a leg's reward pick for a class other than the one its contract was written for. The pin names a quest;
	/// the pick is what <see cref="ChooseReward"/> takes from the list the server offers this class there.
	/// </summary>
	public NaturalAltgardRewardChoice RewardChoiceFor(NaturalAltgardRewardChoice pinned, int level, IEnumerable<BotInventoryItem> inventory,
		NaturalGearRules rules)
	{
		int index = ChooseReward(pinned.QuestId, level, inventory, rules);
		if (index < 0) throw new InvalidDataException($"Q{pinned.QuestId} offers the {rules.Class} no reward to choose.");
		int itemId = rewards[pinned.QuestId][index];
		return new(pinned.QuestId, $"SELECTED_QUEST_REWARD{index + 1}", itemId, items[itemId].Group);
	}

	public bool AutoLearnedPriestSkillsObserved(int level, IReadOnlyDictionary<int, BotSkill> learned) =>
		NaturalGearRules.Priest.AutoLearnedSkillsObserved(level, learned);

	private static int Quality(string? value) => value switch
	{
		"MYTHIC" => 6, "EPIC" => 5, "UNIQUE" => 4, "LEGEND" => 3, "RARE" => 2,
		"COMMON" => 1, _ => 0,
	};
}
