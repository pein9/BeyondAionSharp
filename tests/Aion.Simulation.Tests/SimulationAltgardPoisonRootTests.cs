using Aion.Bots.Movement;
using Aion.Bots.Navigation;
using Aion.Bots.Navigation.NavMesh;
using Aion.Bots.Scenarios;
using Aion.Bots.World;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Services;

namespace Aion.Simulation.Tests;

public sealed partial class SimulationFastScenarioTests
{
	/// <summary>
	/// AM-04 (docs/natural-altgard-leveling.md): Q2213 "Poison Root, Potent Fruit" and its poison. A level 13 Cleric (GM
	/// setup, as NA-23) takes the quest from Tigg, walks to the Okaru Tree, uses it and loots the Okaru Log, which poisons
	/// it (skill 255: 20 HP every 6 s, -1 m/s, 10 minutes, not dispellable). The probe measures the drain, walks back and
	/// hands the log in; Tigg takes it and removes the poison. Aggressive monsters beside the route are despawned (GM setup:
	/// this probe is about the quest and the poison; the fights are AM-06's).
	/// </summary>
	[SkippableFact]
	public async Task AltgardPoisonRootLootsTheOkaruLogAndTiggRemovesThePoison()
	{
		Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
		const int altgard = 220030000, quest = 2213;
		using var policy = NewPolicy("AM04", includeHistory: false);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(10));
		CancellationToken token = timeout.Token;
		NaturalAltgardContract leg = NaturalAltgardContract.LoadLeg("l2");
		NaturalAltgardObjectUse tree = leg.ObjectUseList.Single(use => use.QuestId == quest);
		NaturalAltgardPoison poison = leg.PoisonList.Single(entry => entry.QuestId == quest);
		await using var session = new SimulationL0Session(fixture, policy, "b01", 141, "Asimokaru", Race.ASMODIANS);
		session.BeginStep("s00", "login-create-enter-and-setup");
		await session.LoginAndAuthenticateAsync(token);
		await session.CreateCharacterAsync(token, PlayerClass.PRIEST);
		await session.EnterWorldAsync(token);
		await session.SynchronizeAsync(token);
		Player player = fixture.World.GetPlayer(session.CharacterId);
		Assert.True(ClassChangeService.SetClass(player, PlayerClass.CLERIC, validate: false, updateDaevaStatus: true));
		player.GetCommonData().SetLevel(13);
		var instance = fixture.World.GetWorldMap(altgard).GetMainWorldMapInstance();
		BotNavigationGeometry geometry = BotNavigationGeometry.ForServerWorld(instance.GetInstanceId(), Race.ASMODIANS);
		BotTravelPlanner planner = BotTravelPlanner.For(altgard, geometry, fixture.DataManager.StaticData)
			?? throw new InvalidDataException("Altgard has no travel planner.");
		NaturalAltgardStep offer = leg.Steps.Single(step => step.Key == "q2213-offer-tigg");
		NaturalAltgardStep handIn = leg.Steps.Single(step => step.Key == poison.RemovedByStep);
		BotPosition tigg = geometry.GroundAround(altgard, new BotPosition(offer.Position[0], offer.Position[1], offer.Position[2], 0), [2f, 3f]).First();
		await TeleportForSetupAsync(session, player, altgard, tigg.X, tigg.Y, tigg.Z, token);
		await session.SynchronizeAsync(token);

		session.BeginStep("s01", offer.Key);
		Console.WriteLine($"AM-04 {await NaturalAltgardQuestSteps.TalkAsync(session, offer, await session.WaitForNpcAsync(offer.NpcId, token), token)}");

		session.BeginStep("s02", "walk-to-the-okaru-tree");
		BotPosition treeGround = geometry.GroundAround(altgard, new BotPosition(1412.96f, 1441.74f, 282.495f, 0), [1.5f, 2f, 2.5f])
			.First(point => leg.Area(tree.Area!).Contains(point.X, point.Y, point.Z));
		await WalkClearAsync(planner.PlanJourney(altgard, session.CurrentPosition, treeGround, 13, [])?.Route
			?? geometry.FindJourneyPath(altgard, session.CurrentPosition, treeGround));

		session.BeginStep("s03", "use-the-okaru-tree-and-loot-the-log");
		int hpBefore = session.Api.World.CurrentHp;
		bool looted = await NaturalAltgardQuestSteps.UseObjectAsync(session, await session.WaitForNpcAsync(tree.NpcId, token), tree.LootItemId, token);
		await session.SynchronizeAsync(token);
		Assert.True(looted, "The Okaru Tree gave no log.");
		Assert.Contains(session.Api.World.Inventory.Values, item => item.ItemId == tree.LootItemId);
		Assert.Equal(((byte)3, tree.ToVar), NaturalAltgardQuestSteps.State(session.Api.World, quest));
		Assert.True(NaturalAltgardQuestSteps.HasEffect(session.Api.World, poison.SkillId), "The client sees no Okaru Poison on the bot.");

		// The drain, standing still for 30 s: 20 HP per 6 s tick against natural regeneration.
		int hpPoisoned = session.Api.World.CurrentHp;
		await session.AdvanceAsync(TimeSpan.FromSeconds(30), token);
		await session.SynchronizeAsync(token);
		int hpAfter = session.Api.World.CurrentHp;
		Console.WriteLine($"AM-04 poisoned: HP {hpBefore} -> {hpPoisoned} at the loot -> {hpAfter} after 30 s of {session.Api.World.MaxHp}; " +
			$"server effect remaining {player.GetEffectController().GetAbnormalEffect("Q_OKARUTREE_POISON")?.GetRemainingTimeMillis()} ms");
		Assert.True(player.GetEffectController().HasAbnormalEffect(poison.SkillId));

		session.BeginStep("s04", "walk-back-to-tigg");
		BotPosition back = geometry.GroundAround(altgard, new BotPosition(handIn.Position[0], handIn.Position[1], handIn.Position[2], 0), [2f, 3f]).First();
		await WalkClearAsync(planner.PlanJourney(altgard, session.CurrentPosition, back, 13, [])?.Route
			?? geometry.FindJourneyPath(altgard, session.CurrentPosition, back));

		session.BeginStep("s05", handIn.Key);
		Console.WriteLine($"AM-04 {await NaturalAltgardQuestSteps.TalkAsync(session, handIn, await session.WaitForNpcAsync(handIn.NpcId, token), token)}");
		await session.SynchronizeAsync(token);
		Assert.Contains(quest, session.Api.World.CompletedQuestIds);
		Assert.DoesNotContain(session.Api.World.Inventory.Values, item => item.ItemId == tree.LootItemId);
		Assert.False(player.GetEffectController().HasAbnormalEffect(poison.SkillId), "Tigg did not remove the poison.");
		Assert.False(NaturalAltgardQuestSteps.HasEffect(session.Api.World, poison.SkillId), "The client still sees the poison.");
		Assert.False(player.IsDead());
		policy.AssertClean();

		async Task WalkClearAsync(IReadOnlyList<BotPosition> route)
		{
			Assert.NotEmpty(route);
			foreach (var npc in instance.GetNpcs().Where(npc =>
				NaturalHostility.IsAggressive(npc.GetObjectTemplate(), fixture.DataManager.StaticData.TribeRelations, TribeClass.PC_DARK) &&
				route.Any(point => MathF.Pow(npc.GetX() - point.X, 2) + MathF.Pow(npc.GetY() - point.Y, 2) <= 30 * 30)).ToArray())
				fixture.World.Despawn(npc);
			float speed = session.Api.World.MovementSpeed ?? throw new InvalidOperationException("No movement speed.");
			await session.ExecuteMovementAsync(new BotMover(session.Api.World, session.Api.Timing).CreateGroundPlan(route, session.CurrentPosition, speed), token);
			await session.SynchronizeAsync(token);
		}
	}
}
