using Aion.Bots.Api;
using Aion.Bots.Protocol;
using Aion.Bots.World;
using Aion.GameServer.Model;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.Bots.Scenarios;

public interface IWarehouseScenarioDriver
{
	BotApi Api { get; }
	Task StepAsync(string action, Func<CancellationToken, Task> operation, CancellationToken token);
	Task<int> PrepareAsync(CancellationToken token);
	Task SendAsync(BotClientPacket packet, CancellationToken token);
	Task<DecodedBotServerPacket> WaitAsync(Type type, Func<DecodedBotServerPacket, bool> predicate, CancellationToken token);
	Task SynchronizeAsync(CancellationToken token);
	Task VerifyPersistenceAsync(CancellationToken token);
}

/// <summary>E8: ordinary character/account storage transfers, stack splitting, kinah and paid expansion.</summary>
public static class WarehouseScenario
{
	public const int MapId = 110010000, NpcId = 203749, WeaponId = 100000086, OreId = 152000102;
	public static BotPosition Position { get; } = new(1349.60f, 1409.73f, 573.386f, 7);
	public static IReadOnlyList<(int Id, int Count)> Grants { get; } = new[]
	{
		(BotWorldModel.KinahItemId, 100_000), (WeaponId, 12), (OreId, 20)
	};

	public static async Task RunAsync(IWarehouseScenarioDriver driver, CancellationToken token = default)
	{
		int npc = 0;
		await driver.StepAsync("prepare-raw-items-and-walk-to-warehouse", async ct =>
		{
			npc = await driver.PrepareAsync(ct);
			await driver.SynchronizeAsync(ct);
			Require(Items(1).Count == 0 && Items(2).Values.All(i => i.ItemId == BotWorldModel.KinahItemId && i.Count == 0), "Warehouse setup must be empty.");
			Require(driver.Api.World.Warehouses[1].CharacterCapacity == 24, "Unexpected initial character warehouse capacity.");
			Require(Inventory().Values.Count(i => i.ItemId == WeaponId) == 12, "Expected twelve non-stackable weapons for a paginated warehouse snapshot.");
			await OpenAsync(ct);
		}, token);
		await driver.StepAsync("deposit-twelve-weapons-in-character-warehouse", async ct =>
		{
			short slot = 0;
			foreach (var item in Inventory().Values.Where(i => i.ItemId == WeaponId).OrderBy(i => i.ObjectId).ToArray())
			{
				var cube = Inventory(); var warehouse = Items(1).ToDictionary();
				driver.Api.Timing.EnsureCanMove();
				await driver.SendAsync(GameClientPackets.MoveItem(item.ObjectId, 0, 1, slot), ct);
				await driver.SynchronizeAsync(ct);
				cube.Remove(item.ObjectId);
				warehouse.Add(item.ObjectId, item with { EquipmentSlot = unchecked((ushort)slot++), Cloth = false });
				Equal(cube, Inventory(), "deposit cube"); Equal(warehouse, Items(1), "deposit warehouse");
			}
			await driver.VerifyPersistenceAsync(ct); // Fresh login must reconstruct both warehouse pages.
			Require(Items(1).Count == 12, "Paginated warehouse lost items at relog.");
			await OpenAsync(ct);
		}, token);
		await driver.StepAsync("withdraw-weapon-and-split-account-stack", async ct =>
		{
			var item = Items(1).Values.OrderBy(i => i.ObjectId).First();
			var cube = Inventory(); var warehouse = Items(1).ToDictionary();
			await driver.SendAsync(GameClientPackets.MoveItem(item.ObjectId, 1, 0, 20), ct);
			await driver.SynchronizeAsync(ct);
			warehouse.Remove(item.ObjectId); cube.Add(item.ObjectId, item with { EquipmentSlot = 20 });
			Equal(cube, Inventory(), "withdraw cube"); Equal(warehouse, Items(1), "withdraw warehouse");
			var ore = Inventory().Values.Single(i => i.ItemId == OreId);
			var account = Items(2).ToDictionary();
			await driver.SendAsync(GameClientPackets.SplitItem(ore.ObjectId, 8, 0, 0, 2, 0), ct);
			await driver.SynchronizeAsync(ct);
			var deposited = Items(2).Values.Single(i => i.ItemId == OreId);
			Require(deposited.Count == 8 && deposited.ObjectId != ore.ObjectId, "Split deposit did not create a distinct eight-item stack.");
			cube[ore.ObjectId] = ore with { Count = 12 }; account.Add(deposited.ObjectId, deposited);
			Equal(cube, Inventory(), "split cube"); Equal(account, Items(2), "split account");
			await driver.SendAsync(GameClientPackets.SplitItem(deposited.ObjectId, 3, 2, ore.ObjectId, 0, 0), ct);
			await driver.SynchronizeAsync(ct);
			cube[ore.ObjectId] = ore with { Count = 15 }; account[deposited.ObjectId] = deposited with { Count = 5 };
			Equal(cube, Inventory(), "partial withdrawal cube"); Equal(account, Items(2), "partial withdrawal account");
		}, token);
		await driver.StepAsync("deposit-and-withdraw-account-kinah", async ct =>
		{
			var cube = Inventory(); var account = Items(2).ToDictionary();
			var carried = cube.Values.Single(i => i.ItemId == BotWorldModel.KinahItemId);
			var priorStored = account.Values.SingleOrDefault(i => i.ItemId == BotWorldModel.KinahItemId);
			await driver.SendAsync(GameClientPackets.SplitItem(carried.ObjectId, 25_000, 0, priorStored?.ObjectId ?? 0, 2, -1), ct);
			await driver.SynchronizeAsync(ct);
			var stored = Items(2).Values.Single(i => i.ItemId == BotWorldModel.KinahItemId);
			Require(stored.Count == (priorStored?.Count ?? 0) + 25_000 && stored.ObjectId != carried.ObjectId, "Kinah deposit did not credit distinct account storage.");
			if (priorStored != null) Require(stored.ObjectId == priorStored.ObjectId, "Kinah deposit changed account item identity.");
			var expectedStored = priorStored ?? carried with { ObjectId = stored.ObjectId, Count = 0, EquipmentSlot = ushort.MaxValue, Cloth = false };
			cube[carried.ObjectId] = carried with { Count = carried.Count - 25_000 };
			account[stored.ObjectId] = expectedStored with { Count = expectedStored.Count + 25_000 };
			Equal(cube, Inventory(), "kinah deposit cube"); Equal(account, Items(2), "kinah deposit account");
			await driver.SendAsync(GameClientPackets.SplitItem(stored.ObjectId, 10_000, 2, carried.ObjectId, 0, -1), ct);
			await driver.SynchronizeAsync(ct);
			cube[carried.ObjectId] = carried with { Count = carried.Count - 15_000 };
			account[stored.ObjectId] = expectedStored with { Count = expectedStored.Count + 15_000 };
			Equal(cube, Inventory(), "kinah withdrawal cube"); Equal(account, Items(2), "kinah withdrawal account");
			await driver.VerifyPersistenceAsync(ct);
			await OpenAsync(ct);
		}, token);
		await driver.StepAsync("decline-then-buy-eight-character-warehouse-slots", async ct =>
		{
			var cube = Inventory(); var regular = Items(1).ToDictionary(); var account = Items(2).ToDictionary();
			await RequestExpansionAsync(ct);
			await driver.SendAsync(driver.Api.Answer(0), ct);
			await driver.SynchronizeAsync(ct);
			Require(driver.Api.World.Warehouses[1].CharacterCapacity == 24, "Declining expansion changed capacity.");
			Equal(cube, Inventory(), "decline inventory");
			await RequestExpansionAsync(ct);
			await driver.SendAsync(driver.Api.Answer(1), ct);
			await driver.SynchronizeAsync(ct);
			Require(driver.Api.World.Warehouses[1].CharacterCapacity == 32, "Paid expansion did not add eight slots.");
			var kinah = cube.Values.Single(i => i.ItemId == BotWorldModel.KinahItemId);
			cube[kinah.ObjectId] = kinah with { Count = kinah.Count - 1200 };
			Equal(cube, Inventory(), "expansion fee"); Equal(regular, Items(1), "expansion character contents"); Equal(account, Items(2), "expansion account contents");
			await driver.SendAsync(driver.Api.CloseDialog(npc), ct);
			await driver.VerifyPersistenceAsync(ct);
			Require(driver.Api.World.Warehouses[1].CharacterCapacity == 32, "Warehouse expansion lost at relog.");
		}, token);

		async Task OpenAsync(CancellationToken ct)
		{
			await driver.SendAsync(driver.Api.Target(npc), ct);
			await driver.SendAsync(driver.Api.TalkTo(npc), ct);
			await driver.WaitAsync(typeof(SM_DIALOG_WINDOW), p => p.Get<int>("targetObjectId") == npc, ct);
			await driver.SendAsync(driver.Api.SelectDialog(npc, DialogAction.DEPOSIT_CHAR_WAREHOUSE), ct);
			await driver.WaitAsync(typeof(SM_DIALOG_WINDOW), p => p.Get<int>("targetObjectId") == npc && p.Get<ushort>("dialogPageId") == 26, ct);
			await driver.SynchronizeAsync(ct);
		}
		async Task RequestExpansionAsync(CancellationToken ct)
		{
			await driver.SendAsync(driver.Api.SelectDialog(npc, DialogAction.EXTEND_CHAR_WAREHOUSE), ct);
			var question = await driver.WaitAsync(typeof(SM_QUESTION_WINDOW), p => p.Get<int>("code") == SM_QUESTION_WINDOW.STR_WAREHOUSE_EXPAND_WARNING, ct);
			Require(question.Get<string[]>("params").SequenceEqual(new[] { "1200", "", "" }), "Unexpected warehouse expansion quote.");
		}
		Dictionary<int, BotInventoryItem> Inventory() => driver.Api.World.Inventory.ToDictionary();
		IReadOnlyDictionary<int, BotInventoryItem> Items(byte type) => driver.Api.World.Warehouses[type].Items;
	}

	private static void Equal(IReadOnlyDictionary<int, BotInventoryItem> expected, IReadOnlyDictionary<int, BotInventoryItem> actual, string action) =>
		Require(expected.OrderBy(p => p.Key).SequenceEqual(actual.OrderBy(p => p.Key)), $"Unexpected item delta after {action}.");
	private static void Require(bool condition, string message)
	{
		if (!condition) throw new InvalidDataException(message);
	}
}
