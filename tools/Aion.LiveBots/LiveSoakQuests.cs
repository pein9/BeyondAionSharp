using Aion.Bots.Movement;
using Aion.Bots.Protocol;
using Aion.Bots.Scenarios;
using Aion.Bots.World;
using Aion.GameServer.Model;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.LiveBots;

public static partial class LiveBotRunner
{
	private static async Task SoakQuestsAsync(L0Actor actor, SoakCohort cohort, SoakQuestResources resources, CancellationToken token)
	{
		var session = actor.Session;
		var world = session.Api.World;
		var stages = StarterSoakQuest.ForRace(cohort.FirstRace);
		int prologue = cohort.FirstRace == ScenarioRace.Elyos ? 1000 : 2000;
		await session.SynchronizeAsync(token);
		if (world.GroupId != null || world.IsDead || world.Level < 3 || session.Navigation == null)
			throw new InvalidDataException("Soak quest journey requires a living ungrouped subject and checked navigation.");
		if (!StarterSoakQuest.ObservedComplete(world, prologue)) await session.WaitForQuestStatusAsync(prologue, 5, token);
		foreach (var stage in stages)
		{
			if (world.CompletedQuests.ContainsKey(stage.Id) || world.Quests.GetValueOrDefault(stage.Id)?.Status == 5)
				throw new InvalidDataException($"Finite soak quest {stage.Id} was already completed.");
			actor.Trace.WriteAction(actor.LastStep, "soak:quest-start", new Dictionary<string, object?> { ["quest"] = stage.Id });
			var expected = SoakTotals(world);
			if (stage.Start is { } start)
			{
				int npc = await ApproachAsync(start);
				await session.StartQuestAsync(npc, stage.Id, token);
				await session.SynchronizeAsync(token);
				if (stage.WorkItem != 0) SoakAdd(expected, stage.WorkItem, 1);
				SoakAssertInventory(world, expected);
			}
			else await session.WaitForQuestStatusAsync(stage.Id, 3, token);
			if (world.Quests.GetValueOrDefault(stage.Id)?.Status != 3)
				throw new InvalidDataException($"Soak quest {stage.Id} did not start.");
			if (stage.Objective is { } objective)
			{
				for (int index = 0; index < objective.Positions.Count; index++)
				{
					await SoakQuestWalkAsync(session, objective.Positions[index], token);
					SoakQuestResources.Lease? lease = null;
					while (lease == null)
					{
						await session.SynchronizeAsync(token);
						foreach (var candidate in world.Objects.Values.Where(value => value.TemplateId == objective.TemplateId &&
							QuestDistance(session.CurrentPosition, value.Position) <= 30).OrderBy(value => QuestDistance(session.CurrentPosition, value.Position)))
						{
							lease = resources.TryAcquire(cohort.MapId, SoakLifePolicy.StarterChannel(cohort), candidate.ObjectId);
							if (lease != null) break;
						}
						if (lease == null) await Task.Delay(1000, token);
					}
					using (lease)
					{
						await session.MoveToNpcAsync(lease.ObjectId, token);
						if (objective.Kill) await KillAsync(lease.ObjectId);
						if (objective.ItemId != 0)
						{
							await session.LootQuestItemAsync(lease.ObjectId, objective.ItemId, objective.Kill, token);
							SoakAdd(expected, objective.ItemId, 1);
						}
						await session.SynchronizeAsync(token);
						SoakAssertInventory(world, expected);
						if (objective.ItemId == 0 && (world.Quests[stage.Id].StepAndFlags & 63) != index + 1)
							throw new InvalidDataException($"Soak quest {stage.Id} kill credit was not exactly {index + 1}.");
						lease.Complete();
						actor.Trace.WriteAction(actor.LastStep, "soak:quest-objective", new Dictionary<string, object?>
						{ ["quest"] = stage.Id, ["ordinal"] = index + 1, ["object"] = lease.ObjectId, ["template"] = objective.TemplateId });
					}
				}
			}
			int end = await ApproachAsync(stage.End);
			await session.SynchronizeAsync(token);
			long xp = world.CurrentExperience;
			if (stage.Objective is { ItemId: > 0 }) await session.FinishItemQuestAsync(end, stage.Id, token);
			else await session.FinishQuestWithActionAsync(end, stage.Id,
				stage.Campaign ? DialogAction.SELECTED_QUEST_REWARD1 : DialogAction.SELECTED_QUEST_NOREWARD, token);
			await session.SynchronizeAsync(token);
			if (stage.Objective is { ItemId: > 0 } collected) SoakAdd(expected, collected.ItemId, -collected.Positions.Count);
			if (stage.WorkItem != 0) SoakAdd(expected, stage.WorkItem, -1);
			if (stage.Gold != 0) SoakAdd(expected, BotWorldModel.KinahItemId, stage.Gold);
			if (stage.RewardItem != 0) SoakAdd(expected, stage.RewardItem, stage.RewardCount);
			SoakAssertInventory(world, expected);
			stage.AssertImmediateReward(world, xp);
			actor.Trace.WriteAction(actor.LastStep, "soak:quest-complete", new Dictionary<string, object?>
			{ ["quest"] = stage.Id, ["xp"] = stage.Experience, ["gold"] = stage.Gold });
		}
		foreach (var point in StarterSoakQuest.ReturnVia(cohort.FirstRace).Append(SoakStartPoint(cohort)))
			await SoakQuestWalkAsync(session, point, token);
		await session.SynchronizeAsync(token);
		// Force normal persistence and packet-list reconstruction before retiring this cohort's workload.
		await session.ReenterForSoakAsync(false, token);
		int[] ids = stages.Select(stage => stage.Id).Prepend(prologue).ToArray();
		await session.VerifyQuestRowsAsync(ids, token);
		foreach (int id in ids)
			if (world.CompletedQuests.GetValueOrDefault(id) is not { CompleteCount: 1, NonRepeatable: true })
				throw new InvalidDataException($"Relog did not restore exactly one nonrepeatable Q{id} completion.");
		actor.Trace.WriteAction(actor.LastStep, "soak:quest-journey-persisted", new Dictionary<string, object?> { ["quests"] = ids });

		async Task<int> ApproachAsync(StarterQuestNpc npc)
		{
			await SoakQuestWalkAsync(session, npc.Position, token);
			await session.SynchronizeAsync(token);
			int id = await session.WaitForNearestNpcExceptAsync(npc.TemplateId, new HashSet<int>(), token);
			await session.MoveToNpcAsync(id, token);
			return id;
		}

		async Task KillAsync(int id)
		{
			if (!world.Skills.TryGetValue(1282, out var bolt)) throw new InvalidDataException("Quest mage lacks Flame Bolt.");
			await session.SendPacketAsync(session.Api.Target(id), token);
			for (int cast = 0; cast < 30; cast++)
			{
				await session.SendPacketAsync(session.Api.Cast(new SpellCastData(1282, checked((byte)bolt.Level), 0)
				{ TargetObjectId = id, HitTime = SocialBasicsScenario.DuelHitTime(session.CurrentPosition, world.Objects[id].Position, RaceOf(cohort.FirstRace)) }), token);
				var started = await session.WaitForPacketAsync(typeof(SM_CASTSPELL), token,
					packet => packet.Get<int>("objectId") == session.CharacterId && packet.Get<ushort>("spellId") == 1282);
				await Task.Delay(started.Get<ushort>("castDuration") + 1, token);
				var result = await session.WaitForPacketAsync(typeof(SM_CASTSPELL_RESULT), token,
					packet => packet.Get<int>("effectorId") == session.CharacterId && packet.Get<ushort>("skillId") == 1282);
				await Task.Delay(Math.Max(2000, result.Get<ushort>("hitTime") + 1), token);
				await session.SynchronizeAsync(token);
				if (world.IsDead) throw new InvalidDataException("Quest subject died.");
				if (world.LootStatuses.TryGetValue(id, out byte status) && status == (byte)SM_LOOT_STATUS.Status.LOOT_ENABLE) return;
			}
			throw new InvalidDataException("Quest target survived thirty ordinary casts.");
		}
	}

	private static async Task SoakQuestWalkAsync(LiveBotSession session, BotPosition destination, CancellationToken token)
	{
		var nav = session.Navigation ?? throw new InvalidOperationException("Quest journey requires checked navigation.");
		int map = session.Api.World.MapId ?? throw new InvalidDataException("Missing quest map.");
		var path = nav.Graph.FindPath(map, session.CurrentPosition, destination);
		if (path.Count == 0) path = nav.Geometry.FindLocalPath(map, session.CurrentPosition, destination);
		if (path.Count == 0) path = nav.Geometry.FindJourneyPath(map, session.CurrentPosition, destination);
		if (path.Count == 0) throw new InvalidDataException($"No checked quest route from {session.CurrentPosition} to {destination}.");
		// Keep the single client reader draining during long walks, not just at the final NPC.
		foreach (var segment in path.Chunk(24))
		{
			await session.ExecuteMovementAsync(new BotMover(session.Api.World, session.Api.Timing).CreateGroundPlan(segment,
				session.CurrentPosition, session.Api.World.MovementSpeed ?? throw new InvalidDataException("Missing quest movement speed.")), token);
			await session.SynchronizeAsync(token);
			if (session.Api.World.IsDead) throw new InvalidDataException("Quest subject died while travelling.");
		}
	}
	private static float QuestDistance(BotPosition a, BotPosition b) => MathF.Sqrt(MathF.Pow(a.X - b.X, 2) + MathF.Pow(a.Y - b.Y, 2) + MathF.Pow(a.Z - b.Z, 2));
}
