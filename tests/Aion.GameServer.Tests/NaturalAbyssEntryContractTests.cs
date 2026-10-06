using System.Globalization;
using System.Xml.Linq;
using Aion.Bots.Scenarios;
using Aion.Bots.World;
using Aion.GameServer.Model;
using Aion.GameServer.TestKit;

namespace Aion.GameServer.Tests;

/// <summary>AX-03: the Morheim arrival and Abyss-entry contract, pinned against the shipped data it was read from.</summary>
public sealed class NaturalAbyssEntryContractTests
{
	private static readonly NaturalAltgardContract Leg = NaturalAltgardContract.LoadLeg(NaturalAbyssEntry.Leg);
	private static readonly NaturalAbyssEntry Scope = Leg.AbyssEntry!;
	private static string Data(params string[] path) => Path.Combine([RealStaticData.RepoRoot(), "game-server", "data", "static_data", .. path]);
	private static float F(XElement node, string name) => float.Parse((string)node.Attribute(name)!, CultureInfo.InvariantCulture);
	private static float[] Spot(string file, int npcId)
	{
		XElement spot = XDocument.Load(Data("spawns", file)).Descendants("spawn").First(n => (int?)n.Attribute("npc_id") == npcId).Element("spot")!;
		return [F(spot, "x"), F(spot, "y"), F(spot, "z")];
	}

	[Fact]
	public void ContractStartsAtTheContinuousEndpointAndKeepsItsWholeJournal()
	{
		NaturalAltgardContract prior = NaturalAltgardContract.LoadLeg("l12");
		Assert.All(prior.Start.CompletedQuestIds.Concat(prior.Order), id => Assert.Contains(id, Leg.Start.CompletedQuestIds));
		Assert.Equal(176, Leg.Start.CompletedQuestIds.Length);
		Assert.Equal(("altgard-rc-complete-s1", 220030000, 25, 700065), (Leg.Start.Snapshot, Leg.Start.MapId, Leg.Start.Level, Leg.Start.BindNpcId));
		Assert.Equal([2945], Leg.Start.StartedQuestIds ?? []);
		Assert.Equal([24020, 2945, 2946, 2947, 2042], Leg.Order);
		Assert.Empty(Leg.Order.Intersect(Leg.Start.CompletedQuestIds));
		Assert.Equal(("morheim-abyss-entry-s1", 220020000, 26, 700231), (Leg.Endpoint.Snapshot, Leg.Endpoint.MapId, Leg.Endpoint.MinimumLevel, Leg.Endpoint.BindNpcId));
		Assert.Equal(Leg.Order.Order(), Leg.Endpoint.CompletedQuestIds.Order());
		// No generic timer or instance trip: Java's arena and course keep their own failure vars and are tried again, not abandoned.
		Assert.Empty(Leg.TimerList);
		Assert.Empty(Leg.InstanceTripList);
		Assert.Equal(["l12", "ax"], NaturalAltgardContract.Legs.Keys.TakeLast(2));
	}

	[Fact]
	public void QuestsPayWhatTheHandlersSelectAndTheFiveTogetherReachLevelTwentySix()
	{
		XElement[] quests = XDocument.Load(Data("quest_data", "quest_data.xml")).Descendants("quest").ToArray();
		foreach (NaturalAltgardQuest quest in Leg.Quests)
		{
			XElement shipped = Assert.Single(quests, n => (int?)n.Attribute("id") == quest.Id);
			// Java reads Q2947's reward group 1 as a zero-based index: the second <rewards>, not the first.
			XElement rewards = shipped.Elements("rewards").ElementAt(quest.Id == 2947 ? 1 : 0);
			Assert.Equal((quest.MinimumLevel, quest.Category, quest.RewardExperience),
				((int)shipped.Attribute("minlevel_permitted")!, (string)shipped.Attribute("category")!, (int)rewards.Attribute("exp")!));
			Assert.True(File.Exists(Path.Combine(RealStaticData.RepoRoot(), "src", "Aion.GameServer", "Handlers", "Quest",
				quest.Id == 24020 ? "morheim" : "abyss_entry", quest.Handler + ".cs")), quest.Handler);
		}
		XElement following = quests.Single(n => (int?)n.Attribute("id") == 2947);
		Assert.Equal(3, following.Elements("rewards").Count());
		Assert.Equal((4000, (int?)null), ((int)following.Elements("rewards").ElementAt(1).Attribute("gold")!, (int?)following.Elements("rewards").ElementAt(1).Attribute("ap")));
		Assert.Equal("1", (string)following.Attribute("use_class_reward")!);
		Assert.Equal([100101199, 101501224], following.Elements("priest_selectable_reward").Select(n => (int)n.Attribute("item_id")!));
		Assert.Equal(110551147, (int)quests.Single(n => (int?)n.Attribute("id") == 24020).Element("rewards")!
			.Elements("selectable_reward_item").ElementAt(3).Attribute("item_id")!);
		Assert.Equal(["SELECTED_QUEST_REWARD4", "SELECTED_QUEST_REWARD2"], Leg.RewardChoiceList.Select(choice => choice.Action));
		Assert.Equal([5, 6], new[] { "q24020-aegir", "q2947-reward" }.Select(key => Leg.Steps.Single(step => step.Key == key).Pages[^1]));

		// 880,687 XP to level 26 at the start snapshot (docs/natural-ntc-readiness.md); the five quests pay 157,945 more than that.
		Assert.Equal(880_687 + 157_945, Leg.Quests.Sum(quest => (long)quest.RewardExperience));
	}

	[Fact]
	public void EveryStepStandsAtItsShippedNpcAndTheThreeTeleportsExist()
	{
		foreach (NaturalAltgardStep step in Leg.Steps)
		{
			string file = Leg.StepMap(step) == NaturalAbyssEntry.Morheim ? "Npcs/220020000_Morheim.xml" : "Npcs/120010000_Pandaemonium.xml";
			float[] spot = Spot(file, step.NpcId);
			Assert.True(spot.Zip(step.Position, (a, b) => MathF.Abs(a - b)).All(d => d < 0.01f), step.Key);
		}
		Assert.Equal(19, Leg.Steps.Length);
		Assert.Equal([0, 0, 1, 0, 1, 2, 3, 0, 4, 6, 5, 0, 1, 9, 8], Leg.Steps.Where(step => step.ExpectedStatus == "START").Select(step => step.Var!.Value));
		Assert.Equal(4, Leg.Steps.Count(step => step.ExpectedStatus == "REWARD"));
		Assert.Equal(89, Leg.Steps.Single(step => step.Key == Scope.RingCourse.StartStep).MovieId);

		XElement[] teleporters = XDocument.Load(Data("npc_teleporter.xml")).Descendants("teleporter_template").ToArray();
		foreach (NaturalAltgardMapTrip trip in Leg.MapTripList)
		{
			XElement template = Assert.Single(teleporters, n => ((string)n.Attribute("npc_ids")!).Split(' ').Contains(trip.TeleporterNpcId.ToString(CultureInfo.InvariantCulture)));
			XElement location = Assert.Single(template.Descendants("telelocation"), n => (int)n.Attribute("loc_id")! == trip.LocationId);
			Assert.Equal(trip.Fare, (int)location.Attribute("price")!);
		}
		XElement landing = XDocument.Load(Data("teleport_location.xml")).Root!.Elements().Single(n => (int)n.Attribute("loc_id")! == 10);
		Assert.Equal((NaturalAbyssEntry.Morheim, Scope.Arrival[0], Scope.Arrival[1]), ((int)landing.Attribute("mapid")!, F(landing, "posX"), F(landing, "posY")));
		XElement bind = XDocument.Load(Data("bind_points", "bind_points.xml")).Root!.Elements().Single(n => (int)n.Attribute("npcid")! == Leg.Bind!.NpcId);
		Assert.Equal(Leg.Bind!.Price, (int)bind.Attribute("price")!);
		Assert.True(Spot("Npcs/220020000_Morheim.xml", Leg.Bind.NpcId).Zip(Leg.Bind.Position, (a, b) => MathF.Abs(a - b)).All(d => d < 0.01f));
	}

	[Fact]
	public void RingCourseIsTheSixShippedRingsInsideTheFortressFlyZone()
	{
		XElement[] rings = XDocument.Load(Data("fly_rings", "fly_rings.xml")).Root!.Elements("fly_ring").ToArray();
		foreach (NaturalAbyssRing ring in Scope.RingCourse.Rings)
		{
			XElement shipped = Assert.Single(rings, n => (string)n.Attribute("name")! == ring.Name);
			XElement center = shipped.Element("center")!;
			Assert.Equal((NaturalAbyssEntry.Morheim, Scope.RingCourse.RingRadius), ((int)shipped.Attribute("map")!, F(shipped, "radius")));
			Assert.Equal(ring.Center, new[] { F(center, "x"), F(center, "y"), F(center, "z") });
		}
		XElement zone = XDocument.Load(Data("zones", "zones_220020000.xml")).Descendants("zone")
			.Single(n => (string)n.Attribute("name")! == Leg.Flight!.Zones[0].Name);
		Assert.Equal("FLY", (string)zone.Attribute("zone_type")!);
		Assert.Equal((Scope.RingCourse.ZoneBottom, Scope.RingCourse.ZoneTop), (F(zone.Element("points")!, "bottom"), F(zone.Element("points")!, "top")));
		// Ring 5 is the one to fly carefully: 3.44 m under the zone's ceiling, less than the ring's own radius.
		Assert.Equal(3.44f, Scope.RingCourse.ZoneTop - Scope.RingCourse.Rings[4].Center[2], 2);
		Assert.All(Scope.RingCourse.Rings.Where((_, index) => index != 4), ring => Assert.True(Scope.RingCourse.ZoneTop - ring.Center[2] > 25));
	}

	[Fact]
	public void ArenaIsTheShippedSoloInstanceWithElevenSpirits()
	{
		NaturalAbyssArena arena = Scope.Arena;
		XElement spawns = XDocument.Load(Data("spawns", "Instances", "320090000_Triniel_Underground_Arena.xml")).Root!;
		foreach (NaturalAbyssSpirit spirit in arena.Spirits)
			Assert.Equal(spirit.Count, spawns.Descendants("spawn").Single(n => (int)n.Attribute("npc_id")! == spirit.NpcId).Elements("spot").Count());
		Assert.Equal((6, 5), (arena.Groups.Sum(group => group.Mages), arena.Groups.Sum(group => group.Warriors)));
		Assert.True(Spot("Instances/320090000_Triniel_Underground_Arena.xml", arena.ExitNpcId).Zip(arena.ExitPosition, (a, b) => MathF.Abs(a - b)).All(d => d < 0.01f));
		Assert.True(Spot("Npcs/120010000_Pandaemonium.xml", arena.EntranceNpcId).Zip(arena.EntrancePosition, (a, b) => MathF.Abs(a - b)).All(d => d < 0.01f));
		XElement path = XDocument.Load(Data("portals", "portal_template2.xml")).Descendants("portal_use")
			.Single(n => (int)n.Attribute("npc_id")! == arena.EntranceNpcId).Element("portal_path")!;
		XElement loc = XDocument.Load(Data("portals", "portal_loc.xml")).Root!.Elements().Single(n => (int)n.Attribute("loc_id")! == (int)path.Attribute("loc_id")!);
		Assert.Equal((arena.MapId, arena.Arrival[0], arena.Arrival[1], arena.Arrival[2]), ((int)loc.Attribute("world_id")!, F(loc, "x"), F(loc, "y"), F(loc, "z")));
		XElement limits = XDocument.Load(Data("instance_cooltimes", "instance_cooltimes.xml")).Root!.Elements()
			.Single(n => (int)n.Attribute("worldId")! == arena.MapId && (string)n.Attribute("race")! == "ASMODIANS");
		Assert.Equal(("0", "1"), ((string)limits.Element("ent_cool_time")!, (string)limits.Element("max_member_dark")!));
	}

	[Fact]
	public void CoinArmorIsWhatVebnaSellsAndTheRewardsAreTheShippedItems()
	{
		NaturalAbyssCoinArmor armor = Scope.CoinArmor;
		XElement trade = XDocument.Load(Data("npc_trade_list.xml")).Descendants("tradelist_template").Single(n => (int?)n.Attribute("npc_id") == armor.VendorNpcId);
		Assert.Equal("REWARD", (string)trade.Attribute("npc_type")!);
		Assert.Contains(armor.GoodsListId, trade.Elements("tradelist").Select(n => (int)n.Attribute("id")!));
		Assert.True(Spot("Npcs/220020000_Morheim.xml", armor.VendorNpcId).Zip(armor.VendorPosition, (a, b) => MathF.Abs(a - b)).All(d => d < 0.01f));
		int[] sold = XDocument.Load(Data("goodslists", "goodslists.xml")).Descendants("list").Single(n => (int)n.Attribute("id")! == armor.GoodsListId)
			.Elements("item").Select(n => (int)n.Attribute("id")!).ToArray();
		Dictionary<int, XElement> items = XDocument.Load(Data("items", "item_templates.xml")).Root!.Elements().ToDictionary(n => (int)n.Attribute("id")!);
		string[] groups = ["CH_TORSO", "CH_GLOVE", "CH_SHOES", "CH_SHOULDER", "CH_PANTS"];
		foreach (NaturalAbyssCoinTier tier in armor.Tiers)
		{
			Assert.Equal(groups.Order(), tier.Pieces.Select(piece => (string)items[piece.ItemId].Attribute("item_group")!).Order());
			foreach (NaturalCoinGearPurchase piece in tier.Pieces)
			{
				XElement item = items[piece.ItemId];
				XElement price = item.Element("acquisition")!;
				Assert.Contains(piece.ItemId, sold);
				Assert.Equal((tier.Level, "REWARD", armor.CoinItemId, piece.Cost),
					((int)item.Attribute("level")!, (string)price.Attribute("type")!, (int)price.Attribute("item")!, (int)price.Attribute("count")!));
			}
		}
		Assert.Equal(["RARE", "LEGEND"], armor.Tiers.Select(tier => tier.Pieces.Select(piece => (string)items[piece.ItemId].Attribute("quality")!).Distinct().Single()));
		// The level-26 tier is the best one Vebna sells: no chain piece on her list has more defence.
		Assert.All(armor.Tiers[1].Pieces, piece => Assert.Equal(sold.Select(id => items[id]).Where(item =>
			(string?)item.Attribute("item_group") == (string)items[piece.ItemId].Attribute("item_group")!).Max(Defence), Defence(items[piece.ItemId])));

		// AX-Q1: the staff taken from Q2947 has more magic boost than the staff the snapshot wears.
		static int Boost(XElement item) => (int)item.Element("weapon_stats")!.Attribute("boost_magical_skill")!;
		Assert.Equal((460, 370), (Boost(items[101501224]), Boost(items[101501357])));
		// AX-01: a scroll is gated by its restrict attribute, not its item level, so the level-30 scroll works at 25.
		Assert.Equal(("30", (string?)null), ((string)items[164000079].Attribute("level")!, (string?)items[164000079].Attribute("restrict")));
		Assert.All(Scope.Inventory.Open.Concat(Scope.Inventory.Discard).Concat(Scope.Inventory.KeepSealed).Concat(Scope.ProtectedItemIds),
			id => Assert.True(items.ContainsKey(id), id.ToString(CultureInfo.InvariantCulture)));

		static int Defence(XElement item) => item.Element("modifiers")!.Elements("add")
			.Where(n => (string)n.Attribute("name")! == "PHYSICAL_DEFENSE").Sum(n => (int)n.Attribute("value")!);
	}

	[Fact]
	public void SuppliedHelpIsLimitedToTheApprovedLegAndTotals()
	{
		NaturalHelpItemSupply.RequireApproved(164000079, 1, "ax");
		Assert.Throws<InvalidOperationException>(() => NaturalHelpItemSupply.RequireApproved(164000079, 1, "ax", alreadySupplied: 1));
		Assert.Throws<InvalidOperationException>(() => NaturalHelpItemSupply.RequireApproved(164000079, 2, "ax"));
		NaturalHelpItemSupply.RequireApproved(186000007, 4, "ax");
		NaturalHelpItemSupply.RequireApproved(186000007, 40, "ax", alreadySupplied: 4);
		Assert.Throws<InvalidOperationException>(() => NaturalHelpItemSupply.RequireApproved(186000007, 41, "ax", alreadySupplied: 4));
		// Outside the ax leg neither item is help at all.
		foreach (string? leg in new[] { null, "l12", "cg", "all" })
		{
			Assert.Throws<InvalidOperationException>(() => NaturalHelpItemSupply.RequireApproved(164000079, 1, leg));
			Assert.Throws<InvalidOperationException>(() => NaturalHelpItemSupply.RequireApproved(186000007, 1, leg));
		}
		// The level-band kit is unchanged on every leg.
		NaturalHelpItemSupply.RequireApproved(164000133, 60, "ax");
		NaturalHelpItemSupply.RequireApproved(164000133, 60, "l12");
		Assert.Throws<InvalidOperationException>(() => NaturalHelpItemSupply.RequireApproved(164000133, 61, "ax"));
	}

	[Fact]
	public void TimedAttemptsAreBoundedAndReachTheOutcomeLedger()
	{
		NaturalAbyssArena arena = Scope.Arena;
		var attempts = new List<NaturalAbyssAttempt>();
		Assert.Equal("enter", NaturalAbyssAttempts.NextArena(arena, attempts).Action);
		attempts.Add(new(NaturalAbyssAttempts.Arena, 1, "timer-expired", 1_000, 241_000, 7, "seven of ten"));
		// D34: the server destroys a failed attempt's instance, so the next try enters at once, in a new one.
		Assert.Equal("enter", NaturalAbyssAttempts.NextArena(arena, attempts).Action);
		attempts.Add(new(NaturalAbyssAttempts.Arena, 2, "died", 241_500, 300_000, 3, "died to the south group"));
		Assert.Equal("enter", NaturalAbyssAttempts.NextArena(arena, attempts).Action);
		attempts.Add(new(NaturalAbyssAttempts.Arena, 3, "timer-expired", 300_500, 540_500, 9, "nine of ten"));
		Assert.Equal("stop-finding", NaturalAbyssAttempts.NextArena(arena, attempts).Action);
		Assert.Equal("complete", NaturalAbyssAttempts.NextArena(arena, [new(NaturalAbyssAttempts.Arena, 1, NaturalAbyssAttempts.Done, 0, 200_000, 10, "ten kills")]).Action);

		NaturalAbyssRingCourse course = Scope.RingCourse;
		var flights = new List<NaturalAbyssAttempt>();
		for (int number = 1; number <= course.MaxAttempts; number++)
		{
			Assert.Equal("start", NaturalAbyssAttempts.NextRingCourse(course, flights).Action);
			flights.Add(new(NaturalAbyssAttempts.RingCourse, number, "timer-expired", number * 100_000, number * 100_000 + 70_000, 4, "four rings"));
		}
		Assert.Equal("ask-operator-recorded-flight", NaturalAbyssAttempts.NextRingCourse(course, flights).Action);
		// The arena's tries do not count against the course's, and a gap in the numbering is refused.
		Assert.Equal("start", NaturalAbyssAttempts.NextRingCourse(course, attempts).Action);
		Assert.Throws<InvalidDataException>(() => NaturalAbyssAttempts.NextRingCourse(course, [flights[0], flights[2]]));

		// The ledger auditor keeps every trace row whose name contains "timed".
		Assert.All(new[] { NaturalAbyssAttempts.Arena, NaturalAbyssAttempts.RingCourse }, kind => Assert.Contains("timed", NaturalAbyssAttempts.Diagnostic(kind)));
		Assert.Equal(7, NaturalAbyssAttempts.Row(attempts[0])["progress"]);
		Assert.Contains("'timed' in packet", File.ReadAllText(Path.Combine(RealStaticData.RepoRoot(), "scripts", "sim", "audit-natural-complete.py")));
	}

	[Theory]
	[InlineData(PlayerClass.CLERIC, 25, "ax", 220020000, true)]
	[InlineData(PlayerClass.CLERIC, 26, "ax", 320090000, true)]
	[InlineData(PlayerClass.CLERIC, 24, "ax", 220020000, false)]
	[InlineData(PlayerClass.CLERIC, 25, "l12", 220020000, false)]
	[InlineData(PlayerClass.CLERIC, 25, null, 320090000, false)]
	[InlineData(PlayerClass.CHANTER, 25, "ax", 220020000, false)]
	public void MorheimAndTheArenaBelongOnlyToTheLevelTwentyFiveClericOnThisLeg(PlayerClass playerClass, int level, string? leg, int map, bool accepted)
	{
		if (accepted) Assert.Equal(NaturalJourneyStage.AscensionCleric, NaturalJourneyIdentityRules.Classify(playerClass, level, map, leg));
		else Assert.Throws<InvalidDataException>(() => NaturalJourneyIdentityRules.Classify(playerClass, level, map, leg));
	}

	[Fact]
	public void OnlyThisLegCarriesTheScopeAndEarlierLegsKeepTheirs()
	{
		Assert.All(NaturalAltgardContract.Legs.Keys.Where(leg => leg != NaturalAbyssEntry.Leg), leg => Assert.Null(NaturalAltgardContract.LoadLeg(leg).AbyssEntry));
		Assert.Equal((true, false, false), (Scope.Inventory.AfterEveryTurnIn, Scope.Level.Hunting, Scope.Weapon.Buy));
		Assert.Equal([21, 26], Scope.CoinArmor.Tiers.Select(tier => tier.Level));
		Assert.Contains(Scope.CoinArmor.VendorNpcId, Leg.GraphNpcIds(NaturalAltgardContract.LoadPlans(NaturalAbyssEntry.Leg)));
		Assert.Empty(NaturalAltgardContract.LoadPlans(NaturalAbyssEntry.Leg));
	}

	[Fact]
	public void StartIsVerifiedFromObservedStateAndNothingElseIsAccepted()
	{
		NaturalAbyssEntryStart start = NaturalAbyssEntryLeg.VerifyStart(Leg, StartState(), 133276, 73_064_001);
		Assert.Equal((true, "ax", 133276, 220030000, 25, 748_485L, 7L, 176, 156530),
			(start.Verified, start.Leg, start.CharacterId, start.MapId, start.Level, start.Kinah, start.BronzeCoins, start.CompletedQuests, start.StaffObjectId));
		Assert.Equal([2945], start.StartedQuestIds);
		Assert.Equal([101501357, 186000006, 186000007, 188053787], start.ProtectedItems.Select(item => item.ItemId));
		// Base prices: 1,700 + 1,500 + 1,500 for the teleports and 2,690 for the bind. AX-01 saw 9,327 charged.
		Assert.Equal(7_390, NaturalAbyssEntryLeg.BaseTravelCost(Leg));

		NaturalAltgardObservation ok = StartState();
		NaturalJourneyItem[] inventory = ok.Inventory!;
		NaturalAltgardObservation With(params NaturalJourneyItem[] items) => ok with
		{
			Inventory = items, ItemCounts = items.GroupBy(item => item.ItemId).ToDictionary(group => group.Key, group => group.Sum(item => item.Count)),
		};
		NaturalAltgardObservation[] refused =
		[
			ok with { Level = 24 },
			ok with { MapId = NaturalAbyssEntry.Morheim },
			ok with { IsDead = true },
			ok with { Synchronized = false },
			ok with { Kinah = 1_000 },
			ok with { Bind = new BotBindPoint(120010000, ok.Position, 0) },
			ok with { Quests = new Dictionary<int, BotQuestState> { [2945] = new(2945, 3, 1, 0, null) } },
			ok with { Quests = new Dictionary<int, BotQuestState> { [2945] = new(2945, 3, 0, 0, null), [24020] = new(24020, 3, 0, 0, null) } },
			ok with { Quests = new Dictionary<int, BotQuestState>() },
			ok with { CompletedQuestIds = ok.CompletedQuestIds.Where(id => id != 24016).ToHashSet() },
			ok with { CompletedQuestIds = ok.CompletedQuestIds.Append(2945).ToHashSet() },
			With([.. inventory.Where(item => item.ItemId != 188053787)]),
			With([.. inventory.Where(item => item.ItemId != 186000007), new(157702, 186000007, 8, 65535)]),
			With([.. inventory.Where(item => item.ItemId != 101501357), new(156530, 101501357, 1, 65535)]),
			With([.. inventory, new(900001, 110551147, 1, 65535)]),
			With([.. inventory, new(900002, 164000079, 1, 65535)]),
			With([.. inventory, new(900003, 110501097, 1, 65535)]),
		];
		Assert.All(refused, state => Assert.Throws<InvalidDataException>(() => NaturalAbyssEntryLeg.VerifyStart(Leg, state, 133276, 0)));
	}

	/// <summary>The start as the snapshot's own receipt records it (run/snapshots/altgard-rc-complete-s1).</summary>
	private static NaturalAltgardObservation StartState()
	{
		var position = new BotPosition(1660.43f, 1813.49f, 253.726f, 98);
		NaturalJourneyItem[] inventory =
		[
			new(133277, 182400001, 748_485, 65535), new(156530, 101501357, 1, 3), new(156843, 188053787, 1, 65535),
			new(157702, 186000007, 7, 65535), new(139546, 186000006, 19, 65535), new(140185, 110551139, 1, 8),
		];
		return new(true, 220030000, 25, false, new Dictionary<int, BotQuestState> { [2945] = new(2945, 3, 0, 0, null) },
			Leg.Start.CompletedQuestIds.ToHashSet(), position,
			inventory.GroupBy(item => item.ItemId).ToDictionary(group => group.Key, group => group.Sum(item => item.Count)),
			Bind: new BotBindPoint(220030000, position, 0), Kinah: 748_485, Inventory: inventory);
	}
}
