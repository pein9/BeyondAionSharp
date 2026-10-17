using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.Bots.Scenarios.Classes;

/// <summary>
/// MR-g: the one stigma each second class wears below level 30 (docs/natural-muster.md). Q2900 "No Escaping Destiny"
/// opens the slot and pays the Stigma Support Bundle, from which a character takes one stone of its class. The stone
/// taken is the one whose skill the class's fight table can cast as it stands: on one target or on the character itself,
/// no charge and no area. A Stigma Master's window is where a stone is put in (Java StigmaService.notifyEquipAction: the
/// first slot until level 30, 25,000 Kinah for a stone of this grade).
/// </summary>
public static class NaturalStigma
{
	public const int BundleItemId = 188053787;

	/// <summary>The first regular stigma slot (Java ItemSlot.STIGMA1).</summary>
	public const long Slot = 1L << 30;

	public const int InstallationBaseFee = 25_000;

	/// <summary>The title every Stigma Master carries.</summary>
	public const int MasterTitleId = 350410;

	/// <param name="StoneItemId">The stone taken from the bundle.</param>
	/// <param name="Role">The role its skill has in the class's table.</param>
	/// <param name="Why">What the skill does for the class.</param>
	public sealed record Choice(int StoneItemId, string Role, string Why);

	public static readonly IReadOnlyDictionary<PlayerClass, Choice> Choices = new Dictionary<PlayerClass, Choice>
	{
		[PlayerClass.GLADIATOR] = new(140001304, "crippling",
			"Crippling Cut: its hardest hit, on a target it has thrown down, which Wrathful Strike does seven times in ten."),
		[PlayerClass.TEMPLAR] = new(140001328, "inquisitor",
			"Inquisitor's Blow: an attack from 15 m, where it had only Taunt: its pull, and a hit every 30 s."),
		[PlayerClass.RANGER] = new(140001363, "bow-blessing",
			"Bow of Blessing: a fifth more attack with the bow for one minute in three."),
		[PlayerClass.ASSASSIN] = new(140001345, "searching",
			"Searching Strike: a hard hit that stuns for 1.5 s, every 30 s, from any side."),
		[PlayerClass.SORCERER] = new(140001384, "ice-harpoon",
			"Ice Harpoon: a second harpoon, ready every 6 s."),
		[PlayerClass.SPIRIT_MASTER] = new(140001399, "cyclone",
			"Cyclone of Wrath: a hit and 8 s of damage after it, every 12 s."),
		[PlayerClass.CLERIC] = new(140001431, "saving-grace",
			"Saving Grace: a heal without a cast for a character at half its health, itself or a group member."),
		[PlayerClass.CHANTER] = new(140001414, "life",
			"Word of Life: 10 s of healing for itself and its group, without a cast."),
		[PlayerClass.BARD] = new(140001484, "hymn",
			"Hymn of Rejuvenation: a heal without a cast, and 8 s of healing after it."),
		[PlayerClass.GUNNER] = new(140001452, "grab",
			"Power Grab: a shot that gives its damage back as health and half of it as mana, with the pistols it holds."),
		[PlayerClass.RIDER] = new(140001466, "drill",
			"Drillbore: the one stone of its bundle that is neither a charge nor an area: a hit that stuns for 3 s."),
	};

	/// <summary>L2 (docs/level-31-profiles.md): the second regular stigma slot (Java ItemSlot.STIGMA2), which opens at
	/// level 30 (Java StigmaService.getPossibleStigmaCount).</summary>
	public const long SecondSlot = 1L << 31;
	public const int SecondSlotLevel = 30;

	/// <param name="StoneItemId">The stone bought for the second slot.</param>
	/// <param name="VendorNpcId">Who sells it: Giles (204289) or Sinamara (204290) in Pandaemonium.</param>
	/// <param name="Role">The role its skill has in the class's table.</param>
	/// <param name="Why">What the skill does for the class.</param>
	public sealed record Second(int StoneItemId, int VendorNpcId, string Role, string Why);

	/// <summary>L2: the original eight's second stones, chosen by MR-g's rule from the level-20 stones Pandaemonium sells
	/// (695,500 Kinah in the SIM world).</summary>
	public static readonly IReadOnlyDictionary<PlayerClass, Second> Seconds = new Dictionary<PlayerClass, Second>
	{
		[PlayerClass.GLADIATOR] = new(140001305, 204289, "draining",
			"Draining Blow: a hit on a thrown-down target that gives its damage back as health, in Crippling Cut's window."),
		[PlayerClass.TEMPLAR] = new(140001325, 204289, "punishing-thrust",
			"Punishing Thrust: its hardest hit, weapon +986 every minute, and hate for the tank."),
		[PlayerClass.ASSASSIN] = new(140001344, 204289, "rune-knife",
			"Rune Knife: a 20 m throw that carves the rune its rune attacks burst; a pull that hits."),
		[PlayerClass.RANGER] = new(140001357, 204289, "dilation",
			"Dilation Arrow: the target attacks at half its speed."),
		[PlayerClass.SORCERER] = new(140001375, 204290, "storm-strike",
			"Storm Strike: 2,716 damage behind a 4 s cast, as the opener."),
		[PlayerClass.SPIRIT_MASTER] = new(140001396, 204290, "freedom",
			"Magic's Freedom: +500 magic boost for every caster on the target."),
		[PlayerClass.CLERIC] = new(140001439, 204290, "noble-grace",
			"Noble Grace: 15 s of +35% healing received and +1,200 health for the group, without a cast."),
		[PlayerClass.CHANTER] = new(140001412, 204290, "inspiration",
			"Word of Inspiration: 15 s of +12% physical attack, +80 accuracy and +80 magic boost for the group."),
	};

	/// <summary>
	/// The skills the class's stone teaches, with the level each comes at: every skill of the stone's groups that the
	/// class's skill tree holds as a stigma row (Java StigmaService.addStigmaSkills). Empty for a class without a stone.
	/// </summary>
	public static IReadOnlyDictionary<int, int> Skills(StaticData data, PlayerClass playerClass, Race race = Race.ASMODIANS) =>
		Choices.TryGetValue(playerClass, out Choice? choice) ? StoneSkills(data, playerClass, choice.StoneItemId, race) : new Dictionary<int, int>();

	/// <summary>L2: the skills any one stone teaches the class, the second stone's among them.</summary>
	public static IReadOnlyDictionary<int, int> StoneSkills(StaticData data, PlayerClass playerClass, int stoneItemId, Race race = Race.ASMODIANS)
	{
		ArgumentNullException.ThrowIfNull(data);
		var skills = new Dictionary<int, int>();
		Stigma stone = data.ItemDataDh.GetItemTemplate(stoneItemId)?.GetStigma()
			?? throw new InvalidDataException($"Item {stoneItemId} is no stigma stone.");
		foreach (string group in stone.GetGainSkillGroups())
			foreach (SkillTemplate template in data.SkillDataDh.GetSkillTemplatesByGroup(group) ?? [])
				foreach (SkillLearnTemplate row in data.SkillTreeDataDh.GetTemplatesForSkill(template.GetSkillId(), playerClass, race))
					if (row.IsStigma()) skills.TryAdd(row.GetSkillId(), row.GetMinLevel());
		if (skills.Count == 0) throw new InvalidDataException($"Stone {stoneItemId} teaches a {playerClass} no skill.");
		return skills;
	}
}
