using Aion.Bots.World;

namespace Aion.Bots.Scenarios;

public enum NaturalTravelChoice { Walk, Return, FlightToAnturoon, FlightToAldelle }

/// <summary>Compare actual 4.8 flight durations and the observed bind/cooldown with a
/// conservative lower bound for walking. Unknown fares or unavailable skills stay walking.</summary>
public static class NaturalJourneyTravelPolicy
{
	private const float WalkingMetersPerSecond = 6f;
	private const float InteractionOverheadSeconds = 15f;
	private const float MinimumSavingSeconds = 30f;
	public static readonly BotPosition AldelleDeparture = new(526.66f, 2449.63f, 281.74f, 0);
	public static readonly BotPosition AnturoonArrival = new(936.85f, 1707.28f, 258.9f, 0);
	public static readonly BotPosition AnturoonDeparture = new(938.1f, 1710.94f, 258.81f, 0);
	public static readonly BotPosition AldelleArrival = new(526.25f, 2449.65f, 281.82f, 0);

	public static NaturalTravelChoice Choose(BotPosition origin, BotPosition destination,
		BotBindPoint? bind, bool returnReady, bool flightFareAffordable)
	{
		float walk = Seconds(origin, destination);
		var choices = new List<(NaturalTravelChoice Choice, float Seconds)>
		{
			(NaturalTravelChoice.Walk, walk),
		};
		if (bind is { MapId: 220010000 } && returnReady &&
			NaturalIshalgenHubPolicy.Distance(origin, bind.Position) > 30)
			choices.Add((NaturalTravelChoice.Return,
				Seconds(bind.Position, destination) + 6 + InteractionOverheadSeconds));
		if (flightFareAffordable)
		{
			choices.Add((NaturalTravelChoice.FlightToAnturoon,
				Seconds(origin, AldelleDeparture) + 42 +
				Seconds(AnturoonArrival, destination) + InteractionOverheadSeconds));
			choices.Add((NaturalTravelChoice.FlightToAldelle,
				Seconds(origin, AnturoonDeparture) + 35 +
				Seconds(AldelleArrival, destination) + InteractionOverheadSeconds));
		}
		(NaturalTravelChoice choice, float seconds) = choices.MinBy(option => option.Seconds);
		return walk - seconds >= MinimumSavingSeconds ? choice : NaturalTravelChoice.Walk;
	}

	private static float Seconds(BotPosition a, BotPosition b) =>
		NaturalIshalgenHubPolicy.Distance(a, b) / WalkingMetersPerSecond;
}
