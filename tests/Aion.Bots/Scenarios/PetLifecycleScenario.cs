using System.Buffers.Binary;
using Aion.Bots.Api;
using Aion.Bots.Protocol;
using Aion.Bots.World;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.Bots.Scenarios;

public interface IPetLifecycleDriver
{
	BotApi Api { get; }
	int CharacterId { get; }
	Task StepAsync(string action, Func<CancellationToken, Task> operation, CancellationToken token);
	Task PrepareAsync(CancellationToken token);
	Task SendAsync(BotClientPacket packet, CancellationToken token);
	Task<DecodedBotServerPacket> WaitAsync(Func<DecodedBotServerPacket, bool> predicate, CancellationToken token);
	Task DelayAsync(TimeSpan duration, CancellationToken token);
	Task SynchronizeAsync(CancellationToken token);
	Task VerifyStateAsync(int petObjectId, bool summoned, int feedProgress, CancellationToken token);
	Task<IReadOnlyList<DecodedBotServerPacket>> ReloginAsync(CancellationToken token);
}

/// <summary>L5: director grants existing egg/food only; the ordinary subject adopts, summons, feeds and dismisses.</summary>
public static class PetLifecycleScenario
{
	public const int TemplateId = 900043, EggItem = 190000020, FoodItem = 182003659;
	public const string PetName = "Mookie";
	public static async Task RunAsync(IPetLifecycleDriver driver, CancellationToken token)
	{
		await driver.StepAsync("prepare-existing-pet-egg-and-three-food-items", async ct =>
		{
			await driver.PrepareAsync(ct); await driver.SynchronizeAsync(ct);
			Require(driver.Api.World.Inventory.Values.Single(i => i.ItemId == EggItem).Count == 1, "Expected one egg.");
			Require(driver.Api.World.Inventory.Values.Single(i => i.ItemId == FoodItem).Count == 3, "Expected three food items.");
		}, token);
		var inventory = driver.Api.World.Inventory.ToDictionary();
		var egg = inventory.Values.Single(i => i.ItemId == EggItem);
		var food = inventory.Values.Single(i => i.ItemId == FoodItem);
		BotPetData? adopted = null;
		await driver.StepAsync("adopt-egg-through-normal-client-packet", async ct =>
		{
			await driver.SendAsync(GameClientPackets.AdoptPet(egg.ObjectId, TemplateId, PetName), ct);
			var packet = await driver.WaitAsync(p => Action(p, 1), ct); adopted = packet.Get<BotPetData>("pet");
			VerifyIdentity(adopted, driver.CharacterId); Require(adopted.ObjectId > 0, "Adoption needs a server-generated pet identity.");
			Require(FeedProgress(adopted) == 0, "New pet must have no feeding progress.");
			inventory.Remove(egg.ObjectId); await driver.SynchronizeAsync(ct); AssertInventory(driver, inventory);
			await driver.VerifyStateAsync(adopted.ObjectId, false, 0, ct);
		}, token);
		int petId = adopted!.ObjectId;
		await driver.StepAsync("summon-adopted-pet", ct => SummonAsync(driver, petId, ct), token);
		int progress = 0;
		await driver.StepAsync("feed-two-items-through-timed-server-feeding", async ct =>
		{
			await driver.SendAsync(GameClientPackets.FeedPet(food.ObjectId, 2), ct);
			var started = await driver.WaitAsync(p => Feeding(p, 1), ct);
			VerifyFeed(started, food.ObjectId, 2); AssertInventory(driver, inventory);
			for (int eaten = 1; eaten <= 2; eaten++)
			{
				await driver.DelayAsync(TimeSpan.FromMilliseconds(2500), ct);
				var result = await driver.WaitAsync(p => Feeding(p, 2), ct);
				VerifyFeed(result, food.ObjectId, 2 - eaten);
				progress = result.Get<int>("feedProgress");
				Require(((uint)progress >> 24) == eaten, "Feeding receipt did not increment the regular-food count.");
				inventory[food.ObjectId] = food with { Count = 3 - eaten };
				await driver.SynchronizeAsync(ct); AssertInventory(driver, inventory);
			}
			await driver.VerifyStateAsync(petId, true, progress, ct);
		}, token);
		await driver.StepAsync("dismiss-and-persist-pet", async ct =>
		{
			await DismissAsync(driver, petId, ct); await driver.VerifyStateAsync(petId, false, progress, ct);
		}, token);
		await driver.StepAsync("fresh-login-verifies-pet-and-consumed-inventory", async ct =>
		{
			var packets = await driver.ReloginAsync(ct);
			var lists = packets.Where(p => Action(p, 0)).ToArray();
			Require(lists.Length == 1, "Expected one fresh pet list.");
			var pets = lists[0].Get<BotPetData[]>("pets"); Require(pets.Length == 1, "Expected exactly one owned pet.");
			VerifyIdentity(pets[0], driver.CharacterId);
			Require(pets[0].ObjectId == petId && FeedProgress(pets[0]) == progress, "Relog lost pet identity or feeding progress.");
			AssertInventory(driver, inventory); await driver.VerifyStateAsync(petId, false, progress, ct);
		}, token);
		await driver.StepAsync("resummon-persisted-pet-and-dismiss-again", async ct =>
		{
			await SummonAsync(driver, petId, ct); await driver.VerifyStateAsync(petId, true, progress, ct);
			await DismissAsync(driver, petId, ct); await driver.VerifyStateAsync(petId, false, progress, ct);
		}, token);
	}

	public static bool Action(DecodedBotServerPacket p, ushort action) => p.PacketType == typeof(SM_PET) && p.Get<ushort>("action") == action;
	public static bool Feeding(DecodedBotServerPacket p, byte subtype) => Action(p, 9) && p.Get<byte>("subType") == subtype;
	public static void VerifyIdentity(BotPetData pet, int owner)
	{
		Require(pet.Name == PetName && pet.TemplateId == TemplateId && pet.MasterObjectId == owner && pet.Decoration == 0
			&& pet.SecondsUntilExpiration == 0, "Pet identity, ownership, decoration or lifetime differs from the adopted egg.");
	}
	public static int FeedProgress(BotPetData pet) => BinaryPrimitives.ReadInt32LittleEndian(pet.Functions.Single(f => f.Id == 1).Data);
	public static void VerifyFeed(DecodedBotServerPacket packet, int foodId, int remaining)
	{
		Require(Action(packet, 9) && packet.Get<int>("itemObjectId") == foodId && packet.Get<int>("count") == remaining,
			"Feeding receipt references the wrong food or remaining count.");
	}
	private static async Task SummonAsync(IPetLifecycleDriver driver, int petId, CancellationToken token)
	{
		await driver.SendAsync(GameClientPackets.SummonPet(TemplateId), token);
		var spawn = await driver.WaitAsync(p => Action(p, 3), token);
		Require(spawn.Get<int>("petObjectId") == petId && spawn.Get<int>("templateId") == TemplateId
			&& spawn.Get<int>("masterObjectId") == driver.CharacterId && spawn.Get<string>("petName") == PetName,
			"Summoned pet is not the adopted pet owned by this subject.");
		await driver.SynchronizeAsync(token);
	}
	private static async Task DismissAsync(IPetLifecycleDriver driver, int petId, CancellationToken token)
	{
		await driver.SendAsync(GameClientPackets.DismissPet(TemplateId), token);
		var dismiss = await driver.WaitAsync(p => Action(p, 4), token);
		Require(dismiss.Get<int>("petObjectId") == petId, "Dismissal removed a different pet.");
		await driver.SynchronizeAsync(token);
	}
	private static void AssertInventory(IPetLifecycleDriver driver, Dictionary<int, BotInventoryItem> expected) =>
		Require(expected.OrderBy(p => p.Key).SequenceEqual(driver.Api.World.Inventory.OrderBy(p => p.Key)), "Egg/feeding changed inventory unexpectedly.");
	private static void Require(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
}
