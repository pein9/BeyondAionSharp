using System.Xml.Linq;
using Aion.Bots.Scenarios;

namespace Aion.GameServer.Tests;

/// <summary>NA-20: the approved help-item allowlist is pinned against the shipped item data.</summary>
public sealed class NaturalHelpItemAllowlistTests
{
	private static readonly Lazy<XDocument> Items = new(() => XDocument.Load(Path.Combine(
		Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../")),
		"game-server/data/static_data/items/item_templates.xml")));

	[Fact]
	public void EveryApprovedIdExistsWithItsSkillDelayGroupAndItemLevel()
	{
		foreach (NaturalHelpSupply supply in NaturalHelpItemAllowlist.Approved)
		{
			XElement item = Item(supply.ItemId);
			Assert.Equal(supply.ItemLevel, (int?)item.Attribute("level"));
			Assert.Equal(supply.SkillId, (int?)item.Element("actions")?.Element("skilluse")?.Attribute("skillid") ?? 0);
			Assert.Equal(supply.UseDelayId, (int?)item.Element("uselimits")?.Attribute("usedelayid") ?? 0);
			// A required level never exceeds the band's first level.
			int required = ((string?)item.Attribute("restrict"))?.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).Max() ?? 1;
			Assert.True(required <= supply.FromLevel, $"{supply.ItemId} requires level {required}, above {supply.FromLevel}.");
			Assert.True(supply.TopUpTo > supply.Below && supply.Below > 0 && supply.FromLevel <= supply.ToLevel);
		}
	}

	[Fact]
	public void EveryRowIsTheBestTierItsBandCanUse()
	{
		// The operator, 2026-10-12: the best a character can use, from level 10. A tier is one of the items whose client
		// name differs from the row's only in its number (remedy_hp_50a: remedy_hp_10a to remedy_hp_70a); a better one has
		// the higher item level; "can use" is the restrict row.
		static int Required(XElement item) =>
			((string?)item.Attribute("restrict"))?.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).Max() ?? 1;
		// The potions and scrolls end in a number and an a (remedy_hp_50a); the power shards in the number alone (battery_70).
		static string Stem(XElement item) => System.Text.RegularExpressions.Regex.Match((string)item.Attribute("cName")!, @"^(.*_)\d+a?$").Groups[1].Value;
		foreach (NaturalHelpSupply supply in NaturalHelpItemAllowlist.Approved.Where(row => row.Kind != NaturalHelpKind.Reagent))
		{
			XElement item = Item(supply.ItemId);
			string stem = Stem(item);
			Assert.NotEqual("", stem);
			XElement[] better = Items.Value.Descendants("item_template")
				.Where(other => (string?)other.Attribute("cName") is { } name && name.StartsWith(stem, StringComparison.Ordinal) && Stem(other) == stem &&
					(int)other.Attribute("level")! > supply.ItemLevel).ToArray();
			// No better tier is usable anywhere in the band, and the band ends where the next one becomes usable.
			Assert.All(better, other => Assert.True(Required(other) > supply.ToLevel, $"{(int)other.Attribute("id")!} is usable inside {supply.ItemId}'s band."));
			Assert.Equal(10, supply.FromLevel);
			Assert.Equal(better.Select(Required).DefaultIfEmpty(50).Min() - 1, supply.ToLevel);
		}
		// MR-e: the shard is the one with the most damage of its tiers, and two full stacks of it, one for each slot.
		NaturalHelpSupply shard = Assert.Single(NaturalHelpItemAllowlist.Approved, row => row.Family == "power-shard");
		XElement[] tiers = Items.Value.Descendants("item_template").Where(other => (string?)other.Attribute("item_group") == "POWER_SHARDS" &&
			Stem(other) == "battery_").ToArray();
		Assert.Equal(8, tiers.Length);
		Assert.Equal(tiers.Max(tier => (int)tier.Attribute("weapon_boost")!), (int)Item(shard.ItemId).Attribute("weapon_boost")!);
		Assert.Equal(2 * (int)Item(shard.ItemId).Attribute("max_stack_count")!, shard.TopUpTo);
		Assert.Equal(NaturalHelpKind.EveryClass, shard.Kind);
		foreach (NaturalHelpSupply supply in NaturalHelpItemAllowlist.Approved.Where(s => s.Family is "awakening" or "courage" or "running" or "anti-shock" or "dp-jelly"))
		{
			NaturalHelpItem help = Assert.Single(NaturalHelpItemPolicy.All, item => item.ItemId == supply.ItemId);
			Assert.Equal(supply.Family, help.Family);
			Assert.True(help.RequiredLevel <= supply.FromLevel);
		}
		// The two scrolls of the shared slot go to different kinds of class, so no class is supplied both.
		Assert.All(NaturalHelpItemAllowlist.Approved.Where(supply => supply.Family == "awakening"), row => Assert.Equal(NaturalHelpKind.Caster, row.Kind));
		Assert.All(NaturalHelpItemAllowlist.Approved.Where(supply => supply.Family == "courage"), row => Assert.Equal(NaturalHelpKind.Striker, row.Kind));
		Assert.DoesNotContain(NaturalHelpItemAllowlist.Kit(caster: true, reagent: true), row => row.Family == "courage");
		Assert.DoesNotContain(NaturalHelpItemAllowlist.Kit(caster: false, reagent: true, striker: true), row => row.Family is "awakening" or "mana-serum");
		// Bands of one family never overlap, so one tier is supplied at a time.
		foreach (var family in NaturalHelpItemAllowlist.Approved.GroupBy(supply => supply.Family))
			Assert.All(family.Zip(family.Skip(1)), pair => Assert.True(pair.First.ToLevel < pair.Second.FromLevel));
	}

	[Fact]
	public void TheApprovedHealIsTheHealOverTimePotionAndThereAreNoRevivalStones()
	{
		// OD-13 (2026-09-28): the Life Potion heals over time (a 2 s tick for 20 s), more in total than the instant serum.
		XDocument skills = XDocument.Load(Path.Combine(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../")),
			"game-server/data/static_data/skills/skill_templates.xml"));
		foreach (NaturalHelpSupply potion in NaturalHelpItemAllowlist.Approved.Where(s => s.Family == "life-potion"))
		{
			XElement skill = Assert.Single(skills.Descendants("skill_template"), node => (int?)node.Attribute("skill_id") == potion.SkillId);
			Assert.Contains(skill.Element("effects")!.Elements("heal"), heal => (int?)heal.Attribute("checktime") == 2000);
		}
		Assert.DoesNotContain(NaturalHelpItemAllowlist.Approved, s => s.Family is "life-serum" or "revival-stone");
	}

	[Fact]
	public void TheOwnedEventScrollsCoverTheLesserTiersAtLevelTen()
	{
		// Castafodin (casting speed) and Accelerox (run speed): group 34/35, 30 min, no required level.
		Assert.Equal(("34", "10467"), Uses(164002118));
		Assert.Equal(("35", "10465"), Uses(164002116));
		Assert.All(NaturalHelpItemAllowlist.OwnedEventScrolls, id => Assert.Null(Item(id).Attribute("restrict")));
	}

	private static (string?, string?) Uses(int id) => ((string?)Item(id).Element("uselimits")?.Attribute("usedelayid"),
		(string?)Item(id).Element("actions")?.Element("skilluse")?.Attribute("skillid"));

	private static XElement Item(int id) =>
		Assert.Single(Items.Value.Descendants("item_template"), node => (int?)node.Attribute("id") == id);
}
