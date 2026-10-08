using System.Collections.Concurrent;
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

	/// <summary>CP-39: the class fights by a rule table (<see cref="NaturalRotationCombatPolicy"/>), so the fight loop
	/// keeps its chain by the catalog's chain time and swings by the weapon's speed. False for the Priest line.</summary>
	public bool TableDriven => Combat is NaturalRotationCombatPolicy;

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
	private static readonly Dictionary<PlayerClass, NaturalClassProfile> Profiles = new()
	{
		[PlayerClass.PRIEST] = NaturalPriestProfile.Priest,
		[PlayerClass.CLERIC] = NaturalPriestProfile.Cleric,
		[PlayerClass.CHANTER] = NaturalChanterProfile.Chanter,
	};

	/// <summary>
	/// The profile of the class the client observes now. It is looked up at every decision and never kept, because a
	/// starter becomes its second class inside one run. An unobserved class is the line's starter; a class outside the
	/// line, or one without a profile, is refused.
	/// </summary>
	public static NaturalClassProfile For(byte? observedClassId, NaturalClassLine line)
	{
		ArgumentNullException.ThrowIfNull(line);
		PlayerClass playerClass = observedClassId is byte classId
			? PlayerClassExtensions.GetPlayerClassById(classId, true)
				?? throw new InvalidDataException($"The client observed class id {classId}, which is no player class.")
			: line.Starter;
		if (!line.Holds(playerClass))
			throw new InvalidDataException($"The client observed {playerClass}, which is outside the class line {line.Id}.");
		return Profiles.TryGetValue(playerClass, out NaturalClassProfile? profile) ? profile
			: throw new InvalidDataException($"{playerClass} has no natural class profile.");
	}
}
