using System.Xml;
using System.Xml.Linq;

namespace Aion.Bots.Scenarios;

public sealed record SoakCookingOrder(CookingWorkOrder Order, IReadOnlyDictionary<int, long> Materials);

/// <summary>The shipped apprentice Cooking work orders, through the natural skill-99 mastery gate.</summary>
public static class SoakCookingCatalog
{
	private static readonly Lazy<IReadOnlyList<SoakCookingOrder>> Orders = new(Load);
	public static IReadOnlyList<SoakCookingOrder> All => Orders.Value;
	public static IReadOnlySet<int> ShopMaterials => All.SelectMany(entry => entry.Materials.Keys.Where(id => id != entry.Order.IssuedItemId)).ToHashSet();
	public static SoakCookingOrder Select(CookingMaster master, int skillLevel)
	{
		if (skillLevel is < 1 or > 99) throw new ArgumentOutOfRangeException(nameof(skillLevel), "Apprentice soak profile requires Cooking 1..99.");
		return All.Where(entry => entry.Order.Master == master && entry.Order.SkillLevel <= skillLevel && skillLevel - entry.Order.SkillLevel <= 40)
			.MaxBy(entry => entry.Order.SkillLevel) ?? throw new InvalidDataException("No eligible shipped Cooking work order.");
	}

	private static IReadOnlyList<SoakCookingOrder> Load()
	{
		string root = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ScenarioManifest.FindDefaultPath())!, "..", "..", "game-server", "data", "static_data"));
		var scripts = XElement.Load(Path.Combine(root, "quest_script_data", "work_order.xml"));
		var quests = XElement.Load(Path.Combine(root, "quest_data", "quest_data.xml"));
		var orders = new Dictionary<int, CookingWorkOrder>();
		foreach (var master in new[] { CookingMaster.Hestia, CookingMaster.Lainita })
		{
			int first = CookingWorkOrder.For(master)[0].QuestId;
			foreach (int id in Enumerable.Range(first, 10))
			{
				var script = scripts.Elements("work_order").Single(value => (int?)value.Attribute("id") == id);
				var quest = quests.Elements("quest").Single(value => (int?)value.Attribute("id") == id);
				var issued = script.Elements("give_component").Single();
				var product = quest.Element("collect_items")!.Elements("collect_item").Single();
				if ((int?)quest.Attribute("max_repeat_count") != 255 || (int?)quest.Attribute("combineskill") != CookingLearnScenario.SkillId ||
					(string?)quest.Attribute("race_permitted") != master.Race.ToString() || (string?)quest.Element("bonus")?.Attribute("type") != "TASK")
					throw new InvalidDataException($"Work-order contract changed for {id}.");
				var order = new CookingWorkOrder(master, id, (int)script.Attribute("recipe_id")!, (int)issued.Attribute("item_id")!,
					(int)issued.Attribute("count")!, (int)product.Attribute("item_id")!, (int)product.Attribute("count")!,
					(int)quest.Attribute("combine_skillpoint")!, CookingWorkOrder.For(master)[0].OvenStaticId);
				orders.Add(order.RecipeId, order);
			}
		}
		var result = new List<SoakCookingOrder>();
		using var reader = XmlReader.Create(Path.Combine(root, "recipe", "recipe_templates.xml"));
		while (reader.Read())
		{
			if (reader.NodeType != XmlNodeType.Element || reader.LocalName != "recipe_template" ||
				!int.TryParse(reader.GetAttribute("id"), out int id) || !orders.TryGetValue(id, out var order)) continue;
			var recipe = (XElement)XNode.ReadFrom(reader);
			if ((int?)recipe.Attribute("quantity") != 1 || (int?)recipe.Attribute("productid") != order.ProductId ||
				(int?)recipe.Attribute("skillpoint") != order.SkillLevel || (int?)recipe.Attribute("skillid") != CookingLearnScenario.SkillId ||
				recipe.Attribute("max_production_count") != null || recipe.Attributes().Any(attribute => attribute.Name.LocalName.StartsWith("combo", StringComparison.Ordinal)))
				throw new InvalidDataException($"Unsupported work recipe {id}.");
			var materials = recipe.Elements("components_data").Single().Elements("component")
				.ToDictionary(item => (int)item.Attribute("itemid")!, item => (long)item.Attribute("quantity")!);
			if (materials.GetValueOrDefault(order.IssuedItemId) != 1) throw new InvalidDataException("Work-order issued-component contract changed.");
			result.Add(new SoakCookingOrder(order, materials));
		}
		if (result.Count != orders.Count) throw new InvalidDataException("Incomplete apprentice Cooking recipe catalog.");
		// The soak completion model uses ordinary COMMON-quality, unlimited, non-combo work products.
		var products = result.Select(entry => entry.Order.ProductId).ToHashSet();
		using var items = XmlReader.Create(Path.Combine(root, "items", "item_templates.xml"));
		while (items.Read())
			if (items.NodeType == XmlNodeType.Element && items.LocalName == "item_template" &&
				int.TryParse(items.GetAttribute("id"), out int itemId) && products.Remove(itemId) && items.GetAttribute("quality") != "COMMON")
				throw new InvalidDataException("Work-order product quality changed; update the probability model explicitly.");
		if (products.Count != 0) throw new InvalidDataException("Work-order products are missing.");
		return result.AsReadOnly();
	}
}
