using System.Collections.Generic;
using Aion.GameServer.Dao;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Account;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Players;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.Audit;
using State = global::Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_QUIT (-Nemesiss-, Neon). Leaves world to character-select / plastic-surgery screen (stayConnected) or closes the connection. PlayerLeaveWorldService/PlayerPunishmentsDAO/SM_QUIT_RESPONSE red-tolerated.</summary>
public class CM_QUIT : AionClientPacket
{
    /// <summary>if true, player wants to go to the character selection or plastic surgery screen.</summary>
    private bool stayConnected;

    public CM_QUIT(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        stayConnected = ReadC() == 1;
    }

    protected override void RunImpl()
    {
        AionConnection con = GetConnection();
        Player player = con.GetActivePlayer();
        bool charEditScreen = false;

        if (player != null)
        {
            charEditScreen = player.GetCommonData().IsInEditMode();
            if (charEditScreen)
            {
                VisibleObject target = player.GetTarget();
                if (!(target is Npc npc) || (!npc.GetObjectTemplate().SupportsAction(DialogAction.EDIT_CHARACTER_ALL) && !npc.GetObjectTemplate().SupportsAction(DialogAction.EDIT_CHARACTER_GENDER)) || !PositionUtil.IsInTalkRange(player, npc))
                {
                    AuditLogger.Log(player, "tried to enter the plastic surgery screen without targeting the respective npc within talk distance");
                    return;
                }
            }
            else
            {
                long countdownMillis = SystemClock.CurrentMillis() - player.GetQuitCountdownStartMillis();
                if (countdownMillis < 8_000 || countdownMillis > 20_000)
                {
                    AuditLogger.Log(player, countdownMillis < 8_000
                        ? "tried to quit the game without waiting the full 10 seconds after sending CM_MAY_QUIT"
                        : "tried to quit the game without sending CM_MAY_QUIT");
                    SendPacket(SM_QUIT_RESPONSE.Refused(unchecked((int)Math.Min(countdownMillis, int.MaxValue))));
                    return;
                }
            }
            long blockedMillis = player.GetMillisUntilQuittingAllowed();
            if (blockedMillis > 0)
            {
                SendPacket(SM_QUIT_RESPONSE.Refused(unchecked((int)((blockedMillis + 999) / 1000))));
                return;
            }
            if (stayConnected)
            { // update char selection info
                player.GetAccountData().SetVisibleItems(player.GetEquipment().GetEquippedForAppearance());
                foreach (PlayerAccountData plAccData in con.GetAccount().GetPlayerAccDataList())
                    plAccData.SetCharBanInfo(PlayerPunishmentsDAO.GetCharBanInfo(plAccData.GetPlayerCommonData().GetPlayerObjId()));
            }
            PlayerLeaveWorldService.LeaveWorldNow(player);
        }

        SM_QUIT_RESPONSE response = charEditScreen ? SM_QUIT_RESPONSE.QuitToEditScreen() : SM_QUIT_RESPONSE.Quit();
        if (stayConnected)
            SendPacket(response);
        else
            con.Close(response);
    }
}
