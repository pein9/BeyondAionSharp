using System.Collections.Concurrent;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;

namespace Aion.Bots.Scenarios.Classes;

/// <summary>
/// The help items a class uses (docs/natural-class-profiles.md, CP-16): its kit by level band, as rows of
/// <see cref="NaturalHelpItemAllowlist"/>, and whether the supply, the shield scroll, the mana potion and the scroll
/// upkeep are on at a level. A profile can only switch a use off or name the scroll for the shared slot; what may be
/// supplied at all is the allowlist's.
/// </summary>
/// <param name="LastLevel">The last level the class uses help items at; null for every level.</param>
/// <param name="SharedSlotFamily">The scroll kept up in the slot Awakening and Courage share: <c>awakening</c> (casting
/// speed) or <c>courage</c> (attack speed).</param>
public sealed record NaturalHelpItemRules(IReadOnlyList<NaturalHelpSupply> Kit, int? LastLevel, string SharedSlotFamily = "awakening")
{
	/// <summary>
	/// NR-34 (NR-Q8): the rules of a class that plays past level 9, by the kinds of help it takes
	/// (<see cref="NaturalHelpItemAllowlist.Kit"/>): what every class gets, the mana serum and the Awakening scroll for a
	/// class that casts from mana, the powder for a class that rests with a reagent skill. On at every level.
	/// </summary>
	public static NaturalHelpItemRules ForKinds(bool caster, bool reagent, string sharedSlotFamily = "awakening") =>
		new(NaturalHelpItemAllowlist.Kit(caster, reagent), null, sharedSlotFamily);

	/// <summary>The stock check supplies the kit.</summary>
	public bool Supplied(int level) => On(level);

	/// <summary>The fight asks for the Anti-Shock scroll.</summary>
	public bool ShieldScroll(int level) => On(level);

	/// <summary>The fight may drink an owned mana potion.</summary>
	public bool ManaPotion(int level) => On(level);

	/// <summary>The buff-ourself check keeps the help scrolls up.</summary>
	public bool ScrollUpkeep(int level) => On(level);

	private bool On(int level) => LastLevel is not int last || level <= last;
}

/// <summary>One buff kept up between fights: the best learned skill of <paramref name="Role"/>, recast when its effect
/// is not observed, and traced as <paramref name="TraceKind"/>.</summary>
public sealed record NaturalUpkeepBuff(string Role, string TraceKind);

/// <summary>What a class does when a patrol or its helpers block a planned pull.</summary>
public enum NaturalPatrolRule
{
	/// <summary>The run's short baseline waits (<see cref="NaturalMauPolicyParameters.PatrolWaitCycles"/>).</summary>
	Baseline,
	/// <summary>Hold 15 s at a time, then assess the fight (<see cref="NaturalPatrolPolicy"/>).</summary>
	HoldAndAssess,
}

/// <summary>Whether a class holds at range for a target that attacks from range.</summary>
public enum NaturalRangedHold
{
	/// <summary>The run's own option decides (the Priest line: the hub-optimizing run holds).</summary>
	RunOption,
	Always,
	Never,
}

/// <summary>
/// How one class plays (docs/natural-class-profiles.md, the seam, section 2). A profile holds pure decisions and data;
/// the shared executor in the journey sends every packet and never names a class.
/// </summary>
public sealed class NaturalClassProfile
{
	public required PlayerClass Class { get; init; }

	/// <summary>The class's skill catalog. The learned skill list is still the authority: a catalog never grants a skill.</summary>
	public required NaturalPriestSkill[] Skills { get; init; }

	/// <summary>Auto-learned active skills the class does not cast, each with its reason.</summary>
	public required IReadOnlyDictionary<int, string> Excluded { get; init; }

	public required INaturalCombatPolicy Combat { get; init; }

	public required NaturalHelpItemRules HelpItems { get; init; }

	/// <summary>The buffs kept up between fights, in the order they are checked.</summary>
	public required IReadOnlyList<NaturalUpkeepBuff> Upkeep { get; init; }

	public required NaturalPatrolRule PatrolRule { get; init; }

	public required NaturalRangedHold RangedHold { get; init; }

	/// <summary>How the class recovers between fights.</summary>
	public required NaturalRestRules Rest { get; init; }

	/// <summary>The distances the shared helpers engage at.</summary>
	public required NaturalEngageRanges Ranges { get; init; }

	/// <summary>The HP and MP the shared helpers ask for before they go on.</summary>
	public required NaturalReadinessThresholds Readiness { get; init; }

	/// <summary>What the class treats as gear, how it ranks it and what it keeps.</summary>
	public required NaturalGearRules Gear { get; init; }

	/// <summary>What the class buys at a vendor when its stock runs down: potions only.</summary>
	public required NaturalRestockRules Restock { get; init; }

	/// <summary>The numbers of the Ishalgen quest executors and of the wait for Return.</summary>
	public required NaturalCampaignRules Campaign { get; init; }

	/// <summary>How the class moves inside a fight, with its pull style.</summary>
	public required NaturalFightMovement Movement { get; init; }

	public NaturalPullStyle PullStyle => Movement.Style;

	/// <summary>The opening distance of a planned pull: the profile's own, or the run's when it names none.</summary>
	public float PullDistance(NaturalMauPolicyParameters run) => Ranges.PullDistance ?? run.PullDistanceMeters;

	private readonly ConcurrentDictionary<string, IReadOnlySet<int>> effectIds = new(StringComparer.Ordinal);

	/// <summary>The skill ids of a role in <see cref="Skills"/>, for recognising them in the client's effect list.</summary>
	public IReadOnlySet<int> EffectIds(string role) => effectIds.GetOrAdd(role,
		wanted => Skills.Where(skill => skill.Role == wanted).Select(skill => (int)skill.Id).ToHashSet());

	/// <param name="runOption">What the run itself asks for.</param>
	public bool HoldsAtRange(bool runOption) => RangedHold switch
	{
		NaturalRangedHold.Always => true,
		NaturalRangedHold.Never => false,
		_ => runOption,
	};
}

public static class NaturalClassProfiles
{
	/// <summary>CP-42: every class's catalog is generated from the shipped data (NR-13: the Priest's, the Cleric's and the
	/// Chanter's too). Each profile is built once, from the static data of the first run that asks for it.</summary>
	private static readonly Dictionary<PlayerClass, Func<StaticData, NaturalClassProfile>> Generated = new()
	{
		[PlayerClass.PRIEST] = NaturalPriestProfile.CreatePriest,
		[PlayerClass.CLERIC] = NaturalPriestProfile.CreateCleric,
		[PlayerClass.CHANTER] = NaturalChanterProfile.Create,
		[PlayerClass.WARRIOR] = NaturalWarriorProfile.Create,
		[PlayerClass.MAGE] = NaturalMageProfile.Create,
		[PlayerClass.ARTIST] = NaturalArtistProfile.Create,
		[PlayerClass.ENGINEER] = NaturalEngineerProfile.Create,
		[PlayerClass.SCOUT] = NaturalScoutProfile.Create,
	};

	private static readonly ConcurrentDictionary<PlayerClass, NaturalClassProfile> Built = new();
	private static volatile StaticData? shipped;

	/// <summary>NR-13: a run hands its static data over when it starts, so a caller without it never comes first.</summary>
	public static void Supply(StaticData data) => shipped = data ?? throw new ArgumentNullException(nameof(data));

	/// <summary>
	/// The profile of the class the client observes now. It is looked up at every decision and never kept, because a
	/// starter becomes its second class inside one run. An unobserved class is the line's starter; a class outside the
	/// line, or one without a profile, is refused.
	/// </summary>
	/// <param name="data">The run's static data, for a class with a generated catalog. A caller without it gets the
	/// profile a caller with it already built; the fight loop asks with it at every decision.</param>
	public static NaturalClassProfile For(byte? observedClassId, NaturalClassLine line, StaticData? data = null)
	{
		ArgumentNullException.ThrowIfNull(line);
		if (data != null) shipped = data;
		PlayerClass playerClass = observedClassId is byte classId
			? PlayerClassExtensions.GetPlayerClassById(classId, true)
				?? throw new InvalidDataException($"The client observed class id {classId}, which is no player class.")
			: line.Starter;
		if (!line.Holds(playerClass))
			throw new InvalidDataException($"The client observed {playerClass}, which is outside the class line {line.Id}.");
		if (!Generated.TryGetValue(playerClass, out Func<StaticData, NaturalClassProfile>? create))
			throw new InvalidDataException($"{playerClass} has no natural class profile.");
		return Built.GetOrAdd(playerClass, _ => create(shipped
			?? throw new InvalidDataException($"The {playerClass} profile is generated from the shipped data, and no run has supplied it yet.")));
	}
}
