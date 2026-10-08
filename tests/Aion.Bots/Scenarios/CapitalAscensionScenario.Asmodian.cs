using Aion.Bots.Protocol;
using Aion.Bots.World;
using Aion.GameServer.Model;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.Bots.Scenarios;

/// <summary>
/// CAPITAL-ASMO (NA-02): level-nine setup at Munin, then the Asmodian Ascension bridge entirely through client
/// actions: Q2008 as a Cleric, the Q2009 ceremony (Karmic Staff), Q2904, the Altgard Fortress bind and Q24010.
/// Server coverage for docs/natural-ascension-altgard.md; ids, positions and dialogs come from its contract.
/// <para>
/// CP-30 (docs/natural-class-profiles.md): the class pair is the contract's. The class chosen and checked, the Q2009 var
/// and preceptor, the ceremony weapon and the dispatch quest are read from it, and the four class-dependent steps are
/// found by role, so another pair's bridge (<see cref="NaturalAscensionContract.ForChoice"/>) runs the same scenario.
/// With the reviewed contract it does what it always did.
/// </para>
/// </summary>
public static partial class CapitalAscensionScenario
{
	/// <param name="stopAtDispatchStart">The short endpoint: stop once Doman's SETPRO1 has moved the dispatch quest from
	/// var 0 to var 1, before his teleporter is used. The driver's own check then ends the scenario in Pandaemonium.</param>
	/// <param name="trialSwings">CP-67a: the normal attacks Hellion is given; unset means the scenario's 600. A caller whose
	/// starter weapon does less than the Priest's mace names its own bound, with its reason.</param>
	public static async Task RunAsmodianAsync(ICapitalAscensionDriver driver, NaturalAscensionContract contract,
		CancellationToken token = default, bool stopAtDispatchStart = false, int? trialSwings = null)
	{
		NaturalAscensionStep Step(string key) => contract.Steps.Single(step => step.Key == key);
		static BotPosition At(float[] position) => new(position[0], position[1], position[2], 0);
		static int Action(string name) => NaturalAscensionContract.DialogActionId(name);
		const int ascension = 2008, ceremony = 2009, suthran = 24010;
		int dispatch = contract.Dispatch.QuestId;
		// The chosen class as a message names it: CLERIC reads Cleric.
		string chosen = string.Join(' ', contract.ClassChoice.ToClass.Split('_').Select(word => word[..1] + word[1..].ToLowerInvariant()));

		await driver.StepAsync("setup-level-nine-at-munin", driver.PrepareAsync, token);
		await driver.SynchronizeAsync(token);
		Require(driver.Api.World.Level == contract.Start.Level && State(driver, ascension, 3, 0), "Ascension was not auto-started at level nine.");

		await driver.StepAsync("munin-and-the-three-norns", async ct =>
		{
			foreach (string key in new[] { "q2008-v0-munin", "q2008-v1-urd", "q2008-v2-verdandi", "q2008-v3-skuld" })
			{
				NaturalAscensionStep step = Step(key);
				int npc = await ApproachAsync(driver, step.NpcId, At(step.Position), ct);
				await DialogAsync(driver, npc, ascension, Action(step.Actions[0]), checked((ushort)step.Pages[0]), ct);
				await SelectAsync(driver, npc, ascension, Action(step.Actions[1]), ct);
				await driver.CompleteTeleportAsync(step.Teleport!.MapId, ct);
				await driver.SynchronizeAsync(ct);
				Require(State(driver, ascension, 3, step.Var!.Value + 1), $"{key} did not advance Ascension.");
				if (step.ReceivesItemId is { } card)
					Require(ItemCount(driver, card) == 1, $"{key} did not hand over Destiny Card {card}.");
			}
		}, token);

		await driver.StepAsync("munin-opens-ataxiar", async ct =>
		{
			NaturalAscensionStep step = Step("q2008-v4-munin");
			int munin = await ApproachAsync(driver, step.NpcId, At(step.Position), ct);
			await DialogAsync(driver, munin, ascension, Action("QUEST_SELECT"), checked((ushort)step.Pages[0]), ct);
			await SelectAsync(driver, munin, ascension, Action("SELECT5_1"), ct);
			await driver.WaitAsync(typeof(SM_PLAY_MOVIE), packet => packet.Get<int>("cutsceneId") == step.MovieId, ct);
			await driver.WaitAsync(typeof(SM_DIALOG_WINDOW), packet => packet.Get<int>("questId") == ascension &&
				packet.Get<ushort>("dialogPageId") == step.Pages[1], ct);
			await SelectAsync(driver, munin, ascension, Action("SETPRO5"), ct);
			await driver.CompleteTeleportAsync(contract.Instance.MapId, ct);
			await driver.SynchronizeAsync(ct);
			Require(State(driver, ascension, 3, 99) && driver.Api.World.MapId == contract.Instance.MapId, "Munin did not open the Ataxiar instance.");
			Require(new[] { 182203009, 182203010, 182203011 }.All(card => ItemCount(driver, card) == 0), "The Destiny Cards were not consumed.");
		}, token);

		await driver.StepAsync("hagen-scripted-flight", async ct =>
		{
			NaturalAscensionStep step = Step("q2008-v99-hagen");
			int hagen = await ApproachAsync(driver, step.NpcId, At(step.Position), ct);
			await SelectAsync(driver, hagen, ascension, Action("QUEST_SELECT"), ct);
			await driver.WaitAsync(typeof(SM_EMOTION), packet => packet.Get<int>("senderObjectId") == driver.Api.World.SelfObjectId &&
				packet.Get<byte>("emotionType") == (byte)EmotionType.START_FLYTELEPORT && packet.Get<int>("teleportId") == 3001, ct);
			await driver.FlyAsync(At(contract.Instance.FlyPathEnd), TimeSpan.FromSeconds(contract.Instance.FlightSeconds), ct);
			await driver.SendAsync(GameClientPackets.Emotion((byte)EmotionType.LAND_FLYTELEPORT), ct);
			await driver.SynchronizeAsync(ct);
			Require(State(driver, ascension, 3, 51), "The 43-second quest timer did not start the trial.");
			NaturalAscensionTrialGroup assassins = contract.Instance.Trial[0];
			Require(driver.Api.World.Objects.Values.Count(npc => npc.TemplateId == assassins.NpcId) == assassins.Count, "Expected four guardian assassins.");
		}, token);

		await driver.StepAsync("defeat-four-assassins-with-normal-attacks", async ct =>
		{
			NaturalAscensionTrialGroup assassins = contract.Instance.Trial[0];
			for (int i = 0; i < assassins.Count; i++)
			{
				int target = driver.Api.World.Objects.Values.Where(npc => npc.TemplateId == assassins.NpcId).OrderBy(npc => npc.ObjectId).First().ObjectId;
				await DefeatAsync(driver, target, ct);
				Require(State(driver, ascension, 3, i == assassins.Count - 1 ? 5 : 52 + i), "An assassin kill did not advance the trial exactly once.");
			}
		}, token);

		await driver.StepAsync("defeat-hellion-with-normal-attacks", async ct =>
		{
			await driver.SynchronizeAsync(ct);
			int hellion = driver.Api.World.Objects.Values.Single(npc => npc.TemplateId == contract.Instance.Trial[1].NpcId).ObjectId;
			// 1,461 HP against a level-nine Priest's mace (about five per swing, with stuns): a wider bound than the
			// Gladiator needs for Orissan.
			await DefeatAsync(driver, hellion, ct, maxAttacks: trialSwings ?? 600);
			// Movie 152 arrives with the kill, inside the attack loop; the driver's final check finds it in history.
			await WaitStateAsync(driver, ascension, 3, 6, ct);
		}, token);

		await driver.StepAsync("choose-cleric-and-leave-ataxiar", async ct =>
		{
			NaturalAscensionStep step = contract.Step(NaturalAscensionStepRole.ClassChoice);
			int munin = await ApproachAsync(driver, step.NpcId, At(step.Position), ct);
			await DialogAsync(driver, munin, ascension, Action("QUEST_SELECT"), checked((ushort)step.Pages[0]), ct);
			await DialogAsync(driver, munin, ascension, Action("SETPRO6"), checked((ushort)contract.ClassChoice.ClassPageId), ct);
			await DialogAsync(driver, munin, ascension, Action(contract.ClassChoice.Action), 5, ct);
			await SelectAsync(driver, munin, ascension, Action(contract.Instance.ExitAction), ct);
			await driver.CompleteTeleportAsync(contract.Start.MapId, ct);
			await driver.SynchronizeAsync(ct);
			Require(State(driver, ascension, 5) && State(driver, ceremony, 3, 0), "Ascension completion did not start the ceremony.");
			Require(driver.Api.World.Objects[driver.Api.World.SelfObjectId!.Value].PlayerClass == contract.Endpoint.ClassId,
				$"Quest did not change the character's class to {chosen}.");
			Require(driver.Api.World.Level == contract.Start.Level, "Ascension's own experience must not pass the non-Daeva cap.");
		}, token);

		await driver.StepAsync("munin-sends-player-to-pandaemonium", async ct =>
		{
			NaturalAscensionStep step = Step("q2009-v0-munin");
			int munin = await ApproachAsync(driver, step.NpcId, At(step.Position), ct);
			await DialogAsync(driver, munin, ceremony, Action("QUEST_SELECT"), checked((ushort)step.Pages[0]), ct);
			await SelectAsync(driver, munin, ceremony, Action("SETPRO1"), ct);
			await driver.CompleteTeleportAsync(step.Teleport!.MapId, ct);
			await driver.SynchronizeAsync(ct);
			Require(driver.Api.World.MapId == step.Teleport.MapId && State(driver, ceremony, 3, 1), "Quest did not take the player to Pandaemonium.");
		}, token);

		await driver.StepAsync("heimdall-balder-and-lyfjaberga-ceremony", async ct =>
		{
			foreach (string key in new[] { "q2009-v1-heimdall", "q2009-v2-balder" })
			{
				NaturalAscensionStep step = Step(key);
				int npc = await ApproachAsync(driver, step.NpcId, At(step.Position), ct);
				await DialogAsync(driver, npc, ceremony, Action(step.Actions[0]), checked((ushort)step.Pages[0]), ct);
				await MovieAsync(driver, npc, ceremony, Action(step.Actions[1]), step.MovieId!.Value, ct);
				await SelectAsync(driver, npc, ceremony, Action(step.Actions[2]), ct);
			}
			NaturalAscensionStep reward = contract.Step(NaturalAscensionStepRole.Ceremony);
			await WaitStateAsync(driver, ceremony, 4, reward.Var!.Value, ct);
			int lyfjaberga = await ApproachAsync(driver, reward.NpcId, At(reward.Position), ct);
			await DialogAsync(driver, lyfjaberga, ceremony, Action("SELECT_QUEST_REWARD"), checked((ushort)reward.Pages[1]), ct);
			var expected = InventoryTotals(driver);
			expected[contract.CeremonyReward.ItemId] = expected.GetValueOrDefault(contract.CeremonyReward.ItemId) + 1;
			expected[contract.CeremonyReward.TeaItemId] = expected.GetValueOrDefault(contract.CeremonyReward.TeaItemId) + contract.CeremonyReward.TeaCount;
			expected[BotWorldModel.KinahItemId] = expected.GetValueOrDefault(BotWorldModel.KinahItemId) + contract.CeremonyReward.Kinah;
			await SelectAsync(driver, lyfjaberga, ceremony, Action(contract.CeremonyReward.Action), ct);
			await WaitStateAsync(driver, ceremony, 5, null, ct);
			await driver.SynchronizeAsync(ct);
			Require(expected.OrderBy(pair => pair.Key).SequenceEqual(InventoryTotals(driver).OrderBy(pair => pair.Key)),
				$"Ceremony did not grant exactly the ceremony weapon {contract.CeremonyReward.ItemId}, the teas and the kinah.");
			Require(driver.Api.World.Level >= contract.Endpoint.MinimumLevel, "The ceremony payout did not reach level 10.");
			Require(State(driver, dispatch, 3, 0), "Dispatch to Altgard did not start when the ceremony completed.");
		}, token);

		await driver.StepAsync("doman-dispatch-and-teleport-to-altgard", async ct =>
		{
			NaturalAscensionStep step = contract.Step(NaturalAscensionStepRole.DispatchStart);
			int doman = await ApproachAsync(driver, step.NpcId, At(step.Position), ct);
			await DialogAsync(driver, doman, dispatch, Action("QUEST_SELECT"), checked((ushort)step.Pages[0]), ct);
			await SelectAsync(driver, doman, dispatch, Action("SETPRO1"), ct);
			await WaitStateAsync(driver, dispatch, 3, 1, ct);
			if (stopAtDispatchStart)
			{
				await driver.SynchronizeAsync(ct);
				await driver.VerifyAsync(ct);
				return;
			}
			long kinah = driver.Api.World.Kinah;
			await SelectAsync(driver, doman, 0, Action("AIRLINE_SERVICE"), ct);
			await driver.WaitAsync(typeof(SM_TELEPORT_MAP), _ => true, ct);
			await driver.SendAsync(driver.Api.Teleport(doman, contract.Teleporter.LocationId), ct);
			await driver.CompleteTeleportAsync(contract.Teleporter.Destination.MapId, ct);
			await driver.SynchronizeAsync(ct);
			Require(driver.Api.World.MapId == contract.Teleporter.Destination.MapId, "Doman's teleporter did not reach Altgard.");
			// The fare goes through PricesService (global prices, modifier and influence taxes) as SM_PRICES shows it.
			long fare = driver.Api.World.VendorPrices?.ServicePrice(contract.Teleporter.BasePrice) ?? contract.Teleporter.BasePrice;
			Require(kinah - driver.Api.World.Kinah == fare, $"Teleport cost {kinah - driver.Api.World.Kinah}, expected {fare}.");
			Require(State(driver, suthran, 3, 0), "Suthran's Orders did not start on entering Altgard.");
		}, token);
		if (stopAtDispatchStart) return;

		await driver.StepAsync("bind-at-altgard-fortress", async ct =>
		{
			long kinah = driver.Api.World.Kinah;
			await driver.MoveAsync(At(contract.Bind.Position) with { X = contract.Bind.Position[0] - 1 }, ct);
			await driver.SynchronizeAsync(ct);
			int obelisk = driver.Api.World.Objects.Values.Single(npc => npc.TemplateId == contract.Bind.NpcId).ObjectId;
			await driver.SendAsync(driver.Api.TalkTo(obelisk), ct);
			DecodedBotServerPacket question = await driver.WaitAsync(typeof(SM_QUESTION_WINDOW), packet => packet.Get<int>("code") == contract.Bind.QuestionId, ct);
			await driver.SendAsync(GameClientPackets.QuestionResponse(question.Get<int>("code"), 1, question.Get<int>("senderId")), ct);
			await driver.WaitAsync(typeof(SM_BIND_POINT_INFO), _ => true, ct);
			await driver.SynchronizeAsync(ct);
			Require(driver.Api.World.ObeliskBindPoint is { } bound && bound.MapId == contract.Bind.MapId, "The Altgard Fortress bind was not registered.");
			Require(kinah - driver.Api.World.Kinah == contract.Bind.Price, $"Bind cost {kinah - driver.Api.World.Kinah}, expected {contract.Bind.Price}.");
		}, token);

		await driver.StepAsync("meiyer-and-suthran-turn-ins", async ct =>
		{
			foreach ((NaturalAscensionStep step, int quest) in new[] { (contract.Step(NaturalAscensionStepRole.DispatchReward), dispatch),
				(Step("q24010-reward-suthran"), suthran) })
			{
				int npc = await ApproachAsync(driver, step.NpcId, At(step.Position), ct);
				await DialogAsync(driver, npc, quest, Action("QUEST_SELECT"), checked((ushort)step.Pages[0]), ct);
				await DialogAsync(driver, npc, quest, Action("SELECT_QUEST_REWARD"), checked((ushort)step.Pages[1]), ct);
				await SelectAsync(driver, npc, quest, Action("SELECTED_QUEST_NOREWARD"), ct);
				await WaitStateAsync(driver, quest, 5, null, ct);
			}
			await driver.SynchronizeAsync(ct);
			Require(contract.Endpoint.CompletedQuestIds.All(quest => State(driver, quest, 5)), "Not every bridge quest is complete.");
			Require(!driver.Api.World.IsDead && driver.Api.World.MapId == contract.Endpoint.MapId, "The bridge did not finish alive in Altgard.");
			await driver.VerifyAsync(ct);
		}, token);
	}
}
