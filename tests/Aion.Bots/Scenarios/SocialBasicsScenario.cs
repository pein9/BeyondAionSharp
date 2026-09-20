using Aion.Bots.Api;
using Aion.Bots.Protocol;
using Aion.Bots.Timing;
using Aion.Bots.World;
using Aion.GameServer.Model;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.SkillEngine.Model;
using System.Xml;
using System.Xml.Serialization;

namespace Aion.Bots.Scenarios;

public interface ISocialBasicsDriver
{
	BotApi Api { get; }
	int CharacterId { get; }
	string CharacterName { get; }
	BotPosition CurrentPosition { get; }
	Task StepAsync(string action, Func<CancellationToken, Task> operation, CancellationToken token);
	Task SendAsync(BotClientPacket packet, CancellationToken token);
	Task<DecodedBotServerPacket> WaitAsync(Type type, Func<DecodedBotServerPacket, bool> predicate, CancellationToken token);
	Task SynchronizeAsync(CancellationToken token);
	Task DelayAsync(TimeSpan duration, CancellationToken token);
	Task MoveAsync(BotPosition position, CancellationToken token);
	Task VerifyAsync(int otherId, string legionName, CancellationToken token);
}

/// <summary>S1: two access-zero level-ten characters at Losadis, using only ordinary client actions after setup.</summary>
public static class SocialBasicsScenario
{
	public const int RegistrarId = 203806;
	public static readonly BotPosition Registrar = new(1917.46f, 1388.3f, 590.365f, 40);
	public const long LegionFee = 10_000;
	private static readonly Lazy<(BotMotionTiming Timing, SkillTemplate Skill)> DuelTiming = new(LoadDuelTiming);

	public static ushort DuelHitTime(BotPosition source, BotPosition target, Race race = Race.ELYOS)
	{
		if (race is not (Race.ELYOS or Race.ASMODIANS)) throw new ArgumentOutOfRangeException(nameof(race));
		var (timing, skill) = DuelTiming.Value;
		double distance = Math.Sqrt(Math.Pow(source.X - target.X, 2) + Math.Pow(source.Y - target.Y, 2) + Math.Pow(source.Z - target.Z, 2));
		int travelMillis = checked((int)Math.Ceiling(distance / skill.GetAmmoSpeed() * 1000));
		// Subjects use unmodified starter spellbooks and attack speed; soak includes both races.
		return checked((ushort)timing.CalculateClientHitTime(skill, new BotMotionProfile(race, Gender.MALE, BotWeaponMotionType.Book), travelMillis));
	}

	private static (BotMotionTiming, SkillTemplate) LoadDuelTiming()
	{
		string directory = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ScenarioManifest.FindDefaultPath())!,
			"..", "..", "game-server", "data", "static_data", "skills"));
		using var reader = XmlReader.Create(Path.Combine(directory, "skill_templates.xml"));
		while (reader.Read())
		{
			if (reader.NodeType != XmlNodeType.Element || reader.LocalName != "skill_template" || reader.GetAttribute("skill_id") != "1282") continue;
			using var subtree = reader.ReadSubtree();
			var skill = (SkillTemplate?)new XmlSerializer(typeof(SkillTemplate), new XmlRootAttribute("skill_template")).Deserialize(subtree)
				?? throw new InvalidDataException("Missing Flame Bolt timing template.");
			if (skill.GetAmmoSpeed() <= 0 || skill.GetMotion()?.GetName() == null) throw new InvalidDataException("Flame Bolt timing data changed.");
			return (BotMotionTiming.Load(Path.Combine(directory, "motion_times.xml")), skill);
		}
		throw new InvalidDataException("Missing Flame Bolt skill 1282.");
	}

	public static async Task RunAsync(ISocialBasicsDriver first, ISocialBasicsDriver second, string legionName,
		CancellationToken token = default)
	{
		await BothSyncAsync(token);
		Require(first.Api.World.Level >= 10 && second.Api.World.Level >= 10, "S1 whispers require level-ten subjects.");
		await BothStepAsync("invite-and-accept-group", async ct =>
		{
			await first.SendAsync(first.Api.InviteToGroup(second.CharacterName), ct);
			await second.WaitAsync(typeof(SM_QUESTION_WINDOW), packet => packet.Get<int>("code") == 60000, ct);
			await second.SendAsync(second.Api.Answer(1), ct);
			await first.WaitAsync(typeof(SM_GROUP_INFO), packet => packet.Get<int>("leaderId") == first.CharacterId, ct);
			await second.WaitAsync(typeof(SM_GROUP_INFO), packet => packet.Get<int>("leaderId") == first.CharacterId, ct);
			await BothSyncAsync(ct);
			Require(first.Api.World.GroupId > 0 && first.Api.World.GroupId == second.Api.World.GroupId, "Clients disagree on group identity.");
			Require(first.Api.World.GroupMembers.ContainsKey(second.CharacterId) && second.Api.World.GroupMembers.ContainsKey(first.CharacterId),
				"Group roster did not show the other member to both clients.");
		}, token);
		await BothStepAsync("bidirectional-whispers", async ct =>
		{
			foreach (var (sender, recipient, message) in new[] { (first, second, "S1 hello, party member!"), (second, first, "S1 reply received.") })
			{
				await sender.SendAsync(sender.Api.Whisper(recipient.CharacterName, message), ct);
				var received = await recipient.WaitAsync(typeof(SM_MESSAGE), packet => packet.Get<string>("message") == message, ct);
				Require(received.Get<byte>("chatType") == (byte)ChatType.WHISPER && received.Get<string>("senderName") == sender.CharacterName &&
					received.Get<int>("senderObjectId") == sender.CharacterId, "Whisper lost its type or sender identity.");
			}
		}, token);
		await BothStepAsync("create-legion-at-registrar", async ct =>
		{
			await first.MoveAsync(Registrar with { X = Registrar.X - 1 }, ct);
			await first.SynchronizeAsync(ct);
			int npc = first.Api.World.Objects.Values.Single(value => value.TemplateId == RegistrarId).ObjectId;
			await first.SendAsync(first.Api.TalkTo(npc), ct);
			await first.WaitAsync(typeof(SM_DIALOG_WINDOW), packet => packet.Get<int>("targetObjectId") == npc, ct);
			await first.SendAsync(first.Api.SelectDialog(npc, 5), ct);
			// CREATE_LEGION action 5 opens DialogPage.CREATE_LEGION (page 2), not page 5.
			await first.WaitAsync(typeof(SM_DIALOG_WINDOW), packet => packet.Get<int>("targetObjectId") == npc && packet.Get<ushort>("dialogPageId") == 2, ct);
			var before = Totals(first);
			Require(before.GetValueOrDefault(BotWorldModel.KinahItemId) >= LegionFee, "Missing legion setup kinah.");
			await first.SendAsync(first.Api.CreateLegion(legionName), ct);
			await first.WaitAsync(typeof(SM_SYSTEM_MESSAGE), packet => packet.Get<string?>("name") == "STR_GUILD_CREATED", ct);
			await first.SynchronizeAsync(ct);
			before[BotWorldModel.KinahItemId] -= LegionFee;
			Require(before.OrderBy(pair => pair.Key).SequenceEqual(Totals(first).OrderBy(pair => pair.Key)), "Legion creation changed something other than the exact fee.");
			Require(first.Api.World.LegionName == legionName && first.Api.World.LegionLevel == 1 &&
				first.Api.World.LegionMembers.TryGetValue(first.CharacterId, out var founder) && founder.Rank == 0,
				"Legion founder did not receive level-one legion/Brigade General state.");
			await first.SendAsync(first.Api.CloseDialog(npc), ct);
		}, token);
		await BothStepAsync("invite-and-accept-legion", async ct =>
		{
			await first.SendAsync(first.Api.InviteToLegion(second.CharacterName), ct);
			var question = await second.WaitAsync(typeof(SM_QUESTION_WINDOW), packet => packet.Get<int>("code") == 80001, ct);
			Require(question.Get<string[]>("params")[0] == legionName, "Wrong legion invitation.");
			await second.SendAsync(second.Api.Answer(1), ct);
			foreach (var member in new[] { first, second })
				await member.WaitAsync(typeof(SM_LEGION_ADD_MEMBER), packet => packet.Get<int>("objectId") == second.CharacterId, ct);
			await BothSyncAsync(ct);
			foreach (var member in new[] { first, second })
			{
				Require(member.Api.World.LegionName == legionName && member.Api.World.LegionMembers.Count == 2, "Legion roster must contain exactly two members.");
				Require(member.Api.World.LegionMembers[first.CharacterId].Rank == 0 && member.Api.World.LegionMembers[second.CharacterId].Rank == 4,
					"Founder/invitee legion ranks are wrong.");
			}
		}, token);
		await BothStepAsync("duel-request-accept-and-fight", async ct =>
		{
			// Step back from the registrar's furniture before dueling; talking range alone does not prove LOS.
			await first.MoveAsync(Registrar with { X = Registrar.X - 4 }, ct);
			await second.MoveAsync(Registrar with { X = Registrar.X - 5 }, ct);
			await BothSyncAsync(ct);
			await RunDuelAsync(first, second, Race.ELYOS, ct);
		}, token);
		await BothStepAsync("leave-group-and-verify-cleanup", async ct =>
		{
			await second.SendAsync(second.Api.LeaveGroup(), ct);
			foreach (var member in new[] { first, second })
				await member.WaitAsync(typeof(SM_LEAVE_GROUP_MEMBER), _ => true, ct);
			await BothSyncAsync(ct);
			foreach (var (member, other) in new[] { (first, second), (second, first) })
			{
				Require(member.Api.World.GroupId == null && member.Api.World.GroupMembers.Count == 0, "Disbanded group remains on a client.");
				Require(member.Api.Timing.BlockingActivities.Count == 0, "Social scenario left a blocking action.");
				await member.VerifyAsync(other.CharacterId, legionName, ct);
			}
		}, token);

		Task BothStepAsync(string action, Func<CancellationToken, Task> operation, CancellationToken ct) =>
			first.StepAsync(action, inner => second.StepAsync(action, operation, inner), ct);
		async Task BothSyncAsync(CancellationToken ct) { await first.SynchronizeAsync(ct); await second.SynchronizeAsync(ct); }
	}

	/// <summary>Recover through ordinary resting and server regeneration; never supply HP or MP.</summary>
	public static async Task RecoverForDuelAsync(ISocialBasicsDriver first, ISocialBasicsDriver second, CancellationToken token)
	{
		await SyncAsync();
		foreach (var actor in new[] { first, second })
			Require(!actor.Api.World.IsDead && actor.Api.World.MaxHp > 0 && actor.Api.World.MaxMp > 0 && actor.Api.World.DuelOpponentId == null,
				"Duel recovery requires alive subjects with known life stats and no active duel.");
		if (Recovered(first) && Recovered(second)) return;
		await RestAsync(first, true); await RestAsync(second, true);
		while (!Recovered(first) || !Recovered(second))
		{
			await first.DelayAsync(TimeSpan.FromSeconds(1), token);
			await SyncAsync();
			Require(!first.Api.World.IsDead && !second.Api.World.IsDead, "A subject died during duel recovery.");
		}
		await RestAsync(first, false); await RestAsync(second, false);
		await SyncAsync();

		static bool Recovered(ISocialBasicsDriver actor) => actor.Api.World.CurrentHp == actor.Api.World.MaxHp && actor.Api.World.CurrentMp == actor.Api.World.MaxMp;
		async Task SyncAsync() { await first.SynchronizeAsync(token); await second.SynchronizeAsync(token); }
		async Task RestAsync(ISocialBasicsDriver actor, bool sitting)
		{
			await actor.SendAsync(actor.Api.Rest(sitting), token);
			await actor.WaitAsync(typeof(SM_EMOTION), packet => packet.Get<int>("senderObjectId") == actor.CharacterId &&
				packet.Get<byte>("emotionType") == (byte)(sitting ? EmotionType.SIT : EmotionType.STAND), token);
		}
	}

	/// <summary>The ordinary bounded duel flow shared by S1 and repeatable soak; callers own positioning and recovery.</summary>
	public static async Task RunDuelAsync(ISocialBasicsDriver first, ISocialBasicsDriver second, Race casterRace, CancellationToken ct)
	{
		await first.SendAsync(first.Api.Duel(second.CharacterId), ct);
		await second.WaitAsync(typeof(SM_QUESTION_WINDOW), packet => packet.Get<int>("code") == 50028, ct);
		await second.SendAsync(second.Api.Answer(1), ct);
		await first.WaitAsync(typeof(SM_DUEL), packet => packet.Get<byte>("type") == 0, ct);
		await second.WaitAsync(typeof(SM_DUEL), packet => packet.Get<byte>("type") == 0, ct);
		Require(first.Api.World.DuelOpponentId == second.CharacterId && second.Api.World.DuelOpponentId == first.CharacterId,
			"Duel did not establish reciprocal opponents.");
		Require(first.Api.World.Skills.TryGetValue(1282, out var flameBolt), "Duel mage must know Flame Bolt.");
		await first.SendAsync(first.Api.Target(second.CharacterId), ct);
		for (int cast = 0; cast < 60 && first.Api.World.DuelOpponentId != null; cast++)
		{
			await first.SendAsync(first.Api.Cast(new SpellCastData(1282, checked((byte)flameBolt!.Level), 0)
			{
				TargetObjectId = second.CharacterId, HitTime = DuelHitTime(first.CurrentPosition, second.CurrentPosition, casterRace),
			}), ct);
			var started = await first.WaitAsync(typeof(SM_CASTSPELL), packet => packet.Get<int>("objectId") == first.CharacterId && packet.Get<ushort>("spellId") == 1282, ct);
			await first.DelayAsync(TimeSpan.FromMilliseconds(started.Get<ushort>("castDuration") + 1), ct);
			var result = await first.WaitAsync(typeof(SM_CASTSPELL_RESULT), packet => packet.Get<int>("effectorId") == first.CharacterId && packet.Get<ushort>("skillId") == 1282, ct);
			await first.DelayAsync(TimeSpan.FromMilliseconds(Math.Max(2000, result.Get<ushort>("hitTime") + 1)), ct);
			await first.SynchronizeAsync(ct); await second.SynchronizeAsync(ct);
			Require(!first.Api.World.IsDead && !second.Api.World.IsDead, "A duel participant died rather than losing the duel.");
		}
		Require(first.Api.World.LastDuelResult is { ResultId: 2 } win && win.OpponentName == second.CharacterName &&
			second.Api.World.LastDuelResult is { ResultId: 0 } loss && loss.OpponentName == first.CharacterName,
			"Duel did not finish with reciprocal win/loss within the bounded combat window.");
		Require(first.Api.World.DuelOpponentId == null && second.Api.World.DuelOpponentId == null, "Duel opponent state was not cleared.");
	}

	private static Dictionary<int, long> Totals(ISocialBasicsDriver actor) => actor.Api.World.Inventory.Values
		.GroupBy(item => item.ItemId).ToDictionary(group => group.Key, group => group.Sum(item => item.Count));
	private static void Require(bool condition, string message)
	{
		if (!condition) throw new InvalidDataException(message);
	}
}
