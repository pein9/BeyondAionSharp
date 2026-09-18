using Aion.Bots.Api;
using Aion.Bots.Protocol;
using Aion.Bots.World;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.Bots.Scenarios;

public sealed record GatheringTarget(int MapId, int TemplateId, int ItemId, BotPosition Position)
{
	public static GatheringTarget YoungAria { get; } = new(210010000, 400601, 152000401,
		new BotPosition(1198.3f, 1066.82f, 137.2875f, 0));
	public static GatheringTarget YoungAzpha { get; } = new(220010000, 400651, 152000451,
		new BotPosition(577.529f, 2817.34f, 303.613f, 0));
	public const int HarvestCount = 3;
	public static TimeSpan RespawnDelay { get; } = TimeSpan.FromSeconds(295);
}

/// <summary>Mode-specific setup, clock and observation plumbing; E1's actions and inventory assertions are shared.</summary>
public interface IGatheringScenarioDriver
{
	BotApi Api { get; }
	Task StepAsync(string action, Func<CancellationToken, Task> operation, CancellationToken cancellationToken);
	Task<int> PrepareAsync(GatheringTarget target, CancellationToken cancellationToken);
	Task SendAsync(BotClientPacket packet, CancellationToken cancellationToken);
	Task<DecodedBotServerPacket> WaitAsync(Type packetType, Func<DecodedBotServerPacket, bool> predicate,
		CancellationToken cancellationToken);
	Task AwaitHarvestAsync(CancellationToken cancellationToken);
	Task SynchronizeAsync(CancellationToken cancellationToken);
	Task VerifyReleasedAsync(int objectId, bool depleted, CancellationToken cancellationToken);
	Task VerifyRespawnAsync(GatheringTarget target, int oldObjectId, CancellationToken cancellationToken);
}

/// <summary>
/// E1: Java GatheringTask/GatherableController at ce54b7931, with the starter-zone XML's three uses and 295s respawn.
/// Requires the deterministic pass/fail gathering profile, not increased gathering skill or granted products.
/// </summary>
public static class GatheringScenario
{
	public static async Task RunAsync(IGatheringScenarioDriver driver, GatheringTarget target,
		CancellationToken cancellationToken = default)
	{
		int objectId = 0;
		await driver.StepAsync("prepare-gatherable", async token =>
		{
			objectId = await driver.PrepareAsync(target, token);
			await driver.SynchronizeAsync(token);
		}, cancellationToken);
		Dictionary<int, long> expected = Totals(driver.Api.World);
		for (int harvest = 1; harvest <= GatheringTarget.HarvestCount; harvest++)
		{
			bool depleted = harvest == GatheringTarget.HarvestCount;
			await driver.StepAsync($"harvest-{harvest}", async token =>
			{
				BotInventoryItem? existing = driver.Api.World.Inventory.Values.SingleOrDefault(item => item.ItemId == target.ItemId);
				foreach (BotClientPacket packet in driver.Api.Gather(objectId))
					await driver.SendAsync(packet, token);
				DecodedBotServerPacket initial = await driver.WaitAsync(typeof(SM_GATHER_UPDATE), _ => true, token);
				Require(initial.Get<byte>("action") == 0, "Gathering did not start.");
				await driver.AwaitHarvestAsync(token);
				DecodedBotServerPacket result = await driver.WaitAsync(typeof(SM_GATHER_UPDATE),
					packet => packet.Get<byte>("action") >= 5, token);
				Require(result.Get<byte>("action") == 6, $"Gathering ended with action {result.Get<byte>("action")}.");
				Require(result.Get<int>("itemId") == target.ItemId, "Gathering reported the wrong product.");
				// The completion notification precedes ItemService.AddItem, whose DB write may still be running in LIVE.
				// A later ping/time-check can overtake that write, so wait for the actual inventory notification first.
				if (existing == null)
					await driver.WaitAsync(typeof(SM_INVENTORY_ADD_ITEM), packet =>
						packet.Get<List<IReadOnlyDictionary<string, object?>>>("items")
							.Any(item => item["itemId"] is int id && id == target.ItemId), token);
				else
					await driver.WaitAsync(typeof(SM_INVENTORY_UPDATE_ITEM),
						packet => packet.Get<int>("objectId") == existing.ObjectId, token);
				if (depleted)
					await driver.WaitAsync(typeof(SM_DELETE), packet => packet.Get<int>("objectId") == objectId, token);
				await driver.SynchronizeAsync(token);
				expected[target.ItemId] = expected.GetValueOrDefault(target.ItemId) + 1;
				Require(expected.OrderBy(pair => pair.Key).SequenceEqual(Totals(driver.Api.World).OrderBy(pair => pair.Key)),
					$"Gathering changed inventory by something other than exactly one product (including kinah). " +
					$"Expected: {string.Join(',', expected.OrderBy(pair => pair.Key))}; " +
					$"actual: {string.Join(',', Totals(driver.Api.World).OrderBy(pair => pair.Key))}.");
				Require(driver.Api.World.Objects.ContainsKey(objectId) != depleted,
					"Gatherable did not retain exactly three uses.");
				await driver.VerifyReleasedAsync(objectId, depleted, token);
			}, cancellationToken);
		}
		await driver.StepAsync("verify-295-second-respawn",
			token => driver.VerifyRespawnAsync(target, objectId, token), cancellationToken);
	}

	private static Dictionary<int, long> Totals(BotWorldModel world) => world.Inventory.Values
		.GroupBy(item => item.ItemId).ToDictionary(group => group.Key, group => group.Sum(item => item.Count));
	private static void Require(bool condition, string message)
	{
		if (!condition)
			throw new InvalidDataException(message);
	}
}
