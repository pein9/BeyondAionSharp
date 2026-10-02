using System.Xml.Linq;
using Aion.Bots.Movement;
using Aion.Bots.Navigation;
using Aion.Bots.Navigation.NavMesh;
using Aion.Bots.Protocol;
using Aion.Bots.Scenarios;
using Aion.Bots.World;
using Aion.GameServer.Model;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;
using Aion.GameServer.TestKit;

namespace Aion.Simulation.Tests;

public sealed partial class SimulationFastScenarioTests
{
	/// <summary>BC-02: level-22 routes, Heart flight, every Black Claw target kind/totem, real Bregirun portal and city trip. Free probe 213 only.</summary>
	[SkippableFact]
	public async Task BlackClawTravelWalksTheGroundsEntersBregirunAndVisitsVidar()
	{
		Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
		const int altgard = 220030000, level = 22;
		using var policy = NewPolicy("BC02", includeHistory: false);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(20));
		CancellationToken token = timeout.Token;
		NaturalAltgardContract leg = NaturalAltgardContract.LoadLeg("l10");
		await using var session = new SimulationL0Session(fixture, policy, "b01", 213, "Asimclawroute", Race.ASMODIANS);
		session.BeginStep("s00", "setup-route-probe");
		await session.LoginAndAuthenticateAsync(token);
		await session.CreateCharacterAsync(token, PlayerClass.PRIEST);
		await session.EnterWorldAsync(token);
		await session.SynchronizeAsync(token);
		var player = fixture.World.GetPlayer(session.CharacterId);
		Assert.True(ClassChangeService.SetClass(player, PlayerClass.CLERIC, validate: false, updateDaevaStatus: true));
		player.GetCommonData().SetLevel(level);
		SkillLearnService.LearnNewSkills(player, 1, level);
		player.GetInventory().IncreaseKinah(5000);
		var spawns = XDocument.Load(Path.Combine(RealStaticData.RepoRoot(), "game-server/data/static_data/spawns/Npcs/220030000_Altgard.xml")).Root!;
		BotPosition[] Spots(int npc) => spawns.Descendants("spawn").Where(n => (int?)n.Attribute("npc_id") == npc).Elements("spot")
			.Select(n => new BotPosition((float)n.Attribute("x")!, (float)n.Attribute("y")!, (float)n.Attribute("z")!, 0)).ToArray();
		BotNavigationGeometry Geometry() => BotNavigationGeometry.ForServerWorld(player.GetInstanceId(), Race.ASMODIANS);
		BotPosition Ground(BotPosition at) => Geometry().GroundAround(altgard, at, [2f, 3f, 5f, 8f, 12f]).First();
		BotPosition Nearest(int npc)
		{
			if (npc < 210000 || npc >= 700000) return Ground(Spots(npc).MinBy(at => NaturalFlightPolicy.Distance(at, session.CurrentPosition)));
			var candidates = Spots(npc).SelectMany(at => Geometry().GroundAround(altgard, at, [2f, 3f, 5f, 8f, 12f, 16f, 20f])
				.Select(point => new { At = point, Range = NaturalFlightPolicy.Distance(point, at), Sight = Geometry().HasLineOfSight(altgard, point, at),
					Connected = Geometry().OnSameIsland(altgard, session.CurrentPosition, point) })).ToArray();
			var usable = candidates.Where(c => c.Range <= 20 && c.Sight && c.Connected).ToArray();
			if (usable.Length > 0) return usable.MinBy(c => NaturalFlightPolicy.Distance(c.At, session.CurrentPosition))!.At;
			Console.WriteLine($"BC-02 {npc}: no mesh firing point; {candidates.Length} ring points, " +
				$"{candidates.Count(c => c.Range <= 20)} in range, {candidates.Count(c => c.Sight)} sighted, {candidates.Count(c => c.Connected)} connected; " +
				string.Join("; ", candidates.OrderBy(c => c.Range).Take(3).Select(c => c.ToString())));
			foreach (BotPosition target in Spots(npc).OrderBy(at => NaturalFlightPolicy.Distance(at, session.CurrentPosition)))
			{
				IReadOnlyList<BotPosition> route = Geometry().FindRangedApproachPath(altgard, session.CurrentPosition, target, []);
				if (route.Count > 0) return route[^1];
			}
			throw new InvalidDataException($"Source {npc}: no connected, sighted spell-range ground or checked ranged route.");
		}
		int sequence = 0, walked = 0;
		IReadOnlyList<BotPosition> TalkRoute(int map, BotPosition from, BotPosition to, float range)
		{
			if (range <= 3) return Geometry().FindInteractionPath(map, from, to);
			// Java NpcController/PositionUtil checks talk distance, without a spell's line-of-sight condition.
			foreach (BotPosition at in Geometry().GroundAround(map, to, [range - 1, range - 2])
				.Where(at => NaturalFlightPolicy.Distance(at, to) <= range - 0.5f)
				.OrderBy(at => NaturalFlightPolicy.Distance(from, at)))
			{
				IReadOnlyList<BotPosition> route = Geometry().FindJourneyPath(map, from, at);
				if (route.Count > 0) return route;
			}
			return [];
		}
		async Task WalkAsync(string name, BotPosition to, bool interaction = false, float talkRange = 3)
		{
			session.BeginStep($"s{++sequence:00}", name);
			int map = player.GetWorldId();
			BotPosition from = session.CurrentPosition;
			BotNavigationGeometry geometry = Geometry();
			BotTravelPlan? plan = interaction ? null : BotTravelPlanner.For(map, geometry, fixture.DataManager.StaticData)?.PlanJourney(map, from, to, level, []);
			IReadOnlyList<BotPosition> route = interaction ? TalkRoute(map, from, to, talkRange) : plan?.Route ?? geometry.FindJourneyPath(map, from, to);
			Assert.True(route.Count > 0 || interaction && NaturalFlightPolicy.Distance(from, to) <= talkRange,
				$"{name}: no checked route on {map} ({BotNavMeshRouter.LastOutcome}); {from} -> {to}");
			var hostiles = player.GetPosition().GetWorldMapInstance().GetNpcs().Where(npc => !npc.IsDead() &&
				NaturalHostility.IsAggressive(npc.GetObjectTemplate(), fixture.DataManager.StaticData.TribeRelations, TribeClass.PC_DARK) &&
				route.Any(at => MathF.Pow(npc.GetX() - at.X, 2) + MathF.Pow(npc.GetY() - at.Y, 2) <= 900)).ToArray();
			string hazards = string.Join(", ", hostiles.GroupBy(n => n.GetNpcId()).Select(g => $"{g.Key}x{g.Count()}"));
			foreach (var npc in hostiles) fixture.World.Despawn(npc);
			if (route.Count > 0)
				await session.ExecuteMovementAsync(new BotMover(session.Api.World, session.Api.Timing).CreateGroundPlan(route, from,
					session.Api.World.MovementSpeed!.Value), token);
			await session.SynchronizeAsync(token);
			float miss = NaturalFlightPolicy.Distance(new BotPosition(player.GetX(), player.GetY(), player.GetZ(), 0), to);
			Assert.True(miss <= (interaction ? talkRange : 5), $"{name}: endpoint miss {miss:F1} m");
			Assert.False(player.IsDead());
			walked++;
			Console.WriteLine($"BC-02 {name}: map {map}, {route.Count} checked points; miss {miss:F1}; aggressive within 30 m [{hazards}]");
		}
		async Task PortalAsync(int npcId, int duration, int destination)
		{
			session.BeginStep($"s{++sequence:00}", $"portal-{npcId}");
			int npc = await session.WaitForNpcAsync(npcId, token);
			await NaturalDialogProtocol.OpenAsync(session, npc, token);
			var use = await session.WaitForPacketAsync(typeof(SM_USE_OBJECT), token,
				p => p.Get<int>("targetObjectId") == npc && p.Get<byte>("actionType") == 1);
			Assert.Equal(duration, use.Get<int>("durationMs"));
			session.Api.World.BeginWorldReload();
			await session.AdvanceAsync(TimeSpan.FromMilliseconds(duration + 1), token);
			await session.WaitForPacketAsync(typeof(SM_PLAYER_SPAWN), token, p => p.Get<int>("worldId") == destination);
			await session.WaitForPacketAsync(typeof(SM_PLAYER_INFO), token, p => p.Get<int>("objectId") == session.CharacterId);
			session.AcceptTeleportPosition();
			await session.SynchronizeAsync(token);
			Assert.Equal(destination, player.GetWorldId());
			Console.WriteLine($"BC-02 portal {npcId}: {duration} ms use; entered {destination} instance {player.GetInstanceId()} at {session.CurrentPosition}");
		}
		BotPosition heart = Ground(Spots(700067).Single());
		session.Api.World.BeginWorldReload();
		await TeleportForSetupAsync(session, player, altgard, heart.X, heart.Y, heart.Z, token);
		session.AcceptTeleportPosition();
		await session.SynchronizeAsync(token);
		var flight = Assert.Single(Assert.IsType<NaturalAirlineJourney>(NaturalAirlineRoutes.Journey(
			NaturalAirlineRoutes.Load(RealStaticData.RepoRoot()), altgard, session.CurrentPosition, Ground(Spots(700065).Single()))).Flights);
		Assert.Equal(205258, flight.NpcId);
		await WalkAsync("heart-transporter", Ground(flight.Departure));
		int transporter = await session.WaitForNpcAsync(flight.NpcId, token);
		var flown = await new NaturalServiceSteps(session).FlyAsync(transporter, session.Api.World.Objects[transporter].Position, 6, flight, token);
		Assert.True(flown.IsDone, flown.Reason);
		Console.WriteLine($"BC-02 {flown.Reason}");
		await WalkAsync("fortress-bind", Nearest(700065));
		int stone = await session.WaitForNpcAsync(700065, token);
		Assert.True((await new NaturalServiceSteps(session).BindAsync(stone, session.Api.World.Objects[stone].Position,
			altgard, leg.Bind!.Price, leg.Bind.AcceptRange, token)).IsDone);
		foreach (int npc in new[] { 203556, 203560, 203558, 203557, 203579, 203665, 203669, 203668 })
			await WalkAsync($"giver-{npc}", Nearest(npc));
		foreach (int npc in new[] { 210498, 210499, 210551, 210552, 210508, 210509, 210560, 210561, 211283, 210533, 210534,
			211279, 210535, 210536, 211280, 210538, 210562, 216893, 210751 })
		{
			await WalkAsync($"source-{npc}", Nearest(npc));
			float range = Spots(npc).Min(at => NaturalFlightPolicy.Distance(session.CurrentPosition, at));
			Assert.True(range <= 22, $"Source {npc} is not in ordinary spell range ({range:F1}).");
			bool sight = Spots(npc).Any(at => NaturalFlightPolicy.Distance(session.CurrentPosition, at) <= 22 &&
				Geometry().HasLineOfSight(altgard, session.CurrentPosition, at));
			Console.WriteLine($"BC-02 source {npc}: firing distance {range:F1} m, endpoint sight {sight}");
		}
		NaturalAltgardZoneStep zone = Assert.Single(leg.ZoneStepList);
		await WalkAsync("totem-sensory-zone", new BotPosition(zone.Anchor![0], zone.Anchor[1], zone.Anchor[2], 0));
		BotPosition[] totems = Spots(700099);
		Assert.Equal(10, totems.Length);
		for (int i = 0; i < totems.Length; i++) await WalkAsync($"totem-{i + 1}", Ground(totems[i]));
		NaturalAltgardInstanceTrip trip = Assert.Single(leg.InstanceTripList);
		await WalkAsync("black-claw-to-suthran", Nearest(203557));
		QuestState? quest = player.GetQuestStateList().GetQuestState(trip.QuestId);
		if (quest == null)
		{
			quest = new QuestState(trip.QuestId, QuestStatus.START);
			Assert.True(player.GetQuestStateList().AddQuest(trip.QuestId, quest));
		}
		quest.SetStatus(QuestStatus.START);
		quest.SetQuestVar(0); // Controlled campaign gate setup; BC-04 proves its prerequisite unlock.
		Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, new SM_QUEST_LIST([quest]));
		await session.SynchronizeAsync(token);
		int suthran = await session.WaitForNpcAsync(203557, token);
		await NaturalDialogProtocol.OpenAsync(session, suthran, token);
		await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token, p => p.Get<int>("targetObjectId") == suthran);
		await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(suthran, DialogAction.QUEST_SELECT, questId: trip.QuestId), token);
		await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token, p => p.Get<ushort>("dialogPageId") == 1693);
		await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(suthran, DialogAction.SELECT3_1_1, questId: trip.QuestId), token);
		await NaturalMovieGate.FinishAsync(session, token);
		await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token, p => p.Get<ushort>("dialogPageId") == 1695);
		session.Api.World.BeginWorldReload();
		await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(suthran, DialogAction.SETPRO1, questId: trip.QuestId), token);
		await session.WaitForPacketAsync(typeof(SM_CHANNEL_INFO), token);
		await session.WaitForPacketAsync(typeof(SM_PLAYER_INFO), token, p => p.Get<int>("objectId") == session.CharacterId);
		session.AcceptTeleportPosition();
		await session.SynchronizeAsync(token);
		Assert.Equal(trip.FromVar, quest.GetQuestVarById(0));
		Console.WriteLine($"BC-02 Suthran's ordinary quest teleport reached {session.CurrentPosition}");
		await WalkAsync("source-210539-on-gate-ground", Nearest(210539));
		await WalkAsync("bregirun-portal", new(trip.PortalPosition[0], trip.PortalPosition[1], trip.PortalPosition[2], 0), interaction: true);
		await PortalAsync(trip.PortalNpcId, trip.UseMillis, trip.MapId);
		Assert.Equal(trip.EnterVar, quest.GetQuestVarById(0));
		XElement instanceSpawns = XDocument.Load(Path.Combine(RealStaticData.RepoRoot(),
			"game-server/data/static_data/spawns/Instances/320030000_Bregirun.xml")).Root!;
		foreach (int npcId in new[] { 211625, 211624, 700140, 700141 })
		{
			// The generated Bregirun mesh and every emitted edge pass the server geometry checks.
			BotPosition at = instanceSpawns.Descendants("spawn").Where(n => (int?)n.Attribute("npc_id") == npcId).Elements("spot")
				.Select(n => new BotPosition((float)n.Attribute("x")!, (float)n.Attribute("y")!, (float)n.Attribute("z")!, 0))
				.Where(p => npcId >= 700000 || NaturalFlightPolicy.Distance(p, session.CurrentPosition) >= 6)
				.MinBy(p => NaturalFlightPolicy.Distance(p, session.CurrentPosition));
			await WalkAsync($"instance-{npcId}", at, interaction: true);
		}
		await WalkAsync("gate-back-to-guardian", new(261.418f, 229.531f, 213.918f, 0), interaction: true);
		await WalkAsync("guardian-back-to-entry", new(trip.Arrival[0], trip.Arrival[1], trip.Arrival[2], 0));
		float exitRange = player.GetPosition().GetWorldMapInstance().GetNpcs(trip.ExitNpcId).Single().GetObjectTemplate().GetTalkDistance();
		Assert.Equal(7, exitRange);
		await WalkAsync("dimension-exit", new(trip.ExitPosition[0], trip.ExitPosition[1], trip.ExitPosition[2], 0), interaction: true, talkRange: exitRange);
		await PortalAsync(trip.ExitNpcId, trip.ExitUseMillis, altgard);
		Assert.Equal(trip.ResetVar, quest.GetQuestVarById(0));
		await ReturnAsync();
		// The second city Return respects the shipped 1,200-second reuse (skill 243 cooldown 12000).
		await session.AdvanceAsync(TimeSpan.FromSeconds(1201), token);
		await session.SynchronizeAsync(token);
		await WalkAsync("return-to-fortress", Nearest(700065));
		NaturalAltgardMapTrip city = Assert.Single(leg.MapTripList);
		await WalkAsync("city-teleporter", Nearest(city.TeleporterNpcId));
		int teleporter = await session.WaitForNpcAsync(city.TeleporterNpcId, token);
		var travelled = await new NaturalServiceSteps(session).TeleportAsync(teleporter, session.Api.World.Objects[teleporter].Position,
			city.TalkRange, city.LocationId, city.Fare, city.MapId, token);
		Assert.True(travelled.IsDone, travelled.Reason);
		Console.WriteLine($"BC-02 {travelled.Reason}");
		QuestRunPosition vidar = NaturalAltgardContract.LoadPlans("l10")[2283].EndNpcs.Single().Positions.Single(p => !p.ConditionalEvent);
		float vidarRange = player.GetPosition().GetWorldMapInstance().GetNpcs(204052).Single().GetObjectTemplate().GetTalkDistance();
		await WalkAsync("vidar", new(vidar.X, vidar.Y, vidar.Z, 0), interaction: true, talkRange: vidarRange);
		int recipient = await session.WaitForNpcAsync(204052, token);
		await NaturalDialogProtocol.OpenAsync(session, recipient, token);
		await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token, p => p.Get<int>("targetObjectId") == recipient);
		await session.SendPacketAsync(session.Api.CloseDialog(recipient), token);
		await ReturnAsync();
		Assert.Equal(altgard, player.GetWorldId());
		await WalkAsync("return-to-fortress-endpoint", Nearest(700065));
		Console.WriteLine($"BC-02 {walked} ground legs, one hub flight, both ordinary instance portals and the city trip/Return; endpoint {session.CurrentPosition}");
		policy.AssertClean();

		async Task ReturnAsync()
		{
			BotSkill learned = session.Api.World.Skills[243];
			await session.SendPacketAsync(session.Api.Target(session.CharacterId), token);
			await session.SendPacketAsync(session.Api.Cast(new SpellCastData(243, checked((byte)learned.Level), 0) { TargetObjectId = session.CharacterId }), token);
			var started = await BotCastProtocol.WaitForStartAsync((predicate, waitToken) => session.WaitForPacketAsync(
				p => predicate(p) || BotCastProtocol.IsStartRejection(p), waitToken), session.CharacterId, 243, token);
			Assert.Equal(typeof(SM_CASTSPELL), started.PacketType);
			session.Api.World.BeginWorldReload();
			await session.AdvanceAsync(TimeSpan.FromMilliseconds(started.Get<ushort>("castDuration") + 1), token);
			var result = await BotCastProtocol.WaitForCompletionAsync(session.WaitForPacketAsync, session.CharacterId, 243, token);
			await session.AdvanceAsync(TimeSpan.FromMilliseconds(result.Get<ushort>("hitTime") + 1), token);
			await session.WaitForPacketAsync(typeof(SM_PLAYER_INFO), token, p => p.Get<int>("objectId") == session.CharacterId);
			session.AcceptTeleportPosition();
			await session.SynchronizeAsync(token);
			Assert.Equal(altgard, player.GetWorldId());
			Console.WriteLine($"BC-02 learned Return to fortress from the gate/city at {session.CurrentPosition}");
		}
	}
}
