using Aion.Bots.Dashboard;
using Aion.Bots.Navigation;
using Aion.Bots.Tracing;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Templates.Npc;
using Aion.Bots.Protocol;
using Aion.Bots.Timing;
using Aion.Bots.World;
using Aion.GameServer.Model.Templates.Items.Enums;

namespace Aion.Bots.Scenarios;

/// <summary>Static walkthrough knowledge and host services. The entry callback returns whether
/// this is a retained character; all later decisions use fresh client observations.</summary>
public sealed record NaturalJourneyRuntime(string RepoRoot, string Profile, int Seed, StaticData Data,
	Func<long> ElapsedMilliseconds, DateTimeOffset Epoch, Func<BotNavigationGeometry> CreateGeometry,
	Func<CancellationToken, Task<bool>> EnterAsync, Action AssertClean, Func<object> SnapshotProblems,
	BotActionTraceWriter Trace, LiveBotDashboardState Dashboard)
{
	/// <summary>SIM-only benchmark preparation. Runs after ordinary login, before policy decisions.</summary>
	public Func<CancellationToken, Task>? PrepareCourseAsync { get; init; }
	/// <summary>NA-21: supplies one approved help item (OD-13) to the natural character: ItemService in SIM, the
	/// director's //add on the isolated LIVE stack. Null when supply is off (NA_HELP_ITEMS=0) or not allowed
	/// (the operator's own world).</summary>
	public Func<int, long, CancellationToken, Task>? SupplyHelpItemAsync { get; init; }
	private readonly Lazy<BotMotionTiming> motions = new(() => BotMotionTiming.Load(
		Path.Combine(RepoRoot, "game-server/data/static_data/skills/motion_times.xml")));
	public long NowMillis => ElapsedMilliseconds();
	public bool IsAggressive(NpcTemplate? template) => NaturalHostility.IsAggressive(template, Data.TribeRelations, TribeClass.PC_DARK);
	public float AggroRadius(NpcTemplate? template) => NaturalHostility.AggroRadius(template, Data.TribeRelations, TribeClass.PC_DARK);

	public SpellCastData CreateSpellCast(BotWorldModel world, BotPosition origin, ushort skillId, byte level, int target)
	{
		var template = Data.SkillDataDh.GetSkillTemplate(skillId)
			?? throw new InvalidDataException($"Missing client skill template {skillId}.");
		var equipped = world.Inventory.Values.SingleOrDefault(item => item.Details.EquippedSlot is 1 or 3);
		ItemGroup group = equipped == null ? ItemGroup.NONE : Data.ItemDataDh.GetItemTemplate(equipped.ItemId).GetItemGroup();
		BotWeaponMotionType weapon = group switch
		{
			ItemGroup.MACE => BotWeaponMotionType.Mace,
			ItemGroup.STAFF => BotWeaponMotionType.Staff,
			ItemGroup.NONE => BotWeaponMotionType.NoWeapon,
			_ => throw new InvalidDataException($"Unexpected Priest weapon motion group {group}."),
		};
		BotPosition destination = target == world.SelfObjectId ? origin : world.Objects[target].Position;
		float distance = MathF.Sqrt(MathF.Pow(origin.X - destination.X, 2) + MathF.Pow(origin.Y - destination.Y, 2) +
			MathF.Pow(origin.Z - destination.Z, 2));
		int travel = template.GetAmmoSpeed() > 0 ? checked((int)Math.Ceiling(distance / template.GetAmmoSpeed() * 1000)) : 0;
		// The unboosted animation is conservative during speed buffs; the server still
		// validates it against Java Skill.updateHitTime and supplies the resulting delay.
		int hitTime = motions.Value.CalculateClientHitTime(template,
			new BotMotionProfile(Race.ASMODIANS, Gender.MALE, weapon), travel);
		return new(skillId, level, 0) { TargetObjectId = target, HitTime = checked((ushort)hitTime) };
	}
}

/// <summary>SIM diagnostic checkpoints are explicit; LIVE acceptance uses the complete default.</summary>
public enum NaturalMauCourse { GeneratorToRae, RaeToHatata }

/// <summary>Short Phase 1 SIM starts use the same navigation and combat paths as the area legs.</summary>
public enum NaturalMauEncounter
{
	IsolatedStalker,
	TwoAttackerPull,
	MovingPatrol,
	BlockedGeneratorRejoin,
	HatataAlone,
	HatataWithAdd,
}

public sealed record NaturalJourneyOptions(int? StopAfterQuest = null, string? RelogAt = null,
	string? StopAt = null, bool StopOnDeath = false, bool OptimizeHubs = false,
	NaturalMauCourse? Course = null, NaturalMauEncounter? Encounter = null,
	NaturalMauPolicyParameters? MauPolicy = null, bool AscensionBridge = false);
