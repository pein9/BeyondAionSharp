using Aion.Bots.Reflexes;

namespace Aion.Bots.Scenarios;

/// <summary>
/// NA-10: finishes a quest movie the client is watching. In skip mode the reflex already answered and there is
/// nothing to do. In watch mode (or for an unskippable movie) the client waits the movie's length — game time in
/// SIM, real time in LIVE — then sends CM_PLAY_MOVIE_END, which clears Java's WATCHING_CUTSCENE state. Until then
/// the timing contract refuses CM_MOVE, as the server would drop it.
/// </summary>
public static class NaturalMovieGate
{
	public static async Task<BotPendingMovie?> FinishAsync(INaturalJourneySession session, CancellationToken token)
	{
		if (session.Api.Reflexes.PendingMovie is not { } movie) return null;
		await session.AdvanceAsync(movie.Length, token);
		BotPendingMovie finished = session.Api.FinishPendingMovie()!;
		await session.SendPacketAsync(finished.End, token);
		session.TraceDiagnostic("movie-watched", new Dictionary<string, object?>
		{
			["movieId"] = finished.MovieId, ["questId"] = finished.QuestId, ["canSkip"] = finished.CanSkip,
			["mode"] = session.Api.Reflexes.MoviePolicy.Mode.ToString(), ["heldMillis"] = finished.Length.TotalMilliseconds,
		});
		return finished;
	}
}
