using Aion.Bots.Api;
using Aion.Bots.Protocol;
using Aion.Bots.World;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.Bots.Scenarios;

public interface IPlayerCommandScenarioDriver
{
	BotApi Api { get; }
	IReadOnlyList<DecodedBotServerPacket> History { get; }
	Task PrepareAsync(CancellationToken token);
	Task StepAsync(string name, Func<CancellationToken, Task> action, CancellationToken token);
	Task SendAsync(BotClientPacket packet, CancellationToken token);
	Task SynchronizeAsync(CancellationToken token);
	Task DelayAsync(TimeSpan delay, CancellationToken token);
	Task VerifyStateAsync(string action, IReadOnlyDictionary<int, long> inventory, CancellationToken token);
	Task ReloginAsync(CancellationToken token);
}

/// <summary>L8C: all shipped player commands through normal chat, never direct handler calls.</summary>
public static class PlayerCommandScenario
{
	public static IReadOnlyList<string> Aliases { get; } = new[]
	{
		"advent", "buy", "decompose", "del", "easter", "faction", "gmlist", "help",
		"id", "lock", "noexp", "nomorph", "preview", "pvp", "questrestart", "symphony"
	};
	// These are disabled in the shipped configuration. Only the isolated L8C profile enables them.
	public static IReadOnlyList<string> EnabledCommands { get; } = new[] { "easter", "faction", "lock", "questrestart", "symphony" };
	public static IReadOnlyList<(int Id, int Count)> Grants { get; } = new[]
	{
		(BotWorldModel.KinahItemId, 20_000), (186000409, 40), (152000103, 2),
		(182005453, 2), (186000175, 50), (182007170, 3)
	};

	public static async Task RunAsync(IPlayerCommandScenarioDriver driver, CancellationToken token)
	{
		await driver.PrepareAsync(token); await driver.SynchronizeAsync(token);
		var expected = Totals();
		var exercised = new HashSet<string>(StringComparer.Ordinal);
		await Command("help", ".help", packets =>
		{
			string help = string.Join('\n', Messages(packets));
			Require(help.Contains("List of available commands (16):", StringComparison.Ordinal), "Expected exactly 16 ordinary player commands.");
			foreach (string alias in Aliases) Require(help.Contains("." + alias, StringComparison.Ordinal), $"Help omitted .{alias}.");
			Require(!help.Contains("//", StringComparison.Ordinal), "An ordinary subject can see staff commands.");
		});
		await Command("gmlist", ".gmlist", p => Message(p, "There is no GM online."));
		await Command("id", ".id 182005453", p => Message(p, "ID: 182005453"));
		await Command("pvp", ".pvp info", p => Message(p, "There are currently no players on the map."));
		await Command("questrestart", ".questrestart 1101", p => Message(p, "Only currently active quests can be restarted."));
		await Command("noexp-on", ".noexp", p => Message(p, "inactive"));
		await Command("noexp-off", ".noexp", p => ActiveMessage(p));
		await Command("nomorph-on", ".nomorph", p => Message(p, "inactive"));
		await Command("nomorph-off", ".nomorph", p => ActiveMessage(p));
		await Command("lock-on", ".lock enable", p => Message(p, "Your account is now locked."));
		await Command("lock-off", ".lock disable", p => Message(p, "Your account is unlocked."));
		await Command("faction", ".faction L8-command-probe", p =>
		{
			Message(p, "Simcommands: L8-command-probe"); Change(BotWorldModel.KinahItemId, -10_000);
		});
		await Command("del", ".del 182005453 1", p => { Message(p, "Deleted 1x"); Change(182005453, -1); });
		await Command("easter", ".easter 2", _ => { Change(186000175, -50); Change(186000147, 2); });
		await Command("symphony", ".symphony 1", _ => { Change(182007170, -3); Change(186000236, 10); });
		await Command("advent-show", ".advent show", p => Message(p, "164002167"));
		await Command("advent-get", ".advent get", _ => Change(164002167, 25));
		await Command("advent-duplicate", ".advent get", p => Message(p, "You have already opened today's advent calendar door on this account."));

		await driver.StepAsync("buy-with-confirmation", async ct =>
		{
			int start = driver.History.Count;
			await Say(".buy 166000194", ct); await driver.SynchronizeAsync(ct);
			var question = driver.History.Skip(start).Single(p => p.PacketType == typeof(SM_QUESTION_WINDOW));
			Require(question.Get<int>("code") == SM_QUESTION_WINDOW.STR_AIONJEWEL_SHOP_BUY_CONFIRM, "Wrong reward-purchase confirmation.");
			Require(question.Get<string[]>("params")[0] == "40", "Purchase quoted the wrong coin price.");
			await Verify("buy-before-confirm", ct);
			await driver.SendAsync(GameClientPackets.QuestionResponse(question.Get<int>("code"), 1, question.Get<int>("senderId")), ct);
			await driver.SynchronizeAsync(ct); Message(driver.History.Skip(start), "You have spent 40.");
			Change(186000409, -40); Change(166000194, 5); await Verify("buy", ct);
		}, token);

		await driver.StepAsync("decompose-one-of-two-through-timed-command", async ct =>
		{
			int start = driver.History.Count;
			await Say(".decompose 152000103 1", ct); await driver.SynchronizeAsync(ct);
			await driver.DelayAsync(TimeSpan.FromMilliseconds(3009), ct); await driver.SynchronizeAsync(ct);
			await Verify("decompose-before-completion", ct);
			await driver.DelayAsync(TimeSpan.FromMilliseconds(102), ct); await driver.SynchronizeAsync(ct);
			Message(driver.History.Skip(start), "Decomposing finished: Processed 1x");
			Change(152000103, -1); Change(152000102, 3); await Verify("decompose", ct);
			await driver.DelayAsync(TimeSpan.FromMilliseconds(3100), ct); await driver.SynchronizeAsync(ct);
			await Verify("decompose-stays-finished", ct);
		}, token);

		await driver.StepAsync("preview-expires-without-changing-owned-equipment", async ct =>
		{
			int start = driver.History.Count;
			await Say(".preview 100000002", ct); await driver.SynchronizeAsync(ct);
			Message(driver.History.Skip(start), "Previewing the following items for 10 seconds");
			Require(Appearances(start) == 1, "Preview did not send an appearance update.");
			var preview = driver.History.Skip(start).Single(p => p.PacketType == typeof(SM_UPDATE_PLAYER_APPEARANCE));
			Require(preview.Get<BotEquipmentAppearance[]>("equipment").Any(i => i.SkinId == 100000002), "Preview omitted the requested weapon skin.");
			await driver.DelayAsync(TimeSpan.FromMilliseconds(9999), ct); await driver.SynchronizeAsync(ct);
			Require(Appearances(start) == 1, "Preview ended before ten seconds."); await Verify("preview-active", ct);
			await driver.DelayAsync(TimeSpan.FromMilliseconds(1), ct); await driver.SynchronizeAsync(ct);
			Require(Appearances(start) == 2, "Preview failed to restore appearance at ten seconds.");
			var restored = driver.History.Skip(start).Last(p => p.PacketType == typeof(SM_UPDATE_PLAYER_APPEARANCE));
			Require(restored.Get<int>("playerObjectId") == preview.Get<int>("playerObjectId"), "Preview reset targeted another character.");
			Message(driver.History.Skip(start), "Preview time ended."); await Verify("preview-restored", ct);
		}, token);
		await driver.StepAsync("relogin-keeps-rewards-and-prevents-second-advent-claim", async ct =>
		{
			await driver.ReloginAsync(ct); await Verify("relogin", ct);
			int start = driver.History.Count; await Say(".advent get", ct); await driver.SynchronizeAsync(ct);
			Message(driver.History.Skip(start), "You have already opened today's advent calendar door on this account.");
			await Verify("advent-persisted", ct);
		}, token);
		Require(exercised.SetEquals(Aliases), "Not every player-command handler was exercised.");

		int Appearances(int start) => driver.History.Skip(start).Count(p => p.PacketType == typeof(SM_UPDATE_PLAYER_APPEARANCE));
		Dictionary<int, long> Totals() => driver.Api.World.Inventory.Values.GroupBy(i => i.ItemId).ToDictionary(g => g.Key, g => g.Sum(i => i.Count));
		void Change(int id, long delta)
		{
			long count = expected.GetValueOrDefault(id) + delta; Require(count >= 0, "Invalid expected inventory delta.");
			if (count == 0) expected.Remove(id); else expected[id] = count;
		}
		async Task Verify(string action, CancellationToken ct)
		{
			Require(expected.OrderBy(p => p.Key).SequenceEqual(Totals().OrderBy(p => p.Key)), $"{action}: whole-inventory totals differ.");
			await driver.VerifyStateAsync(action, expected, ct);
		}
		Task Say(string text, CancellationToken ct)
		{
			exercised.Add(text.Split(' ')[0][1..]); return driver.SendAsync(driver.Api.Say(text), ct);
		}
		Task Command(string name, string text, Action<IEnumerable<DecodedBotServerPacket>> check) => driver.StepAsync(name, async ct =>
		{
			int start = driver.History.Count; await Say(text, ct); await driver.SynchronizeAsync(ct);
			check(driver.History.Skip(start)); await Verify(name, ct);
		}, token);
	}
	private static IEnumerable<string> Messages(IEnumerable<DecodedBotServerPacket> packets) => packets.Where(p => p.PacketType == typeof(SM_MESSAGE)).Select(p => p.Get<string>("message"));
	private static void ActiveMessage(IEnumerable<DecodedBotServerPacket> packets)
	{
		string[] messages = Messages(packets).ToArray();
		Require(messages.Any(m => m.Contains("active", StringComparison.Ordinal) && !m.Contains("inactive", StringComparison.Ordinal)), "Missing re-enabled command response.");
	}
	private static void Message(IEnumerable<DecodedBotServerPacket> packets, string text) => Require(Messages(packets).Any(m => m.Contains(text, StringComparison.Ordinal)), "Missing command response: " + text);
	private static void Require(bool condition, string message) { if (!condition) throw new InvalidDataException("L8C: " + message); }
}
