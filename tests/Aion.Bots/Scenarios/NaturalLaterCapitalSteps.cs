using Aion.Bots.World;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.Bots.Scenarios;

/// <summary>PC-08's normal client actions. Route approaches belong to the caller.</summary>
public static class NaturalLaterCapitalSteps
{
	public static readonly NaturalAltgardStep[] HeritagePickup =
	[
		NaturalCapitalSteps.Offer(2917, 203574, item: 182207008) with { MapId = 220030000 },
		NaturalCapitalSteps.Progress(2917, 0, 798029, "SETPRO1", 1352, 10, 1) with { MapId = 220030000 },
	];
	public static readonly NaturalAltgardStep[] BookPreparation =
	[
		NaturalCapitalSteps.Offer(2919, 204206),
		NaturalCapitalSteps.Progress(2919, 0, 204215, "SETPRO2", 1352, 0, 1),
		NaturalCapitalSteps.Progress(2919, 1, 204192, "SETPRO3", 1693, 0, 2),
		NaturalCapitalSteps.Progress(2919, 2, 700212, "SETPRO4", 2034, 0, 3) with
			{ Actions = ["USE_OBJECT", "SETPRO4"] },
		NaturalCapitalSteps.Progress(2919, 3, 204206, "SETPRO5", 2375, 0, 4),
	];

	/// <summary>Returns false below the observed gate, so the Leg 5 city visit can prepare it later.</summary>
	public static async Task<bool> PrepareBookAsync(INaturalJourneySession session, Func<NaturalAltgardStep, Task> talk)
	{
		BotWorldModel world = session.Api.World;
		if (world.CompletedQuestIds.Contains(2919)) return true;
		if (world.Level < 13) return false;
		if (world.MapId != 120010000) throw new InvalidDataException("Book preparation requires Pandaemonium.");
		foreach (NaturalAltgardStep step in BookPreparation)
		{
			var state = NaturalAltgardQuestSteps.State(world, 2919);
			if (step.ExpectedStatus == "OFFER" ? state is null or (1 or 2, _) : state is (3, int current) && current == step.Var)
				await talk(step);
		}
		if (NaturalAltgardQuestSteps.State(world, 2919) is not (3, >= 4) and not (4, _))
			throw new InvalidDataException("Book preparation did not reach the collecting step.");
		return true;
	}

	public static async Task PickUpHeritageAsync(INaturalJourneySession session,
		Func<NaturalAltgardStep, Task> talk)
	{
		BotWorldModel world = session.Api.World;
		if (world.CompletedQuestIds.Contains(2917)) return;
		if (world.Level < 10 || world.MapId != 220030000)
			throw new InvalidDataException("Arekedil's Heritage pickup requires level 10 in Altgard.");
		if (NaturalAltgardQuestSteps.State(world, 2917) is null or (1 or 2, _))
			await talk(HeritagePickup[0]);
		if (NaturalAltgardQuestSteps.State(world, 2917) is (3, 0))
			await talk(HeritagePickup[1]);
		if (NaturalAltgardQuestSteps.State(world, 2917) is not (3, 1) ||
			world.Inventory.Values.Where(item => item.ItemId == 182207008).Sum(item => item.Count) != 1)
			throw new InvalidDataException("The heritage pickup must retain START/1 and its supplied item for Lanse.");
		session.TraceDiagnostic("later-capital-heritage-pickup", new Dictionary<string, object?>
		{ ["quest"] = 2917, ["status"] = 3, ["var"] = 1, ["item"] = 182207008 });
	}

	/// <summary>QuestItemNpcAI opens the book's page after its normal use bar; it is not an immediate NPC dialog.</summary>
	public static async Task ReadQuestBookAsync(INaturalJourneySession session, NaturalAltgardStep step, int book,
		CancellationToken token)
	{
		if (step.NpcId != 700212 || NaturalAltgardQuestSteps.State(session.Api.World, 2919) is not (3, int current) || current != step.Var)
			throw new InvalidDataException("The library book is not at the required accepted quest step.");
		int start = session.PacketHistory.Count;
		if (!await NaturalAltgardQuestSteps.UseObjectAsync(session, book, null, token))
			throw new InvalidDataException("The library book interaction was interrupted.");
		if (!session.PacketHistory.Skip(start).Any(packet => packet.PacketType == typeof(SM_DIALOG_WINDOW) &&
			packet.Get<int>("targetObjectId") == book && packet.Get<ushort>("dialogPageId") == step.Pages[0]))
			throw new InvalidDataException("The library book did not open its expected quest page.");
		await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(book,
			checked((ushort)NaturalAscensionContract.DialogActionId(step.Actions[1])), questId: step.QuestId), token);
		await session.SynchronizeAsync(token);
		if (NaturalAltgardQuestSteps.State(session.Api.World, 2919) is not (3, int next) || next != step.NextVar)
			throw new InvalidDataException("Reading the library book did not advance Q2919.");
		if (!session.PacketHistory.Skip(start).Any(packet => packet.PacketType == typeof(SM_USE_OBJECT) &&
			packet.Get<int>("targetObjectId") == book && packet.Get<byte>("actionType") == 2 && packet.Get<int>("durationMs") > 0))
			throw new InvalidDataException("The library book use bar did not complete.");
		await session.SendPacketAsync(session.Api.CloseDialog(book), token);
	}
}
