using Aion.GameServer.Controllers.Attack;

namespace Aion.GameServer.SkillEngine.Model;

/// <summary>
/// Reserved resource change (HP/MP/FP/DP) for an effect, with display position/ordering.
/// Java parity: skillengine/model/EffectReserved.
/// </summary>
public class EffectReserved : IComparable<EffectReserved>
{
    private readonly int _position;
    private readonly int _value;
    private readonly ResourceType _type;
    private readonly bool _isDamage = true;
    private readonly bool _send = true;
    private readonly AttackStatus _attackStatus = AttackStatus.NORMALHIT;

    // Java parity: nested enum EffectReserved.ResourceType
    public enum ResourceType
    {
        HP = 0,
        MP = 1,
        FP = 2,
        DP = 3, // TODO recheck
    }

    public EffectReserved(int position, int value, ResourceType type, bool isDamage) : this(position, value, type, isDamage, true) { }

    public EffectReserved(int position, int value, ResourceType type, bool isDamage, bool send, AttackStatus attackStatus) : this(position, value, type, isDamage, send)
    {
        _attackStatus = attackStatus;
    }

    public EffectReserved(int position, int value, ResourceType type, bool isDamage, bool send)
    {
        _position = position;
        _value = value;
        _type = type;
        _isDamage = isDamage;
        _send = send;
    }

    public int GetPosition() => _position;
    public int GetValue() => _value;

    // Java parity: getValueToSend()
    public int GetValueToSend() => _isDamage ? _value : -_value;

    public ResourceType GetType_() => _type;
    public bool IsDamage() => _isDamage;
    public bool IsSend() => _send;

    /// <summary>The attack status of this position, NORMALHIT for values which cannot crit (like drained mp).</summary>
    public AttackStatus GetAttackStatus() => _attackStatus;

    // Java parity: compareTo(EffectReserved) — by position, then hashCode tiebreak.
    public int CompareTo(EffectReserved? o)
    {
        int result = 0;
        if (_position < o!.GetPosition())
            result = -1;
        else if (_position > o.GetPosition())
            result = 1;

        if (result == 0)
            result = GetHashCode() - o.GetHashCode();

        return result;
    }
}

public static class EffectReservedResourceTypeExtensions
{
    // Java parity: getValue()
    public static int GetValue(this EffectReserved.ResourceType type) => (int)type;

    // Java parity: static ResourceType.of(HealType) — valueOf(healType.name())
    public static EffectReserved.ResourceType Of(HealType healType) =>
        Enum.Parse<EffectReserved.ResourceType>(healType.ToString());
}
