using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.Templates;
using Aion.GameServer.Model.Templates.Factions;
using Aion.GameServer.Model.Templates.Quest;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Handlers.Models;
using Microsoft.Extensions.Logging;

namespace Aion.GameServer.QuestEngine;

/// <summary>Java parity: questEngine/QuestSpawnAnalyzer. Streams→LINQ; Map&lt;Set&lt;Integer&gt;,List&lt;Integer&gt;&gt;→Dictionary with HashSet&lt;int&gt;.CreateSetComparer() for value-semantics keys; computeIfAbsent→TryGetValue+init. Java's runtime scan of shipped handler sources is replaced by the checked-in C# build artifact <see cref="HandlerSpawnedNpcIds"/>; the structured result is a planner/testing seam. DataManager red-tolerated.</summary>
public class QuestSpawnAnalyzer
{
    private static readonly ILogger log = AionLog.For(nameof(QuestSpawnAnalyzer));

    private QuestSpawnAnalyzer()
    {
    }

    public static QuestSpawnAnalysisResult LastResult { get; private set; } = QuestSpawnAnalysisResult.Empty;

    internal static QuestSpawnAnalysisResult Run(ICollection<AbstractQuestHandler> questHandlers, ICollection<QuestNpc> questNpcs, bool ignoreEventQuests)
    {
        log.LogInformation("Analyzing quest handlers (ignoreEventQuests=" + ignoreEventQuests + ")...");
        var stopwatch = new Stopwatch();
        stopwatch.Start();
        HashSet<int> unobtainableQuests = new();
        HashSet<int> factionIds = new();
        HashSet<int> allSpawns = LoadNpcIdsSpawnedByHandlers();
        DataManager.SPAWNS_DATA.AddAllNpcIdsToSet(allSpawns);
        DataManager.TOWN_SPAWNS_DATA.AddAllNpcIdsToSet(allSpawns);
        DataManager.EVENT_DATA.AddAllNpcIdsToSet(allSpawns);
        foreach (NpcFactionTemplate nft in DataManager.NPC_FACTIONS_DATA.GetNpcFactionsData())
        {
            if (nft.GetNpcIds() == null || nft.GetNpcIds().Any(allSpawns.Contains))
                factionIds.Add(nft.GetId());
        }
        foreach (AbstractQuestHandler qh in questHandlers)
        {
            QuestTemplate qt = DataManager.QUEST_DATA.GetQuestById(qh.GetQuestId());
            if (qt.GetMinlevelPermitted() == 99 || qt.GetNpcFactionId() > 0 && !factionIds.Contains(qt.GetNpcFactionId()))
                unobtainableQuests.Add(qh.GetQuestId()); // players can still have these quests from before an update
        }
        Dictionary<HashSet<int>, List<int>> missingSpawnsByQuests = new(HashSet<int>.CreateSetComparer());
        foreach (QuestNpc npc in questNpcs)
        {
            if (allSpawns.Contains(npc.GetNpcId()))
                continue;
            HashSet<int> questIds = npc.FindAllRegisteredQuestIds(id => (!ignoreEventQuests || id < 80000) && !IsUnobtainable(id, unobtainableQuests) && !ExistsSpawnDataForAnyAlternativeNpc(id, npc.GetNpcId(), allSpawns));
            if (questIds.Count == 0)
                continue;
            if (!missingSpawnsByQuests.TryGetValue(questIds, out List<int> list))
            {
                list = new List<int>();
                missingSpawnsByQuests[questIds] = list;
            }
            list.Add(npc.GetNpcId());
        }
        long timeMillis = stopwatch.ElapsedMilliseconds;
        QuestSpawnAnalysisResult result = QuestSpawnAnalysisResult.Create(unobtainableQuests, missingSpawnsByQuests);
        LastResult = result;
        if (missingSpawnsByQuests.Count == 0)
        {
            log.LogInformation("Quest handler analysis finished in {Time} ms without errors", timeMillis);
        }
        else
        {
            string missingSpawns = string.Concat(missingSpawnsByQuests
                .Select(e => "\n\tNpc " + string.Join("/", e.Value.OrderBy(v => v).Select(v => v.ToString())) + " (quests: " + string.Join(", ", e.Key.OrderBy(v => v).Select(v => v.ToString())) + ")")
                .OrderBy(s => s, StringComparer.Ordinal));
            log.LogWarning("Quest handler analysis finished in {Time} ms. Found {Count} missing quest npc spawns:{Spawns}", timeMillis, missingSpawnsByQuests.Count, missingSpawns);
        }
        return result;
    }

    private static bool IsUnobtainable(int questId, HashSet<int> unobtainableQuests)
    {
        if (unobtainableQuests.Contains(questId))
            return true;
        QuestTemplate qt = DataManager.QUEST_DATA.GetQuestById(questId);
        foreach (XMLStartCondition startCondition in qt.GetXMLStartConditions())
        {
            if (startCondition.GetFinishedPreconditions() == null)
                continue;
            if (startCondition.GetFinishedPreconditions().All(fpc => IsUnobtainable(fpc.GetQuestId(), unobtainableQuests)))
                return true;
        }
        return false;
    }

    /// <summary>
    /// True, if alternative npc ids, which are valid for this quest, appear in spawn templates (e.g. mobs for quest kills or talk npcs)
    /// </summary>
    private static bool ExistsSpawnDataForAnyAlternativeNpc(int questId, int npcId, HashSet<int> allSpawns)
    {
        XMLQuest quest = DataManager.XML_QUESTS.GetQuest(questId);
        if (quest == null)
            return true; // no way to get alternative npcs from non-xml based handlers, so assume the quest spawns work (lol)
        ISet<int> alternativeNpcs = quest.GetAlternativeNpcs(npcId);
        if (alternativeNpcs == null)
            return false;
        return alternativeNpcs.Any(allSpawns.Contains);
    }

    public static HashSet<int> LoadNpcIdsSpawnedByHandlers()
    {
        return new HashSet<int>(HandlerSpawnedNpcIds.All);
    }
}

public sealed record MissingQuestNpcSpawns(IReadOnlyList<int> NpcIds, IReadOnlyList<int> QuestIds);

public sealed record QuestSpawnAnalysisResult(
    IReadOnlySet<int> UnobtainableQuestIds,
    IReadOnlySet<int> UnreachableQuestIds,
    IReadOnlyList<MissingQuestNpcSpawns> MissingSpawns)
{
    public static QuestSpawnAnalysisResult Empty { get; } = new(
        new HashSet<int>(),
        new HashSet<int>(),
        Array.Empty<MissingQuestNpcSpawns>());

    internal static QuestSpawnAnalysisResult Create(
        HashSet<int> unobtainableQuestIds,
        Dictionary<HashSet<int>, List<int>> missingSpawnsByQuests)
    {
        MissingQuestNpcSpawns[] missingSpawns = missingSpawnsByQuests
            .Select(entry => new MissingQuestNpcSpawns(
                entry.Value.OrderBy(id => id).ToArray(),
                entry.Key.OrderBy(id => id).ToArray()))
            .OrderBy(entry => entry.NpcIds[0])
            .ToArray();
        return new QuestSpawnAnalysisResult(
            new HashSet<int>(unobtainableQuestIds),
            missingSpawns.SelectMany(entry => entry.QuestIds).ToHashSet(),
            missingSpawns);
    }
}
