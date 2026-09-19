using Aion.Bots.Protocol;
using Aion.Bots.World;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.Bots.Scenarios;

public interface IGodstoneDriver : IGearSocketDriver
{
	IReadOnlyList<DecodedBotServerPacket> History { get; }
	Task VerifyProcAsync(int targetObjectId, bool active, CancellationToken token);
}

/// <summary>G2: socket a real godstone, equip, trigger it with ordinary attacks and watch its full poison lifetime.</summary>
public static class GodstoneScenario
{
	public const int MapId = 210010000;
	// GM setup spawns an existing, attackable 99,999,999-HP training-dummy template without editing its stats or AI.
	public const int DummyId = 216688;
	public static BotPosition Position { get; } = new(598.261f, 1094.75f, 101.514f, 0);
	public const int WeaponId = GearSocketScenario.WeaponId;
	public const int GodstoneId = 168000124; // Zikel's Arrogance: 10% main-hand proc, no break probability.
	public const int SkillId = 8542;
	public const int AttackLimit = 150;
	public const int AttackSpeedMillis = 1400;

	public static async Task RunAsync(IGodstoneDriver driver, CancellationToken token = default)
	{
		int target = 0;
		await driver.StepAsync("prepare-godstone-and-training-dummy", async ct =>
		{
			target = await driver.PrepareAsync(ct);
			await driver.SynchronizeAsync(ct);
		}, token);
		var weapon = driver.Api.World.Inventory.Values.Single(item => item.ItemId == WeaponId);
		var stone = driver.Api.World.Inventory.Values.Single(item => item.ItemId == GodstoneId);
		Require(stone.Count == 1 && weapon.Details.Enchantment is { GodstoneId: 0 } && weapon.Details.EquippedSlot == 0,
			"G2 needs one unsocketed inventory weapon and one godstone, not a granted finished product.");
		await driver.StepAsync("socket-godstone-through-inventory", async ct =>
		{
			var expected = driver.Api.World.Inventory.ToDictionary();
			await driver.SendAsync(driver.Api.SocketGodstone(weapon.ObjectId, stone.ObjectId), ct);
			var start = await driver.WaitAsync(typeof(SM_ITEM_USAGE_ANIMATION), p => p.Get<int>("playerObjId") == driver.Api.World.SelfObjectId &&
				p.Get<int>("itemObjId") == stone.ObjectId && p.Get<byte>("animationId") == 0, ct);
			Require(start.Get<int>("castTime") == 2000, "Godstone socketing must advertise its two-second use time.");
			await driver.DelayAsync(TimeSpan.FromMilliseconds(start.Get<int>("castTime")), ct);
			var finish = await driver.WaitAsync(typeof(SM_ITEM_USAGE_ANIMATION), p => p.Get<int>("playerObjId") == driver.Api.World.SelfObjectId &&
				p.Get<int>("itemObjId") == stone.ObjectId && p.Get<byte>("animationId") != 0, ct);
			Require(finish.Get<byte>("animationId") == 1, "Godstone socketing did not complete successfully.");
			// Java sends completion BEFORE consuming the stone and updating the weapon; wait for the actual inventory packet.
			await driver.WaitAsync(typeof(SM_INVENTORY_UPDATE_ITEM), p => p.Get<int>("objectId") == weapon.ObjectId &&
				driver.Api.World.Inventory[weapon.ObjectId].Details.Enchantment?.GodstoneId == GodstoneId, ct);
			await driver.SynchronizeAsync(ct);
			expected.Remove(stone.ObjectId);
			expected[weapon.ObjectId] = weapon with { Details = weapon.Details with { Enchantment = weapon.Details.Enchantment! with { GodstoneId = GodstoneId } } };
			AssertInventory(driver, expected, "Godstone socketing must consume exactly one stone with no fee or unrelated item changes.");
		}, token);
		await driver.StepAsync("equip-socketed-weapon", async ct =>
		{
			var expected = driver.Api.World.Inventory.ToDictionary();
			await driver.SendAsync(driver.Api.Equip(0, 1, weapon.ObjectId), ct);
			await driver.WaitAsync(typeof(SM_INVENTORY_UPDATE_ITEM), p => p.Get<int>("objectId") == weapon.ObjectId &&
				driver.Api.World.Inventory[weapon.ObjectId].Details.EquippedSlot == 1, ct);
			await driver.SynchronizeAsync(ct);
			foreach (var old in expected.Values.Where(item => (item.Details.EquippedSlot.GetValueOrDefault() & 1) != 0).ToArray())
				expected[old.ObjectId] = old with { EquipmentSlot = 0, Details = old.Details with { EquippedSlot = 0 } };
			weapon = expected[weapon.ObjectId];
			expected[weapon.ObjectId] = weapon with { EquipmentSlot = 1, Details = weapon.Details with { EquippedSlot = 1 } };
			AssertInventory(driver, expected, "Equipping must retain the godstone, replace the main-hand slot and consume no items.");
		}, token);

		int historyStart = driver.History.Count;
		int messagesStart = driver.Api.World.SystemMessages.Count;
		await driver.SendAsync(driver.Api.Target(target), token);
		bool proc = false;
		for (int attempt = 0; attempt < AttackLimit && !proc; attempt++)
		{
			await driver.StepAsync($"normal-attack-for-godstone-proc-{attempt + 1:D3}", async ct =>
			{
				await driver.SendAsync(driver.Api.Attack(target, AttackSpeedMillis, checked((byte)attempt)), ct);
				await driver.DelayAsync(TimeSpan.FromMilliseconds(AttackSpeedMillis + 50), ct);
				await driver.SynchronizeAsync(ct);
				proc = driver.Api.World.SystemMessages.Skip(messagesStart).Any(message => message.Name == "STR_SKILL_PROC_EFFECT_OCCURRED");
				Require(!driver.Api.World.IsDead, "Subject died during the training-dummy test.");
			}, token);
		}
		Require(proc, "No real godstone proc within 150 honest normal attacks; do not force the roll or treat missing coverage as passed.");
		await driver.VerifyProcAsync(target, true, token);
		await driver.StepAsync("watch-poison-ticks-through-expiry", async ct =>
		{
			// AbstractOverTimeEffect adds 1s to XML duration and 300ms to the first tick: 2.3..20.3s, ten ticks.
			// Stop attacking after the first proc so another proc cannot refresh the 21-second lifetime.
			for (int second = 0; second < 22; second++)
			{
				await driver.DelayAsync(TimeSpan.FromSeconds(1), ct);
				await driver.SynchronizeAsync(ct);
			}
			var ticks = PoisonTicks().ToArray();
			Require(ticks.Length == 10, $"Expected ten poison ticks before expiry, observed {ticks.Length}.");
			Require(ticks.All(p => p.Get<int>("writtenValue") < 0 && p.Get<byte>("typeId") == 7 && p.Get<byte>("logId") == 25),
				"Godstone must cause actual poison damage, not only announce a proc.");
			await driver.VerifyProcAsync(target, false, ct);
			await driver.DelayAsync(TimeSpan.FromSeconds(5), ct);
			await driver.SynchronizeAsync(ct);
			Require(PoisonTicks().Count() == 10, "Poison continued damaging after its lifetime.");
			Require(driver.Api.World.SystemMessages.Skip(messagesStart).Count(message => message.Name == "STR_SKILL_PROC_EFFECT_OCCURRED") == 1,
				"Expected one proc; periodic damage must not recursively proc the godstone.");
		}, token);
		await driver.StepAsync("verify-equipped-godstone-persistence", driver.VerifyPersistenceAsync, token);

		IEnumerable<DecodedBotServerPacket> PoisonTicks() => driver.History.Skip(historyStart).Where(p => p.PacketType == typeof(SmAttackStatus) &&
			p.Get<int>("objectId") == target && p.Get<ushort>("skillId") == SkillId);
	}
	private static void AssertInventory(IGodstoneDriver driver, Dictionary<int, BotInventoryItem> expected, string message) =>
		Require(expected.OrderBy(pair => pair.Key).SequenceEqual(driver.Api.World.Inventory.OrderBy(pair => pair.Key)), message);
	private static void Require(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
}
