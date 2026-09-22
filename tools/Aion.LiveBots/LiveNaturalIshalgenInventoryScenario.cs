using System.Xml.Linq;
using Aion.Bots.Navigation;
using Aion.Bots.Scenarios;
using Aion.Bots.World;
using Aion.GameServer.Model;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.LiveBots;

public static partial class LiveBotRunner
{
	private const int IshalgenVendor = 203526; // Shipped Ungfu, active trade actions 2/3.

	private static async Task<int> RunNaturalIshalgenInventoryAsync(LiveBotOptions options,
		LiveBotProblemWriter problems, CancellationToken token)
	{
		NaturalIshalgenIdentity identity = NaturalIshalgenIdentityScenario.Identity;
		await using var actor = new L0Actor(options, problems, 1, Race.ASMODIANS, bot: "b01",
			account: identity.AccountName, characterName: identity.CharacterName);
		actor.Trace.WriteAction("s00", "scenario:start", new Dictionary<string, object?> { ["scenario"] = "NI-05" });
		try
		{
			var identityDriver = new LiveNaturalIshalgenIdentityDriver(options, actor, identity);
			NaturalIshalgenIdentityResult subject = await NaturalIshalgenIdentityScenario.EnterAsync(identityDriver, token);
			await WriteNaturalIdentityReceiptAsync(options, identity, subject, token);
			string root = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ScenarioManifest.FindDefaultPath())!, "../.."));
			BotWorldModel world = actor.Session.Api.World;
			if (world.MapId != 220010000 || world.Level is < 1 or > 9 || world.IsDead)
				throw new InvalidDataException("NI-05 requires a living pre-Ascension Priest in Ishalgen.");
			var policy = NaturalIshalgenInventoryPolicy.Load(root, world.Inventory.Values.Select(item => item.ItemId));
			if (!policy.AutoLearnedPriestSkillsObserved(world.Level, world.Skills))
				throw new InvalidDataException("The client did not observe all automatically learned Priest skills for this level.");
			NaturalInventoryPlan plan = policy.Decide(world);
			PublishInventoryDecision(options, actor, plan, "inventory-audit", 1);
			foreach (NaturalInventoryDecision equip in plan.Equips)
			{
				await actor.StepAsync($"equip-{equip.ItemId}", async ct =>
				{
					BotInventoryItem owned = world.Inventory[equip.ObjectId];
					NaturalItem item = policy.Item(equip.ItemId);
					await actor.Session.SendPacketAsync(actor.Session.Api.Equip(0, item.EquipSlot, owned.ObjectId), ct);
					await actor.Session.SynchronizeAsync(ct);
					if (!world.Inventory.TryGetValue(equip.ObjectId, out BotInventoryItem? equipped) ||
						equipped.Details.EquippedSlot != item.EquipSlot)
						throw new InvalidDataException($"Client did not observe item {equip.ItemId} in slot {item.EquipSlot} after equip.");
				}, token);
			}
			plan = policy.Decide(world);
			PublishInventoryDecision(options, actor, plan, "post-equip-inventory", 2);
			if (plan.Sales.Count != 0)
			{
				BotNavigationAssets assets = await BotNavigationAssets.LoadAsync(root,
					Path.Combine(options.OutputDirectory, "navigation-cache"), token);
				int channel = world.ChannelInfo?.Index ?? 0;
				actor.Session.Navigation = assets.StarterRoute(Race.ASMODIANS, channel + 1);
				BotPosition anchor = ReadIshalgenVendorAnchor(root);
				var navigation = new LiveNaturalIshalgenNavigationDriver(options, actor, 2, selectedQuestId: null);
				NaturalNavigationResult result = await NaturalIshalgenNavigator.ApproachNpcAsync(
					220010000, IshalgenVendor, anchor, navigation, token);
				if (!result.Arrived || result.TargetObjectId is not int vendor)
					throw new InvalidDataException($"Could not reach an observed active vendor: {result.Reason}");
				await actor.StepAsync("sell-unneeded-items", async ct =>
				{
					LiveBotSession session = actor.Session;
					await session.SendPacketAsync(session.Api.TalkTo(vendor), ct);
					await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), ct,
						packet => packet.Get<int>("targetObjectId") == vendor);
					await session.SendPacketAsync(session.Api.SelectDialog(vendor, 2), ct);
					var trade = await session.WaitForPacketAsync(typeof(SM_TRADELIST), ct,
						packet => packet.Get<int>("targetObjectId") == vendor);
					if (!trade.Get<bool>("showSellTab"))
						throw new InvalidDataException("The client-observed vendor has no active sell tab.");
					await session.SendPacketAsync(session.Api.SelectDialog(vendor, 3), ct);
					await session.WaitForPacketAsync(typeof(SM_SELL_ITEM), ct,
						packet => packet.Get<int>("targetObjectId") == vendor);
					foreach (NaturalInventoryDecision sale in policy.Decide(world).Sales)
					{
						BotInventoryItem owned = world.Inventory[sale.ObjectId];
						long before = owned.Count;
						long kinah = world.Kinah;
						if (before <= 0 || before > 20000) throw new InvalidDataException("Unsupported sale stack count.");
						await session.SendPacketAsync(session.Api.Sell(vendor, [(owned.ObjectId, before)]), ct);
						await session.SynchronizeAsync(ct);
						if (world.Inventory.TryGetValue(owned.ObjectId, out BotInventoryItem? remaining) && remaining.Count != 0)
							throw new InvalidDataException($"Sale of {owned.ItemId} was not reflected in client inventory.");
						if (world.Kinah < kinah)
							throw new InvalidDataException("Kinah decreased during a sell-only transaction.");
						actor.Trace.WriteAction(actor.LastStep, "natural:item-sold", new Dictionary<string, object?>
						{
							["itemId"] = owned.ItemId, ["objectId"] = owned.ObjectId, ["count"] = before,
							["kinahBefore"] = kinah, ["kinahAfter"] = world.Kinah,
						});
					}
					await session.SendPacketAsync(session.Api.CloseDialog(vendor), ct);
				}, token);
			}
			NaturalInventoryPlan after = policy.Decide(world);
			PublishInventoryDecision(options, actor, after, "inventory-housekeeping-complete", 3);
			if (after.Sales.Count != 0)
				throw new InvalidDataException("Sellable unneeded inventory remains after vendor housekeeping.");
			if (after.CubePressure)
				throw new InvalidDataException("Fewer than three cube slots remain for quest rewards and gathering; no destructive fallback is allowed.");
			if (options.DecisionViewSeconds > 0)
				await Task.Delay(TimeSpan.FromSeconds(options.DecisionViewSeconds), token);
			await actor.StepAsync("quit-without-deleting-character", actor.Session.QuitAsync, token);
			actor.Trace.WriteAction(actor.LastStep, "scenario:complete", new Dictionary<string, object?>
			{
				["scenario"] = "NI-05", ["characterId"] = subject.CharacterId, ["freeSlots"] = after.FreeSlots,
				["kinah"] = world.Kinah,
			});
			Console.WriteLine($"LIVE NI-05: retained Priest {identity.CharacterName} has {after.FreeSlots} free cube slots; sell-only housekeeping complete.");
			return 0;
		}
		catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
		catch (Exception exception)
		{
			Console.Error.WriteLine($"NI-05 failed: {exception}");
			return 1;
		}
	}

	private static BotPosition ReadIshalgenVendorAnchor(string root)
	{
		XElement spawns = XDocument.Load(Path.Combine(root,
			"game-server/data/static_data/spawns/Npcs/220010000_Ishalgen.xml")).Root!;
		XElement spot = spawns.Descendants("spawn").Single(spawn => (int)spawn.Attribute("npc_id")! == IshalgenVendor)
			.Element("spot") ?? throw new InvalidDataException("Shipped vendor spawn has no approach anchor.");
		return new((float)spot.Attribute("x")!, (float)spot.Attribute("y")!, (float)spot.Attribute("z")!,
			checked((byte)(int)spot.Attribute("h")!));
	}

	private static void PublishInventoryDecision(LiveBotOptions options, L0Actor actor,
		NaturalInventoryPlan plan, string action, int sequence)
	{
		actor.Trace.WriteAction(actor.LastStep, "natural:inventory-decision", new Dictionary<string, object?>
		{
			["action"] = action, ["capacity"] = plan.Capacity, ["occupied"] = plan.Occupied,
			["freeSlots"] = plan.FreeSlots, ["items"] = plan.Decisions,
		});
		options.Dashboard.PublishDecision(actor.Bot, new NaturalDecision(sequence, action, null,
			plan.CubePressure ? "blocked" : "planned", $"Cube {plan.Occupied}/{plan.Capacity}; " +
			$"{plan.Equips.Count} equip candidates, {plan.Sales.Count} sale candidates; no buying.",
			plan.Decisions.Select(item => new NaturalDecisionCheck($"item-{item.ItemId}-{item.ObjectId}", item.Action,
				item.Reason)).ToArray(), []));
	}
}
