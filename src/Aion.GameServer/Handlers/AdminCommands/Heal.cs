using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;
using TYPE = Aion.GameServer.Network.Aion.ServerPackets.SmAttackStatus.TYPE;
using LOG = Aion.GameServer.Network.Aion.ServerPackets.SmAttackStatus.LOG;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>Java parity: data/handlers/admincommands/Heal (Mrakobes, Loxo).</summary>
public class Heal : AdminCommand
{
    public Heal()
        : base("heal", "Restores HP, MP, DP, flight time and energy of repose.", """
             - Heals your target's HP, MP and removes soul sickness.
            dp - Heals your target's DP.
            fp - Heals your target's flight time.
            repose - Heals your target's energy of repose.
            <number> - Heals your target's HP by given amount.
            <number%> - Heals your target's HP by given percentage.
            """)
    {
    }

    public override void Execute(Player player, params string[] paramsArr)
    {
        VisibleObject target = player.GetTarget();
        if (target == null)
        {
            SendInfo(player);
            return;
        }
        if (!(target is Creature creature))
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_INVALID_TARGET());
            return;
        }
        if (paramsArr.Length == 0)
        {
            creature.GetLifeStats().IncreaseHp(TYPE.HP, creature.GetLifeStats().GetMaxHp());
            creature.GetLifeStats().IncreaseMp(TYPE.HEAL_MP, creature.GetLifeStats().GetMaxMp(), 0, LOG.MPHEAL);
            creature.GetEffectController().RemoveByDispelSlotType(DispelSlotType.SPECIAL2);
            if (!player.Equals(creature))
                SendInfo(player, Name(creature) + " has been refreshed.");
        }
        else if (paramsArr[0].Equals("dp", System.StringComparison.OrdinalIgnoreCase) && creature is Player)
        {
            Player targetPlayer = (Player)creature;
            targetPlayer.GetCommonData().SetDp(targetPlayer.GetGameStats().GetMaxDp().GetCurrent());
            if (!player.Equals(creature))
                SendInfo(player, Name(targetPlayer) + "'s DP have been fully refreshed.");
        }
        else if (paramsArr[0].Equals("fp", System.StringComparison.OrdinalIgnoreCase) && creature is Player)
        {
            Player targetPlayer = (Player)creature;
            targetPlayer.GetLifeStats().SetCurrentFp(targetPlayer.GetLifeStats().GetMaxFp());
            if (!player.Equals(creature))
                SendInfo(player, Name(targetPlayer) + "'s flight time has been fully refreshed.");
        }
        else if (paramsArr[0].Equals("repose", System.StringComparison.OrdinalIgnoreCase) && creature is Player)
        {
            Player targetPlayer = (Player)creature;
            PlayerCommonData pcd = targetPlayer.GetCommonData();
            pcd.SetCurrentReposeEnergy(pcd.GetMaxReposeEnergy());
            PacketSendUtility.SendPacket(targetPlayer,
                new SM_STATUPDATE_EXP(pcd.GetExpShown(), pcd.GetExpRecoverable(), pcd.GetExpNeed(), pcd.GetCurrentReposeEnergy(), pcd.GetMaxReposeEnergy()));
            if (!player.Equals(creature))
                SendInfo(player, Name(targetPlayer) + "'s Energy of Repose has been fully refreshed.");
        }
        else
        {
            int value;
            if (paramsArr[0].EndsWith('%'))
            {
                int hpPercent = ParseInt(paramsArr[0], 0, paramsArr[0].Length - 1, 10);
                value = Math.Clamp((int)(hpPercent / 100f * creature.GetLifeStats().GetMaxHp()), 0, creature.GetLifeStats().GetMaxHp());
            }
            else
                value = ParseInt(paramsArr[0]);
            creature.GetLifeStats().IncreaseHp(TYPE.HP, value);
            if (!player.Equals(creature))
                SendInfo(player, Name(creature) + " has been healed by " + value + " health points.");
        }
    }
}
