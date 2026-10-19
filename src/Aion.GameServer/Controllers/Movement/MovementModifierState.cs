using Aion.GameServer.Utils;

namespace Aion.GameServer.Controllers.Movement;

/// <summary>
/// Java's two movement modifier slots: a pending direction activates after 666 ms; an effective outgoing
/// direction fades for 1332 ms. At most two directions apply. Missing packets do not expire pending movement.
/// </summary>
public sealed class MovementModifierState
{
    private const int ACTIVATION_DELAY = 666;
    private const int DEACTIVATION_DELAY = 1332;
    private int pendingState = MoveState.NONE;
    private long pendingSince;
    private int confirmedState = MoveState.NONE;
    private long confirmedSince;

    public void SetMoveState(int moveState) => SetMoveState(moveState, SystemClock.CurrentMillis());

    internal void SetMoveState(int moveState, long now)
    {
        if (pendingState == moveState)
            return;
        if (pendingState != MoveState.NONE && unchecked(now - pendingSince) > ACTIVATION_DELAY)
        {
            confirmedState = pendingState;
            confirmedSince = now;
        }
        pendingState = moveState;
        pendingSince = now;
    }

    public int GetMoveState() => GetMoveState(SystemClock.CurrentMillis());

    internal int GetMoveState(long now)
    {
        int moveState = MoveState.NONE;
        if (confirmedState != MoveState.NONE && unchecked(now - confirmedSince) < DEACTIVATION_DELAY)
            moveState = confirmedState;
        if (pendingState != MoveState.NONE && unchecked(now - pendingSince) > ACTIVATION_DELAY)
            moveState |= pendingState;
        return moveState;
    }

    public override string ToString()
    {
        long now = SystemClock.CurrentMillis();
        string state = $"[moveState={GetMoveState(now)}, pending={pendingState} since {unchecked(now - pendingSince)}ms";
        if (confirmedState != MoveState.NONE && unchecked(now - confirmedSince) < DEACTIVATION_DELAY)
            state += $", {confirmedState} fades out in {unchecked(DEACTIVATION_DELAY - (now - confirmedSince))}ms";
        return state + "]";
    }

    public static class MoveState
    {
        public const int NONE = 0;
        public const int FORWARD = 1;
        public const int BACKWARD = 2;
        public const int SIDEWAYS = 4;
        public const int COUNT = 8;
    }
}
