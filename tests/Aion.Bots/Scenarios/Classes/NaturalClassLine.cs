using Aion.GameServer.Model;

namespace Aion.Bots.Scenarios.Classes;

/// <summary>
/// Which character a natural run plays (docs/natural-class-profiles.md, the seam, section 1): the starter class, the
/// second class it takes at Ascension or none, and the SIM account and character name the operator gave the line.
/// Every line is a male Asmodian. What the server decides for a class is in <see cref="NaturalClassLineContract"/>.
/// </summary>
/// <param name="Second">The class chosen at Ascension; null for a line that stops before it.</param>
/// <param name="CeremonyItemId">NR-30: the weapon the line takes at the ceremony (Q2009), the operator's choice for its
/// class (NR-Q5). Null is the reviewed bridge's pick, the staff, which the Cleric's and the Chanter's lists offer. A line
/// whose second class is offered no staff names its pick here.</param>
public sealed record NaturalClassLine(string Id, PlayerClass Starter, PlayerClass? Second, int SimAccountId, string CharacterName,
	int? CeremonyItemId = null)
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

	/// <summary>NR-50: the Warrior who becomes a Templar and takes the sword at the ceremony (NR-Q5). It has a name of its
	/// own, because the lines of a round play in one world (rule (w)).</summary>
	public static NaturalClassLine WarriorTemplar { get; } = new("warrior-templar", PlayerClass.WARRIOR, PlayerClass.TEMPLAR, 41, "Asimtemplar",
		CeremonyItemId: 100000640);

	/// <summary>NR-60: the Mage who becomes a Sorcerer and takes the spellbook at the ceremony (NR-Q5).</summary>
	public static NaturalClassLine MageSorcerer { get; } = new("mage-sorcerer", PlayerClass.MAGE, PlayerClass.SORCERER, 41, "Asimsorcerer",
		CeremonyItemId: 100600532);

	/// <summary>NR-80: the Warrior who becomes a Gladiator and takes the greatsword at the ceremony (NR-Q5).</summary>
	public static NaturalClassLine WarriorGladiator { get; } = new("warrior-gladiator", PlayerClass.WARRIOR, PlayerClass.GLADIATOR, 41, "Asimgladiator",
		CeremonyItemId: 100900488);

	/// <summary>NR-90: the Scout who becomes an Assassin and takes the dagger at the ceremony (NR-Q5).</summary>
	public static NaturalClassLine ScoutAssassin { get; } = new("scout-assassin", PlayerClass.SCOUT, PlayerClass.ASSASSIN, 41, "Asimassassin",
		CeremonyItemId: 100200605);

	/// <summary>NR-100: the Scout who becomes a Ranger and takes the bow at the ceremony (NR-Q5).</summary>
	public static NaturalClassLine ScoutRanger { get; } = new("scout-ranger", PlayerClass.SCOUT, PlayerClass.RANGER, 41, "Asimranger",
		CeremonyItemId: 101700515);

	/// <summary>NR-110: the Mage who becomes a Spirit Master and takes the spellbook at the ceremony (NR-Q5).</summary>
	public static NaturalClassLine MageSpiritMaster { get; } = new("mage-spirit-master", PlayerClass.MAGE, PlayerClass.SPIRIT_MASTER, 41, "Asimspirit",
		CeremonyItemId: 100600532);

	/// <summary>NR-120: the Engineer who becomes a Gunner and takes the pistol at the ceremony (NR-Q5).</summary>
	public static NaturalClassLine EngineerGunner { get; } = new("engineer-gunner", PlayerClass.ENGINEER, PlayerClass.GUNNER, 41, "Asimgunner",
		CeremonyItemId: 101800506);

	/// <summary>NR-130: the Engineer who becomes a Rider and takes the cipher-blade at the ceremony (NR-Q5).</summary>
	public static NaturalClassLine EngineerRider { get; } = new("engineer-rider", PlayerClass.ENGINEER, PlayerClass.RIDER, 41, "Asimrider",
		CeremonyItemId: 102100489);

	/// <summary>NR-140: the Artist who becomes a Bard and takes the harp at the ceremony (NR-Q5).</summary>
	public static NaturalClassLine ArtistBard { get; } = new("artist-bard", PlayerClass.ARTIST, PlayerClass.BARD, 41, "Asimbard",
		CeremonyItemId: 102000523);

	/// <summary>Every line a run can name. Each class's first profile item adds its line here.</summary>
	public static IReadOnlyList<NaturalClassLine> All { get; } =
	[
		PriestCleric, PriestChanter, Warrior, Mage, Artist, Engineer, Scout, WarriorTemplar, MageSorcerer, WarriorGladiator, ScoutAssassin,
		ScoutRanger, MageSpiritMaster, EngineerGunner, EngineerRider, ArtistBard,
	];

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

	/// <summary>NR-31: the second class as a gate names it when it refuses (<c>Cleric</c>, <c>Spirit Master</c>). A line
	/// that takes none is refused by every such gate, which then asks for "the second class".</summary>
	public string SecondName => Second is { } second
		? string.Join(' ', second.ToString().Split('_').Select(word => char.ToUpperInvariant(word[0]) + word[1..].ToLowerInvariant()))
		: "second class";

	/// <summary>CP-23 (CP-Q13, on its default): a line of a new starter runs the equipment check after each completed
	/// Ishalgen quest. A line whose starter is the Priest keeps the accepted Priest's check points, at the end of a rest,
	/// so its Ishalgen trace stays as recorded: the accepted line, and (CP-32) the Chanter line, whose levels 1-9 are the
	/// accepted Priest's up to the class choice.</summary>
	public bool ChecksGearAfterIshalgenTurnIns => Starter != PlayerClass.PRIEST;

	/// <summary>The class is one this line's character can be: its starter or its second class.</summary>
	public bool Holds(PlayerClass playerClass) => playerClass == Starter || playerClass == Second;
}
