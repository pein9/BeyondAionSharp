using System.Text.RegularExpressions;
using Aion.GameServer.QuestEngine;
using Aion.GameServer.TestKit;

namespace Aion.GameServer.Tests;

public sealed partial class QuestSpawnAnalyzerTests
{
    [Fact]
    public void CheckedInHandlerSpawnIdsMatchCSharpSources()
    {
        string repoRoot = RealStaticData.RepoRoot();
        string[] handlerRoots =
        [
            Path.Combine(repoRoot, "src", "Aion.GameServer", "Handlers", "Instance"),
            Path.Combine(repoRoot, "src", "Aion.GameServer", "Handlers", "Quest"),
            Path.Combine(repoRoot, "src", "Aion.GameServer", "Handlers", "AI"),
        ];
        HashSet<int> expected = handlerRoots
            .SelectMany(root => Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
            .SelectMany(path => SpawnArgumentRegex().Matches(File.ReadAllText(path)).Cast<Match>())
            .SelectMany(match => NpcIdRegex().Matches(match.Groups["argument"].Value).Cast<Match>())
            .Select(match => int.Parse(match.Value, System.Globalization.CultureInfo.InvariantCulture))
            .ToHashSet();

        Assert.Equal(expected.OrderBy(id => id), QuestSpawnAnalyzer.LoadNpcIdsSpawnedByHandlers().OrderBy(id => id));
    }

    [Fact]
    public void AnalysisResultExposesUnobtainableAndSpawnUnreachableQuestIds()
    {
        QuestSpawnAnalysisResult result = QuestSpawnAnalysisResult.Create(
            [90001],
            new Dictionary<HashSet<int>, List<int>>(HashSet<int>.CreateSetComparer())
            {
                [[1102, 1101]] = [203001, 203000],
                [[2101]] = [204000],
            });

        Assert.Equal([90001], result.UnobtainableQuestIds);
        Assert.Equal([1101, 1102, 2101], result.UnreachableQuestIds.OrderBy(id => id));
        Assert.Equal([203000, 203001], result.MissingSpawns[0].NpcIds);
        Assert.Equal([1101, 1102], result.MissingSpawns[0].QuestIds);
    }

    [GeneratedRegex(@"\bSpawn\s*\((?<argument>[^,\r\n]*)")]
    private static partial Regex SpawnArgumentRegex();

    [GeneratedRegex(@"(?<!\d)\d{6}(?!\d)")]
    private static partial Regex NpcIdRegex();
}
