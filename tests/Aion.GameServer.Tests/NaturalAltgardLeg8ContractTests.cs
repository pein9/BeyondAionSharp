using System.Xml.Linq;
using Aion.Bots.Scenarios;
using Aion.Bots.World;
using Aion.GameServer.TestKit;

namespace Aion.GameServer.Tests;

public sealed class NaturalAltgardLeg8ContractTests
{
	private static readonly NaturalAltgardContract Leg = NaturalAltgardContract.LoadLeg("l8");
	private static readonly IReadOnlyDictionary<int, QuestRunPlan> Plans = NaturalAltgardContract.LoadPlans("l8");
	private static string Data(params string[] path) => Path.Combine([RealStaticData.RepoRoot(), "game-server", "data", "static_data", .. path]);
	private static readonly XElement Spawns = XDocument.Load(Data("spawns", "Npcs", "220030000_Altgard.xml")).Root!;
	private static float[][] Spots(int npc) => Spawns.Descendants("spawn").Where(node => (int?)node.Attribute("npc_id") == npc)
		.Elements("spot").Select(node => new[] { (float)node.Attribute("x")!, (float)node.Attribute("y")!, (float)node.Attribute("z")! }).ToArray();

	[Fact]
	public void StartMatchesLeg7AndEndpointPreservesTheHeldQuests()
	{
		NaturalAltgardContract previous = NaturalAltgardContract.LoadLeg("l7");
		Assert.Equal(previous.Start.CompletedQuestIds.Concat(previous.Endpoint.CompletedQuestIds).Order(), Leg.Start.CompletedQuestIds.Order());
		Assert.Equal(("natural-altgard-l7-contract.json", "altgard-l7", 700065, 21),
			(Leg.Start.Contract, Leg.Start.Snapshot, Leg.Start.BindNpcId, Leg.Start.Level));
		Assert.Equal(new[] { 2146, 24115, 2900, 24014, 24015 }.Order(), Leg.Start.StartedQuestIds!.Order());
		Assert.Equal([2146, 24115], Leg.Endpoint.HeldQuestIds!);
		Assert.Equal(Leg.Order.Order(), Leg.Endpoint.CompletedQuestIds.Order());
		Assert.Equal((7, "altgard-l8", 700822, 60f),
			(Leg.Order.Length, Leg.Endpoint.Snapshot, Leg.Endpoint.BindNpcId, Leg.Endpoint.Radius));
		Assert.Equal(Assert.Single(Spots(700822)), Leg.Bind!.Position);
		Assert.Equal(2035, Leg.Bind.Price);
		Assert.Equal(203657, Leg.Town!.VendorNpcId);
	}

	[Fact]
	public void ShippedQuestsSourcesScriptsAndD32PlanAreCovered()
	{
		XElement data = XDocument.Load(Data("quest_data", "quest_data.xml")).Root!;
		foreach (NaturalAltgardQuest expected in Leg.Quests)
		{
			XElement quest = data.Elements("quest").Single(node => (int?)node.Attribute("id") == expected.Id);
			Assert.Equal((expected.Category, expected.MinimumLevel, expected.RewardExperience),
				((string?)quest.Attribute("category"), (int)quest.Attribute("minlevel_permitted")!, (int)quest.Element("rewards")!.Attribute("exp")!));
			Assert.Equal(expected.Prerequisite is int before ? [before] : [],
				quest.Elements("start_conditions").Elements("finished").Select(node => (int)node.Attribute("quest_id")!).ToArray());
			if (!expected.IsTemplate) continue;
			QuestRunPlan plan = Plans[expected.Id];
			Assert.Equal((expected.Template, expected.StartNpcId), (plan.Template, Assert.Single(plan.StartNpcs).Id));
			NaturalAltgardArea area = Leg.Area(expected.Area!);
			foreach (int source in plan.Steps.Where(step => step.Kind is "kill" or "collect").SelectMany(step =>
				step.Npcs.Select(npc => npc.Id).Concat(step.Sources.Select(source => source.NpcId).OfType<int>())).Distinct())
			{
				float[][] spots = Spots(source);
				if (spots.Length > 0) Assert.Contains(spots, at => area.Contains(at[0], at[1], at[2]));
				else Assert.Contains(source, new[] { 210529, 210531 }); // Java's alternatives, never shipped on this map.
			}
		}
		Assert.Equal(Leg.Quests.Where(quest => quest.IsTemplate).Select(quest => quest.Id).Order(), Plans.Keys.Order());
		Assert.Equal(File.ReadAllText(Path.Combine(RealStaticData.RepoRoot(), "parity-artifacts/e2e/retail-quest-plans/24113.json")),
			File.ReadAllText(Path.Combine(RealStaticData.RepoRoot(), "parity-artifacts/e2e/natural-altgard-l8-plans/24113.json")));
		foreach (NaturalAltgardStep step in Leg.Steps)
		{
			string source = File.ReadAllText(Directory.GetFiles(Path.Combine(RealStaticData.RepoRoot(), "src/Aion.GameServer/Handlers/Quest/altgard"), $"_{step.QuestId}*.cs").Single());
			Assert.Contains($"{step.NpcId}", source);
			Assert.Contains(Spots(step.NpcId), at => at.SequenceEqual(step.Position));
			foreach (int page in step.Pages.Where(page => page is not (0 or 5 or 10 or 1003))) Assert.Contains($"SendQuestDialog(env, {page})", source);
		}
	}

	[Theory]
	[InlineData(0, "talk")]
	[InlineData(3, "bind")]
	[InlineData(4, "bind")]
	[InlineData(5, "bind")]
	public void DepartureMessagePrecedesTheNewBindAndIsNotRepeated(byte status, string action)
	{
		var quests = new Dictionary<int, BotQuestState>();
		if (status > 0) quests[2266] = new(2266, status, 0, 0, null);
		NaturalAltgardDecision decision = NaturalAltgardDecisionEngine.Decide(Leg,
			new NaturalAltgardObservation(true, 220030000, 21, false, quests, Leg.Start.CompletedQuestIds.ToHashSet(),
				new BotPosition(1662, 1812, 254, 0), new Dictionary<int, long>()), NaturalTemplateObjective.From(Plans), 1);
		Assert.Equal(action, decision.Action);
		if (status == 0) Assert.Equal((2266, Leg.Bind!.BeforeStep), (decision.QuestId, decision.StepKey));
	}
}
