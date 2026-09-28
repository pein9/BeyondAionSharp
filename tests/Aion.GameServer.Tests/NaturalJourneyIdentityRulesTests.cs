using Aion.Bots.Scenarios;
using Aion.Bots.World;
using Aion.GameServer.Model;

namespace Aion.GameServer.Tests;

/// <summary>NA-07: the retained natural character is the Priest before Ascension or the Cleric after it, nothing else.</summary>
public sealed class NaturalJourneyIdentityRulesTests
{
	[Theory]
	[InlineData(1, 220010000)]
	[InlineData(9, 220010000)]
	[InlineData(9, 320010000)] // Q2002's instance
	[InlineData(9, 320020000)] // Q2008's instance before the class choice
	public void ThePriestLivesInIshalgenAndItsInstances(int level, int map) =>
		Assert.Equal(NaturalJourneyStage.IshalgenPriest, NaturalJourneyIdentityRules.Classify(PlayerClass.PRIEST, level, map));

	[Theory]
	[InlineData(9, 320020000)]  // chosen in Ataxiar, still at the level-10 cap
	[InlineData(9, 220010000)]  // back at Munin for Q2009
	[InlineData(10, 120010000)] // Pandaemonium
	[InlineData(10, 220030000)] // Altgard
	public void TheClericLivesOnTheBridgeMaps(int level, int map) =>
		Assert.Equal(NaturalJourneyStage.AscensionCleric, NaturalJourneyIdentityRules.Classify(PlayerClass.CLERIC, level, map));

	[Theory]
	[InlineData(PlayerClass.PRIEST, 10, 220010000)] // a Priest past the cap cannot exist naturally
	[InlineData(PlayerClass.PRIEST, 9, 120010000)]  // nor a Priest in Pandaemonium
	[InlineData(PlayerClass.CHANTER, 10, 220030000)] // the other Priest ascension is not ours
	[InlineData(PlayerClass.CLERIC, 10, 210010000)] // nor a Cleric in Poeta
	[InlineData(PlayerClass.WARRIOR, 1, 220010000)]
	public void EverythingElseIsRefusedNeverFixed(PlayerClass playerClass, int level, int map) =>
		Assert.Throws<InvalidDataException>(() => NaturalJourneyIdentityRules.Classify(playerClass, level, map));

	[Fact]
	public void WireClassIdsAndAnUnshownMapClassifyTheSameWay()
	{
		Assert.Equal(NaturalJourneyStage.AscensionCleric, NaturalJourneyIdentityRules.Classify(PlayerClass.CLERIC.GetClassId(), 10, 220030000));
		Assert.Equal(NaturalJourneyStage.IshalgenPriest, NaturalJourneyIdentityRules.Classify(PlayerClass.PRIEST.GetClassId(), 5, null));
		Assert.Throws<InvalidDataException>(() => NaturalJourneyIdentityRules.Classify(250, 5, null));
	}

	[Fact]
	public void TheJournalMustAgreeWithTheClass()
	{
		NaturalJourneyIdentityRules.RequireJournal(NaturalJourneyStage.IshalgenPriest, false, 3, 220010000);
		NaturalJourneyIdentityRules.RequireJournal(NaturalJourneyStage.AscensionCleric, true, null, 120010000);
		// The class is set before NOREWARD completes Q2008: a Cleric in Ataxiar at REWARD is legitimate.
		NaturalJourneyIdentityRules.RequireJournal(NaturalJourneyStage.AscensionCleric, false, 4, 320020000);
		Assert.Throws<InvalidDataException>(() => NaturalJourneyIdentityRules.RequireJournal(NaturalJourneyStage.IshalgenPriest, true, null, 220010000));
		Assert.Throws<InvalidDataException>(() => NaturalJourneyIdentityRules.RequireJournal(NaturalJourneyStage.AscensionCleric, false, 3, 220010000));
	}

	[Fact]
	public void RelogMustKeepTheClassAndTheBindPoint()
	{
		NaturalJourneyCheckpoint Checkpoint(int generation, byte playerClass, BotBindPoint? bind) => new(
			7, generation, 220030000, new BotPosition(1660, 1750, 260, 10), 10, 100, 100, 50, 50, false,
			[], [2008, 2009], [], [], null!, playerClass, bind);
		var fortress = new BotBindPoint(220030000, new BotPosition(1658.4f, 1815.3f, 254.1f, 0), 0);
		byte cleric = PlayerClass.CLERIC.GetClassId();
		NaturalJourneyPersistence.Verify(Checkpoint(1, cleric, fortress), Checkpoint(2, cleric, fortress));
		Assert.Throws<InvalidDataException>(() => NaturalJourneyPersistence.Verify(
			Checkpoint(1, cleric, fortress), Checkpoint(2, PlayerClass.PRIEST.GetClassId(), fortress)));
		Assert.Throws<InvalidDataException>(() => NaturalJourneyPersistence.Verify(
			Checkpoint(1, cleric, fortress), Checkpoint(2, cleric, fortress with { MapId = 220010000 })));
	}
}
