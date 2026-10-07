using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Aion.Bots.Scenarios;
using Aion.GameServer.Model;

namespace Aion.GameServer.Tests;

/// <summary>
/// CP-05: the level 1-9 help kit. The manifest is the table in docs/natural-class-profiles.md; this test requires it to
/// agree with the allowlist's level 1-9 rows and with the shipped item, skill and starter data.
/// </summary>
public sealed class NaturalStarterHelpKitTests
{
	private const string Marker = "<!-- CP-05 manifest -->";
	private const int LifePotionGroup = 11;

	private static readonly PlayerClass[] Starters = Enum.GetValues<PlayerClass>().Where(playerClass => playerClass.IsStartingClass()).ToArray();

	private sealed record ManifestRow(string Item, int Id, string Family, string Levels, string Source, string Keep, int ItemLevel,
		string UseSkill, string Delay, string Does, string Use);

	private sealed record ItemFacts(int Id, int Level, int[]? Restrict, int? SkillId, int? SkillLevel, int? DelayGroup, int? DelayMillis);

	[Fact]
	public void EverySuppliedRowIsALevelOneToNineRowOfTheAllowlist()
	{
		ManifestRow[] supplied = Manifest.Value.Where(row => row.Source == "supplied").ToArray();
		Assert.Equal(NaturalHelpItemAllowlist.Starter.Length, supplied.Length);
		foreach (ManifestRow row in supplied)
		{
			NaturalHelpSupply supply = Assert.Single(NaturalHelpItemAllowlist.Starter, entry => entry.ItemId == row.Id);
			Match keep = Regex.Match(row.Keep, @"^(\d+) when below (\d+)$");
			Assert.True(keep.Success, $"Row {row.Id} does not say how many are kept.");
			(int skillId, _) = SkillAndLevel(row);
			(int delayGroup, _) = GroupAndDelay(row);
			Assert.Equal(new NaturalHelpSupply(row.Id, row.Family, 1, 9, Int(keep, 1), Int(keep, 2), row.ItemLevel, skillId, delayGroup), supply);
			Assert.True(supply.TopUpTo > supply.Below && supply.Below > 0);
		}
		Assert.All(Manifest.Value, row => Assert.Equal("1-9", row.Levels));
	}

	[Fact]
	public void EveryRowMatchesTheShippedItemAndEveryStarterMayUseItAtLevelOne()
	{
		foreach (ManifestRow row in Manifest.Value)
		{
			ItemFacts item = Items.Value[row.Id];
			Assert.Equal(item.Level, row.ItemLevel);
			if (row.UseSkill == "none")
				Assert.Null(item.SkillId);
			else
				Assert.Equal((item.SkillId!.Value, item.SkillLevel!.Value), SkillAndLevel(row));
			if (row.Delay == "none")
				Assert.Null(item.DelayGroup);
			else
				Assert.Equal((item.DelayGroup!.Value, item.DelayMillis!.Value), GroupAndDelay(row));
			Assert.True(UsableByEveryStarterAtLevelOne(item), $"{row.Item} ({row.Id}) is not usable by every starter at level 1.");
			Assert.Matches(@"^(Used|Left out): \S.{20,}", row.Use);
		}
		// A supplied row is used, and a row that is left out is not supplied.
		Assert.All(Manifest.Value.Where(row => row.Source == "supplied"), row => Assert.StartsWith("Used:", row.Use, StringComparison.Ordinal));
	}

	[Fact]
	public void TheManifestHoldsEveryConsumableAStarterOwns()
	{
		XElement initial = XDocument.Load(Data("player_initial_data.xml")).Root!;
		foreach (PlayerClass starter in Starters)
		{
			XElement data = Assert.Single(initial.Descendants("player_data"), node => (string?)node.Attribute("class") == starter.ToString());
			Dictionary<int, long> owned = data.Descendants("item").ToDictionary(node => (int)node.Attribute("id")!, node => (long)node.Attribute("count")!);
			// Kinah and the three worn pieces are not consumables; everything else a starter owns is in the manifest.
			int[] consumables = owned.Keys.Where(id => id != 182400001 && (Items.Value[id].SkillId != null || id == 169300002)).ToArray();
			Assert.Equal(owned.Count - 4, consumables.Length);
			foreach (int id in consumables)
			{
				ManifestRow row = Assert.Single(Manifest.Value, entry => entry.Id == id);
				Assert.Equal($"owned {owned[id]}", row.Source);
			}
		}
		// And nothing is called owned that a starter does not own.
		Assert.All(Manifest.Value.Where(row => row.Source != "supplied"), row => Assert.Matches(@"^owned \d+$", row.Source));
	}

	[Fact]
	public void NothingElseIsApprovedBelowLevelTen()
	{
		Assert.All(NaturalHelpItemAllowlist.Approved, supply => Assert.True(supply.FromLevel >= 10));
		Assert.All(NaturalHelpItemAllowlist.Starter, supply => Assert.Equal((1, 9), (supply.FromLevel, supply.ToLevel)));
		int[] kit = NaturalHelpItemAllowlist.Starter.Select(supply => supply.ItemId).Order().ToArray();
		for (int level = 1; level <= 9; level++)
			Assert.Equal(kit, NaturalHelpItemSupply.Plan(level, new Dictionary<int, long>()).Select(topUp => topUp.ItemId).Order());
		// From level 10 on the plan is the Cleric's, as before: the potion and the Running scroll of the starter kit stop.
		int[] atTen = NaturalHelpItemSupply.Plan(10, new Dictionary<int, long>()).Select(topUp => topUp.ItemId).ToArray();
		Assert.Equal(NaturalHelpItemAllowlist.Approved.Where(supply => supply.FromLevel == 10).Select(supply => supply.ItemId).Order(), atTen.Order());
		Assert.DoesNotContain(162000006, atTen);
		Assert.DoesNotContain(164000076, atTen);
		foreach (NaturalHelpSupply supply in NaturalHelpItemAllowlist.Starter)
		{
			NaturalHelpItemSupply.RequireApproved(supply.ItemId, supply.TopUpTo);
			Assert.Throws<InvalidOperationException>(() => NaturalHelpItemSupply.RequireApproved(supply.ItemId, 0));
		}
		Assert.Throws<InvalidOperationException>(() => NaturalHelpItemSupply.RequireApproved(162000006, 31));
		Assert.Throws<InvalidOperationException>(() => NaturalHelpItemSupply.RequireApproved(164000076, 21));
		// The refusal is by item, not by level: the level gate is the plan above. Never the other potion tiers, the
		// stronger shield, the owned event scrolls and mana potion, or a bandage.
		foreach (int id in new[] { 162000005, 162000075, 164000070, 164002116, 164002117, 164002118, 162000007, 169300002 })
			Assert.Throws<InvalidOperationException>(() => NaturalHelpItemSupply.RequireApproved(id, 1));
	}

	[Fact]
	public void ThePotionIsTheLargestHealALevelOneCharacterMayDrinkAndTheRunningScrollIsTheGreaterOne()
	{
		ManifestRow potion = Assert.Single(Manifest.Value, row => row.Source == "supplied" && row.Family == "life-potion");
		// Every heal-over-time potion of the life-potion delay group that every starter may use at level 1.
		Dictionary<int, int> heals = Items.Value.Values
			.Where(item => item.DelayGroup == LifePotionGroup && item.SkillId != null && UsableByEveryStarterAtLevelOne(item))
			.Select(item => (item.Id, Heal: TotalHeal(item)))
			.Where(entry => entry.Heal > 0)
			.ToDictionary(entry => entry.Id, entry => entry.Heal);
		Assert.True(heals.Count > 10);
		Assert.Equal(heals.Values.Max(), heals[potion.Id]);
		Assert.Equal(1694, heals[potion.Id]);
		Assert.Contains("1,694 HP", potion.Does, StringComparison.Ordinal);
		// The same heal on the same 30 s delay as the 100 Minor Life Potions a starter owns, which heal 407.
		Assert.Equal(407, heals[162000002]);
		Assert.Equal(Items.Value[162000002].DelayMillis, Items.Value[potion.Id].DelayMillis);
		// A level 50 requirement keeps the next tier out.
		Assert.False(UsableByEveryStarterAtLevelOne(Items.Value[162000075]));

		ManifestRow running = Assert.Single(Manifest.Value, row => row.Source == "supplied" && row.Family == "running");
		Assert.Equal(164000076, running.Id);
		Assert.Equal(30, StatChange(running.Id, "SPEED"));
		Assert.Contains("+30% run speed", running.Does, StringComparison.Ordinal);

		ManifestRow shield = Assert.Single(Manifest.Value, row => row.Source == "supplied" && row.Family == "anti-shock");
		XElement absorb = Effects.Value[Items.Value[shield.Id].SkillId!.Value].Elements("shield").Single();
		Assert.Equal(158, (int)absorb.Attribute("value")! + (int)absorb.Attribute("delta")! * Items.Value[shield.Id].SkillLevel!.Value);
		Assert.Contains("158 damage", shield.Does, StringComparison.Ordinal);
	}

	/// <summary>
	/// The event-scroll finding (hazard 28). A stat change is the value plus the delta times the skill level
	/// (BufEffect), and an item casts its skill at the level its skilluse action names. The three event scrolls a
	/// starter owns name level 3, so they are as strong as the Greater tier and not, as the bot's catalog files them,
	/// as the Lesser one.
	/// </summary>
	[Fact]
	public void TheEventScrollsAreAsStrongAsTheGreaterTier()
	{
		Assert.Equal(3, Items.Value[164002116].SkillLevel);
		Assert.Equal(30, StatChange(164002116, "SPEED"));                 // Accelerox
		Assert.Equal(StatChange(164000076, "SPEED"), StatChange(164002116, "SPEED"));   // the Greater Running Scroll
		Assert.Equal(10, StatChange(164000074, "SPEED"));                 // the Lesser Running Scroll
		Assert.Equal(20, StatChange(164000075, "SPEED"));                 // the Running Scroll the level 20-29 band supplies

		Assert.Equal(9, StatChange(164002118, "BOOST_CASTING_TIME"));     // Castafodin
		Assert.Equal(StatChange(164000134, "BOOST_CASTING_TIME"), StatChange(164002118, "BOOST_CASTING_TIME"));   // the Greater Awakening Scroll
		Assert.Equal(3, StatChange(164000132, "BOOST_CASTING_TIME"));     // the Lesser Awakening Scroll
		Assert.Equal(6, StatChange(164000133, "BOOST_CASTING_TIME"));     // the Awakening Scroll the level 20-29 band supplies

		Assert.Equal(-9, StatChange(164002117, "ATTACK_SPEED"));          // Blitzopan
		Assert.Equal(StatChange(164000073, "ATTACK_SPEED"), StatChange(164002117, "ATTACK_SPEED"));   // the Greater Courage Scroll

		// The event scrolls last 30 minutes with a 1 s delay; the tiered scrolls 5 minutes with 15 s.
		Assert.Equal((1_800_000, 1000), (Duration(164002116), Items.Value[164002116].DelayMillis!.Value));
		Assert.Equal((300_000, 15_000), (Duration(164000076), Items.Value[164000076].DelayMillis!.Value));
		foreach (ManifestRow row in Manifest.Value.Where(row => row.Id is 164002116 or 164002117 or 164002118))
			Assert.Contains("30 min", row.Does, StringComparison.Ordinal);
		Assert.Contains("+30% run speed", Assert.Single(Manifest.Value, row => row.Id == 164002116).Does, StringComparison.Ordinal);
		Assert.Contains("+9% casting speed", Assert.Single(Manifest.Value, row => row.Id == 164002118).Does, StringComparison.Ordinal);
	}

	private static (int SkillId, int Level) SkillAndLevel(ManifestRow row)
	{
		Match match = Regex.Match(row.UseSkill, @"^(\d+) at (\d+)$");
		Assert.True(match.Success, $"Row {row.Id} does not name its use skill and level.");
		return (Int(match, 1), Int(match, 2));
	}

	private static (int Group, int Millis) GroupAndDelay(ManifestRow row)
	{
		Match match = Regex.Match(row.Delay, @"^group (\d+), (\d+) ms$");
		Assert.True(match.Success, $"Row {row.Id} does not name its delay group and delay.");
		return (Int(match, 1), Int(match, 2));
	}

	/// <summary>Java ItemTemplate: the restrict row is indexed by class ordinal, and no row means level 1 for every class.</summary>
	private static bool UsableByEveryStarterAtLevelOne(ItemFacts item) => item.Restrict == null ||
		Starters.All(starter => item.Restrict[Array.IndexOf(Enum.GetValues<PlayerClass>(), starter)] == 1);

	/// <summary>The instant part plus every tick of the heal over time; 0 when the skill is not a heal-over-time potion.</summary>
	private static int TotalHeal(ItemFacts item)
	{
		if (!Effects.Value.TryGetValue(item.SkillId!.Value, out XElement? effects)) return 0;
		XElement? instant = effects.Elements("prochealinstant").FirstOrDefault();
		XElement? overTime = effects.Elements("heal").FirstOrDefault(heal => heal.Attribute("checktime") != null);
		if (instant == null || overTime == null) return 0;
		int Value(XElement effect) => (int)effect.Attribute("value")! + ((int?)effect.Attribute("delta") ?? 0) * item.SkillLevel!.Value;
		return Value(instant) + Value(overTime) * ((int)overTime.Attribute("duration2")! / (int)overTime.Attribute("checktime")!);
	}

	/// <summary>BufEffect: value + delta x skill level, for the item's use skill at the item's skill level.</summary>
	private static int StatChange(int itemId, string stat)
	{
		ItemFacts item = Items.Value[itemId];
		XElement change = Effects.Value[item.SkillId!.Value].Elements("statup").Elements("change").Single(node => (string?)node.Attribute("stat") == stat);
		Assert.Equal("PERCENT", (string?)change.Attribute("func"));
		return ((int?)change.Attribute("value") ?? 0) + ((int?)change.Attribute("delta") ?? 0) * item.SkillLevel!.Value;
	}

	private static int Duration(int itemId) =>
		(int)Effects.Value[Items.Value[itemId].SkillId!.Value].Elements("statup").Single().Attribute("duration2")!;

	/// <summary>The manifest table of docs/natural-class-profiles.md: the first table after the marker.</summary>
	private static readonly Lazy<ManifestRow[]> Manifest = new(() =>
	{
		string[] lines = File.ReadAllLines(Path.Combine(Root(), "docs/natural-class-profiles.md"));
		int marker = Array.FindIndex(lines, line => line.Trim() == Marker);
		Assert.True(marker >= 0, "The manifest marker is missing from docs/natural-class-profiles.md.");
		string[][] table = lines.Skip(marker + 1).SkipWhile(string.IsNullOrWhiteSpace).TakeWhile(line => line.StartsWith('|'))
			.Select(line => line.Trim().Trim('|').Split('|').Select(cell => cell.Trim()).ToArray()).ToArray();
		Assert.Equal(["Item", "Id", "Family", "Levels", "Source", "Keep", "Item level", "Use skill", "Delay", "What it does", "When it is used"], table[0]);
		ManifestRow[] rows = table.Skip(2).Select(cells =>
		{
			Assert.Equal(table[0].Length, cells.Length);
			return new ManifestRow(cells[0], int.Parse(cells[1], CultureInfo.InvariantCulture), cells[2], cells[3], cells[4], cells[5],
				int.Parse(cells[6], CultureInfo.InvariantCulture), cells[7], cells[8], cells[9], cells[10]);
		}).ToArray();
		Assert.Equal(rows.Length, rows.Select(row => row.Id).Distinct().Count());
		return rows;
	});

	private static readonly Lazy<Dictionary<int, ItemFacts>> Items = new(() =>
		Stream(Data("items", "item_templates.xml"), "item_template").Select(node =>
		{
			XElement? use = node.Element("actions")?.Element("skilluse");
			XElement? limits = node.Element("uselimits");
			return new ItemFacts((int)node.Attribute("id")!, (int?)node.Attribute("level") ?? 0,
				((string?)node.Attribute("restrict"))?.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToArray(),
				(int?)use?.Attribute("skillid"), use == null ? null : (int?)use.Attribute("level") ?? 1,
				(int?)limits?.Attribute("usedelayid"), (int?)limits?.Attribute("usedelay"));
		}).ToDictionary(item => item.Id));

	/// <summary>The effects of every skill an item casts.</summary>
	private static readonly Lazy<Dictionary<int, XElement>> Effects = new(() =>
	{
		HashSet<int> cast = Items.Value.Values.Where(item => item.SkillId != null).Select(item => item.SkillId!.Value).ToHashSet();
		return Stream(Data("skills", "skill_templates.xml"), "skill_template")
			.Where(node => cast.Contains((int)node.Attribute("skill_id")!) && node.Element("effects") != null)
			.ToDictionary(node => (int)node.Attribute("skill_id")!, node => node.Element("effects")!);
	});

	/// <summary>The named child elements of a large data file's root, one at a time.</summary>
	private static IEnumerable<XElement> Stream(string path, string elementName)
	{
		using XmlReader reader = XmlReader.Create(path, new XmlReaderSettings { IgnoreWhitespace = true, IgnoreComments = true });
		reader.MoveToContent();
		reader.Read();
		while (!reader.EOF)
		{
			if (reader.NodeType == XmlNodeType.Element && reader.Name == elementName)
				yield return (XElement)XNode.ReadFrom(reader);
			else
				reader.Read();
		}
	}

	private static int Int(Match match, int group) => int.Parse(match.Groups[group].Value, CultureInfo.InvariantCulture);
	private static string Data(params string[] parts) => Path.Combine([Path.Combine(Root(), "game-server/data/static_data"), .. parts]);
	private static string Root() => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ScenarioManifest.FindDefaultPath())!, "../.."));
}
