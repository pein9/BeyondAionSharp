using System;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Event;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Model.Animations;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>Java parity: data/handlers/admincommands/Ai (ATracer, Neon).</summary>
public class Ai : AdminCommand
{
    public Ai()
        : base("ai", "Modifies and shows AI details.", """
            info - Show AI info for your target.
            set <aiName> - Changes the AI of your target.
            state <stateName> [substateName] - Changes the AI state.
            event <eventName> - Fires the AI event for the given name.
            event2 <eventName> <creatureObjId> - Fires the creature AI event for the given name and creature.
            events - Shows last AI events for your target.
            log - Toggles AI logging for your target on and off.
            <createlog|eventlog|movelog> - Toggles logging on and off.
            marker [text] - Prints a marker with the optional text in log.
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

        if (paramsArr[0].Equals("createlog", StringComparison.OrdinalIgnoreCase))
        {
            AIConfig.ONCREATE_DEBUG = !AIConfig.ONCREATE_DEBUG;
            SendInfo(admin, "New createlog value: " + JavaString.ValueOf(AIConfig.ONCREATE_DEBUG));
        }
        else if (paramsArr[0].Equals("eventlog", StringComparison.OrdinalIgnoreCase))
        {
            AIConfig.EVENT_DEBUG = !AIConfig.EVENT_DEBUG;
            SendInfo(admin, "New eventlog value: " + JavaString.ValueOf(AIConfig.EVENT_DEBUG));
        }
        else if (paramsArr[0].Equals("movelog", StringComparison.OrdinalIgnoreCase))
        {
            AIConfig.MOVE_DEBUG = !AIConfig.MOVE_DEBUG;
            SendInfo(admin, "New movelog value: " + JavaString.ValueOf(AIConfig.MOVE_DEBUG));
        }
        else if (paramsArr[0].Equals("marker", StringComparison.OrdinalIgnoreCase))
        {
            if (paramsArr.Length > 1)
                NullLoggerFactory.Instance.CreateLogger(typeof(AILogger).FullName).LogInformation("[AI] marker: " + Join(paramsArr, 1));
            else
                NullLoggerFactory.Instance.CreateLogger(typeof(AILogger).FullName).LogInformation("[AI] marker");
        }
        else
        {
            if (admin.GetTarget() is not Creature npc || npc is Player)
            {
                PacketSendUtility.SendPacket(admin, SM_SYSTEM_MESSAGE.STR_INVALID_TARGET());
                return;
            }
            if (paramsArr[0].Equals("info", StringComparison.OrdinalIgnoreCase))
            {
                SendInfo(admin,
                    "[AI info]\n\tName: " + npc.GetAi().GetName() + "\n\tState: " + npc.GetAi().GetState() + "\n\tSubstate: " + npc.GetAi().GetSubState());
            }
            else if (paramsArr[0].Equals("log", StringComparison.OrdinalIgnoreCase))
            {
                bool oldValue = npc.GetAi().IsLogging();
                npc.GetAi().SetLogging(!oldValue);
                SendInfo(admin, "New log value: " + JavaString.ValueOf(!oldValue));
            }
            else if (paramsArr[0].Equals("events", StringComparison.OrdinalIgnoreCase))
            {
                AIEventLog eventLog = npc.GetAi().GetEventLog();
                if (eventLog == null || eventLog.Count == 0)
                {
                    SendInfo(admin, "No events logged" + (AIConfig.EVENT_DEBUG ? "" : " (enable event logging via eventlog parameter)"));
                }
                else
                {
                    foreach (AiEventType eventType in eventLog)
                    {
                        SendInfo(admin, "EVENT: " + eventType.ToString());
                    }
                }
            }
            else if (paramsArr.Length > 1)
            {
                string param1 = paramsArr[1];
                if (paramsArr[0].Equals("set", StringComparison.OrdinalIgnoreCase))
                {
                    string aiName = param1;
                    AbstractAI newAi = AIEngine.GetInstance().NewAI(aiName, npc);
                    try
                    {
                        // Java parity: getDeclaredField throws NoSuchFieldException; C# GetField returns null instead.
                        FieldInfo aiField = npc.GetType().BaseType.GetField("ai", BindingFlags.NonPublic | BindingFlags.Instance)
                            ?? throw new MissingFieldException(npc.GetType().BaseType.FullName, "ai");
                        World.World.GetInstance().Despawn(npc, ObjectDeleteAnimation.NONE);
                        aiField.SetValue(npc, newAi);
                        World.World.GetInstance().Spawn(npc); // properly init AI states
                        SendInfo(admin, "Npc now has AI " + newAi.GetType().Name);
                    }
                    catch (Exception e) when (e is FieldAccessException || e is MemberAccessException)
                    {
                        NullLoggerFactory.Instance.CreateLogger(typeof(Ai).FullName).LogError(e, "");
                        SendInfo(admin, "Error changing AI (see logs)");
                    }
                }
                else if (paramsArr[0].Equals("event", StringComparison.OrdinalIgnoreCase))
                {
                    AiEventType eventType = ParseEnumName<AiEventType>(param1.ToUpperInvariant());
                    npc.GetAi().OnGeneralEvent(eventType);
                }
                else if (paramsArr[0].Equals("event2", StringComparison.OrdinalIgnoreCase))
                {
                    Creature creature = paramsArr.Length < 3 ? null : (Creature) World.World.GetInstance().FindVisibleObject(ParseInt(paramsArr[2]));
                    if (creature == null)
                        SendInfo(admin, "Please provide a valid creature object ID");
                    else
                    {
                        AiEventType eventType = ParseEnumName<AiEventType>(param1.ToUpperInvariant());
                        npc.GetAi().OnCreatureEvent(eventType, creature);
                    }
                }
                else if (paramsArr[0].Equals("state", StringComparison.OrdinalIgnoreCase))
                {
                    AIState state = ParseEnumName<AIState>(param1.ToUpperInvariant());
                    npc.GetAi().SetStateIfNot(state);
                    if (paramsArr.Length > 2)
                    {
                        AISubState substate = ParseEnumName<AISubState>(paramsArr[2]);
                        npc.GetAi().SetSubStateIfNot(substate);
                    }
                }
            }
            else
            {
                SendInfo(admin);
            }
        }
    }
}
