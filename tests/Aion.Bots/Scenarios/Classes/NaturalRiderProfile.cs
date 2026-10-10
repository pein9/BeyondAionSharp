using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;

namespace Aion.Bots.Scenarios.Classes;

/// <summary>
/// NR-130: the Rider, the Engineer's other choice at Ascension, which lays the pistol down for a cipher-blade and fights
/// from the mech that blade calls (docs/natural-all-classes-ntc.md). The catalog is generated from the shipped data to
/// level 26, where the accepted line's Abyss-entry leg ends. Embark is kept on as a toggle. NR-130a: every other skill
/// of its own needs the mech (useconditions/ride_robot), and the table casts one only while the server says the bot
/// rides.
/// </summary>
public static class NaturalRiderProfile
{
	private const int TopLevel = 26;

	private static readonly IReadOnlyDictionary<int, string> Roles = new Dictionary<int, string>
	{
		// Embark, in its four ranks: the toggle that puts the Rider in its mech. It needs a cipher-blade in hand, costs
		// 104 MP and more once, and gives 4 m more reach, more defence and parry while it is on.
		[2767] = "embark", [2768] = "embark", [2769] = "embark", [2770] = "embark",
		// The mech's fists: Bludgeon opens a chain and Battery follows inside 3.5 s. Neither costs mana.
		[2690] = "bludgeon", [2691] = "bludgeon", [2692] = "bludgeon", [2693] = "bludgeon",
		[2553] = "battery", [2554] = "battery", [2555] = "battery", [2556] = "battery",
		// Cinder Cannon reaches 20 m: the pull, and the strongest attack it has before Rocket Punch.
		[2807] = "cinder", [2808] = "cinder", [2809] = "cinder", [2810] = "cinder",
		// Provoking Whispers is an attack that also raises enmity, which changes nothing for a Rider alone.
		[4647] = "whispers", [4648] = "whispers", [4649] = "whispers", [4650] = "whispers",
		[2543] = "rocket", [2544] = "rocket", [2545] = "rocket",
		// Electric Shock holds its target for 1 s from 8 m, and Lightning Tether follows it and roots the target for 5.5 s.
		[2726] = "shock", [2727] = "shock",
		[2717] = "tether",
		// Sundering Blade lowers the target's defence against everything the mech does for 14 s.
		[2779] = "sundering",
		// Overdrive Trigger: 2,000 DP for a minute of more magic boost and speed.
		[2794] = "overdrive", [2795] = "overdrive", [2796] = "overdrive", [2797] = "overdrive",
		// Nullification Trigger: 8 s in which every hit is parried or resisted, for 2% of its mana, every 30 s.
		[2529] = "nullify",
		// NR-50a: the two powder skills, cast only in a rest.
		[246] = "herb", [247] = "herb", [251] = "herb", [253] = "herb",
		[249] = "mp-recovery", [250] = "mp-recovery", [252] = "mp-recovery", [254] = "mp-recovery",
	};

	private const string Pistol = "needs a pistol in hand (startconditions/weapon), and the Rider holds a cipher-blade.";
	private const string Charge = "is a charge skill, and the fight loop holds no charge.";
	private const string Area = "hits up to eight monsters around the mech. The bot pulls one at a time, and an area skill wakes every other " +
		"one in reach.";
	private const string Enmity = "doubles the enmity of what the Rider does; alone, the monster has no one else to turn to.";
	private const string Shield = "takes three tenths of every hit and pays half of that with mana, costs mana every 6 s besides (64 MP in its " +
		"third rank) and ends by itself after 90 s. The mana is what its attacks are paid with (NR-131 looks at it again).";

	/// <summary>Every other active skill and toggle a Rider learns by itself to level 26, and why it is not cast.</summary>
	private static readonly IReadOnlyDictionary<int, string> Excluded = new Dictionary<int, string>
	{
		[2440] = "Kinetic Battery I, a toggle, " + Shield, [2441] = "Kinetic Battery II, a toggle, " + Shield,
		[2442] = "Kinetic Battery III, a toggle, " + Shield,
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
	/// NR-130a: every attack needs the mech, which the buff check boards; without it the table swings the cipher-blade.
	/// <para>
	/// From range: Cinder Cannon (20 m, every 16 s), and then it holds where it stands while the monster comes. Electric
	/// Shock from 8 m, and from 6 m the attacks of the mech's arms. Lightning Tether is not cast from range: it would root
	/// the monster outside the mech's reach.
	/// </para>
	/// <para>
	/// On the target, an open follow-up first: Battery after Bludgeon, Lightning Tether after Electric Shock, each inside
	/// 3.5 s. Of the openers, Sundering Blade goes first (it lowers the target's defence for 14 s), then Rocket Punch, the
	/// strongest, then Bludgeon, which costs nothing and is ready every 5 s, then Electric Shock, Cinder Cannon and
	/// Provoking Whispers. The cipher-blade swings whenever no skill is ready. Overdrive Trigger is cast in a fight
	/// whenever its 2,000 DP are there and its minute is over.
	/// </para>
	/// <para>
	/// The ladder: the shield scroll at 50% HP, Nullification Trigger at or below 60%, the life potion at or below 75%.
	/// It leaves at three attackers, as the two plate classes do, or at 25% HP with nothing ready.
	/// </para>
	/// </summary>
	private static readonly NaturalRotationRules Rules = new("natural-rider-v1",
		Adjacent: ["sundering", "rocket", "bludgeon", "battery", "shock", "tether", "cinder", "whispers"],
		AtRange: ["cinder", "shock", "sundering", "rocket", "bludgeon", "battery", "whispers"],
		Upkeep: [new("overdrive", DuringFight: true)],
		Recovery:
		[
			new(NaturalRecoveryKind.ShieldScroll, 50),
			new(NaturalRecoveryKind.Skill, 60, "nullify"),
			new(NaturalRecoveryKind.LifePotion, 75),
		],
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
			// It pulls with Cinder Cannon and holds, as the Templar pulls with its Taunt, and walks to a target that does
			// not come.
			Combat = new NaturalRotationCombatPolicy(Rules, skills, NaturalTemplarProfile.Movement),
			// NR-34, NR-Q13: no mana serum and no Awakening scroll; the powder; Courage in the shared scroll slot, as the
			// Engineer keeps it.
			HelpItems = NaturalHelpItemRules.ForKinds(caster: false, reagent: true, sharedSlotFamily: "courage"),
			Upkeep = [],
			Toggles = KeptToggles,
			// NR-36: in flight it swings the cipher-blade, as the Templar swings its sword. Cinder Cannon is ready every
			// 16 s, which is too seldom for a kill inside the flight time the air fight counts with.
			AirAttackRoles = [],
			// NR-37: every second class holds for a patrol and assesses the fight, by its own table.
			PatrolRule = NaturalPatrolRule.HoldAndAssess,
			Patrol = NaturalPatrolView.From(Rules, skills, PlayerClass.RIDER),
			RangedHold = NaturalRangedHold.Never,
			// NR-50a: the powder first, then the life potion below 90% HP, then sitting.
			Rest = new NaturalRestRules(skills, HealBelowPercent: 90, ManaSitBelowPercent: 25, ManaSitUntilPercent: 50, MaximumQuietSits: 12,
				PotionPlan: new NaturalPotionRestPlan(HpTargetPercent: 90, UsesMana: true), RestSkills: NaturalRestSkills.ReagentOnly(90)),
			// The distances and the movement of a pull from 15 m, the Templar's: Cinder Cannon reaches 5 m farther.
			Ranges = NaturalTemplarProfile.Ranges,
			Readiness = NaturalWarriorProfile.Readiness,
			Movement = NaturalTemplarProfile.Movement,
			Campaign = NaturalPriestProfile.PriestLineCampaign,
			Gear = Gear,
			Restock = NaturalWarriorProfile.Restock,
		};
	}
}
