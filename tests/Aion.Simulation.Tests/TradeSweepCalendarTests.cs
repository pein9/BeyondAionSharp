using Aion.GameServer.Utils.Time.Gametime;

namespace Aion.Simulation.Tests;

public sealed class TradeSweepCalendarTests
{
	[Theory]
	[InlineData("0.15.03", 0, 0, 3, 15, 0)]
	[InlineData("0.15.03", 109440, 1, 3, 15, 0)]
	[InlineData("4.*.*", 0, 0, 1, 1, 4)]
	[InlineData("/2./3.*", 0, 0, 1, 3, 0)]
	public void SelectsFutureShippedBoundaryUsingAionsCalendar(string expression, int current, int year, int month, int day, int hour)
	{
		int selected = SimulationFastScenarioTests.NextTradeSweepGameTime(expression, current);
		Assert.True(selected > current);
		var time = new GameTime(selected);
		Assert.Equal(year, time.GetYear()); Assert.Equal(month, time.GetMonth());
		Assert.Equal(day, time.GetDay()); Assert.Equal(hour, time.GetHour()); Assert.Equal(0, time.GetMinute());
	}

	[Fact]
	public void ImpossibleBoundariesFailRatherThanInventingAContentDate()
	{
		Assert.Throws<InvalidDataException>(() => SimulationFastScenarioTests.NextTradeSweepGameTime("0.32.01", 0));
		Assert.Throws<InvalidDataException>(() => SimulationFastScenarioTests.NextTradeSweepGameTime("0.1", 0));
	}
}
