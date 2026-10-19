using System;
using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.SkillEngine.Condition;

/// <summary>
/// Java parity: skillengine/condition/ChainCondition (ATracer, kecimis, Neon).
/// </summary>
public class ChainCondition : Condition
{
    [XmlAttribute("selfcount")]
    public int selfCount = 1;

    [XmlAttribute("precount")]
    public int preCount = 1;

    [XmlAttribute("category")]
    public string? category;

    [XmlAttribute("precategory")]
    public string? preCategory;

    [XmlAttribute("time")]
    public int time;

    /// <summary>Validation observes the chain. Only a cast that actually starts consumes it.</summary>
    public override bool Validate(Skill env)
    {
        if (env.GetEffector() is Player player)
        {
            ChainSkills chain = player.GetChainSkills();
            if (IsRepeatable() && chain.CanRepeat(category, selfCount, GetActivationWindow()))
                return true;
            if (preCategory != null)
            {
                if (!chain.CanContinue(preCategory, preCount, GetActivationWindow()))
                {
                    PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SM_SYSTEM_MESSAGE.STR_SKILL_CANT_CAST_IN_NONE_CHAINSKILL_STATUS());
                    return false;
                }
                env.SetChainResetOnCastStart(true);
            }
            else if (IsRepeatable() && chain.IsCurrentCategory(category))
            {
                env.SetChainResetOnCastStart(true);
            }
        }
        return true;
    }

    private bool IsRepeatable() => selfCount > 1;

    /// <summary>Number of allowed skill activations of this chain skill.</summary>
    public int GetAllowedActivations()
    {
        return selfCount;
    }

    public string? GetCategory()
    {
        return category;
    }

    /// <summary>Own window after the predecessor's hit: missing/zero time means 3000 ms, otherwise time + 1500 ms.</summary>
    public int GetActivationWindow() => time == 0 ? 3000 : unchecked(time + 1500);
}
