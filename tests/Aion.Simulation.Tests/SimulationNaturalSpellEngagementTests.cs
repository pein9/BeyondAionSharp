using Aion.Bots.Dashboard;
using Aion.Bots.Navigation;
using Aion.Bots.Protocol;
using Aion.Bots.Scenarios;
using Aion.Bots.Tracing;
using Aion.Bots.World;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.TestKit;

namespace Aion.Simulation.Tests;

public sealed partial class SimulationFastScenarioTests
{
	[SkippableFact]
	public async Task NaturalRestDefendsAgainstNativeCasterAfterReturnInterruption()
	{
		Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
		using var policy = NewPolicy("RC11SpellEngagement", includeHistory: false);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));
		CancellationToken token = timeout.Token;
		string run = Environment.GetEnvironmentVariable("AION_SIM_RUN_ID") ?? "rc11-spell-engagement";
		string path = Path.Combine(RealStaticData.RepoRoot(), "run", $"{run}.spell-engagement.trace.jsonl");
		using var trace = BotActionTraceWriter.Open(path, run, "b01", "sim-player-251",
			virtualTime: () => TimeSpan.FromMilliseconds(fixture.Clock.NowMillis));
		await using var session = new SimulationL0Session(fixture, policy, "b01", 251, "Asimspellrest", Race.ASMODIANS, trace, path);
		var dashboard = new LiveBotDashboardState();
		await using var host = new LiveBotDashboardHost(run, ["RC-11"], dashboard,
			int.Parse(Environment.GetEnvironmentVariable("AION_BOT_DASHBOARD_PORT") ?? "17880"));
		session.Dashboard = dashboard;
		if (host.Enabled) Console.WriteLine($"RC-11 spell-engagement probe dashboard: {host.Url}");
		var probe = new DestinyProbe(this, fixture, session, token);
		await probe.InitializeAsync(); // Labelled disposable-probe class/skills/prerequisites only.
		await probe.SetupAtFortressAsync();
		int obelisk = await probe.WalkNpcAsync(700065);
		var bound = await new NaturalServiceSteps(session).BindAsync(obelisk,
			session.Api.World.Objects[obelisk].Position, 220030000, 451, 6, token);
		Assert.True(bound.IsDone, bound.Reason);
		var group = fixture.DataManager.StaticData.SpawnsDh.GetSpawnsByWorldId(220030000).First(g => g.GetNpcId() == 210751);
		var shipped = group.GetSpawnTemplates().First();
		var spawn = Aion.GameServer.SpawnEngine.SpawnEngine.NewSingleTimeSpawn(220030000, 210751,
			shipped.GetX(), shipped.GetY(), shipped.GetZ(), shipped.GetHeading());
		var caster = Assert.IsType<Npc>(Aion.GameServer.SpawnEngine.SpawnEngine.SpawnObject(spawn, probe.Server.GetInstanceId()), exactMatch: false);
		BotPosition position = new(caster.GetX(), caster.GetY(), caster.GetZ(), 0);
		var geometry = BotNavigationGeometry.ForServerWorld(probe.Server.GetInstanceId(), Race.ASMODIANS);
		BotPosition spot = geometry.GroundAround(220030000, position, [15f]).First();
		foreach (var npc in probe.Server.GetWorldMapInstance().GetNpcs().Where(n => n != caster && !n.IsDead() &&
			NaturalHostility.IsAggressive(n.GetObjectTemplate(), fixture.DataManager.StaticData.TribeRelations, TribeClass.PC_DARK) &&
			NaturalFlightPolicy.Distance(spot, new(n.GetX(), n.GetY(), n.GetZ(), 0)) < 100).ToArray())
			fixture.World.Despawn(npc);
		Console.WriteLine($"RC-11 labelled free probe 251: fresh native Abija {caster.GetObjectId()}, cleared neighbours, setup teleport, low MP and native hate; no natural character changed.");
		session.Api.World.BeginWorldReload();
		await TeleportForSetupAsync(session, probe.Server, 220030000, spot.X, spot.Y, spot.Z, token);
		session.AcceptTeleportPosition();
		probe.Server.GetLifeStats().SetCurrentMp(probe.Server.GetLifeStats().GetMaxMp() / 4);
		caster.GetAggroList().AddHate(probe.Server, 1);
		await session.SynchronizeAsync(token);
		int packetStart = session.PacketHistory.Count;
		Assert.Equal(typeof(SM_SKILL_CANCEL), (await ReturnAsync()).PacketType);
		await session.SynchronizeAsync(token);
		var incoming = new HashSet<int>();
		NaturalCombatRetreatPolicy.ObserveEngagement(incoming, session.PacketHistory.Skip(packetStart),
			session.CharacterId, hostileSkill: probe.Runtime.IsHostileSkill);
		Assert.Contains(caster.GetObjectId(), incoming);
		Assert.Contains(session.PacketHistory.Skip(packetStart), p => p.PacketType == typeof(SM_CASTSPELL_RESULT) &&
			p.Get<int>("effectorId") == caster.GetObjectId() && p.Get<int>("targetId") == session.CharacterId);
		var completed = session.Api.World.CompletedQuestCounts.OrderBy(p => p.Key).ToArray();
		var skills = session.Api.World.Skills.Values.OrderBy(s => s.SkillId).ToArray();
		// The diagnostic rests before invoking this callback. It must detect and kill
		// the native caster through rest defence, rather than explicitly pulling it here.
		var recovery = await new NaturalIshalgenJourney(session, probe.Runtime, new()).RunObservedCombatAsync(_ =>
		{
			Assert.True(caster.IsDead(), "Natural rest ignored the caster that interrupted Return.");
			return Task.FromResult(caster.GetObjectId());
		}, token);
		Assert.Equal(0, recovery.Deaths);
		Assert.Equal(typeof(SM_CASTSPELL_RESULT), (await ReturnAsync()).PacketType);
		Assert.Equal(completed, session.Api.World.CompletedQuestCounts.OrderBy(p => p.Key).ToArray());
		Assert.Equal(skills, session.Api.World.Skills.Values.OrderBy(s => s.SkillId).ToArray());
		Assert.DoesNotContain(caster.GetObjectId(), session.Api.World.Objects.Keys);
		Assert.True(NaturalFlightPolicy.Distance(spot, session.CurrentPosition) > 200);
		Console.WriteLine($"RC-11 native Return cancelled by Magic Missile, rest defence killed Abija, ordinary Return reached fortress; deaths {recovery.Deaths}, journal/skills retained.");
		policy.AssertClean();

		async Task<DecodedBotServerPacket> ReturnAsync()
		{
			session.BeginStep("probe-native-return", "ordinary-return-cast-under-observed-native-caster");
			var learned = session.Api.World.Skills[243];
			TimeSpan gate = session.Api.Timing.TimeUntilCast(243);
			if (gate > TimeSpan.Zero) await session.AdvanceAsync(gate + TimeSpan.FromMilliseconds(1), token);
			await session.SendPacketAsync(session.Api.Target(session.CharacterId), token);
			await session.SendPacketAsync(session.Api.Cast(new SpellCastData(243, checked((byte)learned.Level), 0)
				{ TargetObjectId = session.CharacterId }), token);
			var started = await BotCastProtocol.WaitForStartAsync((predicate, waitToken) => session.WaitForPacketAsync(
				p => predicate(p) || BotCastProtocol.IsStartRejection(p), waitToken), session.CharacterId, 243, token);
			Assert.Equal(typeof(SM_CASTSPELL), started.PacketType);
			var visible = session.Api.World.SnapshotObjects();
			session.Api.World.BeginWorldReload();
			await session.AdvanceAsync(TimeSpan.FromMilliseconds(started.Get<ushort>("castDuration") + 1), token);
			var result = await BotCastProtocol.WaitForCompletionAsync(session.WaitForPacketAsync, session.CharacterId, 243, token);
			if (result.PacketType == typeof(SM_SKILL_CANCEL)) session.Api.World.RestoreObjects(visible);
			else
			{
				await session.AdvanceAsync(TimeSpan.FromMilliseconds(result.Get<ushort>("hitTime") + 1), token);
				await session.WaitForPacketAsync(typeof(SM_PLAYER_INFO), token, p => p.Get<int>("objectId") == session.CharacterId);
				session.AcceptTeleportPosition();
			}
			await session.SynchronizeAsync(token);
			return result;
		}
	}
}
