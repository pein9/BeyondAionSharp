using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.Templates;
using Aion.GameServer.Model.Templates.Housing;
using Aion.GameServer.Model.Templates.Quest;
using Aion.GameServer.QuestEngine;
using Aion.GameServer.QuestEngine.Handlers.Models;
using Aion.GameServer.TestKit;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed partial class QuestSpawnAnalyzerTests
{
    [Fact]
    public void NamedSourceHelpersAndCommentBoundariesMatchExactJavaVectors()
    {
        using var fixture = SpawnFixture();
        foreach (var c in fixture.RootElement.GetProperty("regexCases").EnumerateArray())
            Assert.Equal(c.GetProperty("expected").EnumerateArray().Select(v => v.GetInt32()), ExtractSourceIds(c.GetProperty("csharpSource").GetString()!));
        // Preserve native C# whitespace and full conditional-argument handling from the existing generator.
        Assert.Equal([211636], ExtractSourceIds("x Spawn (211636, 1);"));
        Assert.Equal([236227, 855708, 855709], ExtractSourceIds("x Spawn(npcId == 236227 ? 855708 : 855709, 1);"));
        Assert.Empty(ExtractSourceIds("x Spawn(1234567, 1);"));
        Assert.Contains(211621, QuestSpawnAnalyzer.LoadNpcIdsSpawnedByHandlers()); // SpawnForFiveMinutes in the preserved Q2223 handler.
    }

    [Fact]
    public void KillAlternativesIncludeSpawnerAndKeepFirstMatchAndSuperclassFallback()
    {
        using var fixture = SpawnFixture();
        foreach (var c in fixture.RootElement.GetProperty("killCases").EnumerateArray())
        {
            var data = new KillProbe { monster = ParseMonsters(c.GetProperty("monsters").GetString()!) };
            int query = c.GetProperty("query").GetInt32();
            if (c.GetProperty("throwsNull").GetBoolean()) Assert.Throws<NullReferenceException>(() => data.GetAlternativeNpcs(query));
            else Assert.Equal(c.GetProperty("expected").EnumerateArray().Select(v => v.GetInt32()), data.GetAlternativeNpcs(query).OrderBy(v => v));
        }
    }

    [Fact]
    public void HousingManagerUnionMatchesJavaIncludingDuplicatesAndZero()
    {
        using var fixture = SpawnFixture();
        foreach (var c in fixture.RootElement.GetProperty("houseCases").EnumerateArray())
        {
            var houses = new HouseData { lands = c.GetProperty("managers").EnumerateArray().Select(v => new HousingLand { managerNpc = v.GetInt32() }).ToList() };
            HashSet<int> ids = [777777]; houses.AddAllManagerNpcIdsToSet(ids);
            Assert.Equal(c.GetProperty("expected").EnumerateArray().Select(v => v.GetInt32()), ids.OrderBy(v => v));
        }
    }

    [Fact]
    public void ActualAnalyzerRecognizesHouseManagersAndSpawnerAlternativesButStillReportsMissingNpc()
    {
        const int manager = 987651, missing = 987652, killed = 987653;
        var previous = DataManager.GetRegisteredInstance(); var previousResult = QuestSpawnAnalyzer.LastResult;
        var sd = (StaticData)RuntimeHelpers.GetUninitializedObject(typeof(StaticData));
        void Set(string name, object value) => typeof(StaticData).GetProperty(name)!.SetValue(sd, value);
        Set("SpawnsDh", new SpawnsData()); Set("TownSpawns", new TownSpawnsData()); Set("Events", new EventData()); Set("NpcFactionsDataDh", new NpcFactionsData());
        Set("HouseDataDh", new HouseData { lands = [new HousingLand { managerNpc = manager }] });
        var quests = new QuestsData { questsData = [new QuestTemplate { id = 991001 }, new QuestTemplate { id = 991002 }, new QuestTemplate { id = 991003 }] };
        quests.AfterUnmarshal(null!); Set("Quests", quests);
        var xml = new XMLQuests(); xml.SetData([new KillSpawnedData { id = 991001, monster = [] }, new KillSpawnedData { id = 991002, monster = [] }, new KillSpawnedData { id = 991003, monster = [new Monster { NpcIdsRaw = killed.ToString(), spawnerObjectId = manager }] }]); Set("XmlQuests", xml);
        var data = (DataManager)RuntimeHelpers.GetUninitializedObject(typeof(DataManager));
        typeof(DataManager).GetField("<StaticData>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(data, sd);
        // Populate these owned registrations directly, without changing the process-wide QuestEngine index.
        var managerNpc = new QuestNpc(manager); managerNpc.GetOnTalkEvent().Add(991001);
        var missingNpc = new QuestNpc(missing); missingNpc.GetOnTalkEvent().Add(991002);
        var killedNpc = new QuestNpc(killed); killedNpc.GetOnKillEvent().Add(991003);
        DataManager.RegisterInstance(data);
        try
        {
            var result = QuestSpawnAnalyzer.Run([], [managerNpc, missingNpc, killedNpc], false);
            Assert.Empty(result.UnobtainableQuestIds);
            var absent = Assert.Single(result.MissingSpawns); Assert.Equal([missing], absent.NpcIds); Assert.Equal([991002], absent.QuestIds);
            Assert.Equal([991002], result.UnreachableQuestIds);
        }
        finally
        {
            DataManager.RestoreInstance(previous);
            typeof(QuestSpawnAnalyzer).GetProperty(nameof(QuestSpawnAnalyzer.LastResult))!.SetValue(null, previousResult);
        }
    }

    private static int[] ExtractSourceIds(string source) => SpawnArgumentRegex().Matches(source).Cast<System.Text.RegularExpressions.Match>()
        .SelectMany(match => NpcIdRegex().Matches(match.Groups["argument"].Value).Cast<System.Text.RegularExpressions.Match>()).Select(m => int.Parse(m.Value)).Distinct().OrderBy(v => v).ToArray();
    private static JsonDocument SpawnFixture()
    {
        var fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(RealStaticData.RepoRoot(), "parity-artifacts/golden/formulas/QuestSpawn.cabd580f3.json")));
        Assert.Equal("cabd580f3c2a5bd59b274f4ee57e9b467a44bea2", fixture.RootElement.GetProperty("sourceJavaCommit").GetString()); return fixture;
    }
    private static List<Monster> ParseMonsters(string rows) => rows == "empty" ? [] : rows.Split(';').Select(row =>
    {
        var parts = row.Split(':'); return new Monster { spawnerObjectId = int.Parse(parts[0]), NpcIdsRaw = parts[1] == "null" ? null! : parts[1].Replace(',', ' ') };
    }).ToList();
    private sealed class KillProbe : KillSpawnedData
    {
        internal KillProbe() { startNpcIds = [999900, 999901, 999902]; }
    }
}
