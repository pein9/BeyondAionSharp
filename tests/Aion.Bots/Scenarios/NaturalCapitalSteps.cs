using Aion.Bots.World;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.Bots.Scenarios;

public static class NaturalCapitalSteps
{
	// Positions are resolved from the shipped spawn graph by the caller; these
	// steps describe only the client dialogs specified by Java and D32 Q2929.
	public static readonly NaturalAltgardStep[] Supply =
	[
		Offer(2953, 204191, item: 182207039),
		Progress(2953, 0, 204071, "SETPRO1", 1352, 0, 1),
		Finish(2953, 1, 204191),
	];
	public static readonly NaturalAltgardStep[] Artisans =
	[
		Offer(2929, 204092, page: 4762),
		Finish(2929, 0, 798317, page: 10002),
	];
	public static readonly NaturalAltgardStep[] Pets =
	[
		Offer(29040, 798385), Finish(29040, 0, 798443),
		Offer(29044, 798443), Finish(29044, 0, 798441),
		Offer(29045, 798441), Finish(29045, 0, 798442),
	];
	public static readonly NaturalAltgardStep[] Blessing =
	[
		Offer(2911, 204079),
		Step("q2911-ribbon-branch", 2911, 0, "START", 204193,
			["QUEST_SELECT", "SETPRO1", "SELECTED_QUEST_REWARD1"], [1352, 5]),
		Offer(2912, 204193),
		Progress(2912, 0, 204089, "SETPRO1", 1352, 10, 1),
		Progress(2912, 1, 204088, "SETPRO3", 2034, 10, 2),
		Progress(2912, 2, 204240, "SETPRO2", 1693, 10, 3),
		Finish(2912, 3, 204236),
		Offer(2914, 204147),
		Progress(2914, 0, 204236, "SETPRO1", 1352, 10, 1),
		Finish(2914, 1, 204147, rewardPage: 10),
	];
	public static readonly NaturalAltgardStep[] Convent =
	[
		Offer(29004, 204071),
		Progress(29004, 0, 204075, "SETPRO1", 1352, 0, 1),
		Progress(29004, 1, 204053, "SETPRO2", 1693, 0, 2),
		Step("q29004-angulof-reward", 29004, 2, "REWARD", 798700,
			["USE_OBJECT", "SELECT_QUEST_REWARD", "SELECTED_QUEST_REWARD1"], [2375, 5]) with { MapId = 120020000 },
	];
	public static NaturalAltgardStep BookOffer => Offer(29048, 798304, item: 182212217);
	public static NaturalAltgardStep BookReward => Step("q29048-book-reward", 29048, 1, "REWARD", 798304,
		["USE_OBJECT", "SELECT_QUEST_REWARD", "SELECTED_QUEST_REWARD1"], [2375, 5]);

	public static NaturalAltgardStep Offer(int quest, int npc, int page = 1011, int? item = null) =>
		Step($"q{quest}-offer", quest, null, "OFFER", npc, ["QUEST_SELECT", "QUEST_ACCEPT"], [page, 1003], item);
	public static NaturalAltgardStep Progress(int quest, int var, int npc, string action, int page, int closePage, int next) =>
		Step($"q{quest}-v{var}-{npc}", quest, var, "START", npc, ["QUEST_SELECT", action], [page, closePage], next: next);
	public static NaturalAltgardStep Finish(int quest, int var, int npc, int page = 2375, int rewardPage = 5) =>
		Step($"q{quest}-finish", quest, var, "START", npc,
			["QUEST_SELECT", "SELECT_QUEST_REWARD", "SELECTED_QUEST_REWARD1"], [page, rewardPage]);
	public static NaturalAltgardStep ResumeReward(NaturalAltgardStep finish, int page = 5) => finish with
	{
		Key = finish.Key + "-resume", Status = "REWARD", Var = null,
		Actions = ["USE_OBJECT", "SELECTED_QUEST_REWARD1"], Pages = [page],
	};
	private static NaturalAltgardStep Step(string key, int quest, int? var, string status, int npc,
		string[] actions, int[] pages, int? item = null, int? next = null) =>
		new(key, quest, var, status, npc, [], 5, actions, pages, null, item, "capital", false, null,
			MapId: 120010000, NextVar: next);

	/// <summary>Read the supplied manual through CM_USE_ITEM; the handler sets REWARD/1.</summary>
	public static async Task ReadBookAsync(INaturalJourneySession session, ItemTemplate template, CancellationToken token)
	{
		if (NaturalAltgardQuestSteps.State(session.Api.World, 29048) is not (3, 0))
			throw new InvalidDataException("Seriphim's manual requires Q29048 START/0.");
		BotInventoryItem book = session.Api.World.Inventory.Values.Single(item => item.ItemId == 182212217);
		await session.SendPacketAsync(session.Api.UseItem(book.ObjectId, template), token);
		await session.AdvanceAsync(TimeSpan.FromMilliseconds(template.GetCastingDelay() + 100), token);
		await session.SynchronizeAsync(token);
		if (NaturalAltgardQuestSteps.State(session.Api.World, 29048) is not (4, 1))
			throw new InvalidDataException("Reading the supplied manual did not produce Q29048 REWARD/1.");
		session.TraceDiagnostic("capital-book-read", new Dictionary<string, object?>
		{ ["item"] = book.ItemId, ["quest"] = 29048, ["status"] = 4, ["var"] = 1 });
	}

	/// <summary>The shipped statue dialog, including discarding the old map view before departure.</summary>
	public static async Task PortalAsync(INaturalJourneySession session, NaturalCapitalPortal portal, int npc,
		CancellationToken token)
	{
		if (session.Api.World.MapId != portal.MapId || session.Api.World.Objects[npc].TemplateId != portal.NpcId)
			throw new InvalidDataException("Capital statue is on the wrong map or has the wrong identity.");
		await NaturalDialogProtocol.OpenAsync(session, npc, token);
		await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token, p => p.Get<int>("targetObjectId") == npc);
		session.Api.World.BeginWorldReload();
		await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(npc, portal.Action), token);
		await session.WaitForPacketAsync(typeof(SM_PLAYER_SPAWN), token);
		await session.WaitForPacketAsync(typeof(SM_PLAYER_INFO), token, p => p.Get<int>("objectId") == session.CharacterId);
		session.AcceptTeleportPosition();
		await session.SynchronizeAsync(token);
		if (session.Api.World.MapId != portal.DestinationMapId)
			throw new InvalidDataException($"Statue {portal.NpcId} did not enter map {portal.DestinationMapId}.");
		session.TraceDiagnostic("capital-statue", new Dictionary<string, object?>
		{ ["npc"] = portal.NpcId, ["action"] = portal.Action, ["map"] = session.Api.World.MapId });
	}
}
