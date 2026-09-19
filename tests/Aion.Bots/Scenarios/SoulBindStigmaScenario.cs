using Aion.Bots.World;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.Bots.Scenarios;

/// <summary>G4: decline/accept real soul-binding, then equip a stigma and observe skill acquisition/removal across relogs.</summary>
public static class SoulBindStigmaScenario
{
	public const int WeaponId = 100000196; // Noble Durable Steel Sword: level 20, binds on equip.
	public const int StigmaId = 140001112; // Magical Defense, regular level-20 Gladiator stigma.
	public const int SkillId = 600;
	public const int UnlockQuestId = 1929;
	public const long StigmaSlot = 1L << 30;
	public const int BindQuestion = 95006;

	public static async Task RunAsync(IGearSocketDriver driver, CancellationToken token = default)
	{
		await driver.StepAsync("prepare-unbound-weapon-and-unlearned-stigma", async ct =>
		{
			await driver.PrepareAsync(ct);
			await driver.SynchronizeAsync(ct);
		}, token);
		var weapon = driver.Api.World.Inventory.Values.Single(item => item.ItemId == WeaponId);
		var stigma = driver.Api.World.Inventory.Values.Single(item => item.ItemId == StigmaId);
		Require(weapon.Details.Enchantment is { SoulBound: false } && weapon.Details.EquippedSlot == 0 && stigma.Details.EquippedSlot == 0,
			"Setup must grant unfinished inventory items, not already-bound/equipped products.");
		AssertSkill(false);
		await driver.StepAsync("decline-soul-binding-without-changing-inventory", async ct =>
		{
			var expected = driver.Api.World.Inventory.ToDictionary();
			await AskToBindAsync(ct);
			await driver.SendAsync(driver.Api.Answer(0), ct);
			await driver.WaitAsync(typeof(SM_SYSTEM_MESSAGE), p => p.Get<string>("name") == "STR_SOUL_BOUND_ITEM_CANCELED", ct);
			await driver.SynchronizeAsync(ct);
			AssertInventory(expected, "declining soul-binding");
		}, token);
		await driver.StepAsync("accept-soul-binding-and-honor-five-second-use", async ct =>
		{
			var expected = driver.Api.World.Inventory.ToDictionary();
			await AskToBindAsync(ct);
			await driver.SendAsync(driver.Api.Answer(1), ct);
			var start = await driver.WaitAsync(typeof(SM_ITEM_USAGE_ANIMATION), p => OwnBinding(p) && p.Get<byte>("animationId") == 4, ct);
			Require(start.Get<int>("castTime") == 5000, "Soul binding must advertise a five-second use time.");
			await driver.DelayAsync(TimeSpan.FromMilliseconds(start.Get<int>("castTime")), ct);
			await driver.WaitAsync(typeof(SM_ITEM_USAGE_ANIMATION), p => OwnBinding(p) && p.Get<byte>("animationId") == 6, ct);
			await driver.WaitAsync(typeof(SM_INVENTORY_UPDATE_ITEM), p => p.Get<int>("objectId") == weapon.ObjectId &&
				driver.Api.World.Inventory[weapon.ObjectId].Details.EquippedSlot == 1, ct);
			await driver.SynchronizeAsync(ct);
			foreach (var old in expected.Values.Where(item => (item.Details.EquippedSlot.GetValueOrDefault() & 1) != 0).ToArray())
				expected[old.ObjectId] = old with { EquipmentSlot = 0, Details = old.Details with { EquippedSlot = 0 } };
			expected[weapon.ObjectId] = weapon with { EquipmentSlot = 1, Details = weapon.Details with
			{
				EquippedSlot = 1, Enchantment = weapon.Details.Enchantment! with { SoulBound = true }
			} };
			AssertInventory(expected, "accepting soul-binding and equipping");
		}, token);
		await driver.StepAsync("equip-stigma-and-learn-its-skill", async ct =>
		{
			var expected = driver.Api.World.Inventory.ToDictionary();
			await driver.SendAsync(driver.Api.Equip(0, StigmaSlot, stigma.ObjectId), ct);
			await driver.WaitAsync(typeof(SM_INVENTORY_UPDATE_ITEM), p => p.Get<int>("objectId") == stigma.ObjectId &&
				driver.Api.World.Inventory[stigma.ObjectId].Details.EquippedSlot == StigmaSlot, ct);
			await driver.SynchronizeAsync(ct);
			expected[stigma.ObjectId] = stigma with { EquipmentSlot = unchecked((ushort)StigmaSlot), Details = stigma.Details with { EquippedSlot = StigmaSlot } };
			var kinah = expected.Values.Single(item => item.ItemId == BotWorldModel.KinahItemId);
			long fee = (driver.Api.World.VendorPrices ?? throw new InvalidDataException("No client service prices.")).ServicePrice(25_000);
			Require(fee > 0, "A regular stigma has a nonzero equip fee.");
			expected[kinah.ObjectId] = kinah with { Count = kinah.Count - fee };
			AssertInventory(expected, "equipping stigma with the exact fee");
			AssertSkill(true);
		}, token);
		await driver.StepAsync("persist-bound-weapon-equipped-stigma-and-skill", async ct =>
		{
			await driver.VerifyPersistenceAsync(ct);
			AssertSkill(true);
		}, token);
		await driver.StepAsync("unequip-stigma-and-remove-its-skill", async ct =>
		{
			var expected = driver.Api.World.Inventory.ToDictionary();
			await driver.SendAsync(driver.Api.Equip(1, StigmaSlot, stigma.ObjectId), ct);
			await driver.WaitAsync(typeof(SM_SKILL_REMOVE), p => p.Get<ushort>("skillId") == SkillId, ct);
			await driver.WaitAsync(typeof(SM_INVENTORY_UPDATE_ITEM), p => p.Get<int>("objectId") == stigma.ObjectId &&
				driver.Api.World.Inventory[stigma.ObjectId].Details.EquippedSlot == 0, ct);
			await driver.SynchronizeAsync(ct);
			expected[stigma.ObjectId] = stigma with { EquipmentSlot = 0 };
			AssertInventory(expected, "unequipping stigma without a fee or losing soul-binding");
			AssertSkill(false);
		}, token);
		await driver.StepAsync("persist-stigma-removal-and-retained-soul-binding", async ct =>
		{
			await driver.VerifyPersistenceAsync(ct);
			AssertSkill(false);
			Require(driver.Api.World.Inventory[weapon.ObjectId].Details.Enchantment?.SoulBound == true, "Soul-binding did not persist.");
		}, token);

		async Task AskToBindAsync(CancellationToken ct)
		{
			await driver.SendAsync(driver.Api.Equip(0, 1, weapon.ObjectId), ct);
			await driver.WaitAsync(typeof(SM_QUESTION_WINDOW), p => p.Get<int>("code") == BindQuestion, ct);
		}
		bool OwnBinding(Aion.Bots.Protocol.DecodedBotServerPacket packet) =>
			packet.Get<int>("playerObjId") == driver.Api.World.SelfObjectId && packet.Get<int>("itemObjId") == weapon.ObjectId;
		void AssertSkill(bool present)
		{
			Require(driver.Api.World.Skills.ContainsKey(SkillId) == present, $"Stigma skill presence should be {present}.");
			if (present) Require(driver.Api.World.Skills[SkillId] is { Level: 1, SkillType: 1 }, "Expected the level-one stigma skill, not an auto-learned skill.");
		}
		void AssertInventory(Dictionary<int, BotInventoryItem> expected, string action) =>
			Require(expected.OrderBy(pair => pair.Key).SequenceEqual(driver.Api.World.Inventory.OrderBy(pair => pair.Key)), $"G4 {action} changed unexpected inventory fields.");
	}
	private static void Require(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
}
