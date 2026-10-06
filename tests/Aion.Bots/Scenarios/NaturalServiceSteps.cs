using Aion.Bots.Protocol;
using Aion.Bots.World;
using Aion.GameServer.Model;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.Bots.Scenarios;

/// <summary>What a service step decided or observed: "ready" to act, "done", or why it did not act.</summary>
public sealed record NaturalServiceOutcome(string Outcome, string Reason)
{
	public bool IsReady => Outcome == "ready";
	public bool IsDone => Outcome == "done";
	public static NaturalServiceOutcome Ready { get; } = new("ready", "");
}

public sealed record NaturalSale(int ObjectId, int ItemId, long Count);
public sealed record NaturalPurchase(int ItemId, long Count);
public sealed record NaturalRefusedPurchase(NaturalPurchase Purchase, string Reason);

/// <summary>What will be bought after the trade window was observed; purchases are capped by observed Kinah.</summary>
public sealed record NaturalVendorPlan(IReadOnlyList<NaturalPurchase> Buys, IReadOnlyList<NaturalRefusedPurchase> Refused, long Cost);

public sealed record NaturalVendorResult(int VendorObjectId, IReadOnlyList<NaturalSale> Sold, IReadOnlyList<NaturalPurchase> Bought,
	IReadOnlyList<NaturalRefusedPurchase> Refused, long KinahBefore, long KinahAfter);

/// <summary>
/// NA-08: pure checks for binding at an obelisk, taking an NPC teleporter and trading with a vendor on any map.
/// Everything is decided from client-observed state; the server still validates every request.
/// </summary>
public static class NaturalServicePolicy
{
	/// <summary>SM_QUESTION_WINDOW.STR_ASK_REGISTER_RESURRECT_POINT, asked by Java ResurrectAI.</summary>
	public const int BindQuestionId = 160012;

	/// <summary>ResurrectAI refuses a second bind within 20 m of the same obelisk.</summary>
	public const float SameObeliskRadius = 20;

	/// <summary>AX-12b: soul healing. Java DialogService answers dialog action RECOVERY with
	/// SM_QUESTION_WINDOW.STR_ASK_RECOVER_EXPERIENCE, whose parameter is the price. Soul Healers carry title 350412.</summary>
	public const int SoulHealQuestionId = 160011, SoulHealDialogAction = 35, SoulHealerTitleId = 350412;

	/// <summary>Java: <c>(int) (expLost * (expLost &lt; 1000000 ? 0.25 - (0.00000015 * expLost) : 0.1))</c>.</summary>
	public static long SoulHealPrice(long recoverable) => (int)(recoverable * (recoverable < 1_000_000 ? 0.25 - (0.00000015 * recoverable) : 0.1));

	public static NaturalServiceOutcome Bind(BotPosition self, int selfMap, BotPosition obelisk, int obeliskMap,
		float acceptRange, long kinah, long price, BotBindPoint? current)
	{
		if (current is { } bound && bound.MapId == obeliskMap && Distance(bound.Position, obelisk) < SameObeliskRadius)
			return new("already-bound", $"Bound {Distance(bound.Position, obelisk):F1} m from this obelisk.");
		if (selfMap != obeliskMap) return new("wrong-map", $"On map {selfMap}, the obelisk is on {obeliskMap}.");
		if (Distance(self, obelisk) > acceptRange)
			return new("too-far", $"{Distance(self, obelisk):F1} m from the obelisk; binding needs {acceptRange} m.");
		if (kinah < price) return new("not-enough-kinah", $"{kinah} Kinah, the bind costs {price}.");
		return NaturalServiceOutcome.Ready;
	}

	public static NaturalServiceOutcome Teleport(BotPosition self, BotPosition npc, float talkRange, long kinah, long fare, bool flying)
	{
		if (flying) return new("flying", "The teleporter refuses a flying player.");
		if (Distance(self, npc) > talkRange)
			return new("too-far", $"{Distance(self, npc):F1} m from the teleporter; talking needs {talkRange} m.");
		if (kinah < fare) return new("not-enough-kinah", $"{kinah} Kinah, the fare is {fare}.");
		return NaturalServiceOutcome.Ready;
	}

	/// <summary>Buy what the observed trade window offers, in order, while observed Kinah lasts.</summary>
	public static NaturalVendorPlan Vendor(IReadOnlyCollection<int> offeredTabs, Func<int, IReadOnlyCollection<int>> tabItems,
		IEnumerable<NaturalPurchase> wanted, Func<int, long> unitPrice, long kinah)
	{
		var buys = new List<NaturalPurchase>();
		var refused = new List<NaturalRefusedPurchase>();
		long cost = 0;
		foreach (NaturalPurchase purchase in wanted)
		{
			if (purchase.Count <= 0) continue;
			if (!offeredTabs.Any(tab => tabItems(tab).Contains(purchase.ItemId)))
			{
				refused.Add(new(purchase, "not-in-trade-list"));
				continue;
			}
			long price = unitPrice(purchase.ItemId);
			long affordable = price <= 0 ? purchase.Count : Math.Min(purchase.Count, (kinah - cost) / price);
			if (affordable <= 0)
			{
				refused.Add(new(purchase, "not-enough-kinah"));
				continue;
			}
			if (affordable < purchase.Count) refused.Add(new(purchase with { Count = purchase.Count - affordable }, "not-enough-kinah"));
			buys.Add(purchase with { Count = affordable });
			cost += affordable * price;
		}
		return new(buys, refused, cost);
	}

	internal static float Distance(BotPosition a, BotPosition b) =>
		MathF.Sqrt(MathF.Pow(a.X - b.X, 2) + MathF.Pow(a.Y - b.Y, 2) + MathF.Pow(a.Z - b.Z, 2));
}

/// <summary>AX-12b: one soul healing as the client saw it: the recoverable XP that came back and the Kinah it cost.</summary>
public sealed record NaturalSoulHeal(int HealerNpcId, long Recovered, long Price, long KinahBefore, long KinahAfter, long AtMillis);

/// <summary>NA-08: the client side of those services on any map, verified by client-observed packets and Kinah.</summary>
public sealed class NaturalServiceSteps(INaturalJourneySession session)
{
	private BotWorldModel World => session.Api.World;

	/// <summary>Talk to an obelisk the character stands within <paramref name="acceptRange"/> of, accept its question and
	/// verify SM_BIND_POINT_INFO and the charged price (a bind is charged raw, without price modifiers).</summary>
	public async Task<NaturalServiceOutcome> BindAsync(int obeliskObjectId, BotPosition obelisk, int obeliskMap, long price,
		float acceptRange, CancellationToken token)
	{
		NaturalServiceOutcome check = NaturalServicePolicy.Bind(session.CurrentPosition, World.MapId ?? 0, obelisk, obeliskMap,
			acceptRange, World.Kinah, price, World.ObeliskBindPoint);
		Trace("service-bind", check, new() { ["obelisk"] = obeliskObjectId, ["price"] = price });
		if (!check.IsReady) return check;
		long before = World.Kinah;
		await session.SendPacketAsync(session.Api.TalkTo(obeliskObjectId), token);
		DecodedBotServerPacket question = await session.WaitForPacketAsync(typeof(SM_QUESTION_WINDOW), token,
			packet => packet.Get<int>("code") == NaturalServicePolicy.BindQuestionId);
		await session.SendPacketAsync(GameClientPackets.QuestionResponse(question.Get<int>("code"), 1, question.Get<int>("senderId")), token);
		await session.WaitForPacketAsync(typeof(SM_BIND_POINT_INFO), token);
		await session.SynchronizeAsync(token);
		NaturalServiceOutcome result = World.ObeliskBindPoint is { } bound && bound.MapId == obeliskMap &&
			NaturalServicePolicy.Distance(bound.Position, obelisk) < NaturalServicePolicy.SameObeliskRadius && before - World.Kinah == price
			? new("done", $"Bound on map {obeliskMap} for {price} Kinah.")
			: new("refused", $"Bind not observed (bind {World.ObeliskBindPoint}, Kinah {before} -> {World.Kinah}).");
		Trace("service-bind-result", result, new() { ["kinahBefore"] = before, ["kinahAfter"] = World.Kinah });
		return result;
	}

	/// <summary>Open the teleporter's airline service, choose <paramref name="locationId"/>, follow the world change and
	/// verify the fare Java's PricesService charges, as SM_PRICES shows it to the client.</summary>
	public async Task<NaturalServiceOutcome> TeleportAsync(int npcObjectId, BotPosition npc, float talkRange, int locationId,
		long basePrice, int destinationMap, CancellationToken token)
	{
		long fare = World.VendorPrices?.ServicePrice(basePrice) ?? basePrice;
		NaturalServiceOutcome check = NaturalServicePolicy.Teleport(session.CurrentPosition, npc, talkRange, World.Kinah, fare, flying: false);
		Trace("service-teleport", check, new() { ["npc"] = npcObjectId, ["location"] = locationId, ["fare"] = fare });
		if (!check.IsReady) return check;
		long before = World.Kinah;
		await NaturalDialogProtocol.OpenAsync(session, npcObjectId, token);
		await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token, packet => packet.Get<int>("targetObjectId") == npcObjectId);
		await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(npcObjectId, DialogAction.AIRLINE_SERVICE), token);
		await session.WaitForPacketAsync(typeof(SM_TELEPORT_MAP), token);
		bool otherMap = World.MapId != destinationMap;
		if (otherMap) World.BeginWorldReload();
		await session.SendPacketAsync(session.Api.Teleport(npcObjectId, locationId), token);
		if (otherMap)
		{
			await session.WaitForPacketAsync(typeof(SM_PLAYER_SPAWN), token, packet => packet.Get<int>("worldId") == destinationMap);
			await session.WaitForPacketAsync(typeof(SM_PLAYER_INFO), token, packet => packet.Get<int>("objectId") == session.CharacterId);
		}
		else
			await session.WaitForPacketAsync(typeof(SM_CHANNEL_INFO), token);
		session.AcceptTeleportPosition();
		await session.SynchronizeAsync(token);
		NaturalServiceOutcome result = World.MapId == destinationMap && before - World.Kinah == fare
			? new("done", $"Teleported to {destinationMap} for {fare} Kinah.")
			: new("refused", $"Teleport not observed (map {World.MapId}, Kinah {before} -> {World.Kinah}, fare {fare}).");
		Trace("service-teleport-result", result, new() { ["kinahBefore"] = before, ["kinahAfter"] = World.Kinah });
		return result;
	}

	/// <summary>The maintainer's 2026-10-01 note: take a flight transporter between hubs. Open the airline service, choose the
	/// route's location, fly the client route after START_FLYTELEPORT (CM_MOVE_IN_AIR), land with LAND_FLYTELEPORT, and verify
	/// the fare Java charges (as SM_PRICES shows it) and the landing.</summary>
	public async Task<NaturalServiceOutcome> FlyAsync(int npcObjectId, BotPosition npc, float talkRange, NaturalAirlineRoute route,
		CancellationToken token)
	{
		long fare = World.VendorPrices?.ServicePrice(route.Price) ?? route.Price;
		NaturalServiceOutcome check = NaturalServicePolicy.Teleport(session.CurrentPosition, npc, talkRange, World.Kinah, fare, flying: false);
		Trace("service-flight", check, new() { ["npc"] = npcObjectId, ["location"] = route.LocationId, ["route"] = route.Route, ["fare"] = fare });
		if (!check.IsReady) return check;
		long before = World.Kinah;
		await NaturalDialogProtocol.OpenAsync(session, npcObjectId, token);
		await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token, packet => packet.Get<int>("targetObjectId") == npcObjectId);
		await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(npcObjectId, DialogAction.AIRLINE_SERVICE), token);
		await session.WaitForPacketAsync(typeof(SM_TELEPORT_MAP), token);
		await session.SendPacketAsync(session.Api.Teleport(npcObjectId, route.LocationId), token);
		await session.WaitForPacketAsync(typeof(SM_EMOTION), token, packet =>
			packet.Get<int>("senderObjectId") == session.CharacterId &&
			packet.Get<byte>("emotionType") == (byte)EmotionType.START_FLYTELEPORT && packet.Get<int>("teleportId") == route.TeleportId);
		await session.ExecuteMovementAsync(NaturalAirlineRoutes.Plan(route), token);
		await session.SendPacketAsync(GameClientPackets.Emotion((byte)EmotionType.LAND_FLYTELEPORT), token);
		await session.SynchronizeAsync(token);
		float landed = NaturalServicePolicy.Distance(session.CurrentPosition, route.Landing);
		NaturalServiceOutcome result = landed < 8 && before - World.Kinah == fare
			? new("done", $"Flew {route.Route} for {fare} Kinah.")
			: new("refused", $"Flight not observed (landed {landed:F1} m from {route.Landing}, Kinah {before} -> {World.Kinah}, fare {fare}).");
		Trace("service-flight-result", result, new() { ["kinahBefore"] = before, ["kinahAfter"] = World.Kinah });
		return result;
	}

	/// <summary>Sell, then buy what the observed trade window offers, verifying the observed stock of each purchase.</summary>	/// <summary>Sell, then buy what the observed trade window offers, verifying the observed stock of each purchase.</summary>
	/// <param name="tabItems">Static goods-list contents by tab id.</param>
	/// <param name="basePrice">Static template price by item id; the vendor's modifiers are applied from SM_PRICES.</param>
	public async Task<NaturalVendorResult> TradeAsync(int vendorObjectId, IReadOnlyList<NaturalSale> sales,
		IReadOnlyList<NaturalPurchase> purchases, Func<int, IReadOnlyCollection<int>> tabItems, Func<int, long> basePrice,
		CancellationToken token)
	{
		long before = World.Kinah;
		var sold = new List<NaturalSale>();
		await NaturalDialogProtocol.OpenAsync(session, vendorObjectId, token);
		await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token, packet => packet.Get<int>("targetObjectId") == vendorObjectId);
		if (sales.Count > 0)
		{
			await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(vendorObjectId, 3), token);
			await session.WaitForPacketAsync(typeof(SM_SELL_ITEM), token);
			foreach (NaturalSale sale in sales)
			{
				await session.SendPacketAsync(session.Api.Sell(vendorObjectId, [(sale.ObjectId, sale.Count)]), token);
				await session.SynchronizeAsync(token);
				if (!World.Inventory.TryGetValue(sale.ObjectId, out BotInventoryItem? left) || left.Count < sale.Count) sold.Add(sale);
			}
		}
		var bought = new List<NaturalPurchase>();
		IReadOnlyList<NaturalRefusedPurchase> refused = [];
		if (purchases.Count > 0)
		{
			await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(vendorObjectId, 2), token);
			await session.WaitForPacketAsync(typeof(SM_TRADELIST), token);
			BotTradeWindow trade = World.Trade is { } window && window.TargetObjectId == vendorObjectId
				? window : throw new InvalidDataException("Vendor sent no trade window.");
			BotVendorPrices prices = World.VendorPrices ?? throw new InvalidDataException("No client-observed vendor prices.");
			NaturalVendorPlan plan = NaturalServicePolicy.Vendor(trade.Tabs.ToArray(), tabItems, purchases,
				item => prices.BuyPrice(basePrice(item), trade.BuyPriceModifier), World.Kinah);
			refused = plan.Refused;
			foreach (NaturalPurchase purchase in plan.Buys)
			{
				long stock = World.Inventory.Values.Where(item => item.ItemId == purchase.ItemId).Sum(item => item.Count);
				await session.SendPacketAsync(session.Api.Buy(vendorObjectId, [(purchase.ItemId, purchase.Count)]), token);
				await session.SynchronizeAsync(token);
				long observed = World.Inventory.Values.Where(item => item.ItemId == purchase.ItemId).Sum(item => item.Count);
				if (observed != stock + purchase.Count)
					throw new InvalidDataException($"Purchase of {purchase.Count}x {purchase.ItemId} not observed: {stock} -> {observed}.");
				bought.Add(purchase);
			}
		}
		await session.SendPacketAsync(session.Api.CloseDialog(vendorObjectId), token);
		var result = new NaturalVendorResult(vendorObjectId, sold, bought, refused, before, World.Kinah);
		session.TraceDiagnostic("service-trade", new Dictionary<string, object?>
		{
			["vendor"] = vendorObjectId, ["sold"] = sold.Count, ["bought"] = bought.Select(b => $"{b.ItemId}x{b.Count}").ToArray(),
			["refused"] = refused.Select(r => $"{r.Purchase.ItemId}:{r.Reason}").ToArray(),
			["kinahBefore"] = before, ["kinahAfter"] = World.Kinah,
		});
		return result;
	}

	/// <summary>AX-12b: soul healing at the Soul Healer the character stands beside. Talk, choose the healing (RECOVERY) and
	/// accept the priced question. From the client's view afterwards: no recoverable XP is left, the XP bar is up by what was
	/// recoverable, and the Kinah charged is the price the question named. With nothing to recover the server asks nothing and
	/// takes the soul sickness off for free.</summary>
	public async Task<NaturalSoulHeal> SoulHealAsync(int healerObjectId, int healerNpcId, long nowMillis, CancellationToken token)
	{
		if (!World.Objects.TryGetValue(healerObjectId, out BotKnownObject? known) || known.TemplateId != healerNpcId)
			throw new InvalidDataException($"The observed NPC is not the Soul Healer {healerNpcId}.");
		long kinah = World.Kinah, recoverable = World.RecoverableExperience, experience = World.CurrentExperience, price = 0;
		await NaturalDialogProtocol.OpenAsync(session, healerObjectId, token);
		await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token, packet => packet.Get<int>("targetObjectId") == healerObjectId);
		await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(healerObjectId, NaturalServicePolicy.SoulHealDialogAction), token);
		if (recoverable > 0)
		{
			DecodedBotServerPacket question = await session.WaitForPacketAsync(typeof(SM_QUESTION_WINDOW), token,
				packet => packet.Get<int>("code") == NaturalServicePolicy.SoulHealQuestionId);
			price = long.Parse(question.Get<string[]>("params")[0], System.Globalization.CultureInfo.InvariantCulture);
			await session.SendPacketAsync(GameClientPackets.QuestionResponse(question.Get<int>("code"), 1, question.Get<int>("senderId")), token);
			await session.WaitForPacketAsync(typeof(SM_STATUPDATE_EXP), token);
		}
		await session.SynchronizeAsync(token);
		var heal = new NaturalSoulHeal(healerNpcId, recoverable, price, kinah, World.Kinah, nowMillis);
		NaturalServiceOutcome result = World.RecoverableExperience == 0 && kinah - World.Kinah == price &&
			World.CurrentExperience - experience == recoverable && price == NaturalServicePolicy.SoulHealPrice(recoverable)
			? new("done", $"Soul healed: {recoverable} XP back for {price} Kinah.")
			: new("refused", $"Soul healing not observed (recoverable {recoverable} -> {World.RecoverableExperience}, XP {experience} -> " +
				$"{World.CurrentExperience}, Kinah {kinah} -> {World.Kinah}, price asked {price}).");
		Trace("service-soul-heal", result, new()
		{
			["healer"] = healerNpcId, ["recovered"] = recoverable, ["price"] = price, ["kinahBefore"] = kinah, ["kinahAfter"] = World.Kinah,
		});
		if (!result.IsDone) throw new InvalidDataException(result.Reason);
		return heal;
	}

	private void Trace(string action, NaturalServiceOutcome outcome, Dictionary<string, object?> fields)
	{
		fields["outcome"] = outcome.Outcome;
		fields["reason"] = outcome.Reason;
		fields["position"] = session.CurrentPosition;
		session.TraceDiagnostic(action, fields);
	}
}
