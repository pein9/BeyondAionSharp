using System;
using System.Collections.Generic;
using System.Reflection;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Zone;

namespace Aion.GameServer.World.Zone.Handler;

/// <summary>Java QuestZoneHandler: quest observers bound to an explicit validated quest ID.</summary>
public abstract class QuestZoneHandler : IZoneHandler
{
    protected readonly Dictionary<int, AbstractQuestZoneObserver> observed = new();
    protected readonly int questId;

    public QuestZoneHandler(int questId)
    {
        if (DataManager.QUEST_DATA.GetQuestById(questId) == null) throw new NullReferenceException();
        this.questId = questId;
    }

    public virtual void OnEnterZone(Creature creature, ZoneInstance zone)
    {
        if (!(creature is Player))
            return;
        AbstractQuestZoneObserver observer = CreateObserver((Player)creature, zone.GetZoneTemplate());
        creature.GetObserveController().AddObserver(observer);
        observed[creature.GetObjectId()] = observer;
    }

    public virtual void OnLeaveZone(Creature creature, ZoneInstance zone)
    {
        if (!(creature is Player))
            return;
        AbstractQuestZoneObserver observer = observed.GetValueOrDefault(creature.GetObjectId());
        if (observer != null)
        {
            creature.GetObserveController().RemoveObserver(observer);
            observed.Remove(creature.GetObjectId());
        }
    }

    public abstract AbstractQuestZoneObserver CreateObserver(Player player, ZoneTemplate zoneTemplate);
}
