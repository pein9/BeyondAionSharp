using Aion.Bots.Protocol;
using Aion.Bots.Scenarios;
using Aion.GameServer.Model;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Tests;

public sealed class BotPrologueProtocolTests
{
	private static DecodedBotServerPacket Movie(int quest, int movie) => new(typeof(SM_PLAY_MOVIE),
		new Dictionary<string, object?> { ["questId"] = quest, ["cutsceneId"] = movie });

	[Theory]
	[InlineData(Race.ELYOS, 1000, 1)]
	[InlineData(Race.ASMODIANS, 2000, 2)]
	public async Task UsesTheMovieAlreadyObservedInThisEntryWithoutReadingAnother(Race race, int quest, int movie)
	{
		var expected = Movie(quest, movie);
		var result = await BotPrologueProtocol.WaitAsync([expected], 0, race,
			(_, _) => throw new InvalidOperationException("The handshake already read the prologue."), CancellationToken.None);
		Assert.Same(expected, result);
	}

	[Fact]
	public async Task APreviousEntryOrUnrelatedQuestMovieCannotSatisfyTheWait()
	{
		var old = Movie(2000, 2);
		var unrelated = Movie(2900, 156);
		var expected = Movie(2000, 2);
		int waits = 0;
		var result = await BotPrologueProtocol.WaitAsync([old, unrelated], 1, Race.ASMODIANS, (predicate, _) =>
		{
			waits++;
			Assert.False(predicate(unrelated));
			Assert.False(predicate(Movie(1000, 1)));
			Assert.False(predicate(Movie(2000, 99)));
			Assert.True(predicate(expected));
			return Task.FromResult(expected);
		}, CancellationToken.None);
		Assert.Same(expected, result);
		Assert.Equal(1, waits);
	}

	[Fact]
	public void CancellationIsHonoredEvenWhenTheMovieWasAlreadyObserved()
	{
		using var cancelled = new CancellationTokenSource();
		cancelled.Cancel();
		Assert.Throws<OperationCanceledException>(() =>
		{
			_ = BotPrologueProtocol.WaitAsync([Movie(2000, 2)], 0,
				Race.ASMODIANS, (_, _) => throw new InvalidOperationException(), cancelled.Token);
		});
	}
}
