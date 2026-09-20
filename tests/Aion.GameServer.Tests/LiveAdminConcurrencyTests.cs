using System.Net;
using Aion.LiveBots;

namespace Aion.GameServer.Tests;

[Collection("LoopbackSockets")]
public sealed class LiveAdminConcurrencyTests
{
	[Fact]
	public async Task DifferentSessionsShareAnOracleGateAndQueuedCancellationDoesNotReleaseIt()
	{
		var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		int entered = 0;
		using var first = Client(async (_, token) => { Interlocked.Increment(ref entered); await release.Task.WaitAsync(token); return Response(); });
		using var second = Client((_, _) => { Interlocked.Increment(ref entered); return Task.FromResult(Response()); });
		using var third = Client((_, _) => { Interlocked.Increment(ref entered); return Task.FromResult(Response()); });
		using var a = Request(); using var b = Request(); using var c = Request();
		using var cancel = new CancellationTokenSource();
		Task<HttpResponseMessage> active = first.SendAsync(a, CancellationToken.None);
		Task<HttpResponseMessage> queued = second.SendAsync(b, cancel.Token);
		Task<HttpResponseMessage> following = third.SendAsync(c, CancellationToken.None);
		try
		{
			Assert.Equal(1, entered);
			cancel.Cancel();
			await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queued);
			Assert.Equal(1, entered);
			Assert.False(following.IsCompleted);
		}
		finally { release.TrySetResult(); }
		using var result = await active.WaitAsync(TimeSpan.FromSeconds(5));
		using var next = await following.WaitAsync(TimeSpan.FromSeconds(5));
		Assert.Equal(2, entered);
	}

	[Fact]
	public async Task FaultAndActiveCancellationReleaseTheGateWithoutRetryingOrHidingHttpFailure()
	{
		using var faulty = Client((_, _) => throw new HttpRequestException("injected"));
		using var failedRequest = Request();
		await Assert.ThrowsAsync<HttpRequestException>(() => faulty.SendAsync(failedRequest, CancellationToken.None));
		var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		using var waiting = Client(async (_, token) => { entered.SetResult(); await Task.Delay(Timeout.Infinite, token); return Response(); });
		using var cancel = new CancellationTokenSource();
		using var activeRequest = Request();
		var active = waiting.SendAsync(activeRequest, cancel.Token);
		await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
		cancel.Cancel();
		await Assert.ThrowsAnyAsync<OperationCanceledException>(() => active);
		int requests = 0;
		using var failingStatus = Client((_, _) => { requests++; return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)); });
		using var finalRequest = Request();
		using var response = await failingStatus.SendAsync(finalRequest, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
		Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
		Assert.Equal(1, requests);
	}

	[Fact]
	public async Task GateCoversResponseBodyBufferingAndRejectsMutationRequests()
	{
		var content = new HeldContent();
		using var first = Client((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content }));
		int entered = 0;
		using var second = Client((_, _) => { entered++; return Task.FromResult(Response()); });
		using var a = Request(); using var b = Request();
		var active = first.SendAsync(a, CancellationToken.None);
		await content.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
		var queued = second.SendAsync(b, CancellationToken.None);
		try { Assert.Equal(0, entered); Assert.False(queued.IsCompleted); }
		finally { content.Release.TrySetResult(); }
		using var response = await active.WaitAsync(TimeSpan.FromSeconds(5));
		using var next = await queued.WaitAsync(TimeSpan.FromSeconds(5));
		Assert.Equal(1, entered);
		using var mutation = new HttpRequestMessage(HttpMethod.Post, "admin/test");
		await Assert.ThrowsAsync<InvalidOperationException>(() => second.SendAsync(mutation, CancellationToken.None));
		Assert.Equal(1, entered);
	}

	private static LiveAdminClient Client(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) =>
		new(new Uri("http://oracle.invalid/"), new Handler(send));
	private static HttpRequestMessage Request() => new(HttpMethod.Get, "admin/player-state");
	private static HttpResponseMessage Response() => new(HttpStatusCode.OK) { Content = new StringContent("{}") };
	private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
	{
		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => send(request, token);
	}
	private sealed class HeldContent : HttpContent
	{
		public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
		public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
		protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
		{
			Started.SetResult();
			await Release.Task;
			await stream.WriteAsync("{}"u8.ToArray());
		}
		protected override bool TryComputeLength(out long length) { length = 2; return true; }
	}
}
