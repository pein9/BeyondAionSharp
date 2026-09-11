using System;
using System.Collections.Generic;
using System.Linq;
using Aion.GameServer.Handlers.ConsoleCommands;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Items;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>Java parity: data/handlers/admincommands/RemoveCd (kecimis). Clears cooldowns for skills, items and instances.</summary>
public class RemoveCd : AdminCommand
{
    public RemoveCd()
        : base("removecd", "Clears cooldowns for skills, items and instances.", """
             - Removes all item and skill cooldowns of your target.
            instance all - Removes all instance cooldowns of your target.
            instance <world ID> - Removes the specified instance cooldown of your target.
            Note: Any actions default to your character, if no player is targeted.
            """)
    {
    }

    public override void Execute(Player admin, params string[] paramsArr)
    {
        Player target = admin.GetTarget() is Player p ? p : admin;
        if (paramsArr.Length == 0)
        {
            if (target.GetSkillCoolDowns() != null)
            {
                long nowMillis = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                List<int> cooldownIds = target.GetSkillCoolDowns().Where(e => e.Value > nowMillis).Select(e => e.Key).ToList();
                PacketSendUtility.SendPacket(target, new SM_SKILL_COOLDOWN(target, cooldownIds));
                target.GetSkillCoolDowns().Clear();
            }
            RemoveItemCooldowns(target);
            target.GetHouseObjectCooldowns().Clear();
            if (target.Equals(admin))
            {
                SendInfo(admin, "Your item and skill cooldowns were removed.");
            }
            else
            {
                SendInfo(admin, "You have removed item and skill cooldowns of " + Name(target) + '.');
                SendInfo(target, Name(admin) + " removed your item and skill cooldowns.");
            }
        }
        else if (paramsArr[0].Equals("instance", StringComparison.OrdinalIgnoreCase) && paramsArr.Length >= 2)
        {
            if (paramsArr[1].Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                Clearusercoolt.ClearAllInstanceCooldowns(admin, target);
            }
            else
            {
                int worldId = ParseInt(paramsArr[1]);
                if (target.GetPortalCooldownList().IsPortalUseDisabled(worldId))
                {
                    target.GetPortalCooldownList().RemovePortalCooldown(worldId);
                    target.GetPortalCooldownList().SendEntryInfo(worldId);
                    string worldName = World.World.GetInstance().GetWorldMap(worldId).GetName().Replace('_', ' ');
                    if (target.Equals(admin))
                    {
                        SendInfo(admin, "Your instance cooldown for " + worldName + " was removed.");
                    }
                    else
                    {
                        SendInfo(admin, "You have removed the instance cooldown for " + worldName + " of " + Name(target) + '.');
                        SendInfo(target, Name(admin) + " removed your instance cooldown for " + worldName);
                    }
                }
                else
                    SendInfo(admin, (target.Equals(admin) ? "You have" : Name(target) + " has") + " no cooldown on given instance.");
            }
        }
        else
        {
            SendInfo(admin);
        }
    }

    public static void RemoveItemCooldowns(Player player)
    {
        Dictionary<int, ItemCooldown> dummyCds = new Dictionary<int, ItemCooldown>(); // 4.8 client ignores reuseTime <= currentTime, but sending old cds + useDelay 0 works
        foreach (KeyValuePair<int, ItemCooldown> en in player.GetItemCoolDowns())
        {
            dummyCds[en.Key] = new ItemCooldown(en.Value.GetReuseTime(), 0);
            player.RemoveItemCoolDown(en.Key);
        }
        PacketSendUtility.SendPacket(player, new SM_ITEM_COOLDOWN(dummyCds));
    }
}
