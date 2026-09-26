using Aion.Bots.Scenarios;

namespace Aion.GameServer.Tests;

public sealed class NaturalJourneyPersistenceTests
{
	[Fact]
	public void FreshLoginAllowsWireTimersAndRegenerationButPreservesPlayerState()
	{
		var before = Checkpoint();
		var after = before with
		{
			ConnectionGeneration = 2, CurrentHp = 769, CurrentMp = 1284,
			Position = before.Position with { X = before.Position.X + 0.01f },
			Quests = [before.Quests[0] with { TimerSeconds = 10 }],
			CompletedQuestIds = before.CompletedQuestIds.Reverse().ToArray(),
			Skills = [before.Skills[0] with { Flag = 123456 }],
		};
		NaturalJourneyPersistence.Verify(before, after);
	}

	[Theory]
	[InlineData("generation")]
	[InlineData("identity")]
	[InlineData("map")]
	[InlineData("level")]
	[InlineData("position")]
	[InlineData("heading")]
	[InlineData("completed")]
	[InlineData("ascension")]
	[InlineData("inventory")]
	[InlineData("equipment")]
	[InlineData("skills")]
	public void MissingOrChangedPersistedStateFailsAcceptance(string changed)
	{
		var before = Checkpoint();
		var after = before with { ConnectionGeneration = 2 };
		after = changed switch
		{
			"generation" => after with { ConnectionGeneration = 1 },
			"identity" => after with { CharacterId = 99 },
			"map" => after with { MapId = 320010000 },
			"level" => after with { Level = 10 },
			"position" => after with { Position = before.Position with { X = 100 } },
			"heading" => after with { Position = before.Position with { Heading = 5 } },
			"completed" => after with { CompletedQuestIds = [2000] },
			"ascension" => after with { Quests = [before.Quests[0] with { StepAndFlags = 1 }] },
			"inventory" => after with { Inventory = [before.Inventory[0] with { Count = 9 }] },
			"equipment" => after with { Inventory = [before.Inventory[0] with { EquipmentSlot = 1 }] },
			"skills" => after with { Skills = [before.Skills[0] with { Level = 1 }] },
			_ => throw new ArgumentOutOfRangeException(nameof(changed)),
		};
		Assert.Throws<InvalidDataException>(() => NaturalJourneyPersistence.Verify(before, after));
	}

	private static NaturalJourneyCheckpoint Checkpoint() => new(42, 1, 220010000,
		new(379, 1892.768f, 327.6875f, 30), 9, 600, 769, 1000, 1284, false,
		[new(2008, 3, 0, 0, null)], [2000, 2001], [new(101, 162000002, 10, 65535)],
		[new(1839, 3, 0, 0, 0, 0)], new(1, "journey-complete", null, "complete", "done", [], []));
}
