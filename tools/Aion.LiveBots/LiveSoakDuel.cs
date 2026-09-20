using System.Diagnostics;
using Aion.Bots.Scenarios;
using Aion.GameServer.Model;

namespace Aion.LiveBots;

public static partial class LiveBotRunner
{
	private static async Task SoakDuelAsync(L0Actor winner, L0Actor loser, Race race, CancellationToken token)
	{
		await Task.WhenAll(winner.Session.SynchronizeAsync(token), loser.Session.SynchronizeAsync(token));
		var beforeWinner = SoakTotals(winner.Session.Api.World);
		var beforeLoser = SoakTotals(loser.Session.Api.World);
		foreach (var actor in new[] { winner, loser })
		{
			var world = actor.Session.Api.World;
			if (world.IsDead || world.MaxHp <= 0 || world.MaxMp <= 0 || world.DuelOpponentId != null || world.GroupId != null)
				throw new InvalidDataException("Soak duel requires alive, ungrouped subjects with known life stats and no existing duel.");
		}
		long recovery = Stopwatch.GetTimestamp();
		await SocialBasicsScenario.RecoverForDuelAsync(new LiveSocialDriver(winner), new LiveSocialDriver(loser), token);
		foreach (var actor in new[] { winner, loser })
			actor.Trace.WriteAction(actor.LastStep, "soak:duel-recovered", new Dictionary<string, object?>
			{ ["seconds"] = Stopwatch.GetElapsedTime(recovery).TotalSeconds, ["hp"] = actor.Session.Api.World.CurrentHp, ["mp"] = actor.Session.Api.World.CurrentMp });
		await SocialBasicsScenario.RunDuelAsync(new LiveSocialDriver(winner), new LiveSocialDriver(loser), race, token);
		await Task.WhenAll(winner.Session.SynchronizeAsync(token), loser.Session.SynchronizeAsync(token));
		SoakAssertInventory(winner.Session.Api.World, beforeWinner);
		SoakAssertInventory(loser.Session.Api.World, beforeLoser);
		foreach (var actor in new[] { winner, loser })
		{
			if (actor.Session.Api.Timing.BlockingActivities.Count != 0)
				throw new InvalidDataException("Duel left a blocking action.");
			actor.Trace.WriteAction(actor.LastStep, "soak:duel-outcome", new Dictionary<string, object?>
			{ ["winner"] = winner.Session.CharacterId, ["loser"] = loser.Session.CharacterId, ["hp"] = actor.Session.Api.World.CurrentHp, ["mp"] = actor.Session.Api.World.CurrentMp });
		}
	}
}
