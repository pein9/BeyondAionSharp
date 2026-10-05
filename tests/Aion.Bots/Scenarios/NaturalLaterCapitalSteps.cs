using Aion.Bots.World;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Model.Templates.Items;

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
	public static readonly NaturalAltgardStep[] HeritageCity =
	[
		NaturalCapitalSteps.Progress(2917, 1, 204108, "SETPRO2", 1693, 10, 2),
		NaturalCapitalSteps.Finish(2917, 2, 204241),
	];
	public static readonly NaturalAltgardStep[] RobePreparation =
	[
		NaturalCapitalSteps.Offer(2916, 204141),
		NaturalCapitalSteps.Progress(2916, 0, 204152, "SETPRO1", 1352, 0, 1),
		NaturalCapitalSteps.Progress(2916, 1, 204150, "SETPRO2", 1693, 0, 2),
		NaturalCapitalSteps.Progress(2916, 2, 204151, "SETPRO3", 2034, 0, 3),
	];
	public static readonly NaturalAltgardStep[] Juice =
	[
		NaturalCapitalSteps.Offer(2954, 204191, item: 182207040),
		NaturalCapitalSteps.Progress(2954, 0, 204221, "SETPRO1", 1352, 0, 1),
		NaturalCapitalSteps.Finish(2954, 1, 204191),
	];
	public static readonly NaturalAltgardStep MaternalReturn = NaturalCapitalSteps.Finish(2918, 0, 203574) with { MapId = 220030000 };
	public static readonly NaturalAltgardStep RobeBerth = NaturalCapitalSteps.Progress(2916, 3, 798033,
		"SETPRO4", 2375, 0, 4) with { MapId = 220030000 };
	public static readonly NaturalAltgardStep[] BookFinish =
	[
		NaturalCapitalSteps.Progress(2919, 4, 204224, "CHECK_USER_HAS_QUEST_ITEM", 2716, 2802, 6),
		NaturalCapitalSteps.Progress(2919, 6, 700212, "SETPRO7", 3057, 0, 7) with
			{ Actions = ["USE_OBJECT", "SETPRO7"], ReceivesItemId = 182207013 },
		NaturalCapitalSteps.Finish(2919, 7, 204206, page: 3398) with
			{ Actions = ["USE_OBJECT", "SELECT_QUEST_REWARD", "SELECTED_QUEST_REWARD1"] },
	];
	public static readonly NaturalAltgardStep[] FamilyLetter =
	[
		NaturalCapitalSteps.Offer(2959, 204211), NaturalCapitalSteps.Finish(2959, 0, 204164),
	];
	public static readonly NaturalAltgardStep[] Dye =
	[
		NaturalCapitalSteps.Offer(2984, 204138, item: 182207064), NaturalCapitalSteps.Finish(2984, 0, 204121),
	];
	public static readonly NaturalAltgardStep LibraryOffer = NaturalCapitalSteps.Offer(2938, 204267, page: 4762);
	public static IEnumerable<NaturalAltgardStep> Leg7City => BookFinish.Concat(FamilyLetter).Concat(Dye).Append(LibraryOffer);
	public static readonly NaturalAltgardStep ElementaryOffer = NaturalCapitalSteps.Offer(2920, 204141) with
	{
		Actions = ["QUEST_SELECT", "SELECT_NONE_1", "SELECT_NONE_1_1", "ASK_QUEST_ACCEPT", "QUEST_ACCEPT_1"],
		Pages = [4762, 4763, 4764, 4, 1003],
	};
	public static readonly NaturalAltgardStep ElementaryAnswer = NaturalCapitalSteps.Progress(2920, 0, 204141,
		"SETPRO1", 1011, 1352, 0) with
	{
		Actions = ["QUEST_SELECT", "SETPRO1", "SELECT2_1", "SETPRO11", "SELECTED_QUEST_NOREWARD"],
		Pages = [1011, 1352, 1353, 5],
		NextVar = null,
	};
	public static readonly NaturalAltgardStep RobeHeart = NaturalCapitalSteps.Progress(2916, 4, 203673,
		"SETPRO5", 2716, 0, 5) with { MapId = 220030000 };
	public static IEnumerable<NaturalAltgardStep> Leg9City => [ElementaryOffer, ElementaryAnswer];
	public static readonly NaturalAltgardStep RobeFinish = NaturalCapitalSteps.Finish(2916, 6, 204141, page: 3057) with
	{
		Actions = ["QUEST_SELECT", "CHECK_USER_HAS_QUEST_ITEM", "SELECTED_QUEST_REWARD1"],
	};
	public static readonly NaturalAltgardStep LibraryPermission = NaturalCapitalSteps.Progress(2938, 0, 203557,
		"SET_SUCCEED", 1011, 0, 0) with
	{
		MapId = 220030000, Actions = ["QUEST_SELECT", "SELECT1_1", "SET_SUCCEED"],
		Pages = [1011, 1012, 0], ReceivesItemId = 182207026,
	};
	public static readonly NaturalAltgardStep LibraryFinish = NaturalCapitalSteps.Finish(2938, 0, 204267) with
	{
		Status = "REWARD", Actions = ["USE_OBJECT", "SELECT_QUEST_REWARD", "SELECTED_QUEST_NOREWARD"], Pages = [10002, 5],
	};
	public static IEnumerable<NaturalAltgardStep> FinalCity => [RobeFinish, LibraryFinish];

	public static async Task CompleteLeg10RobeAsync(INaturalJourneySession session, Func<NaturalAltgardStep, Task> talk)
	{
		BotWorldModel world = session.Api.World;
		if (world.CompletedQuestIds.Contains(2916)) return;
		if (world.MapId != 120010000 || NaturalAltgardQuestSteps.State(world, 2916) is not (3, 6) and not (4, 6) ||
			NaturalAltgardQuestSteps.State(world, 2916) is (3, _) && Owned(world, 182207007) != 1)
			throw new InvalidDataException("Deyla needs the carried clothing or its already-observed reward transition.");
		await talk(NaturalAltgardQuestSteps.State(world, 2916) is (4, _) ? NaturalCapitalSteps.ResumeReward(RobeFinish) : RobeFinish);
		if (!world.CompletedQuestIds.Contains(2916) || Owned(world, 182207007) != 0 || Owned(world, 120000833) != 1)
			throw new InvalidDataException("The robe hand-in must consume its actual clothing and award Deyla's hood once.");
	}

	public static async Task ObtainLibraryPermissionAsync(INaturalJourneySession session, Func<NaturalAltgardStep, Task> talk)
	{
		BotWorldModel world = session.Api.World;
		if (world.CompletedQuestIds.Contains(2938)) return;
		if (world.MapId != 220030000 || !world.CompletedQuestIds.Contains(24016))
			throw new InvalidDataException("Suthran's permission requires the completed final Altgard campaign.");
		await RunMatchingStepsAsync(session, [LibraryPermission], talk);
		if (NaturalAltgardQuestSteps.State(world, 2938) is not (4, 0) || Owned(world, 182207026) != 1)
			throw new InvalidDataException("Suthran must leave Q2938 at REWARD/0 with its actual permission item.");
		BotInventoryItem permission = world.Inventory.Values.Single(i => i.ItemId == 182207026);
		session.TraceDiagnostic("later-capital-library-permission", new Dictionary<string, object?>
		{ ["objectId"] = permission.ObjectId, ["item"] = permission.ItemId, ["count"] = permission.Count, ["campaign"] = 24016 });
	}

	public static async Task CompleteLeg11LibraryAsync(INaturalJourneySession session, Func<NaturalAltgardStep, Task> talk)
	{
		BotWorldModel world = session.Api.World;
		if (world.CompletedQuestIds.Contains(2938)) return; // Reopening completed Q2938 is a library teleport.
		if (world.MapId != 120010000 || !world.CompletedQuestIds.Contains(24016) ||
			NaturalAltgardQuestSteps.State(world, 2938) is not (4, 0) || Owned(world, 182207026) != 1)
			throw new InvalidDataException("Oubliette needs the permission carried from Suthran.");
		int start = session.PacketHistory.Count;
		int item = world.Inventory.Values.Single(i => i.ItemId == 182207026).ObjectId;
		await talk(LibraryFinish);
		if (!world.CompletedQuestIds.Contains(2938) || Owned(world, 182207026) != 0 ||
			!session.PacketHistory.Skip(start).Any(p => p.PacketType == typeof(SM_DIALOG_WINDOW) &&
				p.Get<int>("questId") == 2938 && p.Get<ushort>("dialogPageId") == 10002))
			throw new InvalidDataException("The ordinary library finish must present its success page and consume the permission.");
		session.TraceDiagnostic("later-capital-library-finished", new Dictionary<string, object?> { ["consumedObjectId"] = item, ["quest"] = 2938 });
	}

	public static async Task CompleteLeg9CityAsync(INaturalJourneySession session, Func<NaturalAltgardStep, Task> talk,
		Func<int, Task<int>> approach, CancellationToken token)
	{
		BotWorldModel world = session.Api.World;
		if (world.CompletedQuestIds.Contains(2920)) return;
		if (world.MapId != 120010000 || world.Level < 16 || !world.CompletedQuestIds.IsSupersetOf(new[] { 2268, 2269 }))
			throw new InvalidDataException("Deyla's answers require the capital and both completed Observatory quests.");
		long kinah = world.Kinah;
		int start = session.PacketHistory.Count;
		await RunMatchingStepsAsync(session, Leg9City, talk);
		if (NaturalAltgardQuestSteps.State(world, 2920) is (4, _))
		{
			// The journal does not carry the reward group. Observe the actual page when resuming a paid-state boundary.
			int npc = await approach(204141);
			await NaturalDialogProtocol.OpenAsync(session, npc, token);
			await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token, p => p.Get<int>("targetObjectId") == npc &&
				p.Get<int>("questId") == 2920 && p.Get<ushort>("dialogPageId") is 5 or 6);
			await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(npc,
				checked((ushort)NaturalAscensionContract.DialogActionId("SELECTED_QUEST_NOREWARD")), questId: 2920), token);
			await session.SynchronizeAsync(token);
			await session.SendPacketAsync(session.Api.CloseDialog(npc), token);
		}
		if (!world.CompletedQuestIds.Contains(2920)) throw new InvalidDataException("Deyla's actual answer and reward did not finish Q2920.");
		int page = session.PacketHistory.Skip(start).Last(p => p.PacketType == typeof(SM_DIALOG_WINDOW) &&
			p.Get<int>("questId") == 2920 && p.Get<ushort>("dialogPageId") is 5 or 6).Get<ushort>("dialogPageId");
		session.TraceDiagnostic("later-capital-elementary-reward", new Dictionary<string, object?>
		{ ["quest"] = 2920, ["page"] = page, ["group"] = page - 5, ["kinah"] = world.Kinah - kinah });
	}

	public static bool Leg7CityNeeded(BotWorldModel world) => !world.CompletedQuestIds.Contains(2919) ||
		world.Level >= 20 && (!world.CompletedQuestIds.Contains(2959) || !world.CompletedQuestIds.Contains(2984) || !Prepared(world, 2938, 0)) ||
		world.Level >= 19 && !world.CompletedQuestIds.Contains(2954);

	public static async Task CompleteLeg7CityAsync(INaturalJourneySession session, Func<NaturalAltgardStep, Task> talk)
	{
		BotWorldModel world = session.Api.World;
		if (world.MapId != 120010000) throw new InvalidDataException("Leg 7's capital batch requires Pandaemonium.");
		if (!world.CompletedQuestIds.Contains(2919))
		{
			if (NaturalAltgardQuestSteps.State(world, 2919) is (3, 4) &&
				(Owned(world, 182207010) < 3 || Owned(world, 182207011) < 2 || Owned(world, 182207012) < 1))
				throw new InvalidDataException("Neusa needs all naturally collected book materials.");
			await RunMatchingStepsAsync(session, BookFinish, talk);
			if (!world.CompletedQuestIds.Contains(2919) || new[] { 182207010, 182207011, 182207012, 182207013 }.Any(i => Owned(world, i) != 0))
				throw new InvalidDataException("The book finish must consume the materials and the supplied final book item.");
		}
		if (world.Level >= 20)
		{
			await RunMatchingStepsAsync(session, FamilyLetter.Concat(Dye).Append(LibraryOffer), talk);
			if (!world.CompletedQuestIds.IsSupersetOf(new[] { 2959, 2984 }) || !Prepared(world, 2938, 0) ||
				Owned(world, 182207064) != 0 || Owned(world, 169100000) != 8 || Owned(world, 169200002) != 8)
				throw new InvalidDataException("The city errands must finish once, consume the supplied ingredient and retain both unapplied dye rewards.");
		}
		await CompleteJuiceOnceAsync(session, talk);
		session.TraceDiagnostic("later-capital-leg7-city", new Dictionary<string, object?>
		{ ["completed"] = new[] { 2919, 2959, 2984, 2954 }.Where(world.CompletedQuestIds.Contains).ToArray(),
			["library"] = NaturalAltgardQuestSteps.State(world, 2938), ["bleach"] = Owned(world, 169100000), ["dye"] = Owned(world, 169200002) });
	}

	public static bool Leg5CityNeeded(BotWorldModel world) => !world.CompletedQuestIds.IsSupersetOf(new[] { 2917, 2918 }) ||
		world.Level >= 15 && !Prepared(world, 2916, 3) || world.Level >= 19 && !world.CompletedQuestIds.Contains(2954) ||
		world.Level >= 13 && !world.CompletedQuestIds.Contains(2919) &&
			(!Prepared(world, 2919, 4) || Owned(world, 182207011) < 2);

	public static async Task PrepareLeg5CityAsync(INaturalJourneySession session, Func<NaturalAltgardStep, Task> talk,
		ItemTemplate boxTemplate, CancellationToken token)
	{
		BotWorldModel world = session.Api.World;
		if (world.MapId != 120010000) throw new InvalidDataException("Leg 5 preparation requires Pandaemonium.");
		if (!world.CompletedQuestIds.Contains(2917) && NaturalAltgardQuestSteps.State(world, 2917) is not (3, 1 or 2) and not (4, 3))
			throw new InvalidDataException("The city heritage finish needs the carried Arekedil/Chauminerk pickup.");
		await RunMatchingStepsAsync(session, HeritageCity, talk);
		if (!world.CompletedQuestIds.Contains(2917) || Owned(world, 182207008) != 0)
			throw new InvalidDataException("Annemari did not complete the proper heritage delivery.");
		await StartMaternalFromBoxAsync(session, boxTemplate, token);
		if (world.Level >= 15)
		{
			await RunMatchingStepsAsync(session, RobePreparation, talk);
			if (!Prepared(world, 2916, 3)) throw new InvalidDataException("The robe preparation did not reach Annju's START/3.");
		}
		await CompleteJuiceOnceAsync(session, talk);
	}

	public static async Task CompleteJuiceOnceAsync(INaturalJourneySession session, Func<NaturalAltgardStep, Task> talk)
	{
		BotWorldModel world = session.Api.World;
		if (world.Level < 19 || world.CompletedQuestIds.Contains(2954)) return;
		await RunMatchingStepsAsync(session, Juice, talk);
		if (!world.CompletedQuestIds.Contains(2954) || Owned(world, 182207040) != 0)
			throw new InvalidDataException("The single juice delivery did not complete and consume its supplied item.");
	}

	public static async Task CompleteMaternalReturnAsync(INaturalJourneySession session, Func<NaturalAltgardStep, Task> talk)
	{
		if (session.Api.World.CompletedQuestIds.Contains(2918)) return;
		if (session.Api.World.MapId != 220030000 || NaturalAltgardQuestSteps.State(session.Api.World, 2918) is not (4, _) && Owned(session.Api.World, 182207009) != 1)
			throw new InvalidDataException("The Altgard maternal hand-in needs its actual awarded box.");
		await RunMatchingStepsAsync(session, [MaternalReturn], talk);
		if (!session.Api.World.CompletedQuestIds.Contains(2918) || Owned(session.Api.World, 182207009) != 0)
			throw new InvalidDataException("Arekedil did not complete Deep Maternal Love and consume the box.");
	}

	public static async Task RunMatchingStepsAsync(INaturalJourneySession session, IEnumerable<NaturalAltgardStep> steps,
		Func<NaturalAltgardStep, Task> talk)
	{
		foreach (NaturalAltgardStep step in steps)
		{
			if (session.Api.World.CompletedQuestIds.Contains(step.QuestId)) continue;
			var state = NaturalAltgardQuestSteps.State(session.Api.World, step.QuestId);
			if (step.ExpectedStatus == "OFFER" ? state is null or (1 or 2, _) : state is (3, int current) && current == step.Var)
				await talk(step);
			else if (state is (4, _) && step.Actions.Contains("SELECT_QUEST_REWARD"))
				await talk(NaturalCapitalSteps.ResumeReward(step));
		}
	}

	private static bool Prepared(BotWorldModel world, int quest, int step) => world.CompletedQuestIds.Contains(quest) ||
		NaturalAltgardQuestSteps.State(world, quest) is (4, _) || NaturalAltgardQuestSteps.State(world, quest) is (3, int current) && current >= step;
	private static long Owned(BotWorldModel world, int item) => world.Inventory.Values.Where(i => i.ItemId == item).Sum(i => i.Count);

	private static async Task StartMaternalFromBoxAsync(INaturalJourneySession session, ItemTemplate template, CancellationToken token)
	{
		BotWorldModel world = session.Api.World;
		if (world.CompletedQuestIds.Contains(2918)) return;
		if (NaturalAltgardQuestSteps.State(world, 2918) is (4, _)) return; // The reward transition already consumed the box.
		BotInventoryItem box = world.Inventory.Values.Single(i => i.ItemId == 182207009 && i.Count == 1);
		if (NaturalAltgardQuestSteps.State(world, 2918) is null or (1 or 2, _))
		{
			await session.SendPacketAsync(session.Api.UseItem(box.ObjectId, template), token);
			var page = await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token,
				packet => packet.Get<int>("questId") == 2918 && packet.Get<ushort>("dialogPageId") == 4);
			await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(page.Get<int>("targetObjectId"),
				checked((ushort)NaturalAscensionContract.DialogActionId("QUEST_ACCEPT_1")), questId: 2918), token);
			await session.SynchronizeAsync(token);
		}
		if (NaturalAltgardQuestSteps.State(world, 2918) is not (3, 0) and not (4, _) || Owned(world, 182207009) != 1)
			throw new InvalidDataException("The awarded jewelry box did not start Q2918 and remain for Arekedil.");
		session.TraceDiagnostic("later-capital-maternal-box", new Dictionary<string, object?>
		{ ["quest"] = 2918, ["itemObjectId"] = box.ObjectId, ["item"] = box.ItemId });
	}

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
		if (step.ReceivesItemId is int received)
		{
			BotInventoryItem supplied = session.Api.World.Inventory.Values.Single(i => i.ItemId == received && i.Count == 1);
			session.TraceDiagnostic("later-capital-book-supplied-item", new Dictionary<string, object?>
			{ ["objectId"] = supplied.ObjectId, ["item"] = supplied.ItemId, ["count"] = supplied.Count });
		}
		if (!session.PacketHistory.Skip(start).Any(packet => packet.PacketType == typeof(SM_USE_OBJECT) &&
			packet.Get<int>("targetObjectId") == book && packet.Get<byte>("actionType") == 2 && packet.Get<int>("durationMs") > 0))
			throw new InvalidDataException("The library book use bar did not complete.");
		await session.SendPacketAsync(session.Api.CloseDialog(book), token);
	}
}
