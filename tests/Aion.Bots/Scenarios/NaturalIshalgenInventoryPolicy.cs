using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using Aion.Bots.World;

namespace Aion.Bots.Scenarios;

/// <summary>Static, shipped-item knowledge used with packet-observed inventory; no server-state oracle.</summary>
public sealed record NaturalItem(int Id, string Group, int RequiredLevel, int MaximumLevel, string Race,
	int Price, int Quality, int MinimumDamage, int MaximumDamage, int MagicBoost, int Mask)
{
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
	private readonly IReadOnlyDictionary<int, NaturalItem> items;
	private readonly HashSet<int> questItems;
	private readonly IReadOnlyDictionary<int, int[]> rewards;

	private NaturalIshalgenInventoryPolicy(IReadOnlyDictionary<int, NaturalItem> items, HashSet<int> questItems,
		IReadOnlyDictionary<int, int[]> rewards)
	{
		this.items = items;
		this.questItems = questItems;
		this.rewards = rewards;
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
		var needed = observedItemIds.Concat(rewards.Values.SelectMany(value => value)).ToHashSet();
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
			XElement? weapon = element.Element("weapon_stats");
			catalog[id] = new NaturalItem(id, (string?)element.Attribute("item_group") ?? "NONE", priestLevel,
				priestMaximum, (string?)element.Attribute("race") ?? "PC_ALL", (int?)element.Attribute("price") ?? 0,
				Quality((string?)element.Attribute("quality")), (int?)weapon?.Attribute("min_damage") ?? 0,
				(int?)weapon?.Attribute("max_damage") ?? 0, (int?)weapon?.Attribute("boost_magical_skill") ?? 0,
				(int?)element.Attribute("mask") ?? 0);
			if (catalog.Count == needed.Count) break;
		}
		return new(catalog, questItems, rewards);
	}

	public NaturalInventoryPlan Decide(BotWorldModel world) => Decide(world.Inventory.Values, world.Level,
		world.CubeExpansion?.Capacity ?? 27);

	public NaturalItem Item(int itemId) => items.TryGetValue(itemId, out NaturalItem? item) ? item
		: throw new InvalidDataException($"Shipped item template {itemId} was not found.");

	public NaturalInventoryPlan Decide(IEnumerable<BotInventoryItem> inventory, int level, int capacity)
	{
		BotInventoryItem[] observed = inventory.Where(item => item.ItemId != BotWorldModel.KinahItemId).ToArray();
		int occupied = observed.Count(item => item.Details.EquippedSlot.GetValueOrDefault() == 0);
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

	public int ChooseReward(int questId, int level, IEnumerable<BotInventoryItem> inventory)
	{
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
