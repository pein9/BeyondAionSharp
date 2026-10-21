using Aion.GameServer.Custom.Companion;
using Aion.GameServer.Model;
using Aion.GameServer.TestKit;
using System.Text.Json.Nodes;

namespace Aion.GameServer.Tests;

/// <summary>D41 (docs/companion-bots.md): the companion profiles the server reads, and the class a player names.</summary>
public sealed class CompanionProfileTests
{
    private static string Folder => Path.Combine(RealStaticData.RepoRoot(), "game-server", "data", "companions");

    [Fact]
    public void EveryCheckedInProfileIsReadWhole()
    {
        IReadOnlyList<CompanionProfile> profiles = CompanionProfile.LoadAll(Folder);

        // Every class at 26, and the eight original classes at 31 too (step M8 of docs/natural-morheim-leveling.md), and again
        // from the gear check-in at 36, where the quests had carried them to 37 and 38 (NL-4 of docs/natural-leg-36.md).
        // The Templar reached 38 during NL-4; keep its earlier 37 tier beside the new one.
        Assert.Equal(28, profiles.Count);
        Assert.Equal(11, profiles.Where(profile => profile.Level == CompanionService.Level).Select(profile => profile.PlayerClass).Distinct().Count());
        Assert.Equal(8, profiles.Where(profile => profile.Level == 31).Select(profile => profile.PlayerClass).Distinct().Count());
        Assert.Equal(8, profiles.Where(profile => profile.Level is 37 or 38).Select(profile => profile.PlayerClass).Distinct().Count());
        Assert.All(profiles, profile =>
        {
            Assert.Equal(Race.ASMODIANS, profile.Race);
            Assert.Contains(profile.Level, new[] { CompanionService.Level, 31, 37, 38 });
            Assert.False(profile.PlayerClass.IsStartingClass());
            Assert.NotEmpty(profile.Equipment);
            Assert.NotEmpty(profile.Inventory);
            Assert.NotEmpty(profile.Skills);
            Assert.All(profile.Skills.Where(skill => skill.Id < 30000 || skill.Id == 40009), skill => Assert.Equal(1, skill.Level));
            Assert.Equal(53, profile.Appearance.Count);
            Assert.Equal((float)profile.Appearance["height"], profile.NewAppearance().GetHeightScale());
            Assert.Equal((int)(profile.Appearance.TryGetValue("jaw_height", out double chinHeight)
                ? chinHeight : profile.Appearance["chin_height"]), profile.NewAppearance().GetChinHeight());
            Assert.Equal((int)(profile.Appearance.TryGetValue("chin_jut", out double cheekBones)
                ? cheekBones : profile.Appearance["cheek_bones"]), profile.NewAppearance().GetCheekBones());
            Assert.Equal((int)profile.Appearance["skin_rgb"], profile.NewAppearance().GetSkinRGB());
        });
    }

    [Theory]
    [InlineData(1, 2)]
    [InlineData(29999, 9)]
    [InlineData(40009, 399)]
    [InlineData(1001, 0)]
    public void LegacySkillGradesAreRefused(int id, int level)
    {
        InvalidDataException error = Assert.Throws<InvalidDataException>(() => CompanionProfile.Parse(WithSkills((id, level))));

        Assert.Contains($"skill {id} has level {level}", error.Message);
    }

    [Fact]
    public void ProfessionRanksSurviveAlongsideCombatAndMorphingSkills()
    {
        (int Id, int Level)[] skills = [(1001, 1), (29999, 1), (30000, 49), (30001, 99), (30002, 399),
            (30003, 499), (40001, 399), (40008, 99), (40009, 1), (40010, 199)];

        CompanionProfile profile = CompanionProfile.Parse(WithSkills(skills));

        Assert.Equal(skills, profile.Skills.Select(skill => (skill.Id, skill.Level)));
    }

    private static string WithSkills(params (int Id, int Level)[] skills)
    {
        JsonNode profile = JsonNode.Parse(File.ReadAllText(Path.Combine(Folder, "asmodians-cleric-26.json")))!;
        profile["skills"] = new JsonArray(skills.Select(skill => (JsonNode)new JsonObject
            { ["id"] = skill.Id, ["level"] = skill.Level }).ToArray());
        return profile.ToJsonString();
    }

    [Fact]
    public void AProfileWithAColumnTheAppearanceLacksIsRefused()
    {
        string json = File.ReadAllText(Path.Combine(Folder, "asmodians-cleric-26.json")).Replace("\"jaw_line\"", "\"jaw_lime\"", StringComparison.Ordinal);

        Assert.Throws<InvalidDataException>(() => CompanionProfile.Parse(json));
    }

    [Theory]
    [InlineData("cleric", PlayerClass.CLERIC)]
    [InlineData("Templar", PlayerClass.TEMPLAR)]
    [InlineData("Spirit Master", PlayerClass.SPIRIT_MASTER)]
    [InlineData("spirit_master", PlayerClass.SPIRIT_MASTER)]
    [InlineData("SPIRITMASTER", PlayerClass.SPIRIT_MASTER)]
    [InlineData("aethertech", PlayerClass.RIDER)]
    [InlineData("gunslinger", PlayerClass.GUNNER)]
    [InlineData("Songweaver", PlayerClass.BARD)]
    public void AClassIsNamedInAnyCaseAndByTheClientsName(string spoken, PlayerClass expected)
    {
        Assert.True(CompanionService.TryParseClass(spoken, out PlayerClass playerClass));
        Assert.Equal(expected, playerClass);
    }

    [Theory]
    [InlineData("")]
    [InlineData("healer")]
    [InlineData("26")]
    public void AWordThatIsNoClassIsNotOne(string spoken)
    {
        Assert.False(CompanionService.TryParseClass(spoken, out _));
    }
}
