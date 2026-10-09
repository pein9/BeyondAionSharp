using Aion.Bots.World;
using Aion.GameServer.Model;

namespace Aion.Bots.Scenarios;

public sealed record NaturalCapitalDecision(string Action, string Reason,
	NaturalAltgardStep? Step = null, NaturalCapitalPortal? Portal = null);

/// <summary>PC-06: one next action from the observed journal, including interrupted rewards and Convent stays.</summary>
public static class NaturalCapitalDecisionEngine
{
	public static readonly NaturalAltgardStep[] CitySteps =
	[
		NaturalCapitalSteps.Blessing[0], NaturalCapitalSteps.Supply[0], NaturalCapitalSteps.Pets[0],
		NaturalCapitalSteps.Supply[1], NaturalCapitalSteps.Convent[0], NaturalCapitalSteps.Convent[1],
		NaturalCapitalSteps.Blessing[1], NaturalCapitalSteps.Blessing[2],
		.. NaturalCapitalSteps.Pets[1..], .. NaturalCapitalSteps.Blessing[3..],
		.. NaturalCapitalSteps.Artisans, NaturalCapitalSteps.Convent[2],
		NaturalCapitalSteps.BookOffer, NaturalCapitalSteps.BookReward,
		NaturalCapitalSteps.Supply[2],
	];

	public static void ValidateScope(NaturalJourneyOptions options, string profile)
	{
		if (options.CapitalStage == null) return;
		if (options.CapitalStage is not ("start" or "first") || !profile.StartsWith("SIM-", StringComparison.Ordinal) ||
			!options.AscensionBridge || options.AltgardLegId != null || options.AltgardLeg1 ||
			options.StopAfterQuest != null || options.StopAt != null || options.Course != null || options.Encounter != null ||
			options.MauPolicy != null || options.ClericEncounter ||
			options.AltgardOnlyQuests != null || options.CoinGearReceiptPath != null || options.HaramelProgressPath != null)
			throw new InvalidOperationException("Capital checkpoints require a SIM Ascension run with stage start/first and no other diagnostic scope.");
	}

	/// <param name="line">NR-31: the run's class line; the pass is its second class's, with the contract's dispatch quest
	/// (<see cref="NaturalCapitalContract.ForLine"/>). Unset is the accepted line, the Cleric.</param>
	public static NaturalCapitalDecision Decide(NaturalCapitalContract contract, NaturalAscensionObservation state,
		Classes.NaturalClassLine? line = null)
	{
		Classes.NaturalClassLine played = line ?? Classes.NaturalClassLine.Default;
		string ceremony = $"The capital pass requires the completed level-10 {played.SecondName} ceremony";
		NaturalCapitalDecision Block(string reason) => new("blocked", reason);
		bool Done(int id) => state.CompletedQuestIds.Contains(id);
		BotQuestState? Quest(int id) => state.Quests.GetValueOrDefault(id);
		int Var(int id) => (Quest(id)?.StepAndFlags ?? 0) & 0x00FFFFFF;
		if (!state.Synchronized || state.MapId == null) return new("observe", "Wait for both journals and the player view.");
		if (state.IsDead) return new("recover", "Record the death and use the retained bind/revival budget.");
		// CP-27, NR-31: the capital pass is the line's second class's; any other class is refused by name.
		if (played.Second is not { } second || state.ClassId != second.GetClassId())
			return Block($"{ceremony}; the character is {NaturalJourneyIdentityRules.ClassName(state.ClassId)}.");
		if (state.Level < contract.MinimumLevel ||
			!contract.RequiredCompletedQuestIds.All(Done)) return Block($"{ceremony}.");
		if (Quest(contract.DispatchQuestId) is not { Status: 3 } || Var(contract.DispatchQuestId) != 0 || Done(contract.DispatchQuestId))
			return Block($"Leave Q{contract.DispatchQuestId} at START/0 during this pass.");
		if (contract.Branch.ExcludedQuestIds.Any(id => Done(id) || Quest(id)?.Status is 3 or 4) ||
			Quest(2911) is { Status: 4 } && Var(2911) != 2)
			return Block("The observed Blessing branch is outside the approved Ribbon/Lost Love choice.");
		if (state.MapId == 120020000)
		{
			if (!Done(29004) && Quest(29004) is { Status: 4 } && Var(29004) == 2)
				return new("talk", "Claim Veldina's Call at Angulof.", NaturalCapitalSteps.Convent[3]);
			return new("portal", "Return through the Convent statue before doing city work.",
				Portal: contract.Portals.Single(portal => portal.MapId == state.MapId));
		}
		if (state.MapId != contract.MapId && state.MapId != 220010000)
			return Block("The capital pass is outside its city, Convent and Ishalgen recovery maps.");
		if (contract.CompletedQuestIds.All(Done)) return new("complete", "All ten approved quests are complete.");
		if (state.MapId == 220010000) return new("travel-city", "Resume the unfinished capital pass through the ordinary teleporter.");
		foreach (NaturalAltgardStep step in CitySteps)
		{
			int id = step.QuestId;
			if (Done(id)) continue; // Q2953 is repeatable; take only the approved first completion.
			int prerequisite = id switch { 2912 => 2911, 2914 => 2912, 29044 => 29040, 29045 => 29044, _ => 0 };
			if (prerequisite != 0 && !Done(prerequisite)) continue;
			if (step.Key == "q2953-finish" && !Done(29004)) continue; // Doman is the final city stop.
			BotQuestState? quest = Quest(id);
			if (step.Key == NaturalCapitalSteps.BookReward.Key && quest is { Status: 3 } && Var(id) == 0)
				return state.ItemCounts.GetValueOrDefault(182212217) > 0
					? new("read-book", "Use the manual Seriphim supplied.") : Block("The supplied manual is missing.");
			if (step.ExpectedStatus == "OFFER" && quest?.Status is not (3 or 4))
				return new("talk", "Accept the eligible quest at its proper starter.", step);
			if (quest is { Status: 4 } && step.Actions.Contains("SELECTED_QUEST_REWARD1"))
				return new("talk", "Resume the interrupted reward at its proper recipient.",
					step.ExpectedStatus == "REWARD" ? step : NaturalCapitalSteps.ResumeReward(step));
			if (quest is { Status: 3 } && step.ExpectedStatus == "START" && Var(id) == step.Var)
				return new("talk", "Follow the next required NPC contact.", step);
		}
		if (!Done(29004) && Quest(29004) is { Status: 4 } && Var(29004) == 2)
			return new("portal", "Use the shipped statue to reach Angulof.",
				Portal: contract.Portals.Single(portal => portal.MapId == state.MapId));
		return Block("No reviewed capital action fits the observed journal.");
	}
}
