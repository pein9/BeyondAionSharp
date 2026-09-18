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
	private static async Task<int> RunE3Async(LiveBotOptions options, LiveBotProblemWriter problems, CancellationToken cancellationToken)
	{
		await using var subject = new L0Actor(options, problems, 1, Race.ELYOS, characterName: "Aelivevendor");
		await using var director = new L0Actor(options, problems, 99, Race.ELYOS,
			bot: "gm", account: LiveGmFacade.DirectorAccount, characterName: "Director");
		try
		{
			foreach (L0Actor actor in new[] { subject, director })
			{
				actor.Trace.WriteAction("s00", "scenario:start", new Dictionary<string, object?> { ["scenario"] = "E3" });
				await actor.StepAsync("login-game-auth", actor.Session.LoginAndAuthenticateAsync, cancellationToken);
				await actor.StepAsync("create-character", actor.Session.CreateCharacterAsync, cancellationToken);
				await actor.StepAsync("enter-world", actor.Session.EnterWorldAsync, cancellationToken);
			}
			await VendorScenario.RunAsync(new LiveVendorDriver(subject, director, director.Session.CreateLiveGmFacade()), cancellationToken);
			foreach (L0Actor actor in new[] { subject, director })
			{
				await actor.StepAsync("quit", actor.Session.QuitAsync, cancellationToken);
				actor.Trace.WriteAction(actor.LastStep, "scenario:complete", new Dictionary<string, object?> { ["scenario"] = "E3" });
			}
			Console.WriteLine("LIVE E3 vendor buy/sell/repurchase passed.");
			return 0;
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
		catch (Exception exception)
		{
			Console.Error.WriteLine($"E3 failed: {exception}");
			return 1;
		}
	}

	private sealed class LiveVendorDriver(L0Actor subject, L0Actor director, LiveGmFacade gm) : IVendorScenarioDriver
	{
		public BotApi Api => subject.Session.Api;
		public long BasePrice { get; } = VendorScenario.ReadBasePrice();
		public Task StepAsync(string action, Func<CancellationToken, Task> operation, CancellationToken token) =>
			subject.StepAsync(action, operation, token);
		public async Task<int> PrepareAsync(CancellationToken token)
		{
			var point = VendorScenario.Position;
			await MoveSubjectWithDirectorAsync(director, subject, gm, 210010000, point.X - 5, point.Y, point.Z, "setup-vendor-position", token);
			int npc = await subject.Session.WaitForNearestObjectExceptAsync(BotKnownObjectKind.Npc, VendorScenario.VendorId, new HashSet<int>(), token);
			await subject.Session.MoveToKnownObjectAsync(npc, token);
			return npc;
		}
		public Task SendAsync(BotClientPacket packet, CancellationToken token) => subject.Session.SendPacketAsync(packet, token);
		public Task<DecodedBotServerPacket> WaitAsync(Type type, Func<DecodedBotServerPacket, bool> predicate, CancellationToken token) =>
			subject.Session.WaitForPacketAsync(type, token, predicate);
		public async Task SynchronizeAsync(CancellationToken token)
		{
			await SendAsync(GameClientPackets.TimeCheck(unchecked((int)Environment.TickCount64)), token);
			await WaitAsync(typeof(SM_TIME_CHECK), _ => true, token);
		}
		public Task VerifyServerStateAsync(IReadOnlyDictionary<int, long> expected, CancellationToken token)
		{
			// Client packets are E3's LIVE oracle; the server-side storage cross-check is added in P8-04.
			if (Api.Timing.BlockingActivities.Count != 0)
				throw new InvalidDataException("Vendor transaction left a client interaction active.");
			return Task.CompletedTask;
		}
	}
}
