using Aion.Bots.Protocol;
using Aion.Bots.Scenarios;
using Aion.GameServer.Dao;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.Account;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;

namespace Aion.Simulation.Tests;

public sealed partial class SimulationFastScenarioTests
{
	private async Task RunL6Async(ScenarioDefinition scenario, bool includeHistory)
	{
		using var policy = NewEconomyPolicy(scenario.Id, includeHistory);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2)); var token = timeout.Token;
		Assert.Equal(new DateTimeOffset(2020, 12, 16, 8, 58, 0, TimeSpan.Zero), fixture.Epoch);
		const int passportId = 346, rewardItem = 188053412, accountId = 117;
		var template = DataManager.ATREIAN_PASSPORT_DATA.GetAtreianPassportId(passportId);
		Assert.True(template.IsActive()); Assert.Equal(rewardItem, template.GetRewardItemId()); Assert.Equal(1, template.GetRewardItemCount());
		await using var session = new SimulationL0Session(fixture, policy, "b01", accountId, "Simpassport");
		session.BeginStep("s01", "login-before-nine-and-earn-first-daily-passport");
		await session.LoginAndAuthenticateAsync(token); await session.CreateCharacterAsync(token); await session.EnterWorldAsync(token);
		await session.SynchronizeAsync(token);
		Assert.Equal(0, fixture.World.GetPlayer(session.CharacterId).AccessLevel);
		var initial = Assert.Single(DailyPackets(session.PacketHistory).Last());
		Assert.Equal(1, initial.Stamps); Assert.Equal(1, initial.RewardStatus);
		Assert.Equal(SystemClock.CurrentSeconds(), initial.ArriveTime);
		Assert.Equal(0, RewardCount());
		VerifyStored(1, [initial]);

		session.BeginStep("s02", "claim-first-reward-through-client-packet");
		initial = await ClaimAsync(initial, 1);
		VerifyStored(1, [initial]);
		session.BeginStep("s03", "relogin-before-reset-does-not-duplicate-reward");
		var beforeReset = await ReloginAsync();
		Assert.Equal(initial, Assert.Single(beforeReset)); Assert.Equal(1, RewardCount());
		VerifyStored(1, [initial]);

		session.BeginStep("s04", "watch-daily-reset-boundary-before-and-at-nine");
		var reset = new DateTimeOffset(2020, 12, 16, 9, 0, 0, TimeSpan.Zero);
		Assert.True(SystemClock.UtcNow() < reset.AddMilliseconds(-1));
		int packetStart = session.PacketHistory.Count;
		await session.AdvanceAsync(reset.AddMilliseconds(-1) - SystemClock.UtcNow(), token);
		await session.SynchronizeAsync(token);
		Assert.Empty(DailyPackets(session.PacketHistory.Skip(packetStart)));
		VerifyStored(1, [initial]); Assert.Equal(1, RewardCount());
		await session.AdvanceAsync(TimeSpan.FromMilliseconds(1), token);
		await session.SynchronizeAsync(token);
		var afterReset = Assert.Single(DailyPackets(session.PacketHistory.Skip(packetStart)));
		Assert.True(afterReset.Length == 2, "L6 09:00 reset must retain yesterday's claimed passport and award today's passport. Actual: " +
			System.Text.Json.JsonSerializer.Serialize(afterReset));
		Assert.All(afterReset, p => Assert.Equal(2, p.Stamps));
		var second = Assert.Single(afterReset, p => p.RewardStatus == 1);
		Assert.Equal(reset.ToUnixTimeSeconds(), second.ArriveTime);
		Assert.Equal(initial with { Stamps = 2 }, Assert.Single(afterReset, p => p.RewardStatus == 2));
		VerifyStored(2, afterReset);

		session.BeginStep("s05", "claim-new-day-reward-and-verify-second-item");
		second = await ClaimAsync(second, 2);
		VerifyStored(2, [initial with { Stamps = 2 }, second]);
		session.BeginStep("s06", "relogin-after-reset-preserves-two-claims-without-extra-stamp");
		var final = await ReloginAsync();
		Assert.Equal(new[] { initial with { Stamps = 2 }, second }.OrderBy(p => p.ArriveTime), final.OrderBy(p => p.ArriveTime));
		Assert.Equal(2, RewardCount()); VerifyStored(2, final);
		session.BeginStep("s07", "quit-and-confirm-offline");
		await session.QuitAsync(token); await session.VerifyOfflineAsync(token); policy.AssertClean();

		long RewardCount() => session.Api.World.Inventory.Values.Where(i => i.ItemId == rewardItem).Sum(i => i.Count);
		IEnumerable<BotPassport[]> DailyPackets(IEnumerable<DecodedBotServerPacket> packets) => packets
			.Where(p => p.PacketType == typeof(SM_ATREIAN_PASSPORT))
			.Select(p => p.Get<BotPassport[]>("passports").Where(row => row.Id == passportId).ToArray());
		async Task<BotPassport> ClaimAsync(BotPassport passport, int itemCount)
		{
			int start = session.PacketHistory.Count;
			await session.SendPacketAsync(GameClientPackets.ClaimPassports(new BotPassportClaim(passport.Id, passport.ArriveTime)), token);
			await session.SynchronizeAsync(token);
			var updated = Assert.Single(DailyPackets(session.PacketHistory.Skip(start))).Single(p => p.ArriveTime == passport.ArriveTime);
			Assert.Equal(passport with { RewardStatus = 2 }, updated); Assert.Equal(itemCount, RewardCount());
			Assert.Equal(itemCount, fixture.World.GetPlayer(session.CharacterId).GetInventory().GetItemCountByItemId(rewardItem));
			return updated;
		}
		async Task<BotPassport[]> ReloginAsync()
		{
			await session.QuitAsync(token); await session.VerifyOfflineAsync(token); await session.WaitForReentryAsync(token);
			int start = session.PacketHistory.Count;
			await session.ReloginAndVerifyPersistenceAsync(token); await session.EnterWorldAsync(token); await session.SynchronizeAsync(token);
			return Assert.Single(DailyPackets(session.PacketHistory.Skip(start)));
		}
		void VerifyStored(int stamps, BotPassport[] expected)
		{
			var account = new Account(accountId); AccountPassportsDAO.LoadPassport(account);
			Assert.Equal(stamps, account.GetPassportStamps());
			var rows = account.GetPassportsList().GetAllPassports().Where(p => p.GetId() == passportId)
				.Select(p => new BotPassport(p.GetId(), account.GetPassportStamps(), p.GetRewardStatus().GetId(), (int)new DateTimeOffset(p.GetArriveDate()).ToUnixTimeSeconds()));
			Assert.Equal(expected.OrderBy(p => p.ArriveTime), rows.OrderBy(p => p.ArriveTime));
			Assert.Equal(expected.Max(p => p.ArriveTime), account.GetLastStamp()!.Value.ToUnixTimeSeconds());
		}
	}
}
