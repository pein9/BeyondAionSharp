using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Npc;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>Java parity: data/handlers/admincommands/Morph (ATracer, aionchs-, Wylovech, Neon). Morphs a player into any npc.</summary>
public class Morph : AdminCommand
{
    public Morph()
        : base("morph", "Morphs a player into any NPC.", """
             - morphs you into the NPC you are targeting.
            <id> - Morphs your target into the specified NPC (0 to cancel).
            """)
    {
    }

    public override void Execute(Player admin, params string[] paramsArr)
    {
        Player target = admin.GetTarget() is Player p ? p : admin;
        NpcTemplate npcTemplate;
        if (paramsArr.Length == 0)
        {
            if (admin.GetTarget() == null || admin.Equals(admin.GetTarget()))
            {
                SendInfo(admin);
                return;
            }
            if (admin.GetTarget().GetObjectTemplate() is not NpcTemplate t)
            {
                PacketSendUtility.SendPacket(admin, SM_SYSTEM_MESSAGE.STR_INVALID_TARGET());
                return;
            }
            npcTemplate = t;
        }
        else
        {
            int modelId = ParseInt(paramsArr[0]);
            if (modelId == 0)
            {
                target.GetTransformModel().Apply(0);
                SendInfo(admin, "Cancelled" + (target.Equals(admin) ? "" : " " + Name(target) + "'s") + " morph.");
                return;
            }
            npcTemplate = DataManager.NPC_DATA.GetNpcTemplate(modelId);
            if (npcTemplate == null)
            {
                SendInfo(admin, "Invalid ID.");
                return;
            }
        }
        target.GetTransformModel().Apply(npcTemplate.GetTemplateId());
        SendInfo(admin, "You morphed" + (target.Equals(admin) ? "" : " " + Name(target)) + " into " + npcTemplate.GetL10n() + ".");
        if (!target.Equals(admin))
            SendInfo(target, Name(admin) + " morphed you into " + npcTemplate.GetL10n() + ".");
    }
}
