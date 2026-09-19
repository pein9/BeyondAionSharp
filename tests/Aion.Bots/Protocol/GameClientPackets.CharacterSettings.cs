using Aion.GameServer.Network.Aion.ClientPackets;

namespace Aion.Bots.Protocol;

public static partial class GameClientPackets
{
	// Passkeys are opaque fixed-width wire fields, not null-terminated strings. CM_CHARACTER_PASSKEY
	// consumes exactly 48 bytes per value; retain padding rather than silently changing the stored value.
	public static BotClientPacket SetCharacterPasskey(ReadOnlyMemory<byte> value) => CharacterPasskey(0, value);
	public static BotClientPacket UpdateCharacterPasskey(ReadOnlyMemory<byte> current, ReadOnlyMemory<byte> replacement) =>
		CharacterPasskey(2, current, replacement);
	public static BotClientPacket SubmitCharacterPasskey(ReadOnlyMemory<byte> value) => CharacterPasskey(3, value);
	private static BotClientPacket CharacterPasskey(short operation, ReadOnlyMemory<byte> value, ReadOnlyMemory<byte> replacement = default)
	{
		if (value.Length != 48) throw new ArgumentException("A character passkey wire value must contain exactly 48 bytes.", nameof(value));
		if (operation == 2 && replacement.Length != 48) throw new ArgumentException("A replacement passkey wire value must contain exactly 48 bytes.", nameof(replacement));
		return Create<CM_CHARACTER_PASSKEY>(w => { w.H(operation); w.B(value.Span); if (operation == 2) w.B(replacement.Span); });
	}

	public static BotClientPacket SetDisplayTitle(ushort titleId) => Create<CM_TITLE_SET>(w => w.UH(titleId));
	public static BotClientPacket SetBonusTitle(ushort titleId) => Create<CM_BONUS_TITLE>(w => w.UH(titleId));
	public static BotClientPacket CreateMacro(byte position, string xml) => Create<CM_MACRO_CREATE>(w => { w.C(position); w.S(xml); });
	public static BotClientPacket DeleteMacro(byte position) => Create<CM_MACRO_DELETE>(w => w.C(position));
	public static BotClientPacket EditCharacter(int objectId, CharacterCreationData data) => Create<CM_CHARACTER_EDIT>(w =>
	{
		w.D(objectId); WriteCharacterAppearance(w, data);
	});
}
