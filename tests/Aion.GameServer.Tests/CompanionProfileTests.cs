using Aion.GameServer.Custom.Companion;
using Aion.GameServer.Model;
using Aion.GameServer.TestKit;

namespace Aion.GameServer.Tests;

/// <summary>D41 (docs/companion-bots.md): the companion profiles the server reads, and the class a player names.</summary>
public sealed class CompanionProfileTests
{
    private static string Folder => Path.Combine(RealStaticData.RepoRoot(), "game-server", "data", "companions");

    [Fact]
    public void EveryCheckedInProfileIsReadWhole()
    {
        IReadOnlyList<CompanionProfile> profiles = CompanionProfile.LoadAll(Folder);

        Assert.Equal(11, profiles.Count);
        Assert.Equal(11, profiles.Select(profile => profile.PlayerClass).Distinct().Count());
        Assert.All(profiles, profile =>
        {
            Assert.Equal((Race.ASMODIANS, CompanionService.Level), (profile.Race, profile.Level));
            Assert.False(profile.PlayerClass.IsStartingClass());
            Assert.NotEmpty(profile.Equipment);
            Assert.NotEmpty(profile.Inventory);
            Assert.NotEmpty(profile.Skills);
            Assert.Equal(53, profile.Appearance.Count);
            Assert.Equal((float)profile.Appearance["height"], profile.NewAppearance().GetHeight());
            Assert.Equal((int)profile.Appearance["jaw_height"], profile.NewAppearance().GetJawHeigh());
            Assert.Equal((int)profile.Appearance["skin_rgb"], profile.NewAppearance().GetSkinRGB());
        });
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
