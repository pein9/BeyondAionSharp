using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.Model.Stats.Calc.Functions;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>Java parity: data/handlers/admincommands/Speed (ATracer, Neon). Sets your speed.</summary>
public class Speed : AdminCommand, IStatOwner
{
    public Speed()
        : base("speed", "Sets your speed.", """
            <0-100> - Set your speed to the specified value (0 to reset).
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
        float parameter = JavaNumberParser.ParseFloat(paramsArr[0]);
        if (parameter < 0 || parameter > 100)
        {
            SendInfo(admin, "Speed must be between 0 and 100.");
            return;
        }
        admin.GetGameStats().EndEffect(this);
        if (parameter == 0)
        {
            SendInfo(admin, "Your regular speed has been restored.");
            return;
        }
        int speed = (int)(parameter * 1000);
        List<IStatFunction> functions = new List<IStatFunction> { new Stat.CommandStatFunction(StatEnum.SPEED, speed), new Stat.CommandStatFunction(StatEnum.FLY_SPEED, speed) };
        admin.GetGameStats().AddEffect(this, functions);
        SendInfo(admin, "Your speed is now fixed at " + JavaString.ValueOf(parameter) + ".");
    }
}
