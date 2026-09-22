using Aion.Bots.Protocol;
using Aion.Bots.Scenarios;
using Aion.GameServer.Model;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.LiveBots;

namespace Aion.GameServer.Tests;

public sealed class NaturalIshalgenIdentityScenarioTests
{
	[Fact]
	public async Task FreshAccountCreatesPriestOnceThenReloginsWithSameRetainedId()
	{
		var driver = new IdentityDriver(exists: false);

		NaturalIshalgenIdentityResult result = await NaturalIshalgenIdentityScenario.RunAsync(driver);

		Assert.True(result.CreatedThisRun);
		Assert.Equal(driver.CharacterId, result.CharacterId);
		Assert.Equal((ushort)1, result.Level);
		Assert.Equal(1, driver.Creates);
		Assert.Equal(2, driver.Logins);
		Assert.Equal(2, driver.Enters);
		Assert.Equal(2, driver.OnlineVerifications);
		Assert.Equal(2, driver.Quits);
		Assert.Equal(1, driver.ReentryWaits);
		Assert.Equal([driver.CharacterId, driver.CharacterId], driver.Selections);
		Assert.True(driver.Exists);
		Assert.DoesNotContain(driver.Steps, step => step.Contains("delete", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public async Task ExistingValidPriestIsReusedWithoutCreation()
	{
		var driver = new IdentityDriver(exists: true);

		NaturalIshalgenIdentityResult result = await NaturalIshalgenIdentityScenario.RunAsync(driver);

		Assert.False(result.CreatedThisRun);
		Assert.Equal(driver.CharacterId, result.CharacterId);
		Assert.Equal((ushort)1, result.Level);
		Assert.Equal(0, driver.Creates);
		Assert.Equal([driver.CharacterId, driver.CharacterId], driver.Selections);
		Assert.True(driver.Exists);
	}

	[Fact]
	public async Task ProgressedPreAscensionPriestKeepsTheSameIdentity()
	{
		var driver = new IdentityDriver(exists: true, level: 9);

		NaturalIshalgenIdentityResult result = await NaturalIshalgenIdentityScenario.RunAsync(driver);

		Assert.False(result.CreatedThisRun);
		Assert.Equal((ushort)9, result.Level);
		Assert.Equal([driver.CharacterId, driver.CharacterId], driver.Selections);
	}

	[Theory]
	[InlineData("name")]
	[InlineData("race")]
	[InlineData("class")]
	[InlineData("level")]
	[InlineData("deletion")]
	[InlineData("multiple")]
	public async Task ConflictingRetainedIdentityFailsWithoutCreatingOrEntering(string conflict)
	{
		var driver = new IdentityDriver(exists: true, conflict);

		await Assert.ThrowsAsync<InvalidDataException>(() => NaturalIshalgenIdentityScenario.RunAsync(driver));

		Assert.Equal(0, driver.Creates);
		Assert.Equal(0, driver.Enters);
		Assert.Empty(driver.Selections);
	}

	[Fact]
	public void LiveAdmissionIsOneOrdinaryRetainedSubject()
	{
		string[] args = ["--run", "ni01-contract", "--output", "run/ni01-contract", "--git-sha", "test", "--scenario", "NI-01"];
		LiveBotOptions options = LiveBotOptions.Parse(args);
		ScenarioDefinition scenario = Assert.Single(options.ScenarioDefinitions);
		Assert.Equal(1, options.BotCount);
		Assert.Equal(ScenarioMode.Live, Assert.Single(scenario.Modes));
		Assert.Equal(ScenarioRace.Asmodians, scenario.Race);
		Assert.Equal(220010000, scenario.Map);
		Assert.Contains(ScenarioRequirement.Db, scenario.Requires);
		Assert.Throws<ArgumentException>(() => LiveBotOptions.Parse([.. args, "--bots", "2"]));
		Assert.Throws<ArgumentException>(() => LiveBotOptions.Parse([.. args[..^2], "--scenario", "NI-01,connect"]));
	}

	private sealed class IdentityDriver : INaturalIshalgenIdentityDriver
	{
		private readonly string? conflict;
		private readonly ushort level;
		private int selected;

		public IdentityDriver(bool exists, string? conflict = null, ushort level = 1)
		{
			Exists = exists;
			this.conflict = conflict;
			this.level = level;
		}

		public NaturalIshalgenIdentity Identity => NaturalIshalgenIdentityScenario.Identity;
		public int CharacterId { get; } = 42_001;
		public bool Exists { get; private set; }
		public int Creates { get; private set; }
		public int Logins { get; private set; }
		public int Enters { get; private set; }
		public int OnlineVerifications { get; private set; }
		public int Quits { get; private set; }
		public int ReentryWaits { get; private set; }
		public List<int> Selections { get; } = [];
		public List<string> Steps { get; } = [];

		public Task StepAsync(string action, Func<CancellationToken, Task> operation, CancellationToken token)
		{
			Steps.Add(action);
			return operation(token);
		}

		public Task<DecodedBotServerPacket> LoginAsync(CancellationToken token)
		{
			Logins++;
			return Task.FromResult(ListPacket());
		}

		public Task<DecodedBotServerPacket> CreateAsync(string characterName, PlayerClass playerClass, CancellationToken token)
		{
			Assert.False(Exists);
			Assert.Equal(Identity.CharacterName, characterName);
			Assert.Equal(PlayerClass.PRIEST, playerClass);
			Exists = true;
			Creates++;
			return Task.FromResult(Packet<SM_CREATE_CHARACTER>(new()
			{
				["responseCode"] = 0,
				["character"] = Character(),
			}));
		}

		public Task<DecodedBotServerPacket> ListAsync(CancellationToken token) => Task.FromResult(ListPacket());

		public void SelectCharacter(int characterId, string characterName)
		{
			Assert.Equal(CharacterId, characterId);
			Assert.Equal(Identity.CharacterName, characterName);
			selected = characterId;
			Selections.Add(characterId);
		}

		public Task EnterWorldAsync(CancellationToken token)
		{
			Assert.Equal(CharacterId, selected);
			Enters++;
			return Task.CompletedTask;
		}

		public Task VerifyOrdinaryOnlineIdentityAsync(int characterId, ushort level, CancellationToken token)
		{
			Assert.Equal(CharacterId, characterId);
			Assert.Equal(this.level, level);
			Assert.Equal(CharacterId, selected);
			OnlineVerifications++;
			return Task.CompletedTask;
		}

		public Task QuitAndVerifyOfflineAsync(CancellationToken token)
		{
			Assert.Equal(CharacterId, selected);
			selected = 0;
			Quits++;
			return Task.CompletedTask;
		}

		public Task WaitForReentryAsync(CancellationToken token)
		{
			Assert.Equal(0, selected);
			ReentryWaits++;
			return Task.CompletedTask;
		}

		private DecodedBotServerPacket ListPacket()
		{
			var characters = Exists
				? new List<IReadOnlyDictionary<string, object?>> { Character() }
				: [];
			if (conflict == "multiple") characters.Add(Character(CharacterId + 1));
			return Packet<SM_CHARACTER_LIST>(new()
			{
				["characterCount"] = (byte)characters.Count,
				["characters"] = characters,
			});
		}

		private IReadOnlyDictionary<string, object?> Character(int? id = null) => new Dictionary<string, object?>
		{
			["objectId"] = id ?? CharacterId,
			["name"] = conflict == "name" ? "Wrongname" : Identity.CharacterName,
			["race"] = conflict == "race" ? (int)Race.ELYOS : (int)Identity.Race,
			["playerClass"] = conflict == "class" ? (int)PlayerClass.WARRIOR : (int)Identity.PlayerClass,
			["level"] = conflict == "level" ? (ushort)10 : level,
			["deletionTimeSeconds"] = conflict == "deletion" ? 123 : 0,
		};

		private static DecodedBotServerPacket Packet<T>(Dictionary<string, object?> fields) => new(typeof(T), fields);
	}
}
