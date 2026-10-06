using System.Globalization;
using System.Text.RegularExpressions;
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
	public void ArenaIsTheShippedSoloInstanceWithTwelveSpirits()
	{
		NaturalAbyssArena arena = Scope.Arena;
		XElement spawns = XDocument.Load(Data("spawns", "Instances", "320090000_Triniel_Underground_Arena.xml")).Root!;
		foreach (NaturalAbyssSpirit spirit in arena.Spirits)
			Assert.Equal(spirit.Count, spawns.Descendants("spawn").Single(n => (int)n.Attribute("npc_id")! == spirit.NpcId).Elements("spot").Count());
		Assert.Equal((6, 6), (arena.Groups.Sum(group => group.Mages), arena.Groups.Sum(group => group.Warriors))); // D36: NCSoft's twelve
		// AX-08: each group stands behind a shipped static door that is closed and clickable (state 6). The door lies between
		// its stand point in the hall and the group, and the stand point is within a click's reach of it.
		XElement doors = XDocument.Load(Data("staticdoors", "staticdoor_templates.xml")).Descendants("world").Single(n => (int)n.Attribute("world")! == arena.MapId);
		Assert.Equal(3, doors.Elements("staticdoor").Count());
		foreach (NaturalAbyssSpiritGroup group in arena.Groups)
		{
			XElement door = doors.Elements("staticdoor").Single(n => (int)n.Attribute("id")! == group.DoorId);
			Assert.Equal(("6", group.DoorPosition[0], group.DoorPosition[1], group.DoorPosition[2]), ((string)door.Attribute("state")!, F(door, "x"), F(door, "y"), F(door, "z")));
			float Flat(float[] a, float[] b) => MathF.Sqrt(MathF.Pow(a[0] - b[0], 2) + MathF.Pow(a[1] - b[1], 2));
			Assert.Equal(4f, Flat(group.DoorStand, group.DoorPosition), 2);
			Assert.True(Flat(group.DoorStand, group.Center) > Flat(group.DoorPosition, group.Center), $"{group.Key}: the stand point is on the group's side of its door");
		}
		Assert.True(Spot("Instances/320090000_Triniel_Underground_Arena.xml", arena.ExitNpcId).Zip(arena.ExitPosition, (a, b) => MathF.Abs(a - b)).All(d => d < 0.01f));
		// D35: Garm sends the player in. Both of his SETPRO3 talks end at the arena's one portal location, where the entrance
		// 700368 (which no attempt uses any more) also leads.
		XElement path = XDocument.Load(Data("portals", "portal_template2.xml")).Descendants("portal_use")
			.Single(n => (int)n.Attribute("npc_id")! == 700368).Element("portal_path")!;
		XElement loc = XDocument.Load(Data("portals", "portal_loc.xml")).Root!.Elements().Single(n => (int)n.Attribute("world_id")! == arena.MapId);
		Assert.Equal((int)path.Attribute("loc_id")!, (int)loc.Attribute("loc_id")!);
		Assert.Equal((arena.Arrival[0], arena.Arrival[1], arena.Arrival[2]), (F(loc, "x"), F(loc, "y"), F(loc, "z")));
		foreach (string key in new[] { arena.StartStep, arena.RestartStep })
		{
			NaturalAltgardStep garm = Leg.Steps.Single(step => step.Key == key);
			Assert.Equal((204089, "SETPRO3", arena.MapId), (garm.NpcId, garm.Actions[^1], garm.Teleport?.MapId));
			Assert.Equal(arena.Arrival, garm.Teleport!.Position);
		}
		Assert.Null(Leg.Steps.Single(step => step.Key == arena.DoneStep).Teleport);
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

	/// <summary>The physical defence of the pieces of the two manifests, as the shipped tooltips show it: the five worn on
	/// arrival, the five Rank 8 pieces and the five Elite Rank 7 pieces. A piece with a base value and a bonus shows their sum.</summary>
	private static readonly Dictionary<int, int> Defence = new()
	{
		[110551147] = 177, [111501081] = 78, [112501641] = 80, [113501720] = 107, [114501726] = 67,
		[110501097] = 134, [111501066] = 80, [112501016] = 80, [113501075] = 107, [114501082] = 80,
		[110501104] = 177, [111501073] = 107, [112501023] = 123, [113501082] = 142, [114501089] = 123,
	};
	private static int DefenceOf(int itemId) => Defence.GetValueOrDefault(itemId);

	/// <summary>AX-05, AX-06: the leg's first two phases, decision by decision, as run/ax06 played them.</summary>
	[Fact]
	public void MorheimTheCommanderAndTheCoinArmorComeFirstAndThenTheFrontier()
	{
		static (string, string, string?, int?) Shape(NaturalAbyssEntryDecision next) => (next.Phase, next.Action, next.StepKey, next.MapId);
		NaturalAbyssEntryDecision Decide(NaturalAltgardObservation state) => NaturalAbyssEntryDecisionEngine.Decide(Leg, state, 1, DefenceOf);
		NaturalAltgardObservation start = StartState(), arrived = ArrivedState(), commander = CommanderDoneState();

		Assert.Equal(("morheim-arrival", "travel", null, (int?)NaturalAbyssEntry.Morheim), Shape(Decide(start)));
		// On arrival the old Altgard bind is replaced first; Q24020 is already in the journal by then.
		Assert.Equal(("morheim-arrival", "bind", null, null), Shape(Decide(arrived with { Bind = start.Bind })));
		Assert.Equal(("morheim-arrival", "talk", "q24020-aegir", (int?)NaturalAbyssEntry.Morheim), Shape(Decide(arrived)));
		// The commander done: the Rank 8 gloves and brogans beat what is worn; the torso, shoulders and legs do not.
		NaturalAbyssEntryDecision buy = Decide(commander);
		Assert.Equal(("coin-armor-21", "coin-armor", null, null), Shape(buy));
		Assert.Contains("111501066, 114501082 for 4 coins", buy.Reason);
		// Bought and still in the cube: wear them. Worn: the phase is settled and the capital missions are next.
		Assert.Equal(("coin-armor-21", "inventory-check", null, null), Shape(Decide(Adding(commander, new(900016, 111501066, 1, 65535), new(900032, 114501082, 1, 65535)))));
		// Q2945's first step is in Pandaemonium: the approved teleport from Morheim comes first.
		Assert.Equal(("capital-missions", "travel", null, (int?)NaturalAbyssEntry.Pandaemonium), Shape(Decide(CoinArmorWornState())));

		// What the rule waits for, recovers from or refuses.
		Assert.Equal("refresh-observation", Decide(start with { Synchronized = false }).Action);
		Assert.Equal("refresh-observation", Decide(arrived with { Quests = start.Quests }).Action);
		Assert.Equal(("recover", "revive"), (Decide(arrived with { IsDead = true }).Phase, Decide(arrived with { IsDead = true }).Action));
		Assert.Equal("blocked", Decide(start with { MapId = 220010000 }).Action);
		Assert.Equal("blocked", Decide(arrived with { Quests = new Dictionary<int, BotQuestState> { [24020] = new(24020, 3, 1, 0, null) } }).Action);
		Assert.Equal("blocked", Decide(commander with { MapId = NaturalAbyssEntry.Pandaemonium }).Action);
		// The Pandaemonium teleport back to Morheim is an approved trip too.
		Assert.Equal("travel", Decide(start with { MapId = NaturalAbyssEntry.Pandaemonium }).Action);
	}

	/// <summary>AX-06: the manifest the operator's rule gives ("only if any of it is better than what we are wearing").</summary>
	[Fact]
	public void LevelTwentyOneManifestBuysOnlyThePiecesWithMoreDefence()
	{
		NaturalAbyssCoinTier tier = Scope.CoinArmor.Tiers.Single(entry => entry.Level == 21);
		NaturalAbyssCoinManifest manifest = NaturalAbyssCoinArmorPolicy.Plan(Scope.CoinArmor, tier, CommanderDoneState().Inventory!, DefenceOf);
		Assert.Equal([(8, "keep"), (16, "buy"), (2048, "keep"), (4096, "keep"), (32, "buy")], manifest.Slots.Select(slot => ((int)slot.Slot, slot.Action)));
		Assert.Equal((4, 7L, 0L, false), (manifest.Cost, manifest.CoinsOwned, manifest.CoinsToSupply, manifest.Done));
		Assert.Contains("tie", manifest.Slots.Single(slot => slot.Slot == 2048).Reason);
		// Four coins short of a manifest is what the help mechanism would supply; a bare slot takes any piece.
		NaturalAbyssCoinManifest poor = NaturalAbyssCoinArmorPolicy.Plan(Scope.CoinArmor, tier,
			[.. CommanderDoneState().Inventory!.Where(item => item.ItemId is not (186000007 or 110551147))], DefenceOf);
		Assert.Equal((7, 0L, 7L), (poor.Cost, poor.CoinsOwned, poor.CoinsToSupply));
		Assert.Equal("buy", poor.Slots.Single(slot => slot.Slot == 8).Action);
		Assert.True(NaturalAbyssCoinArmorPolicy.Plan(Scope.CoinArmor, tier, CoinArmorWornState().Inventory!, DefenceOf).Done);
		// The defences are the shipped tooltips': every PHYSICAL_DEFENSE entry of the piece, added up.
		string items = File.ReadAllText(Data("items", "item_templates.xml"));
		Assert.All(Defence, piece =>
		{
			int from = items.IndexOf($"<item_template id=\"{piece.Key}\"", StringComparison.Ordinal);
			string template = items[from..items.IndexOf("</item_template>", from, StringComparison.Ordinal)];
			Assert.Equal(piece.Value, Regex.Matches(template, "name=\"PHYSICAL_DEFENSE\" value=\"(\\d+)\"")
				.Sum(match => int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture)));
		});
	}

	/// <summary>AX-12: the level-26 manifest, from what the Cleric wears after the missions. The hauberk is a tie.</summary>
	[Fact]
	public void LevelTwentySixManifestBuysTheFourPiecesWithMoreDefence()
	{
		NaturalAbyssCoinTier tier = Scope.CoinArmor.Tiers.Single(entry => entry.Level == 26);
		Assert.Equal("endpoint", tier.When);
		Assert.Same(tier, NaturalAbyssEntryDecisionEngine.CoinTier(Scope.CoinArmor, NaturalAbyssEntryDecisionEngine.CoinArmor26Phase));
		Assert.Equal(21, NaturalAbyssEntryDecisionEngine.CoinTier(Scope.CoinArmor, NaturalAbyssEntryDecisionEngine.CoinArmor21Phase).Level);
		Assert.Throws<ArgumentException>(() => NaturalAbyssEntryDecisionEngine.CoinTier(Scope.CoinArmor, NaturalAbyssEntryDecisionEngine.ArenaPhase));

		// Three coins are left after the level-21 pieces; the two chests add what they add (ten, then two to nine).
		NaturalAltgardObservation worn = CoinArmorWornState();
		NaturalAbyssCoinManifest manifest = NaturalAbyssCoinArmorPolicy.Plan(Scope.CoinArmor, tier, worn.Inventory!, DefenceOf);
		Assert.Equal([(8, "keep"), (16, "buy"), (2048, "buy"), (4096, "buy"), (32, "buy")], manifest.Slots.Select(slot => ((int)slot.Slot, slot.Action)));
		Assert.Equal([(177, 177), (80, 107), (80, 123), (107, 142), (80, 123)], manifest.Slots.Select(slot => (slot.WornDefence, slot.CoinDefence)));
		Assert.Contains("tie", manifest.Slots.Single(slot => slot.Slot == 8).Reason);
		Assert.Equal((31, 3L, 28L, false), (manifest.Cost, manifest.CoinsOwned, manifest.CoinsToSupply, manifest.Done));
		// With the lowest and the highest chest the supply is 16 and 9 coins; both are inside the approved 44 with the level-21 tier's none.
		NaturalAbyssSupply approved = Scope.Supplies.Single(supply => supply.ItemId == Scope.CoinArmor.CoinItemId);
		Assert.All(new[] { (15, 16L), (22, 9L) }, chest =>
		{
			NaturalAbyssCoinManifest held = NaturalAbyssCoinArmorPolicy.Plan(Scope.CoinArmor, tier,
				[.. worn.Inventory!.Where(item => item.ItemId != 186000007), new(157702, 186000007, chest.Item1, 65535)], DefenceOf);
			Assert.Equal(chest.Item2, held.CoinsToSupply);
			Assert.InRange(held.CoinsToSupply, 1, approved.MaxCount);
		});
		// Bought and worn, nothing of either tier is left to buy, and the Rank 8 pieces in the cube are not put back on.
		NaturalAltgardObservation dressed = Dressed(worn);
		Assert.True(NaturalAbyssCoinArmorPolicy.Plan(Scope.CoinArmor, tier, dressed.Inventory!, DefenceOf).Done);
		Assert.True(NaturalAbyssCoinArmorPolicy.Plan(Scope.CoinArmor, Scope.CoinArmor.Tiers.Single(entry => entry.Level == 21), dressed.Inventory!, DefenceOf).Done);
	}

	[Fact]
	public void FrontiersAreVerifiedFromObservedStateAndTheLedger()
	{
		NaturalAbyssEntryStart start = NaturalAbyssEntryLeg.VerifyStart(Leg, StartState(), 133276, 0);
		NaturalAbyssPayment[] paid = [new(24020, 293_759, 0, 25)];
		static int Boost(int itemId) => itemId switch { 101501357 => 370, 101501355 => 320, _ => 0 };
		NaturalAbyssLedger noCoins = new(293_759, 2_401, 2_690, paid, 2, 0, [], [], 0, [], 0, [], 0, 0, []);
		NaturalAbyssEntryProgress Verify(NaturalAltgardObservation state, NaturalAbyssLedger ledger, string frontier = "coin-armor-21") =>
			NaturalAbyssEntryLeg.VerifyProgress(Leg, start, state, frontier, ledger, Boost, DefenceOf, 59_385);

		// AX-05's frontier: Morheim and the commander, before any coin is spent.
		NaturalAltgardObservation done = CommanderDoneState();
		NaturalAbyssEntryProgress progress = Verify(done, noCoins);
		Assert.Equal(("ax", "coin-armor-21", 220020000, 25, 743_394L, 2_401L, 2_690L, 101501357, 110551147, 2, 7L),
			(progress.Leg, progress.Frontier, progress.MapId, progress.Level, progress.Kinah, progress.Fares, progress.BindPaid, progress.StaffItemId,
				progress.TorsoItemId, progress.InventoryChecks, progress.BronzeCoins));
		Assert.Equal([24020], progress.CompletedLegQuestIds);
		// The six Morheim campaign quests Q24020 puts in the journal are locked (levels 27 to 35); they are recorded, not played.
		Assert.Equal([2945], progress.StartedQuestIds);
		Assert.Equal([24021, 24022, 24023, 24024, 24025, 24026], progress.LockedQuestIds);

		NaturalJourneyItem[] inventory = done.Inventory!;
		Action[] refused =
		[
			() => Verify(done with { MapId = 220030000 }, noCoins),
			() => Verify(done with { Bind = StartState().Bind }, noCoins),
			() => Verify(done with { CompletedQuestIds = StartState().CompletedQuestIds }, noCoins),
			() => Verify(done with { Kinah = 743_395 }, noCoins),
			() => Verify(done with { Quests = new Dictionary<int, BotQuestState> { [2945] = new(2945, 3, 1, 0, null) } }, noCoins),
			() => Verify(done, noCoins with { ExperienceGained = 293_760 }),
			() => Verify(done, noCoins with { Payments = [new(24020, 293_758, 0, 25)], ExperienceGained = 293_758 }),
			() => Verify(done, noCoins with { Payments = [] }),
			() => Verify(done, noCoins with { InventoryChecks = 1 }),
			() => Verify(done, noCoins with { BindPaid = 2_691, Fares = 2_400 }),
			// The hauberk carried and the old one still worn; a mace in the hand; a better staff left in the cube.
			() => Verify(With([.. inventory.Where(item => item.ItemId is not (110551147 or 110551139)), new(133316, 110551147, 1, 65535), new(140185, 110551139, 1, 8)]), noCoins),
			() => Verify(With([.. inventory.Where(item => item.ItemId != 101501357), new(156530, 101501357, 1, 65535), new(900001, 100101334, 1, 1)]), noCoins),
			() => Verify(With([.. inventory.Where(item => item.ItemId != 101501357), new(156530, 101501357, 1, 65535), new(900002, 101501355, 1, 3)]), noCoins),
			() => Verify(With([.. inventory.Where(item => item.ItemId != 188053787)]), noCoins),
			// Coin armor before its turn, or a coin gone with nothing bought.
			() => Verify(done, noCoins with { CoinsSupplied = 4 }),
			() => Verify(With([.. inventory.Where(item => item.ItemId != 186000007), new(157702, 186000007, 6, 65535)]), noCoins),
		];
		Assert.All(refused, verify => Assert.Throws<InvalidDataException>(verify));

		// AX-06's frontier: the manifest decided once, its two pieces bought for coins alone and worn, nothing better left.
		NaturalAltgardObservation worn = CoinArmorWornState();
		NaturalAbyssCoinManifest manifest = NaturalAbyssCoinArmorPolicy.Plan(Scope.CoinArmor, Scope.CoinArmor.Tiers[0], done.Inventory!, DefenceOf);
		NaturalAbyssCoinPurchase[] bought = [new(21, 111501066, 900016, 16, 2, 7, 5, 743_394, 743_394), new(21, 114501082, 900032, 32, 2, 5, 3, 743_394, 743_394)];
		NaturalAbyssLedger coins = noCoins with { OtherInventoryChecks = 1, CoinManifests = [manifest], CoinPurchases = bought };
		NaturalAbyssEntryProgress settled = Verify(worn, coins, "capital-missions");
		Assert.Equal(("capital-missions", 3L, 0L, 3), (settled.Frontier, settled.BronzeCoins, settled.CoinsSupplied, settled.InventoryChecks));
		Assert.Equal([111501066, 114501082], settled.CoinPurchases!.Select(purchase => purchase.ItemId));
		Action[] refusedCoins =
		[
			() => Verify(done, noCoins, "capital-missions"),
			() => Verify(worn, coins with { CoinManifests = [] }, "capital-missions"),
			() => Verify(worn, coins with { CoinManifests = [manifest, manifest] }, "capital-missions"),
			() => Verify(worn, coins with { CoinPurchases = [bought[0]] }, "capital-missions"),
			() => Verify(worn, coins with { CoinsSupplied = 1 }, "capital-missions"),
			() => Verify(worn, coins with { CoinPurchases = [bought[0], bought[1] with { KinahAfter = 743_000 }] }, "capital-missions"),
			() => Verify(worn with { Kinah = 743_000 }, coins, "capital-missions"),
			// A bought piece left in the cube.
			() => Verify(Adding(done, new(900016, 111501066, 1, 65535), new(900032, 114501082, 1, 65535)) with
				{ ItemCounts = worn.ItemCounts }, coins, "capital-missions"),
		];
		Assert.All(refusedCoins, verify => Assert.Throws<InvalidDataException>(verify));

		NaturalAltgardObservation With(params NaturalJourneyItem[] items) => done with
		{
			Inventory = items, ItemCounts = items.GroupBy(item => item.ItemId).ToDictionary(group => group.Key, group => group.Sum(item => item.Count)),
		};
	}

	/// <summary>AX-07: the capital missions, one talk step per observed status and var, so a resumed run repeats no dialog.</summary>
	[Fact]
	public void CapitalMissionsFollowTheirTalkStepsToGarmsDoor()
	{
		NaturalAltgardObservation capital = CoinArmorWornState() with { MapId = NaturalAbyssEntry.Pandaemonium, Kinah = 741_276 };
		string[] Play(int questId, params (byte Status, int Var)[] states) => states.Select(at =>
		{
			var quests = new Dictionary<int, BotQuestState>(capital.Quests) { [questId] = new(questId, at.Status, at.Var, 0, null) };
			if (questId != 2945) quests.Remove(2945);
			int[] done = questId switch { 2945 => [], 2946 => [2945], _ => [2945, 2946] };
			NaturalAbyssEntryDecision next = NaturalAbyssEntryDecisionEngine.Decide(Leg,
				capital with { Quests = quests, CompletedQuestIds = capital.CompletedQuestIds.Concat(done).ToHashSet() }, 1, DefenceOf);
			return next is { Action: "talk", Phase: "capital-missions", StepKey: { } key } ? key : $"{next.Phase}:{next.Action}";
		}).ToArray();

		Assert.Equal(["q2945-balder", "q2945-therf", "q2945-reward"], Play(2945, (3, 0), (3, 1), (4, 1)));
		Assert.Equal(["q2946-balder", "q2946-204210", "q2946-204211", "q2946-204208", "q2946-reward"], Play(2946, (3, 0), (3, 1), (3, 2), (3, 3), (4, 3)));
		// Kvasir's var 0 is the last capital step. After it comes Garm: his first talk, his second after a failure, and at the
		// reward the way back to Morheim. Var 5 outside the arena is a failed attempt the server has not marked yet.
		Assert.Equal(["q2947-kvasir", "arena:talk", "arena:refresh-observation", "arena:talk", "morheim-return:travel"],
			Play(2947, (3, 0), (3, 4), (3, 5), (3, 6), (4, 7)));
		// A mission that is turned in before the next one shows in the journal is waited for; an unknown var is refused.
		Assert.Equal(["capital-missions:refresh-observation", "capital-missions:blocked"], Play(2946, (6, 0), (3, 9)));

		// From Morheim every capital step asks for Orhe's teleport first; Altgard has no approved way to Pandaemonium.
		NaturalAbyssEntryDecision fromMorheim = NaturalAbyssEntryDecisionEngine.Decide(Leg, CoinArmorWornState(), 1, DefenceOf);
		Assert.Equal(("travel", (int?)NaturalAbyssEntry.Pandaemonium, (int?)2945), (fromMorheim.Action, fromMorheim.MapId, fromMorheim.QuestId));
		Assert.Equal("blocked", NaturalAbyssEntryDecisionEngine.Decide(Leg, CoinArmorWornState() with { MapId = 220030000 }, 1, DefenceOf).Action);
	}

	[Fact]
	public void ArenaFrontierIsVerifiedWithTheCapitalsAccounts()
	{
		NaturalAbyssEntryStart start = NaturalAbyssEntryLeg.VerifyStart(Leg, StartState(), 133276, 0);
		static int Boost(int itemId) => itemId switch { 101501357 => 370, 101501355 => 320, _ => 0 };
		NaturalAltgardObservation worn = CoinArmorWornState();
		NaturalAbyssCoinManifest manifest = NaturalAbyssCoinArmorPolicy.Plan(Scope.CoinArmor, Scope.CoinArmor.Tiers[0], CommanderDoneState().Inventory!, DefenceOf);
		NaturalAbyssCoinPurchase[] bought = [new(21, 111501066, 900016, 16, 2, 7, 5, 743_394, 743_394), new(21, 114501082, 900032, 32, 2, 5, 3, 743_394, 743_394)];
		NaturalOpenedContainer[] opened =
		[
			new(188051192, 910001, new Dictionary<int, long> { [166000193] = 1 }), new(188051192, 910002, new Dictionary<int, long> { [166000192] = 1 }),
			new(188050878, 910003, new Dictionary<int, long> { [186000007] = 10 }),
		];
		NaturalAbyssPayment[] paid = [new(24020, 293_759, 0, 25), new(2945, 20_110, 0, 25), new(2946, 20_110, 0, 25)];
		// Two fares now: Ukin's 2,401 and Orhe's 2,118.
		NaturalAbyssLedger ledger = new(333_979, 4_519, 2_690, paid, 4, 1, [manifest], bought, 0, opened, 0, [], 0, 0, []);
		NaturalJourneyItem[] items = [.. worn.Inventory!.Where(item => item.ItemId is not (186000007 or 182400001)),
			new(157702, 186000007, 13, 65535), new(133277, 182400001, 741_276, 65535), new(910011, 166000193, 1, 65535), new(910012, 166000192, 1, 65535)];
		var quests = new Dictionary<int, BotQuestState>(worn.Quests) { [2947] = new(2947, 3, 4, 0, null) };
		quests.Remove(2945);
		NaturalAltgardObservation atGarm = worn with
		{
			MapId = NaturalAbyssEntry.Pandaemonium, Position = new BotPosition(1281.15f, 1176.92f, 215.09f, 0), Kinah = 741_276, Quests = quests,
			CompletedQuestIds = worn.CompletedQuestIds.Concat([2945, 2946]).ToHashSet(), Inventory = items,
			ItemCounts = items.GroupBy(item => item.ItemId).ToDictionary(group => group.Key, group => group.Sum(item => item.Count)),
		};
		NaturalAbyssEntryProgress Verify(NaturalAltgardObservation state, NaturalAbyssLedger counted) =>
			NaturalAbyssEntryLeg.VerifyProgress(Leg, start, state, "arena", counted, Boost, DefenceOf, 600_000);

		NaturalAbyssEntryProgress progress = Verify(atGarm, ledger);
		Assert.Equal(("arena", 120010000, 741_276L, 4_519L, 13L, 5), (progress.Frontier, progress.MapId, progress.Kinah, progress.Fares, progress.BronzeCoins, progress.InventoryChecks));
		Assert.Equal([24020, 2945, 2946], progress.CompletedLegQuestIds);
		Assert.Equal([2947], progress.StartedQuestIds);
		Assert.Equal([188051192, 188051192, 188050878], progress.Opened!.Select(container => container.ItemId));

		// AX-08: the same accounts once the arena is cleared and reported. One failed try and the clear: seventeen kills, nine
		// Mage Spirits at 738 XP and eight Warrior Spirits at 954.
		const long fights = 9 * 738 + 8 * 954;
		NaturalAbyssAttempt[] tries = [new("arena", 1, "timeout", 600_000, 845_000, 7, "ran out"), new("arena", 2, "done", 900_000, 1_010_000, 10, "cleared")];
		NaturalAbyssLedger fought = ledger with { ExperienceGained = 333_979 + fights, Attempts = tries, ArenaExperience = fights };
		NaturalAltgardObservation reported = atGarm with { Quests = new Dictionary<int, BotQuestState>(quests) { [2947] = new(2947, 4, 7 | 10 << 24, 0, null) } };
		NaturalAbyssEntryProgress VerifyCleared(NaturalAltgardObservation state, NaturalAbyssLedger counted) =>
			NaturalAbyssEntryLeg.VerifyProgress(Leg, start, state, "morheim-return", counted, Boost, DefenceOf, 1_100_000);
		NaturalAbyssEntryProgress clearedProgress = VerifyCleared(reported, fought);
		Assert.Equal(("morheim-return", 120010000, fights, 0), (clearedProgress.Frontier, clearedProgress.MapId, clearedProgress.ArenaExperience, clearedProgress.Deaths));
		Assert.Equal(["timeout", "done"], clearedProgress.Attempts!.Select(attempt => attempt.Outcome));
		// A death takes XP, so the arena's net XP is no longer a whole number of kills; with deaths that is accepted.
		VerifyCleared(reported, fought with { ExperienceGained = 333_979 + 9_000, ArenaExperience = 9_000, Deaths = 1 });
		Action[] refusedCleared =
		[
			() => VerifyCleared(atGarm, fought),
			() => VerifyCleared(reported, ledger),
			() => VerifyCleared(reported, fought with { Attempts = [tries[0]] }),
			() => VerifyCleared(reported, fought with { Attempts = [tries[1] with { Number = 1 }, tries[0] with { Number = 2 }] }),
			() => VerifyCleared(reported, fought with { Attempts = [tries[0], tries[0] with { Number = 2 }, tries[0] with { Number = 3 }, tries[1] with { Number = 4 }] }),
			() => VerifyCleared(reported, fought with { Attempts = [tries[0], tries[1] with { Progress = 9 }] }),
			() => VerifyCleared(reported, fought with { Attempts = [tries[0], tries[1] with { EndedMillis = 1_200_000 }] }),
			() => VerifyCleared(reported, fought with { ExperienceGained = 333_979 + 7_000, ArenaExperience = 7_000 }),
			() => VerifyCleared(reported, fought with { ArenaExperience = fights - 954 }),
			// Before Garm's talk no try may be on the ledger.
			() => Verify(atGarm, ledger with { Attempts = [tries[0]] }),
		];
		Assert.All(refusedCleared, verify => Assert.Throws<InvalidDataException>(verify));

		// AX-09: back in Morheim. Q2947 turned in for 403,012 XP and 4,000 Kinah, its staff worn, the manastone discarded,
		// Doman's fare paid, Q2042 taken at Aegir and waiting for Yornduf.
		NaturalAbyssPayment[] paidHome = [.. paid, new(2947, 403_012, 4_000, 25)];
		NaturalJourneyItem[] homeItems = [.. reported.Inventory!.Where(item => item.ItemId is not (101501357 or 182400001)),
			new(156530, 101501357, 1, 65535), new(920001, 101501224, 1, 3), new(133277, 182400001, 743_158, 65535)];
		var homeQuests = new Dictionary<int, BotQuestState>(quests) { [2042] = new(2042, 3, 1, 0, null) };
		homeQuests.Remove(2947);
		NaturalAltgardObservation home = reported with
		{
			MapId = NaturalAbyssEntry.Morheim, Position = new BotPosition(225.225f, 2415.47f, 454.11f, 46), Kinah = 743_158, Quests = homeQuests,
			CompletedQuestIds = reported.CompletedQuestIds.Append(2947).ToHashSet(), Inventory = homeItems,
			ItemCounts = homeItems.GroupBy(item => item.ItemId).ToDictionary(group => group.Key, group => group.Sum(item => item.Count)),
		};
		static int HomeBoost(int itemId) => itemId switch { 101501224 => 460, 101501357 => 370, 101501355 => 320, _ => 0 };
		NaturalAbyssLedger returned = fought with
		{
			ExperienceGained = 333_979 + fights + 403_012, Fares = 6_637, Payments = paidHome, InventoryChecks = 5,
			Discarded = [new(920002, 167000465, 1, 65535)],
		};
		NaturalAbyssEntryProgress VerifyHome(NaturalAltgardObservation state, NaturalAbyssLedger counted) =>
			NaturalAbyssEntryLeg.VerifyProgress(Leg, start, state, "ring-course", counted, HomeBoost, DefenceOf, 1_300_000);
		NaturalAbyssEntryProgress atHome = VerifyHome(home, returned);
		Assert.Equal(("ring-course", 220020000, 743_158L, 101501224, 6_637L), (atHome.Frontier, atHome.MapId, atHome.Kinah, atHome.StaffItemId, atHome.Fares));
		Assert.Equal([24020, 2945, 2946, 2947], atHome.CompletedLegQuestIds);
		Assert.Equal([167000465], atHome.Discarded!.Select(item => item.ItemId));
		Action[] refusedHome =
		[
			() => VerifyHome(home with { MapId = NaturalAbyssEntry.Pandaemonium }, returned),
			() => VerifyHome(home with { Quests = new Dictionary<int, BotQuestState>(homeQuests) { [2042] = new(2042, 3, 0, 0, null) } }, returned),
			() => VerifyHome(home, returned with { Discarded = [] }),
			() => VerifyHome(Adding(home, new NaturalJourneyItem(920003, 167000465, 1, 65535)), returned),
			() => VerifyHome(home, returned with { Payments = [.. paid, new(2947, 301_641, 31_320, 25)], ExperienceGained = 333_979 + fights + 301_641 }),
			() => VerifyHome(home with { Kinah = 739_158 }, returned),
			// The old staff still in the hand with the better one in the cube.
			() => VerifyHome(home with { Inventory = [.. homeItems.Where(item => item.ItemId is not (101501357 or 101501224)),
				new(156530, 101501357, 1, 3), new(920001, 101501224, 1, 65535)] }, returned),
			() => VerifyHome(home, returned with { Attempts = [] }),
			// Nothing may be discarded before Q2947's reward.
			() => VerifyCleared(reported, fought with { Discarded = [new(920002, 167000465, 1, 65535)] }),
		];
		Assert.All(refusedHome, verify => Assert.Throws<InvalidDataException>(verify));

		// AX-10: the missions done. One ring-course try lost and one flown; Q2042 turned in for 301,641 XP; its Bronze Coin
		// Chest opened; the one supplied scroll used, and Q2042's own ten scrolls in the cube.
		NaturalAbyssPayment[] paidAll = [.. paidHome, new(2042, 301_641, 0, 26)];
		NaturalAbyssAttempt[] flights = [new("ring-course", 1, "timeout", 1_300_000, 1_371_500, 0, "ran out"), new("ring-course", 2, "done", 1_373_000, 1_418_000, 6, "flown")];
		NaturalOpenedContainer secondChest = new(188050873, 930001, new Dictionary<int, long> { [186000007] = 5 });
		NaturalJourneyItem[] endItems = [.. homeItems.Where(item => item.ItemId != 186000007), new(157702, 186000007, 18, 65535), new(930002, 164000079, 10, 65535)];
		var endQuests = new Dictionary<int, BotQuestState>(homeQuests);
		endQuests.Remove(2042);
		NaturalAltgardObservation ended = home with
		{
			Level = 26, Quests = endQuests, CompletedQuestIds = home.CompletedQuestIds.Append(2042).ToHashSet(), Inventory = endItems,
			ItemCounts = endItems.GroupBy(item => item.ItemId).ToDictionary(group => group.Key, group => group.Sum(item => item.Count)),
		};
		NaturalAbyssLedger flown = returned with
		{
			ExperienceGained = returned.ExperienceGained + 301_641, Payments = paidAll, InventoryChecks = 6, Opened = [.. opened, secondChest],
			Attempts = [.. tries, .. flights], ScrollsSupplied = 1, ScrollsUsed = 1,
		};
		NaturalAbyssEntryProgress VerifyEnd(NaturalAltgardObservation state, NaturalAbyssLedger counted) =>
			NaturalAbyssEntryLeg.VerifyProgress(Leg, start, state, "coin-armor-26", counted, HomeBoost, DefenceOf, 1_500_000);
		NaturalAbyssEntryProgress atEnd = VerifyEnd(ended, flown);
		Assert.Equal(("coin-armor-26", 26, 18L, 1L, 1L), (atEnd.Frontier, atEnd.Level, atEnd.BronzeCoins, atEnd.ScrollsSupplied, atEnd.ScrollsUsed));
		Assert.Equal([24020, 2945, 2946, 2947, 2042], atEnd.CompletedLegQuestIds);
		Assert.Empty(atEnd.StartedQuestIds);
		Assert.Equal(["timeout", "done"], atEnd.Attempts!.Where(attempt => attempt.Kind == "ring-course").Select(attempt => attempt.Outcome));
		Action[] refusedEnd =
		[
			() => VerifyEnd(home, flown),
			() => VerifyEnd(ended with { Level = 25 }, flown),
			() => VerifyEnd(ended, flown with { Attempts = [.. tries] }),
			() => VerifyEnd(ended, flown with { Attempts = [.. tries, flights[0]] }),
			() => VerifyEnd(ended, flown with { Attempts = [.. tries, flights[1] with { Number = 1, Progress = 5 }] }),
			() => VerifyEnd(ended, flown with { Attempts = [.. tries, flights[0], flights[0] with { Number = 2 }, flights[0] with { Number = 3 }, flights[1] with { Number = 4 }] }),
			() => VerifyEnd(ended, flown with { ScrollsUsed = 0 }),
			() => VerifyEnd(ended, flown with { ScrollsSupplied = 2 }),
			() => VerifyEnd(ended, flown with { Opened = [.. opened] }),
			() => VerifyEnd(ended with { Quests = new Dictionary<int, BotQuestState>(endQuests) { [2042] = new(2042, 4, 8, 0, null) } }, flown),
			() => VerifyEnd(ended, flown with { Payments = [.. paidHome, new(2042, 301_640, 0, 26)], ExperienceGained = flown.ExperienceGained - 1 }),
			// Before Yornduf's talk no flight and no scroll may be on the ledger.
			() => VerifyHome(home, returned with { ScrollsUsed = 1 }),
			() => VerifyHome(home, returned with { Attempts = [.. tries, flights[1] with { Number = 1 }] }),
		];
		Assert.All(refusedEnd, verify => Assert.Throws<InvalidDataException>(verify));

		// AX-12: the endpoint. The level-26 manifest decided once from the 18 coins held: four pieces for 31, 13 supplied. The
		// four are worn, the Rank 8 gloves and brogans are back in the cube and no coin is left.
		NaturalAbyssCoinManifest late = NaturalAbyssCoinArmorPolicy.Plan(Scope.CoinArmor, Scope.CoinArmor.Tiers.Single(tier => tier.Level == 26), ended.Inventory!, DefenceOf);
		Assert.Equal((31, 18L, 13L), (late.Cost, late.CoinsOwned, late.CoinsToSupply));
		long purse = ended.Kinah;
		NaturalAbyssCoinPurchase[] boughtLate =
		[
			new(26, 111501073, 940016, 16, 7, 31, 24, purse, purse), new(26, 112501023, 942048, 2048, 7, 24, 17, purse, purse),
			new(26, 113501082, 944096, 4096, 10, 17, 7, purse, purse), new(26, 114501089, 940032, 32, 7, 7, 0, purse, purse),
		];
		NaturalAltgardObservation dressed = Dressed(ended);
		NaturalAbyssLedger settled = flown with
		{
			OtherInventoryChecks = flown.OtherInventoryChecks + 1, CoinManifests = [.. flown.CoinManifests, late],
			CoinPurchases = [.. flown.CoinPurchases, .. boughtLate], CoinsSupplied = 13,
		};
		NaturalAbyssEntryProgress VerifyEndpoint(NaturalAltgardObservation state, NaturalAbyssLedger counted) =>
			NaturalAbyssEntryLeg.VerifyProgress(Leg, start, state, "endpoint", counted, HomeBoost, DefenceOf, 1_600_000);
		NaturalAbyssEntryProgress atEndpoint = VerifyEndpoint(dressed, settled);
		Assert.Equal(("endpoint", 26, 0L, 13L), (atEndpoint.Frontier, atEndpoint.Level, atEndpoint.BronzeCoins, atEndpoint.CoinsSupplied));
		Assert.Equal([21, 26], atEndpoint.CoinManifests!.Select(manifest => manifest.Level));
		Assert.Equal([111501066, 114501082, 111501073, 112501023, 113501082, 114501089], atEndpoint.CoinPurchases!.Select(purchase => purchase.ItemId));
		Assert.Equal(110551147, atEndpoint.TorsoItemId);
		Action[] refusedEndpoint =
		[
			// The endpoint needs the level-26 manifest, and the level-26 manifest needs the missions done first.
			() => VerifyEndpoint(ended, flown),
			() => VerifyEnd(dressed, settled),
			() => VerifyEndpoint(dressed with { Level = 25 }, settled),
			() => VerifyEndpoint(dressed, settled with { CoinManifests = [.. flown.CoinManifests, late, late] }),
			() => VerifyEndpoint(dressed, settled with { CoinPurchases = [.. flown.CoinPurchases, .. boughtLate[..3]] }),
			() => VerifyEndpoint(dressed, settled with { CoinsSupplied = 12 }),
			() => VerifyEndpoint(dressed, settled with { CoinPurchases = [.. flown.CoinPurchases, .. boughtLate[..3], boughtLate[3] with { KinahAfter = purse - 1 }] }),
			// A coin over, as if more had been supplied than the manifest was short.
			() => VerifyEndpoint(Adding(dressed, new NaturalJourneyItem(157702, 186000007, 1, 65535)), settled),
			// A bought piece left in the cube, with the Rank 8 piece still on.
			() => VerifyEndpoint(dressed with { Inventory = [.. dressed.Inventory!.Select(item => item.ItemId switch
				{
					111501073 => item with { EquipmentSlot = 65535 }, 111501066 => item with { EquipmentSlot = 16 }, _ => item,
				})] }, settled),
			// Another object of the same piece worn in place of the one bought.
			() => VerifyEndpoint(dressed with { Inventory = [.. dressed.Inventory!.Select(item => item.ItemId == 114501089 ? item with { ObjectId = 949999 } : item)] }, settled),
		];
		Assert.All(refusedEndpoint, verify => Assert.Throws<InvalidDataException>(verify));

		Action[] refused =
		[
			() => Verify(atGarm with { MapId = NaturalAbyssEntry.Morheim }, ledger),
			() => Verify(atGarm with { Quests = new Dictionary<int, BotQuestState>(quests) { [2947] = new(2947, 3, 0, 0, null) } }, ledger),
			() => Verify(atGarm with { Quests = new Dictionary<int, BotQuestState>(quests) { [2947] = new(2947, 3, 5, 0, null) } }, ledger),
			() => Verify(atGarm with { CompletedQuestIds = worn.CompletedQuestIds.Append(2945).ToHashSet() }, ledger),
			() => Verify(atGarm, ledger with { Payments = [paid[0], paid[2], paid[1]] }),
			() => Verify(atGarm, ledger with { Payments = [paid[0], paid[1] with { Experience = 20_111 }, paid[2]], ExperienceGained = 333_980 }),
			() => Verify(atGarm, ledger with { InventoryChecks = 3 }),
			() => Verify(atGarm, ledger with { Fares = 2_401 }),
			// A chest's ten coins unaccounted for, a container the server refused, and a sack left closed.
			() => Verify(atGarm, ledger with { Opened = [opened[0], opened[1]] }),
			() => Verify(atGarm, ledger with { NotOpened = 1 }),
			() => Verify(Adding(atGarm, new NaturalJourneyItem(910020, 188051192, 1, 65535)), ledger),
			() => Verify(atGarm, ledger with { Opened = [.. opened, new(188053787, 156843, new Dictionary<int, long> { [1] = 1 })] }),
		];
		Assert.All(refused, verify => Assert.Throws<InvalidDataException>(verify));
	}

	/// <summary>AX-09: after the reward Q2042 is in the journal; its first talk with Aegir is the last step before the course.</summary>
	[Fact]
	public void ReturnToMorheimTakesTheRewardAndTheLastMission()
	{
		NaturalAltgardObservation done = CoinArmorWornState();
		var journal = new Dictionary<int, BotQuestState>(done.Quests);
		journal.Remove(2945);
		NaturalAbyssEntryDecision Decide(int map, params BotQuestState[] quests)
		{
			var held = new Dictionary<int, BotQuestState>(journal);
			foreach (BotQuestState quest in quests) held[quest.QuestId] = quest;
			return NaturalAbyssEntryDecisionEngine.Decide(Leg, done with
			{
				MapId = map, Quests = held, CompletedQuestIds = done.CompletedQuestIds.Concat([2945, 2946, 2947]).ToHashSet(),
			}, 1, DefenceOf);
		}
		static (string, string, string?) Shape(NaturalAbyssEntryDecision next) => (next.Phase, next.Action, next.StepKey);
		// Q2947 turned in: Q2042 shows a moment later, then Aegir's var 0, then the course is Yornduf's.
		Assert.Equal(("morheim-return", "refresh-observation", null), Shape(Decide(NaturalAbyssEntry.Morheim)));
		Assert.Equal(("morheim-return", "talk", "q2042-aegir"), Shape(Decide(NaturalAbyssEntry.Morheim, new BotQuestState(2042, 3, 0, 0, null))));
		Assert.Equal(("ring-course", "ring-course-start", "q2042-yornduf-start"), Shape(Decide(NaturalAbyssEntry.Morheim, new BotQuestState(2042, 3, 1, 0, null))));
		// From the capital the first talk asks for Doman's teleport.
		Assert.Equal("travel", Decide(NaturalAbyssEntry.Pandaemonium, new BotQuestState(2042, 3, 0, 0, null)).Action);
		// The staff rule picks the same reward as the contract: the usable staff with the most magic boost on offer.
		NaturalAltgardRewardChoice choice = Leg.RewardChoiceList.Single(entry => entry.QuestId == 2947);
		Assert.Equal(("SELECTED_QUEST_REWARD2", 101501224, "STAFF"), (choice.Action, choice.ItemId, choice.ItemGroup));
	}

	/// <summary>AX-10: Yornduf's ring course, from his talk to Aegir's reward, with the three tries of AX-Q3.</summary>
	[Fact]
	public void RingCourseIsFlownWithinThreeTriesAndThenAsksForARecordedFlight()
	{
		NaturalAltgardObservation done = CoinArmorWornState();
		var journal = new Dictionary<int, BotQuestState>(done.Quests);
		journal.Remove(2945);
		NaturalAbyssEntryDecision Decide(byte status, int var, int map = NaturalAbyssEntry.Morheim, bool complete = false, params NaturalAbyssAttempt[] tries)
		{
			var held = new Dictionary<int, BotQuestState>(journal);
			if (!complete) held[2042] = new(2042, status, var, 0, null);
			int[] finished = complete ? [2945, 2946, 2947, 2042] : [2945, 2946, 2947];
			// Q2042's reward is what takes the Cleric to level 26.
			return NaturalAbyssEntryDecisionEngine.Decide(Leg, done with { MapId = map, Level = complete ? 26 : 25, Quests = held,
				CompletedQuestIds = done.CompletedQuestIds.Concat(finished).ToHashSet() },
				1, DefenceOf, tries);
		}
		static (string, string, string?) Shape(NaturalAbyssEntryDecision next) => (next.Phase, next.Action, next.StepKey);
		NaturalAbyssAttempt Failed(int number) => new("ring-course", number, "timeout", number * 1_000, number * 1_000 + 70_000, 3, "ran out");

		// Yornduf's talk starts the clock; while rings are left the rule asks for the flight and names the next ring.
		Assert.Equal(("ring-course", "ring-course-start", "q2042-yornduf-start"), Shape(Decide(3, 1)));
		Assert.All(Enumerable.Range(2, 6), var =>
		{
			NaturalAbyssEntryDecision fly = Decide(3, var);
			Assert.Equal(("ring-course", "ring-course-fly", null), Shape(fly));
			Assert.Contains($"Ring {var - 1} of 6", fly.Reason);
		});
		// The sixth ring passed: Yornduf, then Aegir's reward, then the leg's missions are done.
		Assert.Equal(("ring-course", "talk", "q2042-yornduf-done"), Shape(Decide(3, 8)));
		Assert.Equal(("ring-course", "talk", "q2042-reward"), Shape(Decide(4, 8)));
		// AX-11: the missions done at level 26 lead to the coin armor; below 26 the leg stops as a finding and never hunts.
		// AX-12: the level-26 pieces are bought in Morheim, put on if they are owned, and then the leg is at its endpoint.
		NaturalAbyssEntryDecision buy = Decide(0, 0, complete: true);
		Assert.Equal(("coin-armor-26", "coin-armor", null), Shape(buy));
		Assert.Contains("111501073, 112501023, 113501082, 114501089 for 31 coins", buy.Reason);
		Assert.Equal(("coin-armor-26", "blocked", null), Shape(Decide(0, 0, NaturalAbyssEntry.Pandaemonium, complete: true)));
		NaturalAltgardObservation finished = done with
		{
			Level = 26, Quests = journal, CompletedQuestIds = done.CompletedQuestIds.Concat([2945, 2946, 2947, 2042]).ToHashSet(),
		};
		NaturalAbyssEntryDecision After(NaturalAltgardObservation state) => NaturalAbyssEntryDecisionEngine.Decide(Leg, state, 1, DefenceOf);
		Assert.Equal(("coin-armor-26", "inventory-check", null), Shape(After(Adding(finished, new NaturalJourneyItem(940016, 111501073, 1, 65535)))));
		Assert.Equal(("endpoint", "frontier", null), Shape(After(Dressed(finished))));
		Assert.Equal(("level-by-quests", "blocked", null), Shape(After(Dressed(finished) with { Level = 25 })));
		NaturalAbyssEntryDecision low = NaturalAbyssEntryDecisionEngine.Decide(Leg, done with
		{
			Level = 25, Quests = journal, CompletedQuestIds = done.CompletedQuestIds.Concat([2945, 2946, 2947, 2042]).ToHashSet(),
		}, 1, DefenceOf);
		Assert.Equal(("level-by-quests", "blocked"), (low.Phase, low.Action));
		Assert.Contains("fortress quests", low.Reason);
		Assert.Contains("No hunting", low.Reason);

		// A failed try (var 9: the timer, a death or a world entry) is Yornduf's second talk, three tries in all (AX-Q3).
		Assert.Equal(("ring-course", "ring-course-start", "q2042-yornduf-again"), Shape(Decide(3, 9, tries: Failed(1))));
		Assert.Equal("ring-course-start", Decide(3, 9, tries: [Failed(1), Failed(2)]).Action);
		NaturalAbyssEntryDecision ask = Decide(3, 9, tries: [Failed(1), Failed(2), Failed(3)]);
		Assert.Equal(("ring-course", "blocked"), (ask.Phase, ask.Action));
		Assert.Contains("ask the operator to record a flight", ask.Reason);
		// Yornduf stands in Morheim: from anywhere else the talk is a teleport away.
		Assert.Equal("travel", Decide(3, 9, NaturalAbyssEntry.Pandaemonium, tries: Failed(1)).Action);
		Assert.Equal(("recover", "revive"), (NaturalAbyssEntryDecisionEngine.Decide(Leg, done with { IsDead = true }, 1, DefenceOf).Phase,
			NaturalAbyssEntryDecisionEngine.Decide(Leg, done with { IsDead = true }, 1, DefenceOf).Action));
	}

	/// <summary>AX-08: Garm's arena, from his first talk to the report, with the three tries of AX-Q3.</summary>
	[Fact]
	public void ArenaIsFoughtOneKillAtATimeWithinThreeTries()
	{
		NaturalAltgardObservation capital = CoinArmorWornState() with { MapId = NaturalAbyssEntry.Pandaemonium, Kinah = 741_276 };
		var journal = new Dictionary<int, BotQuestState>(capital.Quests);
		journal.Remove(2945);
		NaturalAbyssEntryDecision Decide(int map, byte status, int var, int kills = 0, bool dead = false, params NaturalAbyssAttempt[] tries) =>
			NaturalAbyssEntryDecisionEngine.Decide(Leg, capital with
			{
				MapId = map, IsDead = dead, CompletedQuestIds = capital.CompletedQuestIds.Concat([2945, 2946]).ToHashSet(),
				Quests = new Dictionary<int, BotQuestState>(journal) { [2947] = new(2947, status, var | kills << 24, 0, null) },
			}, 1, DefenceOf, tries);
		static (string, string, string?) Shape(NaturalAbyssEntryDecision next) => (next.Phase, next.Action, next.StepKey);
		const int city = NaturalAbyssEntry.Pandaemonium, arena = NaturalAbyssEntry.ArenaMap, morheim = NaturalAbyssEntry.Morheim;
		NaturalAbyssAttempt Failed(int number) => new("arena", number, "timeout", number * 1_000, number * 1_000 + 240_000, 6, "ran out");

		// Garm's first talk sends the Cleric in (D35); inside, one kill per decision until ten are counted.
		Assert.Equal(("arena", "talk", "q2947-garm-start"), Shape(Decide(city, 3, 4)));
		Assert.Equal(("arena", "arena-fight", null), Shape(Decide(arena, 3, 5)));
		Assert.Equal(("arena", "arena-fight", null), Shape(Decide(arena, 3, 5, kills: 9)));
		Assert.Contains("9 of 10", Decide(arena, 3, 5, kills: 9).Reason);
		// Ten counted: out (movie 168's teleport, or the exit), then the report to Garm, then Aegir in Morheim.
		Assert.Equal(("arena", "arena-leave", null), Shape(Decide(arena, 3, 5, kills: 10)));
		Assert.Equal(("arena", "talk", "q2947-garm-done"), Shape(Decide(city, 3, 5, kills: 10)));
		// AX-09: the reward is Aegir's, in Morheim: Doman's teleport first, then the reward talk.
		NaturalAbyssEntryDecision home = Decide(city, 4, 7, kills: 10);
		Assert.Equal(("morheim-return", "travel", (int?)morheim), (home.Phase, home.Action, home.MapId));
		Assert.Equal(("morheim-return", "talk", "q2947-reward"), Shape(Decide(morheim, 4, 7, kills: 10)));
		Assert.Equal(10, NaturalAbyssEntryDecisionEngine.ArenaKills(Scope.Arena, new(2947, 3, 5 | 10 << 24, 0, null)));

		// A failed try (the timer, or a death, D36) is var 6: Garm again, from wherever the Cleric revived.
		Assert.Equal(("arena", "talk", "q2947-garm-again"), Shape(Decide(city, 3, 6, tries: Failed(1))));
		Assert.Equal(("recover", "revive", null), Shape(Decide(arena, 3, 6, dead: true, tries: Failed(1))));
		NaturalAbyssEntryDecision back = Decide(morheim, 3, 6, tries: Failed(1));
		Assert.Equal(("arena", "travel", (int?)city), (back.Phase, back.Action, back.MapId));
		// Revived in place after a death: nothing counts at var 6, so the way on is the exit.
		Assert.Equal(("arena", "arena-leave", null), Shape(Decide(arena, 3, 6, tries: Failed(1))));
		// Three tries (AX-Q3). After the third failure the leg stops as a finding.
		Assert.Equal("talk", Decide(city, 3, 6, tries: [Failed(1), Failed(2)]).Action);
		NaturalAbyssEntryDecision stop = Decide(city, 3, 6, tries: [Failed(1), Failed(2), Failed(3)]);
		Assert.Equal(("arena", "blocked"), (stop.Phase, stop.Action));
		Assert.Contains("All 3 arena attempts failed", stop.Reason);
	}

	private static NaturalAltgardObservation Adding(NaturalAltgardObservation state, params NaturalJourneyItem[] added)
	{
		NaturalJourneyItem[] items = [.. state.Inventory!, .. added];
		return state with { Inventory = items, ItemCounts = items.GroupBy(item => item.ItemId).ToDictionary(group => group.Key, group => group.Sum(item => item.Count)) };
	}

	/// <summary>After Vebna's second sale: the four Elite Rank 7 pieces worn, the pieces they replace in the cube, no coin left.</summary>
	private static NaturalAltgardObservation Dressed(NaturalAltgardObservation state)
	{
		int[] replaced = [111501066, 112501641, 113501720, 114501082];
		NaturalJourneyItem[] items = [.. state.Inventory!.Where(item => item.ItemId != 186000007)
			.Select(item => replaced.Contains(item.ItemId) ? item with { EquipmentSlot = 65535 } : item),
			new(940016, 111501073, 1, 16), new(942048, 112501023, 1, 2048), new(944096, 113501082, 1, 4096), new(940032, 114501089, 1, 32)];
		return state with { Inventory = items, ItemCounts = items.GroupBy(item => item.ItemId).ToDictionary(group => group.Key, group => group.Sum(item => item.Count)) };
	}

	/// <summary>On the Morheim landing, bound at the fortress obelisk, with Q24020 started by the arrival.</summary>
	private static NaturalAltgardObservation ArrivedState() => StartState() with
	{
		MapId = NaturalAbyssEntry.Morheim, Position = new BotPosition(309.53f, 2271.51f, 449.41f, 0), Kinah = 743_394,
		Bind = new BotBindPoint(NaturalAbyssEntry.Morheim, new BotPosition(270.52f, 2338.21f, 443.74f, 0), 0),
		Quests = new Dictionary<int, BotQuestState> { [2945] = new(2945, 3, 0, 0, null), [24020] = new(24020, 3, 0, 0, null) },
	};

	/// <summary>After Aegir's talk: Q24020 complete, its hauberk worn over the armor AX-04's check put on, and the six locked
	/// campaign quests in the journal.</summary>
	private static NaturalAltgardObservation CommanderDoneState()
	{
		NaturalAltgardObservation arrived = ArrivedState();
		NaturalJourneyItem[] inventory = [.. arrived.Inventory!.Where(item => item.ItemId is not (182400001 or 110551139)),
			new(133277, 182400001, 743_394, 65535), new(140185, 110551139, 1, 65535), new(133316, 110551147, 1, 8),
			new(159156, 111501081, 1, 16), new(157353, 112501641, 1, 2048), new(159160, 113501720, 1, 4096), new(140186, 114501726, 1, 32)];
		// The client keeps the completed Q24020 in its quest list at status 5.
		var quests = new Dictionary<int, BotQuestState> { [2945] = new(2945, 3, 0, 0, null), [24020] = new(24020, 5, 0, 1, null) };
		foreach (int locked in new[] { 24021, 24022, 24023, 24024, 24025, 24026 }) quests[locked] = new(locked, 6, 0, 0, null);
		return arrived with
		{
			Position = new BotPosition(225.225f, 2415.47f, 454.11f, 46), Quests = quests, Inventory = inventory,
			CompletedQuestIds = arrived.CompletedQuestIds.Append(24020).ToHashSet(),
			ItemCounts = inventory.GroupBy(item => item.ItemId).ToDictionary(group => group.Key, group => group.Sum(item => item.Count)),
		};
	}

	/// <summary>After Vebna: the Rank 8 gloves and brogans worn, the old ones in the cube, three of the seven coins left.</summary>
	private static NaturalAltgardObservation CoinArmorWornState()
	{
		NaturalAltgardObservation done = CommanderDoneState();
		NaturalJourneyItem[] inventory = [.. done.Inventory!.Where(item => item.ItemId is not (186000007 or 111501081 or 114501726)),
			new(157702, 186000007, 3, 65535), new(159156, 111501081, 1, 65535), new(140186, 114501726, 1, 65535),
			new(900016, 111501066, 1, 16), new(900032, 114501082, 1, 32)];
		return done with
		{
			Position = new BotPosition(220.57f, 2333.51f, 446.32f, 0), Inventory = inventory,
			ItemCounts = inventory.GroupBy(item => item.ItemId).ToDictionary(group => group.Key, group => group.Sum(item => item.Count)),
		};
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
