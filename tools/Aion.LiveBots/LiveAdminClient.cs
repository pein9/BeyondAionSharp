namespace Aion.LiveBots;

/// <summary>Session-owned HTTP pool for the read-only assertion oracle, separate from game traffic.</summary>
internal sealed class LiveAdminClient : IDisposable
{
	// One LIVE run owns this process. Do not turn its assertion reads into an independent
	// HTTP load test: offline common-data lookup nests a quest read in the five-slot DB pool.
	private static readonly SemaphoreSlim OracleReads = new(1, 1);
	private readonly HttpClient client;
	public LiveAdminClient(Uri baseAddress, HttpMessageHandler? handler = null)
	{
		// The managed Linux HttpListener closes a reused connection after 15 seconds
		// without a request. Retire idle oracle connections before that close can race
		// with their next use. Active requests/reuse and the normal request timeout
		// are unchanged; transport failures still propagate without an added retry.
		handler ??= new SocketsHttpHandler { PooledConnectionIdleTimeout = TimeSpan.FromSeconds(10) };
		client = new HttpClient(handler);
		client.BaseAddress = baseAddress;
	}

	public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
	{
		if (request.Method != HttpMethod.Get) throw new InvalidOperationException("The bot assertion oracle is read-only.");
		await OracleReads.WaitAsync(token);
		try { return await client.SendAsync(request, HttpCompletionOption.ResponseContentRead, token); }
		finally { OracleReads.Release(); }
	}
	public void Dispose() => client.Dispose();
}
