using Aion.Bots.Scenarios;
using Aion.Bots.Protocol;
using Aion.GameServer.Model;
using Aion.GameServer.Network.Aion.ServerPackets;

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

	[Fact]
	public async Task AllTwelveCasesUseOneConnectedSubjectAndWaitForGraceDisconnected()
	{
		var population = new LifecyclePopulation();
		var actors = CharacterLifecycleScenario.Cases.Select((value, index) => new LifecycleDriver(population, value, index + 1)).ToArray();
		await CharacterLifecycleScenario.RunAsync(actors);
		Assert.Equal(1, population.Peak);
		Assert.Empty(population.Connected);
		Assert.True(population.DisconnectedGraceWaits > 0);
		Assert.All(actors, actor =>
		{
			Assert.Equal(6, actor.Logins);
			Assert.Equal(6, actor.Closes);
			Assert.Equal(1, actor.Creates);
			Assert.Equal(3, actor.Deletes);
			Assert.Equal(2, actor.Restores);
			Assert.False(actor.Exists);
		});
	}

	private sealed class LifecyclePopulation
	{
		public DateTimeOffset Now = new(2026, 9, 20, 0, 0, 0, TimeSpan.Zero);
		public HashSet<int> Connected { get; } = [];
		public int Peak;
		public int DisconnectedGraceWaits;
	}

	// This exercises the shared orchestrator, not server semantics. Packet/DB behavior
	// is proved by SIM/LIVE runs; asynchronous opens/closes expose accidental overlap.
	private sealed class LifecycleDriver(LifecyclePopulation population, CharacterLifecycleCase value, int id) : ICharacterLifecycleDriver
	{
		private int deadline;
		public CharacterLifecycleCase Case => value;
		public int CharacterId => id;
		public string CharacterName => "Case" + id;
		public DateTimeOffset Now => population.Now;
		public bool Exists { get; private set; }
		public int Logins, Closes, Creates, Deletes, Restores;
		public Task StepAsync(string action, Func<CancellationToken, Task> operation, CancellationToken token) => operation(token);
		public async Task<DecodedBotServerPacket> LoginAsync(CancellationToken token)
		{
			token.ThrowIfCancellationRequested();
			Assert.Empty(population.Connected);
			Assert.True(population.Connected.Add(id));
			population.Peak = Math.Max(population.Peak, population.Connected.Count);
			Logins++;
			await Task.Yield();
			if (deadline > 0 && Now.ToUnixTimeSeconds() >= deadline) Exists = false;
			return await ListAsync(token);
		}
		public Task<DecodedBotServerPacket> CreateAsync(CancellationToken token)
		{
			Assert.False(Exists); Exists = true; Creates++;
			return Packet<SM_CREATE_CHARACTER>(new() { ["responseCode"] = 0, ["character"] = Character() });
		}
		public Task<DecodedBotServerPacket> ListAsync(CancellationToken token) => Packet<SM_CHARACTER_LIST>(new()
		{
			["characterCount"] = (byte)(Exists ? 1 : 0),
			["characters"] = Exists ? new List<IReadOnlyDictionary<string, object?>> { Character() } : new List<IReadOnlyDictionary<string, object?>>(),
		});
		public Task<DecodedBotServerPacket> DeleteAsync(CancellationToken token)
		{
			Assert.True(Exists); Deletes++;
			if (deadline == 0) deadline = (int)Now.ToUnixTimeSeconds() + 300;
			return Packet<SM_DELETE_CHARACTER>(new() { ["responseCode"] = 0, ["playerObjId"] = id, ["deletionTime"] = deadline });
		}
		public Task<DecodedBotServerPacket> RestoreAsync(CancellationToken token)
		{
			Restores++; if (Exists) deadline = 0;
			return Packet<SM_RESTORE_CHARACTER>(new() { ["chaOid"] = id, ["responseCode"] = Exists ? 0 : 0x10, ["success"] = Exists });
		}
		public async Task CloseAsync(CancellationToken token)
		{
			token.ThrowIfCancellationRequested();
			await Task.Yield();
			Assert.True(population.Connected.Remove(id)); Closes++;
		}
		public Task DelayAsync(TimeSpan delay, CancellationToken token)
		{
			if (delay >= TimeSpan.FromSeconds(1))
			{
				Assert.Empty(population.Connected); population.DisconnectedGraceWaits++;
			}
			population.Now += delay;
			return Task.CompletedTask;
		}
		public Task VerifyStoredAsync(bool exists, CancellationToken token) { Assert.Equal(exists, Exists); return Task.CompletedTask; }
		private IReadOnlyDictionary<string, object?> Character() => new Dictionary<string, object?>
		{
			["objectId"] = id, ["name"] = CharacterName, ["race"] = (int)Case.Race, ["playerClass"] = (int)Case.Class,
			["level"] = (ushort)1, ["deletionTimeSeconds"] = deadline,
		};
		private static Task<DecodedBotServerPacket> Packet<T>(Dictionary<string, object?> fields) => Task.FromResult(new DecodedBotServerPacket(typeof(T), fields));
	}
}
