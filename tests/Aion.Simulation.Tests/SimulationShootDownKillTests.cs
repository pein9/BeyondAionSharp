using Aion.Bots.Protocol;
using Aion.Bots.World;
using Aion.Bots.Navigation;
using Aion.Bots.Scenarios;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Services;

namespace Aion.Simulation.Tests;

public sealed partial class SimulationFastScenarioTests
{
	/// <summary>
	/// NaturalAirCombat.ShootDownAsync reports a kill only when the server's death of the target (0% HP) or, for a real quest,
	/// the quest's counter was observed. With quest id 0 it used to report a kill after the first cast whatever happened:
	/// QuestStatus(0) is 0, so "the status left START" always held, and run-fast's AB-06 probe got a "dead" Komu who was alive.
	/// GM setup on the probe only: its class, level and skills, and the target's HP.
	/// </summary>
	[SkippableFact]
	public async Task ShootDownReportsAKillOnlyWhenTheTargetDies()
	{
		Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
		const int altgard = 220030000, lakeSpirit = 210660;
		using var policy = NewPolicy("SHOOT", includeHistory: false);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
		CancellationToken token = timeout.Token;
		await using var session = new SimulationL0Session(fixture, policy, "b01", 71, "Asimshoot", Race.ASMODIANS);
		session.BeginStep("s00", "login-create-enter-and-setup");
		await session.LoginAndAuthenticateAsync(token);
		await session.CreateCharacterAsync(token, PlayerClass.PRIEST);
		await session.EnterWorldAsync(token);
		await session.SynchronizeAsync(token);
		Player Server() => fixture.World.GetPlayer(session.CharacterId);
		Assert.True(ClassChangeService.SetClass(Server(), PlayerClass.CLERIC, validate: false, updateDaevaStatus: true));
		Server().GetCommonData().SetLevel(16);
		SkillLearnService.LearnNewSkills(Server(), 1, 16);
		var instance = fixture.World.GetWorldMap(altgard).GetMainWorldMapInstance();
		BotNavigationGeometry geometry = BotNavigationGeometry.ForServerWorld(instance.GetInstanceId(), Race.ASMODIANS);
		var runtime = new NaturalJourneyRuntime(Aion.GameServer.TestKit.RealStaticData.RepoRoot(), "SIM-shoot", fixture.Seed, fixture.DataManager.StaticData,
			() => fixture.Clock.NowMillis, fixture.Epoch, () => geometry, _ => Task.FromResult(false), policy.AssertClean,
			() => policy.SnapshotProblems(), null!, new Aion.Bots.Dashboard.LiveBotDashboardState());
		var target = instance.GetNpcs().Where(npc => npc.GetNpcId() == lakeSpirit && !npc.IsDead())
			.OrderBy(npc => MathF.Pow(npc.GetX() - 1695f, 2) + MathF.Pow(npc.GetY() - 520f, 2)).First();
		BotPosition near = geometry.GroundAround(altgard, new BotPosition(target.GetX(), target.GetY(), target.GetZ(), 0), [12f, 15f, 10f])
			.First(point => geometry.HasLineOfSight(altgard, point, new BotPosition(target.GetX(), target.GetY(), target.GetZ() + 1, 0)));
		await TeleportForSetupAsync(session, Server(), altgard, near.X, near.Y, near.Z, token);
		session.AcceptTeleportPosition();
		await session.SynchronizeAsync(token);
		await session.WaitForNpcAsync(lakeSpirit, token);
		SpellCastData Cast(BotPosition origin, ushort skill, byte skillLevel, int aim) =>
			runtime.CreateSpellCast(session.Api.World, origin, skill, skillLevel, aim);

		// One Smite at a full-health spirit with no quest: it survives, and no kill is reported.
		session.BeginStep("s01", "one-cast-at-full-health");
		target.GetLifeStats().SetCurrentHp(target.GetLifeStats().GetMaxHp());
		bool reported = await NaturalAirCombat.ShootDownAsync(session, target.GetObjectId(), 0, Cast, token, maximumCasts: 1);
		await session.SynchronizeAsync(token);
		Assert.False(target.IsDead(), "One Smite killed a full-health lake spirit; the test needs a target that survives it.");
		Assert.False(reported, "ShootDownAsync reported a kill of a lake spirit that is alive.");

		// At 1 HP the next Smite kills it, and that is reported.
		session.BeginStep("s02", "kill-at-one-hp");
		target.GetLifeStats().SetCurrentHp(1);
		await session.AdvanceAsync(TimeSpan.FromSeconds(3), token);
		reported = await NaturalAirCombat.ShootDownAsync(session, target.GetObjectId(), 0, Cast, token, maximumCasts: 4);
		await session.SynchronizeAsync(token);
		Assert.True(target.IsDead(), "The lake spirit at 1 HP did not die.");
		Assert.True(reported, "ShootDownAsync did not report the kill it made.");
		policy.AssertClean();
	}
}
