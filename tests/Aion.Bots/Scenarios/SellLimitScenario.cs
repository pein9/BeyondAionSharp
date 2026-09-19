using Aion.Bots.Protocol;
using Aion.Bots.World;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.Bots.Scenarios;

public interface ISellLimitScenarioDriver : IVendorScenarioDriver
{
	Task ReloginAsync(CancellationToken token);
}

/// <summary>L7 keeps the ordinary level-1 account's real 5,300,047-kinah daily cap and dynamic-cap setting off.</summary>
public static class SellLimitScenario
{
	public const int ItemId = 182005453, SetupCount = 64;
	public const long DailyCap = 5_300_047;
	public static async Task RunAsync(ISellLimitScenarioDriver driver, CancellationToken token)
	{
		int vendor = 0;
		await driver.StepAsync("prepare-gold-ingots-and-approach-vendor", async ct =>
		{
			vendor = await driver.PrepareAsync(ct); await driver.SynchronizeAsync(ct);
			Require(driver.Api.World.Inventory.Values.Single(i => i.ItemId == ItemId).Count == SetupCount, "L7 requires 64 ordinary sellable gold ingots.");
		}, token);
		var expected = Totals(driver.Api.World);
		long unitPrice = 0, remaining = DailyCap;
		await driver.StepAsync("open-normal-vendor-sell-dialog", async ct => { unitPrice = await OpenAsync(ct); }, token);
		long capacity = DailyCap / unitPrice;
		Require(capacity > 3 && capacity + 5 < SetupCount, "L7 stock must cross the default cap without exceeding owned stock.");
		await driver.StepAsync("sell-below-daily-cap", ct => SellAsync(capacity - 3, capacity - 3, ct), token);
		await driver.StepAsync("partially-fulfill-sale-at-daily-cap", ct => SellAsync(5, 3, ct), token);
		Require(remaining >= 0 && remaining < unitPrice, "Expected less than one item's price remaining.");
		await driver.StepAsync("refuse-further-sales-without-losing-items-or-kinah", RefuseAsync, token);
		await driver.StepAsync("relogin-keeps-account-sales-cap", async ct =>
		{
			await driver.SendAsync(driver.Api.CloseDialog(vendor), ct); await driver.SynchronizeAsync(ct);
			await driver.ReloginAsync(ct); await VerifyAsync(ct);
			Require(await OpenAsync(ct) == unitPrice, "Relog changed the vendor price.");
			await RefuseAsync(ct);
			await driver.SendAsync(driver.Api.CloseDialog(vendor), ct); await driver.SynchronizeAsync(ct);
		}, token);

		async Task<long> OpenAsync(CancellationToken ct)
		{
			await driver.SendAsync(driver.Api.TalkTo(vendor), ct);
			await driver.WaitAsync(typeof(SM_DIALOG_WINDOW), p => p.Get<int>("targetObjectId") == vendor, ct);
			await driver.SendAsync(driver.Api.SelectDialog(vendor, 3), ct);
			var sell = await driver.WaitAsync(typeof(SM_SELL_ITEM), p => p.Get<int>("targetObjectId") == vendor, ct);
			long price = BotVendorPrices.SellPrice(driver.BasePrice, sell.Get<int>("buyPriceRate"));
			Require(price > 0, "Sell-limit test requires a positive vendor price."); return price;
		}
		async Task SellAsync(long requested, long accepted, CancellationToken ct)
		{
			var item = driver.Api.World.Inventory.Values.Single(i => i.ItemId == ItemId);
			Require(item.Count >= requested, "The bot must not request more items than it owns.");
			await driver.SendAsync(driver.Api.Sell(vendor, [(item.ObjectId, requested)]), ct);
			expected[ItemId] -= accepted; expected[BotWorldModel.KinahItemId] += accepted * unitPrice;
			remaining -= accepted * unitPrice; await VerifyAsync(ct);
		}
		async Task RefuseAsync(CancellationToken ct)
		{
			var item = driver.Api.World.Inventory.Values.Single(i => i.ItemId == ItemId);
			await driver.SendAsync(driver.Api.Sell(vendor, [(item.ObjectId, 1)]), ct);
			var refused = await driver.WaitAsync(typeof(SM_SYSTEM_MESSAGE), p => p.Get<string?>("name") == "STR_MSG_DAY_CANNOT_SELL_NPC", ct);
			VerifyRefusal(refused, remaining); await VerifyAsync(ct);
		}
		async Task VerifyAsync(CancellationToken ct)
		{
			await driver.SynchronizeAsync(ct);
			Require(expected.OrderBy(p => p.Key).SequenceEqual(Totals(driver.Api.World).OrderBy(p => p.Key)), "Daily cap sale changed inventory/kinah incorrectly.");
			await driver.VerifyServerStateAsync(expected, ct);
		}
	}
	public static void VerifyRefusal(DecodedBotServerPacket packet, long remaining)
	{
		Require(packet.PacketType == typeof(SM_SYSTEM_MESSAGE) && packet.Get<string?>("name") == "STR_MSG_DAY_CANNOT_SELL_NPC", "Expected the specific daily-cap refusal.");
		Require(packet.Get<string[]>("params").SequenceEqual(new[] { remaining.ToString(System.Globalization.CultureInfo.InvariantCulture) }), "Refusal reported an incorrect remaining daily allowance.");
	}
	private static Dictionary<int, long> Totals(BotWorldModel world) => world.Inventory.Values.GroupBy(i => i.ItemId)
		.ToDictionary(g => g.Key, g => g.Sum(i => i.Count));
	private static void Require(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
}
