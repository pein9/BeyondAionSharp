using Aion.Bots.World;
using Aion.GameServer.Model;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.Templates.Npc;

namespace Aion.Bots.Navigation.NavMesh;

/// <summary>A static-data place on a map: a service/quest NPC, bind point, portal, gather node or hostile spawn.
/// Hostile sites are aggressive spawns (danger, never hubs); <see cref="IsHub"/> marks friendly destinations.</summary>
public sealed record BotNavSite(int NpcId, string Name, BotTravelNodeKind Kind, BotPosition Position, int Level,
	float AggroRadius, bool Hostile, bool Attackable = false)
{
	public bool IsHub => !Hostile && !Attackable && Kind is BotTravelNodeKind.Npc or BotTravelNodeKind.BindPoint or BotTravelNodeKind.Portal;
}

/// <summary>Reads hub and danger sites for a map from the shipped spawn and NPC template data.
/// Hostility is the static tribe relation toward the player race (Java
/// <c>TribeRelationsData.isAggressiveRelation</c>); live play still trusts only observed attackers.</summary>
public static class BotNavSites
{
	public static Race RaceFor(BotNavWorld world, int mapId) => RaceFor(world.Data, mapId);

	public static Race RaceFor(StaticData data, int mapId)
	{
		string type = data.WorldMaps2.GetTemplate(mapId).GetWorldType().ToString();
		return type.Contains("ASMODAE", StringComparison.OrdinalIgnoreCase) ? Race.ASMODIANS : Race.ELYOS;
	}

	public static IReadOnlyList<BotNavSite> Load(BotNavWorld world, int mapId) => Load(world.Data, mapId);

	/// <summary>Sites from any loaded static data (the offline tools' or a running SIM server's).</summary>
	public static IReadOnlyList<BotNavSite> Load(StaticData data, int mapId)
	{
		Race race = RaceFor(data, mapId);
		TribeClass player = race == Race.ASMODIANS ? TribeClass.PC_DARK : TribeClass.PC;
		var sites = new List<BotNavSite>();
		foreach (var group in data.SpawnsDh.GetSpawnsByWorldId(mapId).OrderBy(g => g.GetNpcId()))
		{
			int npcId = group.GetNpcId();
			NpcTemplate? template = data.NpcDataDh.GetNpcTemplate(npcId);
			bool gather = data.GatherableDataDh.GetGatherableTemplate(npcId) != null;
			bool portal = data.Portal2DataDh.IsPortalNpc(npcId);
			bool bind = data.BindPointDataDh.GetBindPointTemplate(npcId) != null;
			foreach (var spot in group.GetSpawnTemplates().OrderBy(s => s.GetX()).ThenBy(s => s.GetY()))
			{
				var position = new BotPosition(spot.GetX(), spot.GetY(), spot.GetZ(), spot.GetHeading());
				if (gather)
				{
					sites.Add(new BotNavSite(npcId, "gather " + npcId, BotTravelNodeKind.Gather, position, 0, 0, false));
					continue;
				}
				if (template == null) continue;
				bool hostile = data.TribeRelations.IsAggressiveRelation(template.GetTribe(), player);
				bool friendly = data.TribeRelations.IsFriendlyRelation(template.GetTribe(), player) ||
					data.TribeRelations.IsSupportRelation(template.GetTribe(), player);
				bool attackable = hostile || data.TribeRelations.IsHostileRelation(template.GetTribe(), player) ||
					!friendly && template.GetNpcTemplateType() is NpcTemplateType.MONSTER or NpcTemplateType.RAID_MONSTER or NpcTemplateType.NONE;
				BotTravelNodeKind? kind = bind ? BotTravelNodeKind.BindPoint : portal ? BotTravelNodeKind.Portal
					: !attackable && template.GetNpcTemplateType() is NpcTemplateType.GENERAL or NpcTemplateType.ABYSS_GUARD or NpcTemplateType.GUARD
						? BotTravelNodeKind.Npc : null;
				if (hostile || attackable || kind != null)
					sites.Add(new BotNavSite(npcId, template.GetName(), kind ?? BotTravelNodeKind.Npc, position,
						template.GetLevel(), attackable ? Math.Max(template.GetAggroRange(), 1) : 0, hostile, attackable));
			}
		}
		return sites;
	}
}
