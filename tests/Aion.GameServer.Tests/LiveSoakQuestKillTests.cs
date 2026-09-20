using Aion.Bots.World;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.LiveBots;

namespace Aion.GameServer.Tests;

public sealed class LiveSoakQuestKillTests
{
	[Fact]
	public void KillCreditSurvivesLootEnableThenCorpseDeletionInTheSameDrain()
	{
		var world = new BotWorldModel();
		world.Apply(new(typeof(SM_NPC_INFO), new Dictionary<string, object?>
		{ ["objectId"] = 141840, ["npcId"] = 210133, ["visualNpcId"] = 210133, ["x"] = 1114.44f, ["y"] = 1001.72f, ["z"] = 127.24f }));
		Quest(world, 1102, 1);
		Loot(world, 141840, SM_LOOT_STATUS.Status.LOOT_ENABLE);
		Assert.True(LiveBotRunner.SoakQuestKillCompleted(world, 141840, 1102, 1));
		world.Apply(new(typeof(SM_DELETE), new Dictionary<string, object?> { ["objectId"] = 141840 }));
		Assert.False(world.Objects.ContainsKey(141840));
		Assert.False(world.LootStatuses.ContainsKey(141840));
		Assert.True(LiveBotRunner.SoakQuestKillCompleted(world, 141840, 1102, 1));
		Assert.False(LiveBotRunner.SoakQuestKillCompleted(world, 141840, 1102, 2));
	}

	[Theory]
	[InlineData(1102, 0, 3)]
	[InlineData(1102, 1, 5)]
	[InlineData(2102, 1, 3)]
	public void LootEnableCannotSubstituteForExactActiveQuestKillCredit(int observedQuest, int count, byte status)
	{
		var world = new BotWorldModel();
		Quest(world, observedQuest, count, status);
		Loot(world, 141840, SM_LOOT_STATUS.Status.LOOT_ENABLE);
		Assert.False(LiveBotRunner.SoakQuestKillCompleted(world, 141840, 1102, 1));
	}

	[Fact]
	public void KillCreditJumpFailsBeforeAnotherCast()
	{
		var world = new BotWorldModel();
		Quest(world, 1102, 2);
		Assert.Throws<InvalidDataException>(() => LiveBotRunner.SoakQuestKillCompleted(world, 141840, 1102, 1));
	}

	[Fact]
	public void ItemObjectiveStillRequiresItsOwnLootEnableAndDoesNotUseKillCredit()
	{
		var world = new BotWorldModel();
		Quest(world, 1105, 1);
		Loot(world, 99, SM_LOOT_STATUS.Status.LOOT_ENABLE);
		Assert.False(LiveBotRunner.SoakQuestKillCompleted(world, 141840, 1105, null));
		Loot(world, 141840, SM_LOOT_STATUS.Status.LOOT_DISABLE);
		Assert.False(LiveBotRunner.SoakQuestKillCompleted(world, 141840, 1105, null));
		Loot(world, 141840, SM_LOOT_STATUS.Status.LOOT_ENABLE);
		Assert.True(LiveBotRunner.SoakQuestKillCompleted(world, 141840, 1105, null));
		world.Apply(new(typeof(SM_DELETE), new Dictionary<string, object?> { ["objectId"] = 141840 }));
		Assert.False(LiveBotRunner.SoakQuestKillCompleted(world, 141840, 1105, null));
	}

	private static void Quest(BotWorldModel world, int id, int count, byte status = 3) => world.Apply(new(typeof(SM_QUEST_ACTION),
		new Dictionary<string, object?> { ["action"] = (byte)2, ["questId"] = id, ["status"] = status, ["stepAndFlags"] = count }));
	private static void Loot(BotWorldModel world, int id, SM_LOOT_STATUS.Status status) => world.Apply(new(typeof(SM_LOOT_STATUS),
		new Dictionary<string, object?> { ["targetObjectId"] = id, ["status"] = (byte)status, ["lootEffectId"] = 0 }));
}
