namespace Aion.Bots.Scenarios;

/// <summary>Transport-neutral actor contract for the shared L0 scenario body.</summary>
public interface IL0ScenarioActor
{
	string Bot { get; }
	IL0ScenarioSession Session { get; }
	Task StepAsync(
		string action,
		Func<IL0ScenarioSession, CancellationToken, Task> operation,
		CancellationToken cancellationToken);
}

/// <summary>
/// The intent-level L0 operations. LIVE and SIM provide different transport/lifecycle plumbing while the ordered
/// scenario body remains identical; chat operations are invoked only for LIVE.
/// </summary>
public interface IL0ScenarioSession
{
	Task LoginAndAuthenticateAsync(CancellationToken cancellationToken);
	Task CreateCharacterAsync(CancellationToken cancellationToken);
	Task EnterWorldAsync(CancellationToken cancellationToken);
	Task ChangeChannelAsync(int channel, CancellationToken cancellationToken);
	Task ConnectChatAsync(CancellationToken cancellationToken);
	Task SendChatMessageAsync(string message, CancellationToken cancellationToken);
	Task ReceiveChatMessageAsync(string expected, CancellationToken cancellationToken);
	Task WalkTenMetersAsync(CancellationToken cancellationToken);
	Task PingAsync(CancellationToken cancellationToken);
	Task QuitAsync(CancellationToken cancellationToken);
	Task VerifyOfflineAsync(CancellationToken cancellationToken);
	Task WaitForReentryAsync(CancellationToken cancellationToken);
	Task ReloginAndVerifyPersistenceAsync(CancellationToken cancellationToken);
}

public static class L0Scenario
{
	public const string ChatMessage = "L0 channel delivery";

	public static async Task RunAsync(
		IReadOnlyList<IL0ScenarioActor> actors,
		int? channel,
		bool includeChat,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(actors);
		if (actors.Count < 2)
			throw new ArgumentException("L0 requires at least two actors.", nameof(actors));
		if (actors.Select(actor => actor.Bot).Distinct(StringComparer.Ordinal).Count() != actors.Count)
			throw new ArgumentException("L0 actor ids must be distinct.", nameof(actors));

		await AllAsync(actors, "login-game-auth", (session, token) => session.LoginAndAuthenticateAsync(token), cancellationToken);
		await AllAsync(actors, "create-elyos-warrior", (session, token) => session.CreateCharacterAsync(token), cancellationToken);
		await AllAsync(actors, "enter-world", (session, token) => session.EnterWorldAsync(token), cancellationToken);
		if (channel != null)
			await AllAsync(actors, "isolate-channel", (session, token) => session.ChangeChannelAsync(channel.Value, token), cancellationToken);

		if (includeChat)
		{
			await AllAsync(actors, "chat-auth-and-region-join", (session, token) => session.ConnectChatAsync(token), cancellationToken);
			await actors[0].StepAsync("send-region-message",
				(session, token) => session.SendChatMessageAsync(ChatMessage, token), cancellationToken);
			await Task.WhenAll(actors.Skip(1).Select(actor => actor.StepAsync("receive-region-message",
				(session, token) => session.ReceiveChatMessageAsync(ChatMessage, token), cancellationToken)));
		}

		await actors[0].StepAsync("walk-10m", (session, token) => session.WalkTenMetersAsync(token), cancellationToken);
		await actors[0].StepAsync("ping", (session, token) => session.PingAsync(token), cancellationToken);
		await AllAsync(actors, "quit", (session, token) => session.QuitAsync(token), cancellationToken);
		await actors[0].StepAsync("verify-offline", (session, token) => session.VerifyOfflineAsync(token), cancellationToken);
		await actors[0].StepAsync("wait-reentry", (session, token) => session.WaitForReentryAsync(token), cancellationToken);
		await actors[0].StepAsync("relogin-character-list",
			(session, token) => session.ReloginAndVerifyPersistenceAsync(token), cancellationToken);
	}

	private static Task AllAsync(
		IReadOnlyList<IL0ScenarioActor> actors,
		string action,
		Func<IL0ScenarioSession, CancellationToken, Task> operation,
		CancellationToken cancellationToken) => Task.WhenAll(
		actors.Select(actor => actor.StepAsync(action, operation, cancellationToken)));
}
