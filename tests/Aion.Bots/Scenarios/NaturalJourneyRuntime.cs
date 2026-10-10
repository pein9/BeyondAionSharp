using Aion.Bots.Dashboard;
using Aion.Bots.Navigation;
using Aion.Bots.Scenarios.Classes;
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

	/// <summary>NA-23: diagnostic SIM setup before each Cleric encounter stage (single, pair, patrol).</summary>
	public Func<string, CancellationToken, Task>? PrepareEncounterStageAsync { get; init; }
	/// <summary>AB-07: the encounter's stages, in order; NA-23's single, pair and patrol when not given.</summary>
	public IReadOnlyList<string>? EncounterStages { get; init; }
	/// <summary>Diagnostic encounter targets, including neutral quest monsters; null selects ordinary aggressive targets.</summary>
	public IReadOnlyList<int>? EncounterNpcIds { get; init; }
	private readonly Lazy<BotMotionTiming> motions = new(() => BotMotionTiming.Load(
		Path.Combine(RepoRoot, "game-server/data/static_data/skills/motion_times.xml")));
	public long NowMillis => ElapsedMilliseconds();
	public bool IsAggressive(NpcTemplate? template) => NaturalHostility.IsAggressive(template, Data.TribeRelations, TribeClass.PC_DARK);
	public float AggroRadius(NpcTemplate? template) => NaturalHostility.AggroRadius(template, Data.TribeRelations, TribeClass.PC_DARK);
	public bool IsHostileSkill(int skillId) => Data.SkillDataDh.GetSkillTemplate(skillId)?.GetHostileType() is
		Aion.GameServer.SkillEngine.Model.HostileType.DIRECT or Aion.GameServer.SkillEngine.Model.HostileType.INDIRECT;
	/// <param name="spirit">NR-110e: the bot's own spirit, when a strike at it is to count as one at the bot.</param>
	public int? IncomingAttacker(DecodedBotServerPacket packet, int characterId, int? spirit = null) =>
		NaturalCombatRetreatPolicy.IncomingAttacker(packet, characterId, IsHostileSkill, spirit);

	public SpellCastData CreateSpellCast(BotWorldModel world, BotPosition origin, ushort skillId, byte level, int target)
	{
		var template = Data.SkillDataDh.GetSkillTemplate(skillId)
			?? throw new InvalidDataException($"Missing client skill template {skillId}.");
		BotWeaponMotionType weapon = WeaponMotion(world);
		BotPosition destination = target == world.SelfObjectId ? origin : world.Objects[target].Position;
		float distance = MathF.Sqrt(MathF.Pow(origin.X - destination.X, 2) + MathF.Pow(origin.Y - destination.Y, 2) +
			MathF.Pow(origin.Z - destination.Z, 2));
		int travel = template.GetAmmoSpeed() > 0 ? checked((int)Math.Ceiling(distance / template.GetAmmoSpeed() * 1000)) : 0;
		// The unboosted animation is conservative during speed buffs; the server still
		// validates it against Java Skill.updateHitTime and supplies the resulting delay.
		// NR-130a: from a mech the cast is timed by the mech's animations (Java MotionTime.getTimesFor 50-56,
		// MotionData.calculateAnimationTimeUntilFirstHit 59).
		int hitTime = motions.Value.CalculateClientHitTime(template,
			new BotMotionProfile(Race.ASMODIANS, Gender.MALE, weapon, Robot: world.RobotId != 0), travel);
		return new(skillId, level, 0) { TargetObjectId = target, HitTime = checked((ushort)hitTime) };
	}

	/// <summary>
	/// CP-48: how long after a completed cast the skill's animation reaches its last hit, with the weapon in hand. The
	/// server allows no next skill before it (Java Skill.endCast: nextSkillUse = now + lastHitMillis). The unboosted
	/// animation is taken, which is the longer one under an attack-speed buff. 0 for a skill with no motion row.
	/// </summary>
	public int AnimationLastHitMillis(BotWorldModel world, ushort skillId) =>
		Data.SkillDataDh.GetSkillTemplate(skillId) is { } template
			? motions.Value.CalculateAnimationTimesAfterLastHit(template,
				new BotMotionProfile(Race.ASMODIANS, Gender.MALE, WeaponMotion(world), Robot: world.RobotId != 0))?.LastHitMillis ?? 0
			: 0;

	private BotWeaponMotionType WeaponMotion(BotWorldModel world)
	{
		var equipped = world.Inventory.Values.SingleOrDefault(item => item.Details.EquippedSlot is 1 or 3);
		ItemGroup group = equipped == null ? ItemGroup.NONE : Data.ItemDataDh.GetItemTemplate(equipped.ItemId).GetItemGroup();
		// CP-38: any weapon casts. A second weapon in the off hand (slot 2) changes the animation set; a shield does not.
		var offHand = world.Inventory.Values.SingleOrDefault(item => item.Details.EquippedSlot == 2);
		ItemGroup? second = offHand != null && Data.ItemDataDh.GetItemTemplate(offHand.ItemId) is { } offTemplate && offTemplate.IsWeapon()
			? offTemplate.GetItemGroup() : null;
		return BotWeaponMotion.For(group, second);
	}
}

/// <summary>SIM diagnostic checkpoints are explicit; LIVE acceptance uses the complete default.</summary>
public enum NaturalMauCourse { GeneratorToRae, RaeToHatata }

public sealed record NaturalCombatDiagnosticResult(bool Killed, int Deaths, int Retreats, long ElapsedMillis);

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
	NaturalMauPolicyParameters? MauPolicy = null, bool AscensionBridge = false, bool ClericEncounter = false,
	bool AltgardLeg1 = false, string? AltgardLegId = null, int[]? AltgardOnlyQuests = null,
	string? CoinGearReceiptPath = null, string? HaramelProgressPath = null, string? CapitalStage = null,
	bool LaterCapital = false, string? AbyssArenaFirstTry = null, string? AbyssRingFirstTry = null,
	NaturalClassLine? ClassLine = null);
