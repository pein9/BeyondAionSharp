using System.Xml.Serialization;

namespace Aion.GameServer.World.Zone;

/// <summary>
/// Bit-flag attributes for zone templates.
/// Java parity: world/zone/ZoneAttributes.
/// Java getId() is the explicit bit value; enum ordinals are not bit masks.
/// </summary>
[Flags]
[XmlType("ZoneAttributes")]
public enum ZoneAttributes
{
    // Java parity: BIND(1 << 0)
    Bind = 1 << 0,
    // Java parity: RECALL(1 << 1)
    Recall = 1 << 1,
    // Java parity: GLIDE(1 << 2)
    Glide = 1 << 2,
    // Java parity: FLY(1 << 3)
    Fly = 1 << 3,
    // Java parity: RIDE(1 << 4)
    Ride = 1 << 4,
    // Java parity: FLY_RIDE(1 << 5)
    FlyRide = 1 << 5,
    // Java parity: PVP_ENABLED(1 << 6) — @XmlEnumValue("PVP")
    PvpEnabled = 1 << 6,
    // Java parity: DUEL_SAME_RACE_ENABLED(1 << 7) — @XmlEnumValue("DUEL_SAME_RACE")
    DuelSameRaceEnabled = 1 << 7,
    // Java parity: DUEL_OTHER_RACE_ENABLED(1 << 8) — @XmlEnumValue("DUEL_OTHER_RACE")
    DuelOtherRaceEnabled = 1 << 8,
    // Java parity: NO_RETURN_BATTLE(1 << 9)
    NoReturnBattle = 1 << 9,
}
