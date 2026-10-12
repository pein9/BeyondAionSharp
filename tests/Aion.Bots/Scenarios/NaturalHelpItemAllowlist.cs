namespace Aion.Bots.Scenarios;

/// <summary>NR-34 (NR-Q8): whom an approved row is for. A class profile says which kinds it takes.</summary>
public enum NaturalHelpKind
{
	/// <summary>Every class: the healing potion, the shield scroll, the running scroll, the DP jelly.</summary>
	EveryClass,
	/// <summary>A class that casts from mana: the mana serum, and the Awakening scroll, which shortens a cast.</summary>
	Caster,
	/// <summary>A class that rests with a reagent skill: the powder.</summary>
	Reagent,
	/// <summary>A class that keeps the Courage scroll up in the slot it shares with Awakening: the Courage scroll, which
	/// shortens a weapon swing.</summary>
	Striker,
}

/// <summary>One approved supplied help item for a level band (docs/natural-ascension-altgard.md Appendix D.2).</summary>
/// <param name="TopUpTo">Supply up to this many when the owned count is below <paramref name="Below"/>.</param>
/// <param name="SkillId">The item's use skill (0 for the powder reagent).</param>
/// <param name="UseDelayId">The item's use-delay group (0 for the powder reagent).</param>
/// <param name="Kind">NR-34: whom the row is for; every class when not given.</param>
public sealed record NaturalHelpSupply(int ItemId, string Family, int FromLevel, int ToLevel, int TopUpTo, int Below,
	int ItemLevel, int SkillId, int UseDelayId, NaturalHelpKind Kind = NaturalHelpKind.EveryClass);

/// <summary>
/// NA-20: the help-item allowlist the operator APPROVED on 2026-09-28 (OD-13), with two changes: the heal is the
/// timed-healing Life Potion (heal over time, more HP in total than the instant serums) and there are no Revival
/// Stones; powder is supplied too ("GM them anything they need"). NA-21 supplies it. Data only, pinned against the
/// shipped item templates by a test.
/// The operator, 2026-10-12: "We need to give our characters the BEST they can use (as some are level limited) for
/// consumables. This includes all the way down to level 10." A consumable is gated by its restrict row, not by its item
/// level (Java PlayerRestrictions.canUseItem). So from level 10 each family's row is the best tier whose restrict row
/// lets the character use it: the Major potion, serum and shield scroll, the Greater speed scrolls and the Cippo Aether
/// Jelly. The next tiers (Fine) need level 50, so these bands end at 49; a leg that reaches 50 adds them. The class
/// that keeps Courage up is supplied the Courage scroll, as the caster is the Awakening scroll. The powder is no
/// choice: it is the reagent the learned level of Herb Treatment and MP Recovery consumes. Until that day the rows were
/// bands of ten levels with the tier of that level, and no Courage scroll.
/// </summary>
public static class NaturalHelpItemAllowlist
{
	public static readonly NaturalHelpSupply[] Approved =
	[
		new(164000134, "awakening", 10, 49, 60, 15, 30, 9965, 34, NaturalHelpKind.Caster),
		new(164000073, "courage", 10, 49, 60, 15, 30, 9959, 34, NaturalHelpKind.Striker),
		new(164000076, "running", 10, 49, 20, 5, 30, 9960, 35),
		new(164000070, "anti-shock", 10, 49, 30, 8, 50, 9956, 32),
		new(162000006, "life-potion", 10, 49, 30, 10, 50, 9893, 11),
		new(162000021, "mana-serum", 10, 49, 40, 10, 50, 9908, 11, NaturalHelpKind.Caster),
		new(160002274, "dp-jelly", 10, 49, 8, 2, 50, 10164, 23),
		// The operator, 2026-10-12: "they should use the best power shard possible (again like the other consumables)". No
		// shard has a restrict row, so the best is the Ancient Power Shard. Two stacks of the 10,000 a stack holds, one for
		// each shard slot (NaturalPowerShardPolicy); the server adds a supply to the stacks that are worn.
		new(169000011, "power-shard", 10, 49, 20000, 15000, 65, 0, 0),
		new(169300003, "powder", 10, 24, 200, 50, 10, 0, 0, NaturalHelpKind.Reagent),
		new(169300004, "powder", 25, 39, 200, 50, 20, 0, 0, NaturalHelpKind.Reagent),
	];

	/// <summary>
	/// CP-05: the level 1-9 kit the operator approved for every class line on 2026-10-07 (CP-Q12): "Get better healing
	/// potions, the shield scroll, greater running scroll." Its manifest is the table in docs/natural-class-profiles.md,
	/// and a test requires the two to agree. A consumable is gated by its restrict row, not by its item level (Java
	/// PlayerRestrictions.canUseItem), so a level 1 character may use all three. The rows are keyed by item and level,
	/// not by class. <see cref="Approved"/> holds the rows from level 10 on.
	/// </summary>
	public const int StarterMaxLevel = 9;

	public static readonly NaturalHelpSupply[] Starter =
	[
		new(162000006, "life-potion", 1, 9, 30, 10, 50, 9893, 11),
		new(164000067, "anti-shock", 1, 9, 30, 8, 20, 9953, 32),
		new(164000076, "running", 1, 9, 20, 5, 30, 9960, 35),
	];

	/// <summary>Every supplied row: the level 1-9 kit, then the bands from level 10 on.</summary>
	public static IEnumerable<NaturalHelpSupply> AllLevels => Starter.Concat(Approved);

	/// <summary>
	/// NR-34 (NR-Q8): the kit of a class that plays past level 9, by the kinds its profile takes: the level 1-9 kit, then
	/// the bands from level 10 on that are for every class or of a kind it takes.
	/// </summary>
	public static NaturalHelpSupply[] Kit(bool caster, bool reagent, bool striker = false) => [.. AllLevels.Where(row => row.Kind switch
	{
		NaturalHelpKind.Caster => caster,
		NaturalHelpKind.Reagent => reagent,
		NaturalHelpKind.Striker => striker,
		_ => true,
	})];

	/// <summary>Owned natural substitutes the proposal relies on (veteran rewards, VeteranRewardService months 26/30).</summary>
	public static readonly int[] OwnedEventScrolls = [164002118, 164002116];

	/// <summary>
	/// Help the operator approved for one leg only, beyond the level-band kit. AX-Q3 (2026-10-06): one flight-speed scroll,
	/// used when Q2042's timed flight starts. The AX-Q5 revision the same day: enough Bronze Coins for the level-21 and then
	/// the best level-26 coin armor, for the slots it improves. The operator, 2026-10-12: "We shouldn't have a coin limit."
	/// The coins supplied are what a manifest is short, whatever that comes to.
	/// </summary>
	public static readonly NaturalHelpLegSupply[] LegApproved =
	[
		new("ax", 164000079, "flight-speed", 1, "AX-Q3"),
		new("ax", 186000007, "bronze-coin", null, "AX-Q5 revision, no limit"),
	];
}

/// <summary>One approved leg-scoped help item: at most <paramref name="MaxCount"/> over the whole leg, or as many as
/// the leg asks for when it is null.</summary>
public sealed record NaturalHelpLegSupply(string Leg, int ItemId, string Family, int? MaxCount, string Decision);

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

	/// <param name="kit">CP-16: the class profile's kit; every approved row when none is given.</param>
	/// <param name="full">The muster's stock check (2026-10-12): every row is brought to its N, whatever is owned.</param>
	public static IReadOnlyList<NaturalHelpTopUp> Plan(int level, IReadOnlyDictionary<int, long> owned,
		IEnumerable<NaturalHelpSupply>? kit = null, bool full = false) =>
		(kit ?? NaturalHelpItemAllowlist.AllLevels)
			.Where(supply => supply.FromLevel <= level && level <= supply.ToLevel &&
				owned.GetValueOrDefault(supply.ItemId) < (full ? supply.TopUpTo : supply.Below))
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
		if (count <= 0 || alreadySupplied < 0 || scoped.MaxCount is int most && alreadySupplied + count > most)
			throw new InvalidOperationException(
				$"Refusing to supply {count} of {scoped.Family} item {itemId} on leg {leg}: {scoped.Decision} allows {scoped.MaxCount} in all, {alreadySupplied} already supplied.");
	}

	/// <summary>Refuse any id that is not approved, or a count above its band's N.</summary>
	public static void RequireApproved(int itemId, long count)
	{
		NaturalHelpSupply[] entries = NaturalHelpItemAllowlist.AllLevels.Where(supply => supply.ItemId == itemId).ToArray();
		if (entries.Length == 0)
			throw new InvalidOperationException($"Item {itemId} is not an approved help item (OD-13, CP-Q12); refusing to supply it.");
		if (count <= 0 || count > entries.Max(supply => supply.TopUpTo))
			throw new InvalidOperationException($"Refusing to supply {count} of help item {itemId}: outside 1..{entries.Max(supply => supply.TopUpTo)}.");
	}
}
