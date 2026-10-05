using Aion.Bots.Dashboard;
using Aion.Bots.Navigation;
using Aion.Bots.Scenarios;
using Aion.Bots.Tracing;
using Aion.Bots.World;
using Aion.GameServer.Model;
using Aion.GameServer.TestKit;

namespace Aion.Simulation.Tests;

public sealed partial class SimulationFastScenarioTests
{
	[SkippableFact]
	public async Task NaturalSameMapBindReviveDropsOldObservedTargetsAndRetainsProgress()
	{
		Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
		using var policy = NewPolicy("RC11BindView", includeHistory: false);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));
		CancellationToken token = timeout.Token;
		string run = Environment.GetEnvironmentVariable("AION_SIM_RUN_ID") ?? "rc11-bind-view";
		string path = Path.Combine(RealStaticData.RepoRoot(), "run", $"{run}.bind-view.trace.jsonl");
		using var trace = BotActionTraceWriter.Open(path, run, "b01", "sim-player-249",
			virtualTime: () => TimeSpan.FromMilliseconds(fixture.Clock.NowMillis));
		await using var session = new SimulationL0Session(fixture, policy, "b01", 249, "Asimbindview", Race.ASMODIANS, trace, path);
		var dashboard = new LiveBotDashboardState();
		await using var host = new LiveBotDashboardHost(run, ["RC-11"], dashboard,
			int.Parse(Environment.GetEnvironmentVariable("AION_BOT_DASHBOARD_PORT") ?? "17880"));
		session.Dashboard = dashboard;
		if (host.Enabled) Console.WriteLine($"RC-11 bind-view probe dashboard: {host.Url}");
		var probe = new DestinyProbe(this, fixture, session, token);
		await probe.InitializeAsync(); // Labelled disposable-probe class, skills and prerequisite setup.
		await probe.SetupAtFortressAsync();
		int obelisk = await probe.WalkNpcAsync(700065);
		NaturalServiceOutcome bound = await new NaturalServiceSteps(session).BindAsync(obelisk,
			session.Api.World.Objects[obelisk].Position, 220030000, 451, 6, token);
		Assert.True(bound.IsDone, bound.Reason);
		var source = probe.Server.GetWorldMapInstance().GetNpcs(210720).First(n => n.IsSpawned() && !n.IsDead());
		BotPosition sourcePosition = new(source.GetX(), source.GetY(), source.GetZ(), 0);
		var geometry = BotNavigationGeometry.ForServerWorld(probe.Server.GetInstanceId(), Race.ASMODIANS);
		BotPosition spot = geometry.GroundAround(220030000, sourcePosition, [35f]).First();
		Assert.True(NaturalFlightPolicy.Distance(spot, session.CurrentPosition) > 200);
		foreach (var npc in probe.Server.GetWorldMapInstance().GetNpcs().Where(n => n != source && !n.IsDead() &&
			NaturalHostility.IsAggressive(n.GetObjectTemplate(), fixture.DataManager.StaticData.TribeRelations, TribeClass.PC_DARK) &&
			NaturalFlightPolicy.Distance(spot, new(n.GetX(), n.GetY(), n.GetZ(), 0)) < 100).ToArray())
			fixture.World.Despawn(npc);
		Console.WriteLine("RC-11 labelled setup clears aggressive neighbours and teleports only free probe account 249.");
		session.Api.World.BeginWorldReload();
		await TeleportForSetupAsync(session, probe.Server, 220030000, spot.X, spot.Y, spot.Z, token);
		session.AcceptTeleportPosition();
		await session.SynchronizeAsync(token);
		Assert.Contains(source.GetObjectId(), session.Api.World.Objects.Keys);
		var completed = session.Api.World.CompletedQuestCounts.OrderBy(p => p.Key).ToArray();
		var inventory = session.Api.World.Inventory.Values.OrderBy(i => i.ObjectId).ToArray();
		var skills = session.Api.World.Skills.Values.OrderBy(s => s.SkillId).ToArray();
		bool inspectedReload = false;
		NaturalCombatDiagnosticResult result = await new NaturalIshalgenJourney(session, probe.Runtime, new()).RunObservedCombatAsync(async _ =>
		{
			session.BeginStep("probe-controlled-death", "labelled-death-before-ordinary-same-map-bind-revive");
			// The diagnostic's initial rest may also use a recovery item. Capture immediately before death.
			completed = session.Api.World.CompletedQuestCounts.OrderBy(p => p.Key).ToArray();
			inventory = session.Api.World.Inventory.Values.OrderBy(i => i.ObjectId).ToArray();
			skills = session.Api.World.Skills.Values.OrderBy(s => s.SkillId).ToArray();
			Assert.True(probe.Server.GetController().Die(probe.Server));
			await session.SynchronizeAsync(token);
			return source.GetObjectId();
		}, token, afterBindRevive: () =>
		{
			// Inspect the world reload before the ordinary recovery buff consumes a shield scroll.
			Assert.DoesNotContain(source.GetObjectId(), session.Api.World.Objects.Keys);
			Assert.Equal(completed, session.Api.World.CompletedQuestCounts.OrderBy(p => p.Key).ToArray());
			Assert.Equal(inventory, session.Api.World.Inventory.Values.OrderBy(i => i.ObjectId).ToArray());
			Assert.Equal(skills, session.Api.World.Skills.Values.OrderBy(s => s.SkillId).ToArray());
			inspectedReload = true;
		});
		Assert.False(result.Killed);
		Assert.Equal(1, result.Deaths);
		Assert.False(session.Api.World.IsDead);
		Assert.Equal(220030000, session.Api.World.MapId);
		Assert.DoesNotContain(source.GetObjectId(), session.Api.World.Objects.Keys);
		Assert.Equal(completed, session.Api.World.CompletedQuestCounts.OrderBy(p => p.Key).ToArray());
		Assert.True(inspectedReload);
		Console.WriteLine("RC-11 same-map natural CM_REVIVE: old assassin view discarded, no kill invented, inventory/skills/completed counts retained.");
		policy.AssertClean();
	}
}
