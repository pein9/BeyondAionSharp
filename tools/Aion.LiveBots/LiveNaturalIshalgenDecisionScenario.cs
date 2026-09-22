using Aion.Bots.Scenarios;
using Aion.Bots.World;
using Aion.GameServer.Model;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.LiveBots;

public static partial class LiveBotRunner
{
	private static async Task<int> RunNaturalIshalgenDecisionAsync(
		LiveBotOptions options, LiveBotProblemWriter problems, CancellationToken token)
	{
		NaturalIshalgenIdentity identity = NaturalIshalgenIdentityScenario.Identity;
		await using var actor = new L0Actor(options, problems, 1, Race.ASMODIANS, bot: "b01",
			account: identity.AccountName, characterName: identity.CharacterName);
		actor.Trace.WriteAction("s00", "scenario:start", new Dictionary<string, object?> { ["scenario"] = "NI-02" });
		try
		{
			var identityDriver = new LiveNaturalIshalgenIdentityDriver(options, actor, identity);
			NaturalIshalgenIdentityResult subject = await NaturalIshalgenIdentityScenario.EnterAsync(identityDriver, token);
			await WriteNaturalIdentityReceiptAsync(options, identity, subject, token);
			NaturalIshalgenContract contract = NaturalIshalgenContract.LoadDefault();
			var decisionDriver = new LiveNaturalIshalgenDecisionDriver(options, actor);
			NaturalDecision decision = await NaturalIshalgenDecisionLoop.RunAsync(contract, decisionDriver, token: token);
			if (decision.Outcome == "blocked")
				throw new InvalidDataException($"NI-02 decision loop blocked: {decision.Reason}");
			if (options.DecisionViewSeconds > 0)
			{
				actor.Trace.WriteAction(actor.LastStep, "natural:dashboard-view-window", new Dictionary<string, object?>
				{
					["seconds"] = options.DecisionViewSeconds,
				});
				await Task.Delay(TimeSpan.FromSeconds(options.DecisionViewSeconds), token);
			}
			await actor.StepAsync("quit-without-deleting-character", actor.Session.QuitAsync, token);
			actor.Trace.WriteAction(actor.LastStep, "scenario:complete", new Dictionary<string, object?>
			{
				["scenario"] = "NI-02", ["decision"] = decision,
			});
			Console.WriteLine($"LIVE NI-02: retained Priest {identity.CharacterName} ({subject.CharacterId}); " +
				$"selected {decision.SelectedAction} for Q{decision.SelectedQuestId?.ToString() ?? "none"} " +
				$"({decision.Outcome}). No gameplay capability was invented.");
			return 0;
		}
		catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
		catch (Exception exception)
		{
			Console.Error.WriteLine($"NI-02 failed: {exception}");
			return 1;
		}
	}

	private sealed class LiveNaturalIshalgenDecisionDriver(LiveBotOptions options, L0Actor actor)
		: INaturalIshalgenDecisionDriver
	{
		public NaturalIshalgenObservation Observe(bool fresh)
		{
			BotWorldModel world = actor.Session.Api.World;
			return new NaturalIshalgenObservation(fresh,
				actor.Session.PacketHistory.Any(packet => packet.PacketType == typeof(SM_QUEST_LIST)),
				actor.Session.PacketHistory.Any(packet => packet.PacketType == typeof(SM_QUEST_COMPLETED_LIST)),
				world.MapId, world.Level, world.IsDead,
				new Dictionary<int, BotQuestState>(world.Quests), world.CompletedQuestIds.ToHashSet());
		}

		public Task RefreshAsync(CancellationToken token) =>
			actor.StepAsync("synchronize-client-world", actor.Session.SynchronizeAsync, token);

		public void Record(NaturalDecision decision)
		{
			actor.Trace.WriteAction(actor.LastStep, "natural:decision", new Dictionary<string, object?>
			{
				["decision"] = decision,
			});
			options.Dashboard.PublishDecision(actor.Bot, decision);
		}
	}
}
