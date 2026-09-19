using Aion.GameServer.Network.Aion.ClientPackets;

namespace Aion.Bots.Protocol;

public static partial class GameClientPackets
{
	// CM_PET at upstream ce54b7931: adoption includes five reserved fields before the UTF-16 name.
	public static BotClientPacket AdoptPet(int eggObjectId, int templateId, string name, int decorationId = 0) => Create<CM_PET>(w =>
	{
		w.H(1); w.D(eggObjectId); w.D(templateId); w.C(0); w.D(0); w.D(decorationId); w.D(0); w.D(0); w.S(name);
	});
	public static BotClientPacket SummonPet(int templateId) => Create<CM_PET>(w => { w.H(3); w.D(templateId); });
	public static BotClientPacket DismissPet(int templateId) => Create<CM_PET>(w => { w.H(4); w.D(templateId); });
	public static BotClientPacket FeedPet(int foodObjectId, int count) => Create<CM_PET>(w =>
	{
		w.H(9); w.D(1); w.D(foodObjectId); w.D(count); w.D(0);
	});
}
