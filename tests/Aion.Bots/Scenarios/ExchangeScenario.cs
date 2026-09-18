using Aion.Bots.Api;
using Aion.Bots.Protocol;
using Aion.Bots.World;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.Bots.Scenarios;

public interface IExchangeScenarioDriver
{
	BotApi Api { get; }
	int CharacterId { get; }
	string CharacterName { get; }
	Task StepAsync(string action, Func<CancellationToken, Task> operation, CancellationToken token);
	Task SendAsync(BotClientPacket packet, CancellationToken token);
	Task<DecodedBotServerPacket> WaitAsync(Type type, Func<DecodedBotServerPacket, bool> predicate, CancellationToken token);
	Task SynchronizeAsync(CancellationToken token);
	Task DelayAsync(TimeSpan delay, CancellationToken token);
	Task VerifyReleasedAsync(CancellationToken token);
}

/// <summary>E6: cancel a partial/full-stack exchange, then commit one with items and kinah in both directions.</summary>
public static class ExchangeScenario
{
	public const int FirstItem = 152000401;
	public const int SecondItem = 152000451;
	public const int SetupCount = 10;
	public const long FirstKinah = 125;
	public const long SecondKinah = 40;

	public static async Task RunAsync(IExchangeScenarioDriver first, IExchangeScenarioDriver second, CancellationToken token = default)
	{
		await first.SynchronizeAsync(token);
		await second.SynchronizeAsync(token);
		var firstBefore = Totals(first);
		var secondBefore = Totals(second);
		Require(firstBefore.GetValueOrDefault(FirstItem) == SetupCount && secondBefore.GetValueOrDefault(SecondItem) == SetupCount,
			"Exchange setup did not provide both item stacks.");
		Require(first.Api.World.Kinah >= FirstKinah && second.Api.World.Kinah >= SecondKinah, "Insufficient exchange setup kinah.");
		foreach (bool cancel in new[] { true, false })
		{
			string label = cancel ? "cancel" : "commit";
			await BothStepAsync($"{label}-request-and-accept", async stepToken =>
			{
				await first.SendAsync(first.Api.TradeRequest(second.CharacterId), stepToken);
				var question = await second.WaitAsync(typeof(SM_QUESTION_WINDOW), packet => packet.Get<int>("code") == 90001, stepToken);
				Require(question.Get<string[]>("params")[0] == first.CharacterName, "Wrong trade requester.");
				await second.SendAsync(second.Api.Answer(1), stepToken);
				await first.WaitAsync(typeof(SM_EXCHANGE_REQUEST), packet => packet.Get<string>("receiver") == second.CharacterName, stepToken);
				await second.WaitAsync(typeof(SM_EXCHANGE_REQUEST), packet => packet.Get<string>("receiver") == first.CharacterName, stepToken);
			}, token);
			await BothStepAsync($"{label}-offer-items-and-kinah", async stepToken =>
			{
				await OfferAsync(first, second, FirstItem, 3, FirstKinah, stepToken);
				await OfferAsync(second, first, SecondItem, SetupCount, SecondKinah, stepToken);
				// Offered items are hidden from the client cube, but not yet transferred to the other player.
				var firstEscrow = new Dictionary<int, long>(firstBefore);
				var secondEscrow = new Dictionary<int, long>(secondBefore);
				Add(firstEscrow, FirstItem, -3);
				Add(secondEscrow, SecondItem, -SetupCount);
				AssertTotals(first, firstEscrow);
				AssertTotals(second, secondEscrow);
			}, token);
			await BothStepAsync($"{label}-lock-and-finish", async stepToken =>
			{
				await first.SendAsync(first.Api.TradeLock(), stepToken);
				await ConfirmationAsync(second, 3, stepToken);
				await second.SendAsync(second.Api.TradeLock(), stepToken);
				await ConfirmationAsync(first, 3, stepToken);
				if (cancel)
				{
					await first.SendAsync(first.Api.TradeCancel(), stepToken);
					await ConfirmationAsync(second, 1, stepToken);
				}
				else
				{
					await first.SendAsync(first.Api.TradeAccept(), stepToken);
					await ConfirmationAsync(second, 2, stepToken);
					await second.SendAsync(second.Api.TradeAccept(), stepToken);
					await ConfirmationAsync(first, 0, stepToken);
					await ConfirmationAsync(second, 0, stepToken);
					await first.WaitAsync(typeof(SM_INVENTORY_UPDATE_ITEM), _ => first.Api.World.Kinah == firstBefore[BotWorldModel.KinahItemId] - FirstKinah + SecondKinah, stepToken);
					await second.WaitAsync(typeof(SM_INVENTORY_UPDATE_ITEM), _ => second.Api.World.Kinah == secondBefore[BotWorldModel.KinahItemId] - SecondKinah + FirstKinah, stepToken);
				}
				await first.DelayAsync(TimeSpan.FromMilliseconds(250), stepToken);
				await first.SynchronizeAsync(stepToken);
				await second.SynchronizeAsync(stepToken);
				var firstExpected = new Dictionary<int, long>(firstBefore);
				var secondExpected = new Dictionary<int, long>(secondBefore);
				if (!cancel)
				{
					Add(firstExpected, FirstItem, -3); Add(secondExpected, FirstItem, 3);
					Add(firstExpected, SecondItem, SetupCount); Add(secondExpected, SecondItem, -SetupCount);
					Add(firstExpected, BotWorldModel.KinahItemId, SecondKinah - FirstKinah);
					Add(secondExpected, BotWorldModel.KinahItemId, FirstKinah - SecondKinah);
				}
				AssertTotals(first, firstExpected);
				AssertTotals(second, secondExpected);
				Require(Combine(firstBefore, secondBefore).SequenceEqual(Combine(Totals(first), Totals(second))), "Exchange violated pair-wide conservation.");
				await first.VerifyReleasedAsync(stepToken);
				await second.VerifyReleasedAsync(stepToken);
			}, token);
		}

		Task BothStepAsync(string action, Func<CancellationToken, Task> operation, CancellationToken stepToken) =>
			first.StepAsync(action, inner => second.StepAsync(action, operation, inner), stepToken);
	}

	private static async Task OfferAsync(IExchangeScenarioDriver giver, IExchangeScenarioDriver receiver,
		int itemId, int count, long kinah, CancellationToken token)
	{
		var item = giver.Api.World.Inventory.Values.Single(value => value.ItemId == itemId);
		await giver.SendAsync(giver.Api.TradeAddItem(item.ObjectId, count), token);
		foreach (var (driver, side) in new[] { (giver, 0), (receiver, 1) })
		{
			var offered = await driver.WaitAsync(typeof(SM_EXCHANGE_ADD_ITEM), packet => packet.Get<byte>("action") == side, token);
			Require(offered.Get<int>("itemId") == itemId && offered.Get<long>("itemCount") == count, "Exchange offer did not preserve item/count.");
		}
		await giver.SendAsync(giver.Api.TradeAddKinah(kinah), token);
		foreach (var (driver, side) in new[] { (giver, 0), (receiver, 1) })
		{
			var offered = await driver.WaitAsync(typeof(SM_EXCHANGE_ADD_KINAH), packet => packet.Get<byte>("action") == side, token);
			Require(offered.Get<long>("kinahCount") == kinah, "Exchange offer did not preserve kinah.");
		}
	}
	private static Task<DecodedBotServerPacket> ConfirmationAsync(IExchangeScenarioDriver driver, byte action, CancellationToken token) =>
		driver.WaitAsync(typeof(SM_EXCHANGE_CONFIRMATION), packet => packet.Get<byte>("action") == action, token);
	private static Dictionary<int, long> Totals(IExchangeScenarioDriver driver) => driver.Api.World.Inventory.Values.GroupBy(item => item.ItemId)
		.ToDictionary(group => group.Key, group => group.Sum(item => item.Count));
	private static void Add(Dictionary<int, long> totals, int id, long count)
	{
		totals[id] = totals.GetValueOrDefault(id) + count;
		if (totals[id] == 0) totals.Remove(id);
	}
	private static IEnumerable<KeyValuePair<int, long>> Combine(Dictionary<int, long> first, Dictionary<int, long> second) =>
		first.Concat(second).GroupBy(pair => pair.Key).Select(group => new KeyValuePair<int, long>(group.Key, group.Sum(pair => pair.Value))).OrderBy(pair => pair.Key);
	private static void AssertTotals(IExchangeScenarioDriver driver, Dictionary<int, long> expected) => Require(
		expected.OrderBy(pair => pair.Key).SequenceEqual(Totals(driver).OrderBy(pair => pair.Key)), $"Exchange inventory mismatch for {driver.CharacterName}.");
	private static void Require(bool condition, string message)
	{
		if (!condition) throw new InvalidDataException(message);
	}
}
