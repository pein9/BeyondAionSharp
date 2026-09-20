using Aion.Bots.Protocol;
using Aion.GameServer.Model;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.Bots.Scenarios;

public static partial class PvpFlightScenario
{
	/// <summary>Ordinary solo combat with an independent AP/counter oracle. Callers position, recover and revive.</summary>
	public static async Task KillAsync(IPvpFlightDriver winner, IPvpFlightDriver victim, Race casterRace,
		int killsAgainstVictimInWindow, CancellationToken token)
	{
		await SyncAsync();
		foreach (var actor in new[] { winner, victim })
			Require(actor.Api.World.MapId == MapId && !actor.Api.World.IsDead && actor.Api.World.GroupId == null &&
				actor.Api.World.DuelOpponentId == null && actor.Api.World.AbyssRank != null, "PvP requires living, solo, non-duelling Reshanta subjects with observed rank.");
		Require(winner.Api.World.Objects.TryGetValue(victim.CharacterId, out var seenVictim) &&
			victim.Api.World.Objects.TryGetValue(winner.CharacterId, out var seenWinner) && seenVictim.Race != seenWinner.Race,
			"Cross-race opponents must perceive each other before combat.");
		var beforeWinner = winner.Api.World.AbyssRank!;
		var beforeVictim = victim.Api.World.AbyssRank!;
		int winnerLevel = winner.Api.World.Level, victimLevel = victim.Api.World.Level;
		Require(winner.Api.World.Skills.TryGetValue(1282, out var flameBolt), "Missing learned Flame Bolt.");
		await winner.SendAsync(winner.Api.Target(victim.CharacterId), token);
		for (int cast = 0; cast < 60 && !victim.Api.World.IsDead; cast++)
		{
			await winner.SendAsync(winner.Api.Cast(new SpellCastData(1282, checked((byte)flameBolt!.Level), 0)
			{
				TargetObjectId = victim.CharacterId,
				HitTime = SocialBasicsScenario.DuelHitTime(winner.CurrentPosition, victim.CurrentPosition, casterRace),
			}), token);
			var started = await winner.WaitAsync(typeof(SM_CASTSPELL), packet => packet.Get<int>("objectId") == winner.CharacterId && packet.Get<ushort>("spellId") == 1282, token);
			await winner.DelayAsync(TimeSpan.FromMilliseconds(started.Get<ushort>("castDuration") + 1), token);
			var result = await winner.WaitAsync(typeof(SM_CASTSPELL_RESULT), packet => packet.Get<int>("effectorId") == winner.CharacterId && packet.Get<ushort>("skillId") == 1282, token);
			await winner.DelayAsync(TimeSpan.FromMilliseconds(Math.Max(2000, result.Get<ushort>("hitTime") + 1)), token);
			await SyncAsync();
			Require(!winner.Api.World.IsDead, "PvP attacker died before defeating the expected victim.");
		}
		Require(victim.Api.World.IsDead && !winner.Api.World.IsDead, "Cross-race combat did not produce the expected actual death.");
		SoloPvpRewardContract.AssertMatches(SoloPvpRewardContract.Predict(beforeWinner, beforeVictim,
			winnerLevel, victimLevel, winner.Api.World.Level, killsAgainstVictimInWindow),
			winner.Api.World.AbyssRank!, victim.Api.World.AbyssRank!);
		async Task SyncAsync() { await winner.SynchronizeAsync(token); await victim.SynchronizeAsync(token); }
	}
}
