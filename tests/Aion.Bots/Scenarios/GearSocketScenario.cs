using Aion.Bots.Api;
using Aion.Bots.Protocol;
using Aion.Bots.World;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.Bots.Scenarios;

public interface IGearSocketDriver
{
	BotApi Api { get; }
	Task StepAsync(string action, Func<CancellationToken, Task> operation, CancellationToken token);
	Task<int> PrepareAsync(CancellationToken token);
	Task SendAsync(BotClientPacket packet, CancellationToken token);
	Task<DecodedBotServerPacket> WaitAsync(Type type, Func<DecodedBotServerPacket, bool> predicate, CancellationToken token);
	Task DelayAsync(TimeSpan delay, CancellationToken token);
	Task SynchronizeAsync(CancellationToken token);
	Task VerifyPersistenceAsync(CancellationToken token);
}

/// <summary>
/// G1: ordinary client socket/enchant attempts, including real random failures. Java EnchantService and
/// EnchantItemAction at ce54b7931: a failed socket keeps existing stones; enchant failure below +11 loses one level.
/// No outcome setters or changed success rates. Exhausting the bounded attempt budget is a failure, not a skip.
/// </summary>
public static class GearSocketScenario
{
	public const int MapId = 110010000;
	public const int RemoverNpcId = 203729; // Kane, existing Sanctum spawn.
	public static BotPosition Position { get; } = new(1618.827f, 1469.736f, 573.345f, 0);
	public const int WeaponId = 100000086; // Two sockets, max enchant +10, no destruction on failure.
	public const int ManastoneId = 167000226;
	public const int EnchantStoneId = 166000191;
	public const int WeaponCount = 8;
	public const int AttemptLimit = 64;
	public const int StoneCount = AttemptLimit + 2;

	public static async Task RunAsync(IGearSocketDriver driver, CancellationToken token = default)
	{
		int npc = 0;
		await driver.StepAsync("prepare-gear-and-approach-kane", async ct =>
		{
			npc = await driver.PrepareAsync(ct);
			await driver.SynchronizeAsync(ct);
		}, token);
		var weapons = driver.Api.World.Inventory.Values.Where(item => item.ItemId == WeaponId).OrderBy(item => item.ObjectId).ToArray();
		Require(weapons.Length == WeaponCount, "G1 requires eight independently enchantable weapons.");
		Require(weapons.All(item => Enchantment(item).EnchantLevel == 0 && Enchantment(item).Manastones == BotStoneSlots.Empty),
			"G1 setup must not grant already enchanted or socketed products.");
		foreach (int id in new[] { ManastoneId, EnchantStoneId })
			Require(driver.Api.World.Inventory.Values.Single(item => item.ItemId == id).Count == StoneCount, "Incorrect stone setup count.");
		int weapon = weapons[0].ObjectId;
		bool socketSucceeded = false, socketFailedWithExistingStone = false;
		for (int attempt = 1; attempt <= AttemptLimit && !(socketSucceeded && socketFailedWithExistingStone); attempt++)
		{
			if (Enchantment(driver.Api.World.Inventory[weapon]).Manastones.Slot1 != 0)
				await RemoveAsync(driver, npc, weapon, 1, token);
			await driver.StepAsync($"socket-attempt-{attempt:D2}", async ct =>
			{
				var before = CopyInventory(driver);
				var original = before[weapon];
				var stone = before.Values.Single(item => item.ItemId == ManastoneId);
				var enchantment = Enchantment(original);
				bool success = await UseAsync(driver, driver.Api.SocketManastone(weapon, stone.ObjectId), stone, 2000, ct);
				var slots = enchantment.Manastones;
				if (success)
				{
					slots = slots.Slot0 == 0 ? slots with { Slot0 = ManastoneId } : slots with { Slot1 = ManastoneId };
					socketSucceeded = true;
				}
				else if (slots.Slot0 == ManastoneId) socketFailedWithExistingStone = true;
				Consume(before, stone);
				before[weapon] = original with { Details = original.Details with { Enchantment = enchantment with { Manastones = slots } } };
				AssertInventory(driver, before, "socket");
			}, token);
		}
		Require(socketSucceeded && socketFailedWithExistingStone, "G1 did not observe socket success AND failure preserving an existing stone within 64 attempts.");
		// Delete a persisted stone through the NPC too; it must disappear, not become an orphan row.
		await driver.StepAsync("persist-socketed-gear", driver.VerifyPersistenceAsync, token);
		await RemoveAsync(driver, npc, weapon, 0, token);
		await driver.StepAsync("verify-stone-removal-persistence", driver.VerifyPersistenceAsync, token);

		int enchantSuccesses = 0;
		bool downgraded = false;
		for (int attempt = 1; attempt <= AttemptLimit && !(enchantSuccesses >= 2 && downgraded); attempt++)
		{
			await driver.StepAsync($"enchant-attempt-{attempt:D2}", async ct =>
			{
				var before = CopyInventory(driver);
				var original = before.Values.Where(item => item.ItemId == WeaponId && Enchantment(item).EnchantLevel < 10)
					.OrderBy(item => item.ObjectId).FirstOrDefault() ?? throw new InvalidDataException("All test weapons reached +10 without a downgrade.");
				var stone = before.Values.Single(item => item.ItemId == EnchantStoneId);
				var enchantment = Enchantment(original);
				bool success = await UseAsync(driver, driver.Api.EnchantItem(original.ObjectId, stone.ObjectId), stone, 4000, ct);
				byte level = Enchantment(driver.Api.World.Inventory[original.ObjectId]).EnchantLevel;
				if (success)
				{
					Require(level > enchantment.EnchantLevel && level <= Math.Min(10, enchantment.EnchantLevel + 3), "Enchantment success did not add 1..3 levels (capped at +10).");
					enchantSuccesses++;
				}
				else
				{
					Require(level == Math.Max(0, enchantment.EnchantLevel - 1), "Enchantment failure did not downgrade exactly one level.");
					downgraded |= enchantment.EnchantLevel > 0;
				}
				Consume(before, stone);
				before[original.ObjectId] = original with { Details = original.Details with { Enchantment = enchantment with { EnchantLevel = level } } };
				AssertInventory(driver, before, "enchant");
			}, token);
		}
		Require(enchantSuccesses >= 2 && downgraded, "G1 did not observe repeated enchantment advancement AND a positive-level failure downgrade within 64 attempts.");
		await driver.StepAsync("verify-enchanted-gear-persistence", driver.VerifyPersistenceAsync, token);
	}

	internal static async Task<bool> UseAsync(IGearSocketDriver driver, BotClientPacket packet, BotInventoryItem stone,
		int duration, CancellationToken token)
	{
		int messageStart = driver.Api.World.SystemMessages.Count;
		await driver.SendAsync(packet, token);
		bool OwnAnimation(DecodedBotServerPacket p) => p.Get<int>("playerObjId") == driver.Api.World.SelfObjectId && p.Get<int>("itemObjId") == stone.ObjectId;
		var start = await driver.WaitAsync(typeof(SM_ITEM_USAGE_ANIMATION), p => OwnAnimation(p) && p.Get<byte>("animationId") == 0, token);
		Require(start.Get<int>("castTime") == duration, "Unexpected server-advertised gear action duration.");
		await driver.DelayAsync(TimeSpan.FromMilliseconds(start.Get<int>("castTime")), token);
		var finish = await driver.WaitAsync(typeof(SM_ITEM_USAGE_ANIMATION), p => OwnAnimation(p) && p.Get<byte>("animationId") != 0, token);
		Require(finish.Get<byte>("animationId") is 1 or 2, "Gear action was canceled instead of completing.");
		await driver.SynchronizeAsync(token);
		bool success = finish.Get<byte>("animationId") == 1;
		string expectedMessage = stone.ItemId == ManastoneId
			? (success ? "STR_GIVE_ITEM_OPTION_SUCCEED" : "STR_GIVE_ITEM_OPTION_FAILED")
			: (success ? "STR_MSG_ENCHANT_ITEM_SUCCEED_NEW" : "STR_ENCHANT_ITEM_FAILED");
		var messages = driver.Api.World.SystemMessages.Skip(messageStart).Select(message => message.Name).ToArray();
		Require(messages.SequenceEqual(new[] { expectedMessage }),
			$"Expected exactly {expectedMessage}, received {string.Join(',', messages)}. Random failure is an asserted outcome, not an allowlist entry.");
		return success;
	}

	private static async Task RemoveAsync(IGearSocketDriver driver, int npc, int weapon, byte slot, CancellationToken token)
	{
		await driver.StepAsync($"remove-socket-{slot}", async ct =>
		{
			await driver.SendAsync(driver.Api.Target(npc), ct);
			await driver.SendAsync(driver.Api.TalkTo(npc), ct);
			await driver.WaitAsync(typeof(SM_DIALOG_WINDOW), p => p.Get<int>("targetObjectId") == npc, ct);
			await driver.SendAsync(driver.Api.SelectDialog(npc, 42), ct);
			await driver.WaitAsync(typeof(SM_DIALOG_WINDOW), p => p.Get<int>("targetObjectId") == npc, ct);
			var before = CopyInventory(driver);
			var original = before[weapon];
			var enchantment = Enchantment(original);
			Require(enchantment.Manastones.ToList()[slot] == ManastoneId, "Cannot remove an empty socket.");
			await driver.SendAsync(driver.Api.RemoveManastone(npc, weapon, slot), ct);
			await driver.WaitAsync(typeof(SM_INVENTORY_UPDATE_ITEM), p => p.Get<int>("objectId") == weapon, ct);
			await driver.SynchronizeAsync(ct);
			var slots = slot == 0 ? enchantment.Manastones with { Slot0 = 0 } : enchantment.Manastones with { Slot1 = 0 };
			before[weapon] = original with { Details = original.Details with { Enchantment = enchantment with { Manastones = slots } } };
			var kinah = before.Values.Single(item => item.ItemId == BotWorldModel.KinahItemId);
			long fee = (driver.Api.World.VendorPrices ?? throw new InvalidDataException("No client-visible service prices.")).ServicePrice(650);
			Require(fee > 0, "Removal requires a nonzero service fee.");
			before[kinah.ObjectId] = kinah with { Count = kinah.Count - fee };
			AssertInventory(driver, before, "remove socket");
			await driver.SendAsync(driver.Api.CloseDialog(npc), ct);
			await driver.SynchronizeAsync(ct);
		}, token);
	}

	private static BotItemEnchantment Enchantment(BotInventoryItem item) => item.Details.Enchantment ?? throw new InvalidDataException("Gear blob has no enchantment fields.");
	private static Dictionary<int, BotInventoryItem> CopyInventory(IGearSocketDriver driver) => driver.Api.World.Inventory.ToDictionary();
	private static void Consume(Dictionary<int, BotInventoryItem> expected, BotInventoryItem stone)
	{
		if (stone.Count == 1) expected.Remove(stone.ObjectId);
		else expected[stone.ObjectId] = stone with { Count = stone.Count - 1 };
	}
	private static void AssertInventory(IGearSocketDriver driver, Dictionary<int, BotInventoryItem> expected, string operation) =>
		Require(expected.OrderBy(pair => pair.Key).SequenceEqual(driver.Api.World.Inventory.OrderBy(pair => pair.Key)),
			$"G1 {operation} changed an unexpected inventory field, item, stone count or kinah amount.");
	private static void Require(bool condition, string message)
	{
		if (!condition) throw new InvalidDataException(message);
	}
}
