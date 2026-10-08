using Aion.GameServer.Model;

namespace Aion.Bots.Scenarios.Classes;

/// <summary>
/// Which character a natural run plays (docs/natural-class-profiles.md, the seam, section 1): the starter class, the
/// second class it takes at Ascension or none, and the SIM account and character name the operator gave the line.
/// Every line is a male Asmodian. What the server decides for a class is in <see cref="NaturalClassLineContract"/>.
/// </summary>
/// <param name="Second">The class chosen at Ascension; null for a line that stops before it.</param>
public sealed record NaturalClassLine(string Id, PlayerClass Starter, PlayerClass? Second, int SimAccountId, string CharacterName)
{
	/// <summary>The journey test reads the line id from this variable; unset means <see cref="Default"/>.</summary>
	public const string EnvironmentVariable = "CP_CLASS";

	/// <summary>The accepted line: the Priest who becomes a Cleric.</summary>
	public static NaturalClassLine PriestCleric { get; } = new("priest-cleric", PlayerClass.PRIEST, PlayerClass.CLERIC, 41, "Asimnjour");

	/// <summary>CP-32: the Priest who becomes a Chanter. It is the accepted line's character, on its account and under
	/// its name, up to the class choice at Munin; a run plays one line on its own schema, so the two never meet.</summary>
	public static NaturalClassLine PriestChanter { get; } = new("priest-chanter", PlayerClass.PRIEST, PlayerClass.CHANTER, 41, "Asimnjour");

	/// <summary>CP-42: the Warrior, levels 1-9. It takes no second class in this plan and stops at Munin (CP-Q19 for
	/// the account and the name).</summary>
	public static NaturalClassLine Warrior { get; } = new("warrior", PlayerClass.WARRIOR, null, 41, "Asimwar");

	/// <summary>CP-46: the Mage, levels 1-9, to Munin (CP-Q19 for the account and the name).</summary>
	public static NaturalClassLine Mage { get; } = new("mage", PlayerClass.MAGE, null, 41, "Asimmage");

	/// <summary>CP-47: the Artist, levels 1-9, to Munin (CP-Q19 for the account and the name).</summary>
	public static NaturalClassLine Artist { get; } = new("artist", PlayerClass.ARTIST, null, 41, "Asimartist");

	/// <summary>CP-48: the Engineer, levels 1-9, to Munin (CP-Q19 for the account and the name).</summary>
	public static NaturalClassLine Engineer { get; } = new("engineer", PlayerClass.ENGINEER, null, 41, "Asimengi");

	/// <summary>CP-49: the Scout, levels 1-9, to Munin (CP-Q19 for the account and the name).</summary>
	public static NaturalClassLine Scout { get; } = new("scout", PlayerClass.SCOUT, null, 41, "Asimscout");

	/// <summary>Every line a run can name. Each class's first profile item adds its line here.</summary>
	public static IReadOnlyList<NaturalClassLine> All { get; } = [PriestCleric, PriestChanter, Warrior, Mage, Artist, Engineer, Scout];

	public static NaturalClassLine Default => All[0];

	/// <summary>The line with this id; <see cref="Default"/> when none is given.</summary>
	public static NaturalClassLine Parse(string? id) => string.IsNullOrWhiteSpace(id) ? Default
		: All.FirstOrDefault(line => line.Id == id) ?? throw new ArgumentException(
			$"Unknown natural class line '{id}'. Known lines: {string.Join(", ", All.Select(line => line.Id))}.", nameof(id));

	/// <summary>CP-20: the starter as step labels name it (<c>priest</c>). A character keeps this name in labels after its
	/// class change, as the Priest who became a Cleric always did.</summary>
	public string StarterLabel => Starter.ToString().ToLowerInvariant();

	/// <summary>The starter as messages name it (<c>Priest</c>).</summary>
	public string StarterName => char.ToUpperInvariant(StarterLabel[0]) + StarterLabel[1..];

	/// <summary>CP-23 (CP-Q13, on its default): a line of a new starter runs the equipment check after each completed
	/// Ishalgen quest. A line whose starter is the Priest keeps the accepted Priest's check points, at the end of a rest,
	/// so its Ishalgen trace stays as recorded: the accepted line, and (CP-32) the Chanter line, whose levels 1-9 are the
	/// accepted Priest's up to the class choice.</summary>
	public bool ChecksGearAfterIshalgenTurnIns => Starter != PlayerClass.PRIEST;

	/// <summary>The class is one this line's character can be: its starter or its second class.</summary>
	public bool Holds(PlayerClass playerClass) => playerClass == Starter || playerClass == Second;
}
