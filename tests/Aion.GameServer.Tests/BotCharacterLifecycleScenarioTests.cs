using Aion.Bots.Scenarios;
using Aion.GameServer.Model;

namespace Aion.GameServer.Tests;

public sealed class BotCharacterLifecycleScenarioTests
{
	[Fact]
	public void MatrixCoversBothPlayableRacesAndEveryActualStartingClassExactlyOnce()
	{
		var cases = CharacterLifecycleScenario.Cases;
		Assert.Equal(12, cases.Count);
		Assert.Equal(cases.Count, cases.Distinct().Count());
		Assert.Equal(new[] { Race.ELYOS, Race.ASMODIANS }, cases.Select(c => c.Race).Distinct());
		var classes = Enum.GetValues<PlayerClass>().Where(pc => pc.IsStartingClass()).Order().ToArray();
		Assert.Equal(6, classes.Length);
		foreach (var race in new[] { Race.ELYOS, Race.ASMODIANS })
			Assert.Equal(classes, cases.Where(c => c.Race == race).Select(c => c.Class).Order());
	}

	[Fact]
	public async Task MissingSubjectsCannotSilentlyPassTheMatrix() =>
		await Assert.ThrowsAsync<InvalidDataException>(() => CharacterLifecycleScenario.RunAsync([]));
}
