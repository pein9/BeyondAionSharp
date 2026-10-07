using Aion.Bots.Scenarios.Classes;
using Aion.GameServer.Model;

namespace Aion.Bots.Scenarios;

/// <summary>Which leg of the natural journey a retained character is on.</summary>
public enum NaturalJourneyStage
{
	/// <summary>The pre-Ascension Priest: Ishalgen, its Q2002 instance, or Q2008's Ataxiar before the class choice.</summary>
	IshalgenPriest,
	/// <summary>The Cleric chosen at Ascension (D25): the rest of Q2008, Pandaemonium and Altgard.</summary>
	AscensionCleric,
}

/// <summary>
/// NA-07: the only two states a retained natural character may be in. Everything else is rejected — a Chanter,
/// a Priest past level 9, a Cleric outside the bridge's maps — because the journey never "fixes" a character.
/// ND-01 permits the level-20 Cleric's Space of Destiny only when the caller explicitly selects Altgard l11.
/// AX-03 permits the level-25 Cleric's Morheim and Triniel arena only when the caller selects the ax leg.
/// </summary>
public static class NaturalJourneyIdentityRules
{
	public const int AscensionQuestId = 2008;

	/// <summary>Maps a Priest can stand on: Ishalgen, Q2002's instance, and Q2008's instance before SETPRO14.</summary>
	public static readonly int[] PriestMaps = [220010000, 320010000, 320020000];

	/// <summary>Maps a Cleric can stand on: Q2008's instance after SETPRO14, Ishalgen (the Q2008/Q2009 Munin
	/// steps), Pandaemonium and Altgard.</summary>
	public static readonly int[] ClericMaps = [320020000, 220010000, 120010000, 220030000];

	/// <summary>Classify what a character list, an admin view or the client shows; null world means not shown.
	/// The optional leg scopes the approved Q2900 instance without broadening the bridge's default maps.
	/// The accepted line, the Priest who becomes a Cleric.</summary>
	public static NaturalJourneyStage Classify(PlayerClass playerClass, int level, int? worldId, string? altgardLeg = null) =>
		Classify(NaturalClassLine.PriestCleric, playerClass, level, worldId, altgardLeg);

	/// <summary>The same classification from a wire class id (SM_CHARACTER_LIST, SM_PLAYER_INFO).</summary>
	public static NaturalJourneyStage Classify(int classId, int level, int? worldId, string? altgardLeg = null) =>
		Classify(NaturalClassLine.PriestCleric, classId, level, worldId, altgardLeg);

	/// <summary>
	/// CP-25: the same two states for any class line. <see cref="NaturalJourneyStage.IshalgenPriest"/> is the line's
	/// starter before Ascension, at level 1-9 on the starter maps; <see cref="NaturalJourneyStage.AscensionCleric"/> is
	/// the line's second class on the bridge maps, and a line without a second class has no such state. The Convent and
	/// the leg-scoped maps are the Cleric's legs and stay tied to the Cleric.
	/// </summary>
	public static NaturalJourneyStage Classify(NaturalClassLine line, PlayerClass playerClass, int level, int? worldId, string? altgardLeg = null)
	{
		if (playerClass == line.Starter && level is >= 1 and <= 9 && (worldId is null || PriestMaps.Contains(worldId.Value)))
			return NaturalJourneyStage.IshalgenPriest;
		if (line.Second is { } second && playerClass == second && level >= 9 && (worldId is null || ClericMaps.Contains(worldId.Value) ||
			second == PlayerClass.CLERIC && (
			level >= 10 && worldId == 120020000 || // PC-06: ordinary Convent visit after the ceremony.
			altgardLeg == "l11" && level >= 20 && worldId == 320070000 ||
			altgardLeg == "l12" && level >= 16 && worldId == 300200000 ||
			altgardLeg == NaturalAbyssEntry.Leg && level >= 25 && worldId is NaturalAbyssEntry.Morheim or NaturalAbyssEntry.ArenaMap)))
			return NaturalJourneyStage.AscensionCleric;
		throw new InvalidDataException(
			$"Retained natural character is outside the journey: {playerClass} level {level} on map {worldId?.ToString() ?? "unknown"}.");
	}

	/// <summary>The line's classification from a wire class id.</summary>
	public static NaturalJourneyStage Classify(NaturalClassLine line, int classId, int level, int? worldId, string? altgardLeg = null) =>
		Classify(line, PlayerClassExtensions.GetPlayerClassById(checked((byte)classId), true)
			?? throw new InvalidDataException($"Unknown player class id {classId}."), level, worldId, altgardLeg);

	/// <summary>Once the journals are observed: a Priest has not completed Ascension; a Cleric either has, or is
	/// still in Q2008's REWARD step inside Ataxiar (the class is set before NOREWARD completes the quest).</summary>
	public static void RequireJournal(NaturalJourneyStage stage, bool ascensionCompleted, byte? ascensionStatus, int? worldId)
	{
		bool consistent = stage switch
		{
			NaturalJourneyStage.IshalgenPriest => !ascensionCompleted,
			NaturalJourneyStage.AscensionCleric => ascensionCompleted || ascensionStatus == 4 && worldId == 320020000,
			_ => false,
		};
		if (!consistent)
			throw new InvalidDataException($"{stage} contradicts Ascension (completed={ascensionCompleted}, status={ascensionStatus}, map={worldId}).");
	}
}
