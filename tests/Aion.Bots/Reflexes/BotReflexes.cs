using Aion.Bots.Protocol;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.Bots.Reflexes;

public delegate BotReviveType? BotRevivePolicy(BotDeathPrompt prompt);

public delegate byte? BotQuestionPolicy(BotQuestionPrompt prompt);

/// <summary>Immediate protocol reactions that an honest game client sends for specific server packets.</summary>
public sealed class BotReflexes
{
	private readonly BotRevivePolicy revivePolicy;
	private readonly BotQuestionPolicy questionPolicy;

	public BotReflexes(BotRevivePolicy? revivePolicy = null, BotQuestionPolicy? questionPolicy = null)
	{
		this.revivePolicy = revivePolicy ?? (_ => null);
		this.questionPolicy = questionPolicy ?? (_ => null);
	}

	public BotClientPacket? RespondTo(DecodedBotServerPacket packet)
	{
		if (packet.PacketType == typeof(SM_PLAYER_SPAWN))
			return GameClientPackets.LevelReady();
		if (packet.PacketType == typeof(SM_TELEPORT_LOC))
			return GameClientPackets.TeleportAnimationDone();
		if (packet.PacketType == typeof(SM_PLAY_MOVIE))
		{
			return GameClientPackets.PlayMovieEnd(packet.Get<bool>("isMovie") ? (byte)1 : (byte)0,
				packet.Get<int>("objectId"), packet.Get<int>("questId"), packet.Get<int>("cutsceneId"),
				packet.Get<bool>("canSkip"));
		}
		if (packet.PacketType == typeof(SM_DIE))
		{
			var prompt = new BotDeathPrompt(packet.Get<bool>("allowReviveBySkill"),
				packet.Get<bool>("allowReviveByItem"), packet.Get<int>("remainingKiskTimeSeconds"),
				packet.Get<bool>("allowInstanceRevive"), packet.Get<bool>("invasion"));
			var revive = revivePolicy(prompt);
			return revive == null ? null : GameClientPackets.Revive((byte)revive.Value);
		}
		if (packet.PacketType == typeof(SM_QUESTION_WINDOW))
		{
			var prompt = new BotQuestionPrompt(packet.Get<int>("code"), packet.Get<string[]>("params"),
				packet.Get<int>("senderId"), packet.Get<int>("rangeOrCooldownSeconds"));
			var answer = questionPolicy(prompt);
			return answer == null ? null : GameClientPackets.QuestionResponse(prompt.Code, answer.Value, prompt.SenderId);
		}
		return null;
	}
}

public enum BotReviveType : byte
{
	Bind = 0,
	Rebirth = 1,
	SelfReviveItem = 2,
	Skill = 3,
	Kisk = 4,
	Instance = 6,
	Obelisk = 8,
}

public readonly record struct BotDeathPrompt(bool AllowBySkill, bool AllowByItem, int RemainingKiskTimeSeconds,
	bool AllowInstance, bool Invasion);

public sealed record BotQuestionPrompt(int Code, IReadOnlyList<string> Parameters, int SenderId,
	int RangeOrCooldownSeconds);
