using System.Reflection;
using Aion.GameServer.Dao;

namespace Aion.GameServer.Tests;

/// <summary>Upstream 51d7a19cb: saved effects keep which positions rolled a magical critical, one bit per position.</summary>
public sealed class PlayerEffectsMagicalCriticalsPortTests
{
    [Fact]
    public void QueriesStoreAndLoadTheMagicalCriticalBits()
    {
        Assert.Equal("INSERT INTO `player_effects` (`player_id`, `skill_id`, `skill_lvl`, `remaining_time`, `end_time`, `force_type`, `magical_criticals`) VALUES (?,?,?,?,?,?,?)",
            PlayerEffectsDAO.INSERT_QUERY);
        Assert.Equal("SELECT `skill_id`, `skill_lvl`, `remaining_time`, `end_time`, `force_type`, `magical_criticals` FROM `player_effects` WHERE `player_id`=?",
            PlayerEffectsDAO.SELECT_QUERY);
    }

    [Fact]
    public void SchemaAndMigrationAddTheColumn()
    {
        Assert.Contains("`magical_criticals` tinyint NOT NULL DEFAULT '0',", File.ReadAllText(RepoFile("game-server", "sql", "aion_gs.sql")));
        Assert.Contains("ADD COLUMN `magical_criticals` TINYINT NOT NULL DEFAULT '0' AFTER `force_type`;",
            File.ReadAllText(RepoFile("game-server", "sql", "update.sql")));
    }

    [Theory]
    [InlineData(0, new int[0])]
    [InlineData(1, new[] { 1 })]
    [InlineData(0b1010, new[] { 2, 4 })]
    [InlineData(0b1111, new[] { 1, 2, 3, 4 })]
    public void BitZeroDecodesToPositionOne(int bits, int[] expectedPositions)
    {
        MethodInfo decode = typeof(PlayerEffectsDAO).GetMethod("DecodeMagicalCriticalPositions", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(typeof(PlayerEffectsDAO).FullName, "DecodeMagicalCriticalPositions");

        var positions = Assert.IsAssignableFrom<ISet<int>>(decode.Invoke(null, new object[] { bits }));

        Assert.Equal(expectedPositions, positions.OrderBy(p => p));
    }

    private static string RepoFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(new[] { directory.FullName }.Concat(parts).ToArray());
            if (File.Exists(candidate))
                return candidate;
            directory = directory.Parent;
        }
        throw new FileNotFoundException("Could not find repository file", Path.Combine(parts));
    }
}
