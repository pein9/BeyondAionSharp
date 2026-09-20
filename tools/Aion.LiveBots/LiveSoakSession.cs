using Aion.Bots.Timing;

namespace Aion.LiveBots;

internal sealed partial class LiveBotSession
{
	private int historyLimit = int.MaxValue;

	public void BoundSoakHistory(int capacity = 2048)
	{
		if (capacity < 64) throw new ArgumentOutOfRangeException(nameof(capacity));
		historyLimit = capacity;
		TrimHistory();
	}

	private void TrimHistory()
	{
		if (packetHistory.Count > historyLimit)
			packetHistory.RemoveRange(0, packetHistory.Count - historyLimit / 2);
	}

	public async Task CrashForSoakAsync(CancellationToken token)
	{
		if (transport == null) throw new InvalidOperationException("No connection to crash.");
		quitExpected = true;
		_ = api.Crash();
		await CloseChatAsync();
		await transport.CrashAsync(token);
		await CloseConnectionAsync(token);
		// Leave the crashed subject in-world for its normal delayed logout.
		await Task.Delay(TimeSpan.FromSeconds(BotTimingContract.MaximumCrashLeaveDelaySeconds + 1), token);
		await VerifyOfflineAsync(token);
	}

	public async Task ReenterForSoakAsync(bool crash, CancellationToken token)
	{
		int previous = ConnectionGeneration;
		var inventory = api.World.Inventory.Values.GroupBy(item => item.ItemId)
			.ToDictionary(group => group.Key, group => group.Sum(item => item.Count));
		ushort level = api.World.Level;
		if (crash) await CrashForSoakAsync(token);
		else { await QuitAsync(token); await VerifyOfflineAsync(token); }
		await WaitForReentryAsync(token);
		await ReloginAndVerifyPersistenceAsync(token);
		await EnterWorldAsync(token);
		await SynchronizeAsync(token);
		if (ConnectionGeneration != previous + 1 || api.World.SelfObjectId != characterId)
			throw new InvalidDataException("Soak reconnect did not restore the same subject through a fresh connection.");
		var restored = api.World.Inventory.Values.GroupBy(item => item.ItemId)
			.ToDictionary(group => group.Key, group => group.Sum(item => item.Count));
		if (api.World.Level != level || !inventory.OrderBy(pair => pair.Key).SequenceEqual(restored.OrderBy(pair => pair.Key)))
			throw new InvalidDataException("Soak reconnect did not preserve level and exact inventory totals.");
	}
}
