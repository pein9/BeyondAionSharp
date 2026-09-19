using Aion.Bots.World;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.Bots.Scenarios;

/// <summary>G3: socket two unfinished weapons, fuse at Curio, persist, break and persist again.</summary>
public static class WeaponFusionScenario
{
	public const int MapId = 110010000;
	public const int NpcId = 798424;
	public static BotPosition Position { get; } = new(1620.65f, 1529.7f, 573.347f, 56);
	public const int WeaponId = 100900006; // Level 16 rare Verteron Guardian Greatsword, fusible, two sockets.
	public const int StoneCount = 66;

	public static async Task RunAsync(IGearSocketDriver driver, CancellationToken token = default)
	{
		int npc = 0;
		await driver.StepAsync("prepare-two-weapons-and-approach-curio", async ct =>
		{
			npc = await driver.PrepareAsync(ct);
			await driver.SynchronizeAsync(ct);
		}, token);
		var weapons = driver.Api.World.Inventory.Values.Where(item => item.ItemId == WeaponId).OrderBy(item => item.ObjectId).ToArray();
		Require(weapons.Length == 2 && weapons.All(item => item.Details.EquippedSlot == 0 &&
			item.Details.Fusion is { ItemId: 0 } && item.Details.Enchantment?.Manastones == BotStoneSlots.Empty),
			"G3 must begin with two unfinished, unsocketed, unfused inventory weapons.");
		Require(driver.Api.World.Inventory.Values.Single(item => item.ItemId == GearSocketScenario.ManastoneId).Count == StoneCount,
			"G3 requires a bounded supply of real manastones.");
		foreach (var weapon in weapons)
		{
			bool success = false;
			for (int attempt = 1; attempt <= 32 && !success; attempt++)
			{
				await driver.StepAsync($"socket-fusion-component-{weapon.ObjectId}-attempt-{attempt:D2}", async ct =>
				{
					var expected = driver.Api.World.Inventory.ToDictionary();
					var item = expected[weapon.ObjectId];
					var stone = expected.Values.Single(row => row.ItemId == GearSocketScenario.ManastoneId);
					success = await GearSocketScenario.UseAsync(driver, driver.Api.SocketManastone(item.ObjectId, stone.ObjectId), stone, 2000, ct);
					expected[stone.ObjectId] = stone with { Count = stone.Count - 1 };
					if (success) expected[item.ObjectId] = item with { Details = item.Details with
					{
						Enchantment = item.Details.Enchantment! with { Manastones = BotStoneSlots.Empty with { Slot0 = GearSocketScenario.ManastoneId } }
					} };
					AssertInventory(expected, "socket component");
				}, token);
			}
			Require(success, "No component socket succeeded within 32 real attempts.");
		}
		int mainId = weapons[0].ObjectId, secondaryId = weapons[1].ObjectId;
		await driver.StepAsync("fuse-socketed-weapons-at-curio", async ct =>
		{
			await OpenDialogAsync(66, ct);
			var expected = driver.Api.World.Inventory.ToDictionary();
			var main = expected[mainId];
			var secondary = expected[secondaryId];
			await driver.SendAsync(driver.Api.FuseWeapons(npc, mainId, secondaryId), ct);
			await driver.WaitAsync(typeof(SM_SYSTEM_MESSAGE), p => p.Get<string>("name") == "STR_COMPOUND_SUCCESS", ct);
			await driver.SynchronizeAsync(ct);
			expected.Remove(secondaryId);
			expected[mainId] = main with { Details = main.Details with
			{
				Fusion = new BotItemFusion(WeaponId, secondary.Details.Enchantment!.Manastones,
					secondary.Details.Enchantment.OptionalSockets, secondary.Details.Premium!.BonusStatsId)
			} };
			var kinah = expected.Values.Single(item => item.ItemId == BotWorldModel.KinahItemId);
			long fee = (driver.Api.World.VendorPrices ?? throw new InvalidDataException("Missing client service prices.")).ServicePrice(250 * 16 * 16);
			Require(fee > 0, "Armsfusion requires a nonzero service fee.");
			expected[kinah.ObjectId] = kinah with { Count = kinah.Count - fee };
			AssertInventory(expected, "fuse and transfer secondary sockets");
			await driver.SendAsync(driver.Api.CloseDialog(npc), ct);
		}, token);
		await driver.StepAsync("persist-primary-and-fusion-sockets", driver.VerifyPersistenceAsync, token);
		await driver.StepAsync("break-fusion-at-curio", async ct =>
		{
			await OpenDialogAsync(67, ct);
			var expected = driver.Api.World.Inventory.ToDictionary();
			var main = expected[mainId];
			Require(main.Details.Fusion?.Manastones.Slot0 == GearSocketScenario.ManastoneId, "Missing persisted fusion stone before breaking.");
			await driver.SendAsync(driver.Api.BreakWeapons(npc, mainId), ct);
			await driver.WaitAsync(typeof(SM_SYSTEM_MESSAGE), p => p.Get<string>("name") == "STR_COMPOUNDED_ITEM_DECOMPOUND_SUCCESS", ct);
			await driver.SynchronizeAsync(ct);
			expected[mainId] = main with { Details = main.Details with { Fusion = new BotItemFusion(0, BotStoneSlots.Empty, 0, 0) } };
			AssertInventory(expected, "break removes only fusion and its stones, with no fee or returned secondary weapon");
			await driver.SendAsync(driver.Api.CloseDialog(npc), ct);
		}, token);
		await driver.StepAsync("persist-broken-fusion-with-no-orphan-stones", driver.VerifyPersistenceAsync, token);

		async Task OpenDialogAsync(ushort action, CancellationToken ct)
		{
			await driver.SendAsync(driver.Api.Target(npc), ct);
			await driver.SendAsync(driver.Api.TalkTo(npc), ct);
			await driver.WaitAsync(typeof(SM_DIALOG_WINDOW), p => p.Get<int>("targetObjectId") == npc, ct);
			await driver.SendAsync(driver.Api.SelectDialog(npc, action), ct);
			await driver.WaitAsync(typeof(SM_DIALOG_WINDOW), p => p.Get<int>("targetObjectId") == npc, ct);
		}
		void AssertInventory(Dictionary<int, BotInventoryItem> expected, string operation) =>
			Require(expected.OrderBy(pair => pair.Key).SequenceEqual(driver.Api.World.Inventory.OrderBy(pair => pair.Key)),
				$"G3 {operation} changed unexpected inventory fields, item counts or kinah.");
	}
	private static void Require(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
}
