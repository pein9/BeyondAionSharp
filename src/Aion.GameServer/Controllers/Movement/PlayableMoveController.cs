using System;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Controllers.Movement;

/// <summary>
/// Non-generic view of Java's erased <c>PlayableMoveController&lt;?&gt;</c>. Consumers such as SM_MOVE need the
/// playable-only wire fields without assuming whether the owner is a Player or Summon.
/// </summary>
public interface IPlayableMoveController
{
    float VectorX { get; }
    float VectorY { get; }
    float VectorZ { get; }
    byte GlideFlag { get; }
    int GeyserLocationId { get; }
    int VehicleUnknown1 { get; }
    int VehicleUnknown2 { get; }
}

/// <summary>Java parity: controllers/movement/PlayableMoveController (ATracer) — base class for summon &amp; player move controller.</summary>
public abstract class PlayableMoveController<T> : CreatureMoveController<T>, IPlayableMoveController where T : Creature
{
    private bool sendMovePacket = true;
    private readonly MovementModifierState movementModifierState = new();

    public float vehicleX;
    public float vehicleY;
    public float vehicleZ;

    public float vectorX;
    public float vectorY;
    public float vectorZ;
    public byte glideFlag;
    public int unk1;
    public int unk2;
    public int geyserLocationId; // locationId from windstreams.xml

    float IPlayableMoveController.VectorX => vectorX;
    float IPlayableMoveController.VectorY => vectorY;
    float IPlayableMoveController.VectorZ => vectorZ;
    byte IPlayableMoveController.GlideFlag => glideFlag;
    int IPlayableMoveController.GeyserLocationId => geyserLocationId;
    int IPlayableMoveController.VehicleUnknown1 => unk1;
    int IPlayableMoveController.VehicleUnknown2 => unk2;

    public PlayableMoveController(T owner)
        : base(owner)
    {
    }

    public override void StartMovingToDestination()
    {
        UpdateLastMove();
        if (Owner.CanPerformMove())
        {
            if (IsControlled() && Started.CompareAndSet(false, true))
            {
                this.MovementMaskField = MovementMask.NPC_STARTMOVE;
                SendForcedMovePacket();
                Aion.GameServer.Taskmanager.Tasks.PlayerMoveTaskManager.GetInstance().AddPlayer(Owner);
            }
        }
    }

    private bool IsControlled()
    {
        return Owner.GetEffectController().IsUnderFear() || Owner.GetEffectController().IsConfused();
    }

    private void SendForcedMovePacket()
    {
        Aion.GameServer.Utils.PacketSendUtility.BroadcastPacketAndReceive(Owner, new Aion.GameServer.Network.Aion.ServerPackets.SM_MOVE(Owner));
        sendMovePacket = false;
    }

    public override void MoveToDestination()
    {
        if (!Owner.CanPerformMove())
        {
            if (Started.CompareAndSet(true, false))
            {
                SetAndSendStopMove(Owner);
                UpdateLastMove();
                OnMovementStopped();
            }
            return;
        }

        if (sendMovePacket && IsControlled())
        {
            SendForcedMovePacket();
        }

        float x = Owner.GetX();
        float y = Owner.GetY();
        float z = Owner.GetZ();

        float dist = (float)Aion.GameServer.Utils.PositionUtil.GetDistance(x, y, z, TargetDestX, TargetDestY, TargetDestZ);
        if (dist < 0.01f)
            return;

        // Fear/confuse movement takes the speed penalty immediately, without client combat-modifier delays.
        float currentSpeed = Aion.GameServer.Utils.Stats.StatFunctions.AdjustSpeedByMovementModifier(CalculateMovementDirection(), Owner.GetGameStats().GetMovementSpeedFloat());
        long msElapsed = SystemClock.CurrentMillis() - LastMoveUpdate;
        float futureXYDistPassed = Math.Min(currentSpeed * msElapsed / 1000f, dist);
        float futureZDistPassed = IsJumping() ? Math.Min(2 * msElapsed / 1000f, dist) : futureXYDistPassed;

        float distXYFraction = futureXYDistPassed / dist;
        float distZFraction = IsJumping() ? futureZDistPassed / dist : distXYFraction;
        float newX = (TargetDestX - x) * distXYFraction + x;
        float newY = (TargetDestY - y) * distXYFraction + y;
        float newZ = (TargetDestZ - z) * distZFraction + z;

        /*
         * if ((movementMask & MovementMask.MOUSE) == 0) { targetDestX = newX + vectorX; targetDestY = newY + vectorY; targetDestZ = newZ + vectorZ; }
         */

        Aion.GameServer.World.World.GetInstance().UpdatePosition(Owner, newX, newY, newZ, Heading, false);
        UpdateLastMove();
    }

    public override void AbortMove()
    {
        Started.Set(false);
        OnMovementStopped();
        Aion.GameServer.Taskmanager.Tasks.PlayerMoveTaskManager.GetInstance().RemovePlayer(Owner);
        TargetDestX = 0;
        TargetDestY = 0;
        TargetDestZ = 0;
        SetAndSendStopMove(Owner);
    }

    protected override void SetNewDirection(float x, float y, float z)
    {
        if (TargetDestX != x || TargetDestY != y || TargetDestZ != z)
        {
            sendMovePacket = true;
        }
        base.SetNewDirection(x, y, z);
    }

    /// <summary>Feed only client-requested movement, after the position update, using the requested destination.</summary>
    public void UpdateMovementModifierDirection() => SetMovementModifierDirection(CalculateMovementDirection());

    protected void SetMovementModifierDirection(MovementModifierDirection direction)
    {
        movementModifierState.SetMoveState(direction.GetMoveStateFlag());
    }

    private MovementModifierDirection CalculateMovementDirection()
    {
        if (PositionUtil.GetDistance(Owner.GetX(), Owner.GetY(), TargetDestX, TargetDestY) < MOVE_CHECK_OFFSET)
            return MovementModifierDirection.NONE;
        return CalculateDirection(PositionUtil.GetHeadingTowards(Owner.GetX(), Owner.GetY(), TargetDestX, TargetDestY), Heading);
    }

    internal static MovementModifierDirection CalculateDirection(byte destinationHeading, byte ownHeading)
    {
        // Java byte is signed; quantize both headings before comparing the symmetric 45/135 degree boundaries.
        int headingDiff = (unchecked((sbyte)destinationHeading) + 120 - unchecked((sbyte)ownHeading)) % 120;
        if (headingDiff > 60)
            headingDiff = 120 - headingDiff;
        if (headingDiff <= 15)
            return MovementModifierDirection.FORWARD;
        return headingDiff >= 45 ? MovementModifierDirection.BACKWARD : MovementModifierDirection.SIDEWAYS;
    }

    public void OnMovementStopped() => movementModifierState.SetMoveState(MovementModifierState.MoveState.NONE);

    public int GetMoveState() => movementModifierState.GetMoveState();
}
