using Aion.GameServer.Network.Aion.ClientPackets;

namespace Aion.Bots.Protocol;

/// <summary>The housing client packets, laid out as the server's readers (Java's readImpl) read them.</summary>
public static partial class GameClientPackets
{
	/// <summary>CM_GET_HOUSE_BIDS: the house broker's list (no body).</summary>
	public static BotClientPacket HouseBids() => Empty<CM_GET_HOUSE_BIDS>();

	/// <summary>CM_PLACE_BID: D listIndex, Q bid.</summary>
	public static BotClientPacket PlaceHouseBid(int listIndex, long kinah) => Create<CM_PLACE_BID>(w => { w.D(listIndex); w.Q(kinah); });

	/// <summary>CM_REGISTER_HOUSE: Q starting price, Q 100000 (the client always sends it).</summary>
	public static BotClientPacket RegisterHouse(long kinah) => Create<CM_REGISTER_HOUSE>(w => { w.Q(kinah); w.Q(100000); });

	/// <summary>CM_HOUSE_PAY_RENT: C weeks.</summary>
	public static BotClientPacket PayHouseRent(byte weeks) => Create<CM_HOUSE_PAY_RENT>(w => w.C(weeks));

	/// <summary>CM_HOUSE_TELEPORT: C action (1 own house, 2 a friend's, 3 a random friend's), D own id, D friend id.</summary>
	public static BotClientPacket HouseTeleport(byte action, int playerId, int friendId) =>
		Create<CM_HOUSE_TELEPORT>(w => { w.C(action); w.D(playerId); w.D(friendId); });

	/// <summary>CM_HOUSE_TELEPORT_BACK: back to where the house gate took the player from (no body).</summary>
	public static BotClientPacket HouseTeleportBack() => Empty<CM_HOUSE_TELEPORT_BACK>();

	/// <summary>CM_HOUSE_EDIT with an action alone: 1/2 enter/exit decoration mode, 14/15 enter/exit renovation mode.</summary>
	public static BotClientPacket HouseEdit(byte action) => Create<CM_HOUSE_EDIT>(w => w.C(action));

	/// <summary>CM_HOUSE_EDIT 3 (add an inventory item to the registry), 4 (delete from it) or 7 (take back from the house).</summary>
	public static BotClientPacket HouseEdit(byte action, int objectId) => Create<CM_HOUSE_EDIT>(w => { w.C(action); w.D(objectId); });

	/// <summary>CM_HOUSE_EDIT 5 (place) or 6 (move): D object, F x, F y, F z, H rotation.</summary>
	public static BotClientPacket HouseEdit(byte action, int objectId, float x, float y, float z, ushort rotation) =>
		Create<CM_HOUSE_EDIT>(w => { w.C(action); w.D(objectId); w.F(x); w.F(y); w.F(z); w.UH(rotation); });

	/// <summary>CM_HOUSE_DECORATE: D decoration object (0 restores the default), D its template, H the part's line number.</summary>
	public static BotClientPacket HouseDecorate(int objectId, int templateId, ushort lineNo) =>
		Create<CM_HOUSE_DECORATE>(w => { w.D(objectId); w.D(templateId); w.UH(lineNo); });

	/// <summary>CM_HOUSE_SETTINGS: C door state (1 open, 2 friends only, 3 closed), C show owner name, S sign notice.</summary>
	public static BotClientPacket HouseSettings(byte doorState, bool showOwnerName, string signNotice) =>
		Create<CM_HOUSE_SETTINGS>(w => { w.C(doorState); w.C(showOwnerName ? 1 : 0); w.S(signNotice); });

	/// <summary>CM_HOUSE_OPEN_DOOR: D address, C leave (0 enters, 1 leaves).</summary>
	public static BotClientPacket HouseOpenDoor(int address, bool leave) => Create<CM_HOUSE_OPEN_DOOR>(w => { w.D(address); w.C(leave ? 1 : 0); });

	/// <summary>CM_HOUSE_KICK: C option (1 all but friends, 2 everyone), H 0.</summary>
	public static BotClientPacket HouseKick(byte option) => Create<CM_HOUSE_KICK>(w => { w.C(option); w.H(0); });

	/// <summary>CM_USE_HOUSE_OBJECT: D object.</summary>
	public static BotClientPacket UseHouseObject(int objectId) => Create<CM_USE_HOUSE_OBJECT>(w => w.D(objectId));

	/// <summary>
	/// CM_HOUSE_SCRIPT: D address, C script slot, H total size, then (when the size is not 0) D compressed size, D uncompressed
	/// size and the compressed bytes. A total size of 0 deletes the slot.
	/// </summary>
	public static BotClientPacket HouseScript(int address, byte scriptId, byte[]? compressed, int uncompressedSize)
	{
		byte[]? content = compressed?.ToArray();
		return Create<CM_HOUSE_SCRIPT>(w =>
		{
			w.D(address); w.C(scriptId);
			if (content == null)
			{
				w.UH(0);
				return;
			}
			w.UH(8 + content.Length); w.D(content.Length); w.D(uncompressedSize); w.B(content);
		});
	}
}
