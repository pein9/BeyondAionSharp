using Aion.Bots.Gm;
using Aion.Bots.Scenarios;
using Aion.GameServer.Model;

namespace Aion.LiveBots;

public static partial class LiveBotRunner
{
	/// <summary>
	/// NA-21: supplies the approved help items (OD-13) on the isolated LIVE stack through the seeded director's
	/// <c>//add &lt;player&gt; &lt;itemId&gt; &lt;count&gt;</c> (access level 8). This leaves the director's trace step,
	/// the server's gmaudit line and the subject's "received" message. The director logs in on first use. It is
	/// never built for the operator's own world: only the isolated natural profile may supply.
	/// </summary>
	private sealed class LiveNaturalHelpItemSupplier(LiveBotOptions options, LiveBotProblemWriter problems,
		string subjectCharacterName) : IAsyncDisposable
	{
		private L0Actor? director;
		private LiveGmFacade? gm;

		public static LiveNaturalHelpItemSupplier? CreateFor(LiveBotOptions options, LiveBotProblemWriter problems,
			string subjectCharacterName)
		{
			if (!NaturalHelpItemSupply.Enabled(Environment.GetEnvironmentVariable(NaturalHelpItemSupply.Switch))) return null;
			if (options.Profile != "docker-bots-natural")
				throw new InvalidOperationException("Help items are supplied only on the isolated docker-bots-natural stack (OD-13).");
			return new LiveNaturalHelpItemSupplier(options, problems, subjectCharacterName);
		}

		public async Task SupplyAsync(int itemId, long count, CancellationToken token)
		{
			NaturalHelpItemSupply.RequireApproved(itemId, count);
			if (gm == null)
			{
				director = new L0Actor(options, problems, 99, Race.ELYOS, bot: "gm", account: LiveGmFacade.DirectorAccount,
					characterName: "Director");
				await director.StepAsync("login-game-auth", director.Session.LoginAndAuthenticateAsync, token);
				await director.StepAsync("create-character", director.Session.CreateCharacterAsync, token);
				await director.StepAsync("enter-world", director.Session.EnterWorldAsync, token);
				gm = director.Session.CreateLiveGmFacade();
			}
			await director!.StepAsync($"supply-help-item-{itemId}", ct => gm.ExecuteAsync(
				new GmCommand("add", [subjectCharacterName, itemId.ToString(), count.ToString()], "You gave"), cancellationToken: ct), token);
		}

		public async ValueTask DisposeAsync()
		{
			if (director != null) await director.DisposeAsync();
		}
	}
}
