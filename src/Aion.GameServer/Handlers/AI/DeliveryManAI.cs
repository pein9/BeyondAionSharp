using Aion.GameServer.Ai;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Network.Aion.ClientPackets;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/DeliveryManAI (-Nemesiss-, Neon).</summary>
[AIName("deliveryman")]
public class DeliveryManAI : FollowingNpcAI
{
    private const int WAIT_TIME = 2 * 60 * 1000;
    private const int SERVICE_TIME = 10 * 60 * 1000;
    private int despawnMessageId = 390267; // Whiririkk, let's go!

    public DeliveryManAI(Npc owner) : base(owner) { }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        ScheduleLeave(WAIT_TIME);
    }

    private void ScheduleLeave(int delayMillis)
    {
        GetOwner().GetController().CancelTask(TaskId.DESPAWN);
        GetOwner().GetController().AddTask(TaskId.DESPAWN, ThreadPoolManager.GetInstance().Schedule(() =>
        {
            despawnMessageId = 390268; // Time is silver my friend, akakak!
            AIActions.DeleteOwner(this);
        }, delayMillis));
    }

    protected override void HandleDespawned()
    {
        PacketSendUtility.BroadcastMessage(GetOwner(), despawnMessageId);
        Player player = World.World.GetInstance().GetPlayer(GetCreatorId());
        if (player != null)
        {
            if (GetOwner().Equals(player.GetPostman()))
            {
                player.SetPostman(null);
                CM_READ_EXPRESS_MAIL.StartPostmanCooldown(player);
            }
            player.UnsetStorageManager(GetOwner());
        }
        base.HandleDespawned();
    }

    protected override void HandleCreatureSee(Creature creature)
    {
        if (GetCreatorId() == creature.GetObjectId())
        {
            HandleFollowMe(creature);
            HandleCreatureMoved(creature);
            PacketSendUtility.BroadcastMessage(GetOwner(), 390266, 500); // Here is your mail, akakak!
        }
    }

    protected override void HandleCreatureNotSee(Creature creature)
    {
        if (GetCreatorId() == creature.GetObjectId())
            GetOwner().GetController().Delete();
    }

    protected override void HandleDialogStart(Player player)
    {
        if (GetCreatorId() == player.GetObjectId())
        {
            ScheduleLeave(SERVICE_TIME);
            player.GetMailbox().Open(true, GetOwner());
            PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), DialogPage.MAIL.Id()));
        }
        else
            PacketSendUtility.BroadcastMessage(GetOwner(), 390269); // There is no mail for you, nyerk.
    }
}
