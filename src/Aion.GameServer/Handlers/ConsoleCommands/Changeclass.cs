using System;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.ConsoleCommands;

/// <summary>Java parity: data/handlers/consolecommands/Changeclass (ginho1, Neon). Changes the target player's class.</summary>
public class Changeclass : ConsoleCommand
{
    public Changeclass()
        : base("changeclass", "Changes a player's class.", """
            <class> - Changes your target's class to the one specified (defaults to your character, if no player is targeted).
            """)
    {
    }

    public override void Execute(Player admin, params string[] paramsArr)
    {
        if (paramsArr.Length < 1)
        {
            SendInfo(admin);
            return;
        }
        Player player = admin.GetTarget() is Player target ? target : admin;
        PlayerClass playerClass = ParsePlayerClass(paramsArr[0]);
        ClassChangeService.SetClass(player, playerClass, false, true);
        SendInfo(admin, "You have changed " + player.GetName() + "'s class to " + playerClass.ToString().ToLower() + ".");
    }

    /// <remarks>Java parity: <c>protected static</c> in Java, called by the same-package Classup command. C# <c>protected</c> would hide it from Classup, so it is <c>internal</c>.</remarks>
    internal static PlayerClass ParsePlayerClass(string param)
    {
        return param.ToUpperInvariant() switch
        {
            "FIGHTER" => PlayerClass.GLADIATOR,
            "KNIGHT" => PlayerClass.TEMPLAR,
            "WIZARD" => PlayerClass.SORCERER,
            "ELEMENTALIST" => PlayerClass.SPIRIT_MASTER,
            var newClass => ParseEnumName<PlayerClass>(newClass),
        };
    }
}
