using System.Globalization;
using System.Xml.Linq;
using Aion.Bots.Scenarios;
using Aion.Bots.World;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Items;
using Aion.GameServer.TestKit;

namespace Aion.Simulation.Tests;

public sealed partial class SimulationFastScenarioTests
{
	private async Task RunCraftSweepAsync(ScenarioDefinition scenario, bool includeHistory)
	{
		int[] ids = XDocument.Load(Path.Combine(RealStaticData.RepoRoot(), "game-server/data/static_data/recipe/recipe_templates.xml"))
			.Root!.Elements("recipe_template").Select(e => (int)e.Attribute("id")!).Order().ToArray();
		Assert.NotEmpty(ids);
		Assert.Equal(ids, DataManager.RECIPE_DATA.GetRecipeTemplates().Select(r => r.GetId()).Order());
		var report = new DataSweepReport("recipes", ids.Select(Id));
		string reportPath = Path.Combine(Environment.GetEnvironmentVariable("AION_E2E_RUN_DIR")
			?? throw new InvalidOperationException("Sweep requires a run directory."), "data-sweeps", "recipes.json");
		report.Save(reportPath);
		using var policy = NewEconomyPolicy(scenario.Id, includeHistory);
		using var timeout = new CancellationTokenSource(TimeSpan.FromHours(2));
		await using var elyos = new SimulationL0Session(fixture, policy, "b01", 123, "Elycraftsweep");
		await using var asmodian = new SimulationL0Session(fixture, policy, "b02", 124, "Asmocraftsweep", Race.ASMODIANS);
		foreach (var subject in new[] { elyos, asmodian })
		{
			subject.BeginStep("setup", "ordinary-character-and-gm-level-setup");
			await subject.LoginAndAuthenticateAsync(timeout.Token); await subject.CreateCharacterAsync(timeout.Token);
			await subject.EnterWorldAsync(timeout.Token); await subject.SynchronizeAsync(timeout.Token);
			var actor = fixture.World.GetPlayer(subject.CharacterId);
			Assert.True(ClassChangeService.SetClass(actor, PlayerClass.GLADIATOR, validate: true, updateDaevaStatus: true));
			actor.GetCommonData().SetLevel(65);
			Assert.Equal(0, actor.AccessLevel); Assert.False(actor.IsStaff());
		}
		var stations = new List<StaticObject>();
		fixture.World.ForEachObject(o => { if (o is StaticObject s && s.IsSpawned() && s.GetWorldId() is 110010000 or 120010000) stations.Add(s); });
		Assert.NotEmpty(stations);
		var session = elyos;
		int failureChance = CraftConfig.MAX_CRAFT_FAILURE_CHANCE;
		CraftConfig.MAX_CRAFT_FAILURE_CHANCE = 0;
		int completed = 0;
		try
		{
			foreach (int id in ids)
			{
				using var rowTimeout = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token);
				rowTimeout.CancelAfter(TimeSpan.FromSeconds(60)); var token = rowTimeout.Token;
				using var rowPolicy = NewEconomyPolicy(scenario.Id, includeHistory: false);
				var details = new Dictionary<string, string>();
				bool logChecked = false;
				try
				{
					var recipe = DataManager.RECIPE_DATA.GetRecipeTemplateById(id);
					session = recipe.GetRace() == Race.ASMODIANS ? asmodian : elyos;
					var player = fixture.World.GetPlayer(session.CharacterId);
					session.BeginStep($"craft-{id}", "craft-shipped-recipe-once");
					int map = player.GetRace() == Race.ASMODIANS ? 120010000 : 110010000;
					int skill = recipe.GetSkillId();
					int stationTemplate = skill switch
					{
						40001 => 150000009, 40002 => 150000011, 40003 => 150000010, 40004 => 150000015,
						40007 => 150000013, 40008 => 150000012, 40009 => 0, 40010 => 150000023,
						_ => throw new InvalidDataException($"Unmapped craft skill {skill} in recipe {id}."),
					};
					// Morphing needs no station; a shipped oven is only a safe standing location in that case.
					var station = stations.Where(s => s.GetWorldId() == map && s.GetObjectTemplate().GetTemplateId() ==
						(stationTemplate == 0 ? 150000009 : stationTemplate)).OrderBy(s => s.GetObjectId()).First();
					details["mapId"] = Id(map); details["instanceId"] = Id(station.GetInstanceId());
					details["skillId"] = Id(skill); details["stationTemplateId"] = Id(stationTemplate);
					details["race"] = player.GetRace().ToString();
					if (player.GetWorldId() != map || Math.Abs(player.GetX() - station.GetX()) > 4 || Math.Abs(player.GetY() - station.GetY()) > 4)
					{
						await TeleportForSetupAsync(session, player, map, station.GetX() - 5, station.GetY(), station.GetZ(), token, station.GetInstanceId());
						await session.MoveToPositionAsync(new BotPosition(station.GetX() - 1, station.GetY(), station.GetZ(), 0), token);
					}
					// Each row starts from an independent GM-prepared prerequisite state. This does not test
					// repeated crafting during cooldown: the new cooldown is verified before fixture cleanup.
					player.GetCraftCooldowns().Clear();
					// Some shipped quest recipes require 550 even though ordinary master progression caps at 549.
					// This data sweep explicitly grants recipe prerequisites; it is not progression coverage.
					int skillLevel = Math.Max(recipe.GetSkillpoint(), Math.Min(549, recipe.GetSkillpoint() + 41));
					player.GetSkillList().AddSkill(player, skill, skillLevel);
					details["setupSkillLevel"] = Id(skillLevel);
					Assert.True(player.GetSkillList().GetSkillLevel(skill) >= recipe.GetSkillpoint());
					player.GetCommonData().SetDp(recipe.GetDp());
					Assert.Equal(recipe.GetDp(), player.GetCommonData().GetDp());
					if (!player.GetRecipeList().IsRecipePresent(id)) Assert.True(player.GetRecipeList().AddRecipe(player, id));
					var alternatives = recipe.GetComponents(); Assert.NotEmpty(alternatives);
					var components = alternatives[0].GetComponent(); Assert.NotEmpty(components);
					var materials = components.GroupBy(c => c.GetItemId()).Select(g => (ItemId: g.Key, Count: g.Sum(c => (long)c.GetQuantity()))).ToArray();
					foreach (var material in materials)
					{
						Assert.True(material.Count > 0);
						Assert.Equal(0, ItemService.AddItem(player, material.ItemId, material.Count));
					}
					await session.SynchronizeAsync(token);
					Assert.Contains(id, session.Api.World.Recipes);
					var expected = Totals();
					int start = session.PacketHistory.Count;
					await session.SendPacketAsync(session.Api.Craft(stationTemplate, id, stationTemplate == 0 ? 0 : station.GetObjectId(),
						materials, unknown: stationTemplate == 0 ? (byte)129 : (byte)0), token);
					await session.SynchronizeAsync(token);
					Assert.NotNull(player.GetInteractionTask());
					long deadline = fixture.Clock.NowMillis + 120_000;
					while (player.GetInteractionTask() != null)
					{
						long next = fixture.Clock.NextDueMillis ?? throw new InvalidDataException("Crafting has no scheduled work.");
						Assert.InRange(next, fixture.Clock.NowMillis, deadline);
						await session.AdvanceAsync(TimeSpan.FromMilliseconds(next - fixture.Clock.NowMillis), token);
						if (fixture.Clock.Faults.Count > 0)
							throw new AggregateException("Craft sweep observed a virtual timer fault.", fixture.Clock.Faults.Select(f => f.Exception));
					}
					await session.SynchronizeAsync(token);
					var completion = Assert.Single(session.PacketHistory.Skip(start), p => p.PacketType == typeof(SM_CRAFT_UPDATE) && p.Get<byte>("action") >= 4);
					Assert.Equal((byte)5, completion.Get<byte>("action"));
					int product = completion.Get<int>("itemId"), count = recipe.GetQuantity();
					Assert.True(count > 0);
					Assert.Contains(product, Enumerable.Range(1, recipe.GetComboProductSize()).Select(n => recipe.GetComboProduct(n)!.Value).Prepend(recipe.GetProductId()));
					foreach (var material in materials)
					{
						expected[material.ItemId] -= material.Count;
						if (expected[material.ItemId] == 0) expected.Remove(material.ItemId);
					}
					expected[product] = expected.GetValueOrDefault(product) + count;
					Assert.Equal(expected.OrderBy(p => p.Key), Totals().OrderBy(p => p.Key));
					var inventory = player.GetInventory().GetItemsWithKinah().Concat(player.GetEquipment().GetEquippedItems()).ToArray();
					Assert.Equal(expected.OrderBy(p => p.Key), inventory.GroupBy(i => i.GetItemId()).ToDictionary(g => g.Key, g => g.Sum(i => i.GetItemCount())).OrderBy(p => p.Key));
					// ItemService's stackable path deliberately does not invoke CraftedItemPredicate (Java too).
					// Power shards count as weapons but are stacks, so only individually created gear is named.
					Assert.All(inventory.Where(i => i.GetItemId() == product && !i.GetItemTemplate().IsStackable() && (i.GetItemTemplate().IsWeapon() || i.GetItemTemplate().IsArmor())),
						i => Assert.Equal(player.GetName(), i.GetItemCreator()));
					Assert.Equal(0, player.GetCommonData().GetDp());
					if (recipe.GetCraftDelayId() is int cooldown)
					{
						long remaining = player.GetCraftCooldowns()[cooldown] - Aion.GameServer.Utils.SystemClock.CurrentMillis();
						Assert.Equal(recipe.GetCraftDelayTime()!.Value * 1000L, remaining);
						details["cooldownMillis"] = remaining.ToString(CultureInfo.InvariantCulture);
					}
					Assert.Equal(recipe.GetMaxProductionCount() == null, player.GetRecipeList().IsRecipePresent(id));
					Assert.Equal(recipe.GetMaxProductionCount() == null, session.Api.World.Recipes.Contains(id));
					Assert.Null(player.GetInteractionTask()); Assert.Empty(session.Api.Timing.BlockingActivities);
					details["productId"] = Id(product); details["productCount"] = Id(count);
					details["components"] = string.Join(',', materials.Select(m => $"{m.ItemId}:{m.Count}"));
					Assert.True(player.GetInventory().DecreaseByItemId(product, count));
					if (player.GetRecipeList().IsRecipePresent(id)) Assert.True(player.GetRecipeList().DeleteRecipe(player, id));
					await session.SynchronizeAsync(token);
					logChecked = true; rowPolicy.AssertClean();
					report.Record(Id(id), DataSweepStatus.Passed, "CM_CRAFT completed; exact components/product, DP, recipe lifetime, cooldown and whole-inventory verified", details);
					session.PacketHistory.Clear(); session.PacketObservations.Clear();
					completed++;
					if (completed % 100 == 0) { report.Save(reportPath); Console.WriteLine($"SWEEP-CRAFT {completed}/{ids.Length}: {id} -> {product} x{count}"); }
				}
				catch (Exception error)
				{
					details["recentPackets"] = string.Join('\n', session.PacketHistory.TakeLast(20).Select(p => p.PacketType.Name + " " + System.Text.Json.JsonSerializer.Serialize(p.Fields)));
					if (!logChecked)
					{
						try { rowPolicy.AssertClean(); } catch (Exception logError) { details["logFailure"] = logError.ToString(); }
					}
					report.Record(Id(id), DataSweepStatus.Failed, error.ToString(), details); report.Save(reportPath);
					Console.WriteLine($"SWEEP-CRAFT {id}: failed\n" + System.Text.Json.JsonSerializer.Serialize(details));
					throw;
				}
			}
			Assert.True(report.Complete);
			foreach (var subject in new[] { elyos, asmodian })
			{
				await subject.QuitAsync(timeout.Token); await subject.VerifyOfflineAsync(timeout.Token);
			}
			policy.AssertClean();
		}
		finally { CraftConfig.MAX_CRAFT_FAILURE_CHANCE = failureChance; report.Save(reportPath); }
		Dictionary<int, long> Totals() => session.Api.World.Inventory.Values.GroupBy(i => i.ItemId).ToDictionary(g => g.Key, g => g.Sum(i => i.Count));
		static string Id(int value) => value.ToString(CultureInfo.InvariantCulture);
	}
}
