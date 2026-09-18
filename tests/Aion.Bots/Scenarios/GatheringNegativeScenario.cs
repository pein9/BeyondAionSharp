using Aion.Bots.World;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.Bots.Scenarios;

public interface IGatheringNegativeDriver : IGatheringScenarioDriver
{
	Task MoveAsync(GatheringTarget target, float distance, bool interrupt, CancellationToken cancellationToken);
	Task FillCubeAsync(CancellationToken cancellationToken);
	Task VerifyOccupiedAsync(int gathererId, CancellationToken cancellationToken);
}

/// <summary>E2's two ordinary players exercise refusal and interruption paths through client packets.</summary>
public static class GatheringNegativeScenario
{
	public static GatheringTarget Iron { get; } = new(210010000, 400201, 152000201,
		new BotPosition(170.17715f, 1108.7924f, 131.55333f, 0));
	public static GatheringTarget SpareAria { get; } = GatheringTarget.YoungAria with
	{
		Position = new BotPosition(1189.89f, 1060.15f, 136.98375f, 0),
	};

	public static async Task RunAsync(IGatheringNegativeDriver first, IGatheringNegativeDriver second,
		CancellationToken cancellationToken = default)
	{
		int node = 0;
		await first.StepAsync("low-gathering-skill", async token =>
		{
			node = await first.PrepareAsync(Iron, token);
			await RefusalAsync(first, node, "STR_GATHER_OUT_OF_SKILL_POINT", token);
		}, cancellationToken);
		await first.StepAsync("gather-too-far", async token =>
		{
			node = await first.PrepareAsync(GatheringTarget.YoungAria, token);
			await first.MoveAsync(GatheringTarget.YoungAria, 5, false, token);
			await RefusalAsync(first, node, "STR_GATHER_TOO_FAR_FROM_GATHER_SOURCE", token);
			await first.MoveAsync(GatheringTarget.YoungAria, 1, false, token);
		}, cancellationToken);
		await second.StepAsync("approach-shared-node", async token =>
		{
			int other = await second.PrepareAsync(GatheringTarget.YoungAria, token);
			Require(other == node, "Both subjects must target the same physical node.");
			await second.SynchronizeAsync(token);
		}, cancellationToken);
		await first.SynchronizeAsync(cancellationToken);
		var firstInventory = Totals(first.Api.World);
		var secondInventory = Totals(second.Api.World);
		await first.StepAsync("cancel-gather", async token =>
		{
			await StartAsync(first, node, 0, token);
			await first.VerifyOccupiedAsync(first.Api.World.SelfObjectId!.Value, token);
			await CancelAsync(first, node, false, token);
		}, cancellationToken);
		await first.StepAsync("move-aborts-gather", async token =>
		{
			await StartAsync(first, node, 0, token);
			await first.MoveAsync(GatheringTarget.YoungAria, 1.5f, true, token);
			var response = await first.WaitAsync(typeof(SM_GATHER_UPDATE), packet => packet.Get<byte>("action") >= 5, token);
			Require(response.Get<byte>("action") == 5, "Movement did not abort gathering.");
			await first.VerifyReleasedAsync(node, false, token);
		}, cancellationToken);
		await first.StepAsync("occupied-node-refuses-second-bot", async token =>
		{
			await StartAsync(first, node, 0, token);
			await StartAsync(second, node, 8, token);
			await first.VerifyOccupiedAsync(first.Api.World.SelfObjectId!.Value, token);
			await second.VerifyOccupiedAsync(first.Api.World.SelfObjectId!.Value, token);
			// Java counts canceled attempts as uses. The third cancellation must deplete the node.
			await CancelAsync(first, node, true, token);
		}, cancellationToken);
		await first.SynchronizeAsync(cancellationToken);
		await second.SynchronizeAsync(cancellationToken);
		Require(firstInventory.SequenceEqual(Totals(first.Api.World)) && secondInventory.SequenceEqual(Totals(second.Api.World)),
			"Refused/canceled gathering changed items or kinah.");
		await second.StepAsync("full-cube-refuses-gather", async token =>
		{
			int spare = await second.PrepareAsync(SpareAria, token);
			await second.FillCubeAsync(token);
			await RefusalAsync(second, spare, "STR_GATHER_INVENTORY_IS_FULL", token);
		}, cancellationToken);
	}

	private static async Task RefusalAsync(IGatheringNegativeDriver actor, int node, string message, CancellationToken token)
	{
		await actor.SynchronizeAsync(token);
		var inventory = Totals(actor.Api.World);
		foreach (var packet in actor.Api.Gather(node))
			await actor.SendAsync(packet, token);
		await actor.WaitAsync(typeof(SM_SYSTEM_MESSAGE), packet => packet.Get<string>("name") == message, token);
		await actor.SynchronizeAsync(token);
		Require(inventory.SequenceEqual(Totals(actor.Api.World)), $"{message} changed inventory.");
		await actor.VerifyReleasedAsync(node, false, token);
	}

	private static async Task StartAsync(IGatheringNegativeDriver actor, int node, byte action, CancellationToken token)
	{
		foreach (var packet in actor.Api.Gather(node))
			await actor.SendAsync(packet, token);
		var response = await actor.WaitAsync(typeof(SM_GATHER_UPDATE), _ => true, token);
		Require(response.Get<byte>("action") == action, $"Expected gather action {action}, received {response.Get<byte>("action")}.");
	}

	private static async Task CancelAsync(IGatheringNegativeDriver actor, int node, bool depleted, CancellationToken token)
	{
		foreach (var packet in actor.Api.Gather(node, start: false))
			await actor.SendAsync(packet, token);
		var response = await actor.WaitAsync(typeof(SM_GATHER_UPDATE), packet => packet.Get<byte>("action") >= 5, token);
		Require(response.Get<byte>("action") == 5, "Canceled gathering did not abort.");
		if (depleted)
			await actor.WaitAsync(typeof(SM_DELETE), packet => packet.Get<int>("objectId") == node, token);
		await actor.VerifyReleasedAsync(node, depleted, token);
	}

	private static KeyValuePair<int, long>[] Totals(BotWorldModel world) => world.Inventory.Values
		.GroupBy(item => item.ItemId).Select(group => new KeyValuePair<int, long>(group.Key, group.Sum(item => item.Count)))
		.OrderBy(pair => pair.Key).ToArray();
	private static void Require(bool condition, string message)
	{
		if (!condition)
			throw new InvalidDataException(message);
	}
}
