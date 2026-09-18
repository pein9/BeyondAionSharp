using Aion.Bots.Protocol;
using Aion.Bots.Protocol.Login;
using Aion.Bots.Reflexes;
using Aion.Bots.Scenarios;
using Aion.Bots.Timing;
using Aion.Bots.World;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.Bots.Api;

/// <summary>Intent-level bot API over the protocol writers, world model, reflexes and timing contract.</summary>
public sealed class BotApi
{
	private readonly BotMotionTiming? motionTiming;

	public BotApi(BotWorldModel? world = null, BotTimingContract? timing = null, BotReflexes? reflexes = null,
		BotMotionTiming? motionTiming = null, QuestDialogEchoDetector? questDialogEchoes = null)
	{
		World = world ?? new BotWorldModel();
		Timing = timing ?? new BotTimingContract();
		Reflexes = reflexes ?? new BotReflexes();
		QuestDialogEchoes = questDialogEchoes ?? new QuestDialogEchoDetector();
		this.motionTiming = motionTiming;
	}

	public BotWorldModel World { get; }
	public BotTimingContract Timing { get; }
	public BotReflexes Reflexes { get; }
	public QuestDialogEchoDetector QuestDialogEchoes { get; }

	public async Task<BotLoginSession> Login(Stream stream, string username, string password,
		CancellationToken cancellationToken = default)
	{
		var protocol = await LoginClientProtocol.ReadInitAsync(stream, cancellationToken);
		var result = await protocol.LoginAsync(stream, username, password, cancellationToken);
		return new BotLoginSession(protocol, result);
	}

	public BotClientPacket ListCharacters(int playOk2) => GameClientPackets.CharacterList(playOk2);

	public BotClientPacket CreateCharacter(CharacterCreationData character) => GameClientPackets.CreateCharacter(character);

	public BotClientPacket DeleteCharacter(int playOk2, int characterObjectId) =>
		GameClientPackets.DeleteCharacter(playOk2, characterObjectId);

	public BotClientPacket RestoreCharacter(int playOk2, int characterObjectId) =>
		GameClientPackets.RestoreCharacter(playOk2, characterObjectId);

	public BotClientPacket EnterWorld(int characterObjectId)
	{
		Timing.EnsureCanEnterWorld();
		return GameClientPackets.EnterWorld(characterObjectId);
	}

	public BotClientPacket ChangeChannel(int channel) => GameClientPackets.ChangeChannel(channel);

	public BotClientPacket Quit(bool stayConnected)
	{
		Timing.RecordLeftWorld(crashed: false);
		return GameClientPackets.Quit(stayConnected);
	}

	public BotConnectionCommand Crash()
	{
		Timing.RecordLeftWorld(crashed: true);
		return BotConnectionCommand.Crash;
	}

	public BotClientPacket MoveTo(MovementPacketData movement)
	{
		Timing.EnsureCanMove();
		return GameClientPackets.Move(movement);
	}

	public IReadOnlyList<BotClientPacket> Jump(MovementPacketData movement)
	{
		Timing.EnsureCanMove();
		return [GameClientPackets.Emotion((byte)EmotionType.JUMP), GameClientPackets.Move(movement)];
	}

	public BotClientPacket Fly() => GameClientPackets.Emotion((byte)EmotionType.FLY);

	public IReadOnlyList<BotClientPacket> Fly(int worldId, float x, float y, float z, byte heading, int distance) =>
		[Fly(), GameClientPackets.MoveInAir(worldId, x, y, z, heading, distance)];

	public BotClientPacket Land() => GameClientPackets.Emotion((byte)EmotionType.LAND);

	public IReadOnlyList<BotClientPacket> Glide(MovementPacketData movement)
	{
		Timing.EnsureCanMove();
		return [GameClientPackets.Emotion((byte)EmotionType.START_GLIDE), GameClientPackets.Move(movement)];
	}

	public BotClientPacket Rest(bool sitting) =>
		GameClientPackets.Emotion((byte)(sitting ? EmotionType.SIT : EmotionType.STAND));

	public BotClientPacket Walk(bool walking) =>
		GameClientPackets.Emotion((byte)(walking ? EmotionType.WALK : EmotionType.RUN));

	public BotClientPacket Emote(ushort emotionId, int targetObjectId = 0) =>
		GameClientPackets.Emotion((byte)EmotionType.EMOTE, emotionId, targetObjectId);

	public BotClientPacket Target(int targetObjectId, bool selectTargetOfTarget = false)
	{
		Timing.RecordTargetSelection(targetObjectId);
		return GameClientPackets.TargetSelect(targetObjectId, selectTargetOfTarget);
	}

	public BotClientPacket Attack(int targetObjectId, int attackSpeedMillis, byte attackNumber = 0,
		ushort clientTime = 0, byte type = 0)
	{
		if (Timing.SelectedTargetId != targetObjectId)
			throw new InvalidOperationException($"Target({targetObjectId}) must precede Attack({targetObjectId}).");
		Timing.RecordAttack(attackSpeedMillis);
		return GameClientPackets.Attack(targetObjectId, attackNumber, clientTime, type);
	}

	public BotClientPacket Cast(SpellCastData cast, SkillTemplate? skill = null, BotMotionProfile? profile = null,
		int ammoTravelMillis = 0)
	{
		if (skill != null || profile != null)
		{
			if (skill == null || profile == null || motionTiming == null)
				throw new InvalidOperationException("Skill, motion profile and BotMotionTiming are all required to calculate client hit time.");
			var hitTime = motionTiming.CalculateClientHitTime(skill, profile, ammoTravelMillis);
			cast = cast with { HitTime = checked((ushort)hitTime) };
		}
		int? target = cast.TargetType is 0 or 3 or 4 ? cast.TargetObjectId : null;
		Timing.RecordCastStarted(cast.SkillId, target);
		return GameClientPackets.CastSpell(cast);
	}

	public BotClientPacket SummonCommand(byte mode, int targetObjectId, int unknown1 = 0, int unknown2 = 0) =>
		GameClientPackets.SummonCommand(mode, targetObjectId, unknown1, unknown2);

	public BotClientPacket SummonAttack(int summonObjectId, int targetObjectId, byte unknown1 = 0,
		ushort clientTime = 0, byte unknown3 = 0) =>
		GameClientPackets.SummonAttack(summonObjectId, targetObjectId, unknown1, clientTime, unknown3);

	public BotClientPacket SummonCast(int summonObjectId, ushort skillId, byte skillLevel, int targetObjectId) =>
		GameClientPackets.SummonCastSpell(summonObjectId, skillId, skillLevel, targetObjectId);

	public BotClientPacket UseItem(int itemObjectId, ItemTemplate template, byte type = 0, int argument = 0)
	{
		Timing.RecordItemUse(template);
		return GameClientPackets.UseItem(itemObjectId, type, argument);
	}

	public BotClientPacket Equip(byte action, long slot, int itemObjectId) =>
		GameClientPackets.EquipItem(action, slot, itemObjectId);

	public BotClientPacket Loot(int targetObjectId, byte? itemIndex = null, bool close = false)
	{
		if (close && itemIndex != null)
			throw new ArgumentException("Closing a loot window cannot also select an item.", nameof(itemIndex));
		return itemIndex == null
			? GameClientPackets.StartLoot(targetObjectId, close ? (byte)1 : (byte)0)
			: GameClientPackets.LootItem(targetObjectId, itemIndex.Value);
	}

	public BotClientPacket TalkTo(int targetObjectId) => GameClientPackets.ShowDialog(targetObjectId);

	public BotClientPacket SelectDialog(int targetObjectId, ushort actionId, ushort rewardIndex = 0,
		ushort lastPage = 0, int questId = 0)
	{
		QuestDialogEchoes.Record(targetObjectId, actionId, questId);
		return GameClientPackets.DialogSelect(targetObjectId, actionId, rewardIndex, lastPage, questId);
	}

	public BotClientPacket SelectDialogExpectRejection(int targetObjectId, ushort actionId, ushort rewardIndex = 0,
		ushort lastPage = 0, int questId = 0)
	{
		QuestDialogEchoes.Record(targetObjectId, actionId, questId, expectEcho: true);
		return GameClientPackets.DialogSelect(targetObjectId, actionId, rewardIndex, lastPage, questId);
	}

	public BotClientPacket CloseDialog(int targetObjectId) => GameClientPackets.CloseDialog(targetObjectId);
	public BotClientPacket DeleteQuest(int questId) => GameClientPackets.DeleteQuest(questId);

	public BotClientPacket Answer(byte response)
	{
		var question = World.Question ?? throw new InvalidOperationException("There is no open question window to answer.");
		return GameClientPackets.QuestionResponse(question.Code, response, question.SenderId);
	}

	public BotClientPacket Teleport(int targetObjectId, int locationId) =>
		GameClientPackets.TeleportSelect(targetObjectId, locationId);

	public IReadOnlyList<BotClientPacket> Gather(int targetObjectId, bool start = true)
	{
		if (!start)
		{
			Timing.SetActivity(BotBlockingActivity.Gathering, false);
			return [GameClientPackets.Gather(-1)];
		}
		Timing.RecordTargetSelection(targetObjectId);
		Timing.SetActivity(BotBlockingActivity.Gathering, true);
		return [GameClientPackets.TargetSelect(targetObjectId), GameClientPackets.Gather(0)];
	}

	public BotClientPacket Craft(int targetTemplateId, int recipeId, int targetObjectId,
		IReadOnlyList<(int ItemId, long Count)> materials, byte craftType = 1, byte unknown = 0)
	{
		Timing.SetActivity(BotBlockingActivity.Crafting, true);
		return GameClientPackets.Craft(unknown, targetTemplateId, recipeId, targetObjectId, craftType, materials);
	}

	public BotClientPacket Buy(int sellerObjectId, IReadOnlyList<(int ItemId, long Count)> items) =>
		GameClientPackets.BuyItem(sellerObjectId, 13, items);

	public BotClientPacket Sell(int sellerObjectId, IReadOnlyList<(int ItemId, long Count)> items) =>
		GameClientPackets.BuyItem(sellerObjectId, 1, items);

	public BotClientPacket TradeRequest(int targetObjectId) => GameClientPackets.ExchangeRequest(targetObjectId);
	public BotClientPacket TradeAddItem(int itemObjectId, int count) => GameClientPackets.ExchangeAddItem(itemObjectId, count);
	public BotClientPacket TradeAddKinah(long count) => GameClientPackets.ExchangeAddKinah(count);
	public BotClientPacket TradeLock() => GameClientPackets.ExchangeLock();
	public BotClientPacket TradeAccept() => GameClientPackets.ExchangeOk();
	public BotClientPacket TradeCancel() => GameClientPackets.ExchangeCancel();

	public BotClientPacket InviteToGroup(string playerName, byte inviteType = 0) =>
		GameClientPackets.InviteToGroup(inviteType, playerName);

	public BotClientPacket Say(string message, byte chatType = 0) => GameClientPackets.ChatMessagePublic(chatType, message);

	public BotClientPacket Whisper(string playerName, string message) =>
		GameClientPackets.ChatMessageWhisper(playerName, message);

	public BotClientPacket Duel(int targetObjectId) => GameClientPackets.DuelRequest(targetObjectId);

	public BotClientPacket Revive(BotReviveType type = BotReviveType.Bind) => GameClientPackets.Revive((byte)type);

	public BotClientPacket? Observe(DecodedBotServerPacket packet, int animationLastHitMillis = 0)
	{
		World.Apply(packet);
		QuestDialogEchoes.Observe(packet);
		if (packet.PacketType == typeof(SM_SKILL_COOLDOWN))
			Timing.ApplySkillCooldowns(packet);
		else if (packet.PacketType == typeof(SM_CASTSPELL_RESULT) &&
			(World.SelfObjectId == null || packet.Get<int>("effectorId") == World.SelfObjectId))
			Timing.RecordCastResult(animationLastHitMillis);
		else if (packet.PacketType == typeof(SM_SKILL_CANCEL) &&
			(World.SelfObjectId == null || packet.Get<int>("objectId") == World.SelfObjectId))
			Timing.RecordCastCancelled();
		return Reflexes.RespondTo(packet);
	}
}

public sealed record BotLoginSession(LoginClientProtocol Protocol, LoginHandshakeResult Result);

public enum BotConnectionCommand
{
	Crash,
}
