using System.Globalization;
using System.Xml.Linq;
using Aion.Bots.Protocol;
using Aion.Bots.Scenarios;
using Aion.Bots.World;
using Aion.GameServer.Ai;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Base;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Siege;
using Aion.GameServer.Model.Templates.Goods;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.Model.Templates.Npc;
using Aion.GameServer.Model.Templates.Spawns.Basespawns;
using Aion.GameServer.Model.Templates.Spawns.Siegespawns;
using Aion.GameServer.Model.Templates.Tradelist;
using Aion.GameServer.Model.Templates.Zone;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Abyss;
using Aion.GameServer.Services.Instance;
using Aion.GameServer.Services.Items;
using Aion.GameServer.Services.Panesterra.Ahserion;
using Aion.GameServer.Services.Trade;
using Aion.GameServer.TestKit;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.Stats;
using Aion.GameServer.World;

namespace Aion.Simulation.Tests;

public sealed partial class SimulationFastScenarioTests
{
	private async Task RunTradeSweepAsync(ScenarioDefinition scenario, bool includeHistory)
	{
		string root = RealStaticData.RepoRoot();
		var rows = XDocument.Load(Path.Combine(root, "game-server/data/static_data/npc_trade_list.xml")).Root!.Elements()
			.Select(e => (Kind: e.Name.LocalName, NpcId: (int)e.Attribute("npc_id")!))
			.OrderBy(r => r.Kind == "tradelist_template" ? 0 : r.Kind == "trade_in_list_template" ? 1 : 2).ThenBy(r => r.NpcId).ToArray();
		Assert.NotEmpty(rows); Assert.Equal(DataManager.TRADE_LIST_DATA.Size(), rows.Count(r => r.Kind == "tradelist_template"));
		Assert.All(rows, r => Assert.Contains(r.Kind, new[] { "tradelist_template", "trade_in_list_template", "purchase_template" }));
		var sourceSpawns = Directory.EnumerateFiles(Path.Combine(root, "game-server/data/static_data/spawns"), "*.xml", SearchOption.AllDirectories)
			.SelectMany(p => XDocument.Load(p).Descendants("spawn")).Select(e => (Id: (int?)e.Attribute("npc_id") ?? 0,
				Times: e.Descendants("temporary_spawn").Attributes("spawn_time").Select(a => a.Value).ToArray())).ToArray();
		var declared = sourceSpawns.Select(s => s.Id).ToHashSet();
		var spawnTimes = sourceSpawns.GroupBy(s => s.Id).ToDictionary(g => g.Key, g => g.SelectMany(s => s.Times).Distinct().ToArray());
		var report = new DataSweepReport("tradelists", rows.Select(r => Key(r.Kind, r.NpcId)));
		string reportPath = Path.Combine(Environment.GetEnvironmentVariable("AION_E2E_RUN_DIR")
			?? throw new InvalidOperationException("Sweep requires a run directory."), "data-sweeps", "tradelists.json");
		report.Save(reportPath);
		using var policy = NewEconomyPolicy(scenario.Id, includeHistory);
		using var timeout = new CancellationTokenSource(TimeSpan.FromHours(1));
		await using var elyos = new SimulationL0Session(fixture, policy, "b01", 129, "Elytradesweep");
		await using var asmodian = new SimulationL0Session(fixture, policy, "b02", 130, "Asmotradesweep", Race.ASMODIANS);
		var subjects = new[] { elyos, asmodian };
		foreach (var subject in subjects)
		{
			subject.BeginStep("setup", "ordinary-character-and-director-level-legion-funds");
			await subject.LoginAndAuthenticateAsync(timeout.Token); await subject.CreateCharacterAsync(timeout.Token);
			await subject.EnterWorldAsync(timeout.Token); await subject.SynchronizeAsync(timeout.Token);
			var actor = fixture.World.GetPlayer(subject.CharacterId);
			Assert.True(ClassChangeService.SetClass(actor, PlayerClass.GLADIATOR, validate: true, updateDaevaStatus: true));
			actor.GetCommonData().SetLevel(65);
			Assert.Equal(0, ItemService.AddItem(actor, BotWorldModel.KinahItemId, 1_000_000_000));
			LegionService.GetInstance().CreateLegion(actor, subject == elyos ? "Elytrades" : "Asmotrades");
			Assert.NotNull(actor.GetLegion()); actor.GetLegion().SetLegionLevel(8);
			Assert.Equal(0, actor.AccessLevel); Assert.False(actor.IsStaff());
		}
		var session = elyos;
		var instances = new Dictionary<int, WorldMapInstance>();
		int completed = 0;
		foreach (var row in rows)
		{
			string key = Key(row.Kind, row.NpcId);
			using var rowPolicy = NewEconomyPolicy(scenario.Id, includeHistory: false);
			using var rowTimeout = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token);
			rowTimeout.CancelAfter(TimeSpan.FromSeconds(60)); var token = rowTimeout.Token;
			var details = new Dictionary<string, string> { ["npcId"] = Id(row.NpcId), ["kind"] = row.Kind,
				["countryCode"] = Id(Aion.GameServer.Configs.Main.GSConfig.SERVER_COUNTRY_CODE) };
			var restore = new Stack<Action>(); bool logChecked = false;
			try
			{
				TradeListTemplate list = row.Kind switch
				{
					"tradelist_template" => DataManager.TRADE_LIST_DATA.GetTradeListTemplate(row.NpcId),
					"trade_in_list_template" => DataManager.TRADE_LIST_DATA.GetTradeInListTemplate(row.NpcId),
					_ => DataManager.TRADE_LIST_DATA.GetPurchaseTemplate(row.NpcId)
				};
				Assert.NotNull(list); Assert.NotEmpty(list.GetTradeTablist());
				details["npcType"] = list.GetTradeNpcType().ToString();
				int requiredAction = row.Kind switch { "tradelist_template" => DialogAction.BUY, "trade_in_list_template" => DialogAction.TRADE_IN, _ => DialogAction.TRADE_SELL_LIST };
				var npcTemplate = DataManager.NPC_DATA.GetNpcTemplate(row.NpcId);
				if (npcTemplate != null && !npcTemplate.SupportsAction(requiredAction))
				{
					details["unsupportedAction"] = Id(requiredAction);
					logChecked = true; rowPolicy.AssertClean();
					report.Record(key, DataSweepStatus.Inactive, $"Shipped NPC exposes no action {requiredAction}; catalog is inactive content, not a successful transaction (§7 #59)", details);
					continue;
				}
				var candidates = FindTradeSweepNpcs(row.NpcId, subjects.Select(s => fixture.World.GetPlayer(s.CharacterId)).ToArray(), instances, restore, details,
					spawnTimes.GetValueOrDefault(row.NpcId) ?? []);
				if (candidates.Count == 0)
				{
					Assert.False(declared.Contains(row.NpcId), $"Trade NPC {row.NpcId} has shipped spawns but none materialized.");
					logChecked = true; rowPolicy.AssertClean();
					report.Record(key, DataSweepStatus.Unreachable, "No shipped spawn definition or runtime spawn for this catalog NPC", details);
					continue;
				}
				// Static Panesterra temple vendors need faction membership too, not only
				// vendors materialized by the siege/base services. Use their shipped tribe.
				var vendorFaction = Enum.GetValues<PanesterraFaction>().Where(f => f is not (PanesterraFaction.BALAUR or PanesterraFaction.PEACE))
					.Cast<PanesterraFaction?>().FirstOrDefault(f => candidates.Any(n => n.GetTribe() == f!.Value.GetTribe()));
				if (vendorFaction is { } faction)
				{
					var actors = subjects.Select(s => fixture.World.GetPlayer(s.CharacterId)).ToArray();
					var oldFactions = actors.Select(p => p.GetPanesterraFaction()).ToArray();
					restore.Push(() => { for (int i = 0; i < actors.Length; i++) actors[i].SetPanesterraFaction(oldFactions[i]); });
					foreach (var actor in actors) actor.SetPanesterraFaction(faction);
					details["setupPanesterraFaction"] = faction.ToString();
				}
				var npc = candidates.OrderByDescending(n => subjects.Max(s => Affinity(n, s)))
					.ThenBy(n => n.GetWorldId()).ThenBy(n => n.GetInstanceId()).ThenBy(n => n.GetObjectId()).First();
				session = subjects.OrderByDescending(s => Affinity(npc, s)).First();
				Assert.True(Affinity(npc, session) > 0, $"No non-hostile subject for trade NPC {row.NpcId}.");
				var player = fixture.World.GetPlayer(session.CharacterId);
				session.BeginStep(key, "normal-catalog-purchase-and-sale");
				player.GetCommonData().SetLevel(65);
				// Independent catalog rows: remove the previous row's supplied inputs/products, not transaction effects.
				foreach (var item in player.GetInventory().GetItems().ToArray())
					Assert.True(player.GetInventory().DecreaseByObjectId(item.GetObjectId(), item.GetItemCount()));
				RepurchaseService.GetInstance().RemoveRepurchaseItems(player);
				PrepareTradeSweepDialog(player, npc, restore);
				await TeleportForSetupAsync(session, player, npc.GetWorldId(), npc.GetX() - 5, npc.GetY(), npc.GetZ(), token, npc.GetInstanceId());
				await session.MoveToPositionAsync(new BotPosition(npc.GetX() - 1, npc.GetY(), npc.GetZ(), 0), token);
				// Approach wakes the region and can start a guard's fight. Settle it after
				// movement, then approach its actual post-return position again.
				await session.AdvanceAsync(TimeSpan.FromSeconds(1), token);
				await ClearTradeSweepCombatAsync(session, npc, details, token);
				await session.MoveToPositionAsync(new BotPosition(npc.GetX() - 1, npc.GetY(), npc.GetZ(), 0), token);
				Assert.False(player.IsDead(), $"Trade subject died approaching NPC {row.NpcId}.");
				Assert.True(DialogService.IsInteractionAllowed(player, npc));
				PacketSendUtility.SendPacket(player, new SM_PRICES());
				await session.SynchronizeAsync(token);
				details["mapId"] = Id(npc.GetWorldId()); details["instanceId"] = Id(npc.GetInstanceId()); details["race"] = player.GetRace().ToString();
				var goods = list.GetTradeTablist().Select(tab => row.Kind switch
				{
					"tradelist_template" => DataManager.GOODSLIST_DATA.GetGoodsListById(tab.GetId()),
					"trade_in_list_template" => DataManager.GOODSLIST_DATA.GetGoodsInListById(tab.GetId()),
					_ => DataManager.GOODSLIST_DATA.GetGoodsPurchaseListById(tab.GetId())
				}).ToArray();
				Assert.All(goods, g => Assert.NotNull(g));
				var offered = goods.SelectMany(g => g.GetItemIdList()).Distinct().Select(id => DataManager.ITEM_DATA.GetItemTemplate(id)).ToArray();
				Assert.NotEmpty(offered); Assert.All(offered, i => Assert.NotNull(i));
				var product = offered.OrderBy(i => i.GetPrice()).ThenBy(i => i.GetTemplateId()).First();
				details["tabs"] = string.Join(',', goods.Select(g => g.GetId()));
				await session.SendPacketAsync(session.Api.Target(npc.GetObjectId()), token);
				details["dialogAiState"] = npc.GetAi().GetState().ToString();
				await session.SendPacketAsync(session.Api.TalkTo(npc.GetObjectId()), token);
				await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token, p => p.Get<int>("targetObjectId") == npc.GetObjectId());
				if (row.Kind != "purchase_template")
				{
					bool tradeIn = row.Kind == "trade_in_list_template";
					Assert.True(tradeIn ? npc.CanTradeIn() : npc.CanSell());
					await session.SendPacketAsync(session.Api.SelectDialog(npc.GetObjectId(), (ushort)(tradeIn ? DialogAction.TRADE_IN : DialogAction.BUY)), token);
					var window = await session.WaitForPacketAsync(tradeIn ? typeof(SM_TRADE_IN_LIST) : typeof(SM_TRADELIST), token, p => p.Get<int>("targetObjectId") == npc.GetObjectId());
					Assert.Equal(goods.Select(g => g.GetId()).Order(), window.Get<int[]>("tabs").Order());
					int sellRate = list.GetTradeNpcType() == TradeNpcType.ABYSS_KINAH ? list.GetSellPriceRate2() : list.GetSellPriceRate();
					int apRate = list.GetTradeNpcType() == TradeNpcType.ABYSS_KINAH ? list.GetApSellPriceRate2() : list.GetSellPriceRate();
					int apCost = ApPrice(product, apRate);
					long kinahCost = !tradeIn && list.GetTradeNpcType() is TradeNpcType.NORMAL or TradeNpcType.ABYSS_KINAH
						? session.Api.World.VendorPrices!.BuyListPrice(product.GetPrice(), PricesService.GetVendorBuyModifier(), sellRate, 1) : 0;
					var materials = new Dictionary<int, long>();
					if (tradeIn)
					{
						Assert.NotNull(product.GetTradeinList()); var inputs = product.GetTradeinList().GetTradeinItem(); Assert.NotNull(inputs); Assert.NotEmpty(inputs);
						foreach (var input in inputs) materials[input.GetId()] = materials.GetValueOrDefault(input.GetId()) + input.GetPrice();
						apCost = Math.Max(0, apCost - inputs.Sum(i => ApPrice(DataManager.ITEM_DATA.GetItemTemplate(i.GetId()), apRate)));
					}
					else if (product.GetAcquisition() is { } acquisition && acquisition.GetItemId() != 0)
						materials.Add(acquisition.GetItemId(), acquisition.GetItemCount());
					foreach (var material in materials) Assert.Equal(0, ItemService.AddItem(player, material.Key, material.Value));
					if (player.GetInventory().GetKinah() < kinahCost) Assert.Equal(0, ItemService.AddItem(player, BotWorldModel.KinahItemId, kinahCost));
					if (player.GetAbyssRank().GetAp() < apCost) AbyssPointsService.AddAp(player, apCost - player.GetAbyssRank().GetAp());
					// AP setup/purchases can recalculate rank. Re-establish the explicit director
					// prerequisite before each independent transaction, never bypass its check.
					PrepareTradeSweepRank(player, npc);
					PacketSendUtility.SendPacket(player, new SM_ABYSS_RANK(player));
					await session.SynchronizeAsync(token);
					var expected = Totals(); int expectedAp = player.GetAbyssRank().GetAp() - apCost; Assert.True(expectedAp >= 0);
					if (tradeIn)
						await session.SendPacketAsync(GameClientPackets.BuyTradeIn(npc.GetObjectId(), 0, product.GetTemplateId(), 1,
							materials.Keys.Select(id => player.GetInventory().GetItemsByItemId(id).First().GetObjectId()).ToArray()), token);
					else
						await session.SendPacketAsync(GameClientPackets.BuyItem(npc.GetObjectId(), (short)(list.GetTradeNpcType() switch
						{ TradeNpcType.ABYSS => 14, TradeNpcType.REWARD => 15, TradeNpcType.ABYSS_KINAH => 16, _ => 13 }), [(product.GetTemplateId(), 1)]), token);
					Adjust(expected, BotWorldModel.KinahItemId, -kinahCost);
					foreach (var material in materials) Adjust(expected, material.Key, -material.Value);
					Adjust(expected, product.GetTemplateId(), 1);
					await VerifyAsync(player, expected, expectedAp, token);
					details["productId"] = Id(product.GetTemplateId()); details["productCount"] = "1";
					details["buyKinah"] = Id(kinahCost); details["buyAp"] = Id(apCost);
					details["components"] = string.Join(',', materials.Select(m => $"{m.Key}:{m.Value}"));
					details["buyObserved"] = "true";
				}
				if (row.Kind == "purchase_template" || npc.CanBuy() || npc.CanPurchase())
				{
					var purchase = DataManager.TRADE_LIST_DATA.GetPurchaseTemplate(row.NpcId);
					if (row.Kind == "purchase_template")
					{
						await session.SendPacketAsync(session.Api.SelectDialog(npc.GetObjectId(), (ushort)DialogAction.TRADE_SELL_LIST), token);
						var window = await session.WaitForPacketAsync(typeof(SM_SELL_ITEM), token, p => p.Get<int>("targetObjectId") == npc.GetObjectId());
						Assert.Equal(goods.Select(g => g.GetId()).Order(), window.Get<int[]>("tabIds").Order());
					}
					var sale = purchase == null ? DataManager.ITEM_DATA.GetItemTemplate(VendorScenario.ItemId)
						: purchase.GetTradeTablist().SelectMany(t => DataManager.GOODSLIST_DATA.GetGoodsPurchaseListById(t.GetId()).GetItemIdList())
							.Select(id => DataManager.ITEM_DATA.GetItemTemplate(id)).OrderBy(i => i.GetPrice()).ThenBy(i => i.GetTemplateId()).First();
					Assert.True(npc.CanBuy() || npc.CanPurchase());
					Assert.Equal(0, ItemService.AddItem(player, sale.GetTemplateId(), 1));
					PrepareTradeSweepRank(player, npc);
					PacketSendUtility.SendPacket(player, new SM_ABYSS_RANK(player)); await session.SynchronizeAsync(token);
					var expected = Totals(); int expectedAp = player.GetAbyssRank().GetAp();
					long kinahReward = 0; int apReward = 0;
					if (purchase?.GetTradeNpcType() == TradeNpcType.ABYSS)
						apReward = (int)Math.Floor(sale.GetAcquisition().GetRequiredAp() * purchase.GetBuyPriceRate() / 100F + 0.5f);
					else kinahReward = purchase == null ? BotVendorPrices.SellPrice(sale.GetPrice(), PricesService.GetVendorSellModifier())
						: (long)(sale.GetPrice() * purchase.GetBuyPriceRate() / 100D);
					var item = player.GetInventory().GetItemsByItemId(sale.GetTemplateId()).First();
					await session.SendPacketAsync(session.Api.Sell(npc.GetObjectId(), [(item.GetObjectId(), 1)]), token);
					Adjust(expected, sale.GetTemplateId(), -1); Adjust(expected, BotWorldModel.KinahItemId, kinahReward);
					await VerifyAsync(player, expected, expectedAp + apReward, token);
					details["sellObserved"] = "true"; details["soldItemId"] = Id(sale.GetTemplateId());
					details["sellKinah"] = Id(kinahReward); details["sellAp"] = Id(apReward);
				}
				else details["sellNotApplicable"] = "NPC exposes no sell/purchase action";
				await session.SendPacketAsync(session.Api.CloseDialog(npc.GetObjectId()), token); await session.SynchronizeAsync(token);
				Assert.False(player.IsDead()); Assert.Equal(0, player.AccessLevel);
				logChecked = true; rowPolicy.AssertClean();
				report.Record(key, DataSweepStatus.Passed, "Catalog transaction(s), exact currencies/materials/products and complete packet/server inventory verified", details);
				session.PacketHistory.Clear(); session.PacketObservations.Clear();
				completed++; if (completed % 50 == 0) Console.WriteLine($"SWEEP-TRADE {completed} passed: {key}");
			}
			catch (Exception error)
			{
				var failedSubject = fixture.World.GetPlayer(session.CharacterId);
				details["subjectDead"] = failedSubject.IsDead().ToString();
				details["subjectHp"] = Id(failedSubject.GetLifeStats().GetCurrentHp());
				details["recentPackets"] = string.Join('\n', session.PacketHistory.TakeLast(20).Select(p => p.PacketType.Name + " " + System.Text.Json.JsonSerializer.Serialize(p.Fields)));
				if (!logChecked) { try { rowPolicy.AssertClean(); } catch (Exception logError) { details["logFailure"] = logError.ToString(); } }
				report.Record(key, DataSweepStatus.Failed, error.ToString(), details);
				Console.WriteLine($"SWEEP-TRADE {key}: failed\n" + System.Text.Json.JsonSerializer.Serialize(details)); throw;
			}
			finally { try { while (restore.TryPop(out var action)) action(); } finally { report.Save(reportPath); } }
		}
		Assert.True(report.Complete);
		foreach (var subject in subjects) { await subject.QuitAsync(timeout.Token); await subject.VerifyOfflineAsync(timeout.Token); }
		policy.AssertClean();
		// Prefer the vendor's friendly faction over neutral visitors in guarded towns;
		// genuinely neutral vendors (e.g. Varshaka) still use the ordinary trade path.
		int Affinity(Npc npc, SimulationL0Session subject) => npc.GetType_(fixture.World.GetPlayer(subject.CharacterId)) switch
		{
			CreatureType.FRIEND or CreatureType.SUPPORT => 2,
			CreatureType.PEACE => 1,
			_ => 0
		};
		Dictionary<int, long> Totals() => session.Api.World.Inventory.Values.GroupBy(i => i.ItemId).ToDictionary(g => g.Key, g => g.Sum(i => i.Count));
		async Task VerifyAsync(Player player, Dictionary<int, long> expected, int ap, CancellationToken token)
		{
			await session.SynchronizeAsync(token);
			Assert.Equal(expected.OrderBy(p => p.Key), Totals().OrderBy(p => p.Key));
			Assert.Equal(expected.OrderBy(p => p.Key), player.GetInventory().GetItemsWithKinah().Concat(player.GetEquipment().GetEquippedItems())
				.GroupBy(i => i.GetItemId()).ToDictionary(g => g.Key, g => g.Sum(i => i.GetItemCount())).OrderBy(p => p.Key));
			Assert.Equal(ap, player.GetAbyssRank().GetAp()); Assert.Equal(ap, session.Api.World.AbyssRank!.Ap);
		}
		static void Adjust(Dictionary<int, long> expected, int id, long amount)
		{
			expected[id] = expected.GetValueOrDefault(id) + amount;
			if (expected[id] == 0 && id != BotWorldModel.KinahItemId) expected.Remove(id);
		}
		static int ApPrice(ItemTemplate item, int rate) => item.GetAcquisition() is { } a && a.GetType_() is AcquisitionType.AP or AcquisitionType.ABYSS
			? (int)(a.GetRequiredAp() * rate / 100D * PricesService.GetVendorBuyModifier()) / 100 : 0;
		static string Key(string kind, int id) => kind + ":" + id.ToString(CultureInfo.InvariantCulture);
		static string Id(long value) => value.ToString(CultureInfo.InvariantCulture);
	}

	private List<Npc> FindTradeSweepNpcs(int npcId, Player[] players, Dictionary<int, WorldMapInstance> instances,
		Stack<Action> restore, Dictionary<string, string> details, IReadOnlyList<string> spawnTimes)
	{
		var candidates = new List<Npc>();
		void Scan() => fixture.World.ForEachObject(o => { if (o is Npc n && n.IsSpawned() && n.GetNpcId() == npcId) candidates.Add(n); });
		Scan(); if (candidates.Count > 0) return candidates;
		foreach (int map in DataManager.WORLD_MAPS_DATA.Select(m => m.GetMapId()).Order())
		{
			var groups = DataManager.SPAWNS_DATA.GetSpawnsForNpc(map, npcId).Where(g => g.GetSpawnTemplates().Count > 0).ToArray();
			if (groups.Length == 0) continue;
			if (groups.Any(g => g.GetSpawnTemplates().Any(s => s.GetTemporarySpawn() != null)))
			{
				// Same director operation as //time: retain the chosen hour for this isolated
				// sweep, and run the real hour-change callbacks. Never fabricate a spawn.
				var gameTime = GameTimeService.GetInstance().GetGameTime();
				for (int hour = 0; hour < 24; hour++)
				{
					gameTime.AddMinutes((hour - gameTime.GetHour()) * 60 - gameTime.GetMinute());
					Scan();
					if (candidates.Count > 0)
					{
						details["setupGameHour"] = hour.ToString(CultureInfo.InvariantCulture);
						return candidates;
					}
				}
				foreach (string expression in spawnTimes)
				{
					int nextTime = NextTradeSweepGameTime(expression, gameTime.GetTime());
					gameTime.AddMinutes(nextTime - gameTime.GetTime());
					Scan();
					if (candidates.Count > 0)
					{
						details["setupGameDate"] = $"{gameTime.GetYear()}:{gameTime.GetMonth()}:{gameTime.GetDay()}:{gameTime.GetHour()}";
						return candidates;
					}
				}
			}
			if (!fixture.World.GetWorldMap(map).IsInstanceType()) continue;
			if (!instances.TryGetValue(map, out var instance)) instances.Add(map, instance = InstanceService.GetNextAvailableInstance(map, 0, 0, 1, autoDestroy: false));
			candidates.AddRange(instance.OfType<Npc>().Where(n => n.IsSpawned() && n.GetNpcId() == npcId));
			if (candidates.Count > 0) return candidates;
		}
		var sieges = SiegeService.GetInstance();
		var siegeSpawn = sieges.GetSiegeLocations().Keys.Order().SelectMany(id => DataManager.SPAWNS_DATA.GetSiegeSpawnsByLocId(id) ?? [])
			.Where(g => g.GetNpcId() == npcId).SelectMany(g => g.GetSpawnTemplates()).OfType<SiegeSpawnTemplate>()
			.FirstOrDefault(s => s.GetSiegeModType() == SiegeModType.PEACE && s.GetSiegeRace() is SiegeRace.ELYOS or SiegeRace.ASMODIANS);
		if (siegeSpawn != null)
		{
			int id = siegeSpawn.GetSiegeId(); var loc = sieges.GetSiegeLocation(id); var oldRace = loc.GetRace();
			Assert.Empty(fixture.World.GetLocalSiegeNpcs(id));
			restore.Push(() => { sieges.DeSpawnNpcs(id); loc.SetRace(oldRace); });
			loc.SetRace(siegeSpawn.GetSiegeRace()); sieges.SpawnNpcs(id, siegeSpawn.GetSiegeRace(), SiegeModType.PEACE);
			details["setupSiegeId"] = id.ToString(CultureInfo.InvariantCulture); Scan(); if (candidates.Count > 0) return candidates;
		}
		var bases = BaseService.GetInstance();
		var baseSpawn = bases.GetBaseLocations().OrderBy(b => b.GetId()).SelectMany(b => DataManager.SPAWNS_DATA.GetBaseSpawnsByLocId(b.GetId()) ?? [])
			.Where(g => g.GetNpcId() == npcId).SelectMany(g => g.GetSpawnTemplates()).OfType<BaseSpawnTemplate>()
			.FirstOrDefault(s => s.GetOccupier() is not (BaseOccupier.BALAUR or BaseOccupier.PEACE));
		if (baseSpawn != null)
		{
			int id = baseSpawn.GetId(); var loc = bases.GetBaseLocation(id); var oldOccupier = loc.GetOccupier(); bool active = bases.IsActive(id);
			var factions = players.Select(p => p.GetPanesterraFaction()).ToArray();
			restore.Push(() =>
			{
				try { if (active) bases.Capture(id, oldOccupier); else { bases.Stop(id); loc.SetOccupier(oldOccupier); } }
				finally { for (int i = 0; i < players.Length; i++) players[i].SetPanesterraFaction(factions[i]); }
			});
			if (baseSpawn.GetOccupier().GetPanesterraFaction() is { } faction) foreach (var player in players) player.SetPanesterraFaction(faction);
			if (active) bases.Capture(id, baseSpawn.GetOccupier()); else { loc.SetOccupier(baseSpawn.GetOccupier()); bases.Start(id); }
			details["setupBaseId"] = id.ToString(CultureInfo.InvariantCulture); Scan();
		}
		return candidates;
	}

	internal static int NextTradeSweepGameTime(string expression, int currentMinutes)
	{
		// Aion's calendar has twelve 31-day months. This only selects a shipped
		// spawn boundary; GameTime.AddMinutes invokes the real temporary-spawn service.
		string[] parts = expression.Split('.');
		if (parts.Length != 3) throw new InvalidDataException($"Invalid temporary-spawn expression {expression}.");
		const int yearMinutes = 12 * 31 * 24 * 60;
		int yearStart = currentMinutes / yearMinutes * yearMinutes;
		var times = from month in Enumerable.Range(1, 12)
			from day in Enumerable.Range(1, 31)
			from hour in Enumerable.Range(0, 24)
			where Matches(parts[0], hour) && Matches(parts[1], day) && Matches(parts[2], month)
			let time = yearStart + ((month - 1) * 31 + day - 1) * 24 * 60 + hour * 60
			select time > currentMinutes ? time : checked(time + yearMinutes);
		return times.DefaultIfEmpty(-1).Min() is var selected && selected >= 0 ? selected
			: throw new InvalidDataException($"No game-calendar date matches {expression}.");
		static bool Matches(string part, int value) => part == "*" || (part.StartsWith('/')
			? value % int.Parse(part[1..], CultureInfo.InvariantCulture) == 0 : value == int.Parse(part, CultureInfo.InvariantCulture));
	}

	private static void PrepareTradeSweepDialog(Player player, Npc npc, Stack<Action> restore)
	{
		var talk = npc.GetObjectTemplate().GetTalkInfo(); if (talk?.GetSubDialogType() == null) return;
		int value = talk.GetSubDialogValue();
		switch (talk.GetSubDialogType())
		{
			case SubDialogType.SKILL_ID: player.GetSkillList().AddSkill(player, value, 1); break;
			case SubDialogType.ITEM_ID: Assert.Equal(0, ItemService.AddItem(player, value, 1)); break;
			case SubDialogType.RETURN: Assert.Equal(0, ItemService.AddItem(player, 164000335, 1)); break;
			case SubDialogType.ABYSSRANK: player.GetAbyssRank().SetRank(AbyssRankEnumExtensions.GetRankById(value)); break;
			case SubDialogType.TARGET_LEGION_DOMINION: player.GetLegion().SetCurrentLegionDominion(value); break;
			case SubDialogType.LEGION_DOMINION_NPC: player.GetLegion().SetOccupiedLegionDominion(value); break;
			case SubDialogType.LEVEL:
			case SubDialogType.LEVEL_LOW:
			case SubDialogType.LEVEL_HIGH: player.GetCommonData().SetLevel(value); break;
			case SubDialogType.FORT_CAPTURE:
				var zone = npc.FindZones().First(z => z.GetZoneTemplate().GetZoneType() == ZoneClassName.FORT);
				var siegeIds = zone.GetZoneTemplate().GetSiegeId(); Assert.NotNull(siegeIds); Assert.NotEmpty(siegeIds);
				var fort = siegeIds.Select(id => SiegeService.GetInstance().GetFortress(id)).First(f => f != null);
				var race = fort.GetRace(); int legion = fort.GetLegionId();
				restore.Push(() => { fort.SetRace(race); fort.SetLegionId(legion); });
				fort.SetRace(player.GetRace() == Race.ELYOS ? SiegeRace.ELYOS : SiegeRace.ASMODIANS); fort.SetLegionId(player.GetLegion().GetLegionId());
				break;
		}
	}

	private static void PrepareTradeSweepRank(Player player, Npc npc)
	{
		var talk = npc.GetObjectTemplate().GetTalkInfo();
		if (talk?.GetSubDialogType() == SubDialogType.ABYSSRANK)
			player.GetAbyssRank().SetRank(AbyssRankEnumExtensions.GetRankById(talk.GetSubDialogValue()));
	}

	private static async Task ClearTradeSweepCombatAsync(SimulationL0Session session, Npc vendor,
		Dictionary<string, string> details, CancellationToken token)
	{
		var cleared = new List<int>();
		var states = new List<string>();
		for (int second = 0; second < 30; second++)
		{
			states.Add(FormattableString.Invariant($"{second}:{vendor.GetAi().GetState()}:{vendor.GetX()},{vendor.GetY()},{vendor.GetZ()}:canMove={vendor.CanPerformMove()}:speed={vendor.GetGameStats().GetMovementSpeedFloat()}:target={vendor.GetTarget()?.GetObjectId()}"));
			Assert.False(vendor.IsDead(), $"Trade NPC {vendor.GetNpcId()} died before its catalog could be tested.");
			if (vendor.GetAi().GetState() is AIState.IDLE or AIState.WALKING)
			{
				if (cleared.Count > 0) details["setupClearedThreats"] = string.Join(',', cleared);
				return;
			}
			// Director //kill-equivalent setup only: remove actual nearby hostile NPCs,
			// then let normal aggro/return timers settle. Never force the vendor's AI state.
			foreach (var threat in vendor.GetKnownList().Stream().Select(r => r.Get()).OfType<Npc>()
				.Where(n => !n.IsDead() && vendor.IsEnemy(n) && PositionUtil.IsInRange(vendor, n, 40)).ToArray())
			{
				cleared.Add(threat.GetNpcId());
				// Physical GM damage has no skill effect (the legacy signature is not nullable-annotated).
				threat.GetController().OnAttack(vendor, null!, SmAttackStatus.TYPE.REGULAR, threat.GetLifeStats().GetMaxHp(), true,
					SmAttackStatus.LOG.REGULAR, null, Aion.GameServer.SkillEngine.Model.HopType.DAMAGE);
			}
			await session.AdvanceAsync(TimeSpan.FromSeconds(1), token);
		}
		details["combatSetupStates"] = string.Join(';', states);
		details["setupClearedThreats"] = string.Join(',', cleared);
		throw new InvalidDataException($"Trade NPC {vendor.GetNpcId()} did not leave combat after director setup; state={vendor.GetAi().GetState()}.");
	}
}
