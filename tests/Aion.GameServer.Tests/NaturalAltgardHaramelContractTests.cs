using System.Text.Json;
using System.Xml.Linq;
using Aion.Bots.Protocol;
using Aion.Bots.Scenarios;
using Aion.Bots.World;
using Aion.GameServer.Model;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.TestKit;

namespace Aion.GameServer.Tests;

public sealed class NaturalAltgardHaramelContractTests
{
	private static readonly NaturalAltgardContract Leg = NaturalAltgardContract.LoadLeg("l12");
	private static readonly NaturalHaramel Rules = Leg.Haramel!;
	private static readonly IReadOnlyDictionary<int, QuestRunPlan> Plans = NaturalAltgardContract.LoadPlans("l12");
	private static readonly IReadOnlyDictionary<int, NaturalTemplateObjective> Objectives = NaturalTemplateObjective.From(Plans);
	public static bool AssertAuditedWireContract(Type type)
	{
		if (type != typeof(SM_INSTANCE_INFO)) return false;
		new NaturalAltgardHaramelContractTests().InstanceInfoDecodesActualSelfAndTeamCountsAndResetOrMergeSemantics();
		return true;
	}

	[Fact]
	public void ContractPinsEightPlansThreeCustomProtocolsAndTheRetainedLedger()
	{
		Assert.Equal(8, Plans.Count);
		Assert.Equal(11, Leg.Order.Length);
		Assert.Equal(145, Leg.Start.CompletedQuestIds.Length);
		Assert.Contains(2293, Leg.Start.CompletedQuestIds);
		Assert.Equal(958587, Leg.Quests.Sum(q => q.RewardExperience));
		Assert.Equal([28500,28510,28511], Leg.Quests.Where(q => !q.IsTemplate).Select(q => q.Id).Order());
		Assert.Equal([112501641,113501720,123001440], Leg.RewardChoiceList.Select(c => c.ItemId).Order());
		Assert.Equal((16,9,600000,60000,74), (Rules.MaxEntries,Rules.ResetHour,Rules.EmptyExpiryMillis,Rules.CleanupPeriodMillis,Rules.KillSpawnCount));
		Assert.False(Leg.Bind!.OnArrival);
		Assert.Equal("altgard-haramel-l12", Leg.Endpoint.Snapshot);
		Assert.Equal(700067, Rules.WorkingBindNpcId);
		Assert.Contains(700832, Leg.GraphNpcIds(Plans));
		Assert.Contains(730321, Leg.GraphNpcIds(Plans));
		Assert.DoesNotContain(28502, Leg.Order);
	}

	[Theory]
	[InlineData(2945, 24, 3, 0, 0, false)]
	[InlineData(2945, 25, 3, 0, 0, true)]
	[InlineData(2945, 25, 3, 1, 0, false)]
	[InlineData(2945, 25, 4, 0, 0, false)]
	[InlineData(2945, 25, 3, 0, 1, false)]
	[InlineData(2946, 25, 3, 0, 0, false)]
	public void DeferredCampaignAllowsOnlyItsActualLevelUnlock(int id, int level, byte status, int flags, byte count, bool preserved)
	{
		Assert.Equal(preserved, NaturalHaramelDecisionEngine.PreservesDeferredQuest(id, (6, 0), new(id, status, flags, count, null), level));
	}

	[Fact]
	public void TowerSuppliesMatchTheRealChestRequirementsAndAttainableDrops()
	{
		string data = Path.Combine(RealStaticData.RepoRoot(), "game-server/data/static_data");
		XElement chest = XDocument.Load(Path.Combine(data, "chests/chest_templates.xml")).Descendants("chest")
			.Single(e => (int?)e.Attribute("npc_id") == 700853);
		Assert.Equal(chest.Elements("key_item").Select(e => (Item: int.Parse(e.Attribute("item_ids")!.Value), Count: (int)e.Attribute("count")!)),
			Rules.TowerChestKeys!.Select(k => (Item: k.ItemId, k.Count)));
		XDocument drops = XDocument.Load(Path.Combine(data, "global_drops/rules/instances/rules_map_haramel.xml"));
		XDocument spawns = XDocument.Load(Path.Combine(data, "spawns/Instances/300200000_Haramel.xml"));
		foreach (NaturalHaramelKey key in Rules.TowerChestKeys!)
		{
			XElement rule = drops.Descendants("gd_rule").Single(e => e.Descendants("gd_item").Any(i => (int?)i.Attribute("id") == key.ItemId));
			Assert.Equal(100, (int)rule.Attribute("chance")!);
			Assert.Null(rule.Attribute("level_based_chance_reduction"));
			Assert.Equal(key.NpcId, (int)rule.Descendants("gd_npc").Single().Attribute("npc_id")!);
			Assert.True(spawns.Descendants("spawn").Single(e => (int?)e.Attribute("npc_id") == key.NpcId).Elements("spot").Count() >= key.Count);
		}
	}

	[Theory]
	[InlineData(63,false)]
	[InlineData(64,false)]
	[InlineData(65,true)]
	public void SixtyFiveKillsUseBothVarsAndExcludeWireFlags(int count, bool done)
	{
		var quest = new BotQuestState(28504,3,count | 0x7F << 24,0,null);
		Assert.Equal(count, NaturalQuestProgress.KillCount(quest,0,65));
		Assert.Equal(done, Objectives[28504].IsDone(quest, new Dictionary<int,long>()));
		Assert.Throws<InvalidDataException>(() => NaturalQuestProgress.KillCount(quest,3,65));
	}

	[Fact]
	public void LoyaltyAndOverseersNeedEveryCollectionWhileRetreatNeedsAllThreeCounters()
	{
		var items = new Dictionary<int,long> { [182212013]=5, [182212014]=4 };
		Assert.False(Objectives[28501].IsDone(new(28501,3,0,0,null),items));
		items[182212014]=5;
		Assert.True(Objectives[28501].IsDone(new(28501,3,0,0,null),items));
		Assert.False(Objectives[28505].IsDone(new(28505,3,0,0,null),new Dictionary<int,long> { [182212017]=1, [182212018]=1 }));
		Assert.False(Objectives[28508].IsDone(new(28508,3,2 | 3 << 6 | 2 << 12,0,null),items));
		Assert.True(Objectives[28508].IsDone(new(28508,3,2 | 3 << 6 | 3 << 12,0,null),items));
	}

	[Fact]
	public void FreshJournalSkipsAcceptAndRewardAndResumesMovieAcknowledgement()
	{
		NaturalAltgardObservation start=State();
		Assert.Equal("q28500-accept",Decide(start).StepKey);
		Assert.Equal("q28500-gulkalla",Decide(Quest(start,28500,3,0)).StepKey);
		Assert.Equal("haramel-movie",Decide(Quest(start,28500,3,3)).Action);
		NaturalAltgardObservation inside=State(Rules.MapId) with { HaramelProgress=Progress().ObserveEntry(2,100,Entry(1),1000) };
		inside=Done(inside,28500);
		Assert.Equal(("template-accept",28501), (Decide(inside).Action,Decide(inside).QuestId));
		inside=Quest(inside,28501,3,0);
		Assert.Equal(28508,Decide(inside).QuestId);
		inside=Quest(inside,28508,3,0);
		Assert.Equal(("template-work",28501),(Decide(inside).Action,Decide(inside).QuestId));
	}

	[Fact]
	public void OrdinaryExitWaitSurvivesReentryAndColdSerializationWithoutFreshCopyAssumptions()
	{
		NaturalHaramelProgress progress=Progress().ObserveEntry(2,100,Entry(1),1000).ObserveRevive(1100).ObserveExit(2000,Rules);
		Assert.Equal(662000,progress.FreshEntryAfterMillis);
		NaturalHaramelProgress cold=JsonSerializer.Deserialize<NaturalHaramelProgress>(JsonSerializer.Serialize(progress))!;
		Assert.Equal(progress.StartedAtMillis,cold.StartedAtMillis);
		Assert.Equal(1,cold.Revives);
		NaturalAltgardObservation between=PostBoss(State()) with { NowMillis=661999,HaramelProgress=cold };
		Assert.Equal("wait-haramel-expiry",Decide(between).Action);
		Assert.Equal("enter-haramel",Decide(between with { NowMillis=662000 }).Action);
		NaturalHaramelProgress same=cold.ObserveEntry(2,100,Entry(1),662001,postBossQuests:true);
		Assert.Single(same.Visits);
		Assert.False(same.CurrentVisit!.PostBossQuests);
		Assert.Equal("leave-haramel",Decide(between with { MapId=Rules.MapId,NowMillis=662001,HaramelProgress=same,InstanceId=2 }).Action);
		NaturalHaramelProgress fresh=cold.ObserveEntry(3,200,Entry(2),662001,postBossQuests:true,freshSpawnsObserved:true);
		Assert.Equal(2,fresh.Visits.Length);
		Assert.Equal(1,fresh.Revives);
		Assert.Equal("q28511-accept",Decide(between with { MapId=Rules.MapId,NowMillis=662001,HaramelProgress=fresh,InstanceId=3 }).StepKey);
		Assert.Throws<InvalidDataException>(() => cold.ObserveEntry(2,100,Entry(2),662001));
		Assert.Throws<InvalidDataException>(() => cold.ObserveExit(1999,Rules));
		NaturalHaramelProgress earlyRecovery=cold.ObserveEntry(3,300,Entry(2),662001,freshSpawnsObserved:true);
		Assert.Equal(1,earlyRecovery.RecoveryVisits);
		Assert.DoesNotContain(earlyRecovery.Visits,v=>v.PostBossQuests);
		NaturalHaramelProgress recycledAnchor=cold.ObserveEntry(3,100,Entry(2),662001,postBossQuests:true,freshSpawnsObserved:true);
		Assert.Equal(2,recycledAnchor.Visits.Length);
		Assert.Equal(3,recycledAnchor.CurrentVisit!.InstanceId);
		Assert.Throws<InvalidDataException>(()=>cold.ObserveEntry(3,100,Entry(2),662001));
		NaturalHaramelProgress restarted=(cold with { NeedsInstanceObservation=true }).ObserveEntry(2,100,Entry(1),662001,
			postBossQuests:true,freshSpawnsObserved:true);
		Assert.Equal(2,restarted.Visits.Length);
		Assert.Equal(1,restarted.Revives);
	}

	[Fact]
	public void ExhaustedPartialCopiesWaitForTheirRetainedExpiryBeforeRecoveryEntry()
	{
		NaturalHaramelProgress first = Progress().ObserveEntry(2,100,Entry(1),1000).ObserveExit(2000,Rules);
		NaturalAltgardObservation partial = Quest(State(),28500,3,4) with { HaramelProgress=first,NowMillis=661999 };
		Assert.Equal("wait-haramel-expiry",Decide(partial).Action);
		Assert.Equal("enter-haramel",Decide(partial with { NowMillis=662000 }).Action);
		NaturalHaramelProgress second = first.ObserveEntry(3,200,Entry(2),662001,postBossQuests:true,freshSpawnsObserved:true)
			.ObserveExit(663000,Rules);
		partial = Quest(PostBoss(State()),28511,3,0) with { HaramelProgress=second,NowMillis=1322999 };
		Assert.Equal("wait-haramel-expiry",Decide(partial).Action);
		Assert.Equal("enter-haramel",Decide(partial with { NowMillis=1323000 }).Action);
	}

	[Fact]
	public void SoupPaymentAndOwnedSoupPreventDuplicateIngredientCheckOrGive()
	{
		NaturalHaramelProgress progress=Progress().ObserveEntry(3,200,Entry(2),1000,postBossQuests:true,freshSpawnsObserved:true)
			.ObserveSoupPayment(555,5,0,1352,1100);
		NaturalAltgardObservation state=PostBoss(State(Rules.MapId)) with { HaramelProgress=progress,NowMillis=1200,InstanceId=3 };
		state=Done(state,28505); state=Quest(state,28510,4,6); state=Quest(state,28511,3,0);
		Assert.Equal("haramel-soup",Decide(state).Action);
		Assert.Throws<InvalidDataException>(() => progress.ObserveSoupPayment(555,5,0,1352,1200));
		var items=new Dictionary<int,long>(state.ItemCounts) { [182212023]=1 };
		Assert.Equal("refresh-observation",Decide(state with { ItemCounts=items }).Action);
		state=Quest(state,28511,4,1) with { ItemCounts=items };
		Assert.Equal(("template-work",28504),(Decide(state).Action,Decide(state).QuestId));
	}

	[Fact]
	public void EndpointKeepsStaffCurrenciesCleanupAndFinalBind()
	{
		NaturalAltgardObservation state=State();
		foreach (int id in Leg.Order) state=Done(state,id);
		state=state with { ItemCounts=new Dictionary<int,long>(state.ItemCounts) { [186000007]=7 } };
		Assert.Equal("bind",Decide(state).Action);
		state=state with { Position=new(Leg.Hub.Anchor[0],Leg.Hub.Anchor[1],Leg.Hub.Anchor[2],0),
			Bind=new(220030000,new(Leg.Bind!.Position[0],Leg.Bind.Position[1],Leg.Bind.Position[2],0),0) };
		Assert.Equal("leg-complete",Decide(state).Action);
		Assert.Equal("haramel-ledger",Decide(state with { ItemCounts=new Dictionary<int,long>(state.ItemCounts) { [186000006]=18 } }).Action);
		Assert.Equal("haramel-ledger",Decide(state with { ItemCounts=new Dictionary<int,long>(state.ItemCounts) { [182212023]=1 } }).Action);
		Assert.Equal("retained-gear",Decide(state with { Inventory=[] }).Action);
		Assert.Equal("quest-repeat-count",Decide(state with { CompletedQuestCounts=new Dictionary<int,byte>(state.CompletedQuestCounts!) { [28504]=2 } }).Action);
		Assert.Equal("stigma-ledger",Decide(state with { SkillIds=new HashSet<int> { 11504 } }).Action);
	}

	[Fact]
	public void HaramelIdentityRequiresExplicitLegAndAppropriateClassLevel()
	{
		Assert.Equal(NaturalJourneyStage.AscensionCleric,NaturalJourneyIdentityRules.Classify(PlayerClass.CLERIC,24,Rules.MapId,"l12"));
		Assert.Throws<InvalidDataException>(() => NaturalJourneyIdentityRules.Classify(PlayerClass.CLERIC,24,Rules.MapId,"l11"));
		Assert.Throws<InvalidDataException>(() => NaturalJourneyIdentityRules.Classify(PlayerClass.CLERIC,15,Rules.MapId,"l12"));
		Assert.Throws<InvalidDataException>(() => NaturalJourneyIdentityRules.Classify(PlayerClass.CHANTER,24,Rules.MapId,"l12"));
	}

	[Fact]
	public void ProtectedIncomingArmourStaysOwnedAndOnlyChainAccessoriesCanUpgrade()
	{
		var items=Rules.ProtectedItemIds.Select((id,index) => new BotInventoryItem(index+1,id,"",1,4,"",0,false)).ToArray();
		NaturalIshalgenInventoryPolicy policy=NaturalIshalgenInventoryPolicy.Load(RealStaticData.RepoRoot(),Rules.ProtectedItemIds);
		Assert.All(policy.Decide(items,24,80,cleric:true,haramel:Rules).Decisions,d => Assert.Equal("hold",d.Action));
		Assert.True(NaturalHaramel.CanUpgradeGroup("CH_SHOULDER"));
		Assert.True(NaturalHaramel.CanUpgradeGroup("BELT"));
		Assert.False(NaturalHaramel.CanUpgradeGroup("STAFF"));
		Assert.False(NaturalHaramel.CanUpgradeGroup("SHIELD"));
		Assert.False(NaturalHaramel.CanUpgradeGroup("RB_GLOVE"));
	}

	[Fact]
	public void ColdStallBudgetAndRelogReceiptsCannotReset()
	{
		NaturalJourneyCheckpoint checkpoint=new(133297,1,220030000,new(1,2,3,0),24,100,100,100,100,false,[],[2293],[],[],
			new(1,"journey-complete",null,"complete","done",[],[]),HaramelProgress:Progress());
		var first=new NaturalJourneyProgress(TimeSpan.FromMinutes(60));
		first.Observe(checkpoint,TimeSpan.Zero);
		first.Observe(checkpoint,TimeSpan.FromMinutes(59));
		var cold=new NaturalJourneyProgress(TimeSpan.FromMinutes(60),saved:first.State);
		Assert.Throws<TimeoutException>(() => cold.Observe(checkpoint,TimeSpan.FromMinutes(60)));
		Assert.Throws<ArgumentOutOfRangeException>(() => cold.Observe(checkpoint,TimeSpan.Zero));
		NaturalJourneyPersistence.Verify(checkpoint,checkpoint with { ConnectionGeneration=2 });
		Assert.Throws<InvalidDataException>(() => NaturalJourneyPersistence.Verify(checkpoint,checkpoint with
			{ ConnectionGeneration=2,HaramelProgress=Progress() with { StartedAtMillis=1 } }));
		checkpoint=checkpoint with { HaramelProgress=Progress() with { StallBudget=first.State } };
		NaturalJourneyProgressState later=first.State with { LastObserved=TimeSpan.FromMinutes(60) };
		NaturalJourneyPersistence.Verify(checkpoint,checkpoint with { ConnectionGeneration=2,
			HaramelProgress=checkpoint.HaramelProgress with { StallBudget=later } });
		foreach (var changed in new[] { later with { Fingerprint="changed" }, later with { LastProgress=TimeSpan.FromSeconds(1) },
			later with { LastObserved=TimeSpan.FromMinutes(58) } })
			Assert.Throws<InvalidDataException>(() => NaturalJourneyPersistence.Verify(checkpoint,checkpoint with
				{ ConnectionGeneration=2,HaramelProgress=checkpoint.HaramelProgress with { StallBudget=changed } }));
	}

	[Fact]
	public void ColdReceiptUsesFreshLoginAndPreservesFullBeltSlotAndOriginalBudgets()
	{
		BotWorldModel fresh=Login();
		NaturalHaramelProgress saved=NaturalHaramelProgress.Begin(133297,0,fresh,Rules)
			.ObserveEntry(2,100,Entry(1),1000).ObserveRevive(1100).ObserveExit(1200,Rules);
		Assert.Equal(65536,saved.IncomingEquipment.Single(i=>i.ItemId==123001109).Slot);
		string path=Path.Combine(Path.GetTempPath(),"hm01-"+Guid.NewGuid().ToString("N")+".json");
		try
		{
			saved.Write(path);
			NaturalHaramelProgress cold=NaturalHaramelProgress.Read(path,133297,1300,fresh,Leg);
			Assert.Equal((saved.StartedAtMillis,saved.Revives,saved.FreshEntryAfterMillis),
				(cold.StartedAtMillis,cold.Revives,cold.FreshEntryAfterMillis));
			Assert.Equal(saved.IncomingEquipment,cold.IncomingEquipment);
			Assert.Throws<InvalidDataException>(()=>NaturalHaramelProgress.Read(path,99,1300,fresh,Leg));
			Assert.Throws<InvalidDataException>(()=>NaturalHaramelProgress.Read(path,133297,1199,fresh,Leg));
			fresh.Apply(Packet<SM_PLAYER_SPAWN>(("worldId",Rules.MapId),("x",172f),("y",20f),("z",144.22548f),("heading",(byte)0)));
			cold=NaturalHaramelProgress.Read(path,133297,1300,fresh,Leg);
			Assert.True(cold.NeedsInstanceObservation);
			Assert.Equal("observe-haramel-entry",Decide(NaturalAltgardObservation.Observe(fresh,fresh.Position!.Value,
				haramelProgress:cold,nowMillis:1300)).Action);
			Assert.Throws<InvalidDataException>(()=>saved.ObserveEntry(2,100,Entry(1) with { PlayerId=99 },1300));
		}
		finally { File.Delete(path);File.Delete(path+".tmp"); }
	}

	[Fact]
	public void RevisedIncomingEarringIsProtectedAndCannotDisappearOrChangeObjectsAcrossColdResume()
	{
		BotWorldModel fresh = Login();
		int old = fresh.Inventory.Values.Single(i => i.ItemId == 120001521).ObjectId;
		fresh.Apply(Packet<SM_DELETE_ITEM>(("itemObjectId", old)));
		AddEarring(900833);
		Assert.Throws<InvalidDataException>(() => NaturalHaramelProgress.Begin(133297, 0, fresh, Rules));
		NaturalAltgardContract Bound() => NaturalAltgardContinuation.BindIncoming(Leg, fresh.CompletedQuestIds,
			fresh.Inventory.Values.Select(i => new NaturalJourneyItem(i.ObjectId, i.ItemId, i.Count, i.EquipmentSlot)).ToArray(),
			NaturalAltgardContinuation.EquippedItemIds(fresh));
		NaturalAltgardContract bound = Bound();
		Assert.DoesNotContain(120001521, bound.Haramel!.ProtectedItemIds);
		Assert.Contains(120000833, bound.Haramel.ProtectedItemIds);
		Assert.Contains(123001109, NaturalAltgardContinuation.EquippedItemIds(fresh));
		Assert.Contains(120001521, Rules.ProtectedItemIds); // The historical contract is unchanged.
		NaturalHaramelProgress saved = NaturalHaramelProgress.Begin(133297, 0, fresh, bound.Haramel);
		string path = Path.Combine(Path.GetTempPath(), "rc11-handoff-" + Guid.NewGuid().ToString("N") + ".json");
		try
		{
			saved.Write(path);
			Assert.Equal(saved.IncomingEquipment, NaturalHaramelProgress.Read(path, 133297, 1, fresh, Bound()).IncomingEquipment);
			fresh.Apply(Packet<SM_DELETE_ITEM>(("itemObjectId", 900833)));
			Assert.Throws<InvalidDataException>(() => NaturalHaramelProgress.Read(path, 133297, 2, fresh, Bound()));
			AddEarring(900834); // Same item ID cannot replace the retained incoming object.
			Assert.Throws<InvalidDataException>(() => NaturalHaramelProgress.Read(path, 133297, 2, fresh, Bound()));
		}
		finally { File.Delete(path); File.Delete(path + ".tmp"); }

		void AddEarring(int objectId) => fresh.Apply(Packet<SM_INVENTORY_ADD_ITEM>(("items", new List<IReadOnlyDictionary<string, object?>>
		{
			Row(("objectId", objectId), ("itemId", 120000833), ("desc", ""), ("itemCount", 1L), ("itemMask", (ushort)4),
				("itemCreator", ""), ("cloth", false), ("equipmentSlot", (ushort)64), ("details", new BotItemDetails(EquippedSlot: 64))),
		})));
	}

	[Theory]
	[InlineData(111501065)]
	[InlineData(112501015)]
	[InlineData(113501074)]
	[InlineData(111101650)]
	[InlineData(110551139)]
	[InlineData(114501726)]
	[InlineData(188053787)]
	public void RevisedBindingDoesNotDropMissingMandatoryGearFromItsProtection(int itemId)
	{
		BotWorldModel fresh = Login();
		fresh.Apply(Packet<SM_DELETE_ITEM>(("itemObjectId", fresh.Inventory.Values.Single(i => i.ItemId == itemId).ObjectId)));
		NaturalAltgardContract bound = NaturalAltgardContinuation.BindIncoming(Leg, fresh.CompletedQuestIds,
			fresh.Inventory.Values.Select(i => new NaturalJourneyItem(i.ObjectId, i.ItemId, i.Count, i.EquipmentSlot)).ToArray(),
			NaturalAltgardContinuation.EquippedItemIds(fresh));
		Assert.Contains(itemId, bound.Haramel!.ProtectedItemIds);
		Assert.Throws<InvalidDataException>(() => NaturalHaramelProgress.Begin(133297, 0, fresh, bound.Haramel));
	}

	[Fact]
	public void InstanceInfoDecodesActualSelfAndTeamCountsAndResetOrMergeSemantics()
	{
		var decoder=new BotServerPacketDecoder(); var world=new BotWorldModel();
		byte[] Body(byte update, int count, int cooldown=46)
		{
			using var stream=new MemoryStream(); using var w=new BinaryWriter(stream);
			w.Write(update);w.Write(cooldown);w.Write((byte)0);w.Write((ushort)1);w.Write(133297);w.Write((ushort)1);
			w.Write(cooldown);w.Write(0);w.Write(7200);w.Write(16);w.Write(-count);w.Write((byte)1);w.Write((ushort)'X');w.Write((ushort)0);
			return stream.ToArray();
		}
		byte[] body=Body(0,1);
		world.Apply(decoder.Decode(typeof(SM_INSTANCE_INFO),body));
		Assert.Equal(Entry(1),world.InstanceEntries[(133297,46)]);
		world.Apply(decoder.Decode(typeof(SM_INSTANCE_INFO),Body(2,2,47)));
		Assert.Equal(2,world.InstanceEntries.Count);
		world.Apply(decoder.Decode(typeof(SM_INSTANCE_INFO),Body(0,2)));
		Assert.Single(world.InstanceEntries);
		Assert.Equal(2,world.InstanceEntries[(133297,46)].EntriesUsed);
		for(int size=0;size<body.Length;size++) Assert.Throws<InvalidDataException>(() => decoder.Decode(typeof(SM_INSTANCE_INFO),body[..size]));
		Assert.Throws<InvalidDataException>(() => decoder.Decode(typeof(SM_INSTANCE_INFO),[..body,0]));
	}

	private static NaturalHaramelProgress Progress() => new(133297,0,0,0,[],null,[new(137763,101501357,3),new(134334,123001109,65536)]);
	private static BotInstanceEntry Entry(int count) => new(133297,46,7200,16,count,true);
	private static NaturalAltgardDecision Decide(NaturalAltgardObservation state) => NaturalAltgardDecisionEngine.Decide(Leg,state,Objectives,1);
	private static NaturalAltgardObservation State(int map=220030000)
	{
		float[] at=Rules.WorkingBindPosition;
		var position=new BotPosition(at[0],at[1],at[2],0);
		return new(true,map,24,false,new Dictionary<int,BotQuestState>(),Leg.Start.CompletedQuestIds.ToHashSet(),position,
			Rules.ProtectedItemIds.ToDictionary(id=>id,id=>id==186000006 ? 19L : id==186000007 ? 0L : 1L),new(220030000,position,0),
			SkillIds:new HashSet<int>(),CompletedQuestCounts:Leg.Start.CompletedQuestIds.ToDictionary(id=>id,_=>(byte)1),
			Inventory:[new(137763,101501357,1,3)],HaramelProgress:Progress(),NowMillis:1000,InstanceId:map==Rules.MapId ? 2 : null);
	}
	private static NaturalAltgardObservation Quest(NaturalAltgardObservation state,int id,byte status,int var) =>
		state with { Quests=new Dictionary<int,BotQuestState>(state.Quests) { [id]=new(id,status,var,0,null) } };
	private static NaturalAltgardObservation Done(NaturalAltgardObservation state,int id) => state with
		{ CompletedQuestIds=state.CompletedQuestIds.Append(id).ToHashSet(),CompletedQuestCounts=new Dictionary<int,byte>(state.CompletedQuestCounts!) { [id]=1 } };
	private static NaturalAltgardObservation PostBoss(NaturalAltgardObservation state)
	{
		foreach(int id in new[] { 28500,28501,28503,28506,28507,28508,28509 }) state=Done(state,id);
		foreach(int id in new[] { 28504,28505,28510 }) state=Quest(state,id,3,0);
		return state;
	}
	private static BotWorldModel Login()
	{
		var world=new BotWorldModel();
		world.Apply(Packet<SM_STATS_INFO>(("objectId",133297),("level",(ushort)24),("expShown",6458442L),("expNeeded",8000000L),("expRecoverable",0L),
			("maxHp",1545),("currentHp",1545),("maxMp",2834),("currentMp",2834),("maxDp",(ushort)0),("dp",(ushort)0),("maxFp",60),("currentFp",42)));
		world.Apply(Packet<SM_PLAYER_SPAWN>(("worldId",220030000),("x",2663f),("y",1663f),("z",324.69f),("heading",(byte)0)));
		world.Apply(Packet<SM_PLAYER_INFO>(("objectId",133297),("x",2663f),("y",1663f),("z",324.69f),("heading",(byte)0),
			("name","Natural"),("state",(ushort)0),("race",(byte)1),("playerClass",(byte)10)));
		world.Apply(Packet<SM_QUEST_LIST>(("quests",new List<IReadOnlyDictionary<string,object?>>())));
		world.Apply(Packet<SM_QUEST_COMPLETED_LIST>(("updateMode",(byte)0),("quests",Leg.Start.CompletedQuestIds.Select(id=>(IReadOnlyDictionary<string,object?>)
			Row(("questId",id),("completeCount",(byte)1),("nonRepeatable",true))).ToList())));
		world.Apply(Packet<SM_SKILL_LIST>(("silentUpdate",false),("skills",new List<IReadOnlyDictionary<string,object?>>())));
		var slots=new Dictionary<int,long> { [101501357]=3,[111501065]=16,[112501015]=2048,[113501074]=4096,[123001109]=65536 };
		var items=Rules.ProtectedItemIds.Select((id,index)=>(IReadOnlyDictionary<string,object?>)Row(
			("objectId",id==101501357 ? 137763 : index+300), ("itemId",id),("desc",""),("itemCount",id==186000006 ? 19L : id==186000007 ? 0L : 1L),
			("itemMask",(ushort)4),("itemCreator",""),("equipmentSlot",unchecked((ushort)slots.GetValueOrDefault(id))),
			("cloth",false),("details",new BotItemDetails(EquippedSlot:slots.GetValueOrDefault(id))))).ToList();
		world.Apply(Packet<SM_INVENTORY_INFO>(("firstPacket",true),("items",items)));
		world.Apply(Packet<SM_INVENTORY_INFO>(("firstPacket",false),("items",new List<IReadOnlyDictionary<string,object?>>())));
		world.Apply(Packet<SM_BIND_POINT_INFO>(("mapId",220030000),("x",2656.192f),("y",1660.59f),("z",325.052f),("bindPointType",(byte)0),("kiskObjectId",0)));
		return world;
	}
	private static DecodedBotServerPacket Packet<T>(params (string Key,object? Value)[] fields)=>new(typeof(T),Row(fields));
	private static Dictionary<string,object?> Row(params (string Key,object? Value)[] fields)=>fields.ToDictionary(f=>f.Key,f=>f.Value);
}
