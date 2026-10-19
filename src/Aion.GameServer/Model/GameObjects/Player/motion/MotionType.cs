namespace Aion.GameServer.Model.GameObjects.Players.Motion;

/// <summary>Custom-animation slots in Java declaration order; wire IDs are one-based.</summary>
public enum MotionType { IDLE, RUN, JUMP, REST, SHOP }

public static class MotionTypeExtensions
{
    public static int GetId(this MotionType type) => (int)type + 1;
    public static MotionType? GetById(int id) => id switch
    {
        1 => MotionType.IDLE, 2 => MotionType.RUN, 3 => MotionType.JUMP,
        4 => MotionType.REST, 5 => MotionType.SHOP, _ => null
    };
}
