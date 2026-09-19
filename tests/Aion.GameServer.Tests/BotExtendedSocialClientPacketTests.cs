using Aion.Bots.Protocol;
using Aion.GameServer.Model.Team.Legion;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Tests;

public sealed partial class BotGameClientPacketWriterTests
{
	private static IEnumerable<object[]> ExtendedSocialPacketCases(AionConnection.State game)
	{
		yield return C("recruitment-list", GameClientPackets.FindGroupList(), game, "action", 0);
		yield return C("application-list", GameClientPackets.FindGroupList(true), game, "action", 4);
		foreach (bool update in new[] { false, true })
		{
			var expected = new Dictionary<string, object?>
			{
				["action"] = update ? 3 : 2, ["playerOrTeamId"] = 123, ["message"] = "Post", ["groupType"] = 2,
			};
			if (update) { expected["serverId"] = (byte)5; expected["unk1"] = (byte)0; expected["unk2"] = (byte)0; expected["unk3"] = (byte)16; }
			yield return C("recruitment-" + update, GameClientPackets.FindGroupRecruitment(123, "Post", 2, update, 5), game, expected);
			yield return C("application-" + update, GameClientPackets.FindGroupApplication(456, "Apply", 7, 23, 1, update), game,
				new Dictionary<string, object?> { ["action"] = update ? 7 : 6, ["playerOrTeamId"] = 456,
					["message"] = "Apply", ["groupType"] = 1, ["classId"] = 7, ["level"] = 23 });
		}
		yield return C("remove-recruitment", GameClientPackets.FindGroupRemove(123, serverId: 5, soloFlag: 0), game,
			new Dictionary<string, object?> { ["action"] = 1, ["playerOrTeamId"] = 123, ["serverId"] = (byte)5, ["unk3"] = (byte)0 });
		yield return C("remove-application", GameClientPackets.FindGroupRemove(456, true), game,
			new Dictionary<string, object?> { ["action"] = 5, ["playerOrTeamId"] = 456 });
		yield return C("recall-accept", GameClientPackets.RecallAnswer(true), game, "answer", 0);
		yield return C("recall-decline", GameClientPackets.RecallAnswer(false), game, "answer", 1);
		yield return C("legion-emblem", GameClientPackets.LegionEmblem(789, 3, 0, 255, 250, 128, 64), game,
			new Dictionary<string, object?> { ["legionId"] = 789, ["emblemId"] = 3, ["emblemType"] = LegionEmblemType.DEFAULT,
				["alpha"] = 255, ["red"] = 250, ["green"] = 128, ["blue"] = 64 });
		foreach (byte type in new byte[] { 0, 1, 2 })
			yield return C("legion-history-" + type, GameClientPackets.LegionHistory(2, type), game,
				new Dictionary<string, object?> { ["page"] = 2, ["type"] = (LegionHistoryAction.Type)type });
		foreach (bool deposit in new[] { false, true })
			yield return C("legion-kinah-" + deposit, GameClientPackets.LegionWarehouseKinah(5_000_000_001L, deposit), game,
				new Dictionary<string, object?> { ["amount"] = 5_000_000_001L, ["actionType"] = (byte)(deposit ? 1 : 0) });
	}
}
