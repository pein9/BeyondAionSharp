using System.Xml.Serialization;

namespace Aion.GameServer.SkillEngine.Model;

/// <summary>Per-level signet data (effect prob + damage multiplier). Java parity: skillengine/model/SignetData (@XmlType("signet_data")).</summary>
[XmlType("signet_data")]
public class SignetData
{
    [XmlAttribute("lvl")] public int Level { get; set; }
    [XmlAttribute("carve_prob")] public int CarveProb { get; set; } = 100;
    [XmlAttribute("add_effect_prob")] public int AddEffectProb { get; set; } = 1;
    [XmlAttribute("dmg_multi")] public float DamageMultiplier { get; set; }

    public int GetLevel() => Level;
    public int GetCarveProb() => CarveProb;
    public int GetAddEffectProb() => AddEffectProb;
    public float GetDamageMultiplier() => DamageMultiplier;
}
