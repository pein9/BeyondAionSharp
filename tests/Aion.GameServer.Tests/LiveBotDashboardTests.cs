using Aion.Bots.Dashboard;
using System.Text.Json;
using Aion.Bots.Scenarios;
using Aion.LiveBots;
using Aion.Bots.Protocol;
using Aion.Bots.World;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Tests;

public sealed class LiveBotDashboardTests
{
	[Fact]
	public void DashboardIsOptInAndPortIsValidated()
	{
		Assert.Equal(0, Parse().DashboardPort);
		Assert.Equal(17880, Parse("--dashboard-port", "17880").DashboardPort);
		Assert.Throws<ArgumentException>(() => Parse("--dashboard-port", "-1"));
		Assert.Throws<ArgumentException>(() => Parse("--dashboard-port", "65536"));
	}

	[Fact]
	public void DecisionViewWindowIsOnlyAvailableForTheSingleNaturalSubject()
	{
		Assert.Equal(30, LiveBotOptions.Parse(["--run", "decision", "--output", "run/decision",
			"--scenario", "NI-02"]).DecisionViewSeconds);
		Assert.Throws<ArgumentException>(() => Parse("--decision-view-seconds", "0"));
		Assert.Throws<ArgumentException>(() => LiveBotOptions.Parse(["--run", "decision", "--output", "run/decision",
			"--scenario", "NI-02", "--bots", "2"]));
		Assert.Equal(0, LiveBotOptions.Parse(["--run", "navigation", "--output", "run/navigation",
			"--scenario", "NI-03", "--decision-view-seconds", "0"]).DecisionViewSeconds);
		Assert.Throws<ArgumentException>(() => LiveBotOptions.Parse(["--run", "navigation", "--output", "run/navigation",
			"--scenario", "NI-03", "--bots", "2"]));
	}

	[Fact]
	public async Task LoopbackDashboardServesPageAssetsAndImmutableState()
	{
		var state = new LiveBotDashboardState();
		state.Publish(Snapshot());
		state.PublishDecision("b01", new NaturalDecision(1, "find-quest-starter", 2000,
			"awaiting-capability", "Selected frozen quest", [new("client-observation", "pass", "Fresh")],
			[new(2000, "candidate", [new("starter-observation", "unknown", "Not yet observed")])]));
		await using var host = LiveBotDashboardHost.StartForTest("dashboard-test", ["NI-01"], state);
		using var client = new HttpClient { BaseAddress = host.Url };

		using HttpResponseMessage pageResponse = await client.GetAsync("");
		string page = await pageResponse.Content.ReadAsStringAsync();
		string script = await client.GetStringAsync("dashboard.js");
		using JsonDocument api = JsonDocument.Parse(await client.GetStringAsync("api/state"));

		Assert.Equal("127.0.0.1", host.Url.Host);
		Assert.True(pageResponse.IsSuccessStatusCode);
		Assert.Contains("Live bot monitor", page, StringComparison.Ordinal);
		Assert.Contains("fetch(\"/api/state\"", script, StringComparison.Ordinal);
		Assert.Contains("Decision tree", page, StringComparison.Ordinal);
		Assert.Equal("dashboard-test", api.RootElement.GetProperty("run").GetString());
		Assert.Equal("NI-01", api.RootElement.GetProperty("scenarios")[0].GetString());
		JsonElement bot = Assert.Single(api.RootElement.GetProperty("bots").EnumerateArray());
		Assert.Equal("Ishalgenbot", bot.GetProperty("characterName").GetString());
		Assert.Equal("travel-to-quest", bot.GetProperty("action").GetString());
		Assert.Equal(2100, bot.GetProperty("activeQuests")[0].GetProperty("questId").GetInt32());
		Assert.Equal(182400001, bot.GetProperty("inventory")[0].GetProperty("itemId").GetInt32());
		Assert.Equal("find-quest-starter", bot.GetProperty("decisions")[0].GetProperty("selectedAction").GetString());
		Assert.Equal("starter-observation", bot.GetProperty("decisions")[0].GetProperty("quests")[0]
			.GetProperty("checks")[0].GetProperty("rule").GetString());
		Assert.Contains("default-src 'self'", pageResponse.Headers.GetValues("Content-Security-Policy").Single(), StringComparison.Ordinal);
		Assert.Contains("img-src 'self'", pageResponse.Headers.GetValues("Content-Security-Policy").Single(), StringComparison.Ordinal);
		using JsonDocument catalog = JsonDocument.Parse(await client.GetStringAsync("maps/catalog.json"));
		JsonElement ishalgen = catalog.RootElement.GetProperty("maps").EnumerateArray().Single(m => m.GetProperty("mapId").GetInt32() == 220010000);
		Assert.Equal(740, ishalgen.GetProperty("calibration").GetProperty("offsetX").GetInt32());
		Assert.Contains(ishalgen.GetProperty("spawns").EnumerateArray(), s => s.GetProperty("templateId").GetInt32() == 210409);
		using HttpResponseMessage artwork = await client.GetAsync(ishalgen.GetProperty("image").GetString());
		Assert.Equal("image/webp", artwork.Content.Headers.ContentType!.MediaType);
		Assert.Equal("RIFF", System.Text.Encoding.ASCII.GetString((await artwork.Content.ReadAsByteArrayAsync())[..4]));
		Assert.Contains("projectMapPoint", await client.GetStringAsync("dashboard-map.js"), StringComparison.Ordinal);
		Assert.Equal(System.Net.HttpStatusCode.NotFound, (await client.GetAsync("maps/not-an-asset.webp")).StatusCode);
	}

	[Fact]
	public void MapObservationCopiesMovingObjectsAndDropsThemAfterWorldReload()
	{
		var world = new BotWorldModel();
		world.Apply(new DecodedBotServerPacket(typeof(SM_NPC_INFO), new Dictionary<string, object?>
		{
			["objectId"] = 71, ["npcId"] = 210409, ["visualNpcId"] = 210409,
			["x"] = 621f, ["y"] = 859f, ["z"] = 317f, ["heading"] = (byte)30, ["state"] = (ushort)7,
		}));
		BotDashboardSnapshot before = Observe();
		BotDashboardObject corpse = Assert.Single(before.ObservedObjects);
		Assert.True(corpse.IsCorpse);
		Assert.Equal(210409, corpse.TemplateId);
		Assert.Equal(621, corpse.Position.X);
		world.Apply(new DecodedBotServerPacket(typeof(SM_MOVE), new Dictionary<string, object?>
		{
			["objectId"] = 71, ["x"] = 625f, ["y"] = 865f, ["z"] = 317f,
			["heading"] = (byte)30, ["targetX"] = 640f, ["targetY"] = 875f, ["targetZ"] = 317f,
		}));
		BotDashboardObject moving = Assert.Single(Observe().ObservedObjects);
		Assert.Equal(625, moving.Position.X);
		Assert.Equal(640, moving.MoveTarget!.X);
		Assert.Equal(621, corpse.Position.X); // Published snapshots never follow later dictionary mutations.
		world.BeginWorldReload();
		Assert.Empty(Observe().ObservedObjects);
		Assert.Single(before.ObservedObjects);
		BotDashboardSnapshot Observe() => BotDashboardSnapshot.Observe(world, "b01", "account", "Priest", 42,
			"IN_GAME", 1, "walk", "move", "running", "SM_MOVE");
	}

	private static LiveBotOptions Parse(params string[] additional) => LiveBotOptions.Parse(
		["--run", "dashboard", "--output", "run/dashboard", "--scenario", "connect", .. additional]);

	private static BotDashboardSnapshot Snapshot() => new(
		Bot: "b01",
		Account: "niishalgen",
		CharacterName: "Ishalgenbot",
		CharacterId: 42,
		Connection: "IN_GAME",
		ConnectionGeneration: 1,
		Step: "s03",
		Action: "travel-to-quest",
		ActionStatus: "running",
		LastPacket: "SM_MOVE",
		UpdatedAt: DateTimeOffset.UtcNow,
		MapId: 220010000,
		Channel: 0,
		Position: new BotDashboardPosition(100, 200, 300, 4),
		Level: 3,
		Experience: 500,
		ExperienceNeeded: 1000,
		CurrentHp: 450,
		MaxHp: 500,
		CurrentMp: 300,
		MaxMp: 400,
		CurrentDp: 0,
		MaxDp: 4000,
		IsDead: false,
		Kinah: 1234,
		SkillCount: 4,
		CooldownCount: 1,
		Nearby: new BotDashboardObjectCounts(1, 8, 3, 0),
		ActiveQuests: [new BotDashboardQuest(2100, 1, 2, 0, null)],
		CompletedQuestIds: [2001, 2101],
		Inventory: [new BotDashboardItem(7, 182400001, "Kinah", 1234, 0)],
		LastSystemMessage: "STR_QUEST_ACQUIRED");
}
