using Aion.GameServer.Model.Items;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_ITEM_USAGE_ANIMATION (ATracer). Sends a stage of an item use animation (player/target/item ids, cast time, <see cref="ItemUseAnimation"/> stage, animation suppression).</summary>
public class SM_ITEM_USAGE_ANIMATION : AionServerPacket
{
    private readonly int playerObjId;
    private readonly int targetObjId;
    private readonly int itemObjId;
    private readonly int itemId;
    private readonly int castTime;
    private readonly ItemUseAnimation animation;
    private readonly bool suppressAnimation;

    public SM_ITEM_USAGE_ANIMATION(int playerObjId, int itemObjId, int itemId)
        : this(playerObjId, playerObjId, itemObjId, itemId, 0, ItemUseAnimation.USE_SUCCESS, false)
    {
    }

    public SM_ITEM_USAGE_ANIMATION(int playerObjId, int itemObjId, int itemId, int castTime, ItemUseAnimation animation)
        : this(playerObjId, playerObjId, itemObjId, itemId, castTime, animation, false)
    {
    }

    public SM_ITEM_USAGE_ANIMATION(int playerObjId, int targetObjId, int itemObjId, int itemId, int castTime, ItemUseAnimation animation)
        : this(playerObjId, targetObjId, itemObjId, itemId, castTime, animation, false)
    {
    }

    /// <param name="playerObjId">object id of the player using the item</param>
    /// <param name="targetObjId">object id of the target</param>
    /// <param name="itemObjId">object id of the used item</param>
    /// <param name="itemId">template id of the used item</param>
    /// <param name="castTime">cast duration in milliseconds, only carried by the START stages</param>
    /// <param name="animation">stage of the animation</param>
    /// <param name="suppressAnimation">true to make the client skip the animation and only report the use, which is what the pet does when it
    /// consumes an item on its own. Only the four USE stages honour it, the soul bind and identify stages animate either way, so pass false unless a use
    /// has no visible actor.</param>
    public SM_ITEM_USAGE_ANIMATION(int playerObjId, int targetObjId, int itemObjId, int itemId, int castTime, ItemUseAnimation animation,
        bool suppressAnimation)
    {
        this.playerObjId = playerObjId;
        this.targetObjId = targetObjId;
        this.itemObjId = itemObjId;
        this.itemId = itemId;
        this.castTime = castTime;
        this.animation = animation;
        this.suppressAnimation = suppressAnimation;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteD(playerObjId);
        WriteD(targetObjId);
        WriteD(itemObjId);
        WriteD(itemId);
        WriteD(castTime);
        WriteC(animation.GetId());
        WriteC(suppressAnimation ? 1 : 0);
        WriteH(1); // number of trailing elements
        WriteD(0); // the one element, which the client counts but never reads
    }
}
