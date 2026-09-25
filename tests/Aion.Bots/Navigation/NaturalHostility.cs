using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Templates.Npc;

namespace Aion.Bots.Navigation;

/// <summary>
/// Which NPCs will attack the bot on sight. Mirrors the server's own aggro test (Java
/// <c>CreatureEventHandler.checkAggro</c>: the NPC has an aggro range, its tribe is aggressive to the
/// player's tribe, and they are not friends) instead of the template's <c>type</c> attribute. Some
/// aggressive monsters carry no type at all (the Gray Mane Stalkers 210750 and 211284, which the Java
/// data leaves untyped and the server treats as NONE), and a type-based test made them invisible to every
/// positioning decision: 84 of 84 recorded bot deaths in the Q2007 lycan camp had one in the final fight.
/// </summary>
public static class NaturalHostility
{
	/// <summary>The tribe of the bot's own character: PC (Elyos) or PC_DARK (Asmodian).</summary>
	public static TribeClass PlayerTribe { get; set; } = TribeClass.PC_DARK;

	public static TribeClass TribeOf(Race race) => race == Race.ELYOS ? TribeClass.PC : TribeClass.PC_DARK;

	/// <summary>True when this NPC would aggro the player on sight.</summary>
	public static bool IsAggressive(NpcTemplate? template, TribeRelationsData? relations = null, TribeClass? player = null)
	{
		if (template == null || template.GetAggroRange() <= 0) return false;
		relations ??= DataManager.TRIBE_RELATIONS_DATA;
		TribeClass tribe = template.GetTribe(), me = player ?? PlayerTribe;
		return relations.IsAggressiveRelation(tribe, me) && !relations.IsFriendlyRelation(tribe, me);
	}

	/// <summary>The NPC's aggro range, or 0 when it would not aggro the player.</summary>
	public static float AggroRadius(NpcTemplate? template, TribeRelationsData? relations = null, TribeClass? player = null) =>
		IsAggressive(template, relations, player) ? template!.GetAggroRange() : 0f;
}
