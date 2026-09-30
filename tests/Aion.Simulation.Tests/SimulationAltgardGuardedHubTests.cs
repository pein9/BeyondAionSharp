using Aion.Bots.Movement;
using Aion.Bots.Navigation;
using Aion.Bots.Navigation.NavMesh;
using Aion.Bots.Protocol;
using Aion.Bots.Scenarios;
using Aion.Bots.World;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;

namespace Aion.Simulation.Tests;

public sealed partial class SimulationFastScenarioTests
{
	/// <summary>
	/// AM-03 (docs/natural-altgard-leveling.md): talking at Moslan Crossroad with its grove pluma alive. A level 13 Cleric
	/// (GM setup: level and Daeva, as NA-23) takes Q2210 from Rion in the fortress, travels to a staging point 30 m short of
	/// the crossroad, and from there picks each talk spot with <see cref="NaturalGuardedTalkPolicy"/> from what the client sees,
	/// walking to it on a route that keeps out of every aggro circle. It hands Q2210 in to Loriniah and takes Q2211 from
	/// Olenja. No monster may attack the bot from the staging point to the end.
	/// </summary>
	[SkippableFact]
	public async Task AltgardMoslanCrossroadTalksWithThePlumaAlive()
	{
		Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
		const int altgard = 220030000, report = 2210, karnifs = 2211;
		using var policy = NewPolicy("AM03", includeHistory: false);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(10));
		CancellationToken token = timeout.Token;
		NaturalAltgardContract leg = NaturalAltgardContract.LoadLeg("l2");
		IReadOnlyDictionary<int, QuestRunPlan> plans = NaturalAltgardContract.LoadPlans("l2");
		await using var session = new SimulationL0Session(fixture, policy, "b01", 140, "Asimguarded", Race.ASMODIANS);
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
		QuestRunPosition rion = Assert.Single(Assert.Single(plans[report].StartNpcs).Positions);
		// Rion walks a route through the fortress: in a long shared SIM world he can be out of view of his spawn point
		// (AC-08), so the setup teleport goes beside where the server has him now.
		var rionNow = instance.GetNpcs().FirstOrDefault(npc => npc.GetNpcId() == 203603);
		BotPosition rionAt = rionNow != null ? new BotPosition(rionNow.GetX(), rionNow.GetY(), rionNow.GetZ(), 0) : new BotPosition(rion.X, rion.Y, rion.Z, 0);
		BotPosition nearRion = geometry.GroundAround(altgard, rionAt, [2.5f, 3.5f, 5f]).First();
		await TeleportForSetupAsync(session, player, altgard, nearRion.X, nearRion.Y, nearRion.Z, token);
		await session.SynchronizeAsync(token);
		Assert.Equal(13, session.Api.World.Level);

		session.BeginStep("s01", "take-q2210-from-rion");
		// Talk to Rion where the client sees him now: in a long shared SIM world he need not be at his spawn point.
		int rionObject = await session.WaitForNpcAsync(203603, token);
		for (int attempt = 1; ; attempt++)
		{
			BotPosition seen = session.Api.World.Objects[rionObject].Position;
			if (NaturalGuardedTalkPolicy.Distance(session.CurrentPosition, seen) > 4)
				await WalkAsync(geometry.FindInteractionPath(altgard, session.CurrentPosition, seen));
			try { await session.StartQuestAsync(rionObject, report, token); break; }
			catch (NaturalDialogTooFarException) when (attempt < 3) { await session.SynchronizeAsync(token); }
		}

		session.BeginStep("s02", "travel-to-the-crossroad-staging-point");
		BotPosition staging = geometry.GroundAround(altgard, new BotPosition(leg.Hub.Anchor[0], leg.Hub.Anchor[1] + 30, leg.Hub.Anchor[2], 0), [0f, 2f, 4f])
			.First();
		await WalkAsync(planner.PlanJourney(altgard, session.CurrentPosition, staging, 13, [])?.Route
			?? geometry.FindJourneyPath(altgard, session.CurrentPosition, staging));
		int watchFrom = session.PacketHistory.Count;

		foreach ((string key, int npcId, int quest, bool handIn) in new[] { ("hand-in-q2210-to-loriniah", 203605, report, true), ("take-q2211-from-olenja", 203606, karnifs, false) })
		{
			session.BeginStep($"s0{(handIn ? 3 : 4)}", key);
			int npcObject = await session.WaitForNpcAsync(npcId, token);
			BotPosition npc = session.Api.World.Objects[npcObject].Position;
			NaturalTalkHostile[] hostiles = session.Api.World.Objects.Values
				.Where(known => known.Kind == BotKnownObjectKind.Npc && known.TemplateId is int id && !known.IsCorpse &&
					NaturalHostility.IsAggressive(fixture.DataManager.StaticData.NpcDataDh.GetNpcTemplate(id),
						fixture.DataManager.StaticData.TribeRelations, TribeClass.PC_DARK) &&
					NaturalGuardedTalkPolicy.Distance(known.Position, npc) < 60)
				.Select(known => new NaturalTalkHostile(known.ObjectId, known.TemplateId!.Value, known.SettledPosition,
					NaturalHostility.AggroRadius(fixture.DataManager.StaticData.NpcDataDh.GetNpcTemplate(known.TemplateId.Value),
						fixture.DataManager.StaticData.TribeRelations, TribeClass.PC_DARK))).ToArray();
			IReadOnlyList<BotPosition> spots = geometry.GroundAround(altgard, npc, [1.5f, 2.5f, 3.5f, 4.5f], sectors: 16);
			NaturalGuardedTalkDecision decision = NaturalGuardedTalkPolicy.Decide(npc, 6, hostiles, spots);
			Console.WriteLine($"AM-03 {key}: {hostiles.Length} aggressive in view within 60 m " +
				$"({string.Join(", ", hostiles.Select(h => $"{h.NpcId}@{NaturalGuardedTalkPolicy.Distance(h.Position, npc):F0}m/r{h.AggroRadius}"))}); {decision.Action}: {decision.Reason}");
			Assert.Equal("talk", decision.Action);
			BotNavigationHazard[] circles = hostiles.Select(h => new BotNavigationHazard(h.Position, h.AggroRadius + NaturalGuardedTalkPolicy.Margin)).ToArray();
			IReadOnlyList<BotPosition> route = geometry.FindJourneyPathAvoiding(altgard, session.CurrentPosition, decision.Spot!.Value, circles);
			Assert.True(route.Count > 0, $"{key}: no route that keeps out of the circles ({BotNavMeshRouter.LastOutcome})");
			await WalkAsync(route);
			if (handIn)
				await session.FinishQuestAsync(npcObject, quest, token);
			else
				await session.StartQuestAsync(npcObject, quest, token);
		}
		await session.SynchronizeAsync(token);
		Assert.Contains(report, session.Api.World.CompletedQuestIds);
		Assert.Equal(3, session.Api.World.Quests[karnifs].Status);
		DecodedBotServerPacket[] attacks = session.PacketHistory.Skip(watchFrom)
			.Where(packet => packet.PacketType == typeof(SM_ATTACK) && packet.Get<int>("targetObjId") == session.CharacterId).ToArray();
		Assert.True(attacks.Length == 0, $"{attacks.Length} attacks reached the bot at the crossroad.");
		Assert.False(player.IsDead());
		policy.AssertClean();

		async Task WalkAsync(IReadOnlyList<BotPosition> route)
		{
			Assert.NotEmpty(route);
			float speed = session.Api.World.MovementSpeed ?? throw new InvalidOperationException("No movement speed.");
			await session.ExecuteMovementAsync(new BotMover(session.Api.World, session.Api.Timing).CreateGroundPlan(route, session.CurrentPosition, speed), token);
			await session.SynchronizeAsync(token);
		}
	}
}
