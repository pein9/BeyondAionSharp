using Aion.Bots.Protocol;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.Bots.Reflexes;

public delegate BotReviveType? BotRevivePolicy(BotDeathPrompt prompt);

public delegate byte? BotQuestionPolicy(BotQuestionPrompt prompt);

public enum BotMovieMode
{
	/// <summary>Answer at once, as a player who presses Esc does (the default; the bot's behaviour since NI-00).</summary>
	Skip,
	/// <summary>Hold CM_PLAY_MOVIE_END for the movie's length (real-client observation, NA-28).</summary>
	Watch,
}

/// <summary>NA-10: whether quest movies are skipped or watched, and how long a watched movie lasts.</summary>
public sealed record BotMoviePolicy(BotMovieMode Mode, IReadOnlyDictionary<int, TimeSpan>? Lengths = null)
{
	/// <summary>Until NA-28 measures them in the real client, a watched movie is held this long.</summary>
	public static readonly TimeSpan DefaultLength = TimeSpan.FromSeconds(20);
	public static BotMoviePolicy Skip { get; } = new(BotMovieMode.Skip);
	public TimeSpan LengthOf(int movieId) => Lengths?.GetValueOrDefault(movieId) is { } length && length > TimeSpan.Zero ? length : DefaultLength;
}

/// <summary>A movie whose end the client has not sent yet; the session waits <see cref="Length"/> and sends <see cref="End"/>.</summary>
public sealed record BotPendingMovie(int MovieId, int QuestId, bool CanSkip, TimeSpan Length, BotClientPacket End);

/// <summary>Immediate protocol reactions that an honest game client sends for specific server packets.</summary>
public sealed class BotReflexes
{
	private readonly BotRevivePolicy revivePolicy;
	private readonly BotQuestionPolicy questionPolicy;

	public BotReflexes(BotRevivePolicy? revivePolicy = null, BotQuestionPolicy? questionPolicy = null, BotMoviePolicy? moviePolicy = null)
	{
		this.revivePolicy = revivePolicy ?? (_ => null);
		this.questionPolicy = questionPolicy ?? (_ => null);
		MoviePolicy = moviePolicy ?? BotMoviePolicy.Skip;
	}

	public BotMoviePolicy MoviePolicy { get; }

	/// <summary>The movie being watched, if its end has not been sent (watch mode, or an unskippable movie).</summary>
	public BotPendingMovie? PendingMovie { get; private set; }

	private readonly Queue<BotPendingMovie> skippedMovies = new();

	/// <summary>NA-24: the next movie the skip policy answered at once, for the run record; null when none is left.</summary>
	public BotPendingMovie? TakeSkippedMovie() => skippedMovies.TryDequeue(out BotPendingMovie? movie) ? movie : null;

	/// <summary>Hand over the held movie end to send; the cutscene is over for the client.</summary>
	public BotPendingMovie? TakePendingMovie()
	{
		BotPendingMovie? movie = PendingMovie;
		PendingMovie = null;
		return movie;
	}

	public BotClientPacket? RespondTo(DecodedBotServerPacket packet)
	{
		if (packet.PacketType == typeof(SM_PLAYER_SPAWN))
			return GameClientPackets.LevelReady();
		if (packet.PacketType == typeof(SM_TELEPORT_LOC))
			return GameClientPackets.TeleportAnimationDone();
		if (packet.PacketType == typeof(SM_PLAY_MOVIE))
		{
			bool canSkip = packet.Get<bool>("canSkip");
			BotClientPacket end = GameClientPackets.PlayMovieEnd(packet.Get<bool>("isMovie") ? (byte)1 : (byte)0,
				packet.Get<int>("objectId"), packet.Get<int>("questId"), packet.Get<int>("cutsceneId"), canSkip);
			int movieId = packet.Get<int>("cutsceneId");
			if (MoviePolicy.Mode == BotMovieMode.Skip && canSkip)
			{
				skippedMovies.Enqueue(new BotPendingMovie(movieId, packet.Get<int>("questId"), canSkip, TimeSpan.Zero, end));
				return end;
			}
			// Watched, or unskippable: the client plays it through before answering.
			PendingMovie = new BotPendingMovie(movieId, packet.Get<int>("questId"), canSkip, MoviePolicy.LengthOf(movieId), end);
			return null;
		}
		if (packet.PacketType == typeof(SM_DIE))
		{
			var prompt = new BotDeathPrompt(packet.Get<bool>("allowReviveBySkill"),
				packet.Get<bool>("allowReviveByItem"), packet.Get<int>("remainingKiskTimeSeconds"),
				packet.Get<bool>("allowInstanceRevive"), packet.Get<bool>("invasion"));
			var revive = revivePolicy(prompt);
			return revive == null ? null : GameClientPackets.Revive((byte)revive.Value);
		}
		if (packet.PacketType == typeof(SM_QUESTION_WINDOW))
		{
			var prompt = new BotQuestionPrompt(packet.Get<int>("code"), packet.Get<string[]>("params"),
				packet.Get<int>("senderId"), packet.Get<int>("rangeOrCooldownSeconds"));
			var answer = questionPolicy(prompt);
			return answer == null ? null : GameClientPackets.QuestionResponse(prompt.Code, answer.Value, prompt.SenderId);
		}
		return null;
	}
}

public enum BotReviveType : byte
{
	Bind = 0,
	Rebirth = 1,
	SelfReviveItem = 2,
	Skill = 3,
	Kisk = 4,
	Instance = 6,
	Obelisk = 8,
}

public readonly record struct BotDeathPrompt(bool AllowBySkill, bool AllowByItem, int RemainingKiskTimeSeconds,
	bool AllowInstance, bool Invasion);

public sealed record BotQuestionPrompt(int Code, IReadOnlyList<string> Parameters, int SenderId,
	int RangeOrCooldownSeconds);
