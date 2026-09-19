using Aion.Bots.Protocol;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.Bots.World;

public sealed partial class BotWorldModel
{
	private readonly Dictionary<int, BotGroupRecruitment> groupRecruitments = [];
	private readonly Dictionary<int, BotGroupApplication> groupApplications = [];
	private readonly Dictionary<int, BotLegionEmblem> legionEmblems = [];
	private readonly Dictionary<ushort, BotLegionHistoryPage> legionHistoryPages = [];
	public IReadOnlyDictionary<int, BotGroupRecruitment> GroupRecruitments => groupRecruitments;
	public IReadOnlyDictionary<int, BotGroupApplication> GroupApplications => groupApplications;
	// Emblem broadcasts include nearby legions, not necessarily our own.
	public IReadOnlyDictionary<int, BotLegionEmblem> LegionEmblems => legionEmblems;
	public IReadOnlyDictionary<ushort, BotLegionHistoryPage> LegionHistoryPages => legionHistoryPages;
	public BotRecallRequest? RecallRequest { get; private set; }
	public long? LegionWarehouseKinah { get; private set; }
	// The server sends no close-window packet after an answer; the real client closes its own prompt.
	internal void CloseRecallPrompt() => RecallRequest = null;

	private void ApplyExtendedSocial(DecodedBotServerPacket packet)
	{
		if (packet.PacketType == typeof(SM_FIND_GROUP))
		{
			switch (packet.Get<byte>("action"))
			{
				case 0:
					groupRecruitments.Clear();
					foreach (var row in packet.Get<List<IReadOnlyDictionary<string, object?>>>("entries"))
					{
						int id = (int)row["objectId"]!;
						groupRecruitments.Add(id, new BotGroupRecruitment(id, (byte)row["serverId"]!, (byte)row["soloFlag"]!,
							(byte)row["groupType"]!, (string)row["message"]!, (string)row["name"]!, (byte)row["size"]!,
							(byte)row["minLevel"]!, (byte)row["maxLevel"]!, (int)row["lastUpdate"]!));
					}
					break;
				case 1: groupRecruitments.Remove(packet.Get<int>("objectId")); break;
				case 4:
					groupApplications.Clear();
					foreach (var row in packet.Get<List<IReadOnlyDictionary<string, object?>>>("entries"))
					{
						int id = (int)row["objectId"]!;
						groupApplications.Add(id, new BotGroupApplication(id, (byte)row["groupType"]!, (string)row["message"]!,
							(string)row["name"]!, (byte)row["playerClass"]!, (byte)row["level"]!, (int)row["lastUpdate"]!));
					}
					break;
				case 5: groupApplications.Remove(packet.Get<int>("objectId")); break;
			}
		}
		else if (packet.PacketType == typeof(SM_RECALLED_BY_OTHER))
			RecallRequest = packet.Get<bool>("closed") ? null : new BotRecallRequest(packet.Get<string>("casterName"),
				packet.Get<ushort>("skillId"), packet.Get<ushort>("seconds"));
		else if (packet.PacketType == typeof(SM_LEGION_UPDATE_EMBLEM))
		{
			int id = packet.Get<int>("legionId");
			legionEmblems[id] = new BotLegionEmblem(id, packet.Get<byte>("emblemId"), packet.Get<byte>("emblemType"),
				packet.Get<byte>("alpha"), packet.Get<byte>("red"), packet.Get<byte>("green"), packet.Get<byte>("blue"));
		}
		else if (packet.PacketType == typeof(SM_LEGION_HISTORY))
		{
			ushort type = packet.Get<ushort>("historyType");
			var entries = packet.Get<List<IReadOnlyDictionary<string, object?>>>("entries")
				.Select(row => new BotLegionHistoryEntry((int)row["timestamp"]!, (byte)row["action"]!,
					(string)row["name"]!, (string)row["description"]!)).ToArray();
			legionHistoryPages[type] = new BotLegionHistoryPage(type, packet.Get<int>("totalEntries"), packet.Get<int>("page"), entries);
		}
		else if (packet.PacketType == typeof(SM_LEGION_EDIT))
		{
			if (packet.Get<byte>("type") == 0) LegionLevel = packet.Get<byte>("level");
			else if (packet.Get<byte>("type") == 4) LegionWarehouseKinah = packet.Get<long>("warehouseKinah");
		}
	}
}

public sealed record BotGroupRecruitment(int ObjectId, byte ServerId, byte SoloFlag, byte GroupType,
	string Message, string Name, byte Size, byte MinLevel, byte MaxLevel, int LastUpdate);
public sealed record BotGroupApplication(int ObjectId, byte GroupType, string Message, string Name, byte PlayerClass,
	byte Level, int LastUpdate);
public sealed record BotRecallRequest(string CasterName, ushort SkillId, ushort Seconds);
public sealed record BotLegionEmblem(int LegionId, byte EmblemId, byte EmblemType, byte Alpha, byte Red, byte Green, byte Blue);
public sealed record BotLegionHistoryEntry(int Timestamp, byte Action, string Name, string Description);
public sealed record BotLegionHistoryPage(ushort Type, int TotalEntries, int Page, IReadOnlyList<BotLegionHistoryEntry> Entries);
