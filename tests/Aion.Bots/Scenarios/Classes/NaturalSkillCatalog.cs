using Aion.Bots.World;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.Model.Templates.Items.Enums;
using Aion.GameServer.SkillEngine.Action;
using Aion.GameServer.SkillEngine.Condition;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.SkillEngine.Properties;

namespace Aion.Bots.Scenarios.Classes;

/// <summary>
/// CP-35: a class's skill rows read from the shipped skill tree and skill templates (docs/natural-class-profiles.md). A
/// profile supplies only what the data cannot say: the role of each skill it casts and the reason for each it does not.
/// The learned SM_SKILL_LIST stays the authority: a catalog never grants a skill. NR-13: the Priest's and the Cleric's
/// catalogs are built here too; each row equals the hand-typed row it replaced in the fields that row held.
/// </summary>
public static class NaturalSkillCatalog
{
	/// <summary>A second class learned its starter's skills only while it was the starter.</summary>
	private const int StarterTopLevel = 9;

	/// <summary>The last level the skill tree is read to for a catalog.</summary>
	private const int HighestLevel = 65;

	/// <summary>The three castable skills every class auto-learns at level 1, and why no rotation holds them. A profile's
	/// exclusions start from these.</summary>
	public static readonly IReadOnlyDictionary<int, string> CommonExcluded = new Dictionary<int, string>
	{
		[243] = "Return is cast by the journey's travel rules, never in a fight.",
		[245] = "Bandage Heal: no class uses a bandage (CP-Q11).",
		[302] = "Escape frees a stuck character; the journey does not cast it.",
	};

	/// <summary>
	/// The skills the class learns by itself up to a level, with the level each arrives at: its own rows of the skill tree,
	/// and for a second class its starter's rows up to level 9 (Java SkillLearnService.learnNewSkills reads
	/// SkillTreeData.getTemplatesFor for the class the character is at each level). Stigma rows are not auto-learned.
	/// </summary>
	public static IReadOnlyDictionary<int, int> AutoLearned(StaticData data, PlayerClass playerClass, int maximumLevel,
		Race race = Race.ASMODIANS)
	{
		ArgumentNullException.ThrowIfNull(data);
		var learned = new Dictionary<int, int>();
		PlayerClass starter = playerClass.GetStartingClass();
		(PlayerClass Class, int Top)[] lines = starter == playerClass
			? [(playerClass, maximumLevel)]
			: [(starter, Math.Min(StarterTopLevel, maximumLevel)), (playerClass, maximumLevel)];
		foreach ((PlayerClass line, int top) in lines)
			for (int level = 1; level <= top; level++)
				foreach (SkillLearnTemplate row in data.SkillTreeDataDh.GetTemplatesFor(line, level, race))
					if (row.IsAutolearn() && !row.IsStigma())
						learned.TryAdd(row.GetSkillId(), row.GetMinLevel());
		return learned;
	}

	/// <summary>The auto-learned skills a character casts itself: activation ACTIVE or CHARGE. A profile gives each one a
	/// role or an exclusion with its reason (<see cref="NaturalProfileValidator"/>).</summary>
	public static IReadOnlyDictionary<int, int> AutoLearnedCastable(StaticData data, PlayerClass playerClass, int maximumLevel,
		Race race = Race.ASMODIANS) => AutoLearned(data, playerClass, maximumLevel, race)
		.Where(entry => data.SkillDataDh.GetSkillTemplate(entry.Key) is { } template && (template.IsActive() || template.IsCharge()))
		.ToDictionary(entry => entry.Key, entry => entry.Value);

	/// <summary>NR-70a: the auto-learned toggles, which are kept on or left off and never cast in a fight. A profile gives
	/// each one a role or an exclusion with its reason too.</summary>
	public static IReadOnlyDictionary<int, int> AutoLearnedToggles(StaticData data, PlayerClass playerClass, int maximumLevel,
		Race race = Race.ASMODIANS) => AutoLearned(data, playerClass, maximumLevel, race)
		.Where(entry => data.SkillDataDh.GetSkillTemplate(entry.Key)?.IsToggle() == true)
		.ToDictionary(entry => entry.Key, entry => entry.Value);

	/// <summary>
	/// The class's catalog: one row for every skill the profile gives a role, in level and id order. A role for a skill the
	/// class does not auto-learn, or for one the profile also excludes, is refused by name.
	/// </summary>
	/// <param name="roles">Skill id to role.</param>
	/// <param name="excluded">Skill id to the reason it is not cast; checked here only against <paramref name="roles"/>.</param>
	public static NaturalPriestSkill[] Build(StaticData data, PlayerClass playerClass, IReadOnlyDictionary<int, string> roles,
		IReadOnlyDictionary<int, string>? excluded = null, Race race = Race.ASMODIANS)
	{
		ArgumentNullException.ThrowIfNull(roles);
		IReadOnlyDictionary<int, int> learned = AutoLearned(data, playerClass, HighestLevel, race);
		foreach (int id in roles.Keys)
		{
			if (excluded?.ContainsKey(id) == true)
				throw new InvalidDataException($"{playerClass} skill {id} has a role and an exclusion.");
			if (!learned.ContainsKey(id))
				throw new InvalidDataException($"{playerClass} does not auto-learn skill {id}; a catalog holds auto-learned skills only.");
		}
		return roles.OrderBy(entry => learned[entry.Key]).ThenBy(entry => entry.Key)
			.Select(entry => Row(data, entry.Key, learned[entry.Key], entry.Value)).ToArray();
	}

	/// <summary>One row from the skill's template. Range is the template's first-target range, without the weapon's; see
	/// <see cref="Reach(NaturalPriestSkill, int?)"/>.</summary>
	public static NaturalPriestSkill Row(StaticData data, int skillId, int minimumLevel, string role)
	{
		ArgumentNullException.ThrowIfNull(data);
		SkillTemplate template = data.SkillDataDh.GetSkillTemplate(skillId)
			?? throw new InvalidDataException($"Skill {skillId} has no shipped template.");
		Properties? properties = template.GetProperties();
		Condition[] conditions = new[] { template.GetStartconditions(), template.GetUseconditions(), template.GetEndConditions() }
			.SelectMany(set => set?.GetConditions() ?? []).ToArray();
		var actions = template.GetActions()?.GetActions() ?? [];
		ChainCondition? chain = template.GetChainCondition();
		ItemUseAction? reagent = actions.OfType<ItemUseAction>().FirstOrDefault();
		List<ItemGroup>? weapons = conditions.OfType<WeaponCondition>().FirstOrDefault()?.itemGroups;
		return new NaturalPriestSkill(checked((ushort)skillId), minimumLevel, role,
			ManaCost: conditions.OfType<MpCondition>().FirstOrDefault()?.value ?? actions.OfType<MpUseAction>().FirstOrDefault()?.value ?? 0,
			Range: properties?.GetFirstTargetRange() ?? 0,
			CooldownId: template.GetCooldownId(),
			CooldownDeciseconds: template.GetCooldown(),
			ChainCategory: chain?.category,
			RequiresChainCategory: chain?.preCategory,
			ChainWindowMillis: chain?.time ?? 0,
			DpCost: conditions.OfType<DpCondition>().FirstOrDefault()?.Value ?? 0,
			ReagentItemId: reagent?.itemid ?? 0,
			ReagentCount: reagent?.count ?? 0,
			TargetKind: properties?.GetFirstTarget()?.ToString(),
			CastMillis: template.GetDuration(),
			RequiredWeaponGroups: weapons is { Count: > 0 } ? weapons.Select(group => group.ToString()).ToArray() : null,
			AddWeaponRange: properties?.IsAddWeaponRange() ?? false,
			SelfCount: chain?.selfCount ?? 0,
			Activation: template.GetActivationAttribute().ToString(),
			CounterStatus: template.GetCounterSkill()?.ToString(),
			OutOfCombatOnly: conditions.OfType<CombatCheckCondition>().Any(),
			GroundOnly: conditions.OfType<NoFlyingCondition>().Any() ||
				conditions.OfType<SelfFlyingCondition>().Any(self => self.Restriction == FlyingRestriction.GROUND),
			TargetFlight: conditions.OfType<TargetFlyingCondition>().FirstOrDefault()?.Restriction.ToString(),
			RequiredOffHand: conditions.OfType<LeftHandCondition>().FirstOrDefault()?.type.ToString(),
			// Java TargetStatusProperty.set 20-28 asks nothing of the one stack it names.
			TargetStates: properties?.GetTargetStatus() is { Count: > 0 } states && template.GetStack() != "RI_PROTECTIONCURTAIN"
				? states.Select(state => state.ToString()).ToArray() : null,
			CarvesRune: template.GetEffects()?.GetEffects().OfType<CarveSignetEffect>().FirstOrDefault()?.signet,
			BurstsRune: template.GetEffects()?.GetEffects().OfType<SignetBurstEffect>().FirstOrDefault()?.signet);
	}

	/// <summary>
	/// NR-50b: what the worn gear gives a skill's left-hand condition, as the server reads it (Java LeftHandCondition.validate,
	/// Equipment.isShieldEquipped): <c>SHIELD</c> with a shield in the off hand; <c>DUAL</c> with a weapon in the off hand or
	/// a two-hand weapon in both; null otherwise.
	/// </summary>
	/// <param name="template">An item's shipped template; null for an item that has none.</param>
	public static string? OffHandHeld(IEnumerable<BotInventoryItem> inventory, Func<int, ItemTemplate?> template)
	{
		ArgumentNullException.ThrowIfNull(inventory);
		ArgumentNullException.ThrowIfNull(template);
		BotInventoryItem[] worn = inventory.Where(item => item.Details.EquippedSlot is 1 or 2 or 3).ToArray();
		ItemTemplate? offHand = worn.FirstOrDefault(item => item.Details.EquippedSlot == 2) is { } held ? template(held.ItemId) : null;
		if (offHand?.GetItemSubType() == ItemSubType.SHIELD) return "SHIELD";
		if (offHand?.IsWeapon() == true) return "DUAL";
		return worn.Any(item => item.Details.EquippedSlot is 1 or 3 && template(item.ItemId)?.IsTwoHandWeapon() == true) ? "DUAL" : null;
	}

	/// <summary>
	/// How far the skill reaches: its range, plus the main-hand weapon's attack range when the template adds it (Java
	/// FirstTargetRangeProperty: firstTargetRange += attackRange / 1000). An unknown weapon range adds nothing.
	/// </summary>
	public static float Reach(NaturalPriestSkill skill, int? weaponAttackRangeMillis) =>
		skill.Range + (skill.AddWeaponRange ? (weaponAttackRangeMillis ?? 0) / 1000f : 0);

	/// <summary>The reach with the weapon the observation holds.</summary>
	public static float Reach(NaturalPriestSkill skill, NaturalCombatObservation state) => Reach(skill, state.WeaponAttackRangeMillis);

	/// <summary>The first step of a chain: it names a chain category and needs none before it (Java ChainCondition).</summary>
	public static bool IsChainOpener(NaturalPriestSkill skill) => skill.ChainCategory != null && skill.RequiresChainCategory == null;
}
