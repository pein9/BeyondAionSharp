using System.Globalization;
using Aion.Bots.Protocol;
using Aion.Bots.Reflexes;
using Aion.Bots.Scenarios;
using Aion.Bots.Timing;
using Aion.Bots.World;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Items;
using Aion.GameServer.Model.Summons;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.Model.Templates.Items.Enums;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Items;
using Aion.GameServer.Services.Summons;
using Aion.GameServer.SkillEngine.Action;
using Aion.GameServer.SkillEngine.Condition;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.SkillEngine.Properties;
using Aion.GameServer.TestKit;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.Stats;

namespace Aion.Simulation.Tests;

public sealed partial class SimulationFastScenarioTests
{
	private async Task RunSkillSweepAsync(ScenarioDefinition scenario, bool includeHistory)
	{
		string root = Path.Combine(RealStaticData.RepoRoot(), "game-server/data/static_data");
		var cases = SkillSweepInventory.Load(root);
		var motion = BotMotionTiming.Load(Path.Combine(root, "skills/motion_times.xml"));
		var report = new DataSweepReport("skills", cases.Select(c => c.Id));
		string reportPath = Path.Combine(Environment.GetEnvironmentVariable("AION_E2E_RUN_DIR")
			?? throw new InvalidOperationException("Sweep requires a run directory."), "data-sweeps", "skills.json");
		report.Save(reportPath);
		using var policy = NewEconomyPolicy(scenario.Id, includeHistory);
		using var timeout = new CancellationTokenSource(TimeSpan.FromHours(2));
		await using var elyos = new SimulationL0Session(fixture, policy, "b01", 131, "Elyskillsweep");
		await using var asmodian = new SimulationL0Session(fixture, policy, "b02", 132, "Asmoskillsweep", Race.ASMODIANS);
		await using var elyosHelper = new SimulationL0Session(fixture, policy, "b03", 129, "Elyskillhelper");
		await using var asmodianHelper = new SimulationL0Session(fixture, policy, "b04", 130, "Asmoskillhelper", Race.ASMODIANS);
		var sessions = new[] { elyos, asmodian, elyosHelper, asmodianHelper };
		foreach (var subject in sessions)
		{
			subject.BeginStep("setup", "ordinary-character-and-director-skill-prerequisites");
			await subject.LoginAndAuthenticateAsync(timeout.Token); await subject.CreateCharacterAsync(timeout.Token);
			await subject.EnterWorldAsync(timeout.Token); await subject.SynchronizeAsync(timeout.Token);
			Assert.Equal(0, fixture.World.GetPlayer(subject.CharacterId).AccessLevel);
		}
		foreach (var pair in new[] { (Subject: elyos, Helper: elyosHelper), (Subject: asmodian, Helper: asmodianHelper) })
		{
			var helper = fixture.World.GetPlayer(pair.Helper.CharacterId);
			Assert.True(ClassChangeService.SetClass(helper, PlayerClass.GLADIATOR, validate: false, updateDaevaStatus: true));
			helper.GetCommonData().SetLevel(65); Assert.Equal(65, helper.GetLevel());
			await pair.Subject.SendPacketAsync(pair.Subject.Api.InviteToGroup(helper.GetName()), timeout.Token);
			await pair.Helper.SynchronizeAsync(timeout.Token);
			Assert.Equal(SM_QUESTION_WINDOW.STR_PARTY_DO_YOU_ACCEPT_INVITATION, pair.Helper.Api.World.Question!.Code);
			await pair.Helper.SendPacketAsync(pair.Helper.Api.Answer(1), timeout.Token);
			await pair.Subject.SynchronizeAsync(timeout.Token);
			Assert.True(fixture.World.GetPlayer(pair.Subject.CharacterId).GetPlayerGroup().HasMember(helper.GetObjectId()));
		}
		var prepared = new Dictionary<int, PlayerClass>();
		int completed = 0;
		foreach (var row in cases.OrderBy(c => c.Action == SkillSweepAction.Passive ? 0 : c.Action == SkillSweepAction.Cast ? 1 : 2)
			.ThenBy(c => c.Class.GetClassId()).ThenBy(c => c.Race).ThenBy(c => c.SkillId))
		{
			var session = row.Race == Race.ASMODIANS ? asmodian : elyos;
			var player = fixture.World.GetPlayer(session.CharacterId);
			using var rowPolicy = NewEconomyPolicy(scenario.Id, includeHistory: false);
			using var rowTimeout = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token);
			rowTimeout.CancelAfter(TimeSpan.FromSeconds(30)); var token = rowTimeout.Token;
			var details = new Dictionary<string, string>
			{
				["class"] = row.Class.ToString(), ["race"] = row.Race.ToString(), ["skillId"] = SkillSweepId(row.SkillId),
				["skillLevel"] = SkillSweepId(row.SkillLevel), ["minimumLevel"] = SkillSweepId(row.MinimumLevel),
				["activation"] = row.Activation, ["action"] = row.Action.ToString(),
			};
			bool logChecked = false;
			try
			{
				session.BeginStep(row.Id, "learn-and-execute-shipped-class-skill");
				if (player.GetSummon() is { } previousSummon) SummonsService.Release(previousSummon, UnsummonType.UNSPECIFIED);
				ClearSkillSweepObjects(player);
				if (!prepared.TryGetValue(player.GetObjectId(), out var previousClass) || previousClass != row.Class)
				{
					foreach (var item in player.GetEquipment().GetEquippedItems().ToArray())
						Assert.NotNull(player.GetEquipment().UnEquipItem(item.GetObjectId()));
					foreach (var item in player.GetInventory().GetItems().ToArray())
						Assert.True(player.GetInventory().DecreaseByObjectId(item.GetObjectId(), item.GetItemCount()));
					foreach (var skill in player.GetSkillList().GetAllSkills()) SkillLearnService.RemoveSkill(player, skill.GetSkillId());
					player.GetEffectController().RemoveAllEffects();
					// Prepare resources before changing class: starting classes intentionally ignore SetDp.
					// No class changes occur during a tested action; morphing records/checks the carried DP.
					player.GetCommonData().SetDp(4000);
					Assert.True(ClassChangeService.SetClass(player, row.Class, validate: false, updateDaevaStatus: true));
					// Classless level-20 stigmas also expand to starting classes. Explicit
					// director eligibility permits their high-level test setup, not progression.
					player.GetCommonData().SetDaeva(true);
					player.GetCommonData().SetLevel(65);
					SkillLearnService.LearnNewSkills(player, 1, 65);
					prepared[player.GetObjectId()] = row.Class;
				}
				Assert.Equal(row.Class, player.GetPlayerClass()); Assert.Equal(row.Race, player.GetRace());
				Assert.Equal(65, player.GetLevel()); details["subjectLevel"] = SkillSweepId(player.GetLevel());
				details["setupDaeva"] = "true";
				Assert.False(player.IsDead()); Assert.Equal(0, player.AccessLevel); Assert.False(player.IsStaff());
				foreach (var effect in player.GetEffectController().GetAllEffects().Where(e => row.Action == SkillSweepAction.Passive || !e.IsPassive()).ToArray())
					player.GetEffectController().RemoveEffect(effect.GetSkillId());
				player.GetSkillCoolDowns()?.Clear();
				player.GetLifeStats().SetCurrentHp(player.GetLifeStats().GetMaxHp());
				player.GetLifeStats().SetCurrentMp(player.GetLifeStats().GetMaxMp());
				player.GetCommonData().SetDp(4000);
				var template = DataManager.SKILL_DATA.GetSkillTemplate(row.SkillId);
				// Auto-learning supplies class prerequisites, including higher ranks. Independent
				// lower-rank cases must not be suppressed by a previously applied passive stack.
				foreach (var effect in player.GetEffectController().GetAllEffects().Where(e => e.GetSkillTemplate().GetStack() == template.GetStack()).ToArray())
					player.GetEffectController().RemoveEffect(effect.GetSkillId());
				SkillLearnService.RemoveSkill(player, row.SkillId);
				await session.SynchronizeAsync(token);
				Assert.False(session.Api.World.Skills.ContainsKey(row.SkillId));
				Assert.True(player.GetSkillList().AddSkill(player, row.SkillId, row.SkillLevel));
				await session.SynchronizeAsync(token);
				Assert.Equal(row.SkillLevel, player.GetSkillList().GetSkillLevel(row.SkillId));
				// Java SkillEntryWriter sends 1 for normal skills: each rank has its own ID.
				int wireLevel = player.GetSkillList().GetSkillEntry(row.SkillId).IsNormalSkill() ? 1 : row.SkillLevel;
				Assert.Equal(wireLevel, session.Api.World.Skills[row.SkillId].Level);
				details["packetSkillLevel"] = SkillSweepId(wireLevel);
				details["learnedObserved"] = "true"; details["learnedLevel"] = SkillSweepId(row.SkillLevel);
				if (row.Action == SkillSweepAction.Passive)
				{
					var effect = Assert.Single(player.GetEffectController().GetAllEffects(), e => e.GetSkillId() == row.SkillId);
					Assert.True(effect.IsPassive()); Assert.Equal(row.SkillLevel, effect.GetSkillLevel());
					details["passiveObserved"] = "true"; details["effectSkillLevel"] = SkillSweepId(effect.GetSkillLevel());
				}
				else if (row.Action == SkillSweepAction.Cast)
					await CastSkillSweepAsync(session, player, template, row, motion, elyosHelper, asmodianHelper, details, token);
				else
					await UseSkillProfessionAsync(session, player, row, details, token);
				details["mapId"] = SkillSweepId(player.GetWorldId()); details["instanceId"] = SkillSweepId(player.GetInstanceId());
				logChecked = true; rowPolicy.AssertClean();
				report.Record(row.Id, DataSweepStatus.Passed, "Observed skill learning and its required runtime execution on an ordinary class/race subject", details);
				if (++completed % 50 == 0) Console.WriteLine($"SWEEP-SKILL {completed}/{cases.Count}: {row.Id}");
				session.PacketHistory.Clear(); session.PacketObservations.Clear();
			}
			catch (Exception error)
			{
				details["recentPackets"] = string.Join('\n', session.PacketHistory.TakeLast(20).Select(p => p.PacketType.Name + " " + System.Text.Json.JsonSerializer.Serialize(p.Fields)));
				if (!logChecked)
				{
					try { rowPolicy.AssertClean(); } catch (Exception logError) { details["logFailure"] = logError.ToString(); }
				}
				report.Record(row.Id, DataSweepStatus.Failed, error.ToString(), details);
				Console.WriteLine($"SWEEP-SKILL {row.Id}: failed\n" + System.Text.Json.JsonSerializer.Serialize(details));
				throw;
			}
			finally { report.Save(reportPath); }
		}
		Assert.True(report.Complete);
		foreach (var subject in sessions)
		{
			if (fixture.World.GetPlayer(subject.CharacterId).GetPlayerGroup() != null)
				await subject.SendPacketAsync(subject.Api.LeaveGroup(), timeout.Token);
			await subject.QuitAsync(timeout.Token); await subject.VerifyOfflineAsync(timeout.Token);
		}
		policy.AssertClean();
	}

	private async Task CastSkillSweepAsync(SimulationL0Session session, Player player, SkillTemplate template,
		SkillSweepCase row, BotMotionTiming motion, SimulationL0Session elyosHelper, SimulationL0Session asmodianHelper,
		Dictionary<string, string> details, CancellationToken token)
	{
		var items = (template.GetActions()?.GetActions() ?? []).OfType<ItemUseAction>().GroupBy(a => a.itemid)
			.Select(g => (Id: g.Key, Required: g.Sum(a => a.count), Consumed: g.Where(a => a.expendable).Sum(a => a.count))).ToArray();
		foreach (var item in items) Assert.Equal(0, ItemService.AddItem(player, item.Id, item.Required));
		var itemCounts = items.ToDictionary(i => i.Id, i => player.GetInventory().GetItemCountByItemId(i.Id));
		details["setupItems"] = string.Join(',', items.Select(i => $"{i.Id}:{i.Required}"));
		var conditions = (template.GetStartconditions()?.GetConditions() ?? [])
			.Concat(template.GetUseconditions()?.GetConditions() ?? []).ToArray();
		var weapon = conditions.OfType<WeaponCondition>().FirstOrDefault()?.itemGroups;
		if (conditions.OfType<RideRobotCondition>().Any())
		{
			if (weapon != null) Assert.Contains(ItemGroup.KEYBLADE, weapon);
			weapon = [ItemGroup.KEYBLADE]; // Embark's prerequisite is implicit in ride_robot-only skills.
		}
		var left = conditions.OfType<LeftHandCondition>().FirstOrDefault();
		bool shield = left?.type == LeftHandSlot.SHIELD;
		int extraMastery = 0;
		if (weapon is { Count: > 0 } && (!weapon.Contains(player.GetEquipment().GetMainHandWeaponType() ?? default)
			|| shield && player.GetEquipment().GetMainHandWeapon()?.GetItemTemplate().IsTwoHandWeapon() == true))
		{
			var candidates = DataManager.ITEM_DATA.GetItemTemplates().Where(i => weapon.Contains(i.GetItemGroup())
				&& (!shield || !i.IsTwoHandWeapon())
				&& CanEquipSkillSweepItem(player, i, requireMastery: false))
				.OrderBy(i => i.GetLevel()).ThenBy(i => i.GetTemplateId()).ToArray();
			var item = candidates.FirstOrDefault(i => CanEquipSkillSweepItem(player, i));
			if (item == null)
			{
				// The global stigma rows include physical skills even for book-only classes
				// (inherited shipped-data quirk, plan §7 #62). Supply and report only this
				// missing test prerequisite; never change the class or item definitions.
				var learn = DataManager.SKILL_TREE_DATA.GetTemplatesForSkill(row.SkillId, row.Class, row.Race);
				Assert.NotEmpty(learn); Assert.All(learn, entry => Assert.Null(entry.GetClassId()));
				item = candidates.First();
				extraMastery = DataManager.SKILL_DATA.GetMasterySkills(item.GetItemGroup()).Order().First();
				Assert.True(player.GetSkillList().AddSkill(player, extraMastery, DataManager.SKILL_DATA.GetSkillTemplate(extraMastery).GetLvl()));
				details["setupExtraMastery"] = SkillSweepId(extraMastery);
			}
			var instance = GetOrGrantSkillSweepItem(player, item.GetTemplateId());
			Assert.NotNull(player.GetEquipment().EquipItem(instance.GetObjectId(), ItemSlot.MAIN_HAND.GetSlotIdMask()));
			details["setupWeapon"] = SkillSweepId(item.GetTemplateId());
		}
		if (left != null && (shield ? !player.GetEquipment().IsShieldEquipped()
			: player.GetEquipment().GetOffHandWeapon() == null && player.GetEquipment().GetMainHandWeapon()?.GetItemTemplate().IsTwoHandWeapon() != true))
		{
			var item = DataManager.ITEM_DATA.GetItemTemplates().Where(i => shield ? i.GetItemGroup() == ItemGroup.SHIELD
				: i.IsWeapon() && !i.IsTwoHandWeapon() && (weapon?.Contains(i.GetItemGroup()) ?? false))
				.Where(i => CanEquipSkillSweepItem(player, i))
				.OrderBy(i => i.GetLevel()).ThenBy(i => i.GetTemplateId()).First();
			var instance = GetOrGrantSkillSweepItem(player, item.GetTemplateId());
			Assert.NotNull(player.GetEquipment().EquipItem(instance.GetObjectId(), ItemSlot.SUB_HAND.GetSlotIdMask()));
			details["setupOffhand"] = SkillSweepId(item.GetTemplateId());
		}
		player.GetChainSkills().ResetChain();
		foreach (var chain in conditions.OfType<ChainCondition>().Where(c => c.preCategory != null))
		{
			for (int i = 0; i < chain.preCount; i++) player.GetChainSkills().UpdateChain(chain.preCategory!, 60_000);
			details["setupChain"] = $"{chain.preCategory}:{chain.preCount}";
		}
		bool point = template.GetProperties()?.GetFirstTarget() == FirstTargetAttribute.POINT;
		bool petTarget = template.GetProperties()?.GetFirstTarget() == FirstTargetAttribute.MYPET;
		bool petOrder = DataManager.PET_SKILL_DATA.IsPetOrderSkill(row.SkillId);
		bool needsPet = petTarget || petOrder || (template.GetEffects()?.GetEffects() ?? []).OfType<PetOrderUnSummonEffect>().Any();
		bool self = point || petTarget || template.GetProperties()?.GetFirstTarget() is FirstTargetAttribute.ME or FirstTargetAttribute.TARGETORME;
		Creature target = player;
		SimulationL0Session? targetSession = null;
		bool friend = template.GetProperties()?.GetTargetRelation() is TargetRelationAttribute.FRIEND or TargetRelationAttribute.MYPARTY;
		bool playerTarget = conditions.OfType<TargetCondition>().Any(c => c.GetValue() == TargetAttribute.PC)
			|| template.GetProperties()?.GetTargetSpecies() == TargetSpeciesAttribute.PC;
		if (self || friend || playerTarget || template.HasResurrectEffect())
		{
			// Independent non-NPC cases must not inherit a fight from the preceding target.
			// Use the shipped starting location, then end teleport protection through CM_MOVE.
			var start = DataManager.PLAYER_INITIAL_DATA.GetSpawnLocation(row.Race);
			await TeleportForSetupAsync(session, player, start.GetMapId(), start.GetX(), start.GetY(), start.GetZ(), token);
			await session.MoveToPositionAsync(new BotPosition(start.GetX() + 1, start.GetY(), start.GetZ(), 0), token);
			Assert.False(player.IsProtectionActive());
			Assert.DoesNotContain(player.GetPosition().GetWorldMapInstance().GetNpcs(), n => n.IsSpawned() && !n.IsDead()
				&& TribeRelationService.IsAggressive(n, player) && PositionUtil.IsInRange(n, player, n.GetAggroRange() + 10));
			details["setupNonCombatPosition"] = "shipped-start-location";
		}
		if (!self && (friend || playerTarget || template.HasResurrectEffect()))
		{
			bool elyosTarget = friend ? row.Race == Race.ELYOS : row.Race != Race.ELYOS;
			targetSession = elyosTarget ? elyosHelper : asmodianHelper;
			var helper = fixture.World.GetPlayer(targetSession.CharacterId);
			if (helper.IsDead())
			{
				await targetSession.AdvanceAsync(TimeSpan.FromMilliseconds(501), token);
				await targetSession.SendPacketAsync(targetSession.Api.Revive(), token);
				await targetSession.SynchronizeAsync(token); Assert.False(helper.IsDead());
			}
			helper.GetEffectController().RemoveAllEffects();
			helper.GetLifeStats().SetCurrentHp(helper.GetLifeStats().GetMaxHp());
			helper.GetLifeStats().SetCurrentMp(helper.GetLifeStats().GetMaxMp());
			await TeleportForSetupAsync(targetSession, helper, player.GetWorldId(), player.GetX() + 5, player.GetY(), player.GetZ(), token, player.GetInstanceId());
			await targetSession.MoveToPositionAsync(new BotPosition(player.GetX() + 1, player.GetY(), player.GetZ(), 0), token);
			Assert.False(helper.IsProtectionActive());
			target = helper;
			details["setupPlayerTarget"] = helper.GetName();
			if (template.HasResurrectEffect())
			{
				Assert.True(helper.GetController().Die(helper));
				await targetSession.AdvanceAsync(TimeSpan.FromMilliseconds(501), token);
				Assert.True(helper.IsDead());
			}
		}
		else if (!self)
		{
			int map = row.Race == Race.ASMODIANS ? 220010000 : 210010000;
			int npcId = row.Race == Race.ASMODIANS ? 210365 : 210119;
			Npc? FindTarget() => fixture.World.GetWorldMap(map).GetMainWorldMapInstance().GetNpcs(npcId)
				.Where(n => n.IsSpawned() && !n.IsDead() && !n.GetLifeStats().IsAboutToDie()).OrderBy(n => n.GetObjectId()).FirstOrDefault();
			Npc? npc = FindTarget();
			int respawnWait = 0;
			while (npc == null && respawnWait < 60_000)
			{
				await session.AdvanceAsync(TimeSpan.FromSeconds(1), token);
				respawnWait += 1000; npc = FindTarget();
			}
			Assert.NotNull(npc); target = npc;
			details["respawnWaitMillis"] = SkillSweepId(respawnWait);
			target.GetEffectController().RemoveAllEffects();
			await PlaceBesideNpcAsync(session, player, (Npc)target, token);
		}
		if (needsPet)
		{
			var summonSkill = player.GetSkillList().GetAllSkills().Select(s => DataManager.SKILL_DATA.GetSkillTemplate(s.GetSkillId()))
				.Where(s => (s.GetEffects()?.GetEffects() ?? []).OfType<SummonEffect>().Any(e => e.GetType() == typeof(SummonEffect)
					&& (!petOrder || DataManager.PET_SKILL_DATA.GetPetOrderSkill(row.SkillId, e.npcId).HasValue)))
				.OrderBy(s => s.GetSkillId()).First();
			details["setupSummonSkill"] = SkillSweepId(summonSkill.GetSkillId());
			Aion.GameServer.SkillEngine.SkillEngine.GetInstance().ApplyEffectDirectly(summonSkill.GetSkillId(), player, player);
			var pet = Assert.IsType<Summon>(player.GetSummon()); Assert.True(pet.IsPet());
			details["petNpcId"] = SkillSweepId(pet.GetNpcId());
			if (petTarget) target = pet;
		}
		if (conditions.OfType<RideRobotCondition>().Any())
		{
			Assert.Equal(ItemGroup.KEYBLADE, player.GetEquipment().GetMainHandWeaponType());
			Aion.GameServer.SkillEngine.SkillEngine.GetInstance().ApplyEffectDirectly(2767, player, player);
			Assert.True(player.IsInRobotMode()); details["setupRobotSkill"] = "2767";
		}
		await session.SendPacketAsync(session.Api.Target(target.GetObjectId()), token);
		long wait = Math.Max(player.GetNextSkillUse() - SystemClock.CurrentMillis(), (long)Math.Ceiling(session.Api.Timing.TimeUntilCast(row.SkillId).TotalMilliseconds));
		if (conditions.OfType<CombatCheckCondition>().Any())
			wait = Math.Max(wait, player.GetController().GetLastCombatTime() + 10_001 - SystemClock.CurrentMillis());
		if (wait > 0) await session.AdvanceAsync(TimeSpan.FromMilliseconds(wait + 1), token);
		if (conditions.OfType<CombatCheckCondition>().Any()) Assert.False(player.GetController().IsInCombat());
		if (template.GetCounterSkill() is { } counter)
		{
			player.SetLastCounterSkill(counter);
			Assert.Equal(SystemClock.CurrentMillis(), player.GetLastCounterSkill(counter));
			details["setupCounter"] = counter.ToString();
		}
		int statusSkill = 0;
		var statuses = template.GetProperties()?.GetTargetStatus();
		if (statuses is { Count: > 0 } && template.GetStack() != "RI_PROTECTIONCURTAIN")
		{
			// Director setup applies a real shipped effect; the subject's normal cast must
			// still pass TargetStatusProperty and (for Remove Shock) dispel that effect.
			statusSkill = statuses.Contains(AbnormalState.STUN) ? 8255
				: statuses.Contains(AbnormalState.OPENAERIAL) ? 8224
				: statuses.Contains(AbnormalState.STUMBLE) ? 8218
				: statuses.Contains(AbnormalState.BIND) ? 1754
				: statuses.Contains(AbnormalState.FEAR) ? 8345
				: throw new InvalidDataException($"No real effect setup for {string.Join(',', statuses)}");
			Aion.GameServer.SkillEngine.SkillEngine.GetInstance().ApplyEffectDirectly(statusSkill, player, target, 30_000, Effect.ForceType.DEFAULT);
			Assert.Contains(statuses, target.GetEffectController().IsAbnormalSet);
			details["setupTargetStatusSkill"] = SkillSweepId(statusSkill);
		}
		try
		{
			string group = player.GetEquipment().GetMainHandWeaponType()?.ToString() ?? "";
			var weaponMotion = group switch
			{
				"SWORD" => BotWeaponMotionType.OneHand, "DAGGER" => BotWeaponMotionType.Dagger,
				"MACE" => BotWeaponMotionType.Mace, "STAFF" => BotWeaponMotionType.Staff,
				"GREATSWORD" => BotWeaponMotionType.TwoHand, "POLEARM" => BotWeaponMotionType.Polearm,
				"BOW" => BotWeaponMotionType.Bow, "ORB" => BotWeaponMotionType.Orb, "SPELLBOOK" => BotWeaponMotionType.Book,
				"HARP" => BotWeaponMotionType.Harp, "GUN" => BotWeaponMotionType.OneGun, "CANNON" => BotWeaponMotionType.Cannon,
				"KEYBLADE" => BotWeaponMotionType.Keyblade, _ => BotWeaponMotionType.NoWeapon,
			};
			if (player.GetEquipment().GetOffHandWeaponType() is { } offhand)
				weaponMotion = offhand == ItemGroup.GUN ? BotWeaponMotionType.TwoGun : BotWeaponMotionType.TwoWeapon;
			int travel = template.GetAmmoSpeed() > 0 ? checked((int)Math.Ceiling(PositionUtil.GetDistance(player, target) / template.GetAmmoSpeed() * 1000)) : 0;
			int hit = motion.CalculateClientHitTime(template, new BotMotionProfile(row.Race, Gender.MALE, weaponMotion,
				player.GetGameStats().GetAttackSpeedRate(), player.IsInRobotMode(), player.IsHitTimeBoosted(), player.GetHitTimeBoostCastSpeed()), travel);
			var cast = new SpellCastData((ushort)row.SkillId, (byte)row.SkillLevel, point ? (byte)1 : (byte)0)
			{ TargetObjectId = target.GetObjectId(), X = player.GetX(), Y = player.GetY(), Z = player.GetZ(), HitTime = checked((ushort)hit) };
			int firstPacket = session.PacketHistory.Count;
			await session.SendPacketAsync(session.Api.Cast(cast), token);
			await session.SynchronizeAsync(token);
			if (template.IsCharge())
			{
				var casting = player.GetCastingSkill(); Assert.NotNull(casting);
				var charge = DataManager.SKILL_CHARGE_DATA.GetChargedSkillEntry(template.GetSkillChargeCondition()!.GetValue());
				Assert.Equal(row.SkillId, charge.GetSkills()![0].GetId());
				int chargeMillis = checked((int)Math.Ceiling(charge.GetMinTime() * casting.GetCastSpeedForAnimationBoostAndChargeSkills())) + 1;
				details["chargeMillis"] = SkillSweepId(chargeMillis);
				await session.AdvanceAsync(TimeSpan.FromMilliseconds(chargeMillis), token);
				await session.SendPacketAsync(GameClientPackets.UseChargeSkill(), token);
			}
			else if (template.GetDuration() > 0)
				await session.AdvanceAsync(TimeSpan.FromMilliseconds(template.GetDuration() + 1), token);
			await session.AdvanceAsync(TimeSpan.FromMilliseconds(hit + 1), token);
			await session.SynchronizeAsync(token);
			details["castPackets"] = string.Join('\n', session.PacketHistory.Skip(firstPacket)
				.Where(p => p.PacketType == typeof(SM_CASTSPELL) || p.PacketType == typeof(SM_CASTSPELL_RESULT)
					|| p.PacketType == typeof(SM_SKILL_CANCEL) || p.PacketType == typeof(SM_SYSTEM_MESSAGE))
				.Select(p => p.PacketType.Name + " " + System.Text.Json.JsonSerializer.Serialize(p.Fields)));
			Assert.Single(session.PacketHistory.Skip(firstPacket), p => p.PacketType == typeof(SM_CASTSPELL_RESULT) && p.Get<ushort>("skillId") == row.SkillId);
			if (petOrder)
			{
				var pet = Assert.IsType<Summon>(player.GetSummon());
				int petSkillId = DataManager.PET_SKILL_DATA.GetPetOrderSkill(row.SkillId, pet.GetNpcId())!.Value;
				var petSkill = DataManager.SKILL_DATA.GetSkillTemplate(petSkillId);
				Assert.Contains(session.PacketHistory.Skip(firstPacket), p => p.PacketType == typeof(SM_SUMMON_USESKILL));
				await session.SendPacketAsync(session.Api.SummonCast(pet.GetObjectId(), (ushort)petSkillId, (byte)petSkill.GetLvl(), target.GetObjectId()), token);
				await session.AdvanceAsync(TimeSpan.FromMilliseconds(petSkill.GetDuration() + 1), token);
				await session.SynchronizeAsync(token);
				Assert.Single(session.PacketHistory.Skip(firstPacket), p => p.PacketType == typeof(SM_CASTSPELL_RESULT) && p.Get<ushort>("skillId") == petSkillId);
				details["petCastObserved"] = "true"; details["petSkillId"] = SkillSweepId(petSkillId);
			}
			if (targetSession != null)
			{
				await targetSession.SynchronizeAsync(token);
				if (template.HasResurrectEffect())
				{
					Assert.True(((Player)target).GetResStatus());
					await targetSession.SendPacketAsync(targetSession.Api.Revive(BotReviveType.Skill), token);
					await targetSession.SynchronizeAsync(token); Assert.False(target.IsDead());
					details["resurrectionObserved"] = "true";
				}
				targetSession.PacketHistory.Clear(); targetSession.PacketObservations.Clear();
			}
			foreach (var item in items)
			{
				long expected = itemCounts[item.Id] - item.Consumed;
				Assert.Equal(expected, player.GetInventory().GetItemCountByItemId(item.Id));
				Assert.Equal(expected, session.Api.World.Inventory.Values.Where(i => i.ItemId == item.Id).Sum(i => i.Count));
			}
			details["consumedItems"] = string.Join(',', items.Select(i => $"{i.Id}:{i.Consumed}"));
			details["castObserved"] = "true"; details["targetObjectId"] = SkillSweepId(target.GetObjectId());
			details["clientHitMillis"] = SkillSweepId(hit);
		}
		finally
		{
			if (statusSkill != 0) target.GetEffectController().RemoveEffect(statusSkill);
			if (needsPet && player.GetSummon() is { } pet) SummonsService.Release(pet, UnsummonType.UNSPECIFIED);
			if (conditions.OfType<RideRobotCondition>().Any()) player.GetEffectController().RemoveEffect(2767);
			details["cleanupSummonedObjects"] = SkillSweepId(ClearSkillSweepObjects(player));
			if (extraMastery != 0)
			{
				Assert.NotNull(player.GetEquipment().UnEquipItem(player.GetEquipment().GetMainHandWeapon().GetObjectId()));
				SkillLearnService.RemoveSkill(player, extraMastery);
			}
		}
	}

	private async Task UseSkillProfessionAsync(SimulationL0Session session, Player player, SkillSweepCase row,
		Dictionary<string, string> details, CancellationToken token)
	{
		int gatherFailure = CraftConfig.MAX_GATHER_FAILURE_CHANCE, craftFailure = CraftConfig.MAX_CRAFT_FAILURE_CHANCE;
		CraftConfig.MAX_GATHER_FAILURE_CHANCE = 0; CraftConfig.MAX_CRAFT_FAILURE_CHANCE = 0;
		try
		{
			Gatherable? node = null;
			Aion.GameServer.Model.Templates.Recipe.RecipeTemplate? recipe = null;
			(int ItemId, long Count)[] materials = [];
			int dpBefore = 0;
			if (row.Action == SkillSweepAction.Gather)
			{
				var nodes = new List<Gatherable>();
				fixture.World.ForEachObject(o =>
				{
					if (o is Gatherable g && g.IsSpawned() && g.GetObjectTemplate().GetHarvestSkill() == row.SkillId
						&& g.GetObjectTemplate().GetSkillLevel() <= row.SkillLevel && g.GetObjectTemplate().GetRequiredItemId() == 0)
						nodes.Add(g);
				});
				Assert.NotEmpty(nodes);
				node = nodes.OrderBy(g => g.GetWorldId() / 10000000 == (row.Race == Race.ASMODIANS ? 22 : 21) ? 0 : 1)
					.ThenBy(g => g.GetWorldId()).ThenBy(g => g.GetObjectId()).First();
				await TeleportForSetupAsync(session, player, node.GetWorldId(), node.GetX() - 5, node.GetY(), node.GetZ(), token, node.GetInstanceId());
				await session.MoveToPositionAsync(new BotPosition(node.GetX() - 1, node.GetY(), node.GetZ(), 0), token);
				details["gatherableId"] = SkillSweepId(node.GetObjectTemplate().GetTemplateId());
			}
			else
			{
				Assert.Equal(SkillSweepAction.Craft, row.Action); Assert.Equal(40009, row.SkillId);
				recipe = DataManager.RECIPE_DATA.GetRecipeTemplates().Where(r => r.GetSkillId() == row.SkillId
					&& r.GetSkillpoint() <= row.SkillLevel && (r.GetRace() == Race.PC_ALL || r.GetRace() == row.Race))
					.OrderBy(r => r.GetId()).First();
				player.GetCraftCooldowns().Clear(); player.GetCommonData().SetDp(recipe.GetDp());
				dpBefore = player.GetCommonData().GetDp();
				Assert.True(dpBefore >= recipe.GetDp());
				details["dpBefore"] = SkillSweepId(dpBefore);
				details["dpRule"] = row.Class.IsStartingClass() ? "starting-class-unchanged" : "recipe-cost";
				if (!player.GetRecipeList().IsRecipePresent(recipe.GetId())) Assert.True(player.GetRecipeList().AddRecipe(player, recipe.GetId()));
				materials = recipe.GetComponents()[0].GetComponent().GroupBy(c => c.GetItemId())
					.Select(g => (ItemId: g.Key, Count: g.Sum(c => (long)c.GetQuantity()))).ToArray();
				foreach (var material in materials) Assert.Equal(0, ItemService.AddItem(player, material.ItemId, material.Count));
				details["recipeId"] = SkillSweepId(recipe.GetId());
				details["components"] = string.Join(',', materials.Select(m => $"{m.ItemId}:{m.Count}"));
			}
			await session.SynchronizeAsync(token);
			var expected = session.Api.World.Inventory.Values.GroupBy(i => i.ItemId).ToDictionary(g => g.Key, g => g.Sum(i => i.Count));
			int firstPacket = session.PacketHistory.Count;
			if (node != null)
				foreach (var packet in session.Api.Gather(node.GetObjectId())) await session.SendPacketAsync(packet, token);
			else
				await session.SendPacketAsync(session.Api.Craft(0, recipe!.GetId(), 0, materials, unknown: 129), token);
			await session.SynchronizeAsync(token); Assert.NotNull(player.GetInteractionTask());
			long deadline = fixture.Clock.NowMillis + 120_000;
			while (player.GetInteractionTask() != null)
			{
				long next = fixture.Clock.NextDueMillis ?? throw new InvalidDataException("Profession interaction has no scheduled work.");
				Assert.InRange(next, fixture.Clock.NowMillis, deadline);
				await session.AdvanceAsync(TimeSpan.FromMilliseconds(next - fixture.Clock.NowMillis), token);
				if (fixture.Clock.Faults.Count > 0)
					throw new AggregateException("Skill profession observed a timer fault.", fixture.Clock.Faults.Select(f => f.Exception));
			}
			await session.SynchronizeAsync(token);
			var packetType = node != null ? typeof(SM_GATHER_UPDATE) : typeof(SM_CRAFT_UPDATE);
			byte success = node != null ? (byte)6 : (byte)5;
			var completion = Assert.Single(session.PacketHistory.Skip(firstPacket), p => p.PacketType == packetType && p.Get<byte>("action") >= success - 1);
			Assert.Equal(success, completion.Get<byte>("action"));
			int product = completion.Get<int>("itemId"), count = node != null ? Rates.GATHERING_COUNT.CalcResult(player, 1) : recipe!.GetQuantity();
			Assert.True(count > 0);
			if (node != null) Assert.Contains(node.GetObjectTemplate().GetMaterials().GetMaterial(), m => m.GetItemId() == product);
			else
			{
				Assert.Contains(product, Enumerable.Range(1, recipe!.GetComboProductSize()).Select(i => recipe.GetComboProduct(i)!.Value).Prepend(recipe.GetProductId()));
				// Java PlayerCommonData.setDp ignores every DP change for starting classes (plan §7 #63).
				int expectedDp = row.Class.IsStartingClass() ? dpBefore : dpBefore - recipe.GetDp();
				Assert.Equal(expectedDp, player.GetCommonData().GetDp());
				details["dpAfter"] = SkillSweepId(player.GetCommonData().GetDp());
			}
			foreach (var material in materials)
			{
				expected[material.ItemId] -= material.Count;
				if (expected[material.ItemId] == 0) expected.Remove(material.ItemId);
			}
			expected[product] = expected.GetValueOrDefault(product) + count;
			Assert.Equal(expected.OrderBy(i => i.Key), session.Api.World.Inventory.Values.GroupBy(i => i.ItemId)
				.ToDictionary(g => g.Key, g => g.Sum(i => i.Count)).OrderBy(i => i.Key));
			Assert.Equal(expected.OrderBy(i => i.Key), player.GetInventory().GetItemsWithKinah().Concat(player.GetEquipment().GetEquippedItems())
				.GroupBy(i => i.GetItemId()).ToDictionary(g => g.Key, g => g.Sum(i => i.GetItemCount())).OrderBy(i => i.Key));
			details["actionObserved"] = "true"; details["productId"] = SkillSweepId(product); details["productCount"] = SkillSweepId(count);
			Assert.True(player.GetInventory().DecreaseByItemId(product, count));
			await session.SynchronizeAsync(token);
		}
		finally { CraftConfig.MAX_GATHER_FAILURE_CHANCE = gatherFailure; CraftConfig.MAX_CRAFT_FAILURE_CHANCE = craftFailure; }
	}

	private static Item GetOrGrantSkillSweepItem(Player player, int itemId)
	{
		var existing = player.GetInventory().GetItemsByItemId(itemId).FirstOrDefault();
		if (existing != null) return existing;
		Assert.Equal(0, ItemService.AddItem(player, itemId, 1));
		return player.GetInventory().GetItemsByItemId(itemId).First();
	}

	private static int ClearSkillSweepObjects(Player player)
	{
		// Independent rows must not inherit traps or attacking servants from a prior cast.
		var spawned = player.GetPosition().GetWorldMapInstance().OfType<SummonedObject<Creature>>()
			.Where(o => o.GetCreator() == player).ToArray();
		foreach (var creature in spawned) creature.GetController().Delete();
		return spawned.Length;
	}

	private static bool CanEquipSkillSweepItem(Player player, ItemTemplate item, bool requireMastery = true)
	{
		var mastery = DataManager.SKILL_DATA.GetMasterySkills(item.GetItemGroup());
		return (!requireMastery || mastery.Count == 0 || mastery.Any(player.GetSkillList().IsSkillPresent))
			&& item.GetRequiredLevel(player.GetPlayerClass()) > 0 && item.GetRequiredLevel(player.GetPlayerClass()) <= player.GetLevel()
			&& (item.GetMaxLevelRestrict(player.GetPlayerClass()) == 0 || player.GetLevel() <= item.GetMaxLevelRestrict(player.GetPlayerClass()))
			&& (item.GetRace() == Race.PC_ALL || item.GetRace() == player.GetRace())
			&& item.IsClassSpecific(player.GetPlayerClass()) && !item.IsSoulBound();
	}

	private static string SkillSweepId(int value) => value.ToString(CultureInfo.InvariantCulture);
}
