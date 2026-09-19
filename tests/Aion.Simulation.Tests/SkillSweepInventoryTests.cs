using System.Xml.Linq;
using Aion.GameServer.Model;
using Aion.GameServer.TestKit;

namespace Aion.Simulation.Tests;

public sealed class SkillSweepInventoryTests
{
	[Fact]
	public void CompleteShippedTreeIncludesEveryClassAndNonCastableProfessionSkills()
	{
		var rows = SkillSweepInventory.Load(Path.Combine(RealStaticData.RepoRoot(), "game-server", "data", "static_data"));
		Assert.Equal(5033, rows.Count);
		Assert.Equal(rows.Count, rows.Select(r => r.Id).Distinct().Count());
		Assert.Equal(Enum.GetValues<PlayerClass>().Order(), rows.Select(r => r.Class).Distinct().Order());
		Assert.All(rows, r => { Assert.True(r.SkillId > 0); Assert.InRange(r.SkillLevel, 0, byte.MaxValue); Assert.True(r.MinimumLevel > 0); });
		Assert.Contains(rows, r => r.Action == SkillSweepAction.Passive);
		Assert.Contains(rows, r => r.Action == SkillSweepAction.Cast && r.Race == Race.ASMODIANS);
		Assert.Equal(34, rows.Count(r => r.Action == SkillSweepAction.Gather));
		Assert.Equal(17, rows.Count(r => r.Action == SkillSweepAction.Craft));
		Assert.Equal(0, Assert.Single(rows, r => r.SkillId == 3242).SkillLevel);
	}

	[Fact]
	public void AdvancedClassesRetainSubLevelTenStartingClassSkills()
	{
		var rows = SkillSweepInventory.Expand(
			[XElement.Parse("<skill skillId='1' classId='WARRIOR' minLevel='9'/>")], [Skill(1, "ACTIVE")]);
		Assert.Equal(new[] { PlayerClass.WARRIOR, PlayerClass.GLADIATOR, PlayerClass.TEMPLAR }.Order(), rows.Select(r => r.Class).Order());
	}

	[Fact]
	public void GlobalSkillsExpandByClassWhileRaceAndExplicitClassRemainConstrained()
	{
		var rows = SkillSweepInventory.Expand(
			[Learn(1), XElement.Parse("<skill skillId='2' classId='CHANTER' race='ASMODIANS' minLevel='20'/>")],
			[Skill(1, "PASSIVE"), Skill(2, "ACTIVE")]);
		Assert.Equal(18, rows.Count);
		Assert.Equal(17, rows.Count(r => r.SkillId == 1 && r.Action == SkillSweepAction.Passive && r.Race == Race.ELYOS));
		var chanter = Assert.Single(rows, r => r.SkillId == 2);
		Assert.Equal(PlayerClass.CHANTER, chanter.Class); Assert.Equal(Race.ASMODIANS, chanter.Race);
		Assert.Equal(20, chanter.MinimumLevel); Assert.Equal(SkillSweepAction.Cast, chanter.Action);
	}

	[Fact]
	public void MissingDuplicateAndUnknownKindsCannotDisappearFromTheInventory()
	{
		Assert.Throws<InvalidDataException>(() => SkillSweepInventory.Expand([], []));
		Assert.Throws<InvalidDataException>(() => SkillSweepInventory.Expand([Learn(1)], []));
		Assert.Throws<InvalidDataException>(() => SkillSweepInventory.Expand([Learn(1), Learn(1)], [Skill(1, "ACTIVE")]));
		Assert.Throws<InvalidDataException>(() => SkillSweepInventory.Expand([Learn(1)], [Skill(1, "NONE")]));
		var unknownClass = Learn(1); unknownClass.SetAttributeValue("classId", "99");
		Assert.Throws<InvalidDataException>(() => SkillSweepInventory.Expand([unknownClass], [Skill(1, "ACTIVE")]));
	}

	private static XElement Learn(int id) => new("skill", new XAttribute("skillId", id), new XAttribute("minLevel", 1));
	private static XElement Skill(int id, string activation) => new("skill_template", new XAttribute("skill_id", id),
		new XAttribute("lvl", 1), new XAttribute("activation", activation));
}
