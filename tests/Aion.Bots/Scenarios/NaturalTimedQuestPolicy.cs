namespace Aion.Bots.Scenarios;

/// <summary>
/// What the client sees of a timed quest (AB-03). <paramref name="QuestStatus"/> is null while the quest is not taken (or
/// was abandoned). <paramref name="TimerEndsAtMillis"/> is when the timer the client last saw started runs out (its
/// <c>SM_QUEST_ACTION</c> timer plus the time it arrived); null when no timer was seen for the current attempt.
/// <paramref name="UnitsLeft"/> is the kills (Q2288) or items (Q2230) still needed; <paramref name="UnitsPerKill"/> the
/// expected units per kill (1 for a kill counter, the drop chance for a drop); <paramref name="SecondsPerKill"/> a kill
/// with its rest; <paramref name="SecondsBack"/> the walk back to the giver. <paramref name="Attempts"/> counts the timers
/// started so far; <paramref name="Ready"/> is the caller's "full HP and mana, buffed, nothing on the bot".
/// </summary>
public sealed record NaturalTimedObservation(string? QuestStatus, int QuestVar, long NowMillis, long? TimerEndsAtMillis,
	int UnitsLeft, double UnitsPerKill, double SecondsPerKill, double SecondsBack, int Attempts, bool Ready);

/// <param name="Action">take, start-timer, hunt, turn-in, new-chance, wait-until-ready, wait-for-journal, give-up or done.</param>
public sealed record NaturalTimedChoice(string Action, string Reason, double SlackSeconds);

/// <summary>
/// AB-03 (docs/natural-altgard-leveling.md): the pure policy of a timed quest, over the contract's <see cref="NaturalAltgardTimer"/>.
/// Java: Q2288's SETPRO1 starts <c>QuestService.questTimerStart(env, 600)</c>, and the timer's end or a logout abandons the
/// quest. Q2230's accept starts 1,800 s; at the end only the task is cancelled. The next CHECK_USER_HAS_QUEST_ITEM without a
/// running timer takes the tusks and shows page 3057; SETPRO1 then starts a new chance. A logout abandons it and takes the
/// tusks. The policy starts a timer only when the bot is ready, hunts while the budget allows (and on after it runs short,
/// since stopping gains nothing), turns in as soon as the work is done, and spends the retry budget (AB-Q2) on expiry.
/// Nothing that loses the quest is allowed while its timer runs.
/// </summary>
public static class NaturalTimedQuestPolicy
{
	/// <summary>AB-Q2: tries per timed quest before the leg stops on it as a finding.</summary>
	public const int MaxAttempts = 3;
	/// <summary>Headroom kept on every budget: dialogs, a pull that goes wrong, the walk's detours.</summary>
	public const double MarginSeconds = 30;

	/// <summary>What the bot must not do while a timer runs: each abandons the quest, or eats time it cannot spare.</summary>
	public static readonly IReadOnlySet<string> ForbiddenWhileTimed = new HashSet<string>
	{
		"logout", "relog", "return-scroll", "teleport", "rest-trip", "restock-trip",
	};

	public static bool TimerRunning(NaturalTimedObservation state) =>
		state.QuestStatus == "START" && state.TimerEndsAtMillis is long ends && ends > state.NowMillis;

	public static bool Allowed(string action, NaturalTimedObservation state) =>
		!(TimerRunning(state) && ForbiddenWhileTimed.Contains(action));

	/// <summary>Seconds to spare once the rest of the work, the walk back and the margin are paid for; negative when short.</summary>
	public static double Slack(NaturalTimedObservation state)
	{
		if (state.TimerEndsAtMillis is not long ends) return double.PositiveInfinity;
		double left = (ends - state.NowMillis) / 1000.0;
		double work = state.UnitsLeft <= 0 ? 0 : state.UnitsLeft / Math.Max(state.UnitsPerKill, 0.01) * state.SecondsPerKill;
		return left - work - state.SecondsBack - MarginSeconds;
	}

	/// <summary>A rest of <paramref name="seconds"/> fits while a timer runs only when the budget still has room for it.</summary>
	public static bool MayRest(NaturalTimedObservation state, double seconds) => !TimerRunning(state) || Slack(state) >= seconds;

	/// <summary>The work a full timer needs, against its length: can one attempt succeed at all?</summary>
	public static double FullBudgetSlack(NaturalAltgardTimer timer, int units, double unitsPerKill, double secondsPerKill, double secondsBack) =>
		timer.Seconds - units / Math.Max(unitsPerKill, 0.01) * secondsPerKill - secondsBack - MarginSeconds;

	public static NaturalTimedChoice Decide(NaturalTimedObservation state, NaturalAltgardTimer timer)
	{
		double slack = Slack(state);
		if (state.QuestStatus is "REWARD" or "COMPLETE")
			return new("done", "The timed quest is handed in.", slack);
		bool running = TimerRunning(state);
		bool expired = state.QuestStatus == "START" && state.TimerEndsAtMillis is long ends && ends <= state.NowMillis;
		if (state.QuestStatus is null)
		{
			// Q2288's expiry abandoned it; the next take is a new attempt only once a timer has been spent.
			if (state.Attempts >= MaxAttempts)
				return new("give-up", $"{state.Attempts} timers spent on Q{timer.QuestId} (AB-Q2); stop the leg on it as a finding.", slack);
			bool acceptStartsTimer = TimerStartsAtAccept(timer);
			if (acceptStartsTimer && !state.Ready)
				return new("wait-until-ready", "Accepting starts the timer: full HP and mana and nothing on the bot first.", slack);
			return new("take", acceptStartsTimer ? "Ready: accept, which starts the timer." : "Take the quest; the timer starts later.", slack);
		}
		if (state.QuestStatus != "START")
			return new("give-up", $"Q{timer.QuestId} is {state.QuestStatus}: not a timed-quest state.", slack);
		if (expired)
		{
			if (timer.OnExpiry == "abandon")
				return new("wait-for-journal", "The timer ran out; the server abandons the quest at once. Wait for the journal to show it.", slack);
			if (state.Attempts >= MaxAttempts)
				return new("give-up", $"{state.Attempts} timers spent on Q{timer.QuestId} (AB-Q2); stop the leg on it as a finding.", slack);
			return new("new-chance", $"The timer ran out: the check takes the items (page {timer.ExpiredPage}), then {timer.NewChanceAction} starts another.", slack);
		}
		if (!running)
		{
			// Q2288 at var 0: taken, the timer not yet started.
			if (!state.Ready)
				return new("wait-until-ready", "The timer starts with the next dialog: full HP and mana and nothing on the bot first.", slack);
			return new("start-timer", "Ready: start the timer at the giver and go straight to the grounds.", slack);
		}
		if (state.UnitsLeft <= 0)
			return new("turn-in", "The work is done: back to the giver before the timer ends.", slack);
		return new("hunt", slack >= 0
			? $"{state.UnitsLeft} to go and {slack:F0} s to spare: keep hunting."
			: $"{state.UnitsLeft} to go and {-slack:F0} s short: hunt on anyway, since stopping gains nothing.", slack);
	}

	/// <summary>Q2230's accept starts its timer; Q2288's starts at a later step (SETPRO1 at var 0).</summary>
	public static bool TimerStartsAtAccept(NaturalAltgardTimer timer) => timer.StartStep.Contains("-offer-", StringComparison.Ordinal);
}
