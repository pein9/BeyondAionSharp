using Aion.Bots.Movement;
using Aion.Bots.Protocol;
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
	/// AC-05 (docs/natural-altgard-leveling.md): Q2221 "Manir's Uncle" on the live SIM server. A level 15 probe Cleric takes it
	/// from Manir, talks to Groken (var 1), uses Groken's Safe beside Commander Mohen (the 3 s use bar, page 1693 closed with
	/// SETPRO2, the loot 182203215 at 100%, var 2, the safe gone for 295 s), and hands the item back to Groken; Q2290 is then
	/// offered. GM setup on the probe only: the camp's aggressive robbers are despawned (the fight is AC-06's) and the
	/// travel between the three places is a setup teleport (AC-02 walked it).
	/// </summary>
	[SkippableFact]
	public async Task AltgardManirsUncleOpensGrokensSafeAndOffersTheEscort()
	{
		Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
		const int altgard = 220030000, level = 15, quest = 2221, escortQuest = 2290;
		using var policy = NewPolicy("AC05", includeHistory: false);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(10));
		CancellationToken token = timeout.Token;
		NaturalAltgardContract leg = NaturalAltgardContract.LoadLeg("l3");
		NaturalAltgardObjectUse safe = leg.ObjectUseList.Single(use => use.QuestId == quest);
		NaturalAltgardStep Step(string key) => leg.Steps.Single(step => step.Key == key);
		await using var session = new SimulationL0Session(fixture, policy, "b01", 145, "Asimsafe", Race.ASMODIANS);
		session.BeginStep("s00", "login-create-enter-and-setup");
		await session.LoginAndAuthenticateAsync(token);
		await session.CreateCharacterAsync(token, PlayerClass.PRIEST);
		await session.EnterWorldAsync(token);
		await session.SynchronizeAsync(token);
		Player player = fixture.World.GetPlayer(session.CharacterId);
		Assert.True(ClassChangeService.SetClass(player, PlayerClass.CLERIC, validate: false, updateDaevaStatus: true));
		player.GetCommonData().SetLevel(level);
		SkillLearnService.LearnNewSkills(player, 1, level);
		var instance = fixture.World.GetWorldMap(altgard).GetMainWorldMapInstance();
		BotNavigationGeometry geometry = BotNavigationGeometry.ForServerWorld(instance.GetInstanceId(), Race.ASMODIANS);
		BotPosition Ground(float[] at, string what) => geometry.GroundAround(altgard, new BotPosition(at[0], at[1], at[2], 0), [2f, 3f])
			.FirstOrDefault() is { } ground && ground != default ? ground : throw new InvalidDataException($"No ground near {what}.");
		int despawned = 0;
		foreach (var npc in instance.GetNpcs().Where(npc =>
			NaturalHostility.IsAggressive(npc.GetObjectTemplate(), fixture.DataManager.StaticData.TribeRelations, TribeClass.PC_DARK) &&
			(leg.Area("safe-camp").Contains(npc.GetX(), npc.GetY(), npc.GetZ()) || leg.Area("groken").Contains(npc.GetX(), npc.GetY(), npc.GetZ()))).ToArray())
		{
			fixture.World.Despawn(npc);
			despawned++;
		}
		Console.WriteLine($"AC-05 despawned {despawned} robbers in the safe camp and around Groken");
		async Task TalkAtAsync(string key)
		{
			NaturalAltgardStep step = Step(key);
			BotPosition at = Ground(step.Position, key);
			await TeleportForSetupAsync(session, player, altgard, at.X, at.Y, at.Z, token);
			session.AcceptTeleportPosition();
			await session.SynchronizeAsync(token);
			session.BeginStep($"s-{key}", key);
			Console.WriteLine($"AC-05 {await NaturalAltgardQuestSteps.TalkAsync(session, step, await session.WaitForNpcAsync(step.NpcId, token), token)}");
		}

		await TalkAtAsync("q2221-offer-manir");
		await TalkAtAsync("q2221-v0-groken");
		Assert.Equal(((byte)3, safe.FromVar), NaturalAltgardQuestSteps.State(session.Api.World, quest));

		session.BeginStep("s-safe", safe.Key);
		BotPosition safeGround = Ground([1266.44f, 1109.84f, 254.125f], "the safe");
		IReadOnlyList<BotPosition> path = geometry.FindJourneyPath(altgard, session.CurrentPosition, safeGround);
		Assert.NotEmpty(path);
		float speed = session.Api.World.MovementSpeed ?? throw new InvalidOperationException("No movement speed.");
		await session.ExecuteMovementAsync(new BotMover(session.Api.World, session.Api.Timing).CreateGroundPlan(path, session.CurrentPosition, speed), token);
		await session.SynchronizeAsync(token);
		int safeObject = await session.WaitForNpcAsync(safe.NpcId, token);
		int useStart = session.PacketHistory.Count;
		bool looted = await NaturalAltgardQuestSteps.UseContractObjectAsync(session, safe, safeObject, token);
		DecodedBotServerPacket[] after = session.PacketHistory.Skip(useStart).ToArray();
		int? useBar = after.FirstOrDefault(packet => packet.PacketType == typeof(SM_USE_OBJECT) && packet.Get<int>("targetObjectId") == safeObject)?.Get<int>("durationMs");
		bool page = after.Any(packet => packet.PacketType == typeof(SM_DIALOG_WINDOW) && packet.Get<int>("targetObjectId") == safeObject &&
			packet.Get<ushort>("dialogPageId") == safe.DialogPage);
		Console.WriteLine($"AC-05 safe: use bar {useBar} ms, page {safe.DialogPage} {page}, looted {looted}, quest {NaturalAltgardQuestSteps.State(session.Api.World, quest)}");
		Assert.True(looted, "The safe gave no item.");
		Assert.True(page, $"The safe did not open page {safe.DialogPage}.");
		Assert.Equal(3000, useBar);
		Assert.Contains(session.Api.World.Inventory.Values, item => item.ItemId == safe.LootItemId);
		Assert.Equal(((byte)3, safe.ToVar), NaturalAltgardQuestSteps.State(session.Api.World, quest));
		Assert.DoesNotContain(instance.GetNpcs(), npc => npc.GetNpcId() == safe.NpcId && !npc.IsDead());

		await TalkAtAsync("q2221-v2-groken");
		Assert.Contains(quest, session.Api.World.CompletedQuestIds);
		Assert.DoesNotContain(session.Api.World.Inventory.Values, item => item.ItemId == safe.LootItemId);
		Assert.Equal(QuestStatus.COMPLETE, player.GetQuestStateList().GetQuestState(quest).GetStatus());

		// Q2290 is offered now: Groken's quest page for it opens (the escort itself is AC-04's).
		session.BeginStep("s-offer", "q2290-offered");
		int groken = await session.WaitForNpcAsync(Step("q2290-offer-groken").NpcId, token);
		await NaturalDialogProtocol.OpenAsync(session, groken, token);
		await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token, packet => packet.Get<int>("targetObjectId") == groken);
		await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(groken,
			checked((ushort)NaturalAscensionContract.DialogActionId("QUEST_SELECT")), questId: escortQuest), token);
		await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token, packet =>
			packet.Get<int>("targetObjectId") == groken && packet.Get<ushort>("dialogPageId") == 1011);
		await session.SendPacketAsync(session.Api.CloseDialog(groken), token);
		await session.SynchronizeAsync(token);
		Assert.Null(NaturalAltgardQuestSteps.State(session.Api.World, escortQuest));
		Console.WriteLine("AC-05 Q2290 is offered (page 1011) and not taken");
		policy.AssertClean();
	}
}
