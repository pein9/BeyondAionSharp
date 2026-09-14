namespace Aion.GameServer.Controllers.Observer;

/// <summary>
/// Creature events an observer can subscribe to.
/// Java parity: controllers/observer/ObserverType.
/// </summary>
public enum ObserverType
{
    MOVE,
    ATTACK,
    ATTACKED,
    EQUIP,
    UNEQUIP,
    STARTSKILLCAST,
    DEATH,
    DOT_ATTACKED,
    ITEMUSE,
    ABNORMALSETTED,
    SUMMONRELEASE,
    SIT,
    HP_CHANGED,
    ENDSKILLCAST,
    BOOSTSKILLCOST,
}
