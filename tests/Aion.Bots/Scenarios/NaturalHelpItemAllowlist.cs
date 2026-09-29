namespace Aion.Bots.Scenarios;

/// <summary>One proposed supplied help item for a level band (docs/natural-ascension-altgard.md Appendix D.2).</summary>
/// <param name="TopUpTo">Supply up to this many when the owned count is below <paramref name="Below"/>.</param>
/// <param name="SkillId">The item's use skill (0 for the powder reagent).</param>
/// <param name="UseDelayId">The item's use-delay group (0 for the powder reagent).</param>
/// <param name="Optional">Proposed only if the operator wants it; not part of the default kit.</param>
public sealed record NaturalHelpSupply(int ItemId, string Family, int FromLevel, int ToLevel, int TopUpTo, int Below,
	int ItemLevel, int SkillId, int UseDelayId, bool Optional = false);

/// <summary>
/// NA-20: the PROPOSED help-item allowlist, awaiting operator approval (OD-13). Nothing supplies it yet; NA-21
/// implements the approved version. Data only, pinned against the shipped item templates by a test.
/// At levels 10–19 the veteran-reward event scrolls the character already owns (Castafodin, Accelerox: the Lesser
/// tiers' effect, 30 min each) cover casting and run speed, so the kit starts those families at level 20.
/// </summary>
public static class NaturalHelpItemAllowlist
{
	public static readonly NaturalHelpSupply[] Proposed =
	[
		new(164000133, "awakening", 20, 29, 60, 15, 20, 9965, 34),
		new(164000134, "awakening", 30, 39, 60, 15, 30, 9965, 34),
		new(164000075, "running", 20, 29, 20, 5, 20, 9960, 35),
		new(164000076, "running", 30, 39, 20, 5, 30, 9960, 35),
		new(164000067, "anti-shock", 10, 19, 30, 8, 20, 9953, 32),
		new(164000068, "anti-shock", 20, 29, 30, 8, 30, 9954, 32),
		new(164000069, "anti-shock", 30, 39, 30, 8, 40, 9955, 32),
		new(162000012, "life-serum", 10, 19, 30, 10, 10, 9899, 11),
		new(162000013, "life-serum", 20, 29, 30, 10, 20, 9900, 11),
		new(162000014, "life-serum", 30, 39, 30, 10, 30, 9901, 11),
		new(162000017, "mana-serum", 10, 19, 40, 10, 10, 9904, 11),
		new(162000018, "mana-serum", 20, 29, 40, 10, 20, 9905, 11),
		new(162000019, "mana-serum", 30, 39, 40, 10, 30, 9906, 11),
		new(160002273, "dp-jelly", 10, 39, 8, 2, 40, 10164, 23),
		new(169300003, "powder", 10, 24, 200, 50, 10, 0, 0, Optional: true),
		new(169300004, "powder", 25, 39, 200, 50, 20, 0, 0, Optional: true),
		new(161001001, "revival-stone", 10, 39, 3, 1, 1, 10802, 72, Optional: true),
	];

	/// <summary>Owned natural substitutes the proposal relies on (veteran rewards, VeteranRewardService months 26/30).</summary>
	public static readonly int[] OwnedEventScrolls = [164002118, 164002116];
}
