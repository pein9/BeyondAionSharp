using System.Globalization;
using System.Text;
using System.Xml.Linq;
using Aion.Bots.Scenarios;
using Aion.Bots.World;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Items;
using Aion.GameServer.TestKit;

namespace Aion.GameServer.Tests;

/// <summary>
/// CP-12: the gear decisions of the Priest (levels 1-9) and the Cleric (levels 9-26) as the code made them when the
/// neutral baselines of CP-08 and CP-09 were recorded, held in <c>parity-artifacts/e2e/natural-gear-golden.txt</c>.
/// The refactor items of docs/natural-class-profiles.md that move gear rules behind the class profile (CP-22, CP-23)
/// keep this test green: they do not regenerate the file. The file was written once, with <c>CP_GEAR_GOLDEN_WRITE=1</c>.
/// </summary>
public sealed class NaturalGearGoldenTests
{
	private const string GoldenFile = "parity-artifacts/e2e/natural-gear-golden.txt";
	private const string WriteSwitch = "CP_GEAR_GOLDEN_WRITE";
	private const int FirstObjectId = 100_000;
	private const int Capacity = 27;
	private const long OffHandSlots = (long)ItemSlot.MAIN_OFF_OR_SUB_OFF;

	private static readonly int[] PriestLevels = Enumerable.Range(1, 9).ToArray();
	private static readonly int[] ClericLevels = Enumerable.Range(9, 18).ToArray();
	private static readonly int[] RewardLevels = Enumerable.Range(1, 26).ToArray();

	/// <summary>Every item id in the baseline traces of scopes m, c and ax (CP-08, CP-09): inventory, loot, gathering,
	/// equips, purchases and the help kit.</summary>
	private static readonly int[] BaselineItemIds =
	[
		100000412, 100000455, 100100011, 100100024, 100100025, 100100296, 100100297, 100200604, 100600331, 101500498,
		101500818, 101501224, 101501355, 101501357, 101800399, 102000365, 102000375, 102100334, 110101250, 110300292,
		110500575, 110551139, 110551147, 111100560, 111100763, 111101650, 111500344, 111500549, 111501065, 111501066,
		111501073, 111501081, 112501015, 112501023, 112501641, 113100573, 113100773, 113300278, 113300315, 113500598,
		113501074, 113501082, 113501720, 114100322, 114100794, 114100795, 114300327, 114300328, 114300595, 114300604,
		114300631, 114500369, 114501082, 114501089, 114501726, 120000833, 120001116, 120001132, 120001305, 121000749,
		121000751, 121001207, 121001208, 122000448, 122000869, 122000871, 122001285, 122001664, 123000398, 123000399,
		123000433, 123000864, 123001109, 123001268, 123001440, 125001764, 125004139, 152000201, 152000451, 152000901,
		152010310, 152010311, 152010314, 152010315, 152011001, 152011003, 152011021, 152011022, 152011041, 152011092,
		160000001, 160002273, 160003002, 160003501, 160003503, 160003504, 160003551, 160003552, 160003557, 160003558,
		162000002, 162000003, 162000004, 162000006, 162000007, 162000009, 162000013, 162000017, 162000018, 162000022,
		162000044, 162000048, 162000052, 162000053, 162000057, 162000058, 162000093, 162001057, 164000036, 164000064,
		164000067, 164000068, 164000071, 164000074, 164000075, 164000076, 164000079, 164000133, 164002039, 164002116,
		164002117, 164002118, 166000191, 166000192, 167000226, 167000227, 167000228, 167000229, 167000230, 167000231,
		167000233, 167000235, 167000258, 167000259, 167000260, 167000261, 167000263, 167000264, 167000265, 167000267,
		167000295, 167000418, 167000420, 167000424, 167000425, 167000427, 167000465, 169000003, 169000004, 169000005,
		169100000, 169200002, 169300002, 169300003, 169300004, 169620005, 182000557, 182003733, 182003734, 182003735,
		182003763, 182003764, 182003765, 182003773, 182003774, 182003794, 182004053, 182004174, 182004274, 182004373,
		182004374, 182004393, 182004493, 182004534, 182004535, 182004536, 182004544, 182004545, 182004764, 182004765,
		182004784, 182004793, 182004813, 182004905, 182004954, 182004976, 182005053, 182005054, 182005985, 182005986,
		182005987, 182203002, 182203003, 182203004, 182203005, 182203006, 182203008, 182203016, 182203017, 182203104,
		182203105, 182203106, 182203108, 182203109, 182203112, 182203113, 182203114, 182203115, 182203116, 182203117,
		182203118, 182203119, 182203120, 182203122, 182203124, 182203125, 182203126, 182203127, 182203128, 182203129,
		182203130, 182203131, 182203132, 182203134, 182203217, 182203218, 182203219, 182203220, 182203223, 182203224,
		182203227, 182203228, 182203233, 182207008, 182215358, 182215359, 182400001, 185000103, 185000107, 186000006,
		186000007, 186000014, 188050873, 188050878, 188050880, 188051192, 188053058, 188053405, 188053406, 188053787,
	];

	/// <summary>One letter for each decision the inventory policy can make.</summary>
	private static readonly (string Action, string Reason, char Code)[] Codes =
	[
		("equip", "best-usable-priest-upgrade", 'P'),
		("equip", "best-usable-cleric-upgrade", 'K'),
		("sell", "surplus-gear", 'G'),
		("sell", "unneeded-or-unusable", 'X'),
		("sell", "surplus-accessory", 'A'),
		("hold", "unknown-static-item", 'u'),
		("hold", "quest-protected", 'q'),
		("hold", "currently-equipped", 'w'),
		("hold", "multi-slot-needs-separate-equip-review", 'm'),
		("hold", "combat-supply", 's'),
		("hold", "not-sellable", 'n'),
		("hold", "haramel-retained-item", 'h'),
		("hold", "coin-gear-protected", 'c'),
		("hold", "retained-staff-no-weapon-swap", 't'),
		("hold", "quest-needed", 'd'),
		("hold", "accessory-for-later", 'l'),
	];

	[Fact]
	public async Task GearDecisionsMatchTheGoldenFile()
	{
		string[] actual = await BuildAsync();
		string path = Path.Combine(RealStaticData.RepoRoot(), GoldenFile);
		if (Environment.GetEnvironmentVariable(WriteSwitch) == "1")
			File.WriteAllText(path, string.Join('\n', actual) + "\n");
		Assert.True(File.Exists(path), $"{GoldenFile} is missing.");
		string[] expected = File.ReadAllText(path).Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n').Split('\n');
		int different = Enumerable.Range(0, Math.Min(expected.Length, actual.Length)).FirstOrDefault(line => expected[line] != actual[line], -1);
		if (different < 0 && expected.Length == actual.Length) return;
		string written = Path.Combine(Path.GetTempPath(), "natural-gear-golden.actual.txt");
		File.WriteAllText(written, string.Join('\n', actual) + "\n");
		Assert.Fail(different < 0
			? $"{GoldenFile} has {expected.Length} lines and the code gives {actual.Length}. The code's text is in {written}."
			: $"{GoldenFile} line {different + 1} differs.{Environment.NewLine}golden: {expected[different]}{Environment.NewLine}" +
				$"code:   {actual[different]}{Environment.NewLine}The code's text is in {written}.");
	}

	private static async Task<string[]> BuildAsync()
	{
		string root = RealStaticData.RepoRoot();
		var templates = (await RealStaticData.LoadAsync()).StaticData.ItemDataDh;
		(int QuestId, int[] Choices)[] quests = RewardQuests(root);
		int[] ids = quests.SelectMany(quest => quest.Choices).Concat(BaselineItemIds).Distinct().Order().ToArray();
		NaturalIshalgenInventoryPolicy policy = NaturalIshalgenInventoryPolicy.Load(root, ids);
		NaturalItem? Known(int id)
		{
			try { return policy.Item(id); }
			catch (InvalidDataException) { return null; }
		}
		BotInventoryItem[] all = ids.Select((id, index) => Bag(FirstObjectId + index, id, unchecked((ushort)(Known(id)?.Mask ?? 0)))).ToArray();
		HashSet<int> baseline = [.. BaselineItemIds];
		BotInventoryItem[] carried = all.Where(item => baseline.Contains(item.ItemId)).ToArray();
		NaturalCoinGear coinGear = NaturalAltgardContract.LoadLeg("cg").CoinGear!;
		NaturalHaramel haramel = NaturalAltgardContract.LoadLeg("l12").Haramel!;

		List<string> lines =
		[
			"# CP-12 golden gear file. Written once from the code the neutral baselines were recorded on; see NaturalGearGoldenTests.",
			$"# Fixed inventory: {ids.Length} item ids, one stack of each, object id {FirstObjectId} + index, nothing worn. It holds every",
			$"# selectable reward of {quests.Length} quests and the {BaselineItemIds.Length} item ids of the baseline traces of m, c and ax.",
			$"# Levels: Priest {PriestLevels[0]}-{PriestLevels[^1]}, Cleric {ClericLevels[0]}-{ClericLevels[^1]}, one letter a level. Decision letters:",
			.. Codes.Select(code => $"#   {code.Code} = {code.Action} / {code.Reason}"),
			"#   . = no decision (Kinah)",
			"# i <item> <facts> | <alone: Priest, Cleric> | <whole inventory: Priest, Cleric>",
			"#   facts: group, p<Priest level>-<max>, c<Cleric level>-<max>, race, q<quality>, m<mask>, L<item level>,",
			"#   <Priest slot>/<Cleric slot>, s<Priest score>, cs<Cleric score>, cube<1 when it counts against the cube>",
			"# plan <class> <level> occupied=<stacks counted against the cube>",
			"# coin / haramel <item> <Cleric letters>: the whole inventory with that leg's rules, only where they change a letter",
			"# r <quest> n<choices> e=<index a level, 1-26, empty cube> b=<the same with the baseline items owned>; - = none",
			"# u <class> <level> <all|carried>: upgrades chosen with nothing worn, as item@slot/item level",
			"# w <class> <level>: upgrades chosen level after level over the carried items, each worn before the next; <n = replaces item level n",
			"# d <class> <level>: the decision letters for the carried items, in item order, with that level's upgrades worn",
		];

		// Decide over the whole inventory, a level at a time.
		Dictionary<int, char>[] priestTogether = PriestLevels.Select(level => Letters(policy.Decide(all, level, Capacity))).ToArray();
		Dictionary<int, char>[] clericTogether = ClericLevels.Select(level => Letters(policy.Decide(all, level, Capacity, cleric: true))).ToArray();
		foreach (BotInventoryItem item in all)
		{
			NaturalItem? known = Known(item.ItemId);
			string facts = known == null ? "unknown" : string.Create(CultureInfo.InvariantCulture,
				$"{known.Group} p{known.RequiredLevel}-{known.MaximumLevel} c{known.ClericLevel}-{known.ClericMaximumLevel} {known.Race} " +
				$"q{known.Quality} m{known.Mask} L{known.ItemLevel} {known.GearSlot ?? "-"}/{known.ClericGearSlot ?? "-"} " +
				$"s{known.GearScore} cs{known.ClericGearScore} cube{(known.InMainCube ? 1 : 0)}");
			string alonePriest = new(PriestLevels.Select(level => Letter(policy.Decide([item], level, Capacity), item)).ToArray());
			string aloneCleric = new(ClericLevels.Select(level => Letter(policy.Decide([item], level, Capacity, cleric: true), item)).ToArray());
			string priest = new(priestTogether.Select(letters => letters.GetValueOrDefault(item.ObjectId, '.')).ToArray());
			string cleric = new(clericTogether.Select(letters => letters.GetValueOrDefault(item.ObjectId, '.')).ToArray());
			lines.Add($"i {item.ItemId} {facts} | {alonePriest} {aloneCleric} | {priest} {cleric}");
		}
		foreach (int level in PriestLevels) lines.Add($"plan P {level} occupied={policy.Decide(all, level, Capacity).Occupied}");
		foreach (int level in ClericLevels) lines.Add($"plan C {level} occupied={policy.Decide(all, level, Capacity, cleric: true).Occupied}");
		foreach ((string name, NaturalCoinGear? coin, NaturalHaramel? instance) in new (string, NaturalCoinGear?, NaturalHaramel?)[]
			{ ("coin", coinGear, null), ("haramel", null, haramel) })
		{
			Dictionary<int, char>[] variant = ClericLevels
				.Select(level => Letters(policy.Decide(all, level, Capacity, cleric: true, coinGear: coin, haramel: instance))).ToArray();
			foreach (BotInventoryItem item in all)
			{
				string plain = new(clericTogether.Select(letters => letters.GetValueOrDefault(item.ObjectId, '.')).ToArray());
				string changed = new(variant.Select(letters => letters.GetValueOrDefault(item.ObjectId, '.')).ToArray());
				if (changed != plain) lines.Add($"{name} {item.ItemId} {changed}");
			}
		}

		// ChooseReward for every quest with a choice list.
		foreach ((int questId, int[] choices) in quests)
		{
			string empty = new(RewardLevels.Select(level => Index(policy.ChooseReward(questId, level, []))).ToArray());
			string owned = new(RewardLevels.Select(level => Index(policy.ChooseReward(questId, level, carried))).ToArray());
			lines.Add($"r {questId} n{choices.Length} e={empty} b={owned}");
		}

		// SelectUpgrades through the client's tooltip view of each item.
		Func<int, NaturalGearInfo?> Describe(PlayerClass playerClass) =>
			id => NaturalInventoryCheck.Describe(templates.GetItemTemplate(id), playerClass, Race.ASMODIANS);
		(string Name, PlayerClass Class, int Level)[] steps =
		[
			.. PriestLevels.Select(level => ("P", PlayerClass.PRIEST, level)),
			.. ClericLevels.Select(level => ("C", PlayerClass.CLERIC, level)),
		];
		foreach ((string name, PlayerClass playerClass, int level) in steps)
		{
			lines.Add($"u {name} {level} all: {Upgrades(NaturalGearPolicy.SelectUpgrades(all, level, Describe(playerClass), OffHandSlots))}");
			lines.Add($"u {name} {level} carried: {Upgrades(NaturalGearPolicy.SelectUpgrades(carried, level, Describe(playerClass), OffHandSlots))}");
		}
		BotInventoryItem[] worn = [.. carried];
		foreach ((string name, PlayerClass playerClass, int level) in steps)
		{
			Func<int, NaturalGearInfo?> describe = Describe(playerClass);
			IReadOnlyList<NaturalGearUpgrade> upgrades = NaturalGearPolicy.SelectUpgrades(worn, level, describe, OffHandSlots);
			lines.Add($"w {name} {level}: {Upgrades(upgrades)}");
			foreach (NaturalGearUpgrade upgrade in upgrades) Wear(worn, upgrade, describe(upgrade.ItemId)!.IsStaff);
			Dictionary<int, char> letters = Letters(policy.Decide(worn, level, Capacity, cleric: playerClass == PlayerClass.CLERIC));
			lines.Add($"d {name} {level}: {new string(worn.Select(item => letters.GetValueOrDefault(item.ObjectId, '.')).ToArray())}");
		}
		return [.. lines];
	}

	/// <summary>The policy's reward lists, read the way <see cref="NaturalIshalgenInventoryPolicy.Load"/> reads them.</summary>
	private static (int QuestId, int[] Choices)[] RewardQuests(string root)
	{
		XElement quests = XDocument.Load(Path.Combine(root, "game-server/data/static_data/quest_data/quest_data.xml")).Root!;
		NaturalIshalgenContract contract = NaturalIshalgenContract.Load(Path.Combine(root, "parity-artifacts/e2e/natural-ishalgen-contract.json"));
		NaturalAscensionContract bridge = NaturalAscensionContract.Load(Path.Combine(root, "parity-artifacts/e2e/natural-ascension-contract.json"));
		HashSet<int> listed = [.. contract.Quests.Select(quest => quest.Id), bridge.CeremonyReward.QuestId, 2008, 2904, 24010];
		var rewards = new SortedDictionary<int, int[]>();
		foreach (XElement quest in quests.Elements("quest"))
		{
			int id = (int)quest.Attribute("id")!;
			int[] choices = id is 2008 or 2904 or 24010 ? []
				: id == bridge.CeremonyReward.QuestId ? quest.Elements("priest_selectable_reward").Select(element => (int)element.Attribute("item_id")!).ToArray()
				: quest.Elements("rewards").FirstOrDefault()?.Elements("selectable_reward_item")
					.Select(element => (int)element.Attribute("item_id")!).ToArray() ?? [];
			bool asmodian = (string?)quest.Attribute("race_permitted") is null or "ASMODIANS" or "PC_ALL";
			if (listed.Contains(id) || (asmodian && choices.Length > 0)) rewards[id] = choices;
		}
		return rewards.Select(entry => (entry.Key, entry.Value)).ToArray();
	}

	private static BotInventoryItem Bag(int objectId, int itemId, ushort mask) =>
		new(objectId, itemId, "", 1, mask, "", 0, false) { Details = BotItemDetails.Empty with { EquippedSlot = 0 } };

	private static Dictionary<int, char> Letters(NaturalInventoryPlan plan) =>
		plan.Decisions.ToDictionary(decision => decision.ObjectId, Letter);

	private static char Letter(NaturalInventoryPlan plan, BotInventoryItem item) =>
		plan.Decisions.SingleOrDefault(decision => decision.ObjectId == item.ObjectId) is { } decision ? Letter(decision) : '.';

	private static char Letter(NaturalInventoryDecision decision)
	{
		foreach ((string action, string reason, char code) in Codes)
			if (action == decision.Action && reason == decision.Reason) return code;
		throw new InvalidDataException($"The golden file has no letter for {decision.Action} / {decision.Reason}.");
	}

	private static char Index(int index) => index < 0 ? '-' : "0123456789abcdefghijklmnopqrstuvwxyz"[index];

	private static string Upgrades(IReadOnlyList<NaturalGearUpgrade> upgrades) => upgrades.Count == 0 ? "none" : string.Join(' ',
		upgrades.Select(upgrade => string.Create(CultureInfo.InvariantCulture,
			$"{upgrade.ItemId}@{upgrade.Slot}/{upgrade.ItemLevel}{(upgrade.ReplacesItemLevel is int replaced ? $"<{replaced}" : "")}")));

	/// <summary>Moves the worn state on for the next level: the item goes into its slot and what was there comes off. A
	/// staff takes both hands. It stands in for the server's equip only to give the next call a worn loadout.</summary>
	private static void Wear(BotInventoryItem[] inventory, NaturalGearUpgrade upgrade, bool staff)
	{
		long taken = upgrade.Slot | (staff ? NaturalGearPolicy.SubHand : 0);
		for (int index = 0; index < inventory.Length; index++)
		{
			BotInventoryItem item = inventory[index];
			if (item.ObjectId == upgrade.ObjectId)
				inventory[index] = item with { Details = item.Details with { EquippedSlot = upgrade.Slot } };
			else if (((item.Details.EquippedSlot ?? 0) & taken) != 0)
				inventory[index] = item with { Details = item.Details with { EquippedSlot = 0 } };
		}
	}
}
