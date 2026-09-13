using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Skill;
using Aion.GameServer.Model.Team.Legion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>Java parity: data/handlers/admincommands/PlayerInfo (lyahim, antness). Shows information about a player.</summary>
public class PlayerInfo : AdminCommand
{
    public PlayerInfo()
        : base("playerinfo", "Shows information about a player.", """
            <player name> - Shows basic information about the given player.
            <player name> <item|party|skills|legion|ap|chars|knownlist> - Shows extended information about the given player.
            """)
    {
    }

    public override void Execute(Player admin, params string[] paramsArr)
    {
        if (paramsArr.Length == 0)
        {
            SendInfo(admin);
            return;
        }

        string playerName = Util.ConvertName(paramsArr[0]);
        Player target = World.World.GetInstance().GetPlayer(playerName);
        if (target == null)
        {
            PacketSendUtility.SendPacket(admin, SM_SYSTEM_MESSAGE.STR_NO_SUCH_USER(playerName));
            return;
        }

        SendInfo(admin,
            "[Info about " + Name(target) + "]\n- Common: lv" + target.GetLevel() + " (" + target.GetCommonData().GetExpShown() + " xp), "
                + target.GetRace() + ", " + target.GetPlayerClass() + "\n- IP: " + target.GetClientConnection().GetIP() + "\n" + "- Account name: "
                + target.GetAccount().GetName() + "\n- " + ChatUtil.Position("Location", target.GetPosition()) + ": " + target.GetPosition().ToCoordString());

        if (paramsArr.Length < 2)
            return;

        if (paramsArr[1].Equals("item", StringComparison.OrdinalIgnoreCase))
        {
            StringBuilder strbld = new StringBuilder("- Items in inventory:");
            AppendItems(strbld, target.GetInventory().GetItemsWithKinah());
            strbld.Append("\n- Equipped items:");
            AppendItems(strbld, target.GetEquipment().GetEquippedItems());
            strbld.Append("\n- Items in warehouse:");
            AppendItems(strbld, target.GetWarehouse().GetItemsWithKinah());
            SendInfo(admin, strbld.ToString());
        }
        else if (paramsArr[1].Equals("party", StringComparison.OrdinalIgnoreCase))
        {
            StringBuilder sb = new StringBuilder("- Party: ");
            var team = target.GetCurrentTeam();
            if (team == null)
            {
                sb.Append("none");
            }
            else
            {
                sb.Append(team.GetType().Name.Replace("Player", ""));
                sb.Append("\n\tLeader: ").Append(Name(team.GetLeaderObject())).Append("\n\tMembers:\n");
                team.ForEach(player => sb.Append("\t").Append(Name(player)).Append("\n"));
            }
            SendInfo(admin, sb.ToString());
        }
        else if (paramsArr[1].Equals("skills", StringComparison.OrdinalIgnoreCase))
        {
            StringBuilder sb = new StringBuilder("- Skills:");
            foreach (PlayerSkillEntry skill in target.GetSkillList().GetAllSkills())
                sb.Append("\n\tlevel " + skill.GetSkillLevel() + " of " + skill.GetSkillTemplate().GetL10n());
            SendInfo(admin, sb.ToString());
        }
        else if (paramsArr[1].Equals("legion", StringComparison.OrdinalIgnoreCase))
        {
            Legion legion = target.GetLegion();
            if (legion == null)
                SendInfo(admin, "- Legion: none");
            else
            {
                StringBuilder sb = new StringBuilder("- Legion: \"" + legion.GetName() + "\", level: " + legion.GetLegionLevel());
                sb.Append("\n\t").Append(legion.GetMembers().Count).Append(" members:");
                foreach (LegionMember lm in legion.GetMembers())
                    sb.Append("\n\t").Append(lm.GetName()).Append(" - ").Append(lm.GetRank()).Append(lm.IsOnline() ? " (online)" : "");
                SendInfo(admin, sb.ToString());
            }
        }
        else if (paramsArr[1].Equals("ap", StringComparison.OrdinalIgnoreCase))
        {
            SendInfo(admin, "- AP info:");
            SendInfo(admin, "\tTotal AP = " + target.GetAbyssRank().GetAp());
            SendInfo(admin, "\tTotal Kills = " + target.GetAbyssRank().GetAllKill());
            SendInfo(admin, "\tToday Kills = " + target.GetAbyssRank().GetDailyKill());
            SendInfo(admin, "\tToday AP = " + target.GetAbyssRank().GetDailyAP());
        }
        else if (paramsArr[1].Equals("chars", StringComparison.OrdinalIgnoreCase))
        {
            SendInfo(admin, "- Characters (" + target.GetAccount().Size() + "):");
            foreach (var d in target.GetAccount())
                SendInfo(admin, "\t" + d.GetPlayerCommonData().GetName());
        }
        else if (paramsArr[1].Equals("knownlist", StringComparison.OrdinalIgnoreCase))
        {
            SendInfo(admin, "- KnownList:" + string.Concat(target.GetKnownList().Stream().Select(o => "\n\t" + o)));
        }
        else
        {
            SendInfo(admin);
        }
    }

    private void AppendItems(StringBuilder strbld, List<Item> items)
    {
        if (items.Count == 0)
            strbld.Append("\nnone");
        else
            foreach (Item item in items)
                strbld.Append("\n").Append(ChatUtil.LeftPad(item.GetItemCount(), 4)).Append("x ")
                    .Append(ChatUtil.Item(item.GetItemId()));
    }
}
