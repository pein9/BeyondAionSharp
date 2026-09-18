using Aion.GameServer.Dao;

namespace Aion.GameServer.Services.Admin;

internal static class AdminPlayerQuestRows
{
	public static object Load(int playerId) =>
		PlayerQuestListDAO.Load(playerId).GetAllQuestState()
			.OrderBy(state => state.GetQuestId())
			.Select(state => new
			{
				questId = state.GetQuestId(),
				status = state.GetStatus().ToString(),
				questVars = state.GetQuestVars().GetQuestVars(),
				flags = state.GetFlags(),
				completeCount = state.GetCompleteCount(),
				reward = state.GetRewardGroup(),
				completeTime = state.GetLastCompleteTime()
			})
			.ToArray();
}
