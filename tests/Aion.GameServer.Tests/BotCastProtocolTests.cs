using System.Buffers.Binary;
using Aion.Bots.Api;
using Aion.Bots.Protocol;
using Aion.Bots.Scenarios;
using Aion.Bots.Timing;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Tests;

public sealed class BotCastProtocolTests
{
	[Fact]
	public async Task CapturedQuestInterruptionIsTerminalWithoutAResultAndReleasesCasting()
	{
		// mixed50-2h-c: b01 (133611), Flame Bolt 1282; the NPC interrupts it at 05:02:54.582Z.
		byte[] body = new byte[6];
		BinaryPrimitives.WriteInt32LittleEndian(body, 133611);
		BinaryPrimitives.WriteUInt16LittleEndian(body.AsSpan(4), 1282);
		var cancel = new BotServerPacketDecoder().Decode(typeof(SM_SKILL_CANCEL), body);
		var api = new BotApi();
		api.Target(12667);
		api.Cast(new SpellCastData(1282, 1, 0) { TargetObjectId = 12667 });
		Assert.Contains(BotBlockingActivity.Casting, api.Timing.BlockingActivities);
		var result = await BotCastProtocol.WaitForCompletionAsync((predicate, _) =>
		{
			api.Observe(cancel);
			Assert.True(predicate(cancel));
			return Task.FromResult(cancel); // No SM_CASTSPELL_RESULT exists for this cast.
		}, 133611, 1282, CancellationToken.None);
		Assert.Same(cancel, result);
		Assert.DoesNotContain(BotBlockingActivity.Casting, api.Timing.BlockingActivities);
		Assert.Equal(TimeSpan.FromSeconds(2), BotCastProtocol.RecoveryDelay(result));
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task CompletionFiltersOtherCastersSkillsAndAmbientPacketsThroughOneReader(bool cancelled)
	{
		var expected = Terminal(cancelled, 133611, 1282);
		var queue = new Queue<DecodedBotServerPacket>([
			new(typeof(SM_MOVE), new Dictionary<string, object?>()),
			Terminal(cancelled, 123, 1282), Terminal(cancelled, 133611, 1), expected]);
		int calls = 0;
		var result = await BotCastProtocol.WaitForCompletionAsync((predicate, _) =>
		{
			calls++;
			while (queue.TryDequeue(out var packet)) if (predicate(packet)) return Task.FromResult(packet);
			throw new InvalidOperationException("Missing terminal packet.");
		}, 133611, 1282, CancellationToken.None);
		Assert.Same(expected, result);
		Assert.Equal(1, calls);
		Assert.Empty(queue);
	}

	[Fact]
	public async Task StartUsesCasterAndSpellIdRatherThanAnotherPlayersStart()
	{
		var start = new DecodedBotServerPacket(typeof(SM_CASTSPELL), new Dictionary<string, object?>
		{ ["objectId"] = 133611, ["spellId"] = (ushort)1282, ["castDuration"] = (ushort)2000 });
		Assert.Same(start, await BotCastProtocol.WaitForStartAsync((predicate, _) =>
		{
			Assert.False(predicate(Terminal(true, 133611, 1282)));
			Assert.False(predicate(new(typeof(SM_CASTSPELL), new Dictionary<string, object?> { ["objectId"] = 1 })));
			Assert.False(predicate(new(typeof(SM_CASTSPELL), new Dictionary<string, object?> { ["objectId"] = 133611, ["spellId"] = (ushort)1 })));
			Assert.True(predicate(start));
			return Task.FromResult(start);
		}, 133611, 1282, CancellationToken.None));
	}

	[Fact]
	public void MovingTargetRangeRejectionTerminatesTheCastStartWait()
	{
		var range = new DecodedBotServerPacket(typeof(SM_SYSTEM_MESSAGE),
			new Dictionary<string, object?> { ["name"] = "STR_SKILL_NOT_ENOUGH_DISTANCE" });
		Assert.True(BotCastProtocol.IsStartRejection(range));
		Assert.True(BotCastProtocol.IsStartRejection(new(typeof(SM_SYSTEM_MESSAGE),
			new Dictionary<string, object?> { ["name"] = "STR_SKILL_CAN_NOT_ATTACK_WHILE_IN_ABNORMAL_STATE" })));
		Assert.False(BotCastProtocol.IsStartRejection(new(typeof(SM_SYSTEM_MESSAGE),
			new Dictionary<string, object?> { ["name"] = "STR_GET_EXP" })));
		Assert.False(BotCastProtocol.IsStartRejection(new(typeof(SM_MOVE),
			new Dictionary<string, object?> { ["name"] = "STR_SKILL_NOT_ENOUGH_DISTANCE" })));
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task MissingPacketFailsAtItsOwnDeadlineWithoutRealTimeSleep(bool start)
	{
		var time = new DeadlineProvider();
		Task<DecodedBotServerPacket> Read(Func<DecodedBotServerPacket, bool> _, CancellationToken token) =>
			new TaskCompletionSource<DecodedBotServerPacket>().Task.WaitAsync(token);
		var task = start ? BotCastProtocol.WaitForStartAsync(Read, 133611, 1282, CancellationToken.None, time)
			: BotCastProtocol.WaitForCompletionAsync(Read, 133611, 1282, CancellationToken.None, time);
		Assert.False(task.IsCompleted);
		Assert.Equal(TimeSpan.FromSeconds(10), time.Due);
		time.Expire();
		var failure = await Assert.ThrowsAsync<TimeoutException>(() => task);
		Assert.Contains(start ? "cast start" : "cast result or cancellation", failure.Message);
		Assert.True(time.TimerDisposed);
	}

	[Fact]
	public async Task CallerCancellationIsNotRelabelledAsAProtocolTimeout()
	{
		var time = new DeadlineProvider();
		using var cancel = new CancellationTokenSource();
		var task = BotCastProtocol.WaitForCompletionAsync((_, token) =>
			new TaskCompletionSource<DecodedBotServerPacket>().Task.WaitAsync(token), 133611, 1282, cancel.Token, time);
		cancel.Cancel();
		await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
		Assert.True(time.TimerDisposed);
	}

	[Theory]
	[InlineData(0, 2000)]
	[InlineData(1999, 2000)]
	[InlineData(2000, 2001)]
	[InlineData(3000, 3001)]
	public void SuccessfulCastKeepsItsAnimationRecovery(ushort hitTime, int expectedMillis) =>
		Assert.Equal(TimeSpan.FromMilliseconds(expectedMillis), BotCastProtocol.RecoveryDelay(Terminal(false, 133611, 1282, hitTime)));

	[Fact]
	public void NonterminalPacketCannotBeUsedAsRecoveryEvidence() =>
		Assert.Throws<ArgumentException>(() => BotCastProtocol.RecoveryDelay(new(typeof(SM_MOVE), new Dictionary<string, object?>())));

	private static DecodedBotServerPacket Terminal(bool cancel, int caster, ushort skill, ushort hitTime = 0) =>
		new(cancel ? typeof(SM_SKILL_CANCEL) : typeof(SM_CASTSPELL_RESULT), new Dictionary<string, object?>
		{ [cancel ? "objectId" : "effectorId"] = caster, ["skillId"] = skill, ["hitTime"] = hitTime });

	private sealed class DeadlineProvider : TimeProvider
	{
		private Action? expire;
		public TimeSpan Due { get; private set; }
		public bool TimerDisposed { get; private set; }
		public void Expire() => expire!();
		public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
		{
			Due = dueTime;
			expire = () => callback(state);
			return new DeadlineTimer(() => TimerDisposed = true);
		}
		private sealed class DeadlineTimer(Action dispose) : ITimer
		{
			public bool Change(TimeSpan dueTime, TimeSpan period) => throw new NotSupportedException();
			public void Dispose() => dispose();
			public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
		}
	}
}
