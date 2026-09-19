using Aion.Bots.Protocol;
using Aion.Bots.Scenarios;
using Aion.Bots.World;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services.Items;

namespace Aion.Simulation.Tests;

public sealed partial class SimulationFastScenarioTests
{
	private async Task RunQuestPlanZoneAsync(ScenarioDefinition scenario, bool includeHistory, string zone,
		Race race, int accountId, string characterName)
	{
		using var policy = NewPolicy(scenario.Id, includeHistory);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(12));
		CancellationToken token = timeout.Token;
		await using var session = new SimulationL0Session(
			fixture, policy, "b01", accountId, characterName, race);

		session.BeginStep("s01", "login-create-enter-and-level-eight");
		await session.LoginAndAuthenticateAsync(token);
		await session.CreateCharacterAsync(token);
		await session.EnterWorldAsync(token);
		await session.WaitForPacketAsync(typeof(SM_PLAY_MOVIE), token);
		Player player = fixture.World.GetPlayer(session.CharacterId);
		player.GetCommonData().SetLevel(8);
		await session.DrainServerPacketsAsync(token);

		string root = Environment.GetEnvironmentVariable("AION_E2E_QUEST_PLAN_ROOT")
			?? throw new InvalidOperationException("AION_E2E_QUEST_PLAN_ROOT is required for quest-plan scenarios.");
		IReadOnlyList<QuestRunPlan> plans = OrderQuestPlans(QuestRunPlan.LoadDirectory(Path.Combine(root, zone)));
		Assert.NotEmpty(plans);
		var driver = new SimulationQuestRunDriver(this, session, player);
		foreach (QuestRunPlan plan in plans)
		{
			session.BeginStep($"q{plan.Id}", $"quest-plan:{plan.Template}:{plan.Name}");
			using var questTimeout = CancellationTokenSource.CreateLinkedTokenSource(token);
			questTimeout.CancelAfter(TimeSpan.FromSeconds(30));
			try
			{
				await QuestRunExecutor.ExecuteAsync(QuestRunBook.Build(plan), driver, questTimeout.Token);
			}
			catch (OperationCanceledException exception) when (!token.IsCancellationRequested)
			{
				throw new TimeoutException($"Q{plan.Id} ({plan.Template}) exceeded its 30 second budget.", exception);
			}
			catch (Exception exception) when (exception is not OperationCanceledException)
			{
				throw new InvalidDataException($"Q{plan.Id} ({plan.Template}) failed: {exception.Message}", exception);
			}
			Assert.Equal(QuestStatus.COMPLETE, player.GetQuestStateList().GetQuestState(plan.Id).GetStatus());
			Assert.True(session.Api.World.CompletedQuests.ContainsKey(plan.Id) ||
				session.Api.World.Quests.TryGetValue(plan.Id, out BotQuestState? state) && state.Status == 5,
				$"Bot world did not observe Q{plan.Id} complete.");
		}
		policy.AssertClean();
		QuestCoverageReceipt.SaveFromEnvironment("SIM", scenario.Id, session.Api.World);
	}

	private static IReadOnlyList<QuestRunPlan> OrderQuestPlans(IReadOnlyList<QuestRunPlan> plans)
	{
		var byId = plans.ToDictionary(plan => plan.Id);
		var ordered = new List<QuestRunPlan>(plans.Count);
		var complete = new HashSet<int>();
		while (ordered.Count != plans.Count)
		{
			QuestRunPlan? next = byId.Values
				.Where(plan => !complete.Contains(plan.Id))
				.Where(plan => plan.FinishedQuestGroups.Count == 0 || plan.FinishedQuestGroups.Any(
					group => group.All(required => !byId.ContainsKey(required) || complete.Contains(required))))
				.OrderBy(plan => plan.Id)
				.FirstOrDefault();
			if (next == null)
				throw new InvalidDataException("Quest-plan prerequisites contain a cycle inside the selected zone.");
			ordered.Add(next);
			complete.Add(next.Id);
		}
		return ordered;
	}

	private sealed class SimulationQuestRunDriver(
		SimulationFastScenarioTests owner,
		SimulationL0Session session,
		Player player) : IQuestRunDriver
	{
		public async Task ExecuteAsync(QuestRunPlan plan, QuestRunOperation operation, CancellationToken cancellationToken)
		{
			switch (operation.Kind)
			{
				case QuestRunOperationKind.Prepare:
					Prepare(plan);
					break;
				case QuestRunOperationKind.StartAtNpc:
					await StartAtNpcAsync(plan, RequiredNpcs(operation), cancellationToken);
					break;
				case QuestRunOperationKind.StartWithItem:
					await StartWithItemAsync(plan, operation.ItemId, cancellationToken);
					break;
				case QuestRunOperationKind.Kill:
				case QuestRunOperationKind.KillSpawned:
					await KillAsync(RequiredNpcs(operation), operation.Count, cancellationToken);
					break;
				case QuestRunOperationKind.CollectQuestDrop:
				case QuestRunOperationKind.UseQuestObject:
					await CollectQuestItemAsync(plan, operation, cancellationToken);
					break;
				case QuestRunOperationKind.Gather:
					await GatherAsync(operation, cancellationToken);
					break;
				case QuestRunOperationKind.Report:
					await ReportAsync(plan, operation, cancellationToken);
					break;
				case QuestRunOperationKind.ClaimReward:
					await ClaimRewardAsync(plan, cancellationToken);
					break;
				case QuestRunOperationKind.StartOnLevelUp:
				case QuestRunOperationKind.StartOnZoneEntry:
				case QuestRunOperationKind.StartOnWorldEntry:
				case QuestRunOperationKind.StartAutomatically:
				case QuestRunOperationKind.Craft:
				case QuestRunOperationKind.UseSkill:
				case QuestRunOperationKind.KillInWorld:
				case QuestRunOperationKind.KillInZone:
					throw new InvalidDataException($"Starter-zone driver cannot execute {operation.Kind} for Q{plan.Id}.");
				default:
					throw new ArgumentOutOfRangeException(nameof(operation));
			}
		}

		private void Prepare(QuestRunPlan plan)
		{
			foreach (IReadOnlyList<int> group in plan.FinishedQuestGroups.Take(1))
			{
				foreach (int prerequisite in group)
				{
					if (player.GetQuestStateList().GetQuestState(prerequisite) != null)
						continue;
					player.GetQuestStateList().AddQuest(prerequisite, new QuestState(prerequisite, QuestStatus.COMPLETE));
				}
			}
		}

		private async Task StartAtNpcAsync(QuestRunPlan plan, IReadOnlyList<QuestRunNpc> npcs, CancellationToken token)
		{
			Npc npc = await MoveToNpcAsync(npcs, token);
			await session.StartQuestAsync(npc.GetObjectId(), plan.Id, token);
			await WaitForQuestStatusAsync(session, plan.Id, 3, token);
		}

		private async Task StartWithItemAsync(QuestRunPlan plan, int itemId, CancellationToken token)
		{
			Assert.Equal(0, ItemService.AddItem(player, itemId, 1, true));
			await session.DrainServerPacketsAsync(token);
			Item item = player.GetInventory().GetFirstItemByItemId(itemId);
			await session.SendPacketAsync(session.Api.UseItem(item.GetObjectId(), item.GetItemTemplate()), token);
			DecodedBotServerPacket dialog = await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token,
				packet => packet.Get<int>("questId") == plan.Id);
			int target = dialog.Get<int>("targetObjectId");
			await session.SendPacketAsync(session.Api.SelectDialog(target, DialogAction.QUEST_ACCEPT_1, questId: plan.Id), token);
			await WaitForQuestStatusAsync(session, plan.Id, 3, token);
		}

		private async Task KillAsync(IReadOnlyList<QuestRunNpc> npcs, int count, CancellationToken token)
		{
			for (int index = 0; index < count; index++)
			{
				Npc npc = await MoveToNpcAsync(npcs, token, index);
				await WaitForAutoAttackAsync(token);
				npc.GetLifeStats().SetCurrentHp(1);
				await KillForQuestAsync(session, player, npc, token);
			}
		}

		private async Task CollectQuestItemAsync(QuestRunPlan plan, QuestRunOperation operation, CancellationToken token)
		{
			IReadOnlyList<QuestRunSource> sources = operation.Sources ??
				(operation.Source == null ? [] : [operation.Source]);
			if (sources.Count == 0 || sources.Any(source => source.Npc == null))
				throw new InvalidDataException("Quest-item operation has no NPC source.");
			bool neutralToNpcs = sources.Any(source => source.Npc!.Id >= 700000);
			if (neutralToNpcs)
			{
				player.SetCustomState(CustomPlayerState.NEUTRAL_TO_ALL_NPCS);
				player.GetController().OnChangedPlayerAttributes();
			}
			try
			{
				int attempts = 0;
				var attemptedObjects = new List<int>();
				while (ItemCount(session.Api.World, operation.ItemId) < operation.Count)
				{
					if (++attempts > operation.Count + 32)
						throw new InvalidDataException(
							$"Could not collect item {operation.ItemId} after {attempts - 1} declared-source attempts.");
					(QuestRunNpc npcPlan, int sourceOrdinal) = SelectQuestItemNpc(sources, attempts - 1);
					bool actionObject = npcPlan.Id >= 700000;
					try
					{
						Npc npc = await MoveToNpcAsync([npcPlan], token, sourceOrdinal);
						attemptedObjects.Add(npc.GetObjectId());
						if (operation.Kind == QuestRunOperationKind.UseQuestObject)
						{
							await UseQuestObjectAsync(session, npc.GetObjectId(), operation.ItemId, token);
						}
						else if (actionObject)
						{
							await LootActionObjectAsync(session, npc.GetObjectId(), operation.ItemId, token);
						}
						else
						{
							await WaitForAutoAttackAsync(token);
							npc.GetLifeStats().SetCurrentHp(1);
							await KillForQuestAsync(session, player, npc, token);
							await TryLootCorpseItemAsync(session, npc.GetObjectId(), operation.ItemId, token);
						}
					}
					catch (Exception exception) when (exception is not OperationCanceledException)
					{
						QuestState? state = player.GetQuestStateList().GetQuestState(plan.Id);
						throw new InvalidDataException(
							$"Collection attempt {attempts} for item {operation.ItemId} from NPC {npcPlan.Id} failed " +
							$"with {ItemCount(session.Api.World, operation.ItemId)}/{operation.Count} collected" +
							$"; latest quest state is {state?.GetStatus().ToString() ?? "missing"}; " +
							$"object sequence is {string.Join(",", attemptedObjects)}; recent packets are " +
							$"{string.Join(",", session.PacketHistory.TakeLast(12).Select(DescribePacket))}.", exception);
					}
				}
				Assert.Equal(operation.Count, ItemCount(session.Api.World, operation.ItemId));
			}
			finally
			{
				if (neutralToNpcs)
				{
					player.UnsetCustomState(CustomPlayerState.NEUTRAL_TO_ALL_NPCS);
					player.GetController().OnChangedPlayerAttributes();
				}
			}
		}

		private static (QuestRunNpc Npc, int Ordinal) SelectQuestItemNpc(
			IReadOnlyList<QuestRunSource> sources, int attempt)
		{
			int ordinal = attempt;
			foreach (QuestRunSource source in sources)
			{
				QuestRunNpc npc = source.Npc!;
				int capacity = Math.Max(1, npc.Positions.Count);
				if (ordinal < capacity)
					return (npc, ordinal);
				ordinal -= capacity;
			}
			QuestRunNpc fallback = sources[^1].Npc!;
			return (fallback, ordinal % Math.Max(1, fallback.Positions.Count));
		}

		private static async Task UseQuestObjectAsync(
			SimulationL0Session session, int objectId, int itemId, CancellationToken token)
		{
			BotInventoryItem? existing = session.Api.World.Inventory.Values.SingleOrDefault(entry => entry.ItemId == itemId);
			await session.SendPacketAsync(session.Api.TalkTo(objectId), token);
			await session.WaitForPacketAsync(typeof(SM_EMOTION), token,
				packet => packet.Get<int>("senderObjectId") == session.CharacterId &&
					packet.Get<byte>("emotionType") == (byte)EmotionType.START_QUESTLOOT);
			await session.AdvanceAsync(TimeSpan.FromMilliseconds(3001), token);
			await session.WaitForPacketAsync(typeof(SM_EMOTION), token,
				packet => packet.Get<int>("senderObjectId") == session.CharacterId &&
					packet.Get<byte>("emotionType") == (byte)EmotionType.START_QUESTLOOT);
			await session.AdvanceAsync(TimeSpan.FromMilliseconds(3001), token);
			if (existing == null)
			{
				await session.WaitForPacketAsync(typeof(SM_INVENTORY_ADD_ITEM), token,
					packet => packet.Get<List<IReadOnlyDictionary<string, object?>>>("items")
						.Any(entry => Get<int>(entry, "itemId") == itemId));
			}
			else
			{
				await session.WaitForPacketAsync(typeof(SM_INVENTORY_UPDATE_ITEM), token,
					packet => packet.Get<int>("objectId") == existing.ObjectId);
			}
		}

		private static async Task<bool> TryLootCorpseItemAsync(
			SimulationL0Session session, int objectId, int itemId, CancellationToken token)
		{
			await session.SendPacketAsync(session.Api.Loot(objectId), token);
			DecodedBotServerPacket list = await session.WaitForPacketAsync(typeof(SM_LOOT_ITEMLIST), token,
				packet => packet.Get<int>("targetObjectId") == objectId);
			IReadOnlyDictionary<string, object?>? item = list
				.Get<List<IReadOnlyDictionary<string, object?>>>("items")
				.SingleOrDefault(entry => Get<int>(entry, "itemId") == itemId);
			if (item == null)
			{
				await session.SendPacketAsync(session.Api.Loot(objectId, close: true), token);
				return false;
			}

			BotInventoryItem? existing = session.Api.World.Inventory.Values.SingleOrDefault(entry => entry.ItemId == itemId);
			await session.SendPacketAsync(session.Api.Loot(objectId, Get<byte>(item, "index")), token);
			if (existing == null)
			{
				await session.WaitForPacketAsync(typeof(SM_INVENTORY_ADD_ITEM), token,
					packet => packet.Get<List<IReadOnlyDictionary<string, object?>>>("items")
						.Any(entry => Get<int>(entry, "itemId") == itemId));
			}
			else
			{
				await session.WaitForPacketAsync(typeof(SM_INVENTORY_UPDATE_ITEM), token,
					packet => packet.Get<int>("objectId") == existing.ObjectId);
			}
			await session.SendPacketAsync(session.Api.Loot(objectId, close: true), token);
			return true;
		}

		private async Task GatherAsync(QuestRunOperation operation, CancellationToken token)
		{
			QuestRunSource source = operation.Source ?? throw new InvalidDataException("Gather operation has no source.");
			int templateId = source.GatherableId ?? throw new InvalidDataException("Gather source has no template id.");
			if (!player.GetSkillList().IsSkillPresent(30001) || player.GetSkillList().GetSkillLevel(30001) < source.SkillLevel)
				player.GetSkillList().AddSkill(player, 30001, source.SkillLevel);
			player.SetCustomState(CustomPlayerState.NEUTRAL_TO_ALL_NPCS);
			player.GetController().OnChangedPlayerAttributes();
			try
			{
				var attemptedObjects = new HashSet<int>();
				for (int gathered = 0; gathered < operation.Count;)
				{
					QuestRunPosition position = source.Positions[attemptedObjects.Count % source.Positions.Count];
					await owner.TeleportForSetupAsync(session, player, position.MapId,
						position.X - 1, position.Y, position.Z, token);
					var visible = player.GetPosition().GetWorldMapInstance().OfType<Gatherable>()
						.Where(candidate => candidate.GetObjectTemplate().GetTemplateId() == templateId && candidate.IsSpawned() &&
							!attemptedObjects.Contains(candidate.GetObjectId()))
						.OrderBy(candidate => MathF.Pow(candidate.GetX() - player.GetX(), 2) +
							MathF.Pow(candidate.GetY() - player.GetY(), 2))
						.ThenBy(candidate => candidate.GetX()).ThenBy(candidate => candidate.GetY()).ThenBy(candidate => candidate.GetZ())
						.Select(candidate => (Node: candidate, Point: FindVisibleGatherApproach(candidate, player.GetRace())))
						.First(candidate => candidate.Point != null);
					Gatherable gatherable = visible.Node;
					BotPosition approach = visible.Point!.Value;
					attemptedObjects.Add(gatherable.GetObjectId());
					BotInventoryItem? existing = session.Api.World.Inventory.Values
						.SingleOrDefault(entry => entry.ItemId == operation.ItemId);
					await owner.TeleportForSetupAsync(session, player, gatherable.GetWorldId(),
						approach.X, approach.Y, approach.Z, token, gatherable.GetInstanceId());
					await session.MoveToPositionAsync(approach, token);
					Assert.True(Aion.GameServer.World.Geo.GeoService.GetInstance().CanSee(player, gatherable),
						$"Gather setup LOS blocked for {templateId}/{gatherable.GetObjectId()} at {gatherable.GetWorldId()} " +
						$"({gatherable.GetX()}, {gatherable.GetY()}, {gatherable.GetZ()}); player ({player.GetX()}, {player.GetY()}, {player.GetZ()}).");
					foreach (BotClientPacket packet in session.Api.Gather(gatherable.GetObjectId()))
						await session.SendPacketAsync(packet, token);
					DecodedBotServerPacket initial = await session.WaitForPacketAsync(typeof(SM_GATHER_UPDATE), token);
					if (initial.Get<byte>("action") != 0)
						throw new InvalidDataException(
							$"Gatherable {templateId}/{gatherable.GetObjectId()} refused start with action {initial.Get<byte>("action")}.");
					await session.AdvanceAsync(TimeSpan.FromSeconds(60), token);
					DecodedBotServerPacket result = await session.WaitForPacketAsync(typeof(SM_GATHER_UPDATE), token,
						packet => packet.Get<byte>("action") is 6 or 7);
					if (result.Get<byte>("action") != 6)
						continue;
					if (existing == null)
					{
						await session.WaitForPacketAsync(typeof(SM_INVENTORY_ADD_ITEM), token,
							packet => packet.Get<List<IReadOnlyDictionary<string, object?>>>("items")
								.Any(entry => Get<int>(entry, "itemId") == operation.ItemId));
					}
					else
					{
						await session.WaitForPacketAsync(typeof(SM_INVENTORY_UPDATE_ITEM), token,
							packet => packet.Get<int>("objectId") == existing.ObjectId);
					}
					gathered++;
				}
				Assert.Equal(operation.Count, ItemCount(session.Api.World, operation.ItemId));
			}
			finally
			{
				player.UnsetCustomState(CustomPlayerState.NEUTRAL_TO_ALL_NPCS);
				player.GetController().OnChangedPlayerAttributes();
			}
		}

		private static string DescribePacket(DecodedBotServerPacket packet) =>
			packet.Fields.TryGetValue("bodyHex", out object? body)
				? $"{packet.PacketType.Name}({body})"
				: packet.PacketType.Name;

		private async Task WaitForAutoAttackAsync(CancellationToken token) =>
			await session.AdvanceAsync(TimeSpan.FromMilliseconds(
				player.GetGameStats().GetAttackSpeed().GetCurrent() + 1), token);

		private async Task ReportAsync(QuestRunPlan plan, QuestRunOperation operation, CancellationToken token)
		{
			if (plan.Template is not ("report_to_many" or "item_order"))
				return;
			int maxSequence = plan.Steps.Where(step => step.Kind == "report").Max(step => step.Sequence);
			if (operation.Sequence == maxSequence)
				return;
			Npc npc = await MoveToNpcAsync(RequiredNpcs(operation), token);
			await session.SendPacketAsync(session.Api.TalkTo(npc.GetObjectId()), token);
			await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token);
			await session.SendPacketAsync(session.Api.SelectDialog(npc.GetObjectId(), DialogAction.QUEST_SELECT, questId: plan.Id), token);
			await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token);
			ushort action = plan.Template == "report_to_many"
				? checked((ushort)(DialogAction.SETPRO1 + operation.Sequence - 1))
				: checked((ushort)DialogAction.SETPRO1);
			await session.SendPacketAsync(session.Api.SelectDialog(npc.GetObjectId(), action, questId: plan.Id), token);
			await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token);
		}

		private async Task ClaimRewardAsync(QuestRunPlan plan, CancellationToken token)
		{
			Npc npc = await MoveToNpcAsync(plan.EndNpcs, token);
			if (plan.Template is "item_collecting" or "xml_quest")
				await FinishItemQuestAsync(session, npc.GetObjectId(), plan.Id, token);
			else
				await FinishStandardQuestAsync(session, npc.GetObjectId(), plan.Id, token,
					plan.HasSelectableReward ? DialogAction.SELECTED_QUEST_REWARD1 : DialogAction.SELECTED_QUEST_NOREWARD);
		}

		private async Task<Npc> MoveToNpcAsync(IReadOnlyList<QuestRunNpc> npcs, CancellationToken token, int ordinal = 0)
		{
			QuestRunNpc selected = npcs.FirstOrDefault(npc => npc.HandlerSpawned || npc.Positions.Count != 0)
				?? throw new InvalidDataException("Quest operation has no reachable NPC alternative.");
			if (selected.Positions.Count != 0)
			{
				QuestRunPosition position = selected.Positions[ordinal % selected.Positions.Count];
				await owner.TeleportForSetupAsync(session, player, position.MapId, position.X - 1, position.Y, position.Z, token);
			}
			Npc npc = FindLivingNpc(player, selected.Id);
			await MoveBesideAsync(session, npc, token);
			return npc;
		}

		private static IReadOnlyList<QuestRunNpc> RequiredNpcs(QuestRunOperation operation) =>
			operation.Npcs is { Count: > 0 } npcs
				? npcs
				: throw new InvalidDataException($"Quest operation {operation.Kind} has no NPC alternatives.");
	}
}
