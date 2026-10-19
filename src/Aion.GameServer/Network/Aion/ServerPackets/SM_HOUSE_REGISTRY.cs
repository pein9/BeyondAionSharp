using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.House;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Model.Templates.Housing;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_HOUSE_REGISTRY (Rolandas). Lists not-spawned house objects (action 1) or default+unused decorations (action 2). HouseObject&lt;?&gt; erased to HouseObject&lt;PlaceableHouseObject&gt;; instanceof UseableItemObject->is+cast; getBuf()->GetBuf(). HouseRegistry/HouseObject/HouseDecoration red-tolerated.</summary>
public class SM_HOUSE_REGISTRY : AionServerPacket
{
    private readonly HousingContainer container;

    public SM_HOUSE_REGISTRY(HousingContainer container)
    {
        this.container = container;
    }

    protected override void WriteImpl(AionConnection con)
    {
        Player player = con.GetActivePlayer();
        if (player == null)
            return;
        HouseRegistry houseRegistry = player.GetActiveHouse().GetRegistry();

        WriteC(container.GetId());
        if (container == HousingContainer.OBJECTS)
        { // Display registered objects
            if (houseRegistry == null)
            {
                WriteH(0);
                return;
            }
            WriteH(houseRegistry.GetNotSpawnedObjects().Count);
            foreach (HouseObject obj in houseRegistry.GetNotSpawnedObjects())
            {
                if (obj == null)
                    continue;
                WriteD(obj.GetObjectId());
                int templateId = obj.GetObjectTemplate().GetTemplateId();
                WriteD(templateId);
                WriteD(player.GetHouseObjectCooldowns().RemainingSeconds(obj.GetObjectId()));
                WriteD(obj.SecondsUntilExpiration());

                WriteDyeInfo(obj.GetColor());
                WriteD(obj.GetColorExpireEnd());

                WriteC(obj.GetObjectTemplate().GetTypeId());
                if (obj is UseableItemObject)
                {
                    ((UseableItemObject)obj).WriteUsageData(GetBuf());
                }
            }
        }
        else if (container == HousingContainer.DECORATIONS)
        { // Display default and registered decoration items
            List<int> defaultDecorIds = houseRegistry.GetOwner().GetBuilding().GetDefaultPartIds();
            List<HouseDecoration> unusedDecors = houseRegistry.GetUnusedDecors();
            WriteH(defaultDecorIds.Count + unusedDecors.Count);
            foreach (int defaultPartId in defaultDecorIds)
            {
                WriteD(0);
                WriteD(defaultPartId);
            }
            foreach (HouseDecoration houseDecor in unusedDecors)
            {
                WriteD(houseDecor.GetObjectId());
                WriteD(houseDecor.GetTemplateId());
            }
        }
    }
}
