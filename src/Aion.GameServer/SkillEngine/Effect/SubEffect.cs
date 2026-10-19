using System.Xml.Serialization;

namespace Aion.GameServer.SkillEngine.Effects;

/// <summary>A secondary skill effect triggered by a parent effect. Java parity: skillengine/effect/SubEffect (@XmlType("SubEffect")).</summary>
[XmlType("SubEffect")]
public class SubEffect
{
    [XmlAttribute("skill_id")] public int SkillId { get; set; }
    [XmlAttribute("chance")] public int Chance { get; set; } = 100;
    [XmlAttribute("chance_delta")] public int ChanceDelta { get; set; }
    [XmlAttribute("addeffect")] public bool AddEffect { get; set; }

    public int GetSkillId() => SkillId;
    public int GetChance(int skillLevel) => unchecked(Chance + ChanceDelta * skillLevel);
    public bool IsAddEffect() => AddEffect;
}
