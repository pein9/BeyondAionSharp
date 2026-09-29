using System.Xml.Linq;
using Aion.Bots.Scenarios;

namespace Aion.GameServer.Tests;

/// <summary>NA-20: the proposed help-item allowlist is pinned against the shipped item data.</summary>
public sealed class NaturalHelpItemAllowlistTests
{
	private static readonly Lazy<XDocument> Items = new(() => XDocument.Load(Path.Combine(
		Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../")),
		"game-server/data/static_data/items/item_templates.xml")));

	[Fact]
	public void EveryProposedIdExistsWithItsSkillDelayGroupAndItemLevel()
	{
		foreach (NaturalHelpSupply supply in NaturalHelpItemAllowlist.Proposed)
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
	public void ScrollTiersMatchTheHelpPolicyAndNeverIncludeCourage()
	{
		foreach (NaturalHelpSupply supply in NaturalHelpItemAllowlist.Proposed.Where(s => s.Family is "awakening" or "running" or "anti-shock"))
		{
			NaturalHelpItem help = Assert.Single(NaturalHelpItemPolicy.All, item => item.ItemId == supply.ItemId);
			Assert.Equal(supply.Family, help.Family);
			// The band is where the NA-19 tier rule picks this tier.
			int allowance = help.Family == "anti-shock" ? NaturalHelpItemPolicy.ShieldLevelAllowance : 0;
			Assert.Equal(supply.FromLevel, Math.Max(10, help.ItemLevel - allowance));
		}
		Assert.DoesNotContain(NaturalHelpItemAllowlist.Proposed, supply => supply.Family == "courage");
		// Bands of one family never overlap, so one tier is supplied at a time.
		foreach (var family in NaturalHelpItemAllowlist.Proposed.GroupBy(supply => supply.Family))
			Assert.All(family.Zip(family.Skip(1)), pair => Assert.True(pair.First.ToLevel < pair.Second.FromLevel));
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
