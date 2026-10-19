using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Team.Alliance;
using Aion.GameServer.Model.Team.Common.Legacy;
using Aion.GameServer.Model.Team.Group;
using Aion.GameServer.Model.Team.League;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Utils.Audit;
using State = global::Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_DISTRIBUTION_SETTINGS (Lyahim, Simple, xTz). Sets group/alliance/league loot distribution rules + per-grade thresholds. PlayerGroupService/LeagueService/PlayerAllianceService red-tolerated.</summary>
public class CM_DISTRIBUTION_SETTINGS : AionClientPacket
{
    private bool autoDistributeJunk;
    private LootRuleType? lootRules;
    private int commonItemAbove;
    private int superiorItemAbove;
    private int heroicItemAbove;
    private int fabledItemAbove;
    private int ethernalItemAbove;
    private int mythicItemAbove;

    public CM_DISTRIBUTION_SETTINGS(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        ReadD(); // isLeague; the team is taken from the leader
        lootRules = LootRuleTypeExtensions.GetById(ReadD());
        autoDistributeJunk = ReadD() == 1;
        commonItemAbove = ReadD();
        superiorItemAbove = ReadD();
        heroicItemAbove = ReadD();
        fabledItemAbove = ReadD();
        ethernalItemAbove = ReadD();
        mythicItemAbove = ReadD();
        ReadD(); // seventh grade, absent from 4.8
    }

    protected override void RunImpl()
    {
        Player leader = GetConnection().GetActivePlayer();

        if (lootRules == null)
            return;
        PlayerGroup group = leader.GetPlayerGroup();
        PlayerAlliance alliance = leader.GetPlayerAlliance();
        bool isLeader = group != null && group.IsLeader(leader)
            || alliance != null && (alliance.IsInLeague() ? leader.Equals(alliance.GetLeague().GetCaptain()) : alliance.IsLeader(leader));
        if (!isLeader)
        {
            AuditLogger.Log(leader, "tried to change the loot rules without being the team leader");
            return;
        }
        if (group != null)
        {
            PlayerGroupService.ChangeGroupRules(group, new LootGroupRules(lootRules.Value, autoDistributeJunk, commonItemAbove, superiorItemAbove, heroicItemAbove,
                fabledItemAbove, ethernalItemAbove, mythicItemAbove));
        }
        if (alliance != null)
        {
            if (alliance.IsInLeague())
                LeagueService.ChangeGroupRules(alliance.GetLeague(), new LootGroupRules(lootRules.Value, autoDistributeJunk, commonItemAbove, superiorItemAbove, heroicItemAbove,
                    fabledItemAbove, ethernalItemAbove, mythicItemAbove));
            else
                PlayerAllianceService.ChangeGroupRules(alliance, new LootGroupRules(lootRules.Value, autoDistributeJunk, commonItemAbove, superiorItemAbove, heroicItemAbove,
                    fabledItemAbove, ethernalItemAbove, mythicItemAbove));
        }
    }
}
