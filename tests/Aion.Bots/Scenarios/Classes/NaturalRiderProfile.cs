using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;

namespace Aion.Bots.Scenarios.Classes;

/// <summary>
/// NR-130: the Rider, the Engineer's other choice at Ascension, which lays the pistol down for a cipher-blade and fights
/// from the mech that blade calls (docs/natural-all-classes-ntc.md). The catalog is generated from the shipped data to
/// level 26, where the accepted line's Abyss-entry leg ends. Embark is kept on as a toggle. Every other skill of its
/// own needs the mech (useconditions/ride_robot), and the table does not know yet whether the bot rides: those are left
/// out with that reason until NR-130a, which writes the table.
/// </summary>
public static class NaturalRiderProfile
{
	private const int TopLevel = 26;

	/// <summary>Embark, in its four ranks: the toggle that puts the Rider in its mech. It needs a cipher-blade in hand,
	/// costs 104 MP and more once, and gives 4 m more reach, more defence and parry while it is on.</summary>
	private static readonly IReadOnlyDictionary<int, string> Roles = new Dictionary<int, string>
	{
		[2767] = "embark", [2768] = "embark", [2769] = "embark", [2770] = "embark",
		// NR-50a: the two powder skills, cast only in a rest.
		[246] = "herb", [247] = "herb", [251] = "herb", [253] = "herb",
		[249] = "mp-recovery", [250] = "mp-recovery", [252] = "mp-recovery", [254] = "mp-recovery",
	};

	private const string Mech = "needs the mech (useconditions/ride_robot; Java RideRobotCondition), and the table does not know yet whether " +
		"the bot rides (NR-130a).";
	private const string Pistol = "needs a pistol in hand (startconditions/weapon), and the Rider holds a cipher-blade.";
	private const string Charge = "is a charge skill, and the fight loop holds no charge.";
	private const string Area = "hits up to eight monsters around the mech. The bot pulls one at a time, and an area skill wakes every other " +
		"one in reach.";
	private const string Enmity = "doubles the enmity of what the Rider does; alone, the monster has no one else to turn to.";

	/// <summary>Every other active skill and toggle a Rider learns by itself to level 26, and why it is not cast.</summary>
	private static readonly IReadOnlyDictionary<int, string> Excluded = new Dictionary<int, string>
	{
		[2690] = "Bludgeon I " + Mech, [2691] = "Bludgeon II " + Mech, [2692] = "Bludgeon III " + Mech, [2693] = "Bludgeon IV " + Mech,
		[2553] = "Battery I " + Mech, [2554] = "Battery II " + Mech, [2555] = "Battery III " + Mech, [2556] = "Battery IV " + Mech,
		[2807] = "Cinder Cannon I " + Mech, [2808] = "Cinder Cannon II " + Mech, [2809] = "Cinder Cannon III " + Mech,
		[2810] = "Cinder Cannon IV " + Mech,
		[4647] = "Provoking Whispers I " + Mech, [4648] = "Provoking Whispers II " + Mech, [4649] = "Provoking Whispers III " + Mech,
		[4650] = "Provoking Whispers IV " + Mech,
		[2543] = "Rocket Punch I " + Mech, [2544] = "Rocket Punch II " + Mech, [2545] = "Rocket Punch III " + Mech,
		[2726] = "Electric Shock I " + Mech, [2727] = "Electric Shock II " + Mech,
		[2717] = "Lightning Tether " + Mech,
		[2779] = "Sundering Blade " + Mech,
		[2794] = "Overdrive Trigger I " + Mech, [2795] = "Overdrive Trigger II " + Mech, [2796] = "Overdrive Trigger III " + Mech,
		[2797] = "Overdrive Trigger IV " + Mech,
		[2529] = "Nullification Trigger " + Mech,
		[2440] = "Kinetic Battery I, a toggle, " + Mech, [2441] = "Kinetic Battery II, a toggle, " + Mech,
		[2442] = "Kinetic Battery III, a toggle, " + Mech,
		[2606] = "Kinetic Slam I " + Charge, [2609] = "Kinetic Slam II " + Charge, [2612] = "Kinetic Slam III " + Charge,
		[2615] = "Kinetic Slam IV " + Charge,
		[2426] = "Chilling Wave I " + Area, [2427] = "Chilling Wave II " + Area, [2428] = "Chilling Wave III " + Area,
		[2838] = "Mounting Frustration I, a toggle, " + Enmity, [2839] = "Mounting Frustration II, a toggle, " + Enmity,
		[2840] = "Mounting Frustration III, a toggle, " + Enmity,
		[2400] = "Boost throws the mech 15 m forward, to a place only the cast's result names; the bot walks checked routes only.",
		[2741] = "Steam Rush dashes to its target, to a place only the cast's result names; the bot walks checked routes only.",
		[2520] = "Fuel Reserves gives back flight time for 250 MP; the journey's flights are short.",
		[2219] = "Direct Shot I " + Pistol, [2220] = "Direct Shot II " + Pistol, [2221] = "Direct Shot III " + Pistol,
		[2222] = "Direct Shot IV " + Pistol, [2223] = "Direct Shot V " + Pistol, [2224] = "Direct Shot VI " + Pistol,
		[1957] = "Gunshot I " + Pistol, [1958] = "Gunshot II " + Pistol, [1959] = "Gunshot III " + Pistol, [1960] = "Gunshot IV " + Pistol,
		[1961] = "Gunshot V " + Pistol,
		[2142] = "Rapidfire I " + Pistol, [2143] = "Rapidfire II " + Pistol, [2144] = "Rapidfire III " + Pistol, [2145] = "Rapidfire IV " + Pistol,
		[2146] = "Rapidfire V " + Pistol,
		[1942] = "Hot Shot I " + Pistol, [1943] = "Hot Shot II " + Pistol, [1944] = "Hot Shot III " + Pistol, [1945] = "Hot Shot IV " + Pistol,
		[2155] = "Green Grenade I " + Pistol, [2156] = "Green Grenade II " + Pistol, [2157] = "Green Grenade III " + Pistol,
		[2158] = "Green Grenade IV " + Pistol,
		[2168] = "Bullet Resistance I " + Pistol, [2169] = "Bullet Resistance II " + Pistol, [2170] = "Bullet Resistance III " + Pistol,
		[2702] = "Bullet Resistance IV " + Pistol,
	};

	/// <summary>
	/// NR-130: the table before NR-130a. It names no skill: every attack of the Rider needs the mech. The cipher-blade
	/// swings whenever the target is in its reach; the ladder is the shield scroll at 50% HP and the life potion at or
	/// below 75%; it leaves at three attackers, as the two plate classes do, or at 25% HP with nothing ready.
	/// </summary>
	private static readonly NaturalRotationRules Rules = new("natural-rider-v1",
		Adjacent: [], AtRange: [], Upkeep: [],
		Recovery: [new(NaturalRecoveryKind.ShieldScroll, 50), new(NaturalRecoveryKind.LifePotion, 75)],
		SwarmAttackers: 3, FleeHpPercent: 25, AutoAttack: NaturalAutoAttack.Filler);

	/// <summary>NR-70a: the one toggle kept on, the mech. The server lets a Rider keep two of its kind.</summary>
	private static readonly NaturalKeptToggle[] KeptToggles = [new("embark", "toggle-embark")];

	// NR-Q5, NR-Q7 and NR-Q13: a cipher-blade, chain first, and the kit of a class that does not cast from mana and rests
	// with the powder.
	private static readonly NaturalGearRules Gear = NaturalClassGearTable.Rider.Rules(NaturalClassLineContract.LoadDefault(),
		NaturalHelpItemAllowlist.Kit(caster: false, reagent: true));

	public static NaturalClassProfile Create(StaticData data)
	{
		ArgumentNullException.ThrowIfNull(data);
		var excluded = NaturalSkillCatalog.CommonExcluded.Concat(Excluded).ToDictionary(entry => entry.Key, entry => entry.Value);
		NaturalPriestSkill[] skills = NaturalSkillCatalog.Build(data, PlayerClass.RIDER, Roles, excluded);
		NaturalProfileValidator.Require(data, PlayerClass.RIDER, TopLevel, skills, excluded, Rules.Lines(skills), Gear, KeptToggles);
		return new NaturalClassProfile
		{
			Class = PlayerClass.RIDER,
			Skills = skills,
			Excluded = excluded,
			// It walks in: the mech's own skills reach 6 m.
			Combat = new NaturalRotationCombatPolicy(Rules, skills, NaturalTemplarProfile.Movement),
			// NR-34, NR-Q13: no mana serum and no Awakening scroll; the powder; Courage in the shared scroll slot, as the
			// Engineer keeps it.
			HelpItems = NaturalHelpItemRules.ForKinds(caster: false, reagent: true, sharedSlotFamily: "courage"),
			Upkeep = [],
			Toggles = KeptToggles,
			// NR-36: nothing to shoot with in flight until NR-130a gives Cinder Cannon its role.
			AirAttackRoles = [],
			// NR-37: every second class holds for a patrol and assesses the fight, by its own table.
			PatrolRule = NaturalPatrolRule.HoldAndAssess,
			Patrol = NaturalPatrolView.From(Rules, skills, PlayerClass.RIDER),
			RangedHold = NaturalRangedHold.Never,
			// NR-50a: the powder first, then the life potion below 90% HP, then sitting.
			Rest = new NaturalRestRules(skills, HealBelowPercent: 90, ManaSitBelowPercent: 25, ManaSitUntilPercent: 50, MaximumQuietSits: 12,
				PotionPlan: new NaturalPotionRestPlan(HpTargetPercent: 90, UsesMana: true), RestSkills: NaturalRestSkills.ReagentOnly(90)),
			// The distances and the movement of a class that walks in, the Templar's.
			Ranges = NaturalTemplarProfile.Ranges,
			Readiness = NaturalWarriorProfile.Readiness,
			Movement = NaturalTemplarProfile.Movement,
			Campaign = NaturalPriestProfile.PriestLineCampaign,
			Gear = Gear,
			Restock = NaturalWarriorProfile.Restock,
		};
	}
}
