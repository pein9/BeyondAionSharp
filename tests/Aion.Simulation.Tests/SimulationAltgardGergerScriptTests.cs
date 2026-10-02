using Aion.Bots.Movement;
using Aion.Bots.Navigation;
using Aion.Bots.Scenarios;
using Aion.Bots.World;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;

namespace Aion.Simulation.Tests;

public sealed partial class SimulationFastScenarioTests
{
	/// <summary>
	/// AG-03 (docs/natural-altgard-leveling.md): Gerger's scripted quests, on their contract steps.
	/// <list type="bullet">
	/// <item>Q2246 (item_collecting): the insignia box (700147) used and looted, then Germir's check;</item>
	/// <item>Q2247 (_2247TheGergersDisguise): Gogaerunerk's SETPRO1 gives the disguise, Germir takes it back and ends the quest;</item>
	/// <item>Q2284 (_2284EscapingAsmodae): the first disguised Germir's SETPRO2 sets var 1. The second (798041) exists only
	/// 04:00-21:00: after 21:00 he is gone and the escort waits for 04:00, when the escort protocol restarts the follow at var 1
	/// (SETPRO3) and walks him to Babarunerk (REWARD at var 2), who ends the quest.</item>
	/// </list>
	/// GM setup on the probe only: its class, level and skills, the Leg 6 start's completed quests, setup teleports, the virtual
	/// clock advanced through the night, and the escort line's aggressive monsters despawned (the fights are AG-06's).
	/// </summary>
	[SkippableFact]
	public async Task GergerScriptedQuestsAndTheNightEscortPlayThroughTheContract()
	{
		Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
		const int altgard = 220030000, level = 20;
		using var policy = NewPolicy("AG03", includeHistory: false);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(20));
		CancellationToken token = timeout.Token;
		NaturalAltgardContract leg = NaturalAltgardContract.LoadLeg("l6");
		IReadOnlyDictionary<int, QuestRunPlan> plans = NaturalAltgardContract.LoadPlans("l6");
		NaturalAltgardEscort escort = leg.EscortList.Single();
		NaturalAltgardStep Step(string key) => leg.Steps.Single(step => step.Key == key);
		await using var session = new SimulationL0Session(fixture, policy, "b01", 66, "Asimgerger", Race.ASMODIANS);
		session.BeginStep("s00", "login-create-enter-and-setup");
		await session.LoginAndAuthenticateAsync(token);
		await session.CreateCharacterAsync(token, PlayerClass.PRIEST);
		await session.EnterWorldAsync(token);
		await session.SynchronizeAsync(token);
		Player Server() => fixture.World.GetPlayer(session.CharacterId);
		Assert.True(ClassChangeService.SetClass(Server(), PlayerClass.CLERIC, validate: false, updateDaevaStatus: true));
		Server().GetCommonData().SetLevel(level);
		SkillLearnService.LearnNewSkills(Server(), 1, level);
		foreach (int id in leg.Start.CompletedQuestIds.Where(id => Server().GetQuestStateList().GetQuestState(id) == null))
			Assert.True(Server().GetQuestStateList().AddQuest(id, new QuestState(id, QuestStatus.COMPLETE)));
		var instance = fixture.World.GetWorldMap(altgard).GetMainWorldMapInstance();
		BotNavigationGeometry geometry = BotNavigationGeometry.ForServerWorld(instance.GetInstanceId(), Race.ASMODIANS);
		BotPosition Ground(float[] at, string what) => geometry.GroundAround(altgard, new BotPosition(at[0], at[1], at[2], 0), [2f, 3f, 5f])
			.FirstOrDefault() is { } ground && ground != default ? ground : throw new InvalidDataException($"No ground near {what}.");
		(byte Status, int Var)? State(int quest) => NaturalAltgardQuestSteps.State(session.Api.World, quest);
		long ItemCount(int itemId) => session.Api.World.Inventory.Values.Where(owned => owned.ItemId == itemId).Sum(owned => owned.Count);
		var log = new List<string>();

		bool Hostile(Aion.GameServer.Model.GameObjects.Npc npc) =>
			!npc.IsDead() && NaturalHostility.IsAggressive(npc.GetObjectTemplate(), fixture.DataManager.StaticData.TribeRelations, TribeClass.PC_DARK);
		// The fights are AG-06's: each scripted spot is cleared of aggressive monsters first, and what was cleared is logged.
		void ClearNear(BotPosition at, string what)
		{
			var monsters = instance.GetNpcs().Where(npc => Hostile(npc) &&
				MathF.Pow(npc.GetX() - at.X, 2) + MathF.Pow(npc.GetY() - at.Y, 2) <= 40 * 40).ToArray();
			foreach (var monster in monsters) fixture.World.Despawn(monster);
			if (monsters.Length > 0)
				log.Add($"cleared at {what}: {string.Join(", ", monsters.GroupBy(npc => npc.GetNpcId()).Select(group => $"{group.Key}x{group.Count()}"))}");
		}
		async Task TeleportNearAsync(float[] at, string what)
		{
			BotPosition ground = Ground(at, what);
			ClearNear(ground, what);
			await TeleportForSetupAsync(session, Server(), altgard, ground.X, ground.Y, ground.Z, token);
			session.AcceptTeleportPosition();
			await session.SynchronizeAsync(token);
		}
		async Task TalkAsync(string key)
		{
			NaturalAltgardStep step = Step(key);
			await TeleportNearAsync(step.Position, $"{step.NpcId}");
			string change = await NaturalAltgardQuestSteps.TalkAsync(session, step, await session.WaitForNpcAsync(step.NpcId, token), token);
			log.Add(change);
		}
		async Task WalkAsync(BotPosition to, CancellationToken walkToken)
		{
			IReadOnlyList<BotPosition> path = geometry.FindJourneyPath(altgard, session.CurrentPosition, to);
			if (path.Count == 0) path = [to];
			float speed = session.Api.World.MovementSpeed ?? throw new InvalidOperationException("No movement speed.");
			await session.ExecuteMovementAsync(new BotMover(session.Api.World, session.Api.Timing).CreateGroundPlan(path, session.CurrentPosition, speed), walkToken);
			await session.SynchronizeAsync(walkToken);
		}
		int GameHour() => GameTimeService.GetInstance().GetGameTime().GetHour();
		async Task AdvanceToHourAsync(int hour)
		{
			for (int slice = 0; GameHour() != hour; slice++)
			{
				Assert.True(slice < 3000, $"the clock did not reach {hour}:00");
				await session.AdvanceAsync(TimeSpan.FromSeconds(30), token);
			}
			await session.SynchronizeAsync(token);
		}
		bool SecondGermirOnServer() => instance.GetNpcs().Any(npc => npc.GetNpcId() == escort.FollowerNpcId && !npc.IsDead());

		// Q2246: Germir, the insignia box, Germir's check.
		session.BeginStep("s01", "q2246-insignia");
		QuestRunPlan insignia = plans[2246];
		await TeleportNearAsync(Step("q2247-offer-germir").Position, "Germir");
		await session.StartQuestAsync(await session.WaitForNpcAsync(insignia.StartNpcs.Single().Id, token), 2246, token);
		int badge = insignia.Steps.Single(step => step.Kind == "collect").ItemId;
		var box = instance.GetNpcs().First(npc => npc.GetNpcId() == 700147);
		await TeleportNearAsync([box.GetX(), box.GetY(), box.GetZ()], "the insignia box");
		int useFrom = session.PacketHistory.Count;
		bool looted = await NaturalAltgardQuestSteps.UseObjectAsync(session, await session.WaitForNpcAsync(700147, token), badge, token);
		Assert.True(looted, $"the box gave no insignia: Q2246 {State(2246)}; packets " +
			string.Join(", ", session.PacketHistory.Skip(useFrom).Select(packet => packet.PacketType.Name)));
		Assert.Equal(1, ItemCount(badge));
		await TeleportNearAsync(Step("q2247-offer-germir").Position, "Germir");
		int germir = await session.WaitForNpcAsync(203645, token);
		await NaturalDialogProtocol.OpenAsync(session, germir, token);
		await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token);
		await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(germir, DialogAction.QUEST_SELECT, questId: 2246), token);
		await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token);
		await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(germir, DialogAction.CHECK_USER_HAS_QUEST_ITEM, questId: 2246), token);
		await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token);
		await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(germir, DialogAction.SELECTED_QUEST_NOREWARD, questId: 2246), token);
		await session.SynchronizeAsync(token);
		await session.SendPacketAsync(session.Api.CloseDialog(germir), token);
		await session.SynchronizeAsync(token);
		Assert.Contains(2246, session.Api.World.CompletedQuestIds);
		Assert.Equal(0, ItemCount(badge));
		log.Add("Q2246 complete");

		// Q2247: the disguise from Gogaerunerk, taken back by Germir.
		session.BeginStep("s02", "q2247-disguise");
		await TalkAsync("q2247-offer-germir");
		await TalkAsync("q2247-v0-gogaerunerk");
		Assert.Equal(1, ItemCount(Step("q2247-v0-gogaerunerk").ReceivesItemId!.Value));
		await TalkAsync("q2247-v1-germir");
		Assert.Contains(2247, session.Api.World.CompletedQuestIds);
		Assert.Equal(0, ItemCount(Step("q2247-v0-gogaerunerk").ReceivesItemId!.Value));

		// Q2284: the offer and the first disguised Germir (var 0 -> 1).
		session.BeginStep("s03", "q2284-offer-and-first-germir");
		await TalkAsync("q2284-offer-germir");
		await TalkAsync("q2284-v0-disguised-germir");
		Assert.Equal(((byte)3, escort.StartVar!.Value), State(escort.QuestId));

		// The second disguised Germir keeps 04:00-21:00: after 21:00 he is gone; at 04:00 he is back.
		session.BeginStep("s04", "the-night-without-germir");
		await AdvanceToHourAsync(escort.FollowerDespawnHour!.Value);
		await session.AdvanceAsync(TimeSpan.FromSeconds(30), token);
		Assert.False(SecondGermirOnServer(), $"798041 is on the server at {GameHour()}:00");
		log.Add($"{GameHour()}:00 the second Germir is gone");
		await AdvanceToHourAsync(escort.FollowerSpawnHour!.Value);
		await session.AdvanceAsync(TimeSpan.FromSeconds(30), token);
		Assert.True(SecondGermirOnServer(), $"798041 is not back at {GameHour()}:00");
		log.Add($"{GameHour()}:00 he is back");

		// The escort: the protocol restarts the follow at var 1 and walks him to Babarunerk.
		session.BeginStep("s05", "escort-to-babarunerk");
		var second = instance.GetNpcs().First(npc => npc.GetNpcId() == escort.FollowerNpcId && !npc.IsDead());
		BotPosition followerStart = Ground([second.GetX(), second.GetY(), second.GetZ()], "the second Germir");
		BotPosition standPoint = NaturalEscortPolicy.GoalStand(escort.Goal, followerStart);
		BotPosition stand = Ground([standPoint.X, standPoint.Y, standPoint.Z], "the goal stand");
		IReadOnlyList<BotPosition> route = geometry.FindJourneyPath(altgard, followerStart, stand);
		Assert.NotEmpty(route);
		bool Aggressive(Aion.GameServer.Model.GameObjects.Npc npc) =>
			Hostile(npc) && escort.ClearAreas.Any(area => leg.Area(area).Contains(npc.GetX(), npc.GetY(), npc.GetZ()));
		int Clear()
		{
			var monsters = instance.GetNpcs().Where(Aggressive).ToArray();
			foreach (var monster in monsters) fixture.World.Despawn(monster);
			return monsters.Length;
		}
		log.Add($"despawned {Clear()} aggressive monsters on the escort line");
		await TeleportForSetupAsync(session, Server(), altgard, followerStart.X + 2, followerStart.Y, followerStart.Z, token);
		session.AcceptTeleportPosition();
		await session.SynchronizeAsync(token);
		var protocol = new NaturalEscortProtocol(session, leg, escort, () => fixture.Clock.NowMillis)
		{
			WalkToAsync = WalkAsync,
			Route = () => route,
			ClearAreasHaveAggressors = () => instance.GetNpcs().Any(Aggressive),
			ClearAsync = _ => { log.Add($"cleared {Clear()} more"); return Task.FromResult<long?>(null); },
		};
		long started = fixture.Clock.NowMillis;
		NaturalEscortResult result = await protocol.RunAsync(token);
		log.Add($"escort {result.Outcome} after {(fixture.Clock.NowMillis - started) / 1000} game s, attempts {result.Attempts}; " +
			string.Join("; ", result.Log.Select(a => $"#{a.Number} {a.Outcome} {(a.EndMillis - a.StartMillis) / 1000.0:F0} s, gap max {a.LongestGap:F1} m")));
		Assert.Equal("done", result.Outcome);
		Assert.Equal(QuestStatus.REWARD, Server().GetQuestStateList().GetQuestState(escort.QuestId).GetStatus());
		Assert.Equal(escort.SuccessVar, Server().GetQuestStateList().GetQuestState(escort.QuestId).GetQuestVarById(0));

		// Babarunerk ends it.
		session.BeginStep("s06", "q2284-hand-in");
		await TalkAsync("q2284-reward-babarunerk");
		Assert.Contains(escort.QuestId, session.Api.World.CompletedQuestIds);
		Console.WriteLine($"AG-03 {string.Join("; ", log)}");
		policy.AssertClean();
	}
}
