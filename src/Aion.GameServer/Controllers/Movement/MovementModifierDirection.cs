namespace Aion.GameServer.Controllers.Movement;

/// <summary>Movement relative to the owner's heading; left and right share one direction.</summary>
public enum MovementModifierDirection
{
    NONE,
    FORWARD,
    BACKWARD,
    SIDEWAYS
}

public static class MovementModifierDirectionExtensions
{
    // Java stores this flag separately from the enum ordinal (SIDEWAYS is ordinal 3, flag 4).
    public static int GetMoveStateFlag(this MovementModifierDirection direction) => direction switch
    {
        MovementModifierDirection.NONE => MovementModifierState.MoveState.NONE,
        MovementModifierDirection.FORWARD => MovementModifierState.MoveState.FORWARD,
        MovementModifierDirection.BACKWARD => MovementModifierState.MoveState.BACKWARD,
        MovementModifierDirection.SIDEWAYS => MovementModifierState.MoveState.SIDEWAYS,
        _ => throw new System.ArgumentOutOfRangeException(nameof(direction))
    };
}
