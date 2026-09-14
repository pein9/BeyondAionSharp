namespace Aion.GameServer.Model.GameObjects.State;

/// <summary>
/// Bit-flag (and a few multibit) creature states sent to the client.
/// Java parity: model/gameobjects/state/CreatureState.
/// </summary>
/// <remarks>
/// Java carries two fields per enum constant: <c>id</c> and <c>mustMatchExact</c>. The single-bit
/// states have unique power-of-two ids; CHAIR and PRIVATE_SHOP are multibit and must match exactly.
/// Here the ids are the explicit enum values and <see cref="CreatureStateExtensions.MustMatchExact"/>
/// reproduces the exact-match flag.
/// </remarks>
public enum CreatureState
{
    ACTIVE = 1,                  // 1
    FLYING = 1 << 1,             // 2
    RESTING = 1 << 2,            // 4
    FLOATING_CORPSE = 1 << 3,     // 8
    UNK = 1 << 4,                // 16
    WEAPON_EQUIPPED = 1 << 5,     // 32
    WALK_MODE = 1 << 6,           // 64 (set = walking, unset = running)
    POWERSHARD = 1 << 7,         // 128
    TREATMENT = 1 << 8,          // 256
    GLIDING = 1 << 9,            // 512

    // multibit (id = combined value of multiple single-bit states)
    CHAIR = FLYING + RESTING,                       // 2 + 4 (mustMatchExact)
    DEAD = ACTIVE + FLYING + RESTING,               // 1 + 2 + 4
    PRIVATE_SHOP = ACTIVE + FLYING + FLOATING_CORPSE, // 1 + 2 + 8 (mustMatchExact)
    LOOTING = RESTING + FLOATING_CORPSE,             // 4 + 8
    ANY_STANCE = ACTIVE + FLYING + RESTING + FLOATING_CORPSE, // 1 + 2 + 4 + 8 (only one stance at a time)
}

public static class CreatureStateExtensions
{
    // Java parity: getId()
    public static int GetId(this CreatureState state) => (int)state;

    // Java parity: mustMatchExact() — only CHAIR and PRIVATE_SHOP are declared exact-match.
    public static bool MustMatchExact(this CreatureState state) =>
        state == CreatureState.CHAIR || state == CreatureState.PRIVATE_SHOP;

    /// <summary>True if the creature just stands there, meaning it is not flying, riding, resting, sitting, dead, looting or running a private store.</summary>
    public static bool IsStanding(int state)
    {
        return (state & (int)CreatureState.ANY_STANCE) == (int)CreatureState.ACTIVE;
    }

    /// <summary>The state naming that creature, meant as a message parameter (for example "You cannot soul-bind an item while %0.").</summary>
    /// <param name="state">the raw state value of a creature</param>
    public static ActionState GetActionState(int state)
    {
        if ((state & (int)CreatureState.WEAPON_EQUIPPED) != 0)
            return ActionState.COMBAT;
        if ((state & (int)CreatureState.TREATMENT) != 0)
            return ActionState.USING_SKILL;
        if ((state & (int)CreatureState.GLIDING) != 0)
            return ActionState.GLIDING;
        if ((state & (int)CreatureState.WALK_MODE) != 0)
            return ActionState.MOVING;
        return (state & (int)CreatureState.ANY_STANCE) switch
        {
            1 => ActionState.STANDING,
            2 => ActionState.PATH_FLYING,
            3 => ActionState.FREE_FLYING,
            4 => ActionState.RIDING,
            5 => ActionState.RESTING,
            6 => ActionState.SITTING,
            7 => ActionState.DEAD,
            8 => ActionState.FLY_DEAD,
            0xB => ActionState.PERSONAL_SHOP,
            0xC => ActionState.LOOTING,
            0xD => ActionState.FLY_LOOTING,
            _ => ActionState.CURRENT_STATUS,
        };
    }
}
