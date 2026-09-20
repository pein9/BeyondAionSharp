using System.Text.Json;
using Aion.Bots.Scenarios;

namespace Aion.GameServer.Tests;

public sealed class QuestPersistenceContractTests
{
	[Fact]
	public void OnlineSnapshotCannotStandInForSavedQuestRows()
	{
		var online = JsonSerializer.SerializeToElement(new { online = true, player = new { characterId = 42 } });
		var error = Assert.Throws<InvalidDataException>(() => QuestPersistenceContract.AssertCompletedOnce(online, [1101]));
		Assert.Contains("offline", error.Message);
		var missing = JsonSerializer.SerializeToElement(new { online = false });
		Assert.Throws<InvalidDataException>(() => QuestPersistenceContract.AssertCompletedOnce(missing, [1101]));
	}

	[Theory]
	[InlineData("COMPLETE", 1, true)]
	[InlineData("COMPLETE", 0, false)]
	[InlineData("COMPLETE", 2, false)]
	[InlineData("START", 1, false)]
	public void EveryRequestedRowMustBeSavedExactlyOnce(string status, int count, bool valid)
	{
		var snapshot = JsonSerializer.SerializeToElement(new
		{
			online = false,
			quests = new[] { new { questId = 1101, status, completeCount = count } },
		});
		if (valid) QuestPersistenceContract.AssertCompletedOnce(snapshot, [1101]);
		else Assert.Throws<InvalidDataException>(() => QuestPersistenceContract.AssertCompletedOnce(snapshot, [1101]));
		Assert.Throws<InvalidDataException>(() => QuestPersistenceContract.AssertCompletedOnce(snapshot, [1101, 1102]));
	}
}
