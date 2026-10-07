using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using Aion.Bots.Navigation;
using Aion.Bots.Scenarios;
using Aion.Bots.Scenarios.Classes;
using Xunit.Abstractions;

namespace Aion.GameServer.Tests;

/// <summary>
/// CP-11: every number the class seam of docs/natural-class-profiles.md will carry, pinned before any code moves.
/// A row is asserted (a public constant or a default parameter value the test reads) or pending (a private constant
/// or a literal inside a method). A pending row names its owner, the one CP item that moves the number behind the
/// profile, and the source lines that hold the literal today; the test finds those lines and fails when one is gone.
/// The owner turns its row on by setting <see cref="Pin.Profile"/> to the profile member and removing the sites. It
/// may not edit <see cref="Pin.Expected"/>. CP-41 makes this test fail while any row is still pending.
/// </summary>
public sealed class NaturalClassSeamPinTests(ITestOutputHelper output)
{
	private const string Journey = "NaturalIshalgenJourney*.cs";
	private const string HelpGate = "Priest 1: on, Priest 9: on, Cleric 10: on, Cleric 26: on";

	/// <summary>The items of the CP list that turn a row on or add its profile side.</summary>
	private static readonly string[] Owners = ["CP-16", "CP-17", "CP-18", "CP-19", "CP-21", "CP-24"];

	/// <param name="Name">What the number is.</param>
	/// <param name="Expected">The value as CP-07 left it. No item edits it.</param>
	/// <param name="Actual">Reads the number from the code today; null on a pending row.</param>
	/// <param name="Owner">The item that moves the number behind the profile; null when no item moves it.</param>
	/// <param name="Profile">Reads the number from the profile; null until the owner adds it.</param>
	/// <param name="Sites">Where a pending row's literal stands in the source.</param>
	private sealed record Pin(string Name, object Expected, Func<object>? Actual = null, string? Owner = null,
		Func<object>? Profile = null, params Site[] Sites)
	{
		public bool Pending => Actual == null && Profile == null;
	}

	/// <param name="Files">File name pattern under tests/Aion.Bots.</param>
	/// <param name="Method">The enclosing method or local function; null searches the whole file.</param>
	/// <param name="Fragment">Source text on one line.</param>
	/// <param name="Count">How many lines of the scope hold it.</param>
	private sealed record Site(string Files, string? Method, string Fragment, int Count = 1);

	private static readonly Pin[] Rows =
	[
		// Asserted now. They stay inside the static policy behind the adapter: no item moves them.
		new("swarm: attackers at which the Priest leaves", 3, () => NaturalPriestCombatPolicy.SwarmedAttackers),
		new("heal: HP percent against one attacker", 55, () => NaturalPriestCombatPolicy.HealPercentSingle),
		new("heal: HP percent against two or more attackers", 70, () => NaturalPriestCombatPolicy.HealPercentMultiple),
		new("Mau policy baseline", new NaturalMauPolicyParameters(22f, 3, 55, 70, 90, 0, 15, false),
			() => NaturalMauPolicyParameters.Baseline),

		// Asserted in the static policy and, since CP-16, on the profile: the fight loop asks the profile's policy.
		new("emergency: enter at HP percent", 35, () => NaturalPriestCombatPolicy.EmergencyPercent, "CP-16",
			() => Both(profile => profile.Combat.EmergencyEnterPercent(0, false))),
		new("emergency: clear at HP percent", 45, () => NaturalPriestCombatPolicy.EmergencyClearPercent, "CP-16",
			() => Both(profile => profile.Combat.EmergencyExitPercent(0, false))),
		new("emergency: enter, one attacker", 35, () => NaturalPriestCombatPolicy.EmergencyEnterPercent(1, true), "CP-16",
			() => Both(profile => profile.Combat.EmergencyEnterPercent(1, true))),
		new("emergency: enter, two attackers on an ordinary target", 35,
			() => NaturalPriestCombatPolicy.EmergencyEnterPercent(2, false), "CP-16",
			() => Both(profile => profile.Combat.EmergencyEnterPercent(2, false))),
		new("emergency: enter, two attackers on a Seasoned target", 55,
			() => NaturalPriestCombatPolicy.EmergencyEnterPercent(2, true), "CP-16",
			() => Both(profile => profile.Combat.EmergencyEnterPercent(2, true))),
		new("emergency: exit, one attacker", 45, () => NaturalPriestCombatPolicy.EmergencyExitPercent(1, true), "CP-16",
			() => Both(profile => profile.Combat.EmergencyExitPercent(1, true))),
		new("emergency: exit, two attackers on a Seasoned target", 65,
			() => NaturalPriestCombatPolicy.EmergencyExitPercent(2, true), "CP-16",
			() => Both(profile => profile.Combat.EmergencyExitPercent(2, true))),

		// Asserted now, profile side by CP-18.
		new("melee reach (m)", 3f, () => NaturalPriestCombatPolicy.MeleeReach, "CP-18"),
		new("pull: spell range (m)", 22f, () => NaturalPullPlanner.SpellRange, "CP-18"),
		new("fight through: firing range (m)", 23f, () => NaturalFightThrough.FiringRange, "CP-18"),
		new("standoff: spell range default (m)", 25f, () => StandoffDefault("spellRange"), "CP-18"),
		new("standoff: arrival tolerance default (m)", 3f, () => StandoffDefault("arrivalTolerance"), "CP-18"),
		new("standoff: safety margin default (m)", 1f, () => StandoffDefault("safetyMargin"), "CP-18"),

		// Asserted now, profile side by CP-24.
		new("restock: buy at or below this many potions", 5, () => NaturalIshalgenPotionPolicy.RestockAtOrBelow, "CP-24"),
		new("restock: buy up to this many potions", 12, () => NaturalIshalgenPotionPolicy.RestockTarget, "CP-24"),

		// Asserted now: what CP-06 set. No item moves them.
		new("help items: last level of the 1-9 kit", 9, () => NaturalHelpItemAllowlist.StarterMaxLevel),
		new("help items: scroll refresh window (ms)", 20_000, () => NaturalHelpItemPolicy.RefreshWindowMillis),
		new("help items: long travel leg (m)", 150f, () => NaturalHelpItemPolicy.LongTravelMeters),
		new("help items: shield scroll at HP percent", 50, () => NaturalHelpItemPolicy.ShieldHpPercent),

		// Turned on by CP-16: the four help-item gates as CP-06 left them, read from the profiles.
		new("help gate: supply", HelpGate, Owner: "CP-16", Profile: () => Gate(rules => rules.Supplied)),
		new("help gate: shield scroll", HelpGate, Owner: "CP-16", Profile: () => Gate(rules => rules.ShieldScroll)),
		new("help gate: mana potion", HelpGate, Owner: "CP-16", Profile: () => Gate(rules => rules.ManaPotion)),
		new("help gate: scroll upkeep", HelpGate, Owner: "CP-16", Profile: () => Gate(rules => rules.ScrollUpkeep)),

		// Pending, owner CP-17: the rest rule.
		new("rest: cast the heal below HP percent", 90, Owner: "CP-17", Sites:
			[new(Journey, "RestAsync", "world.CurrentHp * 100 < world.MaxHp * 90")]),
		new("rest: start the mana sit below MP percent", 50, Owner: "CP-17", Sites:
			[new(Journey, "RestAsync", "world.CurrentMp * 100 < world.MaxMp * 50")]),
		new("rest: end the mana sit at MP percent", 80, Owner: "CP-17", Sites:
			[new(Journey, "RestAsync", "world.CurrentMp * 100 >= world.MaxMp * 80")]),
		new("rest: quiet sits at most", 12, Owner: "CP-17", Sites:
			[new(Journey, "RestAsync", "quietIntervals >= 12")]),

		// Pending, owner CP-18: engage ranges and readiness thresholds of the shared helpers.
		new("router: ranged arrival radius (m)", 20f, Owner: "CP-18", Sites:
			[new("BotNavMeshRouter.cs", null, "private const float RangedRadius = 20f;")]),
		new("grid fallback: ranged arrival radius (m)", 20, Owner: "CP-18", Sites:
			[new("BotNavigationGeometry.cs", "GridRangedApproachPath", "arrivalRadius: 20,")]),
		new("shipped combat spawn: explore within (m)", 23, Owner: "CP-18", Sites:
			[new(Journey, "ApproachShippedCombatSpawnAsync", "anchor.Position, 23, navigator,")]),
		new("shipped combat spawn: target observed inside (m)", 23, Owner: "CP-18", Sites:
			[new(Journey, "ApproachShippedCombatSpawnAsync", "Distance(session.CurrentPosition, npc.Position) <= 23)")]),
		new("shipped combat spawn: refusal text names (m)", 23, Owner: "CP-18", Sites:
			[new(Journey, "ApproachShippedCombatSpawnAsync", "inside 23 m of {anchor.Position}")]),
		new("shipped combat spawn: pull candidates inside (m)", 30, Owner: "CP-18", Sites:
			[new(Journey, "ApproachShippedCombatSpawnAsync", "Distance(session.CurrentPosition, npc.Position) <= 30)")]),
		new("fight through: pull range (m)", 30f, Owner: "CP-18", Sites:
			[new(Journey, "TryFightThroughAsync", "const float PullRange = 30f;")]),
		new("clear around spot: rest below HP percent", 60, Owner: "CP-18", Sites:
			[new(Journey, "ClearAroundSpotAsync", "session.Api.World.MaxHp * 60 ||")]),
		new("clear around spot: rest below MP percent", 40, Owner: "CP-18", Sites:
			[new(Journey, "ClearAroundSpotAsync", "session.Api.World.MaxMp * 40)")]),
		new("move to pull spot: rest below HP percent", 80, Owner: "CP-18", Sites:
			[new(Journey, "MoveToPullSpotAsync", "session.Api.World.MaxHp * 80) await RestSafelyAsync(token);")]),
		new("pull and kill: rest after an add below HP percent", 60, Owner: "CP-18", Sites:
			[new(Journey, "PullAndKillAsync", "session.Api.World.MaxHp * 60 ||", 2)]),
		new("pull and kill: rest after an add below MP percent", 40, Owner: "CP-18", Sites:
			[new(Journey, "PullAndKillAsync", "session.Api.World.MaxMp * 40)", 2)]),
		new("pull and kill: rest before the target below HP percent", 80, Owner: "CP-18", Sites:
			[new(Journey, "PullAndKillAsync", "session.Api.World.MaxHp * 80 ||")]),
		new("pull and kill: rest before the target below MP percent", 60, Owner: "CP-18", Sites:
			[new(Journey, "PullAndKillAsync", "session.Api.World.MaxMp * 60)")]),

		// Pending, owner CP-19: movement inside a fight.
		new("fight loop: ranged route when farther than (m)", 25, Owner: "CP-19", Sites:
			[new(Journey, "TryKillCoreAsync", "Distance(session.CurrentPosition, destination) > 25")]),
		new("cast: close in after a range rejection (m)", 10f, Owner: "CP-19", Sites:
			[new(Journey, "CastAsync", "NaturalPriestCombatPolicy.MeleeReach - 1 : 10f")]),

		// Pending, owner CP-21: the Ishalgen campaign's ranges and readiness thresholds.
		new("Q2002 Sprigg hunt: route when farther than (m)", 25, Owner: "CP-21", Sites:
			[new(Journey, "AdvanceWheresRaeAsync", "Distance(session.CurrentPosition, candidate.Position) > 25")]),
		new("Q2002 Sprigg hunt: stand off at least (m)", 22, Owner: "CP-21", Sites:
			[new(Journey, "AdvanceWheresRaeAsync", "candidate.Position) >= 22", 2)]),
		new("Q2002 Sprigg hunt: select inside (m)", 25, Owner: "CP-21", Sites:
			[new(Journey, "AdvanceWheresRaeAsync", "Distance(session.CurrentPosition, candidate.Position) <= 25")]),
		new("Q2005 firing edge: ground inside (m)", 25, Owner: "CP-21", Sites:
			[new(Journey, "CompleteTeachingALessonAsync", "Distance(ground, target.Position) <= 25")]),
		new("Q2005 stalker search area: explore within (m)", 23, Owner: "CP-21", Sites:
			[new(Journey, "CompleteTeachingALessonAsync", "isolatedStalker, 23, navigator")]),
		new("Q2005 blocker: plan again when farther than (m)", 25, Owner: "CP-21", Sites:
			[new(Journey, "CompleteTeachingALessonAsync", "Distance(session.CurrentPosition, currentBlocker.Position) > 25")]),
		new("Return cooldown wait: rest below HP fraction", 0.75f, Owner: "CP-21", Sites:
			[new(Journey, "UseLearnedReturnToBindAsync", "session.Api.World.MaxHp * 0.75f")]),
		new("Q2005: plan the stalker pull at HP percent", 90, Owner: "CP-21", Sites:
			[new(Journey, "CompleteTeachingALessonAsync", "session.Api.World.CurrentHp * 100 >= session.Api.World.MaxHp * 90")]),
		new("Q2005: hold the search below HP percent", 90, Owner: "CP-21", Sites:
			[new(Journey, "CompleteTeachingALessonAsync", "session.Api.World.CurrentHp * 100 < session.Api.World.MaxHp * 90")]),
		new("Q2006: rest before a sack below HP percent", 80, Owner: "CP-21", Sites:
			[new(Journey, "CompleteHitThemWhereItHurtsAsync", "session.Api.World.MaxHp * 80) await RestSafelyAsync(token);")]),
		new("Q2007: rest before the camp below HP percent", 80, Owner: "CP-21", Sites:
			[new(Journey, "CompleteWheresRaeThisTimeAsync", "session.Api.World.MaxHp * 80 ||")]),
	];

	[Fact]
	public void EveryAssertedRowHoldsItsNumber()
	{
		foreach (Pin row in Rows.Where(row => !row.Pending))
		{
			if (row.Actual != null) Assert.True(Equals(row.Expected, row.Actual()), $"{row.Name}: the code no longer holds {Text(row.Expected)}.");
			if (row.Profile != null) Assert.True(Equals(row.Expected, row.Profile()), $"{row.Name}: the profile does not hold {Text(row.Expected)}.");
			string profile = row.Profile != null ? "on the profile" : row.Owner != null ? $"profile side by {row.Owner}" : "no item moves it";
			output.WriteLine($"asserted  {row.Name} = {Text(row.Expected)} ({profile})");
		}
	}

	[Fact]
	public void EveryPendingRowHasItsOwnerAndItsLiteralInTheSource()
	{
		List<string> moved = [];
		foreach (Pin row in Rows.Where(row => row.Pending))
		{
			Assert.True(row.Owner != null, $"{row.Name}: a pending row has no owner.");
			Assert.NotEmpty(row.Sites);
			List<string> found = [];
			foreach (Site site in row.Sites)
			{
				string[] lines = Find(site);
				if (lines.Length != site.Count)
					moved.Add($"{row.Name}: '{site.Fragment}' stands on {lines.Length} lines of {site.Method ?? site.Files}, not {site.Count} " +
						$"({string.Join(", ", lines)}). {row.Owner} turns this row on in the commit that moves the literal.");
				found.AddRange(lines);
			}
			output.WriteLine($"pending   {row.Name} = {Text(row.Expected)}, owner {row.Owner}, at {string.Join(", ", found.Distinct())}");
		}
		Assert.True(moved.Count == 0, string.Join(Environment.NewLine, moved));
	}

	[Fact]
	public void EveryRowIsNamedOnceAndOwnedByAnItemOfTheList()
	{
		Assert.Equal(Rows.Length, Rows.Select(row => row.Name).Distinct().Count());
		Assert.All(Rows, row =>
		{
			Assert.True(row.Owner == null || Owners.Contains(row.Owner), $"{row.Name}: {row.Owner} owns no pin row.");
			// A row that reads the profile has left its literal behind; one that reads a constant never had a site.
			Assert.True(row.Pending || row.Sites.Length == 0, $"{row.Name}: an asserted row still lists source sites.");
		});
		output.WriteLine($"{Rows.Count(row => !row.Pending)} rows asserted, {Rows.Count(row => row.Pending)} pending: " +
			string.Join(", ", Rows.Where(row => row.Pending).GroupBy(row => row.Owner).Select(group => $"{group.Key} {group.Count()}")));
	}

	/// <summary>The value both profiles of the accepted line give; they must agree.</summary>
	private static object Both(Func<NaturalClassProfile, object> read)
	{
		object priest = read(NaturalPriestProfile.Priest), cleric = read(NaturalPriestProfile.Cleric);
		Assert.Equal(priest, cleric);
		return priest;
	}

	/// <summary>One help-item gate at the four points the row names.</summary>
	private static string Gate(Func<NaturalHelpItemRules, Func<int, bool>> gate)
	{
		string On(NaturalClassProfile profile, int level) => gate(profile.HelpItems)(level) ? "on" : "off";
		return $"Priest 1: {On(NaturalPriestProfile.Priest, 1)}, Priest 9: {On(NaturalPriestProfile.Priest, 9)}, " +
			$"Cleric 10: {On(NaturalPriestProfile.Cleric, 10)}, Cleric 26: {On(NaturalPriestProfile.Cleric, 26)}";
	}

	private static object StandoffDefault(string parameter)
	{
		MethodInfo select = typeof(NaturalCombatStandoff).GetMethod(nameof(NaturalCombatStandoff.Select))!;
		return Convert.ToSingle(select.GetParameters().Single(candidate => candidate.Name == parameter).DefaultValue, CultureInfo.InvariantCulture);
	}

	private static string Text(object value) => value switch
	{
		float number => number.ToString(CultureInfo.InvariantCulture) + "f",
		IFormattable number => number.ToString(null, CultureInfo.InvariantCulture),
		_ => value.ToString()!,
	};

	/// <summary>The lines of the site's scope that hold its fragment, as file:line.</summary>
	private static string[] Find(Site site)
	{
		List<string> found = [];
		foreach (string path in Directory.GetFiles(BotSource.Value, site.Files, SearchOption.AllDirectories).Order(StringComparer.Ordinal))
		{
			string[] lines = File.ReadAllLines(path);
			foreach ((int first, int last) in site.Method == null ? [(0, lines.Length - 1)] : Bodies(lines, site.Method))
				for (int line = first; line <= last; line++)
					if (lines[line].Contains(site.Fragment, StringComparison.Ordinal))
						found.Add($"{Path.GetFileName(path)}:{line + 1}");
		}
		return found.ToArray();
	}

	/// <summary>The bodies of every method or local function of that name: from the declaration to its closing brace.</summary>
	private static IEnumerable<(int First, int Last)> Bodies(string[] lines, string method)
	{
		Regex declaration = new(@"^(?:(?:public|private|protected|internal|static|async|override|sealed)\s+)*[\w<>\[\]?,.() ]+?\s+" +
			Regex.Escape(method) + @"\s*\(");
		Regex statement = new(@"^(?:return|await|var|if|else|while|for|foreach|switch|throw|case|using|lock)\b");
		for (int index = 0; index < lines.Length; index++)
		{
			string text = lines[index].Trim();
			if (!declaration.IsMatch(text) || statement.IsMatch(text) || text.EndsWith(';') || text[..text.IndexOf(method, StringComparison.Ordinal)].Contains('='))
				continue;
			int depth = 0;
			bool opened = false;
			for (int line = index; line < lines.Length; line++)
			{
				foreach (char character in Code(lines[line]))
				{
					if (character == '{') { depth++; opened = true; }
					else if (character == '}') depth--;
				}
				if (!opened || depth > 0) continue;
				yield return (index, line);
				break;
			}
		}
	}

	/// <summary>A source line without its string and character literals and its line comment, for counting braces.</summary>
	private static string Code(string line)
	{
		string code = Regex.Replace(line, @"""(?:[^""\\]|\\.)*""|'(?:[^'\\]|\\.)'", "");
		int comment = code.IndexOf("//", StringComparison.Ordinal);
		return comment < 0 ? code : code[..comment];
	}

	private static readonly Lazy<string> BotSource = new(() => Path.GetFullPath(Path.Combine(
		Path.GetDirectoryName(ScenarioManifest.FindDefaultPath())!, "../../tests/Aion.Bots")));
}
