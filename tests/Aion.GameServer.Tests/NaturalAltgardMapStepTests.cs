using Aion.Bots.Scenarios;
using Aion.Bots.World;
using Aion.GameServer.TestKit;

namespace Aion.GameServer.Tests;

public sealed class NaturalAltgardMapStepTests
{
	public static NaturalAltgardContract Proposal() => NaturalAltgardContract.Load(Path.Combine(RealStaticData.RepoRoot(),
		"parity-artifacts", "e2e", "natural-altgard-q2278-probe-contract.json"));

	[Theory]
	[InlineData(220030000, 1, "travel-to-map", 120010000, "q2278-cavalorn")]
	[InlineData(120010000, 1, "talk", 120010000, "q2278-cavalorn")]
	[InlineData(120010000, 2, "talk", 120010000, "q2278-balder")]
	[InlineData(120010000, 3, "travel-to-map", 220030000, "q2278-end")]
	[InlineData(220030000, 3, "talk", 220030000, "q2278-end")]
	public void ProposalTravelsOnceTalksOnTheCityMapAndReturnsForSuthran(int map, int var, string action, int targetMap, string key)
	{
		NaturalAltgardContract leg = Proposal();
		var state = new NaturalAltgardObservation(true, map, 21, false,
			new Dictionary<int, BotQuestState> { [2278] = new(2278, 3, var, 0, null) },
			new HashSet<int> { 2208 }, new BotPosition(1658, 1815, 254, 0), new Dictionary<int, long>(),
			Bind: new BotBindPoint(220030000, new BotPosition(leg.Bind!.Position[0], leg.Bind.Position[1], leg.Bind.Position[2], 0), 0));
		NaturalAltgardDecision next = NaturalAltgardDecisionEngine.Decide(leg, state, new Dictionary<int, NaturalTemplateObjective>(), 1);
		Assert.Equal((action, targetMap, key), (next.Action, next.MapId, next.StepKey));
	}

	[Fact]
	public void ExistingStepsKeepTheirHubMap()
	{
		NaturalAltgardContract leg = NaturalAltgardContract.LoadLeg("l6");
		Assert.All(leg.Steps, step => Assert.Equal(leg.Hub.MapId, leg.StepMap(step)));
	}
}
