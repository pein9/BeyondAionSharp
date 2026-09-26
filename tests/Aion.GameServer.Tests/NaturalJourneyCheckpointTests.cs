using System.Text.Json;
using Aion.Bots.Api;
using Aion.Bots.Protocol;
using Aion.Bots.Scenarios;
using Aion.Bots.World;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Tests;

public sealed class NaturalJourneyCheckpointTests
{
	private static readonly NaturalIshalgenContract Contract = new(220010000, 2008, 9,
		[new(2101, 1, []), new(2102, 1, [2101])]);

	[Fact]
	public void LoginReconstructionRequiresBothJournalsAndInventoryTerminator()
	{
		var world = Login();
		Assert.False(world.LoginStateObserved);
		Assert.Throws<InvalidDataException>(() => Capture(world));
		world.Apply(Packet<SM_INVENTORY_INFO>(("firstPacket", false), ("items", Rows())));
		Assert.True(world.LoginStateObserved);
		var checkpoint = Capture(world);
		Assert.Equal(42, checkpoint.CharacterId);
		Assert.Equal(new BotPosition(600, 2700, 300, 10), checkpoint.Position);
		Assert.Equal(2102, checkpoint.Next.SelectedQuestId);
		Assert.Equal("continue-quest", checkpoint.Next.SelectedAction);
		Assert.Equal(2, Assert.Single(checkpoint.Quests).StepAndFlags);
		Assert.Equal([2101], checkpoint.CompletedQuestIds);
		Assert.Equal(123, checkpoint.CurrentHp);
	}

	[Fact]
	public void NewLoginCannotReusePriorCompletionOrPositionOrReadiness()
	{
		var api = new BotApi(Login());
		api.World.Apply(Packet<SM_INVENTORY_INFO>(("firstPacket", false), ("items", Rows())));
		Assert.True(api.World.LoginStateObserved);
		var timing = api.Timing;
		timing.RecordTargetSelection(12345);
		timing.SetActivity(Aion.Bots.Timing.BotBlockingActivity.Gathering, true);
		api.QuestDialogEchoes.Record(12345, 1002, 2102);
		api.BeginLoginObservation();
		Assert.Same(timing, api.Timing);
		Assert.Null(timing.SelectedTargetId);
		Assert.Empty(timing.BlockingActivities);
		Assert.Null(api.QuestDialogEchoes.Pending);
		Assert.False(api.World.LoginStateObserved);
		Assert.Null(api.World.Position);
		Assert.Empty(api.World.CompletedQuestIds);
		Assert.Empty(api.World.Quests);
		api.World.Apply(Packet<SM_QUEST_COMPLETED_LIST>(("updateMode", (byte)0), ("quests", Rows())));
		Assert.True(api.World.CompletedJournalObserved);
		Assert.False(api.World.QuestJournalObserved);
		Assert.Throws<InvalidDataException>(() => Capture(api.World));
	}

	[Fact]
	public void CompletionDeltaCannotStandInForTheLoginSnapshot()
	{
		var world = new BotWorldModel();
		world.Apply(Packet<SM_QUEST_COMPLETED_LIST>(("updateMode", (byte)1),
			("quests", Rows(Row(("questId", 2101), ("completeCount", (byte)1), ("nonRepeatable", true))))));
		Assert.False(world.CompletedJournalObserved);
		Assert.Contains(2101, world.CompletedQuestIds);
	}

	[Theory]
	[InlineData(0, 1, 1)]
	[InlineData(1, 0, 1)]
	[InlineData(64, 1, 0)]
	[InlineData(65, 0, 0)]
	[InlineData(unchecked((int)0xAB000001), 0, 1)]
	public void HatataUsesItsOwnSixBitCounterInsteadOfReplayingTheWholeHunt(int packed, int first, int hatata)
	{
		string root = Aion.GameServer.TestKit.RealStaticData.RepoRoot();
		var plan = QuestRunPlan.Load(Path.Combine(root, "parity-artifacts/e2e/natural-ishalgen-plans/2129.json"));
		var world = new BotWorldModel();
		world.Apply(Packet<SM_QUEST_LIST>(("quests", Rows(Row(("questId", 2129),
			("status", (byte)3), ("stepAndFlags", packed), ("completeCount", (byte)0))))));
		var kills = QuestRunBook.Build(plan).Operations.Where(o => o.Kind == QuestRunOperationKind.Kill).ToArray();
		Assert.Equal(first, NaturalQuestProgress.RemainingKills(plan, kills[0], world));
		Assert.Equal(hatata, NaturalQuestProgress.RemainingKills(plan, kills[1], world));
		world.Apply(Packet<SM_QUEST_ACTION>(("action", (byte)2), ("questId", 2129),
			("status", (byte)4), ("stepAndFlags", packed)));
		Assert.All(kills, kill => Assert.Equal(0, NaturalQuestProgress.RemainingKills(plan, kill, world)));
	}

	[Fact]
	public void WrongCharacterIsRejectedAndCheckpointDoesNotChangeWithLaterPackets()
	{
		var world = Login();
		world.Apply(Packet<SM_INVENTORY_INFO>(("firstPacket", false), ("items", Rows())));
		Assert.Throws<InvalidDataException>(() => NaturalJourneyCheckpoint.Capture(world, 99, 2, Contract));
		var checkpoint = Capture(world);
		world.Apply(Packet<SM_QUEST_ACTION>(("action", (byte)2), ("questId", 2102),
			("status", (byte)4), ("stepAndFlags", 4)));
		Assert.Equal(2, Assert.Single(checkpoint.Quests).StepAndFlags);
		Assert.Equal(4, Capture(world).Quests.Single().StepAndFlags);
	}

	[Fact]
	public void ReconnectionMovementAndHealingCannotResetStallBudgetButQuestProgressCan()
	{
		var world = Login();
		world.Apply(Packet<SM_INVENTORY_INFO>(("firstPacket", false), ("items", Rows())));
		var checkpoint = Capture(world);
		var progress = new NaturalJourneyProgress(TimeSpan.FromMinutes(5));
		progress.Observe(checkpoint, TimeSpan.Zero);
		progress.Observe(checkpoint with { ConnectionGeneration = 2, CurrentHp = 999,
			Position = new(100, 200, 300, 0) }, TimeSpan.FromMinutes(4));
		Assert.Throws<TimeoutException>(() => progress.Observe(checkpoint, TimeSpan.FromMinutes(5)));
		checkpoint = checkpoint with { Quests = [new(2102, 3, 3, 0, null)] };
		progress.Observe(checkpoint, TimeSpan.FromMinutes(6));
		progress.Observe(checkpoint, TimeSpan.FromMinutes(10));
		Assert.Throws<TimeoutException>(() => progress.Observe(checkpoint, TimeSpan.FromMinutes(11)));
	}

	[Fact]
	public void FailurePackageRetainsOriginalObservationAndDoesNotOverwriteIncidents()
	{
		string directory = Path.Combine(Path.GetTempPath(), "ni08-" + Guid.NewGuid().ToString("N"));
		try
		{
			var world = Login();
			world.Apply(Packet<SM_INVENTORY_INFO>(("firstPacket", false), ("items", Rows())));
			var failure = new NaturalJourneyFailure("disconnect", "q2102", "kill", 1,
				"EndOfStreamException", "Connection ended", "stack", Capture(world), "trace.jsonl", ["SM_QUEST_ACTION"],
				new("test-run", "SIM-natural-ishalgen", 5, "build-sha", "module-id", 1234))
			{ ServerProblems = JsonSerializer.SerializeToElement(new[] { new { Fingerprint = "server-fault", Message = "timer failed" } }) };
			string first = failure.Write(directory);
			string second = failure.Write(directory);
			Assert.NotEqual(first, second);
			var stored = JsonSerializer.Deserialize<NaturalJourneyFailure>(File.ReadAllText(first))!;
			Assert.Equal("q2102", stored.Step);
			Assert.Equal("trace.jsonl", stored.PacketTracePath);
			Assert.Equal(["SM_QUEST_ACTION"], stored.RecentPackets);
			Assert.Equal(42, stored.LastObservation!.CharacterId);
			Assert.Equal(2, Assert.Single(stored.LastObservation.Quests).StepAndFlags);
			Assert.Equal("build-sha", stored.Context!.Build);
			Assert.Equal(5, stored.Context.Seed);
			Assert.Equal("server-fault", stored.ServerProblems!.Value[0].GetProperty("Fingerprint").GetString());
		}
		finally { Directory.Delete(directory, recursive: true); }
	}

	private static NaturalJourneyCheckpoint Capture(BotWorldModel world) =>
		NaturalJourneyCheckpoint.Capture(world, 42, 1, Contract);

	private static BotWorldModel Login()
	{
		var world = new BotWorldModel();
		world.Apply(Packet<SM_STATS_INFO>(("objectId", 42), ("level", (ushort)2),
			("expNeeded", 900L), ("expRecoverable", 0L), ("expShown", 100L),
			("maxHp", 250), ("currentHp", 123), ("maxMp", 300), ("currentMp", 200),
			("maxDp", (ushort)0), ("dp", (ushort)0), ("maxFp", 60), ("currentFp", 60)));
		world.Apply(Packet<SM_PLAYER_SPAWN>(("worldId", 220010000),
			("x", 600f), ("y", 2700f), ("z", 300f), ("heading", (byte)10)));
		world.Apply(Packet<SM_QUEST_LIST>(("quests", Rows(Row(("questId", 2102),
			("status", (byte)3), ("stepAndFlags", 2), ("completeCount", (byte)0))))));
		world.Apply(Packet<SM_QUEST_COMPLETED_LIST>(("updateMode", (byte)0),
			("quests", Rows(Row(("questId", 2101), ("completeCount", (byte)1), ("nonRepeatable", true))))));
		world.Apply(Packet<SM_SKILL_LIST>(("silentUpdate", false), ("skills", Rows())));
		return world;
	}

	private static DecodedBotServerPacket Packet<T>(params (string Name, object? Value)[] fields) => new(typeof(T), Row(fields));
	private static Dictionary<string, object?> Row(params (string Name, object? Value)[] fields) => fields.ToDictionary(f => f.Name, f => f.Value);
	private static List<IReadOnlyDictionary<string, object?>> Rows(params IReadOnlyDictionary<string, object?>[] rows) => rows.ToList();
}
