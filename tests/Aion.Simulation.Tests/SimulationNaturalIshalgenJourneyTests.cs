using Aion.Bots.Dashboard;
using Aion.Bots.Navigation;
using Aion.Bots.Navigation.NavMesh;
using Aion.Bots.Movement;
using Aion.Bots.Protocol;
using Aion.Bots.Reflexes;
using Aion.Bots.Scenarios;
using Aion.Bots.Tracing;
using Aion.Bots.World;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Templates.Npc;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.Simulation.Tests;

public sealed partial class SimulationFastScenarioTests
{
	private static float Distance(BotPosition a, BotPosition b) => MathF.Sqrt(
		MathF.Pow(a.X - b.X, 2) + MathF.Pow(a.Y - b.Y, 2) + MathF.Pow(a.Z - b.Z, 2));

	/// <summary>
	/// A fresh or retained Priest follows the frozen NI-07 quest contract. The explicitly
	/// selected short scope is a diagnostic checkpoint, not acceptance evidence.
	/// </summary>
	[SkippableFact]
	public async Task NaturalIshalgenPriestCompletesFrozenJourneyWithoutSetup()
	{
		Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
		bool stopAfterQ2004 = Environment.GetEnvironmentVariable("NI07_STOP_AFTER_Q2004") == "1";
		bool stopAfterQ2005 = Environment.GetEnvironmentVariable("NI07_STOP_AFTER_Q2005") == "1";
		bool stopAfterQ2006 = Environment.GetEnvironmentVariable("NI07_STOP_AFTER_Q2006") == "1";
		bool stopAfterQ2007 = Environment.GetEnvironmentVariable("NI07_STOP_AFTER_Q2007") == "1";
		bool fullJourney = Environment.GetEnvironmentVariable("NI07_FULL_JOURNEY") == "1";
		Skip.IfNot(stopAfterQ2004 || stopAfterQ2005 || stopAfterQ2006 || stopAfterQ2007 || fullJourney,
			"Set NI07_STOP_AFTER_Q2004=1, NI07_STOP_AFTER_Q2005=1, NI07_STOP_AFTER_Q2006=1 " +
			"or NI07_STOP_AFTER_Q2007=1 " +
			"for a focused checkpoint, or NI07_FULL_JOURNEY=1 for 41-quest acceptance.");
		string combatTracePath = Path.Combine(
			Environment.GetEnvironmentVariable("AION_NI07_COMBAT_DIR") ??
				Path.Combine(Aion.GameServer.TestKit.RealStaticData.RepoRoot(), "run", "ni07-combat"),
			$"{DateTimeOffset.UtcNow:yyyyMMddTHHmmssfffZ}-{Guid.NewGuid():N}.trace.jsonl");
		using var combatTrace = BotActionTraceWriter.Open(combatTracePath,
			Environment.GetEnvironmentVariable("AION_SIM_RUN_ID") ?? "ni07", "b01", "sim-player-41",
			virtualTime: () => TimeSpan.FromMilliseconds(fixture.Clock.NowMillis));
		Console.WriteLine($"NI-07 combat trace: {combatTracePath}");
		using var policy = NewPolicy("NI07", includeHistory: true);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(
			stopAfterQ2004 ? 6 : stopAfterQ2005 ? 8 : stopAfterQ2006 ? 16 : stopAfterQ2007 ? 20 : 45));
		CancellationToken token = timeout.Token;
		await using var session = new SimulationL0Session(
			fixture, policy, "b01", accountId: 41, "Asimnjour", Race.ASMODIANS,
			combatTrace, combatTracePath);
		var dashboard = new LiveBotDashboardState();
		int dashboardPort = int.Parse(Environment.GetEnvironmentVariable("AION_BOT_DASHBOARD_PORT") ?? "17880");
		await using var dashboardHost = new LiveBotDashboardHost(
			Environment.GetEnvironmentVariable("AION_SIM_RUN_ID") ?? "natural-ishalgen", ["NI-07", "NI-08"], dashboard, dashboardPort);
		session.Dashboard = dashboard;
		if (dashboardHost.Enabled) Console.WriteLine($"Natural Ishalgen dashboard: {dashboardHost.Url}");
		var runtime = new NaturalJourneyRuntime(Aion.GameServer.TestKit.RealStaticData.RepoRoot(),
			"SIM-natural-ishalgen", fixture.Seed, fixture.DataManager.StaticData, () => fixture.Clock.NowMillis, fixture.Epoch,
			() => BotNavigationGeometry.ForServerWorld(fixture.World.GetPlayer(session.CharacterId).GetInstanceId(), Race.ASMODIANS),
			EnterAsync, policy.AssertClean, () => policy.SnapshotProblems(), combatTrace, dashboard);
		await new NaturalIshalgenJourney(session, runtime, new NaturalJourneyOptions(
			stopAfterQ2004 ? 2004 : stopAfterQ2005 ? 2005 : stopAfterQ2006 ? 2006 : stopAfterQ2007 ? 2007 : null,
			Environment.GetEnvironmentVariable("NI08_RELOG_AT"), Environment.GetEnvironmentVariable("NI08_STOP_AT"),
			Environment.GetEnvironmentVariable("NI07_STOP_ON_DEATH") == "1")).RunAsync(token);

		async Task<bool> EnterAsync(CancellationToken token)
		{
			string? resumeIdentity = Environment.GetEnvironmentVariable("NI08_RESUME_CHARACTER");
			bool resuming = !string.IsNullOrWhiteSpace(resumeIdentity);
			int retainedId = 0;
			if (resuming && (!int.TryParse(resumeIdentity, out retainedId) || retainedId <= 0))
				throw new ArgumentException("NI08_RESUME_CHARACTER must be a positive character ID; refusing to create a replacement.");
			session.BeginStep(resuming ? "ni08-login-existing" : "ni07-create", resuming ? "reconstruct-retained-priest" : "create-natural-asmodian-priest");
			if (resuming)
			{
				var list = await session.LoginCharacterListAsync(token);
				var retained = list.Get<List<IReadOnlyDictionary<string, object?>>>("characters")
					.SingleOrDefault(c => Get<int>(c, "objectId") == retainedId)
					?? throw new InvalidDataException($"NI-08 retained character {retainedId} is missing; refusing to create a replacement.");
				if (Get<string>(retained, "name") != "Asimnjour" || Get<int>(retained, "race") != (int)Race.ASMODIANS ||
					Get<int>(retained, "playerClass") != (int)PlayerClass.PRIEST || Get<int>(retained, "deletionTimeSeconds") != 0)
					throw new InvalidDataException($"NI-08 retained character {retainedId} identity changed.");
				session.SelectCharacter(retainedId, "Asimnjour");
			}
			else
			{
				await session.LoginAndAuthenticateAsync(token);
				await session.CreateCharacterAsync(token, PlayerClass.PRIEST);
			}
			await session.EnterWorldAsync(token);
			if (!resuming) await session.WaitForPacketAsync(typeof(SM_PLAY_MOVIE), token);
			await session.SynchronizeAsync(token);
			Assert.Equal(PlayerClass.PRIEST, fixture.World.GetPlayer(session.CharacterId).GetPlayerClass());
			return resuming;
		}
	}
}
