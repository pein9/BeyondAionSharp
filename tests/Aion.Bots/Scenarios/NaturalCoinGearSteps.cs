using Aion.Bots.Protocol;
using Aion.Bots.World;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.Model.Templates.Tradelist;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.Bots.Scenarios;

/// <summary>CG's narrowly authorized reward shop and armour packets, shared by SIM and LIVE.</summary>
public sealed class NaturalCoinGearSteps(INaturalJourneySession session, StaticData data,
	NaturalCoinGear gear, NaturalIshalgenInventoryPolicy inventory)
{
	/// <summary>Freeze retained equipment only after the approved ordinary staff equip has been observed.</summary>
	public static async Task<NaturalJourneyItem[]> PrepareRetainedLoadoutAsync(INaturalJourneySession session,
		NaturalCoinGear gear, CancellationToken token)
	{
		await EnsureRetainedStaffEquippedAsync(session, gear, token);
		HashSet<ushort> replacedSlots = gear.Purchases.Select(p => p.Slot).ToHashSet();
		NaturalJourneyItem[] retained = session.Api.World.Inventory.Values
			.Where(i => i.EquipmentSlot is > 0 and < 65535 && i.EquipmentSlot is not (8192 or 16384) &&
				!replacedSlots.Contains(i.EquipmentSlot)) // Power shards are consumable; only manifest armour may change.
			.Select(i => new NaturalJourneyItem(i.ObjectId, i.ItemId, i.Count, i.EquipmentSlot)).ToArray();
		session.TraceDiagnostic("coin-preparation-loadout-frozen", new Dictionary<string, object?> { ["retained"] = retained });
		return retained;
	}

	public static void VerifyRetainedLoadout(BotWorldModel observed, IReadOnlyList<NaturalJourneyItem> retained)
	{
		foreach (NaturalJourneyItem original in retained)
			NaturalJourneyRequirements.True(observed.Inventory.TryGetValue(original.ObjectId, out BotInventoryItem? item) &&
				item.ItemId == original.ItemId && item.Count == original.Count && item.EquipmentSlot == original.EquipmentSlot,
				$"CG changed retained equipped object {original.ObjectId}/{original.ItemId}.");
	}

	/// <summary>Wear the already earned, approved staff before CG freezes its loadout. No purchase or grant.</summary>
	public static async Task EnsureRetainedStaffEquippedAsync(INaturalJourneySession session,
		NaturalCoinGear gear, CancellationToken token)
	{
		BotWorldModel world = session.Api.World;
		BotInventoryItem[] owned = world.Inventory.Values.Where(i => i.ItemId == gear.StaffItemId).ToArray();
		if (world.IsDead || owned.Length != 1 || owned[0].Count != 1)
			throw new InvalidDataException("Coin preparation needs the already earned, unique approved staff.");
		BotInventoryItem staff = owned[0];
		if (staff.EquipmentSlot == 3) return;
		long kinah = world.Kinah, coins = world.Inventory.Values.Where(i => i.ItemId == gear.CoinItemId).Sum(i => i.Count);
		await session.SendPacketAsync(session.Api.Equip(0, NaturalGearPolicy.MainHand, staff.ObjectId), token);
		await session.SynchronizeAsync(token);
		if (!world.Inventory.TryGetValue(staff.ObjectId, out BotInventoryItem? equipped) ||
			equipped.ItemId != gear.StaffItemId || equipped.Count != 1 || equipped.EquipmentSlot != 3 ||
			world.Kinah != kinah || world.Inventory.Values.Where(i => i.ItemId == gear.CoinItemId).Sum(i => i.Count) != coins)
			throw new InvalidDataException("The ordinary approved staff equip or unchanged currency was not observed.");
		session.TraceDiagnostic("coin-preparation-staff-equipped", new Dictionary<string, object?>
			{ ["itemId"] = gear.StaffItemId, ["objectId"] = staff.ObjectId, ["slot"] = 3, ["kinah"] = kinah, ["coins"] = coins });
	}

	private NaturalAltgardObservation Observe(NaturalCoinGearProgress progress) =>
		NaturalAltgardObservation.Observe(session.Api.World, session.CurrentPosition,
			freeCubeSlots: inventory.Decide(session.Api.World, coinGear: gear).FreeSlots, coinGearProgress: progress);

	public async Task<BotTradeWindow> OpenShopAsync(int vendor, CancellationToken token)
	{
		if (!session.Api.World.Objects.TryGetValue(vendor, out BotKnownObject? known) || known.TemplateId != gear.VendorNpcId)
			throw new InvalidDataException("The observed seller is not the approved coin vendor.");
		await NaturalDialogProtocol.OpenAsync(session, vendor, token);
		await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token, p => p.Get<int>("targetObjectId") == vendor);
		await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(vendor, 2), token);
		await session.WaitForPacketAsync(typeof(SM_TRADELIST), token, p => p.Get<int>("targetObjectId") == vendor);
		BotTradeWindow trade = session.Api.World.Trade ?? throw new InvalidDataException("No observed reward shop.");
		// Java writes TradeNpcType.index(), not its ordinal.
		if (trade.NpcType != TradeNpcType.REWARD.Index() || !trade.ShowBuyTab || !trade.Tabs.Contains(gear.GoodsListId))
			throw new InvalidDataException("The observed shop does not offer the audited chain tab.");
		var offered = data.GoodsListDataDh.GetGoodsListById(gear.GoodsListId)?.GetItemIdList() ?? [];
		foreach (NaturalCoinGearPurchase purchase in gear.Purchases)
		{
			Acquisition? cost = data.ItemDataDh.GetItemTemplate(purchase.ItemId)?.GetAcquisition();
			if (!offered.Contains(purchase.ItemId) || cost == null || cost.Type != AcquisitionType.REWARD ||
				cost.ItemId != gear.CoinItemId || cost.ItemCount != purchase.Cost || cost.Ap != 0)
				throw new InvalidDataException($"The actual reward offer differs for {purchase.ItemId}.");
		}
		session.TraceDiagnostic("coin-shop-offer", new Dictionary<string, object?>
			{ ["vendor"] = vendor, ["type"] = trade.NpcType, ["tabs"] = trade.Tabs, ["manifest"] = gear.Purchases });
		return trade;
	}

	public async Task<NaturalCoinGearProgress> PurchaseAsync(int vendor, int itemId,
		NaturalCoinGearProgress progress, CancellationToken token)
	{
		NaturalAltgardObservation before = Observe(progress);
		NaturalCoinGearDecision next = NaturalCoinGearPolicy.Decide(gear, before);
		if (next.Action != "coin-purchase" || next.ItemId != itemId || next.Outcome != "planned")
			throw new InvalidDataException($"Coin purchase refused: {next.Reason}");
		await OpenShopAsync(vendor, token);
		await session.SendPacketAsync(GameClientPackets.BuyItem(vendor, 15, [(itemId, 1)]), token);
		await session.SynchronizeAsync(token);
		NaturalCoinGearProgress result = progress.ObservePurchase(gear, itemId, before, Observe(progress));
		await session.SendPacketAsync(session.Api.CloseDialog(vendor), token);
		session.TraceDiagnostic("coin-purchase-receipt", new Dictionary<string, object?> { ["receipt"] = result.Purchases.Last() });
		return result;
	}

	public async Task EquipAsync(int itemId, NaturalCoinGearProgress progress, CancellationToken token)
	{
		NaturalCoinGearDecision next = NaturalCoinGearPolicy.Decide(gear, Observe(progress));
		if (next.Action != "coin-equip" || next.ItemId != itemId || next.Outcome != "planned")
			throw new InvalidDataException($"Coin equip refused: {next.Reason}");
		NaturalCoinGearPurchase purchase = gear.Purchases.Single(p => p.ItemId == itemId);
		NaturalCoinPurchaseReceipt receipt = progress.Purchases.Single(p => p.ItemId == itemId);
		await session.SendPacketAsync(session.Api.Equip(0, purchase.Slot, receipt.ObjectId), token);
		await session.SynchronizeAsync(token);
		BotInventoryItem owned = session.Api.World.Inventory[receipt.ObjectId];
		if (owned.ItemId != itemId || owned.EquipmentSlot != purchase.Slot ||
			!session.Api.World.Inventory.TryGetValue(progress.StaffObjectId, out BotInventoryItem? staff) ||
			staff.ItemId != gear.StaffItemId || staff.EquipmentSlot != 3)
			throw new InvalidDataException("The armour equip or unchanged staff was not observed.");
		session.TraceDiagnostic("coin-armour-equipped", new Dictionary<string, object?>
			{ ["item"] = itemId, ["object"] = receipt.ObjectId, ["slot"] = purchase.Slot, ["staff"] = progress.StaffObjectId });
	}
}
