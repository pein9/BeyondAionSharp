using Aion.Bots.Api;
using Aion.Bots.Protocol;
using Aion.Bots.World;
using Aion.GameServer.Model;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.Bots.Scenarios;

public interface IPvpFlightDriver
{
	BotApi Api { get; }
	int CharacterId { get; }
	BotPosition CurrentPosition { get; }
	Task StepAsync(string action, Func<CancellationToken, Task> operation, CancellationToken token);
	Task SendAsync(BotClientPacket packet, CancellationToken token);
	Task<DecodedBotServerPacket> WaitAsync(Type type, Func<DecodedBotServerPacket, bool> predicate, CancellationToken token);
	Task SynchronizeAsync(CancellationToken token);
	Task DelayAsync(TimeSpan duration, CancellationToken token);
	Task FlyAsync(BotPosition destination, CancellationToken token);
	Task VerifyPvpAsync(bool winner, CancellationToken token);
}

/// <summary>S2: two ungrouped, access-zero, level-ten Sorcerers fly to an airborne cross-race encounter.</summary>
public static partial class PvpFlightScenario
{
	public const int MapId = 400010000;
	public const int InitialAp = 500;
	// Open air within Wings of Siel Archipelago's 700..1900 altitude band. No geodata/ground-path claim.
	public static readonly BotPosition ElyosStart = new(3130, 2400, 1800, 0);
	public static readonly BotPosition AsmodianStart = new(3270, 2400, 1800, 60);
	public static readonly BotPosition ElyosEnd = new(3195, 2400, 1810, 0);
	public static readonly BotPosition AsmodianEnd = new(3205, 2400, 1810, 60);

	public static async Task RunAsync(IPvpFlightDriver elyos, IPvpFlightDriver asmodian, CancellationToken token)
	{
		await SyncAsync(token);
		foreach (var actor in new[] { elyos, asmodian })
			Require(actor.Api.World.MapId == MapId && actor.Api.World.Level == 10 && actor.Api.World.GroupId == null &&
				actor.Api.World.AbyssRank is { Ap: InitialAp, Rank: 1, AllKills: 0 }, "S2 requires fresh equal-level, rank-nine solo subjects with 500 AP.");
		var beforeWinner = elyos.Api.World.AbyssRank!;
		var beforeVictim = asmodian.Api.World.AbyssRank!;
		await BothStepAsync("fly-to-cross-race-encounter", async ct =>
		{
			foreach (var (actor, endpoint) in new[] { (elyos, ElyosEnd), (asmodian, AsmodianEnd) })
			{
				int beforeFp = actor.Api.World.CurrentFlightTime;
				await actor.SendAsync(actor.Api.Fly(), ct);
				await actor.WaitAsync(typeof(SM_EMOTION), packet => packet.Get<int>("senderObjectId") == actor.CharacterId &&
					packet.Get<byte>("emotionType") == (byte)EmotionType.FLY, ct);
				await actor.FlyAsync(endpoint, ct);
				await actor.SynchronizeAsync(ct);
				Require(actor.Api.World.CurrentFlightTime > 0 && actor.Api.World.CurrentFlightTime < beforeFp, "Flight did not consume ordinary flight time.");
				Require(Distance(actor.CurrentPosition, endpoint) < 0.1, "Flight did not reach the encounter point.");
			}
			await SyncAsync(ct);
			Require(elyos.Api.World.Objects.TryGetValue(asmodian.CharacterId, out var seenAsmodian) && seenAsmodian.Race == (byte)Race.ASMODIANS &&
				asmodian.Api.World.Objects.TryGetValue(elyos.CharacterId, out var seenElyos) && seenElyos.Race == (byte)Race.ELYOS,
				"Opposite-race clients did not perceive each other after flight.");
		}, token);
		await BothStepAsync("ordinary-pvp-kill-and-abyss-rewards", async ct =>
		{
			await KillAsync(elyos, asmodian, Race.ELYOS, 1, ct);
			Require(elyos.Api.World.CurrentFlightTime > 0, "Attacker ran out of flight time before a PvP kill.");
			// Equal levels/rank-nine, one damage source, ordinary account rates: +300 AP / -90 AP.
			// These pin the configured contract, not the production reward function as its own oracle.
			var win = elyos.Api.World.AbyssRank!;
			var loss = asmodian.Api.World.AbyssRank!;
			SoloPvpRewardContract.AssertMatches(SoloPvpRewardContract.Predict(beforeWinner, beforeVictim,
				10, 10, elyos.Api.World.Level, killsAgainstVictimInWindow: 1), win, loss);
			Require(win.Ap == beforeWinner.Ap + 300 && loss.Ap == beforeVictim.Ap - 90, $"Wrong PvP AP deltas: {win.Ap - beforeWinner.Ap}/{loss.Ap - beforeVictim.Ap}.");
			Require(win.AllKills == 1 && win.Daily.Kills == beforeWinner.Daily.Kills + 1 && win.Weekly.Kills == beforeWinner.Weekly.Kills + 1 && loss.AllKills == 0,
				"PvP kill counters did not update exactly once.");
			Require(win.Daily.Ap == beforeWinner.Daily.Ap + 300 && win.Weekly.Ap == beforeWinner.Weekly.Ap + 300 &&
				loss.Daily == beforeVictim.Daily && loss.Weekly == beforeVictim.Weekly, "Earned-AP period counters changed incorrectly.");
			await elyos.VerifyPvpAsync(true, ct);
			await asmodian.VerifyPvpAsync(false, ct);
		}, token);

		Task BothStepAsync(string action, Func<CancellationToken, Task> operation, CancellationToken ct) =>
			elyos.StepAsync(action, inner => asmodian.StepAsync(action, operation, inner), ct);
		async Task SyncAsync(CancellationToken ct) { await elyos.SynchronizeAsync(ct); await asmodian.SynchronizeAsync(ct); }
	}

	private static double Distance(BotPosition a, BotPosition b) => Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2) + Math.Pow(a.Z - b.Z, 2));
	private static void Require(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
}
