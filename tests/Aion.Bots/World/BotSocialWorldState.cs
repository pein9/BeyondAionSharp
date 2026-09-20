using Aion.Bots.Protocol;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.Bots.World;

public sealed partial class BotWorldModel
{
	private readonly Dictionary<int, BotGroupMember> groupMembers = [];
	private readonly Dictionary<int, BotAllianceMember> allianceMembers = [];
	private readonly Dictionary<int, BotLeagueAlliance> leagueAlliances = [];
	public int? AllianceId { get; private set; }
	public int? AllianceLeaderId { get; private set; }
	public int? LeagueId { get; private set; }
	public IReadOnlyDictionary<int, BotAllianceMember> AllianceMembers => allianceMembers;
	public IReadOnlyDictionary<int, BotLeagueAlliance> LeagueAlliances => leagueAlliances;
	public IReadOnlyList<int> AllianceViceCaptains { get; private set; } = [];
	private readonly Dictionary<int, BotLegionMember> legionMembers = [];
	private readonly Dictionary<int, BotFriend> friends = [];
	private readonly Dictionary<string, string> blockedPlayers = new(StringComparer.OrdinalIgnoreCase);
	public IReadOnlyDictionary<int, BotFriend> Friends => friends;
	public IReadOnlyDictionary<string, string> BlockedPlayers => blockedPlayers;
	public int? GroupId { get; private set; }
	public int? GroupLeaderId { get; private set; }
	public IReadOnlyDictionary<int, BotGroupMember> GroupMembers => groupMembers;
	public IReadOnlyList<int> GroupLootRules { get; private set; } = [];
	public string? LegionName { get; private set; }
	public byte LegionLevel { get; private set; }
	public IReadOnlyDictionary<int, BotLegionMember> LegionMembers => legionMembers;
	public int? DuelOpponentId { get; private set; }
	public BotDuelResult? LastDuelResult { get; private set; }
	public BotAbyssRank? AbyssRank { get; private set; }

	private void ApplySocial(DecodedBotServerPacket packet)
	{
		ApplyExtendedSocial(packet);
		if (packet.PacketType == typeof(SM_FRIEND_LIST))
		{
			friends.Clear();
			foreach (var value in packet.Get<List<IReadOnlyDictionary<string, object?>>>("friends"))
			{
				int id = (int)value["objectId"]!;
				friends.Add(id, new BotFriend(id, (string)value["name"]!, (int)value["level"]!, (int)value["playerClass"]!,
					(byte)value["gender"]!, (int)value["mapId"]!, (int)value["lastOnline"]!, (string)value["note"]!,
					(byte)value["status"]!, (int)value["houseAddress"]!, (byte)value["houseDoor"]!, (string)value["memo"]!));
			}
		}
		else if (packet.PacketType == typeof(SM_FRIEND_UPDATE) && !packet.Get<bool>("empty"))
		{
			var friend = friends.Values.SingleOrDefault(friend => string.Equals(friend.Name, packet.Get<string>("name"), StringComparison.OrdinalIgnoreCase));
			if (friend != null) friends[friend.ObjectId] = friend with
			{
				Level = packet.Get<int>("level"), PlayerClass = packet.Get<int>("playerClass"), Gender = packet.Get<byte>("gender"),
				MapId = packet.Get<int>("mapId"), LastOnline = packet.Get<int>("lastOnline"), Note = packet.Get<string>("note"),
				Status = packet.Get<byte>("status"),
			};
		}
		else if (packet.PacketType == typeof(SM_BLOCK_LIST))
		{
			blockedPlayers.Clear();
			foreach (var value in packet.Get<List<IReadOnlyDictionary<string, object?>>>("blocks"))
				blockedPlayers.Add((string)value["name"]!, (string)value["reason"]!);
		}
		else if (packet.PacketType == typeof(SM_ABYSS_RANK))
		{
			var previous = AbyssRank;
			AbyssRank = new BotAbyssRank(packet.Get<long>("ap"), packet.Get<int>("currentGp"), packet.Get<int>("rank"),
				packet.Get<int>("rankingListPosition"), packet.Get<int>("allKill"), packet.Get<int>("maxRank"),
				ReadPeriod("daily"), ReadPeriod("weekly"), ReadPeriod("last"));
			ObserveAbyssReward(previous, AbyssRank);
		}
		else if (packet.PacketType == typeof(SM_GROUP_INFO))
		{
			int id = packet.Get<int>("groupId");
			if (GroupId != id) groupMembers.Clear();
			GroupId = id;
			GroupLeaderId = packet.Get<int>("leaderId");
			GroupLootRules = packet.Get<int[]>("lootRules");
		}
		else if (packet.PacketType == typeof(SM_LEAVE_GROUP_MEMBER))
		{
			GroupId = null; GroupLeaderId = null; groupMembers.Clear();
			GroupLootRules = [];
			AllianceId = null; AllianceLeaderId = null; allianceMembers.Clear(); AllianceViceCaptains = [];
			LeagueId = null; leagueAlliances.Clear();
		}
		else if (packet.PacketType == typeof(SM_ALLIANCE_INFO))
		{
			int id = packet.Get<int>("allianceId");
			if (AllianceId != id) allianceMembers.Clear();
			AllianceId = id; AllianceLeaderId = packet.Get<int>("leaderId");
			AllianceViceCaptains = packet.Get<int[]>("viceCaptains").Where(value => value != 0).ToArray();
			GroupId = null; GroupLeaderId = null; groupMembers.Clear();
			GroupLootRules = [];
			int leagueId = packet.Get<int>("leagueId");
			LeagueId = leagueId == 0 ? null : leagueId;
			leagueAlliances.Clear();
			foreach (var row in packet.Get<List<IReadOnlyDictionary<string, object?>>>("alliances"))
			{
				int allianceId = (int)row["allianceId"]!;
				leagueAlliances.Add(allianceId, new BotLeagueAlliance(allianceId, (int)row["position"]!,
					(int)row["memberCount"]!, (string)row["captainName"]!, (int)row["mapId"]!));
			}
		}
		else if (packet.PacketType == typeof(SM_ALLIANCE_MEMBER_INFO))
		{
			int id = packet.Get<int>("objectId");
			byte action = packet.Get<byte>("event");
			if (action == 0) { allianceMembers.Remove(id); return; }
			var previous = allianceMembers.GetValueOrDefault(id);
			string? name = packet.Fields.GetValueOrDefault("name") as string ?? previous?.Name;
			bool online = packet.Fields.GetValueOrDefault("online") as bool? ?? (action != 3 && (previous?.Online ?? true));
			allianceMembers[id] = new BotAllianceMember(id, name, packet.Get<int>("allianceGroupId"), packet.Get<int>("mapId"),
				new BotPosition(packet.Get<float>("x"), packet.Get<float>("y"), packet.Get<float>("z"), 0),
				packet.Get<int>("currentHp"), packet.Get<int>("maxHp"), packet.Get<byte>("level"), online);
		}
		else if (packet.PacketType == typeof(SM_GROUP_MEMBER_INFO))
		{
			int id = packet.Get<int>("objectId");
			byte action = packet.Get<byte>("event");
			if (action == 0) { groupMembers.Remove(id); return; }
			string? name = packet.Fields.GetValueOrDefault("name") as string ?? groupMembers.GetValueOrDefault(id)?.Name;
			groupMembers[id] = new BotGroupMember(id, name, packet.Get<int>("mapId"),
				new BotPosition(packet.Get<float>("x"), packet.Get<float>("y"), packet.Get<float>("z"), 0),
				packet.Get<int>("currentHp"), packet.Get<int>("maxHp"), packet.Get<byte>("level"), action is not (3 or 7));
		}
		else if (packet.PacketType == typeof(SM_LEGION_INFO))
		{
			string name = packet.Get<string>("name");
			if (LegionName != name) legionMembers.Clear();
			LegionName = name; LegionLevel = packet.Get<byte>("level");
		}
		else if (packet.PacketType == typeof(SM_LEGION_MEMBERLIST))
		{
			if (packet.Get<bool>("first")) legionMembers.Clear();
			foreach (var member in packet.Get<List<IReadOnlyDictionary<string, object?>>>("members"))
				UpsertLegionMember(member, (bool)member["online"]!);
		}
		else if (packet.PacketType == typeof(SM_LEGION_ADD_MEMBER))
			UpsertLegionMember(packet.Fields, true);
		else if (packet.PacketType == typeof(SM_DUEL))
		{
			if (packet.Get<byte>("type") == 0)
			{
				DuelOpponentId = packet.Get<int>("requesterObjId"); LastDuelResult = null;
			}
			else if (packet.Get<byte>("type") == 1)
			{
				DuelOpponentId = null;
				LastDuelResult = new BotDuelResult(packet.Get<byte>("resultId"), packet.Get<int>("msgId"), packet.Get<string>("playerName"));
			}
		}

		BotAbyssPeriod ReadPeriod(string period) => new(packet.Get<int>(period + "Kill"),
			packet.Get<long>(period + "Ap"), packet.Get<int>(period + "Gp"));
	}

	private void UpsertLegionMember(IReadOnlyDictionary<string, object?> fields, bool online)
	{
		int id = (int)fields["objectId"]!;
		legionMembers[id] = new BotLegionMember(id, (string)fields["name"]!, (byte)fields["rank"]!,
			(byte)fields["playerClass"]!, Convert.ToInt32(fields["level"], System.Globalization.CultureInfo.InvariantCulture),
			(int)fields["mapId"]!, online);
	}
}

public sealed record BotGroupMember(int ObjectId, string? Name, int MapId, BotPosition Position, int CurrentHp, int MaxHp, byte Level, bool Online);
public sealed record BotAllianceMember(int ObjectId, string? Name, int GroupId, int MapId, BotPosition Position, int CurrentHp, int MaxHp, byte Level, bool Online);
public sealed record BotLeagueAlliance(int AllianceId, int Position, int MemberCount, string CaptainName, int MapId);
public sealed record BotLegionMember(int ObjectId, string Name, byte Rank, byte PlayerClass, int Level, int MapId, bool Online);
public sealed record BotDuelResult(byte ResultId, int MessageId, string OpponentName);
public sealed record BotAbyssPeriod(int Kills, long Ap, int Gp);
public sealed record BotAbyssRank(long Ap, int CurrentGp, int Rank, int RankingListPosition, int AllKills, int MaxRank,
	BotAbyssPeriod Daily, BotAbyssPeriod Weekly, BotAbyssPeriod Last);
public sealed record BotFriend(int ObjectId, string Name, int Level, int PlayerClass, byte Gender, int MapId,
	int LastOnline, string Note, byte Status, int HouseAddress, byte HouseDoor, string Memo);
