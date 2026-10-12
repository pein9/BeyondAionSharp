using System.Buffers.Binary;
using Aion.Bots.Protocol;

namespace Aion.Bots.Api;

public sealed partial class BotApi
{
	private HashSet<ushort>? healingSkills;
	private Action<string>? healingViolation;
	public bool HealingOnly => healingSkills != null;

	public IDisposable BeginHealingOnly(IEnumerable<ushort> supportSkills, Action<string>? onViolation = null)
	{
		if (HealingOnly) throw new InvalidOperationException("Healing-only mode is already active.");
		healingSkills = supportSkills.ToHashSet();
		if (healingSkills.Count == 0) { healingSkills = null; throw new InvalidOperationException("This profile has no support skills."); }
		healingViolation = onViolation;
		return new HealingScope(this);
	}

	/// <summary>Called by both session send boundaries, including packets built directly and reflex responses.</summary>
	public void ValidateOutgoing(BotClientPacket packet)
	{
		if (healingSkills == null) return;
		bool Friend(int id) => id == World.SelfObjectId || World.GroupMembers.TryGetValue(id, out var member) && member.Online;
		ReadOnlySpan<byte> body = packet.Body;
		bool allowed = packet.PacketType.Name switch
		{
			"CM_ATTACK" or "CM_SUMMON_ATTACK" or "CM_SUMMON_CASTSPELL" or "CM_SUMMON_COMMAND" or "CM_USE_CHARGE_SKILL" => false,
			// Quest and service dialogues select NPCs too; selection grants no permission to attack or cast at them.
			"CM_CASTSPELL" => body.Length >= 8 && body[3] is 0 or 3 or 4 &&
				(BinaryPrimitives.ReadUInt16LittleEndian(body) == 0 ||
				 healingSkills.Contains(BinaryPrimitives.ReadUInt16LittleEndian(body)) && Friend(BinaryPrimitives.ReadInt32LittleEndian(body[4..]))),
			_ => true,
		};
		if (!allowed)
		{
			healingViolation?.Invoke(packet.PacketType.Name);
			throw new InvalidOperationException($"Healing-only mode refused {packet.PacketType.Name}: support skills on self or group members only.");
		}
	}

	private sealed class HealingScope(BotApi api) : IDisposable
	{
		public void Dispose() { api.healingSkills = null; api.healingViolation = null; }
	}
}
