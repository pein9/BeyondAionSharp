using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using Aion.Bots.World;

namespace Aion.Bots.Scenarios;

/// <summary>Static, shipped-item knowledge used with packet-observed inventory; no server-state oracle.</summary>
public sealed record NaturalItem(int Id, string Group, int RequiredLevel, int MaximumLevel, string Race,
	int Price, int Quality, int MinimumDamage, int MaximumDamage, int MagicBoost, int Mask,
	int ClericLevel = 0, int ClericMaximumLevel = 0, int ItemLevel = 0, int ExtraInventory = 0)
{
	/// <summary>AK-08: Java <c>ItemStorage.getCubeItems</c>: an item counts against the cube's limit unless its template names an
	/// extra inventory (<c>&lt;inventory id="2"/&gt;</c>, the quest tab).</summary>
	public bool InMainCube => ExtraInventory < 1;
	public bool Sellable => (Mask & 4) != 0;
	public bool IsPriestGear => Group is "MACE" or "RB_TORSO" or "RB_GLOVE" or "RB_SHOULDER" or "RB_PANTS" or "RB_SHOES"
		or "CL_TORSO" or "CL_GLOVE" or "CL_SHOULDER" or "CL_PANTS" or "CL_SHOES" or "CL_HEADS"
		or "LT_TORSO" or "LT_GLOVE" or "LT_SHOULDER" or "LT_PANTS" or "LT_SHOES" or "LT_HEADS";
	public string? GearSlot => Group == "MACE" ? "WEAPON" : Group switch
	{
		"CL_HEADS" or "LT_HEADS" => "HEAD",
		_ when IsPriestGear => Group[(Group.IndexOf('_') + 1)..],
		_ => null,
	};
	public long EquipSlot => GearSlot switch
	{
		"WEAPON" => 1, "HEAD" => 4, "TORSO" => 8, "GLOVE" => 16, "SHOES" => 32,
		"SHOULDER" => 2048, "PANTS" => 4096, _ => 0,
	};
	public bool UsableAt(int level) => IsPriestGear && Quality > 0 && RequiredLevel is > 0 and <= 9 && RequiredLevel <= level
		&& (MaximumLevel == 0 || level <= MaximumLevel) && Race is ("PC_ALL" or "ASMODIANS");
	public long GearScore => (long)RequiredLevel * 1_000_000 + (long)Quality * 100_000
		+ (long)MagicBoost * 100 + MaximumDamage * 10L + MinimumDamage;

	// NA-09: after Ascension the Cleric also wears chain, shields and staves (masteries 49/50/89), and its
	// accessories are gear the journey keeps. The Priest rules above stay exactly as the Ishalgen leg used them.
	public bool IsAccessory => Group is "RING" or "EARRING" or "NECKLACE" or "BELT";
	public bool IsClericGear => IsPriestGear || IsAccessory || Group is "STAFF" or "SHIELD" or "HEAD"
		or "CH_TORSO" or "CH_GLOVE" or "CH_SHOULDER" or "CH_PANTS" or "CH_SHOES" or "CH_HEADS";
	public string? ClericGearSlot => Group switch
	{
		"MACE" or "STAFF" => "WEAPON",
		"SHIELD" => "SUB",
		"HEAD" or "CL_HEADS" or "LT_HEADS" or "CH_HEADS" => "HEAD",
		_ when IsAccessory => Group,
		_ when IsClericGear => Group[(Group.IndexOf('_') + 1)..],
		_ => null,
	};
	public bool UsableByClericAt(int level) => IsClericGear && Quality > 0 && ClericLevel is > 0 && ClericLevel <= level
		&& (ClericMaximumLevel == 0 || level <= ClericMaximumLevel) && Race is ("PC_ALL" or "ASMODIANS");
	/// <summary>A Cleric casts: a weapon ranks by magic boost, then damage; armor by item level, then quality.</summary>
	public long ClericGearScore => ClericGearSlot == "WEAPON"
		? (long)MagicBoost * 1_000_000 + MaximumDamage * 1_000L + MinimumDamage
		: (long)ItemLevel * 1_000_000 + (long)Quality * 100_000 + Price;
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
	private static readonly HashSet<int> Supplies = [162000002, 162000007, 162000052]; // starter HP/MP and bought timed healing
	/// <summary>Priest-born class-reward list for Q2009's ceremony (quest_data.xml priest_selectable_reward).</summary>
	private const string CeremonyList = "priest_selectable_reward";
	private readonly IReadOnlyDictionary<int, NaturalItem> items;
	private readonly HashSet<int> questItems;
	private readonly IReadOnlyDictionary<int, int[]> rewards;

	private readonly HashSet<int> clericSupplies;
	private readonly (int QuestId, int ItemId) ceremony;

	private NaturalIshalgenInventoryPolicy(IReadOnlyDictionary<int, NaturalItem> items, HashSet<int> questItems,
		IReadOnlyDictionary<int, int[]> rewards, HashSet<int> clericSupplies, (int, int) ceremony)
	{
		this.items = items;
		this.questItems = questItems;
		this.rewards = rewards;
		this.clericSupplies = clericSupplies;
		this.ceremony = ceremony;
	}

	public static NaturalIshalgenInventoryPolicy Load(string root, IEnumerable<int> observedItemIds)
	{
		var contract = NaturalIshalgenContract.Load(Path.Combine(root, "parity-artifacts/e2e/natural-ishalgen-contract.json"));
		var questIds = contract.Quests.Select(quest => quest.Id).ToHashSet();
		var questItems = new HashSet<int>();
		var rewards = new Dictionary<int, int[]>();
		XElement quests = XDocument.Load(Path.Combine(root, "game-server/data/static_data/quest_data/quest_data.xml")).Root!;
		foreach (XElement quest in quests.Elements("quest").Where(q => questIds.Contains((int)q.Attribute("id")!)))
		{
			int id = (int)quest.Attribute("id")!;
			// All non-reward item references are protected, including starters and collection materials.
			foreach (XAttribute item in quest.DescendantsAndSelf().Where(node => !node.AncestorsAndSelf("rewards").Any())
				.Attributes("item_id"))
				questItems.Add((int)item);
			rewards[id] = quest.Elements("rewards").FirstOrDefault()?.Elements("selectable_reward_item")
				.Select(element => (int)element.Attribute("item_id")!).ToArray() ?? [];
		}
		// AB-08: every other Asmodian quest's selectable rewards, so a template hand-in beyond Ishalgen (Altgard's Q2225 and
		// Q2227) can choose one. Only the choice list is added; the protected quest items stay the contract's.
		foreach (XElement quest in quests.Elements("quest").Where(q => (string?)q.Attribute("race_permitted") is null or "ASMODIANS" or "PC_ALL"))
		{
			int id = (int)quest.Attribute("id")!;
			if (rewards.ContainsKey(id)) continue;
			int[] choices = quest.Elements("rewards").FirstOrDefault()?.Elements("selectable_reward_item")
				.Select(element => (int)element.Attribute("item_id")!).ToArray() ?? [];
			if (choices.Length > 0) rewards[id] = choices;
		}
		// NA-09: the Ascension bridge (docs/natural-ascension-altgard.md): its protected items and the ceremony
		// weapon the operator chose (OD-5), from the reviewed contract.
		var bridge = NaturalAscensionContract.Load(Path.Combine(root, "parity-artifacts/e2e/natural-ascension-contract.json"));
		XElement ceremonyQuest = quests.Elements("quest").Single(q => (int)q.Attribute("id")! == bridge.CeremonyReward.QuestId);
		rewards[bridge.CeremonyReward.QuestId] = ceremonyQuest.Elements(CeremonyList).Select(e => (int)e.Attribute("item_id")!).ToArray();
		foreach (int id in new[] { 2008, 2904, 24010 }) rewards[id] = [];
		// The bridge quests' own item references (Destiny Cards, the dispatch work item) are quest items; the Priest
		// never carries them, so its rules are unaffected.
		foreach (XElement quest in quests.Elements("quest").Where(q => bridge.Quests.Any(b => b.Id == (int)q.Attribute("id")!)))
			foreach (XAttribute item in quest.DescendantsAndSelf().Where(node => !node.AncestorsAndSelf("rewards").Any() &&
				!node.Name.LocalName.EndsWith("selectable_reward", StringComparison.Ordinal)).Attributes("item_id"))
				questItems.Add((int)item);
		questItems.UnionWith([182203009, 182203010, 182203011]); // handed out by Q2008's handler, not its data
		// NA-21: the approved help items (OD-13), every help scroll NA-19 knows and every potion combat drinks are the
		// Cleric's supplies too; the NA-21 run found the shop stop selling the freshly supplied Anti-Shock and serums.
		var clericSupplies = bridge.ProtectedItemIds.Concat(Supplies)
			.Concat(NaturalHelpItemAllowlist.Approved.Select(supply => supply.ItemId))
			.Concat(NaturalHelpItemPolicy.All.Select(help => help.ItemId))
			.Concat(NaturalIshalgenPotionPolicy.ManaPotionIds)
			.Concat([NaturalIshalgenPotionPolicy.LesserLifePotionId, NaturalIshalgenPotionPolicy.LifePotionId]).ToHashSet();
		var needed = observedItemIds.Concat(rewards.Values.SelectMany(value => value)).Concat(clericSupplies).ToHashSet();
		var catalog = new Dictionary<int, NaturalItem>();
		using XmlReader reader = XmlReader.Create(Path.Combine(root, "game-server/data/static_data/items/item_templates.xml"),
			new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit });
		while (reader.Read())
		{
			if (reader.NodeType != XmlNodeType.Element || reader.Name != "item_template" ||
				!int.TryParse(reader.GetAttribute("id"), out int id) || !needed.Contains(id)) continue;
			using XmlReader subtree = reader.ReadSubtree();
			XElement element = XElement.Load(subtree);
			string[] levels = ((string?)element.Attribute("restrict"))?.Split(' ', StringSplitOptions.RemoveEmptyEntries)
				?? Enumerable.Repeat("1", 17).ToArray();
			string[] maximums = ((string?)element.Attribute("restrict_max"))?.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? [];
			int priestLevel = levels.Length > 9 ? int.Parse(levels[9], CultureInfo.InvariantCulture) : 0;
			int priestMaximum = maximums.Length > 9 ? int.Parse(maximums[9], CultureInfo.InvariantCulture) : 0;
			int clericLevel = levels.Length > 10 ? int.Parse(levels[10], CultureInfo.InvariantCulture) : 0;
			int clericMaximum = maximums.Length > 10 ? int.Parse(maximums[10], CultureInfo.InvariantCulture) : 0;
			XElement? weapon = element.Element("weapon_stats");
			catalog[id] = new NaturalItem(id, (string?)element.Attribute("item_group") ?? "NONE", priestLevel,
				priestMaximum, (string?)element.Attribute("race") ?? "PC_ALL", (int?)element.Attribute("price") ?? 0,
				Quality((string?)element.Attribute("quality")), (int?)weapon?.Attribute("min_damage") ?? 0,
				(int?)weapon?.Attribute("max_damage") ?? 0, (int?)weapon?.Attribute("boost_magical_skill") ?? 0,
				(int?)element.Attribute("mask") ?? 0, clericLevel, clericMaximum, (int?)element.Attribute("level") ?? 0,
				(int?)element.Element("inventory")?.Attribute("id") ?? 0);
			if (catalog.Count == needed.Count) break;
		}
		return new(catalog, questItems, rewards, clericSupplies, (bridge.CeremonyReward.QuestId, bridge.CeremonyReward.ItemId));
	}

	/// <param name="questNeeded">AK-08: items an open quest still needs (Q2292's rings): never sold.</param>
	public NaturalInventoryPlan Decide(BotWorldModel world, IReadOnlySet<int>? questNeeded = null, NaturalCoinGear? coinGear = null,
		NaturalHaramel? haramel = null) => Decide(world.Inventory.Values,
		world.Level, world.CubeExpansion?.Capacity ?? 27, IsCleric(world), questNeeded, coinGear, haramel);

	/// <summary>The client-observed class of the player: Cleric after Ascension (D25), else the Priest rules.</summary>
	public static bool IsCleric(BotWorldModel world) => world.SelfObjectId is int self &&
		world.Objects.TryGetValue(self, out BotKnownObject? known) && known.PlayerClass == (byte)Aion.GameServer.Model.PlayerClass.CLERIC;

	public NaturalItem Item(int itemId) => items.TryGetValue(itemId, out NaturalItem? item) ? item
		: throw new InvalidDataException($"Shipped item template {itemId} was not found.");

	public NaturalInventoryPlan Decide(IEnumerable<BotInventoryItem> inventory, int level, int capacity, bool cleric = false,
		IReadOnlySet<int>? questNeeded = null, NaturalCoinGear? coinGear = null, NaturalHaramel? haramel = null)
	{
		if (cleric) return DecideCleric(inventory, level, capacity, questNeeded ?? new HashSet<int>(), coinGear, haramel);
		BotInventoryItem[] observed = inventory.Where(item => item.ItemId != BotWorldModel.KinahItemId).ToArray();
		int occupied = Occupied(observed);
		var best = observed.Where(item => items.TryGetValue(item.ItemId, out var template) && template.UsableAt(level))
			.GroupBy(item => items[item.ItemId].GearSlot)
			.ToDictionary(group => group.Key!, group => group.OrderByDescending(item => items[item.ItemId].GearScore)
				.ThenBy(item => item.ObjectId).First().ObjectId);
		var decisions = new List<NaturalInventoryDecision>();
		foreach (BotInventoryItem item in observed.OrderBy(item => item.ObjectId))
		{
			string action, reason;
			if (!items.TryGetValue(item.ItemId, out NaturalItem? template)) (action, reason) = ("hold", "unknown-static-item");
			else if (questItems.Contains(item.ItemId) || template.Group is "QUEST" or "KEY") (action, reason) = ("hold", "quest-protected");
			else if (item.Details.EquippedSlot.GetValueOrDefault() != 0) (action, reason) = ("hold", "currently-equipped");
			else if (template.Group == "CL_MULTISLOT") (action, reason) = ("hold", "multi-slot-needs-separate-equip-review");
			else if (best.TryGetValue(template.GearSlot ?? "", out int winner) && winner == item.ObjectId)
				(action, reason) = ("equip", "best-usable-priest-upgrade");
			else if (Supplies.Contains(item.ItemId)) (action, reason) = ("hold", "combat-supply");
			else if ((item.ItemMask & 4) == 0 || !template.Sellable) (action, reason) = ("hold", "not-sellable");
			else (action, reason) = ("sell", template.IsPriestGear ? "surplus-gear" : "unneeded-or-unusable");
			decisions.Add(new(item.ObjectId, item.ItemId, action, reason, item.Count));
		}
		return new(capacity, occupied, decisions);
	}

	/// <summary>NA-09: the Cleric keeps every usable armor/weapon upgrade, and protects the bridge's supplies (Lesser Life
	/// Elixirs, mana elixirs, powder, Zeller jelly, Tea of Repose, Destiny Cards). AK-Q4 (b): an accessory still in the cube
	/// once the upgrades are worn is surplus and sold, unless the Cleric is not yet the level to wear it.</summary>
	private NaturalInventoryPlan DecideCleric(IEnumerable<BotInventoryItem> inventory, int level, int capacity, IReadOnlySet<int> questNeeded,
		NaturalCoinGear? coinGear, NaturalHaramel? haramel)
	{
		BotInventoryItem[] observed = inventory.Where(item => item.ItemId != BotWorldModel.KinahItemId).ToArray();
		int occupied = Occupied(observed);
		var best = observed.Where(item => items.TryGetValue(item.ItemId, out var template) && template.UsableByClericAt(level) && !template.IsAccessory)
			.GroupBy(item => items[item.ItemId].ClericGearSlot)
			.ToDictionary(group => group.Key!, group => group.OrderByDescending(item => items[item.ItemId].ClericGearScore)
				.ThenBy(item => item.ObjectId).First().ObjectId);
		var decisions = new List<NaturalInventoryDecision>();
		foreach (BotInventoryItem item in observed.OrderBy(item => item.ObjectId))
		{
			string action, reason;
			if (haramel?.ProtectedItemIds.Contains(item.ItemId) == true) (action, reason) = ("hold", "haramel-retained-item");
			else if (coinGear?.ProtectedItemIds.Contains(item.ItemId) == true) (action, reason) = ("hold", "coin-gear-protected");
			else if (!items.TryGetValue(item.ItemId, out NaturalItem? template)) (action, reason) = ("hold", "unknown-static-item");
			else if ((coinGear != null || haramel != null) && template.ClericGearSlot is "WEAPON" or "SUB") (action, reason) = ("hold", "retained-staff-no-weapon-swap");
			else if (questItems.Contains(item.ItemId) || template.Group is "QUEST" or "KEY") (action, reason) = ("hold", "quest-protected");
			else if (questNeeded.Contains(item.ItemId)) (action, reason) = ("hold", "quest-needed");
			else if (item.Details.EquippedSlot.GetValueOrDefault() != 0) (action, reason) = ("hold", "currently-equipped");
			else if (template.Group == "CL_MULTISLOT") (action, reason) = ("hold", "multi-slot-needs-separate-equip-review");
			else if (template.IsAccessory && template.ClericLevel > level) (action, reason) = ("hold", "accessory-for-later");
			else if (template.IsAccessory && (item.ItemMask & 4) != 0 && template.Sellable) (action, reason) = ("sell", "surplus-accessory");
			else if (best.TryGetValue(template.ClericGearSlot ?? "", out int winner) && winner == item.ObjectId)
				(action, reason) = ("equip", "best-usable-cleric-upgrade");
			else if (clericSupplies.Contains(item.ItemId)) (action, reason) = ("hold", "combat-supply");
			else if ((item.ItemMask & 4) == 0 || !template.Sellable) (action, reason) = ("hold", "not-sellable");
			else (action, reason) = ("sell", template.IsClericGear ? "surplus-gear" : "unneeded-or-unusable");
			decisions.Add(new(item.ObjectId, item.ItemId, action, reason, item.Count));
		}
		return new(capacity, occupied, decisions);
	}

	/// <summary>The stacks in the cube that count against its limit: not worn, and not in the quest tab.</summary>
	private int Occupied(IEnumerable<BotInventoryItem> observed) => observed.Count(item => item.Details.EquippedSlot.GetValueOrDefault() == 0 &&
		(!items.TryGetValue(item.ItemId, out NaturalItem? template) || template.InMainCube));

	public int ChooseReward(int questId, int level, IEnumerable<BotInventoryItem> inventory)
	{
		// The ceremony weapon is the operator's choice (OD-5: the Karmic Staff), not a score.
		if (questId == ceremony.QuestId && rewards.TryGetValue(questId, out int[]? list))
			return Array.IndexOf(list, ceremony.ItemId);
		if (!rewards.TryGetValue(questId, out int[]? choices) || choices.Length == 0) return -1;
		var owned = inventory.Where(item => items.TryGetValue(item.ItemId, out var template) && template.UsableAt(level))
			.GroupBy(item => items[item.ItemId].GearSlot)
			.ToDictionary(group => group.Key!, group => group.Max(item => items[item.ItemId].GearScore));
		return choices.Select((id, index) => (id, index))
			.OrderByDescending(choice => items.TryGetValue(choice.id, out NaturalItem? item) && item.UsableAt(level)
				&& item.GearScore > owned.GetValueOrDefault(item.GearSlot!) ? item.GearScore : 0)
			.ThenByDescending(choice => items.TryGetValue(choice.id, out NaturalItem? item) && item.Sellable
				? item.Price : 0)
			.ThenBy(choice => choice.index).First().index;
	}

	public bool AutoLearnedPriestSkillsObserved(int level, IReadOnlyDictionary<int, BotSkill> learned) =>
		(new[] { 39, 40, 41, 103 }).Concat(NaturalPriestSkills.All.Where(skill => skill.MinimumLevel <= level)
			.Select(skill => (int)skill.Id)).All(learned.ContainsKey);

	private static int Quality(string? value) => value switch
	{
		"MYTHIC" => 6, "EPIC" => 5, "UNIQUE" => 4, "LEGEND" => 3, "RARE" => 2,
		"COMMON" => 1, _ => 0,
	};
}
