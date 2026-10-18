using System.Buffers.Binary;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using Aion.Bots.Protocol;
using Aion.Bots.Scenarios;
using Aion.Bots.World;
using Aion.Commons.Database;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.House;
using Aion.GameServer.Model.Templates.Housing;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Items;
using Aion.GameServer.Services.Mail;
using Aion.GameServer.Services.Players;
using Aion.GameServer.Taskmanager.Tasks.Housing;
using Aion.GameServer.Utils;

namespace Aion.Simulation.Tests;

/// <summary>
/// P11-06, scenario HOUSING: Java's housing calendar played through on the virtual clock from the process's own epoch,
/// Sunday 2026-09-20 23:58 UTC (<c>reset-HOUSING</c>), two minutes before the Monday 00:00 auto-fill of the house broker
/// (housing.properties: auto-fill and maintenance Monday 00:00, auction end Sunday 12:00, registration Monday to Friday).
/// Three ordinary Asmodians and an Elyos play two weeks: the auto-fill, bids by the client's packets with an outbid and its
/// refund, the auction end and the acquisition, the first maintenance (a week free), rent, the house gate of Homeward
/// Bound and its way back, the decoration of the house, a house script, the door settings with a friend and a stranger
/// inside, the relationship crystal, the owner's own registration and a sale whose last-minute bid prolongs the auction,
/// and an overdue house's two warnings, its impoundment with Java's refund and its re-auction. Every expected value is
/// Java's: <c>AuctionAutoFillTask</c>, <c>HousingBidService</c> (auction, bid, endAuction, onPlayerLogin),
/// <c>AuctionEndTask</c> (prolongation), <c>MaintenanceTask</c>, <c>MailFormatter</c>, <c>HousingService</c>
/// (changeOwner, onPlayerLogin), <c>HouseController</c>, <c>HouseGateAI</c>, and the client packets <c>CM_GET_HOUSE_BIDS</c>,
/// <c>CM_PLACE_BID</c>, <c>CM_REGISTER_HOUSE</c>, <c>CM_HOUSE_PAY_RENT</c>, <c>CM_HOUSE_EDIT</c>, <c>CM_HOUSE_DECORATE</c>,
/// <c>CM_HOUSE_SCRIPT</c>, <c>CM_HOUSE_SETTINGS</c>, <c>CM_HOUSE_OPEN_DOOR</c>, <c>CM_HOUSE_KICK</c>, <c>CM_HOUSE_TELEPORT</c>
/// and <c>CM_HOUSE_TELEPORT_BACK</c>.
/// <para>
/// Director setup, recorded here as the plan asks: every subject is made a level-30 Gladiator; the Asmodians' housing quest
/// (28802, the condition of <c>HousingService.canOwnHouse</c>) is completed for them, while the Elyos keeps his (18802)
/// open, and the buyer is given that quest's reward, the Homeward Bound skill book, which he reads himself; the Asmodians get
/// kinah for their bids and rent, and the house owner two tables and a wallpaper to decorate with; the third Asmodian is
/// given a Pernon house as the calendar would have left one bought on Sunday 2026-09-13 whose free week ended unpaid
/// (acquired 09-13 12:00, rent due Monday 09-21 00:00), the first whose door goes through Java's signed heading; the
/// visitors are placed at the house door by setup teleports. The auto-fill shuffles with an unseeded Random, as Java does,
/// so the house bought is a different Pernon house from run to run.
/// </para>
/// </summary>
public sealed partial class SimulationFastScenarioTests
{
	internal static readonly DateTimeOffset HousingEpoch = new(2026, 9, 20, 23, 58, 0, TimeSpan.Zero);
	private const int Pernon = 710010000;
	private const int AsmodianHousingQuest = 28802;
	private const int ElyosHousingQuest = 18802;
	private const int PernonHouseLand = 338001; // HOUSE size, level 21, 12,000,000 kinah, fee 400,000
	private const int HomewardBound = 296; // the Asmodians' house gate skill, from its book at 21
	private const int HomewardBoundBook = 169500953; // 28802's reward
	private const int AsmodianHouseGate = 749247;
	private const int TableItem = 170000005; // passive house object 3000006, no use days
	private const int TableObject = 3000006;
	private const int WallpaperItem = 171110023; // house part 3524001, INWALL_ANY
	private const int Wallpaper = 3524001;
	private const int FriendInvitation = 1401498;

	private async Task RunHousingAsync(ScenarioDefinition scenario, bool includeHistory)
	{
		var sessions = new List<SimulationL0Session>();
		try
		{
			await PlayHousingAsync(scenario, includeHistory, sessions);
		}
		catch (Exception exception) when (exception is not SimulationLogPolicyException)
		{
			// Harness rule 2: the whole exception and every bot's recent packets.
			throw new InvalidOperationException("HOUSING failed at " + SystemClock.UtcNow().ToString("O") + "; recent packets:" + Environment.NewLine +
				string.Join(Environment.NewLine, sessions.Select(session => session.CurrentStep + " " + string.Join(" | ",
					session.PacketHistory.TakeLast(30).Select(packet => packet.PacketType.Name +
						(packet.PacketType == typeof(SM_SYSTEM_MESSAGE) ? "(" + packet.Get<string?>("name") + ")" : ""))))), exception);
		}
	}

	private sealed class HousingCast(SimulationL0Session session, string name)
	{
		public SimulationL0Session Session { get; } = session;
		public string Name { get; } = name;
		public int Id => Session.CharacterId;
		public bool Online { get; set; }
		public int BidMark { get; set; }
	}

	private async Task PlayHousingAsync(ScenarioDefinition scenario, bool includeHistory, List<SimulationL0Session> created)
	{
		using var policy = NewPolicy(scenario.Id, includeHistory);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(90));
		CancellationToken token = timeout.Token;
		Assert.Equal(HousingEpoch, fixture.Epoch);
		Assert.Equal(DayOfWeek.Sunday, HousingEpoch.DayOfWeek);
		int firstLogEntry = fixture.LogCapture.Entries.Count;
		DateTimeOffset monday1 = new(2026, 9, 21, 0, 0, 0, TimeSpan.Zero), sunday1 = monday1.AddDays(6).AddHours(12);
		DateTimeOffset monday2 = monday1.AddDays(7), sunday2 = sunday1.AddDays(7), monday3 = monday2.AddDays(7);
		Assert.True(SystemClock.UtcNow() < monday1);
		// The cron expressions of housing.properties, as Java reads them.
		Assert.Equal(new[] { 1, 5 }, HousingConfig.HOUSE_AUCTION_REGISTER_DAYS);
		Assert.Equal(monday1, AuctionAutoFillTask.GetInstance().GetNextRunAfter(HousingEpoch));
		Assert.Equal(monday1, MaintenanceTask.GetInstance().GetNextRunAfter(HousingEpoch));
		Assert.Equal(sunday1, AuctionEndTask.GetInstance().GetNextRunAfter(HousingEpoch));

		await using var buyerSession = new SimulationL0Session(fixture, policy, "b01", 102, "Asimhousea", Race.ASMODIANS);
		await using var rivalSession = new SimulationL0Session(fixture, policy, "b02", 103, "Asimhouseb", Race.ASMODIANS);
		await using var friendSession = new SimulationL0Session(fixture, policy, "b03", 104, "Asimhousec", Race.ASMODIANS);
		await using var elyosSession = new SimulationL0Session(fixture, policy, "b04", 105, "Aesimhoused", Race.ELYOS);
		created.AddRange([buyerSession, rivalSession, friendSession, elyosSession]);
		HousingCast buyer = new(buyerSession, "Asimhousea"), rival = new(rivalSession, "Asimhouseb"), friend = new(friendSession, "Asimhousec"),
			elyos = new(elyosSession, "Aesimhoused");
		HousingCast[] cast = [buyer, rival, friend, elyos];
		var kinah = new Dictionary<HousingCast, long> { [buyer] = 40_000_000, [rival] = 40_000_000, [friend] = 20_000_000, [elyos] = 0 };

		// s01: login, and the director's setup.
		foreach (HousingCast actor in cast)
		{
			actor.Session.BeginStep("s01", "login-create-enter-and-setup");
			await actor.Session.LoginAndAuthenticateAsync(token);
			await actor.Session.CreateCharacterAsync(token);
			await actor.Session.EnterWorldAsync(token);
			await actor.Session.WaitForPacketAsync(typeof(SM_PLAY_MOVIE), token);
			actor.Online = true;
			Player player = fixture.World.GetPlayer(actor.Id);
			Assert.Equal(0, player.GetClientConnection().GetAccount().GetAccessLevel());
			Assert.True(ClassChangeService.SetClass(player, PlayerClass.GLADIATOR, validate: true, updateDaevaStatus: true));
			player.GetCommonData().SetLevel(30);
			Assert.True(player.GetCommonData().IsDaeva());
			if (player.GetRace() == Race.ASMODIANS)
			{
				Assert.True(player.GetQuestStateList().AddQuest(AsmodianHousingQuest, new QuestState(AsmodianHousingQuest, QuestStatus.COMPLETE)));
			}
			if (actor == buyer)
			{
				// 28802's reward, the Homeward Bound skill book, read as the client does (CM_USE_ITEM; SkillLearnAction).
				Assert.Equal(0, ItemService.AddItem(player, HomewardBoundBook, 1));
				await actor.Session.SynchronizeAsync(token);
				Item book = Assert.IsType<Item>(player.GetInventory().GetFirstItemByItemId(HomewardBoundBook));
				await actor.Session.SendPacketAsync(actor.Session.Api.UseItem(book.GetObjectId(), book.GetItemTemplate()), token);
				await actor.Session.SynchronizeAsync(token);
				Assert.True(player.GetSkillList().IsSkillPresent(HomewardBound), $"{actor.Name} did not learn Homeward Bound from its book");
				Assert.Equal(0, player.GetInventory().GetItemCountByItemId(HomewardBoundBook));
			}
			if (kinah[actor] > 0)
				Assert.Equal(0, ItemService.AddItem(player, BotWorldModel.KinahItemId, kinah[actor]));
			kinah[actor] = player.GetInventory().GetKinah();
			// HousingService.onPlayerLogin: SM_HOUSE_OWNER_INFO of a player without a house (SINGLE_HOUSE, BIDDING_ALLOWED only
			// once the quest is done, which it was not at login).
			HouseOwnerInfo info = ReadHouseOwnerInfo(Assert.Single(actor.Session.PacketHistory, packet => packet.PacketType == typeof(SM_HOUSE_OWNER_INFO)));
			Assert.Equal(new HouseOwnerInfo(0, 0, (int)HouseOwnerState.SINGLE_HOUSE.GetId(), 0, 0, 0, 0, 0), info);
		}

		// The friend's Pernon house, as the calendar would have left one bought on Sunday 09-13: its free week ended on Monday
		// 09-21 00:00 and nobody paid. It is the first whose relationship crystal sees its butler at a heading below 30, so that
		// the way out of its door goes through Java's signed byte arithmetic (HouseController.teleportNearHouseDoor).
		HousingService housing = HousingService.GetInstance();
		House overdue = housing.GetCustomHouses().Where(house => house.GetLand().GetId() == PernonHouseLand && house.GetOwnerId() == 0
			&& HouseDoorHeading(house) < 30).OrderBy(house => house.GetAddress().GetId()).First();
		housing.ChangeOwner(overdue, friend.Id);
		overdue.SetAcquiredTime(new DateTimeOffset(2026, 9, 13, 12, 0, 0, TimeSpan.Zero));
		overdue.SetNextPay(monday1);
		overdue.Save();
		Assert.Same(overdue, fixture.World.GetPlayer(friend.Id).GetActiveHouse());
		AssertHouseRow(overdue, friend.Id, new DateTimeOffset(2026, 9, 13, 12, 0, 0, TimeSpan.Zero), monday1);
		await SyncHousingAsync(cast, token);

		// s02: before the auto-fill the broker is empty: one SM_HOUSE_BIDS, first and last, with nothing in it.
		buyer.Session.BeginStep("s02", "empty-broker");
		Assert.Empty(HousingBidService.GetInstance().GetBidInfo(Race.ASMODIANS));
		HouseBidsList empty = await RequestHouseBidsAsync(buyer, token);
		Assert.Equal(new HouseBidsList(true, true, 0, 0, 0, 0, []), empty);

		// s03: Monday 00:00, AuctionAutoFillTask and MaintenanceTask.
		int[] marks = HousingMarks(cast);
		foreach (HousingCast actor in cast) actor.Session.BeginStep("s03", "auto-fill-and-first-maintenance");
		await AdvanceHousingWorldToAsync(cast, monday1.AddSeconds(1), TimeSpan.FromSeconds(1), token);
		await SyncHousingAsync(cast, token);
		Dictionary<Race, int> autoFilled = ExpectedAutoFill(new HashSet<int>());
		foreach (Race race in new[] { Race.ELYOS, Race.ASMODIANS })
		{
			List<HouseBids> listed = HousingBidService.GetInstance().GetBidInfo(race);
			Assert.Equal(autoFilled[race], listed.Count);
			Assert.All(listed, bids =>
			{
				House house = housing.FindHouse(bids.GetHouseObjectId());
				Assert.Equal(0, house.GetOwnerId());
				Assert.True(house.MatchesLandRace(race));
				Assert.Same(bids, house.GetBids());
				Assert.Equal(house.GetDefaultAuctionPrice(), bids.GetInitialOffer().GetKinah());
				Assert.Equal(0, bids.GetBidCount());
				Assert.Equal(monday1.ToUnixTimeMilliseconds(), bids.GetInitialOffer().GetTime());
				if (house.GetPosition() != null && house.IsSpawned())
					Assert.Equal(house.GetLand().GetSaleSignNpcId(), house.GetCurrentSign().GetNpcId());
				Assert.Equal(HouseDoorState.OPEN, house.GetDoorState()); // resetDoorState: unowned, but with bids
			});
			AssertAutoFillLimits(listed, race);
		}
		AssertBidRows(HousingBidService.GetInstance().GetBidInfo(Race.ELYOS).Concat(HousingBidService.GetInstance().GetBidInfo(Race.ASMODIANS)));
		string[] autoFillLog = HousingLog(firstLogEntry, "AuctionAutoFillTask");
		Assert.Contains($"[ELYOS] Added {autoFilled[Race.ELYOS]} new houses automatically to auction.", autoFillLog);
		Assert.Contains($"[ASMODIANS] Added {autoFilled[Race.ASMODIANS]} new houses automatically to auction.", autoFillLog);
		// MaintenanceTask: only the friend's house has an owner; its rent was due at 00:00, so it is overdue now, and the
		// impound date is the first run at least two weeks after it (10-05 00:00): the first warning.
		Assert.Contains("Executing house maintenance for 1 houses", HousingLog(firstLogEntry, "MaintenanceTask"));
		Assert.Equal(monday1, overdue.GetNextPay());
		Assert.Equal(friend.Id, overdue.GetOwnerId());
		AssertOverdueMail(friend, "$$HS_OVERDUE_1ST", overdue, monday3, 0);

		// Each race's broker lists its own houses only.
		HouseBidsList asmodianList = await RequestHouseBidsAsync(buyer, token);
		AssertHouseBidsList(asmodianList, Race.ASMODIANS, buyer);
		HouseBidsList elyosList = await RequestHouseBidsAsync(elyos, token);
		AssertHouseBidsList(elyosList, Race.ELYOS, elyos);

		// s04: bidding.
		foreach (HousingCast actor in cast) actor.Session.BeginStep("s04", "bids-outbid-and-refund");
		// The Elyos has not done 18802: canOwnHouse refuses him before anything else.
		HouseBidsEntry elyosHouse = elyosList.Entries[0];
		await PlaceHouseBidAsync(elyos, elyosHouse.ListIndex, elyosHouse.HighestBid, token);
		AssertBidRefused(elyos, "STR_MSG_HOUSING_CANT_OWN_NOT_COMPLETE_QUEST", [ElyosHousingQuest.ToString(CultureInfo.InvariantCulture)]);
		// The friend's own house is overdue: STR_MSG_HOUSING_CANT_BID_OVERDUE.
		HouseBidsEntry target = asmodianList.Entries.First(entry => entry.LandId == PernonHouseLand);
		House house = housing.GetHouseByAddress(target.Address);
		long price = house.GetDefaultAuctionPrice();
		Assert.Equal(12_000_000, price);
		await PlaceHouseBidAsync(friend, target.ListIndex, price, token);
		AssertBidRefused(friend, "STR_MSG_HOUSING_CANT_BID_OVERDUE", []);
		// The buyer bids the starting price, which HouseBids.bid accepts while nobody has bid.
		await PlaceHouseBidAsync(buyer, target.ListIndex, price, token);
		kinah[buyer] -= price;
		AssertBidAccepted(buyer, target.Address, price, kinah[buyer]);
		// The leader cannot bid on it again, nor on another house.
		await PlaceHouseBidAsync(buyer, target.ListIndex, price + 1, token);
		AssertBidRefused(buyer, "STR_MSG_HOUSING_CANT_BID_SUCC_BID_HOUSE", []);
		// (A house of the same level: isAllowedToBid checks the level before the other house.)
		HouseBidsEntry other = asmodianList.Entries.First(entry => entry.ListIndex != target.ListIndex && entry.LandId == PernonHouseLand);
		await PlaceHouseBidAsync(buyer, other.ListIndex, other.HighestBid, token);
		AssertBidRefused(buyer, "STR_MSG_HOUSING_CANT_BID_OTHER_HOUSE", []);
		// The rival: a raise of 100% or more is refused (step limit), the same price is too low, 15,000,000 outbids.
		await PlaceHouseBidAsync(rival, target.ListIndex, price * 2, token);
		AssertBidRefused(rival, "STR_MSG_HOUSING_CANT_BID_EXCESS_ACCOUNT", []);
		await PlaceHouseBidAsync(rival, target.ListIndex, price, token);
		AssertBidRefused(rival, "STR_MSG_HOUSING_CANT_BID_LOWER", [], receiveBids: true);
		int buyerMark = buyer.Session.PacketHistory.Count;
		await PlaceHouseBidAsync(rival, target.ListIndex, 15_000_000, token);
		kinah[rival] -= 15_000_000;
		AssertBidAccepted(rival, target.Address, 15_000_000, kinah[rival]);
		// The outbid buyer: STR_MSG_HOUSING_BID_CANCEL, SM_RECEIVE_BIDS(0), and his bid back by an auction mail (FAILED_BID).
		await buyer.Session.SynchronizeAsync(token);
		AssertOutbid(buyer, buyerMark);
		Letter refund = AssertAuctionMail(buyer, house, AuctionResult.FAILED_BID, house.GetBids().GetHighestBid().GetTime(), price);
		await ClaimMailKinahAsync(buyer, refund, token);
		kinah[buyer] += price;
		Assert.Equal(kinah[buyer], fixture.World.GetPlayer(buyer.Id).GetInventory().GetKinah());
		// The buyer outbids the rival with 18,000,000; the rival gets his 15,000,000 back by mail.
		int rivalMark = rival.Session.PacketHistory.Count;
		await PlaceHouseBidAsync(buyer, target.ListIndex, 18_000_000, token);
		kinah[buyer] -= 18_000_000;
		AssertBidAccepted(buyer, target.Address, 18_000_000, kinah[buyer]);
		await rival.Session.SynchronizeAsync(token);
		AssertOutbid(rival, rivalMark);
		Letter rivalRefund = AssertAuctionMail(rival, house, AuctionResult.FAILED_BID, house.GetBids().GetHighestBid().GetTime(), 15_000_000);
		await ClaimMailKinahAsync(rival, rivalRefund, token);
		kinah[rival] += 15_000_000;
		// HouseBidsDAO: each accepted bid has its row; the broker shows the new price, the bid count and the buyer's last bid.
		Assert.Equal([(0, price), (buyer.Id, price), (rival.Id, 15_000_000L), (buyer.Id, 18_000_000L)], BidRows(house.GetObjectId()));
		HouseBidsList afterBids = await RequestHouseBidsAsync(buyer, token);
		Assert.Equal(target.ListIndex, afterBids.LastBidListIndex);
		Assert.Equal(18_000_000, afterBids.LastBidKinah);
		HouseBidsEntry bidEntry = Assert.Single(afterBids.Entries, entry => entry.ListIndex == target.ListIndex);
		Assert.Equal(18_000_000, bidEntry.HighestBid);
		Assert.Equal(3, bidEntry.BidCount);

		// Everybody leaves; the week passes.
		foreach (HousingCast actor in cast)
			await HousingQuitAsync(actor, token);
		await AdvanceHousingWorldToAsync([], sunday1.AddMinutes(-1), TimeSpan.FromHours(1), token);
		Assert.Single(HousingBidService.GetInstance().GetBidInfo(Race.ASMODIANS), bids => bids.GetHouseObjectId() == house.GetObjectId());

		// s05: Sunday 12:00, AuctionEndTask: the buyer gets the house; the other houses go unsold.
		await AdvanceHousingWorldToAsync([], sunday1.AddSeconds(1), TimeSpan.FromSeconds(1), token);
		Assert.Empty(HousingBidService.GetInstance().GetBidInfo(Race.ASMODIANS));
		Assert.Empty(HousingBidService.GetInstance().GetBidInfo(Race.ELYOS));
		Assert.Equal(sunday1, AuctionEndTask.GetInstance().GetLastRun());
		Assert.Equal(sunday2, AuctionEndTask.GetInstance().GetNextRun());
		Assert.Equal(buyer.Id, house.GetOwnerId());
		Assert.Null(house.GetBids());
		Assert.False(house.IsInactive());
		Assert.Null(house.GetNextPay());
		Assert.Equal(sunday1, house.GetAcquiredTime());
		Assert.Equal(HouseDoorState.OPEN, house.GetDoorState());
		Assert.Equal(house.GetLand().GetHomeSignNpcId(), house.GetCurrentSign().GetNpcId());
		Assert.Equal(buyer.Name, house.GetButler().GetMasterName());
		AssertHouseRow(house, buyer.Id, sunday1, null);
		Assert.Empty(BidRows(house.GetObjectId()));
		string[] auctionLog = HousingLog(firstLogEntry, "HOUSE_AUCTION_LOG");
		Assert.Contains($"Address {target.Address} sold for 18000000 kinah (3 bids; result: WIN_BID) to player {buyer.Id}", auctionLog);
		Assert.Equal(autoFilled[Race.ELYOS] + autoFilled[Race.ASMODIANS] - 1, auctionLog.Count(line => line.StartsWith("Address ", StringComparison.Ordinal)
			&& line.EndsWith(" kinah (result: FAILED_SALE; return: 0 kinah)", StringComparison.Ordinal)));
		AssertAuctionMailOffline(buyer.Id, house, AuctionResult.WIN_BID, sunday1.ToUnixTimeMilliseconds(), 0);

		// The buyer logs in: HousingBidService.onPlayerLogin finds the new WIN_BID mail, and HousingService.onPlayerLogin
		// sends his house: rent free until the next maintenance (one week).
		await AdvanceHousingWorldToAsync([], sunday1.AddMinutes(30), TimeSpan.FromMinutes(10), token);
		buyer.Session.BeginStep("s05", "auction-end-and-login");
		int loginMark = buyer.Session.PacketHistory.Count;
		await HousingReenterAsync(buyer, token);
		await buyer.Session.SynchronizeAsync(token);
		DecodedBotServerPacket[] login = buyer.Session.PacketHistory.Skip(loginMark).ToArray();
		DecodedBotServerPacket win = Assert.Single(login, packet => IsHousingMessage(packet, "STR_MSG_HOUSING_BID_WIN"));
		Assert.Equal([target.Address.ToString(CultureInfo.InvariantCulture)], win.Get<string[]>("params"));
		Assert.Equal(0, ReadReceiveBids(Assert.Single(login, packet => packet.PacketType == typeof(SM_RECEIVE_BIDS))));
		Assert.Equal(new HouseOwnerInfo(target.Address, house.GetBuilding().GetId(), HouseOwnerState.HAS_OWNER.GetId() | HouseOwnerState.BIDDING_ALLOWED.GetId(),
			house.GetTownLevel(), 1, 0, 0, 0), ReadHouseOwnerInfo(Assert.Single(login, packet => packet.PacketType == typeof(SM_HOUSE_OWNER_INFO))));
		await HousingQuitAsync(buyer, token);

		// s06: Monday 09-28 00:00, MaintenanceTask: the new house gets its first rent date a week ahead (the first week is
		// free); the overdue house gets the second warning; the auto-fill lists new houses.
		await AdvanceHousingWorldToAsync([], monday2.AddSeconds(1), TimeSpan.FromHours(1), token);
		Assert.Equal(monday3, house.GetNextPay());
		AssertHouseRow(house, buyer.Id, sunday1, monday3);
		Assert.Equal(2, HousingLog(firstLogEntry, "MaintenanceTask").Count(line => line.StartsWith("Executing house maintenance for ", StringComparison.Ordinal)));
		Assert.Contains("Executing house maintenance for 2 houses", HousingLog(firstLogEntry, "MaintenanceTask"));
		AssertOverdueMailOffline(friend, "$$HS_OVERDUE_2ND", overdue, monday3, 0, monday2);
		Dictionary<Race, int> secondFill = ExpectedAutoFill(new HashSet<int>());
		Assert.Equal(secondFill[Race.ASMODIANS], HousingBidService.GetInstance().GetBidInfo(Race.ASMODIANS).Count);

		await PlayHousingWeekTwoAsync(buyer, rival, friend, house, overdue, monday2, kinah, firstLogEntry, token);

		// s15: Monday 10-05 00:00: the friend's house has gone two weeks unpaid past its due date: impounded, auctioned at
		// its default price, the third mail with 90% of that price back. The new owner's free week begins.
		House sold = house;
		await AdvanceHousingWorldToAsync([], monday3.AddSeconds(1), TimeSpan.FromHours(1), token);
		Assert.Equal(0, overdue.GetOwnerId());
		Assert.Null(overdue.GetAcquiredTime());
		Assert.Null(overdue.GetNextPay());
		HouseBids reauction = Assert.IsType<HouseBids>(overdue.GetBids());
		Assert.Equal(overdue.GetDefaultAuctionPrice(), reauction.GetInitialOffer().GetKinah());
		Assert.Equal(0, reauction.GetBidCount());
		AssertHouseRow(overdue, 0, null, null);
		Assert.Equal([(0, overdue.GetDefaultAuctionPrice())], BidRows(overdue.GetObjectId()));
		Assert.Contains($"Auctioned house {overdue.GetAddress().GetId()} because maintenance fee was overdue.", HousingLog(firstLogEntry, "MaintenanceTask"));
		long compensation = (long)(overdue.GetDefaultAuctionPrice() * 0.9f);
		Assert.Equal(10_800_000, compensation);
		AssertOverdueMailOffline(friend, "$$HS_OVERDUE_3RD", overdue, monday3, compensation, monday3);
		Assert.Equal(monday3.AddDays(7), sold.GetNextPay());
		// AuctionAutoFillTask ran first at this instant (the two Monday jobs fire in the order they were scheduled), so the
		// impounded house comes on top of the auto-fill.
		Dictionary<Race, int> thirdFill = ExpectedAutoFill(new HashSet<int> { overdue.GetObjectId() });
		Assert.Equal(thirdFill[Race.ASMODIANS] + 1, HousingBidService.GetInstance().GetBidInfo(Race.ASMODIANS).Count);

		// The friend logs in: no house, and a new third overdue mail: STR_MSG_HOUSING_SEQUESTRATE.
		friend.Session.BeginStep("s15", "impounded-owner-login");
		await AdvanceHousingWorldToAsync([], monday3.AddMinutes(5), TimeSpan.FromMinutes(1), token);
		loginMark = friend.Session.PacketHistory.Count;
		await HousingReenterAsync(friend, token);
		await friend.Session.SynchronizeAsync(token);
		login = friend.Session.PacketHistory.Skip(loginMark).ToArray();
		Assert.Single(login, packet => IsHousingMessage(packet, "STR_MSG_HOUSING_SEQUESTRATE"));
		Assert.Equal(new HouseOwnerInfo(0, 0, HouseOwnerState.SINGLE_HOUSE.GetId() | HouseOwnerState.BIDDING_ALLOWED.GetId(), 0, 0, 0, 0, 0),
			ReadHouseOwnerInfo(Assert.Single(login, packet => packet.PacketType == typeof(SM_HOUSE_OWNER_INFO))));
		Letter impoundMail = Assert.Single(fixture.World.GetPlayer(friend.Id).GetMailbox().GetLetters(), letter => letter.GetSenderName() == "$$HS_OVERDUE_3RD");
		await ClaimMailKinahAsync(friend, impoundMail, token);
		Assert.Equal(kinah[friend] + compensation, fixture.World.GetPlayer(friend.Id).GetInventory().GetKinah());
		await HousingQuitAsync(friend, token);
		policy.AssertClean();
	}

	/// <summary>
	/// Week two: the house is lived in. Monday: rent, the house gate, decoration, a house script, friends and strangers at
	/// the door, the relationship crystal and the way back. Tuesday: the owner registers the house for sale. Sunday: a bid in
	/// the last minutes prolongs its auction, and the house is sold.
	/// </summary>
	private async Task PlayHousingWeekTwoAsync(HousingCast buyer, HousingCast rival, HousingCast friend, House house, House overdue,
		DateTimeOffset monday, Dictionary<HousingCast, long> kinah, int firstLogEntry, CancellationToken token)
	{
		HousingService housing = HousingService.GetInstance();
		int address = house.GetAddress().GetId();
		DateTimeOffset morning = monday.AddHours(10), nextMonday = monday.AddDays(7);
		await AdvanceHousingWorldToAsync([], morning, TimeSpan.FromHours(1), token);

		// s07: the three Asmodians log in. The friend's house is overdue: STR_MSG_HOUSING_OVERDUE.
		HousingCast[] online = [buyer, rival, friend];
		foreach (HousingCast actor in online)
		{
			actor.Session.BeginStep("s07", "monday-login");
			int mark = actor.Session.PacketHistory.Count;
			await HousingReenterAsync(actor, token);
			await actor.Session.SynchronizeAsync(token);
			Assert.Equal(actor == friend ? 1 : 0, actor.Session.PacketHistory.Skip(mark).Count(packet => IsHousingMessage(packet, "STR_MSG_HOUSING_OVERDUE")));
		}
		Player buyerPlayer = fixture.World.GetPlayer(buyer.Id), rivalPlayer = fixture.World.GetPlayer(rival.Id), friendPlayer = fixture.World.GetPlayer(friend.Id);
		Assert.Same(house, buyerPlayer.GetActiveHouse());
		Assert.Same(overdue, friendPlayer.GetActiveHouse());
		Assert.Null(rivalPlayer.GetActiveHouse());

		// s08: rent. One week (the land's fee) moves the rent date a run further; four more would pass the client's cap of
		// four paid weeks and are ignored without an answer.
		buyer.Session.BeginStep("s08", "pay-rent");
		long fee = house.GetLand().GetMaintenanceFee();
		Assert.Equal(400_000, fee);
		int rentMark = buyer.Session.PacketHistory.Count;
		await buyer.Session.SendPacketAsync(GameClientPackets.PayHouseRent(1), token);
		await buyer.Session.SynchronizeAsync(token);
		kinah[buyer] -= fee;
		Assert.Equal((0, 1), ReadHousePayRent(Assert.Single(buyer.Session.PacketHistory.Skip(rentMark), packet => packet.PacketType == typeof(SM_HOUSE_PAY_RENT))));
		Assert.Equal(kinah[buyer], buyerPlayer.GetInventory().GetKinah());
		Assert.Equal(nextMonday.AddDays(7), house.GetNextPay());
		AssertHouseRow(house, buyer.Id, house.GetAcquiredTime(), nextMonday.AddDays(7));
		rentMark = buyer.Session.PacketHistory.Count;
		await buyer.Session.SendPacketAsync(GameClientPackets.PayHouseRent(3), token);
		await buyer.Session.SynchronizeAsync(token);
		Assert.DoesNotContain(buyer.Session.PacketHistory.Skip(rentMark), packet => packet.PacketType == typeof(SM_HOUSE_PAY_RENT));
		Assert.Equal(kinah[buyer], buyerPlayer.GetInventory().GetKinah());
		Assert.Equal(nextMonday.AddDays(7), house.GetNextPay());

		// s09: the friend asks the owner for friendship; the owner accepts.
		buyer.Session.BeginStep("s09", "friendship");
		friend.Session.BeginStep("s09", "friendship");
		await friend.Session.SendPacketAsync(friend.Session.Api.AddFriend(buyer.Name, "neighbours"), token);
		await buyer.Session.DrainServerPacketsAsync(token);
		await buyer.Session.WaitForPacketAsync(typeof(SM_QUESTION_WINDOW), token, packet => packet.Get<int>("code") == FriendInvitation);
		await buyer.Session.SendPacketAsync(buyer.Session.Api.Answer(1), token);
		await SyncHousingAsync(online, token);
		Assert.NotNull(buyerPlayer.GetFriendList().GetFriend(friend.Id));
		Assert.NotNull(friendPlayer.GetFriendList().GetFriend(buyer.Id));

		// s10: the house gate. Homeward Bound summons the owner's gate for 30 s; talking to it asks the gate question, and
		// accepting sends SM_HOUSE_TELEPORT, keeps the way back (Ishalgen allows returning to battle) and jumps into the house.
		buyer.Session.BeginStep("s10", "homeward-bound");
		float startX = buyerPlayer.GetX(), startY = buyerPlayer.GetY(), startZ = buyerPlayer.GetZ();
		int startMap = buyerPlayer.GetWorldId();
		Assert.True(buyerPlayer.GetWorldMapInstance().GetParent().CanReturnToBattle());
		float gateX = startX + 2, gateY = startY, gateZ = startZ;
		int gateMark = buyer.Session.PacketHistory.Count;
		await buyer.Session.SendPacketAsync(buyer.Session.Api.Cast(new SpellCastData(HomewardBound, 1, 1) { X = gateX, Y = gateY, Z = gateZ }), token);
		// The 6 s cast, then the skill's hit time (700 ms, SM_CASTSPELL_RESULT) before the effect applies.
		await AdvanceHousingWorldToAsync(online, SystemClock.UtcNow().AddMilliseconds(8_000), TimeSpan.FromMilliseconds(100), token);
		await buyer.Session.SynchronizeAsync(token);
		DecodedBotServerPacket[] castAnswer = buyer.Session.PacketHistory.Skip(gateMark).ToArray();
		if (!castAnswer.Any(packet => packet.PacketType == typeof(SM_NPC_INFO) && packet.Get<int>("npcId") == AsmodianHouseGate))
			Assert.Fail("Homeward Bound summoned no gate; the caster saw " + string.Join(Environment.NewLine, castAnswer.Where(packet =>
				packet.PacketType != typeof(SM_MOVE) && packet.PacketType != typeof(SM_NPC_INFO)).Select(packet => packet.PacketType.Name + " " +
				System.Text.Json.JsonSerializer.Serialize(packet.Fields))));
		DecodedBotServerPacket gateInfo = Assert.Single(castAnswer, packet => packet.PacketType == typeof(SM_NPC_INFO)
			&& packet.Get<int>("npcId") == AsmodianHouseGate);
		int gateId = gateInfo.Get<int>("objectId");
		Npc gate = Assert.IsAssignableFrom<Npc>(fixture.World.FindVisibleObject(gateId));
		Assert.Equal(buyer.Id, gate.GetCreatorId());
		Assert.Equal("housegate", gate.GetAi().GetName());
		await buyer.Session.SendPacketAsync(buyer.Session.Api.TalkTo(gateId), token);
		DecodedBotServerPacket gateQuestion = await buyer.Session.WaitForPacketAsync(typeof(SM_QUESTION_WINDOW), token,
			packet => packet.Get<int>("code") == SM_QUESTION_WINDOW.STR_ASK_GROUP_GATE_DO_YOU_ACCEPT_MOVE);
		int teleportMark = buyer.Session.PacketHistory.Count;
		await buyer.Session.SendPacketAsync(buyer.Session.Api.Answer(1), token);
		await buyer.Session.WaitForPacketAsync(typeof(SM_PLAYER_SPAWN), token, packet => packet.Get<int>("worldId") == Pernon);
		await buyer.Session.WaitForPacketAsync(typeof(SM_PLAYER_INFO), token, packet => packet.Get<int>("objectId") == buyer.Id);
		buyer.Session.AcceptTeleportPosition();
		await buyer.Session.SynchronizeAsync(token);
		Assert.Equal((address, buyer.Id), ReadHouseTeleport(Assert.Single(buyer.Session.PacketHistory.Skip(teleportMark),
			packet => packet.PacketType == typeof(SM_HOUSE_TELEPORT))));
		Assert.Equal(Pernon, buyerPlayer.GetWorldId());
		Assert.Equal(house.GetInstanceId(), buyerPlayer.GetInstanceId());
		Assert.Equal((house.GetX(), house.GetY(), house.GetZ()), (buyerPlayer.GetX(), buyerPlayer.GetY(), buyerPlayer.GetZ()));
		Assert.Equal(unchecked((byte)house.GetTeleportHeading()), buyerPlayer.GetHeading());
		Assert.Equal(startMap, buyerPlayer.GetBattleReturnMap());
		Assert.Equal([startX, startY, startZ], buyerPlayer.GetBattleReturnCoords());
		await SettleHousingZonesAsync(online, token);
		Assert.True(buyerPlayer.IsInsideZone(Aion.GameServer.World.Zone.ZoneName.Get(house.Name)), "the house gate did not put the owner inside his house");

		// s11: decoration: the registry, two tables placed, one moved, one taken back and deleted, a wallpaper put on the
		// first room's walls (line 8, INWALL_ANY room 0).
		buyer.Session.BeginStep("s11", "decorate");
		Assert.Equal(0, ItemService.AddItem(buyerPlayer, TableItem, 2));
		Assert.Equal(0, ItemService.AddItem(buyerPlayer, WallpaperItem, 1));
		await buyer.Session.SynchronizeAsync(token);
		await PlayHouseDecorationAsync(buyer, house, token);

		// s12: a house script in slot 1, and its removal (PlayerScripts, house_scripts). SM_HOUSE_SCRIPTS goes to the players
		// who see the owner, not to him (PacketSendUtility.broadcastPacket without self).
		buyer.Session.BeginStep("s12", "house-script");
		await PlayHouseScriptAsync(buyer, house, token);

		// s13: the door. Both visitors enter by the door; friends only sends the stranger out and keeps the friend; the
		// relationship crystal takes the friend home and back; closed sends everyone out; open lets the stranger in again,
		// and the owner's kick sends him out.
		foreach (HousingCast actor in online) actor.Session.BeginStep("s13", "door-and-visitors");
		await PlayHouseDoorAsync(buyer, rival, friend, house, overdue, online, token);

		// s14: the way back (CM_HOUSE_TELEPORT_BACK) to where Homeward Bound was cast.
		buyer.Session.BeginStep("s14", "house-teleport-back");
		await buyer.Session.SendPacketAsync(GameClientPackets.HouseTeleportBack(), token);
		await buyer.Session.WaitForPacketAsync(typeof(SM_PLAYER_SPAWN), token, packet => packet.Get<int>("worldId") == startMap);
		await buyer.Session.WaitForPacketAsync(typeof(SM_PLAYER_INFO), token, packet => packet.Get<int>("objectId") == buyer.Id);
		buyer.Session.AcceptTeleportPosition();
		Assert.Equal(startMap, buyerPlayer.GetWorldId());
		Assert.Equal(1, buyerPlayer.GetInstanceId());
		Assert.Equal((startX, startY, startZ), (buyerPlayer.GetX(), buyerPlayer.GetY(), buyerPlayer.GetZ()));
		Assert.Equal(0, buyerPlayer.GetBattleReturnMap());
		Assert.Null(buyerPlayer.GetBattleReturnCoords());
		// A second CM_HOUSE_TELEPORT_BACK has no way back to use.
		int backMark = buyer.Session.PacketHistory.Count;
		await buyer.Session.SendPacketAsync(GameClientPackets.HouseTeleportBack(), token);
		await buyer.Session.SynchronizeAsync(token);
		Assert.DoesNotContain(buyer.Session.PacketHistory.Skip(backMark), packet => packet.PacketType == typeof(SM_PLAYER_SPAWN));
		foreach (HousingCast actor in online)
			await HousingQuitAsync(actor, token);

		// s14b: the house was saved at logout (PlayerService.storePlayer: House.save, HouseRegistry.save): the settings in the
		// houses row (show owner name off, 0; door OPEN, 1 << 8; the notice) and the decoration in player_registered_items.
		Assert.Equal((1 << 8, "Welcome"), HouseSettingsRow(house.GetObjectId()));
		AssertRegisteredItems(buyer.Id, house);

		// Tuesday: the owner registers the house; the friend, overdue, cannot.
		DateTimeOffset tuesday = monday.AddDays(1).AddHours(10);
		await AdvanceHousingWorldToAsync([], tuesday, TimeSpan.FromHours(1), token);
		foreach (HousingCast actor in new[] { buyer, friend })
		{
			actor.Session.BeginStep("s16", "register-house");
			await HousingReenterAsync(actor, token);
		}
		buyerPlayer = fixture.World.GetPlayer(buyer.Id);
		const long startingPrice = 20_000_000;
		long registrationFee = (long)(startingPrice * HousingConfig.AUCTION_REGISTRATION_FEE_PERCENT);
		Assert.Equal(6_000_000, registrationFee);
		int registerMark = friend.Session.PacketHistory.Count;
		await friend.Session.SendPacketAsync(GameClientPackets.RegisterHouse(startingPrice), token);
		await friend.Session.SynchronizeAsync(token);
		Assert.Single(friend.Session.PacketHistory.Skip(registerMark), packet => IsHousingMessage(packet, "STR_MSG_HOUSING_CANT_AUCTION_OVERDUE"));
		Assert.Null(overdue.GetBids());
		registerMark = buyer.Session.PacketHistory.Count;
		await buyer.Session.SendPacketAsync(GameClientPackets.RegisterHouse(startingPrice), token);
		await buyer.Session.SynchronizeAsync(token);
		DecodedBotServerPacket[] registered = buyer.Session.PacketHistory.Skip(registerMark).ToArray();
		DecodedBotServerPacket mine = Assert.Single(registered, packet => IsHousingMessage(packet, "STR_MSG_HOUSING_AUCTION_MY_HOUSE"));
		Assert.Equal([address.ToString(CultureInfo.InvariantCulture)], mine.Get<string[]>("params"));
		Assert.Equal(0, ReadReceiveBids(Assert.Single(registered, packet => packet.PacketType == typeof(SM_RECEIVE_BIDS))));
		kinah[buyer] -= registrationFee;
		Assert.Equal(kinah[buyer], buyerPlayer.GetInventory().GetKinah());
		HouseBids sale = Assert.IsType<HouseBids>(house.GetBids());
		Assert.Equal(startingPrice, sale.GetInitialOffer().GetKinah());
		Assert.Equal(tuesday.ToUnixTimeMilliseconds(), sale.GetInitialOffer().GetTime());
		Assert.Equal(house.GetLand().GetSaleSignNpcId(), house.GetCurrentSign().GetNpcId());
		Assert.Equal([(0, startingPrice)], BidRows(house.GetObjectId()));
		// Registered already; and no bid on one's own house.
		registerMark = buyer.Session.PacketHistory.Count;
		await buyer.Session.SendPacketAsync(GameClientPackets.RegisterHouse(startingPrice), token);
		await buyer.Session.SynchronizeAsync(token);
		Assert.Single(buyer.Session.PacketHistory.Skip(registerMark), packet => IsHousingMessage(packet, "STR_MSG_HOUSING_AUCTION_FAIL_ALREADY_REGISTED"));
		Assert.Equal(kinah[buyer], buyerPlayer.GetInventory().GetKinah());
		await PlaceHouseBidAsync(buyer, sale.GetListIndex(), startingPrice, token);
		AssertBidRefused(buyer, "STR_MSG_HOUSING_CANT_BID_MY_HOUSE", []);
		// The seller's broker header names his registered house and its starting price.
		HouseBidsList withSale = await RequestHouseBidsAsync(buyer, token);
		Assert.Equal(sale.GetListIndex(), withSale.RegisteredListIndex);
		Assert.Equal(startingPrice, withSale.RegisteredStartingPrice);
		AssertHouseBidsList(withSale, Race.ASMODIANS, buyer);
		foreach (HousingCast actor in new[] { buyer, friend })
			await HousingQuitAsync(actor, token);

		// Sunday: the friend tries to register on a day that is not a registration day; the rival bids at 11:57, which
		// prolongs the auction to 12:05 (AuctionEndTask.tryProlongAuction: three minutes left, plus five).
		DateTimeOffset sunday = monday.AddDays(6).AddHours(12);
		await AdvanceHousingWorldToAsync([], sunday.AddMinutes(-5), TimeSpan.FromHours(1), token);
		HousingCast[] sundayCast = [buyer, rival, friend];
		foreach (HousingCast actor in sundayCast)
		{
			actor.Session.BeginStep("s17", "prolonged-sale");
			await HousingReenterAsync(actor, token);
		}
		buyerPlayer = fixture.World.GetPlayer(buyer.Id);
		rivalPlayer = fixture.World.GetPlayer(rival.Id);
		registerMark = friend.Session.PacketHistory.Count;
		await friend.Session.SendPacketAsync(GameClientPackets.RegisterHouse(startingPrice), token);
		await friend.Session.SynchronizeAsync(token);
		Assert.Single(friend.Session.PacketHistory.Skip(registerMark), packet => IsHousingMessage(packet, "STR_MSG_HOUSING_CANT_AUCTION_TIMEOUT"));
		await AdvanceHousingWorldToAsync(sundayCast, sunday.AddMinutes(-3), TimeSpan.FromSeconds(1), token);
		Assert.False(AuctionEndTask.GetInstance().IsAuctionProlonged(house.GetObjectId()));
		await PlaceHouseBidAsync(rival, sale.GetListIndex(), startingPrice, token);
		kinah[rival] -= startingPrice;
		AssertBidAccepted(rival, address, startingPrice, kinah[rival]);
		Assert.True(AuctionEndTask.GetInstance().IsAuctionProlonged(house.GetObjectId()));
		DateTimeOffset prolongedEnd = sunday.AddMinutes(5);
		HouseBidsList prolonged = await RequestHouseBidsAsync(rival, token);
		Assert.Equal((int)(prolongedEnd - SystemClock.UtcNow()).TotalSeconds,
			Assert.Single(prolonged.Entries, entry => entry.ListIndex == sale.GetListIndex()).RemainingSeconds);
		// 12:00: every other auction ends; this one goes on, still open for bids.
		await AdvanceHousingWorldToAsync(sundayCast, sunday.AddSeconds(1), TimeSpan.FromSeconds(1), token);
		HouseBids still = Assert.Single(HousingBidService.GetInstance().GetBidInfo(Race.ASMODIANS));
		Assert.Same(sale, still);
		Assert.Equal(buyer.Id, house.GetOwnerId());
		HouseBidsList afterEnd = await RequestHouseBidsAsync(rival, token);
		Assert.Equal((int)(prolongedEnd - SystemClock.UtcNow()).TotalSeconds, Assert.Single(afterEnd.Entries).RemainingSeconds);
		// 12:05: the prolonged auction ends: the rival owns the house; the seller gets the bid less 10% commission plus his
		// registration fee back (HouseBids.Bid.calculateSaleRewardKinah), and his decoration leaves the house with him.
		int[] saleMarks = HousingMarks(sundayCast);
		await AdvanceHousingWorldToAsync(sundayCast, prolongedEnd.AddSeconds(1), TimeSpan.FromSeconds(1), token);
		await SyncHousingAsync(sundayCast, token);
		Assert.Empty(HousingBidService.GetInstance().GetBidInfo(Race.ASMODIANS));
		Assert.False(AuctionEndTask.GetInstance().IsAuctionProlonged(house.GetObjectId()));
		Assert.Equal(rival.Id, house.GetOwnerId());
		Assert.Equal(prolongedEnd, house.GetAcquiredTime());
		Assert.Null(house.GetNextPay());
		Assert.Null(house.GetBids());
		Assert.Equal(rival.Name, house.GetButler().GetMasterName());
		Assert.Empty(house.GetRegistry().GetObjects());
		Assert.Empty(buyerPlayer.GetHouses());
		Assert.Same(house, rivalPlayer.GetActiveHouse());
		AssertHouseRow(house, rival.Id, prolongedEnd, null);
		long reward = startingPrice - (long)(startingPrice * HousingConfig.AUCTION_SALES_COMMISION_PERCENT) + registrationFee;
		Assert.Equal(24_000_000, reward);
		DecodedBotServerPacket[] sellerSaw = buyer.Session.PacketHistory.Skip(saleMarks[0]).ToArray();
		Assert.Equal([address.ToString(CultureInfo.InvariantCulture)],
			Assert.Single(sellerSaw, packet => IsHousingMessage(packet, "STR_MSG_HOUSING_AUCTION_SUCCESS")).Get<string[]>("params"));
		Assert.Equal((buyer.Id, address, 0), ReadHouseAcquire(Assert.Single(sellerSaw, packet => packet.PacketType == typeof(SM_HOUSE_ACQUIRE))));
		Assert.Equal(new HouseOwnerInfo(0, 0, HouseOwnerState.SINGLE_HOUSE.GetId() | HouseOwnerState.BIDDING_ALLOWED.GetId(), 0, 0, 0, 0, 0),
			ReadHouseOwnerInfo(Assert.Single(sellerSaw, packet => packet.PacketType == typeof(SM_HOUSE_OWNER_INFO))));
		Letter saleMail = AssertAuctionMail(buyer, house, AuctionResult.SUCCESS_SALE, prolongedEnd.ToUnixTimeMilliseconds(), reward);
		await ClaimMailKinahAsync(buyer, saleMail, token);
		kinah[buyer] += reward;
		Assert.Equal(kinah[buyer], buyerPlayer.GetInventory().GetKinah());
		DecodedBotServerPacket[] buyerSaw = rival.Session.PacketHistory.Skip(saleMarks[1]).ToArray();
		Assert.Equal([address.ToString(CultureInfo.InvariantCulture)],
			Assert.Single(buyerSaw, packet => IsHousingMessage(packet, "STR_MSG_HOUSING_BID_WIN")).Get<string[]>("params"));
		int ownerInfoIndex = Array.FindIndex(buyerSaw, packet => packet.PacketType == typeof(SM_HOUSE_OWNER_INFO));
		int acquireIndex = Array.FindIndex(buyerSaw, packet => packet.PacketType == typeof(SM_HOUSE_ACQUIRE));
		Assert.True(ownerInfoIndex >= 0 && acquireIndex > ownerInfoIndex, "the new owner did not get SM_HOUSE_OWNER_INFO before SM_HOUSE_ACQUIRE");
		Assert.Equal((rival.Id, address, 1), ReadHouseAcquire(buyerSaw[acquireIndex]));
		Assert.Equal(new HouseOwnerInfo(address, house.GetBuilding().GetId(), HouseOwnerState.HAS_OWNER.GetId() | HouseOwnerState.BIDDING_ALLOWED.GetId(),
			house.GetTownLevel(), 1, 0, 0, 0), ReadHouseOwnerInfo(buyerSaw[ownerInfoIndex]));
		AssertAuctionMail(rival, house, AuctionResult.WIN_BID, prolongedEnd.ToUnixTimeMilliseconds(), 0);
		Assert.Contains($"Address {address} sold by player {buyer.Id} for 20000000 kinah (1 bids; result: WIN_BID) to player {rival.Id}",
			HousingLog(firstLogEntry, "HOUSE_AUCTION_LOG"));
		foreach (HousingCast actor in sundayCast)
			await HousingQuitAsync(actor, token);
		// The seller's registry kept his things: the table back out of the house, the wallpaper discarded (reset).
		AssertRegistryAfterSale(buyer.Id);
	}

	/// <summary>CM_HOUSE_EDIT and CM_HOUSE_DECORATE as the client sends them, with Java's answers to each.</summary>
	private async Task PlayHouseDecorationAsync(HousingCast owner, House house, CancellationToken token)
	{
		Player player = fixture.World.GetPlayer(owner.Id);
		SimulationL0Session session = owner.Session;
		int address = house.GetAddress().GetId();

		// 1: decoration mode: SM_HOUSE_EDIT(1), the unplaced objects (none) and the default and unused decorations.
		DecodedBotServerPacket[] answer = await HouseEditAsync(session, GameClientPackets.HouseEdit(1), token);
		Assert.Equal([1], answer.Where(packet => packet.PacketType == typeof(SM_HOUSE_EDIT)).Select(packet => HousingBody(packet)[0]));
		DecodedBotServerPacket[] registry = answer.Where(packet => packet.PacketType == typeof(SM_HOUSE_REGISTRY)).ToArray();
		Assert.Equal(2, registry.Length);
		Assert.Equal(new byte[] { 1, 0, 0 }, HousingBody(registry[0]));
		Assert.Equal(house.GetBuilding().GetDefaultPartIds().Select(id => (0, id)), ReadHouseRegistryDecorations(registry[1]));

		// 3: each table leaves the cube for the registry as a new object; the wallpaper as a decoration.
		Item[] tables = player.GetInventory().GetItemsByItemId(TableItem).ToArray();
		Assert.Equal(2, tables.Sum(item => item.GetItemCount()));
		var tableObjects = new List<int>();
		for (int i = 0; i < 2; i++)
		{
			Item table = Assert.IsType<Item>(player.GetInventory().GetFirstItemByItemId(TableItem));
			answer = await HouseEditAsync(session, GameClientPackets.HouseEdit(3, table.GetObjectId()), token);
			HouseEditAdd added = ReadHouseEditAdd(Assert.Single(answer, packet => packet.PacketType == typeof(SM_HOUSE_EDIT)));
			Assert.Equal(new HouseEditAdd(1, added.ObjectId, TableObject, 0, 0), added);
			HouseObject obj = Assert.IsAssignableFrom<HouseObject>(house.GetRegistry().GetObjectByObjId(added.ObjectId));
			Assert.False(obj.IsSpawnedByPlayer());
			tableObjects.Add(added.ObjectId);
		}
		Assert.Equal(0, player.GetInventory().GetItemCountByItemId(TableItem));
		Item wallpaperItem = Assert.IsType<Item>(player.GetInventory().GetFirstItemByItemId(WallpaperItem));
		answer = await HouseEditAsync(session, GameClientPackets.HouseEdit(3, wallpaperItem.GetObjectId()), token);
		HouseEditAdd decorAdded = ReadHouseEditAdd(Assert.Single(answer, packet => packet.PacketType == typeof(SM_HOUSE_EDIT)));
		Assert.Equal(new HouseEditAdd(2, decorAdded.ObjectId, Wallpaper, 0, 0), decorAdded);
		HouseDecoration decor = Assert.IsType<HouseDecoration>(house.GetRegistry().GetDecorByObjId(decorAdded.ObjectId));
		Assert.Equal(-1, decor.GetRoom());
		Assert.Equal(0, player.GetInventory().GetItemCountByItemId(WallpaperItem));

		// 5: the first table placed beside the owner: SM_HOUSE_EDIT(5) with the place, the object in the world, SM_HOUSE_EDIT(4).
		float x1 = house.GetX() + 1, y1 = house.GetY() + 1, z1 = house.GetZ();
		answer = await HouseEditAsync(session, GameClientPackets.HouseEdit(5, tableObjects[0], x1, y1, z1, 90), token);
		AssertPlaced(answer, address, owner.Id, tableObjects[0], x1, y1, z1, 90);
		HouseObject first = house.GetRegistry().GetObjectByObjId(tableObjects[0]);
		Assert.True(first.IsSpawnedByPlayer());
		Assert.True(first.IsSpawned());
		Assert.Equal(90, first.GetRotation());
		AssertHouseObject(Assert.Single(answer, packet => packet.PacketType == typeof(SM_HOUSE_OBJECT)), address, owner.Id, tableObjects[0], x1, y1, z1, 90);

		// 6: moved: SM_HOUSE_EDIT(7), the old object deleted, SM_HOUSE_EDIT(5) with the new place, the object again.
		float x2 = house.GetX() - 1, y2 = house.GetY() + 2, z2 = house.GetZ();
		answer = await HouseEditAsync(session, GameClientPackets.HouseEdit(6, tableObjects[0], x2, y2, z2, 180), token);
		DecodedBotServerPacket[] moved = answer.Where(packet => packet.PacketType == typeof(SM_HOUSE_EDIT)).ToArray();
		Assert.Equal(2, moved.Length);
		Assert.Equal(7, HousingBody(moved[0])[0]);
		Assert.Equal(tableObjects[0], BinaryPrimitives.ReadInt32LittleEndian(HousingBody(moved[0]).AsSpan(1)));
		AssertHouseEditPlace(moved[1], address, owner.Id, tableObjects[0], x2, y2, z2, 180);
		Assert.Equal(tableObjects[0], ReadDeleteHouseObject(Assert.Single(answer, packet => packet.PacketType == typeof(SM_DELETE_HOUSE_OBJECT))));
		AssertHouseObject(Assert.Single(answer, packet => packet.PacketType == typeof(SM_HOUSE_OBJECT)), address, owner.Id, tableObjects[0], x2, y2, z2, 180);
		Assert.Equal((x2, y2, z2), (first.GetX(), first.GetY(), first.GetZ()));

		// The second table placed, then taken back (7: SM_HOUSE_EDIT(7), the object gone, SM_HOUSE_EDIT(3) back in the list)
		// and deleted from the registry (4: SM_HOUSE_EDIT(4) twice).
		answer = await HouseEditAsync(session, GameClientPackets.HouseEdit(5, tableObjects[1], x1, y1, z1, 0), token);
		AssertPlaced(answer, address, owner.Id, tableObjects[1], x1, y1, z1, 0);
		answer = await HouseEditAsync(session, GameClientPackets.HouseEdit(7, tableObjects[1]), token);
		DecodedBotServerPacket[] takenBack = answer.Where(packet => packet.PacketType == typeof(SM_HOUSE_EDIT)).ToArray();
		Assert.Equal(2, takenBack.Length);
		Assert.Equal(7, HousingBody(takenBack[0])[0]);
		Assert.Equal(new HouseEditAdd(1, tableObjects[1], TableObject, 0, 0), ReadHouseEditAdd(takenBack[1]));
		Assert.Equal(tableObjects[1], ReadDeleteHouseObject(Assert.Single(answer, packet => packet.PacketType == typeof(SM_DELETE_HOUSE_OBJECT))));
		HouseObject second = house.GetRegistry().GetObjectByObjId(tableObjects[1]);
		Assert.False(second.IsSpawnedByPlayer());
		Assert.Null(second.GetPosition()); // HouseObject.removeFromHouse
		answer = await HouseEditAsync(session, GameClientPackets.HouseEdit(4, tableObjects[1]), token);
		Assert.Equal([(4, 1, tableObjects[1]), (4, 1, tableObjects[1])], answer.Where(packet => packet.PacketType == typeof(SM_HOUSE_EDIT)).Select(ReadHouseEditRemove));
		Assert.Equal(Aion.GameServer.Model.GameObjects.IPersistable.PersistentState.DELETED, second.GetPersistentState());
		Assert.DoesNotContain(second, house.GetRegistry().GetNotSpawnedObjects());

		// CM_HOUSE_DECORATE: the wallpaper on line 8 (INWALL_ANY, room 0): SM_HOUSE_EDIT(4, 2) twice, and SM_HOUSE_UPDATE with it.
		answer = await HouseEditAsync(session, GameClientPackets.HouseDecorate(decor.GetObjectId(), Wallpaper, 8), token);
		Assert.Equal([(4, 2, decor.GetObjectId()), (4, 2, decor.GetObjectId())], answer.Where(packet => packet.PacketType == typeof(SM_HOUSE_EDIT)).Select(ReadHouseEditRemove));
		Assert.Equal(0, decor.GetRoom());
		HouseUpdate update = ReadHouseUpdate(Assert.Single(answer, packet => packet.PacketType == typeof(SM_HOUSE_UPDATE)));
		Assert.Equal(address, update.Address);
		Assert.Equal(owner.Id, update.OwnerId);
		int[] parts = ExpectedHouseParts(house);
		Assert.Equal(Wallpaper, parts[PartType.INWALL_ANY.GetStartLineNr() - 2]);
		Assert.Equal(parts, update.Parts);

		// 2: out of decoration mode.
		answer = await HouseEditAsync(session, GameClientPackets.HouseEdit(2), token);
		Assert.Equal([2], answer.Where(packet => packet.PacketType == typeof(SM_HOUSE_EDIT)).Select(packet => HousingBody(packet)[0]));
		Assert.Equal([first], house.GetRegistry().GetSpawnedObjects());
	}

	/// <summary>CM_HOUSE_SCRIPT: a script in slot 1, then its removal.</summary>
	private async Task PlayHouseScriptAsync(HousingCast owner, House house, CancellationToken token)
	{
		int address = house.GetAddress().GetId();
		byte[] script = Encoding.Unicode.GetBytes("<?xml version=\"1.0\" encoding=\"UTF-16\" ?><lboxes><lbox><id>1</id><name><![CDATA[Bell]]></name>" +
			"<desc><![CDATA[rings]]></desc><script><![CDATA[]]></script><icon>1</icon></lbox></lboxes>");
		byte[] compressed;
		using (var stream = new MemoryStream())
		{
			using (var zlib = new ZLibStream(stream, CompressionLevel.Optimal, leaveOpen: true))
				zlib.Write(script);
			compressed = stream.ToArray();
		}
		int mark = owner.Session.PacketHistory.Count;
		await owner.Session.SendPacketAsync(GameClientPackets.HouseScript(address, 1, compressed, script.Length), token);
		await owner.Session.SynchronizeAsync(token);
		Assert.DoesNotContain(owner.Session.PacketHistory.Skip(mark), packet => packet.PacketType == typeof(SM_HOUSE_SCRIPTS));
		PlayerScript stored = Assert.IsType<PlayerScript>(house.GetPlayerScripts().Get(1));
		Assert.True(stored.HasData());
		Assert.Equal(compressed, stored.CompressedBytes);
		Assert.Equal(script.Length, stored.UncompressedSize);
		Assert.Equal([(1, Encoding.Unicode.GetString(script))], HouseScriptRows(house.GetObjectId()));
		await owner.Session.SendPacketAsync(GameClientPackets.HouseScript(address, 1, null, 0), token);
		await owner.Session.SynchronizeAsync(token);
		Assert.False(Assert.IsType<PlayerScript>(house.GetPlayerScripts().Get(1)).HasData());
		Assert.Empty(HouseScriptRows(house.GetObjectId()));
	}

	/// <summary>CM_HOUSE_OPEN_DOOR, CM_HOUSE_SETTINGS, CM_HOUSE_TELEPORT and CM_HOUSE_KICK with a friend and a stranger.</summary>
	private async Task PlayHouseDoorAsync(HousingCast owner, HousingCast stranger, HousingCast friend, House house, House friendHouse,
		HousingCast[] online, CancellationToken token)
	{
		int address = house.GetAddress().GetId();
		Player ownerPlayer = fixture.World.GetPlayer(owner.Id), strangerPlayer = fixture.World.GetPlayer(stranger.Id), friendPlayer = fixture.World.GetPlayer(friend.Id);
		var houseZone = Aion.GameServer.World.Zone.ZoneName.Get(house.Name);
		(float X, float Y, float Z, byte H) outside = HouseDoor(house, outsideHouse: true), inside = HouseDoor(house, outsideHouse: false);

		// Both visitors come to the door and enter (HouseController.teleportNearHouseDoor, inside).
		foreach (HousingCast visitor in new[] { stranger, friend })
		{
			Player player = fixture.World.GetPlayer(visitor.Id);
			await TeleportForSetupAsync(visitor.Session, player, Pernon, outside.X, outside.Y, outside.Z, token, house.GetInstanceId());
			await EnterHouseAsync(visitor, address, token);
			Assert.Equal((inside.X, inside.Y, inside.Z, inside.H), (player.GetX(), player.GetY(), player.GetZ(), player.GetHeading()));
			await SettleHousingZonesAsync(online, token);
			Assert.True(player.IsInsideZone(houseZone), $"{visitor.Name} entered the door but is not inside the house");
		}

		// Friends only: the stranger is sent out (STR_MSG_HOUSING_REQUEST_OUT, outside the door), the friend stays.
		int[] marks = HousingMarks(online);
		await SetHouseDoorAsync(owner, 2, true, "", token);
		await WaitForHousingTeleportAsync(stranger, token);
		await SyncHousingAsync(online, token);
		AssertDoorSettings(owner, marks[0], house, HouseDoorState.CLOSED_EXCEPT_FRIENDS, "STR_MSG_HOUSING_ORDER_CLOSE_DOOR_WITHOUT_FRIENDS",
			"STR_MSG_HOUSING_ORDER_OUT_WITHOUT_FRIENDS");
		AssertSentOut(stranger, marks[1], outside);
		await SettleHousingZonesAsync(online, token);
		Assert.True(friendPlayer.IsInsideZone(houseZone));
		Assert.DoesNotContain(friend.Session.PacketHistory.Skip(marks[2]), packet => IsHousingMessage(packet, "STR_MSG_HOUSING_REQUEST_OUT"));
		// The stranger may not come back in.
		int strangerMark = stranger.Session.PacketHistory.Count;
		await stranger.Session.SendPacketAsync(GameClientPackets.HouseOpenDoor(address, false), token);
		await stranger.Session.SynchronizeAsync(token);
		Assert.Single(stranger.Session.PacketHistory.Skip(strangerMark), packet => IsHousingMessage(packet, "STR_MSG_HOUSING_CANT_ENTER_NO_RIGHT2"));
		// (The door's outside, 2.5 m past the butler, is still in the house's zone, which takes in its yard: being sent out
		// moves the visitor to the door, not out of HOUSE_<address>.)
		Assert.Equal((outside.X, outside.Y, outside.Z), (strangerPlayer.GetX(), strangerPlayer.GetY(), strangerPlayer.GetZ()));

		// The relationship crystal: the friend goes home from it (1), then back from his own crystal to the owner's house (2).
		Npc crystal = house.GetRelationshipCrystal();
		Assert.Equal("friendportal", crystal.GetAi().GetName());
		await friend.Session.SendPacketAsync(friend.Session.Api.Target(crystal.GetObjectId()), token);
		await friend.Session.SendPacketAsync(GameClientPackets.HouseTeleport(1, friend.Id, 0), token);
		await WaitForHousingTeleportAsync(friend, token);
		Assert.Equal((friendHouse.GetX(), friendHouse.GetY(), friendHouse.GetZ()), (friendPlayer.GetX(), friendPlayer.GetY(), friendPlayer.GetZ()));
		Assert.Equal(unchecked((byte)friendHouse.GetTeleportHeading()), friendPlayer.GetHeading());
		// The friend leaves his own house by its door and comes back in.
		int friendAddress = friendHouse.GetAddress().GetId();
		(float X, float Y, float Z, byte H) friendOutside = HouseDoor(friendHouse, outsideHouse: true), friendInside = HouseDoor(friendHouse, outsideHouse: false);
		await friend.Session.SendPacketAsync(GameClientPackets.HouseOpenDoor(friendAddress, true), token);
		await WaitForHousingTeleportAsync(friend, token);
		Assert.Equal(friendOutside, (friendPlayer.GetX(), friendPlayer.GetY(), friendPlayer.GetZ(), friendPlayer.GetHeading()));
		await EnterHouseAsync(friend, friendAddress, token);
		Assert.Equal(friendInside, (friendPlayer.GetX(), friendPlayer.GetY(), friendPlayer.GetZ(), friendPlayer.GetHeading()));
		Npc homeCrystal = friendHouse.GetRelationshipCrystal();
		await friend.Session.SendPacketAsync(friend.Session.Api.Target(homeCrystal.GetObjectId()), token);
		await friend.Session.SendPacketAsync(GameClientPackets.HouseTeleport(2, friend.Id, owner.Id), token);
		await WaitForHousingTeleportAsync(friend, token);
		Assert.Equal((house.GetX(), house.GetY(), house.GetZ()), (friendPlayer.GetX(), friendPlayer.GetY(), friendPlayer.GetZ()));
		await SettleHousingZonesAsync(online, token);
		Assert.True(friendPlayer.IsInsideZone(houseZone));

		// Closed: everyone out, the friend too.
		marks = HousingMarks(online);
		await SetHouseDoorAsync(owner, 3, true, "", token);
		await WaitForHousingTeleportAsync(friend, token);
		await SyncHousingAsync(online, token);
		AssertDoorSettings(owner, marks[0], house, HouseDoorState.CLOSED, "STR_MSG_HOUSING_ORDER_CLOSE_DOOR_ALL", "STR_MSG_HOUSING_ORDER_OUT_ALL");
		AssertSentOut(friend, marks[2], outside);

		// Open, the owner's name hidden and a notice on the sign: the stranger comes in; the owner's kick sends him out.
		marks = HousingMarks(online);
		await SetHouseDoorAsync(owner, 1, false, "Welcome", token);
		await SyncHousingAsync(online, token);
		AssertDoorSettings(owner, marks[0], house, HouseDoorState.OPEN, "STR_MSG_HOUSING_ORDER_OPEN_DOOR", null);
		Assert.False(house.IsShowOwnerName());
		Assert.Equal("Welcome", house.GetSignNotice());
		await EnterHouseAsync(stranger, address, token);
		await SettleHousingZonesAsync(online, token);
		Assert.True(strangerPlayer.IsInsideZone(houseZone));
		marks = HousingMarks(online);
		await owner.Session.SendPacketAsync(GameClientPackets.HouseKick(2), token);
		await WaitForHousingTeleportAsync(stranger, token);
		await SyncHousingAsync(online, token);
		Assert.Single(owner.Session.PacketHistory.Skip(marks[0]), packet => IsHousingMessage(packet, "STR_MSG_HOUSING_ORDER_OUT_ALL"));
		AssertSentOut(stranger, marks[1], outside);
	}

	/// <summary>Zone membership follows a teleport through ZoneUpdateService (every 500 ms): a second of world time settles it.</summary>
	private Task SettleHousingZonesAsync(IEnumerable<HousingCast> online, CancellationToken token) =>
		AdvanceHousingWorldToAsync(online, SystemClock.UtcNow().AddSeconds(1), TimeSpan.FromMilliseconds(100), token);

	private async Task EnterHouseAsync(HousingCast visitor, int address, CancellationToken token)
	{
		await visitor.Session.SendPacketAsync(GameClientPackets.HouseOpenDoor(address, false), token);
		await WaitForHousingTeleportAsync(visitor, token);
	}

	/// <summary>A teleport inside the map: TeleportService answers with SM_TELEPORT_LOC, then the player stands there.</summary>
	private static async Task WaitForHousingTeleportAsync(HousingCast actor, CancellationToken token)
	{
		await actor.Session.DrainServerPacketsAsync(token);
		await actor.Session.WaitForPacketAsync(typeof(SM_PLAYER_INFO), token, packet => packet.Get<int>("objectId") == actor.Id);
		actor.Session.AcceptTeleportPosition();
	}

	private static async Task SetHouseDoorAsync(HousingCast owner, byte doorState, bool showOwnerName, string notice, CancellationToken token)
	{
		await owner.Session.SendPacketAsync(GameClientPackets.HouseSettings(doorState, showOwnerName, notice), token);
		await owner.Session.DrainServerPacketsAsync(token);
	}

	private void AssertDoorSettings(HousingCast owner, int mark, House house, HouseDoorState door, string orderMessage, string? kickMessage)
	{
		DecodedBotServerPacket[] since = owner.Session.PacketHistory.Skip(mark).ToArray();
		Assert.Equal((owner.Id, house.GetAddress().GetId(), 1), ReadHouseAcquire(Assert.Single(since, packet => packet.PacketType == typeof(SM_HOUSE_ACQUIRE))));
		Assert.Single(since, packet => IsHousingMessage(packet, orderMessage));
		if (kickMessage != null)
			Assert.Single(since, packet => IsHousingMessage(packet, kickMessage));
		HouseUpdate update = ReadHouseUpdate(Assert.Single(since, packet => packet.PacketType == typeof(SM_HOUSE_UPDATE)));
		Assert.Equal((int)door.GetId(), update.DoorState);
		Assert.Equal(door, house.GetDoorState());
	}

	private void AssertSentOut(HousingCast visitor, int mark, (float X, float Y, float Z, byte H) outside)
	{
		Player player = fixture.World.GetPlayer(visitor.Id);
		Assert.Single(visitor.Session.PacketHistory.Skip(mark), packet => IsHousingMessage(packet, "STR_MSG_HOUSING_REQUEST_OUT"));
		Assert.Equal((outside.X, outside.Y, outside.Z), (player.GetX(), player.GetY(), player.GetZ()));
	}

	/// <summary>The heading from a house's relationship crystal towards its butler (Java: 0 to 119).</summary>
	private static int HouseDoorHeading(House house) =>
		PositionUtil.GetHeadingTowards(house.GetRelationshipCrystal(), house.GetButler().GetSpawn()!.GetX(), house.GetButler().GetSpawn()!.GetY());

	/// <summary>
	/// HouseController.teleportNearHouseDoor's place: the midpoint of butler and relationship crystal at the higher of their
	/// heights; the heading towards the butler less 30 (Java's signed byte) faces the door; outside, 2.5 m further that way;
	/// inside, facing in (60 more, or 60 less from 60 on).
	/// </summary>
	private static (float X, float Y, float Z, byte H) HouseDoor(House house, bool outsideHouse)
	{
		var butler = house.GetButler().GetSpawn()!;
		var crystal = house.GetRelationshipCrystal().GetSpawn()!;
		sbyte h = (sbyte)PositionUtil.GetHeadingTowards(house.GetRelationshipCrystal(), butler.GetX(), butler.GetY());
		h -= 30;
		float x = (butler.GetX() + crystal.GetX()) / 2, y = (butler.GetY() + crystal.GetY()) / 2, z = Math.Max(butler.GetZ(), crystal.GetZ());
		if (outsideHouse)
		{
			double radian = Math.PI / 180 * PositionUtil.NormalizeAngle(h * 3f); // Math.toRadians(convertHeadingToAngle(h))
			x += (float)(Math.Cos(radian) * 2.5f);
			y += (float)(Math.Sin(radian) * 2.5f);
		}
		else
		{
			h += (sbyte)(h < 60 ? 60 : -60);
		}
		return (x, y, z, unchecked((byte)h));
	}

	/// <summary>
	/// AuctionAutoFillTask's count per race with nothing listed before it: for each house type, up to its limit of the unowned
	/// houses of the race. <paramref name="excluded"/> are houses that were not unowned when it ran.
	/// </summary>
	private static Dictionary<Race, int> ExpectedAutoFill(ISet<int> excluded) => new[] { Race.ELYOS, Race.ASMODIANS }.ToDictionary(race => race,
		race => HousingConfig.AUCTION_AUTO_FILL_LIMITS.Sum(limit => Math.Min(limit.Value, AutoFillCandidates(race, limit.Key, excluded))));

	private static int AutoFillCandidates(Race race, HouseType type, ISet<int> excluded) => HousingService.GetInstance().GetCustomHouses()
		.Count(house => house.GetOwnerId() == 0 && house.MatchesLandRace(race) && house.GetHouseType() == type && !excluded.Contains(house.GetObjectId()));

	private static void AssertAutoFillLimits(List<HouseBids> listed, Race race)
	{
		Assert.Equal(4, HousingConfig.AUCTION_AUTO_FILL_LIMITS.Count);
		foreach (KeyValuePair<HouseType, int> limit in HousingConfig.AUCTION_AUTO_FILL_LIMITS)
			Assert.Equal(Math.Min(limit.Value, AutoFillCandidates(race, limit.Key, new HashSet<int>())),
				listed.Count(bids => HousingService.GetInstance().FindHouse(bids.GetHouseObjectId()).GetHouseType() == limit.Key));
	}

	private static void AssertHouseBidsList(HouseBidsList list, Race race, HousingCast viewer)
	{
		List<HouseBids> listed = HousingBidService.GetInstance().GetBidInfo(race);
		Assert.True(list.First && list.Last);
		Assert.Equal(listed.Select(bids => bids.GetListIndex()).Order(), list.Entries.Select(entry => entry.ListIndex).Order());
		foreach (HouseBidsEntry entry in list.Entries)
		{
			HouseBids bids = listed.Single(candidate => candidate.GetListIndex() == entry.ListIndex);
			House house = HousingService.GetInstance().FindHouse(bids.GetHouseObjectId());
			// No virtual time passes between the packet's writing and this check.
			Assert.Equal(new HouseBidsEntry(bids.GetListIndex(), house.GetLand().GetId(), house.GetAddress().GetId(), house.GetBuilding().GetId(),
				house.GetHouseType().GetId(), bids.GetHighestBid().GetKinah(), 100_000, bids.GetBidCount(),
				AuctionEndTask.GetInstance().GetRemainingAuctionSeconds(bids.GetHouseObjectId())), entry);
		}
	}

	private async Task<HouseBidsList> RequestHouseBidsAsync(HousingCast actor, CancellationToken token)
	{
		int mark = actor.Session.PacketHistory.Count;
		await actor.Session.SendPacketAsync(GameClientPackets.HouseBids(), token);
		await actor.Session.SynchronizeAsync(token);
		DecodedBotServerPacket[] parts = actor.Session.PacketHistory.Skip(mark).Where(packet => packet.PacketType == typeof(SM_HOUSE_BIDS)).ToArray();
		Assert.NotEmpty(parts);
		HouseBidsList[] lists = parts.Select(ReadHouseBids).ToArray();
		Assert.True(lists[0].First);
		Assert.True(lists[^1].Last);
		HouseBidsList last = lists[^1];
		return last with { First = true, Entries = lists.SelectMany(list => list.Entries).ToArray() };
	}

	private static async Task PlaceHouseBidAsync(HousingCast actor, int listIndex, long kinah, CancellationToken token)
	{
		actor.BidMark = actor.Session.PacketHistory.Count;
		await actor.Session.SendPacketAsync(GameClientPackets.PlaceHouseBid(listIndex, kinah), token);
		await actor.Session.SynchronizeAsync(token);
	}

	private void AssertBidAccepted(HousingCast actor, int address, long bid, long kinahAfter)
	{
		DecodedBotServerPacket[] since = actor.Session.PacketHistory.Skip(actor.BidMark).ToArray();
		Assert.Equal([address.ToString(CultureInfo.InvariantCulture)], Assert.Single(since, packet => IsHousingMessage(packet, "STR_MSG_HOUSING_BID_SUCCESS")).Get<string[]>("params"));
		Assert.Equal([bid.ToString(CultureInfo.InvariantCulture)], Assert.Single(since, packet => IsHousingMessage(packet, "STR_MSG_HOUSING_PRICE_CHANGE")).Get<string[]>("params"));
		Assert.Equal(0, ReadReceiveBids(Assert.Single(since, packet => packet.PacketType == typeof(SM_RECEIVE_BIDS))));
		Assert.Equal(kinahAfter, fixture.World.GetPlayer(actor.Id).GetInventory().GetKinah());
	}

	private static void AssertBidRefused(HousingCast actor, string message, string[] parameters, bool receiveBids = false)
	{
		DecodedBotServerPacket[] since = actor.Session.PacketHistory.Skip(actor.BidMark).ToArray();
		DecodedBotServerPacket refusal = Assert.Single(since, packet => packet.PacketType == typeof(SM_SYSTEM_MESSAGE) && packet.Get<string?>("name") is string name
			&& name.StartsWith("STR_MSG_HOUSING", StringComparison.Ordinal));
		Assert.Equal(message, refusal.Get<string?>("name"));
		Assert.Equal(parameters, refusal.Get<string[]>("params"));
		Assert.Equal(receiveBids ? 1 : 0, since.Count(packet => packet.PacketType == typeof(SM_RECEIVE_BIDS)));
	}

	private static void AssertOutbid(HousingCast actor, int mark)
	{
		DecodedBotServerPacket[] since = actor.Session.PacketHistory.Skip(mark).ToArray();
		Assert.Single(since, packet => IsHousingMessage(packet, "STR_MSG_HOUSING_BID_CANCEL"));
		Assert.Equal(0, ReadReceiveBids(Assert.Single(since, packet => packet.PacketType == typeof(SM_RECEIVE_BIDS))));
	}

	/// <summary>
	/// MailFormatter.sendHouseAuctionMail: sender $$HS_AUCTION_MAIL, the template's title and body formatted with the address,
	/// the time in seconds, the result id and the race id, and the kinah.
	/// </summary>
	private Letter AssertAuctionMail(HousingCast actor, House house, AuctionResult result, long timeMillis, long kinah)
	{
		Player player = fixture.World.GetPlayer(actor.Id);
		Letter letter = player.GetMailbox().GetLetters().Where(candidate => candidate.GetSenderName() == "$$HS_AUCTION_MAIL")
			.OrderBy(candidate => candidate.GetObjectId()).Last();
		AssertAuctionLetter(letter, player.GetRace(), house, result, timeMillis, kinah);
		return letter;
	}

	private static void AssertAuctionMailOffline(int playerId, House house, AuctionResult result, long timeMillis, long kinah)
	{
		Race race = PlayerService.GetOrLoadPlayerCommonData(playerId).GetRace();
		var template = DataManager.SYSTEM_MAIL_TEMPLATES.GetMailTemplate("$$HS_AUCTION_MAIL", "", race);
		var part = new HousingMailPart(house.GetAddress().GetId(), timeMillis / 1000, (int)result, race.GetRaceId());
		HousingMailRow letter = Assert.Single(HousingMailRows(playerId, "$$HS_AUCTION_MAIL"), row => row.Title == template.GetFormattedTitle(part));
		Assert.Equal(new HousingMailRow("$$HS_AUCTION_MAIL", template.GetFormattedTitle(part), template.GetFormattedMessage(part), kinah,
			timeMillis / 1000, true), letter);
	}

	private static void AssertAuctionLetter(Letter letter, Race race, House house, AuctionResult result, long timeMillis, long kinah)
	{
		var template = DataManager.SYSTEM_MAIL_TEMPLATES.GetMailTemplate("$$HS_AUCTION_MAIL", "", race);
		var part = new HousingMailPart(house.GetAddress().GetId(), timeMillis / 1000, (int)result, race.GetRaceId());
		Assert.Equal("$$HS_AUCTION_MAIL", letter.GetSenderName());
		Assert.Equal(template.GetFormattedTitle(part), letter.GetTitle());
		Assert.Equal(template.GetFormattedMessage(part), letter.GetMessage());
		Assert.Equal(kinah, letter.GetAttachedKinah());
		Assert.Null(letter.GetAttachedItem());
	}

	/// <summary>MailFormatter.sendHouseMaintenanceMail: the template of the days left, the address and the impound time in minutes.</summary>
	private void AssertOverdueMail(HousingCast actor, string sender, House house, DateTimeOffset impound, long kinah)
	{
		Letter letter = Assert.Single(fixture.World.GetPlayer(actor.Id).GetMailbox().GetLetters(), candidate => candidate.GetSenderName() == sender);
		AssertOverdueLetter(letter, sender, house, impound, kinah);
	}

	private static void AssertOverdueMailOffline(HousingCast actor, string sender, House house, DateTimeOffset impound, long kinah, DateTimeOffset sent)
	{
		var template = DataManager.SYSTEM_MAIL_TEMPLATES.GetMailTemplate(sender, "", Race.PC_ALL);
		var part = new HousingMailPart(house.GetAddress().GetId(), impound.ToUnixTimeMilliseconds() / 60000, 0, 0);
		Assert.Equal(new HousingMailRow(sender, template.GetFormattedTitle(null!), template.GetFormattedMessage(part), kinah, sent.ToUnixTimeSeconds(), true),
			Assert.Single(HousingMailRows(actor.Id, sender)));
	}

	private static void AssertOverdueLetter(Letter letter, string sender, House house, DateTimeOffset impound, long kinah)
	{
		var template = DataManager.SYSTEM_MAIL_TEMPLATES.GetMailTemplate(sender, "", Race.PC_ALL);
		var part = new HousingMailPart(house.GetAddress().GetId(), impound.ToUnixTimeMilliseconds() / 60000, 0, 0);
		Assert.Equal(sender, letter.GetSenderName());
		Assert.Equal(template.GetFormattedTitle(null!), letter.GetTitle());
		Assert.Equal(template.GetFormattedMessage(part), letter.GetMessage());
		Assert.Equal(kinah, letter.GetAttachedKinah());
	}

	/// <summary>The four parameters of the housing mail templates, as MailFormatter's MailPart formats them.</summary>
	private sealed class HousingMailPart(int address, long datetime, int resultId, int raceId) : Aion.GameServer.Model.Templates.Mail.MailPart
	{
		public override string GetParamValue(string name) => name switch
		{
			"address" => address.ToString(CultureInfo.InvariantCulture),
			"datetime" => datetime.ToString(CultureInfo.InvariantCulture),
			"resultid" => resultId.ToString(CultureInfo.InvariantCulture),
			"raceid" => raceId.ToString(CultureInfo.InvariantCulture),
			_ => "",
		};
	}

	private sealed record HousingMailRow(string Sender, string Title, string Message, long Kinah, long ReceivedSeconds, bool Unread);

	private static HousingMailRow[] HousingMailRows(int playerId, string sender)
	{
		using var connection = DatabaseFactory.GetConnection();
		connection.Open();
		using var command = connection.CreateCommand();
		command.CommandText = "SELECT sender_name, mail_title, mail_message, attached_kinah_count, UNIX_TIMESTAMP(recieved_time), unread FROM mail " +
			"WHERE mail_recipient_id = @id AND sender_name = @sender ORDER BY mail_unique_id";
		command.Parameters.AddWithValue("@id", playerId);
		command.Parameters.AddWithValue("@sender", sender);
		using var reader = command.ExecuteReader();
		var rows = new List<HousingMailRow>();
		while (reader.Read())
			rows.Add(new HousingMailRow(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetInt64(3),
				(long)Convert.ToDecimal(reader.GetValue(4), CultureInfo.InvariantCulture), reader.GetBoolean(5)));
		return rows.ToArray();
	}

	private async Task ClaimMailKinahAsync(HousingCast actor, Letter letter, CancellationToken token)
	{
		await actor.Session.SendPacketAsync(actor.Session.Api.CheckMailList(), token);
		await actor.Session.SendPacketAsync(actor.Session.Api.ReadMail(letter.GetObjectId()), token);
		await actor.Session.SendPacketAsync(actor.Session.Api.GetMailAttachment(letter.GetObjectId(), 1), token);
		await actor.Session.WaitForPacketAsync(typeof(SM_MAIL_SERVICE), token,
			packet => packet.Get<byte>("serviceId") == 5 && packet.Get<int>("letterId") == letter.GetObjectId() && packet.Get<byte>("success") == 1);
		await actor.Session.SynchronizeAsync(token);
		Assert.Equal(0, letter.GetAttachedKinah());
	}

	private async Task<DecodedBotServerPacket[]> HouseEditAsync(SimulationL0Session session, BotClientPacket packet, CancellationToken token)
	{
		int mark = session.PacketHistory.Count;
		await session.SendPacketAsync(packet, token);
		await session.SynchronizeAsync(token);
		return session.PacketHistory.Skip(mark).ToArray();
	}

	private static void AssertPlaced(DecodedBotServerPacket[] answer, int address, int ownerId, int objectId, float x, float y, float z, int rotation)
	{
		DecodedBotServerPacket[] edits = answer.Where(packet => packet.PacketType == typeof(SM_HOUSE_EDIT)).ToArray();
		Assert.Equal(2, edits.Length);
		AssertHouseEditPlace(edits[0], address, ownerId, objectId, x, y, z, rotation);
		Assert.Equal((4, 1, objectId), ReadHouseEditRemove(edits[1]));
	}

	// SM_HOUSE_EDIT 5: C 5, D address, D player, D object, D template, F F F, H rotation, D cooldown, D expiry, 4 dye, D 0, C type.
	private static void AssertHouseEditPlace(DecodedBotServerPacket packet, int address, int ownerId, int objectId, float x, float y, float z, int rotation)
	{
		var body = new HousingBodyReader(HousingBody(packet));
		Assert.Equal(5, body.C());
		Assert.Equal((address, ownerId, objectId, TableObject), (body.D(), body.D(), body.D(), body.D()));
		Assert.Equal((x, y, z), (body.F(), body.F(), body.F()));
		Assert.Equal(rotation, body.H());
		Assert.Equal((0, 0, 0, 0), (body.D(), body.D(), body.D(), body.D()));
		Assert.Equal(DataManager.HOUSING_OBJECT_DATA.GetTemplateById(TableObject).GetTypeId(), body.C());
		body.End();
	}

	// SM_HOUSE_OBJECT: D address, D owner, D object, D object, D template, F F F, H rotation, D cooldown, D expiry, 4 dye, D 0, C type.
	private static void AssertHouseObject(DecodedBotServerPacket packet, int address, int ownerId, int objectId, float x, float y, float z, int rotation)
	{
		var body = new HousingBodyReader(HousingBody(packet));
		Assert.Equal((address, ownerId, objectId, objectId, TableObject), (body.D(), body.D(), body.D(), body.D(), body.D()));
		Assert.Equal((x, y, z), (body.F(), body.F(), body.F()));
		Assert.Equal(rotation, body.H());
		Assert.Equal((0, 0, 0, 0), (body.D(), body.D(), body.D(), body.D()));
		Assert.Equal(DataManager.HOUSING_OBJECT_DATA.GetTemplateById(TableObject).GetTypeId(), body.C());
		body.End();
	}

	/// <summary>AbstractHouseInfoPacket's part ids: each PartType's rooms, the used decoration or the building's default.</summary>
	private static int[] ExpectedHouseParts(House house) => Enum.GetValues<PartType>()
		.SelectMany(type => Enumerable.Range(0, type.GetRooms()).Select(room => house.GetRegistry().GetUsedDecorId(type, room) ?? 0)).ToArray();

	private void AssertRegisteredItems(int playerId, House house)
	{
		HouseObject table = Assert.Single(house.GetRegistry().GetSpawnedObjects());
		HouseDecoration decor = Assert.Single(house.GetRegistry().GetDecors());
		var rows = RegisteredItemRows(playerId);
		Assert.Equal(2, rows.Length);
		// MySQL sends a FLOAT column back with six significant digits.
		var tableRow = Assert.Single(rows, row => row.ItemUniqueId == table.GetObjectId());
		Assert.Equal((TableObject, (int)table.GetHeading(), table.GetPlaceArea().ToString(), 0), (tableRow.ItemId, tableRow.H, tableRow.Area, tableRow.Room));
		Assert.Equal(table.GetX(), tableRow.X, 0.01f);
		Assert.Equal(table.GetY(), tableRow.Y, 0.01f);
		Assert.Equal(table.GetZ(), tableRow.Z, 0.01f);
		Assert.Contains((decor.GetObjectId(), Wallpaper, 0f, 0f, 0f, 0, "DECOR", 0), rows);
	}

	private static void AssertRegistryAfterSale(int playerId)
	{
		// HouseRegistry.reset on the owner change: the placed table is taken back out of the house (RESET_QUERY or its row
		// stored at 0,0,0 with area NONE); the used wallpaper is discarded.
		var rows = RegisteredItemRows(playerId);
		var table = Assert.Single(rows);
		Assert.Equal(TableObject, table.ItemId);
		Assert.Equal((0f, 0f, 0f, 0, "NONE"), (table.X, table.Y, table.Z, table.H, table.Area));
	}

	private static (int ItemUniqueId, int ItemId, float X, float Y, float Z, int H, string Area, int Room)[] RegisteredItemRows(int playerId)
	{
		using var connection = DatabaseFactory.GetConnection();
		connection.Open();
		using var command = connection.CreateCommand();
		command.CommandText = "SELECT item_unique_id, item_id, x, y, z, h, area, room FROM player_registered_items WHERE player_id = @id ORDER BY item_unique_id";
		command.Parameters.AddWithValue("@id", playerId);
		using var reader = command.ExecuteReader();
		var rows = new List<(int, int, float, float, float, int, string, int)>();
		while (reader.Read())
			rows.Add((reader.GetInt32(0), reader.GetInt32(1), reader.GetFloat(2), reader.GetFloat(3), reader.GetFloat(4), reader.IsDBNull(5) ? 0 : reader.GetInt32(5),
				reader.GetString(6), reader.GetInt32(7)));
		return rows.ToArray();
	}

	private static (int Settings, string? Notice) HouseSettingsRow(int houseId)
	{
		using var connection = DatabaseFactory.GetConnection();
		connection.Open();
		using var command = connection.CreateCommand();
		command.CommandText = "SELECT settings, sign_notice FROM houses WHERE id = @id";
		command.Parameters.AddWithValue("@id", houseId);
		using var reader = command.ExecuteReader();
		Assert.True(reader.Read());
		return (reader.GetInt32(0), reader.IsDBNull(1) ? null : reader.GetString(1));
	}

	private static (int ScriptId, string Script)[] HouseScriptRows(int houseId)
	{
		using var connection = DatabaseFactory.GetConnection();
		connection.Open();
		using var command = connection.CreateCommand();
		command.CommandText = "SELECT script_id, script FROM house_scripts WHERE house_id = @id ORDER BY script_id";
		command.Parameters.AddWithValue("@id", houseId);
		using var reader = command.ExecuteReader();
		var rows = new List<(int, string)>();
		while (reader.Read())
			rows.Add((reader.GetInt32(0), reader.GetString(1)));
		return rows.ToArray();
	}

	private static void AssertHouseRow(House house, int ownerId, DateTimeOffset? acquired, DateTimeOffset? nextPay)
	{
		using var connection = DatabaseFactory.GetConnection();
		connection.Open();
		using var command = connection.CreateCommand();
		command.CommandText = "SELECT player_id, address, building_id, UNIX_TIMESTAMP(acquire_time), UNIX_TIMESTAMP(next_pay) FROM houses WHERE id = @id";
		command.Parameters.AddWithValue("@id", house.GetObjectId());
		using var reader = command.ExecuteReader();
		Assert.True(reader.Read(), $"houses has no row {house.GetObjectId()}");
		long? Seconds(int ordinal) => reader.IsDBNull(ordinal) ? null : (long)Convert.ToDecimal(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
		Assert.Equal((ownerId, house.GetAddress().GetId(), house.GetBuilding().GetId(), acquired?.ToUnixTimeSeconds(), nextPay?.ToUnixTimeSeconds()),
			(reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2), Seconds(3), Seconds(4)));
		Assert.False(reader.Read());
	}

	private static (int PlayerId, long Bid)[] BidRows(int houseId)
	{
		using var connection = DatabaseFactory.GetConnection();
		connection.Open();
		using var command = connection.CreateCommand();
		command.CommandText = "SELECT player_id, bid FROM house_bids WHERE house_id = @id ORDER BY bid_time, bid";
		command.Parameters.AddWithValue("@id", houseId);
		using var reader = command.ExecuteReader();
		var rows = new List<(int, long)>();
		while (reader.Read())
			rows.Add((reader.GetInt32(0), reader.GetInt64(1)));
		return rows.ToArray();
	}

	private static void AssertBidRows(IEnumerable<HouseBids> listed)
	{
		foreach (HouseBids bids in listed)
			Assert.Equal([(0, bids.GetInitialOffer().GetKinah())], BidRows(bids.GetHouseObjectId()));
	}

	private string[] HousingLog(int firstEntry, string category) => fixture.LogCapture.Entries.Skip(firstEntry)
		.Where(entry => entry.Category == category).Select(entry => entry.Message).ToArray();

	private static bool IsHousingMessage(DecodedBotServerPacket packet, string name) =>
		packet.PacketType == typeof(SM_SYSTEM_MESSAGE) && packet.Get<string?>("name") == name;

	private static int[] HousingMarks(IEnumerable<HousingCast> cast) => cast.Select(actor => actor.Session.PacketHistory.Count).ToArray();

	private static async Task SyncHousingAsync(IEnumerable<HousingCast> cast, CancellationToken token)
	{
		foreach (HousingCast actor in cast.Where(actor => actor.Online))
			await actor.Session.SynchronizeAsync(token);
	}

	private static async Task HousingQuitAsync(HousingCast actor, CancellationToken token)
	{
		await actor.Session.QuitAsync(token);
		actor.Online = false;
	}

	/// <summary>Logs a known character back in: the character list, the same character, the world.</summary>
	private static async Task HousingReenterAsync(HousingCast actor, CancellationToken token)
	{
		await actor.Session.LoginCharacterListAsync(token);
		actor.Session.SelectCharacter(actor.Id, actor.Name);
		await actor.Session.EnterWorldAsync(token);
		actor.Online = true;
	}

	/// <summary>
	/// Moves the world's clock to <paramref name="target"/> in steps of at most <paramref name="step"/>, serializing every
	/// online bot's packets after each step (harness rule 7). With nobody online the clock is moved directly.
	/// </summary>
	private async Task AdvanceHousingWorldToAsync(IEnumerable<HousingCast> cast, DateTimeOffset target, TimeSpan step, CancellationToken token)
	{
		HousingCast[] online = cast.Where(actor => actor.Online).ToArray();
		while (SystemClock.UtcNow() < target)
		{
			TimeSpan next = target - SystemClock.UtcNow();
			if (next > step)
				next = step;
			if (online.Length == 0)
			{
				fixture.Clock.Advance(next);
				continue;
			}
			await online[0].Session.AdvanceAsync(next, token);
			foreach (HousingCast actor in online.Skip(1))
				await actor.Session.DrainServerPacketsAsync(token);
		}
	}

	private sealed record HouseBidsEntry(int ListIndex, int LandId, int Address, int BuildingId, int TypeId, long HighestBid, long Unknown, int BidCount,
		int RemainingSeconds);
	private sealed record HouseBidsList(bool First, bool Last, int LastBidListIndex, long LastBidKinah, int RegisteredListIndex, long RegisteredStartingPrice,
		HouseBidsEntry[] Entries)
	{
		public bool Equals(HouseBidsList? other) => other is not null && (First, Last, LastBidListIndex, LastBidKinah, RegisteredListIndex, RegisteredStartingPrice)
			== (other.First, other.Last, other.LastBidListIndex, other.LastBidKinah, other.RegisteredListIndex, other.RegisteredStartingPrice)
			&& Entries.SequenceEqual(other.Entries);
		public override int GetHashCode() => HashCode.Combine(First, Last, LastBidListIndex, Entries.Length);
	}
	private sealed record HouseOwnerInfo(int ActiveAddress, int ActiveBuilding, int State, int TownLevel, int WeeksUntilNextPay, int InactiveAddress,
		int InactiveBuilding, int SecondsUntilGraceEnd);
	private sealed record HouseEditAdd(int Store, int ObjectId, int TemplateId, int SecondsUntilExpiration, int TypeId);
	private sealed record HouseUpdate(int Address, int OwnerId, int DoorState, string OwnerName, bool ShowOwnerName, string Notice, int[] Parts);

	// The bot keeps these packets as raw bodies (they are not in its decoder inventory); their layouts are Java's writeImpl.
	private static byte[] HousingBody(DecodedBotServerPacket packet) => Convert.FromHexString(packet.Get<string>("bodyHex"));

	// SM_HOUSE_BIDS: C first, C last, D last bid's list index, Q its kinah, D registered house's list index, Q its starting
	// price, H count, each D index, D land, D address, D building, D type, Q highest bid, Q 100000, D bid count, D seconds left.
	private static HouseBidsList ReadHouseBids(DecodedBotServerPacket packet)
	{
		var body = new HousingBodyReader(HousingBody(packet));
		bool first = body.C() != 0, last = body.C() != 0;
		int lastBid = body.D();
		long lastKinah = body.Q();
		int registered = body.D();
		long registeredPrice = body.Q();
		int count = body.H();
		var entries = new HouseBidsEntry[count];
		for (int i = 0; i < count; i++)
			entries[i] = new HouseBidsEntry(body.D(), body.D(), body.D(), body.D(), body.D(), body.Q(), body.Q(), body.D(), body.D());
		body.End();
		return new HouseBidsList(first, last, lastBid, lastKinah, registered, registeredPrice, entries);
	}

	// SM_RECEIVE_BIDS: D.
	private static int ReadReceiveBids(DecodedBotServerPacket packet)
	{
		var body = new HousingBodyReader(HousingBody(packet));
		int value = body.D();
		body.End();
		return value;
	}

	// SM_HOUSE_ACQUIRE: D player, D address, D acquired.
	private static (int PlayerId, int Address, int Acquire) ReadHouseAcquire(DecodedBotServerPacket packet)
	{
		var body = new HousingBodyReader(HousingBody(packet));
		var value = (body.D(), body.D(), body.D());
		body.End();
		return value;
	}

	// SM_HOUSE_OWNER_INFO: D address, D building, C owner state, C town level, D weeks, D inactive address, D its building, D grace.
	private static HouseOwnerInfo ReadHouseOwnerInfo(DecodedBotServerPacket packet)
	{
		var body = new HousingBodyReader(HousingBody(packet));
		var info = new HouseOwnerInfo(body.D(), body.D(), body.C(), body.C(), body.D(), body.D(), body.D(), body.D());
		body.End();
		return info;
	}

	// SM_HOUSE_PAY_RENT: C 0, C weeks.
	private static (int Zero, int Weeks) ReadHousePayRent(DecodedBotServerPacket packet)
	{
		var body = new HousingBodyReader(HousingBody(packet));
		var value = (body.C(), body.C());
		body.End();
		return value;
	}

	// SM_HOUSE_TELEPORT: D address, D player.
	private static (int Address, int PlayerId) ReadHouseTeleport(DecodedBotServerPacket packet)
	{
		var body = new HousingBodyReader(HousingBody(packet));
		var value = (body.D(), body.D());
		body.End();
		return value;
	}

	// SM_HOUSE_REGISTRY 2: C 2, H count, each D object (0 for a default part), D part.
	private static (int ObjectId, int PartId)[] ReadHouseRegistryDecorations(DecodedBotServerPacket packet)
	{
		var body = new HousingBodyReader(HousingBody(packet));
		Assert.Equal(2, body.C());
		int count = body.H();
		var parts = new (int, int)[count];
		for (int i = 0; i < count; i++)
			parts[i] = (body.D(), body.D());
		body.End();
		return parts;
	}

	// SM_HOUSE_EDIT 3: C 3, C store, D object, D template, D seconds until expiration, 4 dye, D 0, C type.
	private static HouseEditAdd ReadHouseEditAdd(DecodedBotServerPacket packet)
	{
		var body = new HousingBodyReader(HousingBody(packet));
		Assert.Equal(3, body.C());
		int store = body.C(), objectId = body.D(), templateId = body.D(), expiration = body.D();
		Assert.Equal((0, 0), (body.D(), body.D()));
		var add = new HouseEditAdd(store, objectId, templateId, expiration, body.C());
		body.End();
		return add;
	}

	// SM_HOUSE_EDIT 4: C 4, C store, D object.
	private static (int Action, int Store, int ObjectId) ReadHouseEditRemove(DecodedBotServerPacket packet)
	{
		var body = new HousingBodyReader(HousingBody(packet));
		var value = (body.C(), body.C(), body.D());
		body.End();
		return value;
	}

	// SM_DELETE_HOUSE_OBJECT: D object.
	private static int ReadDeleteHouseObject(DecodedBotServerPacket packet)
	{
		var body = new HousingBodyReader(HousingBody(packet));
		int value = body.D();
		body.End();
		return value;
	}

	// SM_HOUSE_UPDATE: H 1, H 0, H 1, then AbstractHouseInfoPacket.writeCommonInfo: D 0, D address, D owner, D building type,
	// C 1, D building, C owner states, C door, S(25) owner name, D legion, C show owner name, S(64) notice, D per part room
	// (19), D 0, D 0, C 0, 6 emblem bytes.
	private static HouseUpdate ReadHouseUpdate(DecodedBotServerPacket packet)
	{
		var body = new HousingBodyReader(HousingBody(packet));
		Assert.Equal((1, 0, 1), (body.H(), body.H(), body.H()));
		Assert.Equal(0, body.D());
		int address = body.D(), owner = body.D();
		body.D();
		Assert.Equal(1, body.C());
		body.D();
		body.C();
		int door = body.C();
		string name = body.FixedS(25);
		body.D();
		bool show = body.C() != 0;
		string notice = body.FixedS(64);
		int[] parts = Enumerable.Range(0, Enum.GetValues<PartType>().Sum(type => type.GetRooms())).Select(_ => body.D()).ToArray();
		Assert.Equal((0, 0, 0), (body.D(), body.D(), body.C()));
		body.Skip(6);
		body.End();
		return new HouseUpdate(address, owner, door, name, show, notice, parts);
	}

	private struct HousingBodyReader(byte[] body)
	{
		private int position;
		public int C() => body[position++];
		public int H() { int value = BinaryPrimitives.ReadUInt16LittleEndian(body.AsSpan(position)); position += 2; return value; }
		public int D() { int value = BinaryPrimitives.ReadInt32LittleEndian(body.AsSpan(position)); position += 4; return value; }
		public long Q() { long value = BinaryPrimitives.ReadInt64LittleEndian(body.AsSpan(position)); position += 8; return value; }
		public float F() { float value = BinaryPrimitives.ReadSingleLittleEndian(body.AsSpan(position)); position += 4; return value; }
		// AionServerPacket.writeS(text, fixedLength): fixedLength + 1 UTF-16 characters, the text then zeros.
		public string FixedS(int fixedLength)
		{
			string text = Encoding.Unicode.GetString(body, position, (fixedLength + 1) * 2);
			position += (fixedLength + 1) * 2;
			int end = text.IndexOf('\0');
			return end < 0 ? text : text[..end];
		}
		public void Skip(int count) => position += count;
		public readonly void End() => Assert.Equal(body.Length, position);
	}
}
