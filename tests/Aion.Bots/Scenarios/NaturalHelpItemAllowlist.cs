namespace Aion.Bots.Scenarios;

/// <summary>One approved supplied help item for a level band (docs/natural-ascension-altgard.md Appendix D.2).</summary>
/// <param name="TopUpTo">Supply up to this many when the owned count is below <paramref name="Below"/>.</param>
/// <param name="SkillId">The item's use skill (0 for the powder reagent).</param>
/// <param name="UseDelayId">The item's use-delay group (0 for the powder reagent).</param>
public sealed record NaturalHelpSupply(int ItemId, string Family, int FromLevel, int ToLevel, int TopUpTo, int Below,
	int ItemLevel, int SkillId, int UseDelayId);

/// <summary>
/// NA-20: the help-item allowlist the operator APPROVED on 2026-09-28 (OD-13), with two changes: the heal is the
/// timed-healing Life Potion (heal over time, more HP in total than the instant serums) and there are no Revival
/// Stones; powder is supplied too ("GM them anything they need"). NA-21 supplies it. Data only, pinned against the
/// shipped item templates by a test.
/// At levels 10–19 the veteran-reward event scrolls the character already owns (Castafodin, Accelerox: the Lesser
/// tiers' effect, 30 min each) cover casting and run speed, so the kit starts those families at level 20.
/// </summary>
public static class NaturalHelpItemAllowlist
{
	public static readonly NaturalHelpSupply[] Approved =
	[
		new(164000133, "awakening", 20, 29, 60, 15, 20, 9965, 34),
		new(164000134, "awakening", 30, 39, 60, 15, 30, 9965, 34),
		new(164000075, "running", 20, 29, 20, 5, 20, 9960, 35),
		new(164000076, "running", 30, 39, 20, 5, 30, 9960, 35),
		new(164000067, "anti-shock", 10, 19, 30, 8, 20, 9953, 32),
		new(164000068, "anti-shock", 20, 29, 30, 8, 30, 9954, 32),
		new(164000069, "anti-shock", 30, 39, 30, 8, 40, 9955, 32),
		new(162000002, "life-potion", 10, 19, 30, 10, 10, 9889, 11),
		new(162000003, "life-potion", 20, 29, 30, 10, 20, 9890, 11),
		new(162000004, "life-potion", 30, 39, 30, 10, 30, 9891, 11),
		new(162000017, "mana-serum", 10, 19, 40, 10, 10, 9904, 11),
		new(162000018, "mana-serum", 20, 29, 40, 10, 20, 9905, 11),
		new(162000019, "mana-serum", 30, 39, 40, 10, 30, 9906, 11),
		new(160002273, "dp-jelly", 10, 39, 8, 2, 40, 10164, 23),
		new(169300003, "powder", 10, 24, 200, 50, 10, 0, 0),
		new(169300004, "powder", 25, 39, 200, 50, 20, 0, 0),
	];

	/// <summary>Owned natural substitutes the proposal relies on (veteran rewards, VeteranRewardService months 26/30).</summary>
	public static readonly int[] OwnedEventScrolls = [164002118, 164002116];

	/// <summary>
	/// Help the operator approved for one leg only, beyond the level-band kit. AX-Q3 (2026-10-06): one flight-speed scroll,
	/// used when Q2042's timed flight starts. The AX-Q5 revision the same day: enough Bronze Coins for the level-21 and then
	/// the best level-26 coin armor, for the slots it improves; 44 is the whole legendary set.
	/// </summary>
	public static readonly NaturalHelpLegSupply[] LegApproved =
	[
		new("ax", 164000079, "flight-speed", 1, "AX-Q3"),
		new("ax", 186000007, "bronze-coin", 44, "AX-Q5 revision"),
	];
}

/// <summary>One approved leg-scoped help item: at most <paramref name="MaxCount"/> over the whole leg.</summary>
public sealed record NaturalHelpLegSupply(string Leg, int ItemId, string Family, int MaxCount, string Decision);

/// <summary>One help item supplied, as the run profile (help-items.json) and the trace record it.</summary>
public sealed record NaturalHelpSupplied(string Trigger, int ItemId, string Family, long Count, long Before, long After,
	int Level, long GameMillis);

/// <summary>One top-up: bring an approved id from <paramref name="Owned"/> up to its band's N.</summary>
public sealed record NaturalHelpTopUp(int ItemId, string Family, long Owned, long Count);

/// <summary>
/// NA-21: the supply rule for the approved help items (OD-13). At each stock check (run start, level-up, town
/// visit, checkpoint) every approved id whose level band holds the character's level is topped up to N when fewer
/// than M are owned; a new tier's id is supplied when its band starts and the old tier's stock simply runs out.
/// Anything else is refused. <c>NA_HELP_ITEMS=0</c> turns supply off for a clean natural run.
/// </summary>
public static class NaturalHelpItemSupply
{
	public const string Switch = "NA_HELP_ITEMS";

	/// <summary>On unless the switch is exactly "0".</summary>
	public static bool Enabled(string? value) => value != "0";

	public static IReadOnlyList<NaturalHelpTopUp> Plan(int level, IReadOnlyDictionary<int, long> owned) =>
		NaturalHelpItemAllowlist.Approved
			.Where(supply => supply.FromLevel <= level && level <= supply.ToLevel &&
				owned.GetValueOrDefault(supply.ItemId) < supply.Below)
			.Select(supply => new NaturalHelpTopUp(supply.ItemId, supply.Family, owned.GetValueOrDefault(supply.ItemId),
				supply.TopUpTo - owned.GetValueOrDefault(supply.ItemId)))
			.ToArray();

	/// <summary>The run profile's <c>helpItems</c> block: supply is on, and every item supplied, in order.</summary>
	public static string ProfileJson(IReadOnlyList<NaturalHelpSupplied> supplied) =>
		System.Text.Json.JsonSerializer.Serialize(new { helpItems = new { enabled = true, supplied } },
			new System.Text.Json.JsonSerializerOptions { WriteIndented = true });

	/// <summary>Refuse any id that is not approved, or a count above its band's N. A leg-scoped item is approved only on
	/// its own leg, and only while the leg's running total stays inside what the operator allowed.</summary>
	public static void RequireApproved(int itemId, long count, string? leg, long alreadySupplied = 0)
	{
		if (NaturalHelpItemAllowlist.LegApproved.SingleOrDefault(supply => supply.Leg == leg && supply.ItemId == itemId) is not { } scoped)
		{
			RequireApproved(itemId, count);
			return;
		}
		if (count <= 0 || alreadySupplied < 0 || alreadySupplied + count > scoped.MaxCount)
			throw new InvalidOperationException(
				$"Refusing to supply {count} of {scoped.Family} item {itemId} on leg {leg}: {scoped.Decision} allows {scoped.MaxCount} in all, {alreadySupplied} already supplied.");
	}

	/// <summary>Refuse any id that is not approved, or a count above its band's N.</summary>
	public static void RequireApproved(int itemId, long count)
	{
		NaturalHelpSupply[] entries = NaturalHelpItemAllowlist.Approved.Where(supply => supply.ItemId == itemId).ToArray();
		if (entries.Length == 0)
			throw new InvalidOperationException($"Item {itemId} is not an approved help item (OD-13); refusing to supply it.");
		if (count <= 0 || count > entries.Max(supply => supply.TopUpTo))
			throw new InvalidOperationException($"Refusing to supply {count} of help item {itemId}: outside 1..{entries.Max(supply => supply.TopUpTo)}.");
	}
}
