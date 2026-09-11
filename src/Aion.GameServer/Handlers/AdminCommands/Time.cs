using System;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;
using Aion.GameServer.Utils.Time.Gametime;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>Java parity: data/handlers/admincommands/Time (Pan, Neon, Sykra).</summary>
public class Time : AdminCommand
{
    public Time()
        : base("time", "Changes the game time.", """
            <dawn|day|dusk|night> - Sets the specified day time.
            <0-23> - Sets the specified hour.
            <0-23> <0-59> - Sets the specified hour and minute.
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
        int hour;
        int minute = 0;
        if (paramsArr[0].Equals("night", StringComparison.OrdinalIgnoreCase))
        {
            hour = 22;
        }
        else if (paramsArr[0].Equals("dusk", StringComparison.OrdinalIgnoreCase))
        {
            hour = 18;
        }
        else if (paramsArr[0].Equals("day", StringComparison.OrdinalIgnoreCase))
        {
            hour = 9;
        }
        else if (paramsArr[0].Equals("dawn", StringComparison.OrdinalIgnoreCase))
        {
            hour = 4;
        }
        else
        {
            hour = ParseInt(paramsArr[0]);
            if (hour < 0 || hour > 23)
            {
                SendInfo(admin, "Hour must be between 0 and 23.");
                return;
            }
            if (paramsArr.Length == 2)
            {
                minute = ParseInt(paramsArr[1]);
                if (minute < 0 || minute > 59)
                {
                    SendInfo(admin, "Minute must be between 0 and 59.");
                    return;
                }
            }
        }

        GameTime gameTime = GameTimeService.GetInstance().GetGameTime();
        int hourOffset = hour - gameTime.GetHour(); // hour offset inside the same day
        int minutesToAdd = 60 * hourOffset;
        if (minute == 0)
        {
            minutesToAdd -= gameTime.GetMinute();
        }
        else
        {
            int minuteOffset = minute - gameTime.GetMinute();
            minutesToAdd += minuteOffset;
        }
        gameTime.AddMinutes(minutesToAdd);
        PacketSendUtility.BroadcastToWorld(new SM_GAME_TIME());
        SendInfo(admin, "You changed the time to " + gameTime.GetHour() + ":" + string.Format("{0:D2}", gameTime.GetMinute()) + ".");
    }
}
