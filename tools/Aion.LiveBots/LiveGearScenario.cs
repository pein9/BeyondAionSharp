using Aion.Bots.Api;
using Aion.Bots.Gm;
using Aion.Bots.Protocol;
using Aion.Bots.Scenarios;
using Aion.Bots.World;
using Aion.GameServer.Model;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.LiveBots;

public static partial class LiveBotRunner
{
	private static async Task<int> RunG1Async(LiveBotOptions options, LiveBotProblemWriter problems, CancellationToken token)
		=> await RunGearAsync(options, problems, godstone: false, token);
	private static async Task<int> RunG2Async(LiveBotOptions options, LiveBotProblemWriter problems, CancellationToken token)
		=> await RunGearAsync(options, problems, godstone: true, token);
	private static async Task<int> RunG3Async(LiveBotOptions options, LiveBotProblemWriter problems, CancellationToken token)
		=> await RunGearAsync(options, problems, godstone: false, token, fusion: true);
	private static async Task<int> RunG4Async(LiveBotOptions options, LiveBotProblemWriter problems, CancellationToken token)
		=> await RunGearAsync(options, problems, godstone: false, token, binding: true);
	private static async Task<int> RunG5Async(LiveBotOptions options, LiveBotProblemWriter problems, CancellationToken token)
		=> await RunGearAsync(options, problems, godstone: false, token, upgrade: true);
	private static async Task<int> RunG6Async(LiveBotOptions options, LiveBotProblemWriter problems, CancellationToken token)
		=> await RunGearAsync(options, problems, godstone: false, token, utility: true);
	private static async Task<int> RunGearAsync(LiveBotOptions options, LiveBotProblemWriter problems, bool godstone, CancellationToken token, bool fusion = false, bool binding = false, bool upgrade = false, bool utility = false)
	{
		string scenario = utility ? "G6" : upgrade ? "G5" : binding ? "G4" : fusion ? "G3" : godstone ? "G2" : "G1";
		await using var subject = new L0Actor(options, problems, 1, Race.ELYOS, characterName: "Aelivegear");
		await using var director = new L0Actor(options, problems, 99, Race.ELYOS,
			bot: "gm", account: LiveGmFacade.DirectorAccount, characterName: "Director");
		try
		{
			foreach (var actor in new[] { subject, director })
			{
				actor.Trace.WriteAction("s00", "scenario:start", new Dictionary<string, object?> { ["scenario"] = scenario });
				await actor.StepAsync("login-game-auth", actor.Session.LoginAndAuthenticateAsync, token);
				await actor.StepAsync("create-character", actor.Session.CreateCharacterAsync, token);
				await actor.StepAsync("enter-world", actor.Session.EnterWorldAsync, token);
			}
			var driver = new LiveGearDriver(subject, director, director.Session.CreateLiveGmFacade(), godstone, fusion, binding, upgrade, utility);
			if (utility) await InventoryUtilityScenario.RunAsync(driver, token);
			else if (upgrade) await GearUpgradeScenario.RunAsync(driver, token);
			else if (binding) await SoulBindStigmaScenario.RunAsync(driver, token);
			else if (fusion) await WeaponFusionScenario.RunAsync(driver, token);
			else if (godstone) await GodstoneScenario.RunAsync(driver, token);
			else await GearSocketScenario.RunAsync(driver, token);
			foreach (var actor in new[] { subject, director })
			{
				await actor.StepAsync("quit", actor.Session.QuitAsync, token);
				actor.Trace.WriteAction(actor.LastStep, "scenario:complete", new Dictionary<string, object?> { ["scenario"] = scenario });
			}
			Console.WriteLine($"LIVE {scenario} gear progression and relog persistence passed.");
			return 0;
		}
		catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
		catch (Exception exception)
		{
			Console.Error.WriteLine($"{scenario} failed: {exception}");
			return 1;
		}
	}

	private sealed class LiveGearDriver(L0Actor subject, L0Actor director, LiveGmFacade gm, bool godstone, bool fusion, bool binding, bool upgrade, bool utility) : IGodstoneDriver, IGearUpgradeDriver
	{
		public BotApi Api => subject.Session.Api;
		public IReadOnlyList<DecodedBotServerPacket> History => subject.Session.PacketHistory;
		public Task StepAsync(string action, Func<CancellationToken, Task> operation, CancellationToken token) => subject.StepAsync(action, operation, token);
		public async Task<int> PrepareAsync(CancellationToken token)
		{
			var point = utility ? InventoryUtilityScenario.Position : upgrade ? GearUpgradeScenario.Position : fusion ? WeaponFusionScenario.Position : godstone ? GodstoneScenario.Position : GearSocketScenario.Position;
			await MoveSubjectWithDirectorAsync(director, subject, gm, godstone ? GodstoneScenario.MapId : GearSocketScenario.MapId,
				point.X - 5, point.Y, point.Z, "setup-gear-position", token);
			string level = utility || upgrade ? "65" : binding ? "20" : "16";
			await gm.ExecuteVerifiedAsync(new GmCommand("set", ["class", "gladiator"], "replyless class change"),
				new GmCommand("set", ["level", level], "level to " + level), new GmSubject(subject.Session.CharacterId, subject.Session.CharacterName), token);
			var grants = utility ? InventoryUtilityScenario.Grants.ToArray() : upgrade ? GearUpgradeScenario.Grants.ToArray()
				: binding ? new[] { (BotWorldModel.KinahItemId, 100_000), (SoulBindStigmaScenario.WeaponId, 1), (SoulBindStigmaScenario.StigmaId, 1) }
				: fusion ? new[] { (BotWorldModel.KinahItemId, 100_000), (WeaponFusionScenario.WeaponId, 2), (GearSocketScenario.ManastoneId, WeaponFusionScenario.StoneCount) }
				: godstone ? new[] { (BotWorldModel.KinahItemId, 100_000), (GodstoneScenario.WeaponId, 1), (GodstoneScenario.GodstoneId, 1) }
				: new[] { (BotWorldModel.KinahItemId, 100_000), (GearSocketScenario.WeaponId, GearSocketScenario.WeaponCount),
					(GearSocketScenario.ManastoneId, GearSocketScenario.StoneCount), (GearSocketScenario.EnchantStoneId, GearSocketScenario.StoneCount) };
			foreach (var (id, count) in grants)
				await gm.ExecuteAsync(new GmCommand("add", [subject.Session.CharacterName, id.ToString(System.Globalization.CultureInfo.InvariantCulture),
					count.ToString(System.Globalization.CultureInfo.InvariantCulture)], "You gave"), cancellationToken: token);
			if (utility) return await ApproachServiceAsync(InventoryUtilityScenario.NpcId, token);
			if (upgrade) return await ApproachServiceAsync(GearUpgradeScenario.RemodelNpcId, token);
			if (binding)
			{
				await gm.ExecuteAsync(new GmCommand("quest", [subject.Session.CharacterName, SoulBindStigmaScenario.UnlockQuestId.ToString(System.Globalization.CultureInfo.InvariantCulture),
					"set", "COMPLETE", "0"], "Set quest status"), cancellationToken: token);
				return 0;
			}
			if (godstone)
			{
				await director.Session.MoveToPositionAsync(point, token);
				await gm.ExecuteVerifiedAsync(new GmCommand("spawn", [GodstoneScenario.DummyId.ToString(System.Globalization.CultureInfo.InvariantCulture)], "replyless temporary spawn"),
					new GmCommand("coords", [], "'s position:"), cancellationToken: token);
			}
			int npc = await subject.Session.WaitForNearestObjectExceptAsync(BotKnownObjectKind.Npc,
				fusion ? WeaponFusionScenario.NpcId : godstone ? GodstoneScenario.DummyId : GearSocketScenario.RemoverNpcId, new HashSet<int>(), token);
			await subject.Session.MoveToKnownObjectAsync(npc, token);
			return npc;
		}
		public Task SendAsync(BotClientPacket packet, CancellationToken token) => subject.Session.SendPacketAsync(packet, token);
		public async Task<int> ApproachServiceAsync(int npcTemplateId, CancellationToken token)
		{
			int npc = await subject.Session.WaitForNearestObjectExceptAsync(BotKnownObjectKind.Npc, npcTemplateId, new HashSet<int>(), token);
			await subject.Session.MoveToKnownObjectAsync(npc, token);
			return npc;
		}
		public Task<DecodedBotServerPacket> WaitAsync(Type type, Func<DecodedBotServerPacket, bool> predicate, CancellationToken token) =>
			subject.Session.WaitForPacketAsync(type, token, predicate);
		public Task DelayAsync(TimeSpan delay, CancellationToken token) => Task.Delay(delay, token);
		public async Task SynchronizeAsync(CancellationToken token)
		{
			await SendAsync(GameClientPackets.TimeCheck(unchecked((int)Environment.TickCount64)), token);
			await WaitAsync(typeof(SM_TIME_CHECK), _ => true, token);
		}
		public async Task VerifyPersistenceAsync(CancellationToken token)
		{
			var expected = Api.World.Inventory.OrderBy(pair => pair.Key).ToArray();
			await subject.StepAsync("verify-gear-before-logout", subject.Session.VerifyInventoryAsync, token);
			await subject.Session.QuitAsync(token);
			await subject.Session.VerifyOfflineAsync(token);
			await subject.Session.WaitForReentryAsync(token);
			await subject.Session.ReloginAndVerifyPersistenceAsync(token);
			await subject.Session.EnterWorldAsync(token);
			await SynchronizeAsync(token);
			if (!expected.SequenceEqual(Api.World.Inventory.OrderBy(pair => pair.Key)))
				throw new InvalidDataException("Gear inventory changed across a real logout and fresh login.");
			await subject.StepAsync("verify-gear-after-relogin", subject.Session.VerifyInventoryAsync, token);
		}
		// Both modes assert the ten real wire damage notifications and their cessation; SIM also reads the live effect controller.
		public Task VerifyProcAsync(int targetObjectId, bool active, CancellationToken token) => Task.CompletedTask;
	}
}
