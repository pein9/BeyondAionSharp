using System;
using System.Collections.Generic;
using System.Linq;
using Aion.GameServer.Configs.Administration;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.GameObjects.Players.Npcfaction;
using Aion.GameServer.Model.Templates;
using Aion.GameServer.Model.Templates.Quest;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>Java parity: data/handlers/admincommands/Quest (MrPoke, Neon, Pad).</summary>
public class Quest : AdminCommand
{
    public Quest()
        : base("quest", "Handles quest states of your target.", """
            [player] <quest> <reset|start|delete> - Resets/starts/deletes the specified quest.
            [player] <quest> status - Shows the quest status of the specified quest.
            [player] <quest> set <status> <var> [varNum] - Sets the specified quest state (default: apply var to all varNums, optional: set var to varNum [0-5]).
            [player] <quest> setflags <flags> - Sets the specified quest flags.
            [player] <quest> dialog <page ID> - Sends the dialog page with the given page ID.
            Note: If no player parameter is given, your current target will be taken (defaults to your character, if no player is targeted).
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

        byte index = 0;
        Player target;
        int questId = ChatUtil.GetQuestId(paramsArr[index]);
        if (questId == 0)
        {
            string playerName = Util.ConvertName(paramsArr[index]);
            target = World.World.GetInstance().GetPlayer(playerName);
            if (target == null)
            {
                PacketSendUtility.SendPacket(admin, SM_SYSTEM_MESSAGE.STR_NO_SUCH_USER(playerName));
                return;
            }
            if (++index >= paramsArr.Length)
            {
                SendInfo(admin);
                return;
            }
            questId = ChatUtil.GetQuestId(paramsArr[index]);
        }
        else
        {
            target = admin.GetTarget() is Player p ? p : admin;
        }

        if (questId == 0 || DataManager.QUEST_DATA.GetQuestById(questId) == null)
        {
            SendInfo(admin, "Invalid quest.");
            return;
        }

        if (++index >= paramsArr.Length)
        {
            SendInfo(admin);
            return;
        }

        // quest and target are both valid at this point
        if (paramsArr[index].Equals("reset", StringComparison.OrdinalIgnoreCase))
        {
            ResetQuest(admin, target, questId);
        }
        else if (paramsArr[index].Equals("start", StringComparison.OrdinalIgnoreCase))
        {
            StartQuest(admin, target, questId);
        }
        else if (paramsArr[index].Equals("delete", StringComparison.OrdinalIgnoreCase))
        {
            DeleteQuest(admin, target, questId);
        }
        else if (paramsArr[index].Equals("status", StringComparison.OrdinalIgnoreCase))
        {
            ShowQuestStatus(admin, target, questId);
        }
        else if (paramsArr[index].Equals("set", StringComparison.OrdinalIgnoreCase) && paramsArr.Length > index + 2)
        {
            QuestStatus status = ParseEnumName<QuestStatus>(paramsArr[++index].ToUpperInvariant());
            int var = ParseInt(paramsArr[++index]);
            int varNum = -1;
            if (++index < paramsArr.Length)
            { // optional
                varNum = ParseInt(paramsArr[index]);
                if (varNum < 0 || varNum > 5)
                {
                    SendInfo(admin, "[varNum] must be between 0 and 5.");
                    return;
                }
            }
            SetQuestStatus(admin, target, questId, status, var, varNum);
        }
        else if (paramsArr[index].Equals("setflags", StringComparison.OrdinalIgnoreCase) && paramsArr.Length > index + 1)
        {
            int flags = ParseInt(paramsArr[++index]);
            SetQuestFlags(admin, target, questId, flags);
        }
        else if (paramsArr[index].Equals("dialog", StringComparison.OrdinalIgnoreCase) && paramsArr.Length > index + 1)
        {
            int dialogPageId = ParseInt(paramsArr[++index]);
            SendQuestDialog(admin, questId, dialogPageId);
        }
        else
        {
            SendInfo(admin);
        }
    }

    private void ResetQuest(Player admin, Player target, int questId)
    {
        QuestState qs = target.GetQuestStateList().GetQuestState(questId);
        if (qs == null || qs.GetStatus() != QuestStatus.START)
        {
            SendInfo(admin, "Only currently active quests can be reset.");
            return;
        }
        if (qs.GetQuestVars().GetQuestVars() == 0 && qs.GetRewardGroup() == null)
        {
            SendInfo(admin, Name(target) + "'s quest is already at the beginning.");
            return;
        }
        qs.SetStatus(QuestStatus.START);
        qs.SetQuestVar(0);
        qs.SetRewardGroup(null);
        PacketSendUtility.SendPacket(target, new SM_QUEST_ACTION(SM_QUEST_ACTION.ActionType.UPDATE, qs));
        SendInfo(admin, "Reset " + ChatUtil.Quest(questId) + " for " + Name(target) + ".");
    }

    private void StartQuest(Player admin, Player target, int questId)
    {
        QuestTemplate template = DataManager.QUEST_DATA.GetQuestById(questId);
        if (template.GetNpcFactionId() > 0)
        {
            StartNpcFactionQuest(admin, target, questId, template.GetNpcFactionId());
            return;
        }
        else if (QuestService.StartQuest(new QuestEnv(null, target, questId)))
        {
            SendInfo(admin, "Started " + ChatUtil.Quest(questId) + " for " + Name(target) + ".");
            return;
        }
        QuestState qs = target.GetQuestStateList().GetQuestState(questId);
        if (qs != null && (qs.GetStatus() == QuestStatus.START || qs.GetStatus() == QuestStatus.REWARD))
        {
            SendInfo(admin, "Quest is already started.");
        }
        else if (qs != null && qs.GetStatus() == QuestStatus.COMPLETE && !qs.CanRepeat())
        {
            SendInfo(admin, "Quest is already completed.");
        }
        else
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            List<XMLStartCondition> preconditions = template.GetXMLStartConditions();
            if (preconditions != null)
            {
                foreach (XMLStartCondition condition in preconditions)
                {
                    List<FinishedQuestCond> finisheds = condition.GetFinishedPreconditions();
                    if (finisheds != null)
                    {
                        foreach (FinishedQuestCond fcondition in finisheds)
                        {
                            QuestState qs1 = target.GetQuestStateList().GetQuestState(fcondition.GetQuestId());
                            if (qs1 == null || qs1.GetStatus() != QuestStatus.COMPLETE)
                            {
                                sb.Append("\n\t" + ChatUtil.Quest(fcondition.GetQuestId()));
                            }
                        }
                    }
                }
            }
            SendInfo(admin,
                "Quest not started. " + (sb.Length > 0 ? "These quest(s) must be completed first:" + sb.ToString() : "Some preconditions failed."));
        }
    }

    private void StartNpcFactionQuest(Player admin, Player target, int questId, int factionId)
    {
        NpcFaction faction = target.GetNpcFactions().GetActiveNpcFaction(false);
        if (faction == null || faction.GetId() != factionId)
        {
            SendInfo(admin, Name(target) + " is not registered to the organization for this quest.");
            return;
        }
        foreach (QuestTemplate template in DataManager.QUEST_DATA.GetQuestsByNpcFaction(faction.GetId(), target))
        {
            if (template.GetId() == questId)
            {
                // simulate daily reset
                faction.SetActive(false);
                faction.SetTime(-1);
                target.GetNpcFactions().AddNpcFaction(faction);
                faction.SetActive(true);
                // set daily quest Id and time to avoid random quest
                faction.SetState(ENpcFactionQuestState.NOTING);
                faction.SetQuestId(questId);
                faction.SetTime(faction.GetTime() + 100);
                // send the daily quest to player
                target.GetNpcFactions().SendDailyQuest();
                SendInfo(admin, "Started NPC faction quest " + ChatUtil.Quest(questId) + " for " + Name(target) + ".");
                return;
            }
        }
        SendInfo(admin, "Quest not implemented or player level doesn't match.");
    }

    public void DeleteQuest(Player admin, Player target, int questId)
    {
        if (!admin.HasAccess(AdminConfig.CMD_QUEST_ADV_PARAMS))
        {
            SendInfo(admin, "<You need access level " + AdminConfig.CMD_QUEST_ADV_PARAMS + " or higher to use this function>");
            return;
        }
        QuestState qs = target.GetQuestStateList().DeleteQuest(questId);
        if (qs == null)
        {
            SendInfo(admin, Name(target) + " does not have that quest.");
            return;
        }
        if (qs.GetStatus() == QuestStatus.COMPLETE)
            QuestEngine.QuestEngine.GetInstance().SendCompletedQuests(target); // rewrite completed quest list
        else
            PacketSendUtility.SendPacket(target, new SM_QUEST_ACTION(SM_QUEST_ACTION.ActionType.ABANDON, qs));
        target.GetController().UpdateNearbyQuests();
        if (!admin.Equals(target))
            SendInfo(admin, "Deleted " + ChatUtil.Quest(questId) + " for " + Name(target) + ".");
    }

    private void ShowQuestStatus(Player admin, Player target, int questId)
    {
        if (!admin.HasAccess(AdminConfig.CMD_QUEST_ADV_PARAMS))
        {
            SendInfo(admin, "<You need access level " + AdminConfig.CMD_QUEST_ADV_PARAMS + " or higher to use this function>");
            return;
        }
        QuestState qs = target.GetQuestStateList().GetQuestState(questId);
        System.Text.StringBuilder sb = new System.Text.StringBuilder("Player: " + Name(target) + ", quest: " + ChatUtil.Quest(questId) + "\n\tQuest status: ");
        if (qs == null)
        {
            sb.Append("NULL");
        }
        else
        {
            sb.Append(qs.GetStatus().ToString());
            sb.Append("\n\tQuest vars:");
            for (int i = 0; i <= 5; i++)
                sb.Append(" " + qs.GetQuestVarById(i));
            sb.Append(", encoded [" + qs.GetQuestVars().GetQuestVars() + "]");
            sb.Append("\n\tQuest flags: " + (qs.GetFlags() & 0x3F) + " " + qs.GetStepGroup()); // needs rework when flags are implemented like vars
            sb.Append(", encoded [" + qs.GetFlags() + "]");
        }
        SendInfo(admin, sb.ToString());
    }

    private void SetQuestStatus(Player admin, Player target, int questId, QuestStatus status, int var, int varNum)
    {
        if (!admin.HasAccess(AdminConfig.CMD_QUEST_ADV_PARAMS))
        {
            SendInfo(admin, "<You need access level " + AdminConfig.CMD_QUEST_ADV_PARAMS + " or higher to use this function>");
            return;
        }
        QuestState qs = target.GetQuestStateList().GetQuestState(questId);
        SM_QUEST_ACTION.ActionType actionType;
        if (qs == null)
        { // player doesn't have that quest
            actionType = SM_QUEST_ACTION.ActionType.ADD;
            qs = new QuestState(questId, status);
            target.GetQuestStateList().AddQuest(questId, qs);
        }
        else
        {
            actionType = qs.GetStatus() == QuestStatus.COMPLETE ? SM_QUEST_ACTION.ActionType.ADD : SM_QUEST_ACTION.ActionType.UPDATE;
            qs.SetStatus(status);
        }
        if (status == QuestStatus.COMPLETE)
        {
            qs.SetQuestVar(0); // completed quests vars are always 0
            if (DataManager.QUEST_DATA.GetQuestById(qs.GetQuestId()).GetRewards().Count != 0)
                qs.SetRewardGroup(0); // follow quests could require reward group > 0 to be unlocked (see quest_data.xml)
            QuestEngine.QuestEngine.GetInstance().OnQuestCompleted(target, questId);
        }
        else
        {
            if (varNum == -1)
                qs.SetQuestVar(var);
            else
                qs.SetQuestVarById(varNum, var);
        }
        if (actionType == SM_QUEST_ACTION.ActionType.ADD && status == QuestStatus.COMPLETE)
            PacketSendUtility.SendPacket(target, new SM_QUEST_COMPLETED_LIST(1, new List<QuestState> { qs }));
        else
            PacketSendUtility.SendPacket(target, new SM_QUEST_ACTION(actionType, qs));
        target.GetController().UpdateNearbyQuests();
        SendInfo(admin, "Set quest status of " + ChatUtil.Quest(questId) + " for " + Name(target) + ".");
    }

    private void SetQuestFlags(Player admin, Player target, int questId, int flags)
    { // needs rework when flags are implemented like vars
        if (!admin.HasAccess(AdminConfig.CMD_QUEST_ADV_PARAMS))
        {
            SendInfo(admin, "<You need access level " + AdminConfig.CMD_QUEST_ADV_PARAMS + " or higher to use this function>");
            return;
        }
        QuestState qs = target.GetQuestStateList().GetQuestState(questId);
        if (qs == null || qs.GetStatus() != QuestStatus.START)
        {
            SendInfo(admin, "Flags can only be set for active quests.");
            return;
        }
        qs.SetFlags(flags);
        PacketSendUtility.SendPacket(target, new SM_QUEST_ACTION(SM_QUEST_ACTION.ActionType.UPDATE, qs));
        SendInfo(admin, "Set " + Name(target) + "'s quest flags to " + flags + ".");
    }

    private void SendQuestDialog(Player admin, int questId, int dialogPageId)
    {
        if (!admin.HasAccess(AdminConfig.CMD_QUEST_ADV_PARAMS))
        {
            SendInfo(admin, "<You need access level " + AdminConfig.CMD_QUEST_ADV_PARAMS + " or higher to use this function>");
            return;
        }
        PacketSendUtility.SendPacket(admin, new SM_DIALOG_WINDOW(0, dialogPageId, questId));
        SendInfo(admin, "Sent dialog page " + dialogPageId + " of Q" + questId + ".");
    }
}
