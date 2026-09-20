using Aion.Bots.Transport;
using Aion.LiveBots;

namespace Aion.GameServer.Tests;

public sealed class BotIdentityTests
{
	[Theory]
	[InlineData(50)]
	[InlineData(200)]
	[InlineData(500)]
	[InlineData(1000)]
	public void EntirePopulationHasUniqueValidCharacterNamesAndMacAddresses(int population)
	{
		var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var addresses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
		{
			BotIdentity.MacAddress(BotIdentity.MacBytes(1, director: true)),
		};
		foreach (int index in Enumerable.Range(1, population))
		{
			string name = BotIdentity.CharacterName(index);
			Assert.Matches("^[A-Z][a-z]{1,15}$", name);
			Assert.True(names.Add(name), $"Duplicate name for bot {index}.");
			byte[] bytes = BotIdentity.MacBytes(index);
			Assert.Equal(6, bytes.Length);
			Assert.Equal(2, bytes[0]); // locally administered unicast address
			string address = BotIdentity.MacAddress(bytes);
			Assert.Matches("^([0-9A-F]{2}-){5}[0-9A-F]{2}$", address);
			Assert.True(addresses.Add(address), $"MAC collision for bot {index}, including the director.");
			Assert.Equal(index, BotIdentity.ParseSubjectNumber($"b{index:D2}"));
		}
		Assert.Equal(population, names.Count);
		Assert.Equal(population + 1, addresses.Count);
	}

	[Fact]
	public void LegacySmallRunIdentitiesRemainStable()
	{
		Assert.Equal("Aeliveaa", BotIdentity.CharacterName(1));
		Assert.Equal("Aelivedu", BotIdentity.CharacterName(99));
		Assert.Equal("Aelivezz", BotIdentity.CharacterName(676));
		Assert.Equal("Aelivebaa", BotIdentity.CharacterName(677));
		Assert.Equal("02-00-00-00-00-01", BotIdentity.MacAddress(BotIdentity.MacBytes(1)));
		Assert.Equal("02-00-00-00-00-FE", BotIdentity.MacAddress(BotIdentity.MacBytes(1, director: true)));
		Assert.Equal("02-00-00-00-00-FF", BotIdentity.MacAddress(BotIdentity.MacBytes(254)));
		Assert.Equal("02-00-00-00-01-00", BotIdentity.MacAddress(BotIdentity.MacBytes(255)));
	}

	[Theory]
	[InlineData(50)]
	[InlineData(200)]
	[InlineData(500)]
	[InlineData(1000)]
	public void LiveOptionsAcceptCapacityPopulation(int population)
	{
		LiveBotOptions options = LiveBotOptions.Parse([
			"--run", "capacity-options", "--output", Path.GetTempPath(), "--git-sha", "test",
			"--bots", population.ToString(System.Globalization.CultureInfo.InvariantCulture)]);
		Assert.Equal(population, options.BotCount);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	[InlineData(1001)]
	public void OutOfContractPopulationsAreRejected(int population)
	{
		Assert.Throws<ArgumentOutOfRangeException>(() => BotIdentity.CharacterName(population));
		Assert.Throws<ArgumentOutOfRangeException>(() => BotIdentity.MacBytes(population));
		Assert.Throws<ArgumentException>(() => LiveBotOptions.Parse([
			"--run", "capacity-options", "--output", Path.GetTempPath(), "--git-sha", "test",
			"--bots", population.ToString(System.Globalization.CultureInfo.InvariantCulture)]));
	}
}
