using Aion.Bots.Protocol;
using Aion.Bots.World;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.Bots.Scenarios;

/// <summary>One escort attempt as the client saw it.</summary>
/// <param name="Outcome">done, lost, or open when the run ended mid-attempt.</param>
/// <param name="LossReason">For a loss: death, distance (the last gap was at or past the leash), or unobserved.</param>
public sealed record NaturalEscortAttempt(int Number, string Outcome, long StartMillis, long EndMillis, float LongestGap, int Hops,
	string? LossReason);

/// <param name="Outcome">done, give-up or dead (the caller revives and runs the protocol again).</param>
/// <param name="EndedFollowers">Follower objects whose attempt ended (deleted by the server): pass them to the next run.</param>
/// <param name="FollowerGoneAtMillis">When the follower was last seen to go, for the next run's respawn wait.</param>
public sealed record NaturalEscortResult(string Outcome, int Attempts, NaturalEscortAttempt[] Log, bool MovieSeen, float? FollowerSpeed,
	int[]? EndedFollowers = null, long? FollowerGoneAtMillis = null);

/// <summary>
/// AC-04 (docs/natural-altgard-leveling.md, "The escort handler"): the executor of <see cref="NaturalEscortPolicy"/>. Each tick
/// it observes the follower from its own packets (<c>SM_MOVE</c> positions; gone once deleted), the quest's status and var,
/// and the success movie (<c>SM_PLAY_MOVIE</c>), asks the policy, and acts: walk a hop, wait, talk through the contract's
/// start or restart step, fight, clear, or wait out a respawn on the game clock. It never logs out, flies or teleports.
/// Success is the success var together with the movie, never distance alone. Every tick and attempt is traced
/// (<c>escort-tick</c>, <c>escort-attempt</c>). Walking, fighting, clearing and retreating are the caller's.
/// </summary>
public sealed class NaturalEscortProtocol(INaturalJourneySession session, NaturalAltgardContract contract, NaturalAltgardEscort escort,
	Func<long> nowMillis)
{
	/// <summary>Walk to a point on the ground (a hop, back toward the follower, or up to him).</summary>
	public required Func<BotPosition, CancellationToken, Task> WalkToAsync { get; init; }
	/// <summary>The route from the follower's start to the goal stand.</summary>
	public required Func<IReadOnlyList<BotPosition>> Route { get; init; }
	public Func<CancellationToken, Task>? FightAsync { get; init; }
	public Func<CancellationToken, Task>? RetreatAsync { get; init; }
	/// <summary>Clear the escort's clear areas; returns when the first monster killed comes back, if one was.</summary>
	public Func<CancellationToken, Task<long?>>? ClearAsync { get; init; }
	public Func<bool>? ClearAreasHaveAggressors { get; init; }
	public Func<(int Attackers, bool DeathPredicted)>? Threat { get; init; }
	/// <summary>Attempts already spent before this run (they count against the contract's budget).</summary>
	public int PriorAttempts { get; init; }
	/// <summary>When the follower was last seen to go, if before this run.</summary>
	public long? FollowerGoneAtMillis { get; init; }
	/// <summary>Follower objects of attempts that ended before this run. Java deletes the follower at every end
	/// (<c>FollowEventHandler.stopFollow</c>), so such an object is gone even if the client missed its delete.</summary>
	public IReadOnlyCollection<int> EndedFollowerObjectIds { get; init; } = [];
	/// <summary>AG-07: true while a follower that keeps hours (Q2284's, 04:00-21:00) is outside them. An escort not under way
	/// then ends with <c>off-hours</c>, and the leg's decisions wait for the window (AG-Q3 (a)).</summary>
	public Func<bool>? OffHours { get; init; }
	public TimeSpan WaitTick { get; init; } = TimeSpan.FromMilliseconds(500);
	public int MaxTicks { get; init; } = 4000;

	public async Task<NaturalEscortResult> RunAsync(CancellationToken token)
	{
		int attempts = PriorAttempts, hops = 0, movieCursor = session.PacketHistory.Count;
		long? goneAt = FollowerGoneAtMillis, earliestRespawn = null, attemptStart = null;
		float longestGap = 0;
		float? speed = null;
		bool movieSeen = false, followerWasSeen = false;
		var ended = new HashSet<int>(EndedFollowerObjectIds);
		int? followingId = null;
		var log = new List<NaturalEscortAttempt>();
		(byte Status, int Var)? previous = NaturalAltgardQuestSteps.State(session.Api.World, escort.QuestId);
		IReadOnlyList<BotPosition> route = Route();
		for (int tick = 0; tick < MaxTicks; tick++)
		{
			await session.SynchronizeAsync(token);
			BotWorldModel world = session.Api.World;
			if (movieCursor > session.PacketHistory.Count) movieCursor = 0; // the history was cleared
			foreach (DecodedBotServerPacket packet in session.PacketHistory.Skip(movieCursor))
				if (packet.PacketType == typeof(SM_PLAY_MOVIE) && packet.Get<int>("cutsceneId") == escort.MovieId)
					movieSeen = true;
			movieCursor = session.PacketHistory.Count;
			if (session.Api.Reflexes.PendingMovie != null)
				await NaturalMovieGate.FinishAsync(session, token);
			NaturalMovieGate.RecordSkipped(session);

			BotKnownObject? follower = world.Objects.Values.FirstOrDefault(known =>
				known.Kind == BotKnownObjectKind.Npc && known.TemplateId == escort.FollowerNpcId && !known.IsCorpse &&
				!ended.Contains(known.ObjectId));
			if (follower != null)
			{
				followerWasSeen = true;
				goneAt = null;
				speed = follower.MovementSpeed ?? speed;
			}
			else if (followerWasSeen && goneAt == null)
				goneAt = nowMillis();
			(byte Status, int Var)? state = NaturalAltgardQuestSteps.State(world, escort.QuestId);
			bool completed = world.CompletedQuestIds.Contains(escort.QuestId);
			string? status = completed ? "COMPLETE" : state switch { (3, _) => "START", (4, _) => "REWARD", null => null, _ => "OTHER" };
			float gap = follower != null ? NaturalEscortPolicy.Distance3(session.CurrentPosition, follower.Position) : float.NaN;
			bool dead = world.IsDead || world.CurrentHp <= 0;

			// Attempt bookkeeping from the var the server set.
			if (previous is (3, int was) && was == escort.FollowVar && state is (3, int now) && now == escort.LostVar && attemptStart is long started)
			{
				string reason = dead ? "death" : longestGap >= escort.Leash - 5 || gap >= escort.Leash ? "distance" : "unobserved";
				Record(new(attempts, "lost", started, nowMillis(), longestGap, hops, reason));
				attemptStart = null;
				End();
			}
			if (state is (3, int follow) && follow == escort.FollowVar && attemptStart == null)
			{
				attempts++;
				attemptStart = nowMillis();
				longestGap = 0;
				hops = 0;
			}
			if (attemptStart != null && !float.IsNaN(gap)) longestGap = MathF.Max(longestGap, gap);
			if (state is (3, int following) && following == escort.FollowVar && follower != null) followingId = follower.ObjectId;
			previous = state;

			(int attackers, bool deathPredicted) = Threat?.Invoke() ?? (0, false);
			var observation = new NaturalEscortObservation(session.CurrentPosition, dead, follower?.Position, status, state?.Var ?? 0,
				movieSeen, attempts, nowMillis(), attackers, deathPredicted, ClearAreasHaveAggressors?.Invoke() ?? false, earliestRespawn,
				goneAt, route, world.MovementSpeed ?? 6);
			NaturalEscortChoice choice = NaturalEscortPolicy.Decide(observation, escort);
			session.TraceDiagnostic("escort-tick", new Dictionary<string, object?>
			{
				["escort"] = escort.Key, ["action"] = choice.Action, ["reason"] = choice.Reason, ["attempt"] = attempts,
				["status"] = status, ["var"] = state?.Var, ["gap"] = float.IsNaN(gap) ? null : gap,
				["goalDistance"] = follower is { } seen ? NaturalEscortPolicy.Distance3(seen.Position,
					new BotPosition(escort.Goal[0], escort.Goal[1], escort.Goal[2], 0)) : null,
				["movie"] = movieSeen,
			});
			if (choice.Action != "done" && !(state is (3, int on) && on == escort.FollowVar) && OffHours?.Invoke() == true)
				return new("off-hours", attempts, log.ToArray(), movieSeen, speed, [.. ended], goneAt);
			switch (choice.Action)
			{
				case "done":
					if (attemptStart is long began)
						Record(new(attempts, "done", began, nowMillis(), longestGap, hops, null));
					if (!movieSeen && !completed && status != "REWARD")
						throw new InvalidDataException($"{escort.Key}: var {escort.SuccessVar} without movie {escort.MovieId}.");
					return new("done", attempts, log.ToArray(), movieSeen, speed, [.. ended], goneAt);
				case "give-up":
					return new("give-up", attempts, log.ToArray(), movieSeen, speed, [.. ended], goneAt);
				case "revive":
					if (attemptStart is long lost)
					{
						Record(new(attempts, "lost", lost, nowMillis(), longestGap, hops, "death"));
						End();
					}
					return new("dead", attempts, log.ToArray(), movieSeen, speed, [.. ended], goneAt);
				case "blocked":
					throw new InvalidDataException($"{escort.Key}: {choice.Reason}");
				case "advance":
					hops++;
					await WalkToAsync(choice.MoveTo!.Value, token);
					break;
				case "close-gap":
				case "approach-follower":
					await WalkToAsync(choice.MoveTo!.Value, token);
					break;
				case "hold-and-fight":
					if (FightAsync == null) throw new InvalidOperationException($"{escort.Key}: attacked, and no fight routine was given.");
					await FightAsync(token);
					break;
				case "retreat":
					if (RetreatAsync == null) throw new InvalidOperationException($"{escort.Key}: retreat needed, and no retreat routine was given.");
					await RetreatAsync(token);
					break;
				case "clear":
					if (choice.WaitUntilMillis is long until && until > nowMillis())
						await session.AdvanceAsync(TimeSpan.FromMilliseconds(until - nowMillis() + 1), token);
					if (ClearAsync == null) throw new InvalidOperationException($"{escort.Key}: clearing needed, and no clear routine was given.");
					earliestRespawn = await ClearAsync(token);
					break;
				case "wait-for-respawn":
					long wait = Math.Clamp((choice.WaitUntilMillis ?? nowMillis()) - nowMillis(), 0, 5_000);
					await session.AdvanceAsync(TimeSpan.FromMilliseconds(Math.Max(wait, WaitTick.TotalMilliseconds)), token);
					break;
				case "start":
					NaturalAltgardStep step = contract.Steps.Single(candidate => candidate.Key == choice.StepKey);
					session.TraceDiagnostic("escort-start", new Dictionary<string, object?>
					{
						["escort"] = escort.Key, ["step"] = step.Key, ["attempt"] = attempts + 1, ["follower"] = follower!.ObjectId,
					});
					await NaturalAltgardQuestSteps.TalkAsync(session, step, follower.ObjectId, token);
					break;
				default: // wait-for-follower, wait-at-goal
					await session.AdvanceAsync(WaitTick, token);
					break;
			}
		}
		throw new TimeoutException($"{escort.Key}: no end after {MaxTicks} ticks.");

		// Either end deletes the follower on the server; retire his object so a missed delete is never talked to.
		void End()
		{
			if (followingId is int id) ended.Add(id);
			followingId = null;
			goneAt = nowMillis();
		}

		void Record(NaturalEscortAttempt attempt)
		{
			log.Add(attempt);
			session.TraceDiagnostic("escort-attempt", new Dictionary<string, object?>
			{
				["escort"] = escort.Key, ["number"] = attempt.Number, ["outcome"] = attempt.Outcome, ["seconds"] = (attempt.EndMillis - attempt.StartMillis) / 1000.0,
				["longestGap"] = attempt.LongestGap, ["hops"] = attempt.Hops, ["lossReason"] = attempt.LossReason,
			});
		}
	}
}
