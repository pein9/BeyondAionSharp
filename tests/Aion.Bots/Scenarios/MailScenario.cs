using Aion.Bots.Api;
using Aion.Bots.Protocol;
using Aion.Bots.World;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.Bots.Scenarios;

public interface IMailScenarioDriver
{
	BotApi Api { get; }
	int CharacterId { get; }
	string CharacterName { get; }
	Task StepAsync(string action, Func<CancellationToken, Task> operation, CancellationToken token);
	Task SendAsync(BotClientPacket packet, CancellationToken token);
	Task<DecodedBotServerPacket> WaitAsync(Type type, Func<DecodedBotServerPacket, bool> predicate, CancellationToken token);
	Task SynchronizeAsync(CancellationToken token);
	Task VerifyMailboxAsync(int letterId, long itemCount, long kinah, bool deleted, CancellationToken token);
	Task VerifyInventoryAsync(CancellationToken token);
}

/// <summary>E7: partial-stack normal mail, full-stack express return, fees, attachment claims and duplicate-claim safety.</summary>
public static class MailScenario
{
	public const int ItemId = 152000401; // COMMON Aria; read its price from checked-in static data.
	public const int SetupCount = 40;
	public const int AttachmentCount = 15;
	public const long AttachedKinah = 125;

	public static async Task RunAsync(IMailScenarioDriver first, IMailScenarioDriver second, CancellationToken token = default)
	{
		foreach (var driver in new[] { first, second }) await driver.SynchronizeAsync(token);
		Require(Totals(first).GetValueOrDefault(ItemId) == SetupCount && Totals(second).GetValueOrDefault(ItemId) == 0,
			"Mail setup must supply only the first player's Aria stack.");
		foreach (var (sender, recipient, letterType) in new[] { (first, second, (byte)0), (second, first, (byte)1) })
		{
			var senderExpected = Totals(sender);
			var recipientExpected = Totals(recipient);
			string title = letterType == 0 ? "E7 normal" : "E7 express";
			const string message = "Attachment conservation.";
			int letterId = 0;
			await BothStepAsync(sender, recipient, $"{title}-send-and-deliver", async stepToken =>
			{
				var prices = sender.Api.World.VendorPrices ?? throw new InvalidDataException("No SM_PRICES for mail fee.");
				int factor = letterType == 0 ? 1 : 5;
				long itemCommission = (long)(VendorScenario.ReadBasePrice(ItemId) * 0.02f * AttachmentCount * factor);
				long kinahCommission = (long)(AttachedKinah * 0.01f * factor);
				long fee = prices.ServicePrice((letterType == 0 ? 10 : 500) + itemCommission + kinahCommission);
				Require(fee > 0 && sender.Api.World.Kinah >= fee + AttachedKinah, "Insufficient mail setup kinah.");
				var item = sender.Api.World.Inventory.Values.Single(value => value.ItemId == ItemId);
				await sender.SendAsync(sender.Api.SendMail(recipient.CharacterName, title, message, item.ObjectId, AttachmentCount, AttachedKinah, letterType), stepToken);
				var result = await MailAsync(sender, 1, _ => true, stepToken);
				Require(result.Get<byte>("messageId") == 0, "Server refused valid mail.");
				Add(senderExpected, ItemId, -AttachmentCount);
				Add(senderExpected, BotWorldModel.KinahItemId, -fee - AttachedKinah);
				await sender.SynchronizeAsync(stepToken);
				AssertTotals(sender, senderExpected);
				// Send success precedes the other connection's delivery. Observe its mailbox notification first.
				await MailAsync(recipient, 0, packet => packet.Get<ushort>("totalCount") > 0, stepToken);
				var letters = await ListAsync(recipient, stepToken);
				var matches = letters.Where(row => (string)row["title"]! == title && (string)row["sender"]! == sender.CharacterName).ToArray();
				Require(matches.Length == 1, "Expected exactly one delivered scenario letter.");
				var letter = matches[0];
				letterId = (int)letter["letterId"]!;
				Require((int)letter["itemId"]! == ItemId && (long)letter["kinah"]! == AttachedKinah &&
					(byte)letter["letterType"]! == letterType && !(bool)letter["isRead"]!, "Mail list lost attachment metadata.");
				AssertTotals(recipient, recipientExpected); // Attachments are not in the cube yet.
				await recipient.VerifyMailboxAsync(letterId, AttachmentCount, AttachedKinah, false, stepToken);
			}, token);
			await BothStepAsync(sender, recipient, $"{title}-read-and-claim", async stepToken =>
			{
				await recipient.SendAsync(recipient.Api.ReadMail(letterId), stepToken);
				var read = await MailAsync(recipient, 3, packet => packet.Get<int>("letterId") == letterId, stepToken);
				Require(read.Get<int>("recipientId") == recipient.CharacterId && read.Get<string>("sender") == sender.CharacterName &&
					read.Get<string>("title") == title && read.Get<string>("message") == message && read.Get<int>("itemId") == ItemId &&
					read.Get<long>("itemCount") == AttachmentCount && read.Get<int>("kinah") == AttachedKinah &&
					read.Get<byte>("letterType") == letterType, "Read-mail content or attachment mismatch.");
				foreach (byte attachment in new byte[] { 0, 1 })
				{
					await recipient.SendAsync(recipient.Api.GetMailAttachment(letterId, attachment), stepToken);
					await MailAsync(recipient, 5, packet => packet.Get<int>("letterId") == letterId && packet.Get<byte>("attachmentType") == attachment && packet.Get<byte>("success") == 1, stepToken);
				}
				Add(recipientExpected, ItemId, AttachmentCount);
				Add(recipientExpected, BotWorldModel.KinahItemId, AttachedKinah);
				await recipient.SynchronizeAsync(stepToken);
				AssertTotals(recipient, recipientExpected);
				await recipient.VerifyMailboxAsync(letterId, 0, 0, false, stepToken);
				// Repeat both claims: no second item, no second kinah payment, no audit warning.
				await recipient.SendAsync(recipient.Api.GetMailAttachment(letterId, 0), stepToken);
				await recipient.SendAsync(recipient.Api.GetMailAttachment(letterId, 1), stepToken);
				await recipient.SynchronizeAsync(stepToken);
				AssertTotals(recipient, recipientExpected);
				await recipient.SendAsync(recipient.Api.ReadMail(letterId), stepToken);
				var empty = await MailAsync(recipient, 3, packet => packet.Get<int>("letterId") == letterId, stepToken);
				Require(empty.Get<int>("itemObjectId") == 0 && empty.Get<int>("kinah") == 0, "Claimed attachments remain on the letter.");
				await recipient.SendAsync(recipient.Api.DeleteMail(letterId), stepToken);
				await MailAsync(recipient, 6, packet => packet.Get<int[]>("deletedIds").SequenceEqual(new[] { letterId }), stepToken);
				Require(!(await ListAsync(recipient, stepToken)).Any(row => (int)row["letterId"]! == letterId), "Deleted mail is still listed.");
				await recipient.VerifyMailboxAsync(letterId, 0, 0, true, stepToken);
				foreach (var driver in new[] { sender, recipient }) await driver.VerifyInventoryAsync(stepToken);
				AssertTotals(sender, senderExpected);
			}, token);
		}
	}

	private static Task BothStepAsync(IMailScenarioDriver sender, IMailScenarioDriver recipient, string action,
		Func<CancellationToken, Task> operation, CancellationToken token) =>
		sender.StepAsync(action, inner => recipient.StepAsync(action, operation, inner), token);

	private static Task<DecodedBotServerPacket> MailAsync(IMailScenarioDriver driver, byte service,
		Func<DecodedBotServerPacket, bool> predicate, CancellationToken token) => driver.WaitAsync(typeof(SM_MAIL_SERVICE),
			packet => packet.Get<byte>("serviceId") == service && predicate(packet), token);
	private static async Task<List<IReadOnlyDictionary<string, object?>>> ListAsync(IMailScenarioDriver driver, CancellationToken token)
	{
		await driver.SendAsync(driver.Api.CheckMailList(), token);
		var letters = new List<IReadOnlyDictionary<string, object?>>();
		while (true)
		{
			var part = await MailAsync(driver, 2, _ => true, token);
			letters.AddRange(part.Get<List<IReadOnlyDictionary<string, object?>>>("letters"));
			if (part.Get<bool>("lastPacket")) return letters;
		}
	}
	private static Dictionary<int, long> Totals(IMailScenarioDriver driver) => driver.Api.World.Inventory.Values.GroupBy(item => item.ItemId)
		.ToDictionary(group => group.Key, group => group.Sum(item => item.Count));
	private static void Add(Dictionary<int, long> totals, int id, long count)
	{
		totals[id] = totals.GetValueOrDefault(id) + count;
		if (totals[id] == 0) totals.Remove(id);
	}
	private static void AssertTotals(IMailScenarioDriver driver, Dictionary<int, long> expected) => Require(
		expected.OrderBy(pair => pair.Key).SequenceEqual(Totals(driver).OrderBy(pair => pair.Key)), $"Mail inventory mismatch for {driver.CharacterName}.");
	private static void Require(bool condition, string message)
	{
		if (!condition) throw new InvalidDataException(message);
	}
}
