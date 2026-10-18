using System.Text;
using Aion.GameServer.Dao;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.House;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.Collections;
using Aion.GameServer.Utils.Xml;
using Microsoft.Extensions.Logging;

namespace Aion.GameServer.Model.GameObjects;

/// <summary>
/// Java parity: model/gameobjects/player/PlayerScripts (Rolandas, Neon, Sykra). A house's eight script slots: setting or
/// removing one stores it through HouseScriptsDAO, as Java does (only the DAO's own load passes storeInDb false). An
/// empty slot holds an empty byte array where Java holds null; both have no data.
/// </summary>
public sealed class PlayerScripts
{
	public const byte SCRIPT_LIMIT = 8; // max number of scripts a player can have
	private static readonly ILogger log = AionLog.For(nameof(PlayerScripts));

	private readonly int houseObjId;
	private readonly PlayerScript[] scripts = new PlayerScript[SCRIPT_LIMIT];

	public PlayerScripts(int houseId)
	{
		houseObjId = houseId;
		FillEmptyScriptsArray();
	}

	private void FillEmptyScriptsArray()
	{
		for (int i = 0; i < SCRIPT_LIMIT; i++)
			scripts[i] = new PlayerScript(i, Array.Empty<byte>(), 0);
	}

	public bool Set(int id, byte[] compressedXML, int uncompressedSize)
	{
		return Set(id, compressedXML, uncompressedSize, true);
	}

	public bool Set(int id, byte[] compressedXML, int uncompressedSize, bool storeInDb)
	{
		if (IsInvalidScriptId(id))
			return false;
		string? scriptXML = DecompressAndValidate(compressedXML, uncompressedSize);
		if (scriptXML == null)
			return false;
		if (storeInDb)
		{
			Aion.GameServer.Model.House.House house = HousingService.GetInstance().FindHouseOrStudio(houseObjId);
			if (house.GetPersistentState() == IPersistable.PersistentState.NEW)
				house.Save(); // new houses must be inserted first, due to foreign key constraints
			HouseScriptsDAO.StoreScript(houseObjId, id, scriptXML);
		}
		scripts[id] = new PlayerScript(id, compressedXML, uncompressedSize);
		return true;
	}

	public void Remove(int scriptId)
	{
		if (IsInvalidScriptId(scriptId))
			return;
		HouseScriptsDAO.DeleteScript(houseObjId, scriptId);
		scripts[scriptId] = new PlayerScript(scriptId, Array.Empty<byte>(), 0);
	}

	public void RemoveAll()
	{
		HouseScriptsDAO.DeleteScriptsForHouse(houseObjId);
		FillEmptyScriptsArray();
	}

	public PlayerScript? Get(int scriptId)
	{
		if (IsInvalidScriptId(scriptId))
			return null;
		return scripts[scriptId];
	}

	private bool IsInvalidScriptId(int scriptId)
	{
		return scriptId < 0 || scriptId >= scripts.Length;
	}

	private string? DecompressAndValidate(byte[]? compressedXML, int uncompressedSize)
	{
		if (compressedXML == null || compressedXML.Length == 0)
			return "";
		byte[] bytes;
		try
		{
			bytes = CompressUtil.Decompress(compressedXML);
		}
		catch (Exception ex)
		{
			log.LogError(ex, "Housing script data for house {HouseObjId} could not be decompressed", houseObjId);
			return null;
		}
		string scriptXML = Encoding.Unicode.GetString(bytes);
		if (bytes.Length != uncompressedSize)
		{
			log.LogWarning("Unexpected housing script size after decompression for house {HouseObjId}: Expected {Expected} bytes, got {Actual} bytes:\n{Script}",
				houseObjId, uncompressedSize, bytes.Length, scriptXML);
			return null;
		}
		return scriptXML;
	}

	public void SendToPlayer(Player player, int houseAddress)
	{
		if (player == null)
			return;
		SplitList<PlayerScript> scriptSplitList = new DynamicServerPacketBodySplitList<PlayerScript>(
			new List<PlayerScript>(scripts), false, SM_HOUSE_SCRIPTS.STATIC_BODY_SIZE,
			SM_HOUSE_SCRIPTS.DYNAMIC_BODY_PART_SIZE_CALCULATOR);
		foreach (var part in scriptSplitList)
			PacketSendUtility.SendPacket(player, new SM_HOUSE_SCRIPTS(houseAddress, part));
	}
}
