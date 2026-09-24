using Aion.Bots.Scenarios;

namespace Aion.GameServer.Tests;

public sealed class NaturalRestCadenceTests
{
	[Fact]
	public async Task AttackStandsPriestBeforeDefendingWithoutWaitingFullTenSeconds()
	{
		var actions = new List<string>();
		int slices = 0;
		NaturalRestOutcome outcome = await NaturalRestCadence.RunAsync(
			(sit, _) => { actions.Add(sit ? "sit" : "stand"); return Task.CompletedTask; },
			(delay, _) =>
			{
				Assert.Equal(TimeSpan.FromSeconds(2), delay);
				actions.Add($"observe-{++slices}");
				return Task.FromResult(new NaturalRestTick(false,
					slices == 2 ? new[] { 17, 17, 18 } : Array.Empty<int>()));
			},
			(attackers, _) =>
			{
				Assert.Equal([17, 18], attackers);
				actions.Add("defend");
				return Task.CompletedTask;
			});
		Assert.Equal(NaturalRestOutcome.InterruptedByAttack, outcome);
		Assert.Equal(["sit", "observe-1", "observe-2", "stand", "defend"], actions);
	}

	[Fact]
	public async Task QuietRestStandsAfterFiveShortObservations()
	{
		var actions = new List<string>();
		NaturalRestOutcome outcome = await NaturalRestCadence.RunAsync(
			(sit, _) => { actions.Add(sit ? "sit" : "stand"); return Task.CompletedTask; },
			(_, _) => { actions.Add("observe"); return Task.FromResult(new NaturalRestTick(false, [])); },
			(_, _) => throw new Xunit.Sdk.XunitException("Quiet rest must not defend."));
		Assert.Equal(NaturalRestOutcome.Completed, outcome);
		Assert.Equal("sit", actions[0]);
		Assert.Equal("stand", actions[^1]);
		Assert.Equal(5, actions.Count(action => action == "observe"));
	}

	[Fact]
	public async Task DeathStandsWithoutDefending()
	{
		var actions = new List<string>();
		NaturalRestOutcome outcome = await NaturalRestCadence.RunAsync(
			(sit, _) => { actions.Add(sit ? "sit" : "stand"); return Task.CompletedTask; },
			(_, _) => Task.FromResult(new NaturalRestTick(true, [17])),
			(_, _) => throw new Xunit.Sdk.XunitException("Death must not start another fight."));
		Assert.Equal(NaturalRestOutcome.Dead, outcome);
		Assert.Equal(["sit", "stand"], actions);
	}
}
