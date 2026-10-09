using Aion.Bots.Dashboard;
using Aion.Bots.Navigation;
using Aion.Bots.Navigation.NavMesh;
using Aion.Bots.Movement;
using Aion.Bots.Protocol;
using Aion.Bots.Reflexes;
using Aion.Bots.Scenarios.Classes;
using Aion.Bots.Timing;
using Aion.Bots.Tracing;
using Aion.Bots.World;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Templates.Npc;
using Aion.GameServer.Model.Templates.Quest;
using Aion.GameServer.Network.Aion.ServerPackets;

using Require = Aion.Bots.Scenarios.NaturalJourneyRequirements;

namespace Aion.Bots.Scenarios;

public sealed partial class NaturalIshalgenJourney
{
	private sealed class NaturalJourneyCombat(INaturalJourneySession session,
		NaturalJourneyNavigator navigator, NaturalJourneyRuntime runtime,
		BotNavigationGeometry geometry, bool stopOnDeath, bool conservativeRangedHold,
		NaturalMauPolicyParameters mauPolicy, NaturalClassLine classLine, int initialRevives = 0)
	{
		// A contained encounter or map segment may create another observer for the same client.
		// Java Skill.setCooldowns belongs to the player, not that observer: retain the packet-derived
		// group deadlines (Herb Treatment and MP Recovery share 1153) across those boundaries.
		private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<BotWorldModel,
			Dictionary<int, DateTimeOffset>> CooldownsByWorld = new();
		private readonly Dictionary<int, DateTimeOffset> cooldowns = CooldownsByWorld.GetValue(session.Api.World, _ => new());
		// CP-39: the chain the last chain skill opened, kept the server's way (Java ChainSkills), from the chain flag of
		// its SM_CASTSPELL_RESULT.
		private NaturalChainState tableChain = NaturalChainState.None;
		// CP-39: the weapon's swings; the attack number wraps at 256 as a byte does.
		private int swings;
		private ushort? lastCancelledSkillId;
		private bool lastCastCompleted;
		private ushort? lastPowderSkill;
		// NA-19: when the current visible-effect snapshot was first seen (its remaining times are as of then).
		private IReadOnlyList<BotVisibleEffect>? effectsSnapshot;
		private long effectsSeenAtMillis;
		private int revives = initialRevives is >= 0 and <= MaximumRevives ? initialRevives : throw new ArgumentOutOfRangeException(nameof(initialRevives));
		private int completedRetreats;
		private int? engagedTarget;
		private int combatAttemptId;
		private string[] lastCombatTrace = [];
		private int obstacleRepositions;
		private int rangeRejections;
		private int readinessRejections;
		public int ReviveCount => revives;
		/// <summary>The map a far target is approached on (NA-23 fights in Altgard); Ishalgen for the journey.</summary>
		public int ApproachMapId { get; set; } = 220010000;
		/// <summary>NR-52: asked when the bot must go to its target on a map that is not the one this fight was made for.
		/// The journey then puts the fight on the map the client observes (<see cref="EnterMap"/>). The Ascension trial is
		/// fought in its own instance by the journey's Ishalgen fight: a class that fights from where it stands never
		/// asks, and one that walks to its opponent could not reach it.</summary>
		public Action? EnterObservedMap { get; set; }
		public Action? AfterBindRevive { get; set; }
		/// <summary>Revives at the bound obelisk, as against revives inside an instance.</summary>
		public int BindReviveCount => bindRevives;
		public int InstanceReviveCount => instanceRevives;
		private int bindRevives, instanceRevives;
		/// <summary>Every soul healing this observer made after an obelisk resurrection, as the client saw it.</summary>
		public IReadOnlyList<NaturalSoulHeal> SoulHeals => soulHeals;
		private readonly List<NaturalSoulHeal> soulHeals = [];
		/// <summary>The map view to rebuild after a revive inside an instance; the bind revive's hook when a leg sets none.</summary>
		public Action? AfterInstanceRevive { get; set; }

		/// <summary>
		/// The operator's death rule (2026-10-06): after a resurrection at the obelisk, "find the nearest soul healer and recover".
		/// It is a rule of the bot, for every leg and level, not leg data: the Soul Healers are the NPCs with the shipped title
		/// 350412, and every obelisk of the journey's maps has one within ten metres. The healing gives back the recoverable XP
		/// for Kinah and takes the soul sickness off (Java DialogService, action RECOVERY). Where no Soul Healer stands near the
		/// revive point, or it cannot be reached, nothing is forced: the XP stays recoverable until the next obelisk resurrection.
		/// </summary>
		private async Task SoulHealAtTheNearestSoulHealerAsync(CancellationToken token)
		{
			BotWorldModel world = session.Api.World;
			if (world.MapId is not int map || world.IsDead) return;
			void Skipped(string reason) => session.TraceDiagnostic("soul-heal-skipped", new Dictionary<string, object?>
			{
				["map"] = map, ["position"] = session.CurrentPosition, ["reason"] = reason, ["recoverableExperience"] = world.RecoverableExperience,
			});
			var soulHealers = runtime.Data.SpawnsDh.GetSpawnsByWorldId(map)
				.Where(group => runtime.Data.NpcDataDh.GetNpcTemplate(group.GetNpcId())?.GetTitleId() == NaturalServicePolicy.SoulHealerTitleId)
				.SelectMany(group => group.GetSpawnTemplates().Select(spot => (group.GetNpcId(), new BotPosition(spot.GetX(), spot.GetY(), spot.GetZ(), spot.GetHeading()))));
			if (NaturalServicePolicy.NearestSoulHealer(soulHealers, session.CurrentPosition) is not { } healer)
			{
				Skipped($"No Soul Healer stands within {NaturalServicePolicy.SoulHealerSearchRadius} m of the revive point.");
				return;
			}
			float talkRange = Math.Min(5, runtime.Data.NpcDataDh.GetNpcTemplate(healer.NpcId)!.GetTalkDistance());
			BotKnownObject? InTalkRange() => world.Objects.Values.Where(known => known.TemplateId == healer.NpcId && !known.IsCorpse &&
					Distance(session.CurrentPosition, known.SettledPosition) <= talkRange)
				.OrderBy(known => Distance(session.CurrentPosition, known.SettledPosition)).FirstOrDefault();
			int? healerObject = InTalkRange()?.ObjectId;
			if (healerObject == null)
			{
				NaturalNavigationResult reached = await NaturalIshalgenNavigator.ApproachNpcAsync(map, healer.NpcId, healer.Position, navigator, token);
				healerObject = reached.Arrived ? reached.TargetObjectId : InTalkRange()?.ObjectId;
				if (healerObject == null)
				{
					Skipped($"Soul Healer {healer.NpcId} was not reached: {reached.Reason}");
					return;
				}
			}
			soulHeals.Add(await new NaturalServiceSteps(session).SoulHealAsync(healerObject.Value, healer.NpcId, runtime.NowMillis, token));
		}

		/// <summary>BC-06: retain combat/revival counters while switching to the new map's checked navigation.</summary>
		public void EnterMap(NaturalJourneyNavigator currentNavigator, BotNavigationGeometry currentGeometry, int mapId)
		{
			navigator.InCombat = false;
			navigator = currentNavigator;
			navigator.InCombat = InCombat;
			geometry = currentGeometry;
			ApproachMapId = mapId;
			tableChain = NaturalChainState.None;
		}

		/// <summary>CP-15: the profile of the class the client observes now. It is read again on every use, because the
		/// starter becomes its second class inside one run.</summary>
		public NaturalClassProfile ClassProfile => NaturalClassProfiles.For(
			session.Api.World.Objects.GetValueOrDefault(session.CharacterId)?.PlayerClass, classLine, runtime.Data);
		/// <summary>NA-18: the observed class chooses the catalog (the Cleric adds its level 10 skills).</summary>
		private NaturalPriestSkill[] Catalog => ClassProfile.Skills;
		public int CompletedRetreats => completedRetreats;
		/// <summary>NR-15: fights that ended because the target gave up and walked home.</summary>
		public int TargetReturns { get; private set; }

		/// <summary>
		/// NR-48: how many targets were taken from under the bot: a monster another player has just killed, or one that
		/// was gone when the bot reached its place. Only a world shared with other players has them. The kill loop counts
		/// them apart from its failed pulls.
		/// </summary>
		public int TargetsTaken { get; private set; }

		/// <summary>NR-48: a target the caller found taken.</summary>
		public void NoteTargetTaken() => TargetsTaken++;
		public bool InCombat { get; private set; }
		public Func<CancellationToken, Task>? MaintainInventoryAsync { get; set; }
		/// <summary>Leave the pack; when no checked escape leads away from it (a pocket, a ledge, more
		/// monsters on every way out), fight the attackers nearest-first instead, as a cornered player would.</summary>
		public async Task EscapeAsync(BotPosition refuge, IReadOnlySet<int> observedAttackers,
			CancellationToken token)
		{
			if (await RetreatFromPackAsync(refuge, observedAttackers, token)) return;
			foreach (int attacker in observedAttackers
				.Select(id => navigator.Observe().Npcs.FirstOrDefault(npc => npc.ObjectId == id))
				.OfType<NaturalNavigationObject>()
				.Where(npc => Distance(session.CurrentPosition, npc.Position) < 30)
				.OrderBy(npc => PriorityForEngagedTarget(npc.ObjectId, observedAttackers.Count,
					session, mauPolicy.PreferWoundedWhenTwoAttackers))
				.ThenBy(npc => Distance(session.CurrentPosition, npc.Position))
				.Select(npc => npc.ObjectId).ToArray())
			{
				cornered = true;
				await TryKillAsync(attacker, token);
				if (session.Api.World.IsDead || session.Api.World.CurrentHp <= 0) return;
			}
		}

		// Set when a retreat found no checked way out; the policy then fights instead of retreating again.
		private bool cornered;

		/// <summary>NA-13: Q2008's Ataxiar trial — no exit and 1-damage NPCs, so never retreat from the swarm.</summary>
		public bool ScriptedTrial { get; set; }
		public const int MaximumCombatActions = 1000;
		private const int MaximumRevives = 20;

		/// <summary>Where the Priest died (client position at death). Pull planning avoids firing from near
		/// these, as a player avoids the spot where hidden or respawning monsters killed them.</summary>
		public List<BotPosition> DeathSpots { get; } = [];

		public async Task KillAsync(int target, CancellationToken token)
		{
			int taken = TargetsTaken;
			if (!await TryKillAsync(target, token))
			{
				// NR-48: another player took this one. The caller hunts by its quest's count or its item count, finds the
				// count unchanged, and goes for the next monster.
				if (TargetsTaken > taken) return;
				throw new InvalidDataException($"Engaged NPC {target} disappeared without client-observed kill evidence.");
			}
		}

		/// <summary>Runs after every fight that ends in a kill, outside the fight (the general quest-loot sweep).</summary>
		public Func<CancellationToken, Task>? AfterKillAsync { get; set; }

		public async Task<bool> TryKillAsync(int target, CancellationToken token,
			BotPosition? retreatAnchor = null, int? attackHistoryStart = null)
		{
			int? fightMap = session.Api.World.MapId;
			bool killed = await FightAsync(target, token, retreatAnchor, attackHistoryStart);
			if (killed && session.Api.World.MapId == fightMap && AfterKillAsync is { } afterKill && !session.Api.World.IsDead)
				await afterKill(token);
			return killed;
		}

		private async Task<bool> FightAsync(int target, CancellationToken token, BotPosition? retreatAnchor, int? attackHistoryStart)
		{
			if (InCombat) throw new InvalidOperationException($"Natural {classLine.StarterName} combat cannot nest another fight.");
			int attemptId = ++combatAttemptId;
			int revivesBefore = revives;
			InCombat = true;
			navigator.InCombat = true;
			session.TraceDiagnostic("combat-encounter-start", new Dictionary<string, object?>
			{
				["encounterId"] = attemptId, ["targetObjectId"] = target,
			});
			try
			{
				bool killed = await TryKillCoreAsync(target, token, retreatAnchor, attackHistoryStart);
				session.TraceDiagnostic("combat-encounter-end", new Dictionary<string, object?>
				{
					["encounterId"] = attemptId, ["targetObjectId"] = target,
					["clientObservedKill"] = killed, ["revives"] = revives - revivesBefore,
					["retreats"] = completedRetreats,
				});
				return killed;
			}
			catch (Exception error)
			{
				session.TraceDiagnostic("combat-encounter-end", new Dictionary<string, object?>
				{
					["encounterId"] = attemptId, ["targetObjectId"] = target,
					["clientObservedKill"] = false, ["revives"] = revives - revivesBefore,
					["error"] = error.GetType().Name,
				});
				throw;
			}
			finally
			{
				InCombat = false;
				navigator.InCombat = false;
				cornered = false;
			}
		}

		private async Task<bool> TryKillCoreAsync(int target, CancellationToken token,
			BotPosition? retreatAnchor, int? attackHistoryStart)
		{
			engagedTarget = target;
			obstacleRepositions = 0;
			rangeRejections = 0;
			BotWorldModel world = session.Api.World;
			long startingExperience = world.CurrentExperience;
			var trace = new List<string>();
			int observedPacketCount = attackHistoryStart ?? session.PacketHistory.Count;
			int statusPacketCount = session.PacketHistory.Count;
			int? observedTargetHpPercent = null;
			bool healedThisFight = false;
			// NR-53b: what this fight has cast, so that a pull is cast once.
			var castThisFight = new HashSet<ushort>();
			bool inEmergency = false;
			var targetTemplate = navigator.Observe().Npcs.FirstOrDefault(npc => npc.ObjectId == target) is { } observedTarget
				? runtime.Data.NpcDataDh.GetNpcTemplate(observedTarget.TemplateId) : null;
			bool targetSeasoned = targetTemplate != null && targetTemplate.GetRank() >= Aion.GameServer.Model.Templates.Npc.NpcRank.SEASONED;
			// A monster that attacks from range (the thorned ampha, 37 m) hits without being anywhere near.
			bool targetRanged = targetTemplate != null && targetTemplate.GetAttackRange() > Navigation.NaturalCombatGeometry.MeleeReach + 1;
			long? lastHitByTargetMillis = null;
			var incomingAttackers = new HashSet<int>();
			// Fights may run long: a cornered Priest alternates heals and damage, and respawns or chain
			// aggro can keep adding monsters. The bound only stops a genuine stall (every action rejected
			// forever) from hanging the run; at a few seconds per action it is about an hour of game time.
			for (int turn = 0; turn < MaximumCombatActions; turn++)
			{
				trace.Add($"t{turn}:pre-sync hp={world.CurrentHp}/{world.MaxHp} mp={world.CurrentMp}/{world.MaxMp} " +
					$"dead={world.IsDead} pos={session.CurrentPosition}");
				await session.SynchronizeAsync(token);
				DecodedBotServerPacket[] recentPackets = session.PacketHistory.Skip(observedPacketCount).ToArray();
				int[] recentAttacks = recentPackets
					.Select(packet => runtime.IncomingAttacker(packet, session.CharacterId)).OfType<int>().ToArray();
				NaturalCombatRetreatPolicy.ObserveEngagement(incomingAttackers,
					recentPackets, session.CharacterId, LocalizedName, runtime.IsHostileSkill);
				bool targetReturned = NaturalCombatRetreatPolicy.TargetReturned(recentPackets,
					target, session.CharacterId, targetTemplate?.GetL10n(), runtime.IsHostileSkill);
				foreach (int attacker in recentAttacks)
				{
					if (attacker == target) lastHitByTargetMillis = runtime.NowMillis;
				}
				observedPacketCount = session.PacketHistory.Count;
				foreach (DecodedBotServerPacket status in session.PacketHistory.Skip(statusPacketCount)
					.Where(packet => packet.PacketType == typeof(SmAttackStatus) &&
						packet.Get<int>("objectId") == target))
					observedTargetHpPercent = status.Get<byte>("hpOrMp");
				statusPacketCount = session.PacketHistory.Count;
				int nearbyAttackers = incomingAttackers.Count(attacker =>
					world.Objects.TryGetValue(attacker, out BotKnownObject? attackerNpc) &&
					Distance(session.CurrentPosition, attackerNpc.Position) < 30);
				trace.Add($"t{turn}:post-sync hp={world.CurrentHp}/{world.MaxHp} mp={world.CurrentMp}/{world.MaxMp} dead={world.IsDead}");
				trace.Add($"t{turn}:client-observed-nearby-attackers={nearbyAttackers}");
				lastCombatTrace = trace.TakeLast(12).ToArray();
				if (world.IsDead || world.CurrentHp <= 0)
				{
					await ReviveAtBindAsync(token);
					return false; // The engaged corpse was not looted; reacquire from the client.
				}
				// Kill evidence: experience, a loot window, or the target's own status at 0% HP (a grey monster
				// pays no experience, so SM_STATUPDATE_EXP never comes; SM_DELETE follows the death status).
				if (world.CurrentExperience > startingExperience || world.LootStatuses.ContainsKey(target) ||
					observedTargetHpPercent == 0) return true;
				if (targetReturned)
				{
					TargetReturns++;
					session.TraceDiagnostic("combat-target-returned", new Dictionary<string, object?>
					{
						["targetObjectId"] = target, ["npcId"] = targetTemplate?.GetTemplateId(),
						["clientObservedKill"] = false,
					});
					return false; // Ordinary give-up, not a corpse: let the caller resume/re-plan.
				}
				if (!world.Objects.TryGetValue(target, out BotKnownObject? npc))
					return false; // Reacquire a new client-observed mob; do not count this as a kill.
				DateTimeOffset now = runtime.Epoch.AddMilliseconds(runtime.NowMillis);
				NaturalClassProfile profile = ClassProfile;
				// A monster that hit us in the last 3 s is in melee reach whatever its lagging client position says.
				bool targetAdjacent = profile.Movement.Adjacent(Distance(session.CurrentPosition, npc.Position), targetRanged,
					lastHitByTargetMillis is long lastHit ? runtime.NowMillis - lastHit : null);
				INaturalCombatPolicy policy = profile.Combat;
				if (world.CurrentHp * 100 <= world.MaxHp * policy.EmergencyEnterPercent(nearbyAttackers, targetSeasoned)) inEmergency = true;
				else if (world.CurrentHp * 100 >= world.MaxHp * policy.EmergencyExitPercent(nearbyAttackers, targetSeasoned)) inEmergency = false;
				IReadOnlySet<int> blessingIds = profile.EffectIds("blessing"), rejuvenationIds = profile.EffectIds("rejuvenation");
				bool hasBlessing = world.VisibleEffects?.Any(effect => blessingIds.Contains(effect.SkillId)) == true;
				BotInventoryItem? hotPotion = NaturalIshalgenPotionPolicy.SelectOwnedPotion(world.Inventory.Values);
				var hotTemplate = hotPotion == null ? null :
					runtime.Data.ItemDataDh.GetItemTemplate(hotPotion.ItemId);
				bool hotReady = hotTemplate != null && session.Api.Timing.TimeUntilItemUse(hotTemplate) == TimeSpan.Zero;
				NaturalHelpItemChoice? shieldChoice = profile.HelpItems.ShieldScroll(world.Level)
					? NaturalHelpItemPolicy.DecideShield(ObserveHelpItems(), now) : null;
				// NA-20a: the Cleric drinks its owned mana potions (the policy's mana-potion rule). CP-06: below level 10 the
				// Priest does too, from the 100 a starter owns.
				BotInventoryItem? manaPotion = profile.HelpItems.ManaPotion(world.Level)
					? NaturalIshalgenPotionPolicy.SelectOwnedManaPotion(world.Inventory.Values) : null;
				var manaTemplate = manaPotion == null ? null : runtime.Data.ItemDataDh.GetItemTemplate(manaPotion.ItemId);
				bool manaReady = manaTemplate != null && session.Api.Timing.TimeUntilItemUse(manaTemplate) == TimeSpan.Zero;
				// CP-39: the main-hand weapon's range and speed, from its tooltip, the chain the server's way and the
				// effects seen on the bot. NR-18: no rule reads HasBlessing and HasRejuvenation any more; they stay in the
				// observation because every recorded decision writes them.
				BotInventoryItem? mainHand = world.Inventory.Values.SingleOrDefault(item => item.Details.EquippedSlot is 1 or 3);
				var weaponStats = mainHand == null ? null : runtime.Data.ItemDataDh.GetItemTemplate(mainHand.ItemId)?.GetWeaponStats();
				// NR-04: a second weapon in the off hand adds a quarter of its own speed to the swing (Java
				// PlayerGameStats.getAttackSpeed), and the server refuses a swing that comes sooner.
				int offHandSwingMillis = world.Inventory.Values.SingleOrDefault(item => item.Details.EquippedSlot == 2) is { } offHand &&
					runtime.Data.ItemDataDh.GetItemTemplate(offHand.ItemId) is { } offTemplate && offTemplate.IsWeapon()
					? (offTemplate.GetWeaponStats()?.GetAttackSpeed() ?? 0) / 4 : 0;
				var observation = new NaturalCombatObservation(
					world.Level, world.CurrentHp, world.MaxHp, world.CurrentMp, world.MaxMp, world.IsDead,
					nearbyAttackers > 0 || recentAttacks.Length > 0,
					Distance(session.CurrentPosition, npc.Position), target, world.Skills, cooldowns,
					NearbyAggressors: nearbyAttackers, TargetHpPercent: observedTargetHpPercent,
					HasHealedThisFight: healedThisFight, HasHotPotion: hotPotion != null,
					HotPotionReady: hotReady,
					HotPotionActive: NaturalIshalgenPotionPolicy.HasActiveHealing(world.VisibleEffects),
					Cornered: cornered || ScriptedTrial, TargetAdjacent: targetAdjacent, InEmergency: inEmergency, HasBlessing: hasBlessing,
					TargetSeasoned: targetSeasoned, TargetRanged: targetRanged,
					ConservativeRangedHold: profile.HoldsAtRange(conservativeRangedHold),
					OpenChainCategory: tableChain.Current,
					OpenChainTargetId: tableChain.Target,
					Dp: world.CurrentDp,
					HasRejuvenation: world.VisibleEffects?.Any(effect => rejuvenationIds.Contains(effect.SkillId)),
					ShieldScrollReady: shieldChoice?.Item != null,
					HasManaPotion: manaPotion != null, ManaPotionReady: manaReady,
					LastCancelledSkillId: lastCancelledSkillId,
					WeaponAttackRangeMillis: weaponStats?.GetAttackRange(), WeaponAttackSpeedMillis: weaponStats?.GetAttackSpeed() + offHandSwingMillis,
					PreviousChainCategory: tableChain.Previous, ChainStepAt: tableChain.StepAt,
					OpenChainUseCount: tableChain.Current != null ? tableChain.UseCount : null,
					ActiveEffectSkillIds: world.VisibleEffects?.Select(effect => effect.SkillId).ToHashSet(),
					// NR-50b: a shield, or a second weapon or a two-hand weapon, for the skills that ask for one.
					OffHand: NaturalSkillCatalog.OffHandHeld(world.Inventory.Values, runtime.Data.ItemDataDh.GetItemTemplate),
					CastThisFight: castThisFight);
				NaturalCombatChoice choice = policy.Decide(observation, now, mauPolicy);
				NaturalCombatCandidate[] candidates = policy.CandidateActions(observation, now, choice, mauPolicy);
				if (!candidates.Any(candidate => candidate.Action == choice.Action &&
					candidate.SkillId == choice.Skill?.Id && candidate.Legal))
					throw new InvalidDataException($"Baseline chose an action absent from the legal candidate list: {choice.Action}/{choice.Skill?.Id}.");
				trace.Add($"t{turn}:action={choice.Action}/{choice.Skill?.Id} targetDistance={Distance(session.CurrentPosition, npc.Position):F1}");
				lastCombatTrace = trace.TakeLast(12).ToArray();
				session.TraceDiagnostic("combat-decision", new Dictionary<string, object?>
				{
					["encounterId"] = combatAttemptId,
					["turn"] = turn,
					["policyVersion"] = policy.PolicyVersion(mauPolicy),
					["seed"] = runtime.Seed,
					["observedState"] = new
					{
						observation.Level, observation.Hp, observation.MaxHp, observation.Mp, observation.MaxMp,
						observation.Dead, observation.Aggro, observation.TargetDistance, observation.TargetObjectId,
						learnedSkillIds = observation.Learned.Keys.Order().ToArray(),
						cooldowns = observation.Cooldowns.OrderBy(entry => entry.Key).Select(entry =>
							new { id = entry.Key, readyAt = entry.Value }).ToArray(),
						observation.NearbyAggressors, observation.TargetHpPercent, observation.HasHealedThisFight,
						observation.HasHotPotion, observation.HotPotionReady, observation.HotPotionActive,
						observation.Cornered, observation.TargetAdjacent, observation.InEmergency,
						observation.HasBlessing, observation.TargetSeasoned, observation.TargetRanged,
						observation.ConservativeRangedHold, observation.OpenChainCategory, observation.Dp,
						observation.HasRejuvenation, observation.LastCancelledSkillId,
					},
					["candidateActions"] = candidates,
					["action"] = choice.Action,
					["skillId"] = choice.Skill?.Id,
					["reason"] = choice.Reason,
					["checks"] = choice.Checks,
					["targetObjectId"] = target,
					["targetDistance"] = Distance(session.CurrentPosition, npc.Position),
					["hp"] = world.CurrentHp,
					["maxHp"] = world.MaxHp,
					["mp"] = world.CurrentMp,
					["observedAttackers"] = nearbyAttackers,
					["targetAdjacent"] = targetAdjacent,
					["inEmergency"] = inEmergency,
					["targetHpPercent"] = observedTargetHpPercent,
					["healedThisFight"] = healedThisFight,
					["hotPotionItemId"] = hotPotion?.ItemId,
					["hotPotionReady"] = hotReady,
					["hotPotionActive"] = NaturalIshalgenPotionPolicy.HasActiveHealing(world.VisibleEffects),
					["shieldScroll"] = shieldChoice?.Item?.ItemId,
				});
				switch (choice.Action)
				{
					case "mana-potion":
					{
						long before = ItemCount(world, manaPotion!.ItemId);
						await session.SendPacketAsync(session.Api.UseItem(manaPotion.ObjectId, manaTemplate!), token);
						await session.SynchronizeAsync(token);
						session.TraceDiagnostic("combat-mana-potion", new Dictionary<string, object?>
						{
							["itemId"] = manaPotion.ItemId, ["before"] = before, ["after"] = ItemCount(world, manaPotion.ItemId),
							["mp"] = world.CurrentMp,
						});
						if (ItemCount(world, manaPotion.ItemId) == before)
							await session.AdvanceAsync(TimeSpan.FromMilliseconds(1000), token); // refused (stunned): decide again
						break;
					}
					case "shield-scroll":
						if (!await UseHelpItemAsync(shieldChoice!.Item!, token))
							await session.AdvanceAsync(TimeSpan.FromMilliseconds(1000), token); // refused (stunned): decide again
						break;
					case "hot-potion":
					{
						if (hotPotion == null || hotTemplate == null)
							throw new InvalidDataException("Combat chose a potion absent from observed inventory.");
						long before = NaturalIshalgenPotionPolicy.Count(world.Inventory.Values, hotPotion.ItemId);
						int useStart = session.PacketHistory.Count;
						await session.SendPacketAsync(session.Api.UseItem(hotPotion.ObjectId, hotTemplate), token);
						await session.SynchronizeAsync(token);
						long after = NaturalIshalgenPotionPolicy.Count(world.Inventory.Values, hotPotion.ItemId);
						if (after == before && session.PacketHistory.Skip(useStart).Any(packet =>
							packet.PacketType == typeof(SM_SYSTEM_MESSAGE) &&
							packet.Get<object>("name") is "STR_SKILL_CAN_NOT_USE_ITEM_WHILE_IN_ABNORMAL_STATE"))
						{
							// Stunned or knocked down (Java PlayerRestrictions.canUseItem): the potion stays in the
							// bag. Wait for the state to wear off and decide again.
							session.TraceDiagnostic("combat-item-while-disabled", new Dictionary<string, object?>
							{
								["itemId"] = hotPotion.ItemId, ["hp"] = world.CurrentHp, ["position"] = session.CurrentPosition,
							});
							await session.AdvanceAsync(TimeSpan.FromMilliseconds(1000), token);
							break;
						}
						if (after != before - 1)
							throw new InvalidDataException($"Timed healing potion {hotPotion.ItemId} was not consumed: {before}->{after}.");
						session.TraceDiagnostic("combat-hot-potion", new Dictionary<string, object?>
						{
							["itemId"] = hotPotion.ItemId, ["before"] = before, ["after"] = after,
							["hp"] = world.CurrentHp, ["sharedUseDelayId"] = NaturalIshalgenPotionPolicy.SharedUseDelayId,
						});
						break;
					}
					case "cast-self": case "cast-target":
						TimeSpan clientCastWait = session.Api.Timing.TimeUntilCast(choice.Skill!.Id);
						if (clientCastWait > TimeSpan.Zero)
						{
							// A server-ready skill may still be inside the client's minimum cast interval.
							// Advance only a short slice, then re-observe HP and attackers before deciding again.
							TimeSpan pacingSlice = TimeSpan.FromMilliseconds(
								Math.Min(clientCastWait.TotalMilliseconds + 1, 350));
							trace.Add($"t{turn}:client-cast-gate skill={choice.Skill.Id} wait={clientCastWait.TotalMilliseconds:F0}ms");
							lastCombatTrace = trace.TakeLast(12).ToArray();
							session.TraceDiagnostic("combat-client-cast-gate", new Dictionary<string, object?>
							{
								["skillId"] = choice.Skill.Id,
								["waitMillis"] = clientCastWait.TotalMilliseconds,
								["hp"] = world.CurrentHp,
								["target"] = target,
							});
							await session.AdvanceAsync(pacingSlice, token);
							break;
						}
						if (!await CastAsync(choice.Skill!, choice.Action == "cast-self" ? session.CharacterId : target, token))
							return false;
						if (lastCastCompleted) castThisFight.Add(choice.Skill!.Id);
						// The fight has had its heal: the class's own heal, the one its rest names (NR-18).
						if (lastCastCompleted && choice.Action == "cast-self" && choice.Skill!.Role == profile.Rest.HealRole)
							healedThisFight = true;
						break;
					case "attack":
						await session.SendPacketAsync(session.Api.Target(target), token);
						// CP-39: swing at the weapon's own speed. Java PlayerController.attackTarget refuses a swing sooner
						// than the attack speed less 300 ms after the last, and allows 1 m more than the weapon's range; the
						// attack number is a byte and wraps.
						int interval = observation.WeaponAttackSpeedMillis is > 0 and int speed ? speed : 2500;
						await session.SendPacketAsync(session.Api.Attack(target, interval, (byte)(swings++ & 0xFF)), token);
						await session.AdvanceAsync(TimeSpan.FromMilliseconds(interval + 100), token);
						break;
					case "approach":
						if (EnterObservedMap != null && world.MapId is int observedMap && observedMap != ApproachMapId)
						{
							int madeFor = ApproachMapId;
							EnterObservedMap();
							session.TraceDiagnostic("combat-enters-observed-map", new Dictionary<string, object?>
							{
								["from"] = madeFor, ["to"] = ApproachMapId, ["targetObjectId"] = target,
							});
						}
						BotPosition destination = npc.Position;
						NaturalApproachStep step = profile.Movement.Approach(Distance(session.CurrentPosition, destination));
						if (step == NaturalApproachStep.Hold)
						{
							// CP-39: a weapon-range class already stands inside its hold distance. It does not walk in; the
							// clock runs so that cooldowns clear and the target's position is seen again.
							await session.AdvanceAsync(TimeSpan.FromMilliseconds(500), token);
							break;
						}
						if (step == NaturalApproachStep.RangedRoute)
						{
							NaturalEngageRanges ranges = profile.Ranges;
							IReadOnlyList<BotPosition> route = navigator.FindRangedApproach(
								session.CurrentPosition, destination, target, ranges.RangedApproachRadius, token);
							if (route.Count == 0)
								throw new NaturalCombatApproachBlockedException($"No checked spell-range approach to {npc.TemplateId}/{target} " +
									$"from {session.CurrentPosition}: {navigator.LastRouteDiagnostic}");
							IReadOnlyList<BotPosition> segment = NaturalCombatStandoff.NextSegment(route, destination,
								spellRange: ranges.StandoffSpellRange, arrivalTolerance: ranges.StandoffArrivalTolerance,
								safetyMargin: ranges.StandoffSafetyMargin);
							if (segment.Count == 0)
								throw new NaturalCombatApproachBlockedException($"No checked spell-range standoff for {npc.TemplateId}/{target}.");
							if (!navigator.IsSegmentSafe(segment, target))
								throw new NaturalCombatApproachBlockedException(
									$"A newly observed hostile blocked the checked approach to {npc.TemplateId}/{target}.");
							BotPosition approachStart = session.CurrentPosition;
							await navigator.MoveAsync(segment, token);
							await navigator.SynchronizeAsync(token);
							if (Distance(session.CurrentPosition, approachStart) < 0.5f)
								throw new NaturalCombatApproachBlockedException(
									$"Client position did not advance toward {npc.TemplateId}/{target}.");
							break;
						}
						long approachStarted = runtime.NowMillis;
						// CP-43a: a walk-in class goes to the target itself. The Priest line's approach, by the template's
						// nearest monster, stays as recorded. NR-53c: the movement rules say which a class does.
						NaturalNavigationResult approach = profile.Movement.GoesToItsTarget
							? await NaturalIshalgenNavigator.ApproachNpcObjectAsync(
								ApproachMapId, npc.TemplateId!.Value, target, destination, navigator, token)
							: await NaturalIshalgenNavigator.ApproachNpcAsync(
								ApproachMapId, npc.TemplateId!.Value, destination, navigator, token);
						if (!approach.Arrived && profile.Movement.GoesToItsTarget)
						{
							// CP-56a: the walk was planned against every other monster's circle, so a target that stands
							// inside its neighbour's circle is never reached. The walk-in planner already counts that
							// neighbour as a helper of the fight; plan the walk once more with the pack's circles left
							// out, unless the pack would bring the fight to the swarm limit. Every other circle stays.
							int[] pack = WalkInPack(target);
							bool accepted = pack.Length > 0 && profile.Combat is NaturalRotationCombatPolicy walkInTable &&
								pack.Length + 1 < walkInTable.SwarmAttackers;
							session.TraceDiagnostic("walk-in-accepts-pack", new Dictionary<string, object?>
							{
								["targetObjectId"] = target, ["pack"] = pack, ["accepted"] = accepted,
								["refused"] = approach.Reason, ["position"] = session.CurrentPosition,
							});
							if (accepted)
							{
								navigator.AcceptedPack = pack.ToHashSet();
								try
								{
									approach = await NaturalIshalgenNavigator.ApproachNpcObjectAsync(
										ApproachMapId, npc.TemplateId!.Value, target, destination, navigator, token);
								}
								finally { navigator.AcceptedPack = new HashSet<int>(); }
							}
						}
						if (!approach.Arrived)
							throw new NaturalCombatApproachBlockedException(approach.Reason);
						// Navigation can already be at a walking NPC's announced destination while combat still
						// sees its last reported position. Let motion and cooldowns advance before re-observing.
						if (runtime.NowMillis == approachStarted)
							await session.AdvanceAsync(TimeSpan.FromMilliseconds(350), token);
						break;
					case "wait":
						await session.AdvanceAsync(TimeSpan.FromMilliseconds(500), token);
						break;
				case "retreat":
						// Some quest pulls begin directly at an interacted object and have
						// no named refuge. Previously walked client positions remain valid
						// candidates, but each escape leg is checked against current mobs.
					if (await RetreatFromPackAsync(retreatAnchor ?? session.CurrentPosition,
						incomingAttackers.Append(target).ToHashSet(), token))
					{
						if (!session.Api.World.IsDead && session.Api.World.CurrentHp > 0) completedRetreats++;
						return false;
					}
						cornered = true; // No way out: stay and fight (heal, potions, then the target).
						break;
					default: throw new InvalidDataException($"Natural combat cannot act: {choice.Action}: {choice.Reason}");
				}
			}
			throw new InvalidDataException($"Natural {classLine.StarterName} exceeded {MaximumCombatActions} actions without a client-observed NPC kill.");

			string? LocalizedName(int id) => world.Objects.TryGetValue(id, out BotKnownObject? known) && known.TemplateId is int kind
				? runtime.Data.NpcDataDh.GetNpcTemplate(kind)?.GetL10n() : null;
		}

		/// <summary>Where an attacker was when the bot first saw it: SM_NPC_INFO arrives as the NPC enters view,
		/// normally at or near its spawn, so this estimates the home its give-up distance is measured from.</summary>
		private BotPosition? FirstSeen(int objectId)
		{
			DecodedBotServerPacket? info = session.PacketHistory.FirstOrDefault(packet =>
				packet.PacketType == typeof(SM_NPC_INFO) && packet.Get<int>("objectId") == objectId);
			return info == null ? null
				: new BotPosition(info.Get<float>("x"), info.Get<float>("y"), info.Get<float>("z"), 0);
		}

		/// <summary>
		/// Break away from a pack: always away from the attackers, never back through them. Candidates are
		/// previously walked ground, the refuge, and navmesh ground in rings around the bot, filtered to the
		/// half-plane away from the attackers and outside other observed circles, and ranked by distance
		/// from the attackers' homes (Java AttackManager.checkGiveupDistance). Keep running until every
		/// pursuer broadcasts its neutral/return emotion or leaves client sight. A gap in distance alone
		/// does not show that the NPC stopped chasing. New attackers join the same retreat.
		/// </summary>
		private async Task<bool> RetreatFromPackAsync(BotPosition refuge,
			IReadOnlySet<int> observedAttackers, CancellationToken token)
		{
			BotPosition origin = session.CurrentPosition;
			int map = session.Api.World.MapId ?? throw new InvalidDataException("Retreat map unobserved.");
			var activeAttackers = observedAttackers.ToHashSet();
			activeAttackers.RemoveWhere(id => !session.Api.World.Objects.ContainsKey(id));
			int packetStart = session.PacketHistory.Count;
			var tried = new List<BotPosition>();
			for (int replan = 0; replan < 24; replan++)
			{
				activeAttackers.RemoveWhere(id => !session.Api.World.Objects.ContainsKey(id));
				if (activeAttackers.Count == 0) return true;
				BotPosition[] attackerPositions = navigator.Observe().Npcs
					.Where(npc => activeAttackers.Contains(npc.ObjectId))
					.Select(npc => npc.Position).ToArray();
				BotPosition[] homes = activeAttackers
					.Select(id => FirstSeen(id) ?? navigator.Observe().Npcs.FirstOrDefault(npc => npc.ObjectId == id)?.Position)
					.OfType<BotPosition>().ToArray();
				BotNavigationHazard[] otherHazards = navigator.Observe().Npcs
					.Where(npc => !activeAttackers.Contains(npc.ObjectId))
					.Select(npc => (npc, template: runtime.Data.NpcDataDh.GetNpcTemplate(npc.TemplateId)))
					.Where(entry => runtime.IsAggressive(entry.template))
					.SelectMany(entry => entry.npc.Hazards(entry.template!.GetAggroRange(), BotPatrolPath.PassingReach)).ToArray();
				IEnumerable<BotPosition> candidates = navigator.Events
					.Where(item => item.Action == "segment-progress" && item.Position != null)
					.Select(item => item.Position!.Value).Append(refuge)
					.Concat(geometry.GroundAround(map, session.CurrentPosition, [45f, 75f, 110f, 150f]))
					.Where(p => !tried.Any(t => Distance(t, p) < 10) && geometry.OnSameIsland(map, session.CurrentPosition, p));
				BotPosition[] escapes = NaturalCombatRetreatPolicy.SelectEscape(session.CurrentPosition,
					attackerPositions, homes, candidates, otherHazards);
				IReadOnlyList<BotPosition> route = [];
				BotPosition? destination = null;
				var rejections = new List<string>();
				// NR-46a: every escape is searched from where the bot stands now, at this one instant. The searches that
				// find no way out walk the same ground; they share their edge traces. Nothing in this loop sends or waits.
				using (geometry.RememberEdges())
				foreach (BotPosition escape in escapes)
				{
					tried.Add(escape);
					// Avoid every other observed monster, not the chasers themselves: they move with the bot,
					// so their circles always surround it and would reject every escape.
					IReadOnlyList<BotPosition> candidate = geometry.FindJourneyPathAvoiding(map, session.CurrentPosition, escape, otherHazards);
					// A sideways turn around terrain is fine; reject only a departure that actually
					// takes the bot deeper into the pack before heading for the outward destination.
					string? why = candidate.Count == 0 ? $"no route ({BotNavMeshRouter.LastOutcome})"
						: Distance(session.CurrentPosition, candidate[^1]) < 10 ? "too short"
						: !NaturalCombatRetreatPolicy.ClearsPackOnDeparture(session.CurrentPosition, candidate, attackerPositions)
							? "route enters the pack" : null;
					if (why != null)
					{
						rejections.Add($"({escape.X:F0},{escape.Y:F0}) {why}");
						continue;
					}
					route = candidate;
					destination = escape;
					break;
				}
				if (destination == null)
				{
					session.TraceDiagnostic("combat-retreat-cornered", new Dictionary<string, object?>
					{
						["position"] = session.CurrentPosition,
						["observedAttackers"] = activeAttackers.ToArray(),
						["attackerPositions"] = attackerPositions,
						["rejections"] = rejections.ToArray(),
						["otherHazards"] = otherHazards.Length,
						["hp"] = session.Api.World.CurrentHp,
					});
					return false;
				}
				session.TraceDiagnostic("combat-retreat-route", new Dictionary<string, object?>
				{
					["origin"] = session.CurrentPosition,
					["destination"] = destination,
					["checkedPoints"] = route.Count,
					["observedAttackers"] = activeAttackers.ToArray(),
					["attackerHomes"] = homes,
					["fromNearestHome"] = homes.Length == 0 ? null : homes.Min(h => Distance(h, destination.Value)),
				});
				foreach (BotPosition[] segment in route.Chunk(2).Take(256))
				{
					BotNavigationHazard[] others = navigator.Observe().Npcs
						.Where(npc => !activeAttackers.Contains(npc.ObjectId))
						.Select(npc => (npc, template: runtime.Data.NpcDataDh.GetNpcTemplate(npc.TemplateId)))
						.Where(entry => runtime.IsAggressive(entry.template))
						.SelectMany(entry => entry.npc.Hazards(entry.template!.GetAggroRange(), BotPatrolPath.PassingReach)).ToArray();
					if (!BotNavigationGeometry.AvoidsHazards(session.CurrentPosition, segment, others)) break; // new monster ahead
					await navigator.MoveAsync(segment, token);
					await session.SynchronizeAsync(token);
					NaturalCombatRetreatPolicy.ObserveEngagement(activeAttackers,
						session.PacketHistory.Skip(packetStart), session.CharacterId, hostileSkill: runtime.IsHostileSkill);
					packetStart = session.PacketHistory.Count;
					if (session.Api.World.CurrentHp <= 0 || session.Api.World.IsDead)
					{
						await ReviveAtBindAsync(token);
						return true;
					}
					if (activeAttackers.Count == 0) return true;
				}
			}
			session.TraceDiagnostic("combat-retreat-cornered", new Dictionary<string, object?>
			{
				["position"] = session.CurrentPosition,
				["origin"] = origin,
				["observedAttackers"] = activeAttackers.ToArray(),
				["reason"] = "pursuers still engaged after 24 away-from-pack replans",
			});
			return false;
		}

		/// <summary>Shipped spawn points of hostile monsters on the map, each with its aggro range. Resting
		/// inside one means every respawn interrupts the rest, a rest/fight/rest loop that ends in death.</summary>
		public IReadOnlyList<BotNavigationHazard> HostileSpawns { get; set; } = [];

		/// <summary>Kept beyond each aggro range when choosing where to rest: Java's assist offset (2 m,
		/// <c>SUPPORT_RANGE_OFFSET</c>) plus room for a respawn to stand a little off its spot.</summary>
		public const float RestMargin = 5f;

		/// <summary>
		/// Before sitting down, move to the nearest ground that no respawning or observed monster can aggro
		/// from: outside every hostile spawn point's aggro range and every observed monster's, each plus
		/// <see cref="RestMargin"/>. Candidates are recently walked ground and navmesh ground in rings around
		/// the bot on its own island, nearest first, reached on a checked route that avoids observed monsters
		/// (and other spawn circles when there is a route that does). Stays put when already clear or when
		/// nothing within 150 m qualifies.
		/// </summary>
		private async Task MoveToRestSpotAsync(CancellationToken token)
		{
			if (session.Api.World.MapId is not int map) return;
			BotPosition here = session.CurrentPosition;
			BotNavigationHazard[] observed = navigator.Observe().Npcs
				.Select(npc => (npc, template: runtime.Data.NpcDataDh.GetNpcTemplate(npc.TemplateId)))
				.Where(entry => runtime.IsAggressive(entry.template))
				.SelectMany(entry => entry.npc.Hazards(entry.template!.GetAggroRange())).ToArray();
			BotNavigationHazard[] spawns = HostileSpawns.Where(spawn => Distance(spawn.Position, here) < 260).ToArray();
			BotNavigationHazard[] threats = [.. spawns, .. observed];
			bool Clear(BotPosition point) => threats.All(threat =>
				Horizontal(threat.Position, point) >= threat.Radius + RestMargin || MathF.Abs(threat.Position.Z - point.Z) > 15);
			if (Clear(here)) return;
			IEnumerable<BotPosition> walked = navigator.Events.AsEnumerable().Reverse()
				.Where(item => item.Action == "segment-progress" && item.Position != null)
				.Select(item => item.Position!.Value).Take(400);
			BotPosition[] candidates = walked.Concat(geometry.GroundAround(map, here, [15f, 25f, 40f, 60f, 85f, 115f, 150f], 24))
				.Where(point => Distance(point, here) <= 150 && Clear(point) && geometry.OnSameIsland(map, here, point))
				.OrderBy(point => Distance(point, here)).ToArray();
			int tried = 0;
			foreach (BotPosition spot in candidates)
			{
				if (++tried > 10) break;
				// Prefer a way there that also stays out of other respawn circles; fall back to observed monsters only.
				BotNavigationHazard[] passing = [.. observed, .. spawns.Where(spawn =>
					Horizontal(spawn.Position, here) >= spawn.Radius && Horizontal(spawn.Position, spot) >= spawn.Radius)];
				IReadOnlyList<BotPosition> route = geometry.FindJourneyPathAvoiding(map, here, spot, passing);
				if (route.Count == 0) route = geometry.FindJourneyPathAvoiding(map, here, spot, observed);
				if (route.Count == 0) continue;
				session.TraceDiagnostic("rest-relocate", new Dictionary<string, object?>
				{
					["from"] = here,
					["to"] = spot,
					["routePoints"] = route.Count,
					["candidates"] = candidates.Length,
					["spawnsNearby"] = spawns.Count(spawn => Horizontal(spawn.Position, here) < spawn.Radius + RestMargin),
				});
				foreach (BotPosition[] segment in route.Chunk(8))
				{
					if (!navigator.IsSegmentSafe(segment, null)) break; // something new ahead: rest where we are
					await navigator.MoveAsync(segment, token);
					await navigator.SynchronizeAsync(token);
					if (session.Api.World.IsDead) return;
				}
				return;
			}
			session.TraceDiagnostic("rest-relocate-none", new Dictionary<string, object?>
			{
				["position"] = here,
				["candidates"] = candidates.Length,
			});
		}

		private static float Horizontal(BotPosition a, BotPosition b) =>
			MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

		/// <summary>
		/// Keep the profile's upkeep buffs up between fights (for the Priest line the learned protection buff, Blessing
		/// of Guardianship, as the recorded human did). The client sees its own effects (SM_ABNORMAL_EFFECT); a buff is
		/// recast only when absent, off cooldown and affordable. The combat policy's own blessing rule never fires
		/// mid-pull, so this is where it happens.
		/// </summary>
		public async Task MaintainBuffsAsync(CancellationToken token)
		{
			BotWorldModel world = session.Api.World;
			NaturalClassProfile profile = ClassProfile;
			foreach (NaturalUpkeepBuff buff in profile.Upkeep)
			{
				if (world.IsDead || world.CurrentHp <= 0 || InCombat) return;
				NaturalPriestSkill? skill = NaturalPriestSkills.Best(buff.Role, world.Level, world.Skills, profile.Skills);
				if (skill == null) continue;
				IReadOnlySet<int> effectIds = profile.EffectIds(buff.Role);
				if (world.VisibleEffects?.Any(effect => effectIds.Contains(effect.SkillId)) == true) continue;
				DateTimeOffset now = runtime.Epoch.AddMilliseconds(runtime.NowMillis);
				if (cooldowns.TryGetValue(skill.CooldownId, out DateTimeOffset readyAt) && readyAt > now) continue;
				if (world.CurrentMp < skill.ManaCost) continue;
				TimeSpan gate = session.Api.Timing.TimeUntilCast(skill.Id);
				if (gate > TimeSpan.Zero) await session.AdvanceAsync(gate + TimeSpan.FromMilliseconds(1), token);
				session.TraceDiagnostic(buff.TraceKind, new Dictionary<string, object?>
				{
					["skillId"] = skill.Id,
					["mp"] = world.CurrentMp,
					["position"] = session.CurrentPosition,
				});
				await CastAsync(skill, session.CharacterId, token);
			}
		}

		public bool IsCleric => session.Api.World.Objects.GetValueOrDefault(session.CharacterId)?.PlayerClass ==
			PlayerClass.CLERIC.GetClassId();

		/// <summary>CP-27: the character is the class its line takes at Ascension.</summary>
		public bool IsLineSecondClass => classLine.Second is { } second &&
			session.Api.World.Objects.GetValueOrDefault(session.CharacterId)?.PlayerClass == second.GetClassId();

		/// <summary>The character as a class gate names it when it refuses.</summary>
		public string ObservedCharacter => $"level {session.Api.World.Level} " +
			NaturalJourneyIdentityRules.ClassName(session.Api.World.Objects.GetValueOrDefault(session.CharacterId)?.PlayerClass);

		/// <summary>NA-19: the client-observed state the help-item policy reads.</summary>
		private NaturalHelpItemObservation ObserveHelpItems(float travelMeters = 0, bool crossMap = false)
		{
			BotWorldModel world = session.Api.World;
			if (!ReferenceEquals(world.VisibleEffects, effectsSnapshot))
			{
				effectsSnapshot = world.VisibleEffects;
				effectsSeenAtMillis = runtime.NowMillis;
			}
			DateTimeOffset now = runtime.Epoch.AddMilliseconds(runtime.NowMillis);
			var counts = world.Inventory.Values.GroupBy(item => item.ItemId).ToDictionary(group => group.Key, group => group.Sum(item => item.Count));
			var delays = new Dictionary<int, DateTimeOffset>();
			foreach (NaturalHelpItem help in NaturalHelpItemPolicy.All.Where(help => counts.GetValueOrDefault(help.ItemId) > 0))
				if (runtime.Data.ItemDataDh.GetItemTemplate(help.ItemId) is { } template)
					delays[help.UseDelayId] = now + session.Api.Timing.TimeUntilItemUse(template);
			IReadOnlySet<BotBlockingActivity> blocking = session.Api.Timing.BlockingActivities;
			return new(world.Level, world.CurrentHp, world.MaxHp, world.IsDead || world.CurrentHp <= 0,
				blocking.Contains(BotBlockingActivity.Casting), blocking.Contains(BotBlockingActivity.Cutscene),
				Flying: false, Disabled: false, world.VisibleEffects, runtime.NowMillis - effectsSeenAtMillis, counts, delays,
				travelMeters, crossMap, world.CurrentDp,
				Catalog.Any(skill => skill.Role == "salvation" && world.Skills.ContainsKey(skill.Id)));
		}

		/// <summary>NA-19: the buff-ourself check. Class buffs as before (pre-pull, after rest); then, for the Cleric and
		/// (CP-06) for every character below level 10, the help scrolls the policy names, one at a time, each decision
		/// traced. With nothing owned it uses nothing.</summary>
		public async Task BuffOurselfAsync(NaturalHelpTrigger trigger, CancellationToken token, float travelMeters = 0, bool crossMap = false)
		{
			if (trigger is NaturalHelpTrigger.PrePull or NaturalHelpTrigger.AfterRest) await MaintainBuffsAsync(token);
			NaturalHelpItemRules help = ClassProfile.HelpItems;
			if (!help.ScrollUpkeep(session.Api.World.Level) || InCombat) return;
			for (int use = 0; use < 3; use++)
			{
				await session.SynchronizeAsync(token);
				DateTimeOffset now = runtime.Epoch.AddMilliseconds(runtime.NowMillis);
				NaturalHelpItemChoice choice = NaturalHelpItemPolicy.DecideBuffs(ObserveHelpItems(travelMeters, crossMap), now, trigger,
					help.SharedSlotFamily);
				session.TraceDiagnostic("buff-ourself", new Dictionary<string, object?>
				{
					["trigger"] = trigger.ToString(), ["itemId"] = choice.Item?.ItemId, ["reason"] = choice.Reason,
					["checks"] = choice.Checks.Select(check => $"{check.Rule}:{check.Verdict}:{check.Reason}").ToArray(),
				});
				if (choice.Item == null || !await UseHelpItemAsync(choice.Item, token)) return;
			}
		}

		/// <summary>Use one help scroll between casts; false when the server refused it (it stays in the bag).</summary>
		private async Task<bool> UseHelpItemAsync(NaturalHelpItem help, CancellationToken token)
		{
			BotWorldModel world = session.Api.World;
			BotInventoryItem? owned = world.Inventory.Values.Where(item => item.ItemId == help.ItemId && item.Count > 0)
				.OrderBy(item => item.ObjectId).FirstOrDefault();
			var template = runtime.Data.ItemDataDh.GetItemTemplate(help.ItemId);
			if (owned == null || template == null) return false;
			long before = ItemCount(world, help.ItemId);
			int useStart = session.PacketHistory.Count;
			await session.SendPacketAsync(session.Api.UseItem(owned.ObjectId, template), token);
			await session.SynchronizeAsync(token);
			long after = ItemCount(world, help.ItemId);
			session.TraceDiagnostic("help-item-used", new Dictionary<string, object?>
			{
				["itemId"] = help.ItemId, ["family"] = help.Family, ["before"] = before, ["after"] = after,
				["hp"] = world.CurrentHp, ["useDelayId"] = help.UseDelayId,
				["refused"] = session.PacketHistory.Skip(useStart).Where(packet => packet.PacketType == typeof(SM_SYSTEM_MESSAGE))
					.Select(packet => packet.Get<object>("name")?.ToString()).FirstOrDefault(),
			});
			return after == before - 1;
		}

		public async Task RestAsync(CancellationToken token)
		{
			// CP-17: the profile's rest rules decide each step; this loop observes and carries it out. For the Priest
			// line: heal while there is mana to spend. Sitting solely for missing HP leaves the Priest exposed to
			// respawns and patrols; reserve sitting for MP below half, then recover it to 80%.
			bool locatedForManaRest = false;
			bool recoveringMana = false;
			int quietIntervals = 0;
			for (int interval = 0; interval < MaximumCombatActions; interval++)
			{
				BotWorldModel world = session.Api.World;
				bool interrupted = false;
				if (world.IsDead || world.CurrentHp <= 0)
				{
					await ReviveAtBindAsync(token);
					return;
				}
				// CP-37: the potion plan's three observations; the Priest line's plan has none and reads none.
				BotInventoryItem? lifePotion = ClassProfile.Rest.PotionPlan == null ? null
					: NaturalIshalgenPotionPolicy.SelectOwnedPotion(world.Inventory.Values);
				var lifePotionTemplate = lifePotion == null ? null : runtime.Data.ItemDataDh.GetItemTemplate(lifePotion.ItemId);
				NaturalRestDecision rest = ClassProfile.Rest.Decide(new NaturalRestObservation(
					world.Level, world.CurrentHp, world.MaxHp, world.CurrentMp, world.MaxMp, recoveringMana, quietIntervals, world.Skills,
					cooldowns, world.Inventory.Values.GroupBy(item => item.ItemId).ToDictionary(group => group.Key, group => group.Sum(item => item.Count)),
					lastPowderSkill, runtime.Epoch.AddMilliseconds(runtime.NowMillis),
					LifePotionOwned: lifePotionTemplate != null,
					LifePotionReady: lifePotionTemplate != null && session.Api.Timing.TimeUntilItemUse(lifePotionTemplate) == TimeSpan.Zero,
					LifePotionHealing: lifePotionTemplate != null && NaturalIshalgenPotionPolicy.HasActiveHealing(world.VisibleEffects)));
				recoveringMana = rest.RecoveringMana;
				if (rest.ManaRecovered) locatedForManaRest = false;
				// NA-18 (OD-9): a Cleric rests with powder first. Sitting and Healing Light stay the fallback below.
				if (rest.PowderChoice is { } powder)
				{
					session.TraceDiagnostic("powder-rest-decision", new Dictionary<string, object?>
					{
						["action"] = powder.Action, ["skillId"] = powder.Skill?.Id, ["reason"] = powder.Reason,
						["hp"] = world.CurrentHp, ["maxHp"] = world.MaxHp, ["mp"] = world.CurrentMp, ["maxMp"] = world.MaxMp,
						["powder"] = ItemCount(world, NaturalClericSkills.LesserOdellaPowder),
					});
					if (rest is { Action: NaturalRestRules.Powder, Skill: { } restSkill })
					{
						TimeSpan gate = session.Api.Timing.TimeUntilCast(restSkill.Id);
						if (gate > TimeSpan.Zero) await session.AdvanceAsync(gate + TimeSpan.FromMilliseconds(1), token);
						int castStart = session.PacketHistory.Count;
						if (!await CastAsync(restSkill, session.CharacterId, token)) return;
						lastPowderSkill = restSkill.Id;
						await session.SynchronizeAsync(token);
						int[] hitBy = session.PacketHistory.Skip(castStart)
							.Select(packet => runtime.IncomingAttacker(packet, session.CharacterId)).OfType<int>().Distinct().ToArray();
						if (hitBy.Length > 0)
						{
							// The hit cancelled the cast: never cast or sit under attack, fight first.
							session.TraceDiagnostic("powder-rest-interrupted", new Dictionary<string, object?>
							{
								["skillId"] = restSkill.Id, ["attackers"] = hitBy, ["hp"] = world.CurrentHp,
							});
							await DefendDuringRestAsync(hitBy, castStart, token);
							locatedForManaRest = false;
						}
						continue;
					}
				}
				if (rest.Action == NaturalRestRules.Blocked) throw new InvalidDataException(rest.BlockedReason);
				if (rest.Action == NaturalRestRules.DrinkLifePotion)
				{
					// CP-37: an item use, so it resets no chain and needs no rest spot. A refusal (stunned) leaves the
					// potion in the bag; either way the next observation decides again.
					long before = ItemCount(world, lifePotion!.ItemId);
					await session.SendPacketAsync(session.Api.UseItem(lifePotion.ObjectId, lifePotionTemplate!), token);
					await session.SynchronizeAsync(token);
					long after = ItemCount(world, lifePotion.ItemId);
					session.TraceDiagnostic("rest-life-potion", new Dictionary<string, object?>
					{
						["itemId"] = lifePotion.ItemId, ["before"] = before, ["after"] = after,
						["hp"] = world.CurrentHp, ["maxHp"] = world.MaxHp, ["sharedUseDelayId"] = NaturalIshalgenPotionPolicy.SharedUseDelayId,
					});
					if (after != before - 1) await session.AdvanceAsync(TimeSpan.FromMilliseconds(1000), token);
					continue;
				}
				if (rest.Action is not (NaturalRestRules.SitForMana or NaturalRestRules.SitForHealth))
				{
					if (rest is { Action: NaturalRestRules.CastHeal, Skill: { } heal })
					{
						TimeSpan gate = session.Api.Timing.TimeUntilCast(heal.Id);
						if (gate > TimeSpan.Zero)
						{
							await session.AdvanceAsync(gate + TimeSpan.FromMilliseconds(1), token);
							await session.SynchronizeAsync(token);
							continue; // Reobserve HP and attackers after the cast gate.
						}
						session.TraceDiagnostic("between-fights-heal", new Dictionary<string, object?>
						{
							["skillId"] = heal.Id, ["hp"] = world.CurrentHp, ["maxHp"] = world.MaxHp,
							["mp"] = world.CurrentMp, ["maxMp"] = world.MaxMp,
						});
						if (!await CastAsync(heal, session.CharacterId, token)) return;
						await session.SynchronizeAsync(token);
						continue;
					}
					await BuffOurselfAsync(NaturalHelpTrigger.AfterRest, token);
					if (MaintainInventoryAsync is { } maintain)
						await maintain(token);
					return;
				}
				if (!locatedForManaRest)
				{
					await MoveToRestSpotAsync(token);
					locatedForManaRest = true;
				}
				// CP-42: the trace does not record the sit packet itself, so a sit for health says so. The mana sit of the
				// Priest line is traced as it always was, by what follows it.
				if (rest.Action == NaturalRestRules.SitForHealth)
					session.TraceDiagnostic("rest-sit-for-health", new Dictionary<string, object?>
					{
						["hp"] = world.CurrentHp, ["maxHp"] = world.MaxHp, ["quietSits"] = quietIntervals,
						["lifePotionOwned"] = lifePotion != null,
						["lifePotionReadyInMillis"] = lifePotionTemplate == null ? null
							: (long)session.Api.Timing.TimeUntilItemUse(lifePotionTemplate).TotalMilliseconds,
					});
				int attackHistoryStart = 0;
				NaturalRestOutcome outcome = await NaturalRestCadence.RunAsync(
					async (resting, waitToken) =>
					{
						await session.SendPacketAsync(session.Api.Rest(resting), waitToken);
						// Pilot's recorded client leaves 343-361 ms between STAND and its first move.
						// Keep the Priest in place until the stand-up animation completes for observers.
						if (!resting) await session.AdvanceAsync(TimeSpan.FromMilliseconds(350), waitToken);
					},
					async (duration, waitToken) =>
					{
						attackHistoryStart = session.PacketHistory.Count;
						await session.AdvanceAsync(duration, waitToken);
						await session.SynchronizeAsync(waitToken);
						int[] attackers = session.PacketHistory.Skip(attackHistoryStart)
							.Select(packet => runtime.IncomingAttacker(packet, session.CharacterId)).OfType<int>().ToArray();
						return new NaturalRestTick(world.IsDead || world.CurrentHp <= 0, attackers);
					},
					async (attackers, defendToken) =>
					{
						interrupted = true;
						await DefendDuringRestAsync(attackers, attackHistoryStart, defendToken);
					}, token);
				if (outcome == NaturalRestOutcome.Dead)
				{
					await ReviveAtBindAsync(token);
					return;
				}
				if (!interrupted) quietIntervals++;
				else locatedForManaRest = false;
				await session.SynchronizeAsync(token);
			}
			throw new InvalidDataException(NaturalRestRules.NotRecovered);
		}

		/// <summary>Fight the attackers that interrupted a sit or a powder cast (NA-18 shares it with the sit).</summary>
		private async Task DefendDuringRestAsync(IReadOnlyList<int> attackers, int attackHistoryStart, CancellationToken defendToken)
		{
			BotWorldModel world = session.Api.World;
			session.TraceDiagnostic("rest-interrupted-by-attack", new Dictionary<string, object?>
			{
				["attackers"] = attackers,
				["hp"] = world.CurrentHp,
				["position"] = session.CurrentPosition,
			});
			if (navigator.DefendOnAttackAsync is { } defend)
				await defend(attackers.ToArray(), navigator.LastMovementStart ?? session.CurrentPosition,
					attackHistoryStart, defendToken);
			// Never sit under attack: fight whatever is still on the Priest, nearest first. A fight that
			// ends in a retreat re-observes; attackers left more than 30 m behind are no longer a threat.
			for (int fight = 0; fight < MaximumCombatActions && !world.IsDead && world.CurrentHp > 0; fight++)
			{
				int[] remaining = attackers.Distinct()
					.Where(attacker => !navigator.UnavailableObjects.Contains(attacker) &&
						world.Objects.TryGetValue(attacker, out BotKnownObject? observed) &&
						Distance(session.CurrentPosition, observed.Position) < 30)
					.OrderBy(attacker => Distance(session.CurrentPosition, world.Objects[attacker].Position))
					.ToArray();
				if (remaining.Length == 2 && mauPolicy.PreferWoundedWhenTwoAttackers)
					remaining = remaining.OrderBy(attacker => PriorityForEngagedTarget(attacker,
						remaining.Length, session, preferWounded: true)).ToArray();
				if (remaining.Length == 0) break;
				session.TraceDiagnostic("rest-defend", new Dictionary<string, object?>
				{
					["attacker"] = remaining[0],
					["remaining"] = remaining,
					["hp"] = world.CurrentHp,
					["position"] = session.CurrentPosition,
				});
				if (await TryKillAsync(remaining[0], defendToken, session.CurrentPosition))
					navigator.UnavailableObjects.Add(remaining[0]);
			}
		}

		private async Task ReviveAtBindAsync(CancellationToken token)
		{
			if (stopOnDeath)
			{
				string nearby = string.Join(',', navigator.Observe().Npcs
					.Where(npc => Distance(npc.Position, session.CurrentPosition) < 30)
					.Select(npc => $"{npc.TemplateId}/{npc.ObjectId}@" +
						$"{Distance(npc.Position, session.CurrentPosition):F1}m"));
				session.TraceDiagnostic("stop-on-death", new Dictionary<string, object?>
				{
					["mapId"] = session.Api.World.MapId,
					["level"] = session.Api.World.Level,
					["position"] = session.CurrentPosition,
					["hp"] = session.Api.World.CurrentHp,
					["mp"] = session.Api.World.CurrentMp,
					["target"] = engagedTarget,
					["nearby"] = nearby,
					["recentCombatChoices"] = lastCombatTrace,
				});
				throw new InvalidDataException($"NI-07 stop-on-death: {session.CurrentStep}/" +
					$"{session.CurrentAction}, level={session.Api.World.Level}, target={engagedTarget}, " +
					$"position={session.CurrentPosition}, nearby={nearby}; " +
					$"trace={string.Join(" | ", lastCombatTrace)}; " +
					$"combatTrace={session.CombatTracePath}.");
			}
			DeathSpots.Add(session.CurrentPosition);
			// A 41-quest journey through Q2007's camps can die several times, as a player does; twenty deaths
			// is a finding worth stopping for, three was not.
			if (++revives > MaximumRevives)
			{
				string nearby = string.Join(", ", navigator.Observe().Npcs
					.Where(npc => Distance(npc.Position, session.CurrentPosition) < 30)
					.Select(npc => $"{npc.TemplateId}/{npc.ObjectId}:{Distance(npc.Position, session.CurrentPosition):F1}m"));
				throw new InvalidDataException($"Natural {classLine.StarterName} exceeded {MaximumRevives} ordinary bind revives; " +
					$"level={session.Api.World.Level}, " +
					$"target={engagedTarget}, position={session.CurrentPosition}, nearby={nearby}; " +
					$"trace={string.Join(" | ", lastCombatTrace)}.");
			}
			// BC-04 proved the ordinary learned self-revival option. Bregirun's die hook resets the quest to var 1;
			// revive here, then let the leg decision leave/re-enter through its real portals.
			if (session.Api.World.MapId == 320030000 && session.Api.World.ReviveOptions?.BySkill == true)
			{
				session.BeginStep($"bc-self-revive-{revives}", "accept-observed-learned-rebirth");
				await session.SendPacketAsync(session.Api.Revive(BotReviveType.Rebirth), token);
				await session.SynchronizeAsync(token);
				Require.True(!session.Api.World.IsDead, "Rebirth did not clear client-observed death.");
				session.TraceDiagnostic("quest-instance-self-revival", new Dictionary<string, object?> { ["revives"] = revives, ["position"] = session.CurrentPosition });
				await BuffOurselfAsync(NaturalHelpTrigger.AfterRevive, token);
				await RestAsync(token);
				return;
			}
			session.BeginStep($"ni07-bind-revive-{revives}", "accept-client-death-and-revive-at-bound-obelisk");
			if (!session.Api.World.IsDead)
			{
				await session.AdvanceAsync(TimeSpan.FromMilliseconds(500), token);
				await session.WaitForPacketAsync(typeof(SM_DIE), token);
			}
			// The operator's death rule (2026-10-06), for every leg and level: "Whenever you die, we res at an Obelisk and soul
			// heal", and "if we are in an instance, we need to res in the instance".
			// In an instance: where SM_DIE offers the instance revive, the client's revive button is that one (Java SM_DIE: 0 is the
			// bind revive, anything else the instance revive). The server puts the player at the instance's own point, a spawn on
			// the same map with no world entry. There is no obelisk and no Soul Healer there, so the soul healing waits for the
			// next obelisk resurrection.
			int diedOnMap = session.Api.World.MapId ?? 0;
			if (session.Api.World.ReviveOptions?.InInstance == true)
			{
				session.BeginStep($"instance-revive-{revives}", "accept-client-death-and-revive-inside-the-instance");
				session.Api.World.BeginWorldReload();
				await session.SendPacketAsync(session.Api.Revive(BotReviveType.Instance), token);
				await session.WaitForPacketAsync(typeof(SM_CHANNEL_INFO), token);
				await session.SynchronizeAsync(token);
				if (session.Api.World.IsDead) throw new InvalidDataException("Instance revive did not clear client-observed death.");
				session.AcceptTeleportPosition();
				if (session.Api.World.MapId == diedOnMap)
				{
					instanceRevives++;
					(AfterInstanceRevive ?? AfterBindRevive)?.Invoke();
					session.TraceDiagnostic("instance-revive", new Dictionary<string, object?>
					{
						["map"] = session.Api.World.MapId, ["position"] = session.CurrentPosition, ["hp"] = session.Api.World.CurrentHp,
						["maxHp"] = session.Api.World.MaxHp, ["mp"] = session.Api.World.CurrentMp, ["maxMp"] = session.Api.World.MaxMp, ["revives"] = revives,
						["recoverableExperience"] = session.Api.World.RecoverableExperience,
					});
					await BuffOurselfAsync(NaturalHelpTrigger.AfterRevive, token);
					await RestAsync(token);
					return;
				}
				// Java's instance revive sends the player to the bind point when the instance has no start position
				// (PlayerReviveService.instanceRevive): that is an obelisk resurrection, and the rule's soul healing follows.
			}
			else
			{
				// Java TeleportService.sendLoc despawns the player before rebuilding its known list, even on the
				// same map. BC-06 saw a pre-death hunter survive in the bot view after a fortress bind revive;
				// its server object was gone when the bot returned. Drop that view before the new spawn packets.
				int? bindMap = session.Api.World.ObeliskBindPoint?.MapId;
				bool otherMap = bindMap is int bound && bound != session.Api.World.MapId;
				// RC-11: Leg 8 retained a pre-revive assassin that the server no longer knew.
				// Every bind revive rebuilds the known list, including on the same map.
				session.Api.World.BeginWorldReload();
				await session.SendPacketAsync(session.Api.Revive(BotReviveType.Bind), token);
				if (otherMap)
				{
					await session.WaitForPacketAsync(typeof(SM_PLAYER_SPAWN), token, packet => packet.Get<int>("worldId") == bindMap);
					await session.WaitForPacketAsync(typeof(SM_PLAYER_INFO), token, packet => packet.Get<int>("objectId") == session.CharacterId);
				}
				else
					await session.WaitForPacketAsync(typeof(SM_CHANNEL_INFO), token);
				await session.SynchronizeAsync(token);
				if (session.Api.World.IsDead) throw new InvalidDataException("Bind revive did not clear client-observed death.");
				session.AcceptTeleportPosition();
			}
			bindRevives++;
			AfterBindRevive?.Invoke();
			await SoulHealAtTheNearestSoulHealerAsync(token);
			await BuffOurselfAsync(NaturalHelpTrigger.AfterRevive, token);
			// A bind revive leaves a quarter of HP and MP (and soul sickness lowers the maximum). Rest at the obelisk before
			// anything else, as a player does: in Leg 4 the walk back out at 25% HP met three swamp mosbears and died three
			// more times in five minutes, because RestAsync returned straight after the revive that it ran.
			session.TraceDiagnostic("rest-after-revive", new Dictionary<string, object?>
			{
				["hp"] = session.Api.World.CurrentHp, ["maxHp"] = session.Api.World.MaxHp,
				["mp"] = session.Api.World.CurrentMp, ["maxMp"] = session.Api.World.MaxMp, ["revives"] = revives,
			});
			await RestAsync(token);
		}

		/// <summary>Walk up to 8 m toward the target's last-known position (never nearer than 10 m), on a
		/// checked route that stays out of other observed circles.</summary>
		/// <param name="reach">How close to get: melee reach for a melee skill, spell range otherwise.</param>
		private async Task CloseInAfterRangeRejectionAsync(int target, CancellationToken token, float reach = 10f)
		{
			if (!session.Api.World.Objects.TryGetValue(target, out BotKnownObject? observed) ||
				session.Api.World.MapId is not int map) return;
			BotPosition start = session.CurrentPosition;
			// A walker is where its last SM_MOVE was heading, not where that move began.
			BotPosition settled = observed.SettledPosition;
			float distance = Distance(start, settled);
			if (distance <= reach) return;
			float t = MathF.Min(12, distance - reach) / distance;
			BotPosition? goal = geometry.SnapToGround(map, start with
			{
				X = start.X + (settled.X - start.X) * t,
				Y = start.Y + (settled.Y - start.Y) * t,
			});
			IReadOnlyList<BotPosition> route = goal == null ? [] : geometry.FindLocalPath(map, start, goal.Value);
			bool safe = route.Count > 0 && navigator.IsSegmentSafe(route, target);
			bool detour = false;
			if (!safe)
			{
				route = navigator.FindCastRecoveryRoute(start, settled, target, token);
				safe = route.Count > 0 && navigator.IsSegmentSafe(route, target);
				detour = safe;
				if (safe) goal = route[^1];
			}
			session.TraceDiagnostic("combat-range-close-in", new Dictionary<string, object?>
			{
				["targetObjectId"] = target,
				["from"] = start,
				["goal"] = goal,
				["clientTargetDistance"] = distance,
				["routePoints"] = route.Count,
				["safe"] = safe,
				["detour"] = detour,
			});
			if (!safe) return;
			await navigator.MoveAsync(route, token);
			await navigator.SynchronizeAsync(token);
		}

		/// <summary>
		/// CP-56a: the monsters that join a fight at the target's own position, as the walk-in planner counts them
		/// (<see cref="NaturalPullPlanner.AddsAt"/>: the server's support rule and every circle that reaches the spot).
		/// </summary>
		private int[] WalkInPack(int targetObjectId)
		{
			NaturalPullMonster? Monster(NaturalNavigationObject npc) =>
				runtime.Data.NpcDataDh.GetNpcTemplate(npc.TemplateId) is { } template && runtime.IsAggressive(template)
					? new(npc, template.GetAggroRange(), template.GetTribe().ToString(), template.GetBoundRadius().GetMaxOfFrontAndSide())
					: null;
			IReadOnlyList<NaturalNavigationObject> observed = navigator.Observe().Npcs;
			if (observed.FirstOrDefault(npc => npc.ObjectId == targetObjectId) is not { } targetNpc || Monster(targetNpc) is not { } target)
				return [];
			NaturalPullMonster[] monsters = observed.Where(npc => npc.ObjectId != targetObjectId)
				.Select(Monster).OfType<NaturalPullMonster>().ToArray();
			bool CanSupport(string helper, string asking) =>
				Enum.TryParse(helper, out Aion.GameServer.Model.TribeClass h) && Enum.TryParse(asking, out Aion.GameServer.Model.TribeClass a) &&
				runtime.Data.TribeRelations.CanSupport(h, a);
			return NaturalPullPlanner.AddsAt(target, targetNpc.Position, monsters, CanSupport,
					(a, b) => geometry.HasLineOfSight(ApproachMapId, a, b), ClassProfile.Ranges.MeleeReach)
				.Select(add => add.Npc.ObjectId).Order().ToArray();
		}

		private async Task<bool> CastAsync(NaturalPriestSkill skill, int target, CancellationToken token)
		{
			lastCastCompleted = false;
			BotSkill learned = session.Api.World.Skills[skill.Id];
			// AM-06: Java Skill.useSkill resets the player's chain when a skill without a chain category is cast, so a
			// Light of Rejuvenation between Smite and Flashbolt breaks the chain and the server silently refuses Flashbolt.
			tableChain = tableChain.CastSent(skill);
			await session.SendPacketAsync(session.Api.Target(target), token);
			await session.SendPacketAsync(session.Api.Cast(runtime.CreateSpellCast(session.Api.World,
				session.CurrentPosition, skill.Id, checked((byte)learned.Level), target)), token);
			DecodedBotServerPacket started;
			try
			{
				started = await BotCastProtocol.WaitForStartAsync(
					(predicate, waitToken) => session.WaitForPacketAsync(packet => predicate(packet) ||
						BotCastProtocol.IsStartRejection(packet), waitToken),
					session.CharacterId, skill.Id, token);
			}
			catch (TimeoutException exception)
			{
				// The cast never started: release the local casting gate, or the next walk is refused ("CM_MOVE is blocked
				// while Casting") even when the caller recovers from this.
				session.Api.Timing.RecordCastCancelled();
				string[] recent = session.PacketHistory.TakeLast(15).Select(packet =>
					packet.PacketType == typeof(SM_SYSTEM_MESSAGE)
						? $"{packet.PacketType.Name}:{packet.Get<object>("name") ?? packet.Get<int>("msgId")}" : packet.PacketType.Name).ToArray();
				throw new InvalidDataException($"Cast {skill.Id} on {target} had no start; " +
					$"HP={session.Api.World.CurrentHp}/{session.Api.World.MaxHp}, " +
					$"MP={session.Api.World.CurrentMp}/{session.Api.World.MaxMp}, " +
					$"position={session.CurrentPosition}; packets={string.Join(',', recent)}.", exception);
			}
			if (started.PacketType == typeof(SM_SYSTEM_MESSAGE))
			{
				// A rejected cast never starts on the server; release the bot's local
				// casting gate before any legal movement or retry.
				session.Api.Timing.RecordCastCancelled();
				if (started.Get<object>("name") is "STR_SKILL_NOT_READY")
				{
					// Java CM_CASTSPELL can refuse the global animation gate before any cast
					// starts. Wait a short slice, then let combat re-observe HP and attackers.
					// Do not synthesize a result, cooldown, chain or successful cast.
					int rejection = ++readinessRejections;
					session.TraceDiagnostic("combat-cast-not-ready", new Dictionary<string, object?>
					{
						["skillId"] = skill.Id, ["targetObjectId"] = target,
						["rejection"] = rejection, ["castStarted"] = false,
						["hp"] = session.Api.World.CurrentHp, ["position"] = session.CurrentPosition,
					});
					if (rejection > 8)
						throw new InvalidDataException($"Cast {skill.Id} remains not ready after {rejection} consecutive refusals.");
					await session.AdvanceAsync(TimeSpan.FromMilliseconds(BotTimingContract.MinimumCastIntervalMillis + 1), token);
					await session.SynchronizeAsync(token);
					return true;
				}
				if (started.Get<object>("name") is "STR_SKILL_NOT_ENOUGH_DISTANCE")
				{
					int rejection = ++rangeRejections;
					session.TraceDiagnostic("combat-range-rejected", new Dictionary<string, object?>
					{
						["targetObjectId"] = target,
						["skillId"] = skill.Id,
						["rejection"] = rejection,
						["clientTargetDistance"] = session.Api.World.Objects.TryGetValue(target,
							out BotKnownObject? observed) ? Distance(session.CurrentPosition, observed.Position) : null,
					});
					if (rejection > 3) return false; // Reacquire another observed guard, not an infinite stale pull.
					// The server measured more than the skill's range although the client's last-known
					// position says otherwise: a walker moved on without a fresh SM_MOVE. Close in along
					// checked ground, as a player walks toward a target that drifted out of range.
					// CP-48: a skill that adds the weapon's range reaches as far as the weapon does; it is not a melee skill
					// for having no range of its own. A row without that flag keeps its own range, as recorded.
					float reach = !skill.AddWeaponRange ? skill.Range : NaturalSkillCatalog.Reach(skill,
						session.Api.World.Inventory.Values.SingleOrDefault(item => item.Details.EquippedSlot is 1 or 3) is { } mainHand
							? runtime.Data.ItemDataDh.GetItemTemplate(mainHand.ItemId)?.GetWeaponStats()?.GetAttackRange() : null);
					await CloseInAfterRangeRejectionAsync(target, token, ClassProfile.Movement.CloseInAfterRangeRefusal(reach));
					await session.AdvanceAsync(TimeSpan.FromMilliseconds(300), token);
					await session.SynchronizeAsync(token);
					return true; // Re-evaluate range, health and attackers before retrying.
				}
				if (started.Get<object>("name") is "STR_SKILL_OBSTACLE" &&
					session.Api.World.Objects.TryGetValue(target, out BotKnownObject? obstructedTarget) &&
					++obstacleRepositions <= 2)
				{
					// The server sees something between us that the bot's geometry does not (a hut wall, a fence,
					// the nest rim). A priest fights at melee anyway: close in to the target along checked ground
					// and cast from there rather than hunting for another spot with the same blind geometry.
					session.TraceDiagnostic("combat-obstacle-close-in", new Dictionary<string, object?>
					{
						["targetObjectId"] = target,
						["skillId"] = skill.Id,
						["reposition"] = obstacleRepositions,
						["clientTargetDistance"] = Distance(session.CurrentPosition, obstructedTarget.SettledPosition),
					});
					NaturalFightMovement movement = ClassProfile.Movement;
					if (movement.AfterObstacleRefusal != NaturalObstacleAnswer.CloseToMelee)
						throw new NotSupportedException($"The {movement.Style} answer to an obstacle has no executor yet (CP-48).");
					await CloseInAfterRangeRejectionAsync(target, token, movement.ObstacleCloseIn);
					await session.AdvanceAsync(TimeSpan.FromMilliseconds(300), token);
					await session.SynchronizeAsync(token);
					return true; // Re-evaluate the visible target and healing state before recasting.
				}
				if (started.Get<object>("name") is "STR_SKILL_OBSTACLE") return false;
				if (started.Get<object>("name") is "STR_SKILL_CAN_NOT_ATTACK_WHILE_IN_ABNORMAL_STATE" or
					"STR_SKILL_CANT_CAST_MAGIC_SKILL_WHILE_SILENCED")
				{
					// Stun/knockdown or silence is an ordinary refusal. Let the clock/effects advance,
					// then re-evaluate health, consumables and the target rather than waiting for a cast.
					session.TraceDiagnostic("combat-cast-while-disabled", new Dictionary<string, object?>
					{
						["skillId"] = skill.Id,
						["reason"] = started.Get<object>("name"),
						["hp"] = session.Api.World.CurrentHp,
						["position"] = session.CurrentPosition,
					});
					await session.AdvanceAsync(TimeSpan.FromMilliseconds(1000), token);
					await session.SynchronizeAsync(token);
					return true;
				}
				if (session.Api.World.CurrentHp <= 0 || session.Api.World.IsDead)
				{
					await ReviveAtBindAsync(token);
					return false;
				}
				if (target != session.CharacterId && started.Get<object>("name") is "STR_SKILL_TARGET_IS_NOT_VALID")
				{
					// NR-48: the server refuses a skill on a dead target with this message (Java Skill.java, canUseSkill:
					// target.isDead()). The monster still stands in the client's view because another player killed it:
					// its death reaches this client as an emotion, and its corpse is only removed later. Alone, a bot is
					// the only one that kills. Leave the corpse to its owner and let the caller look for another target.
					TargetsTaken++;
					navigator.UnavailableObjects.Add(target);
					session.TraceDiagnostic("combat-target-taken", new Dictionary<string, object?>
					{
						["targetObjectId"] = target,
						["skillId"] = skill.Id,
						["npcId"] = session.Api.World.Objects.TryGetValue(target, out BotKnownObject? taken) ? taken.TemplateId : null,
						["position"] = session.CurrentPosition,
					});
					return false;
				}
				throw new InvalidDataException($"Cast {skill.Id} rejected: {started.Get<object>("name")}.");
			}
			readinessRejections = 0;
			// AG-07: Java ChainCondition.shouldReset clears the chain when an opener starts while its own chain is used up (Smite's
			// selfcount is 1), and only a completed cast opens it again (Skill.endCast). A Smite cut short (STR_SKILL_CANCELED)
			// leaves no chain, and the server refuses the Flashbolt after it without a word (the Leg 6 smoke run).
			tableChain = tableChain.CastStarted(skill);
			await session.AdvanceAsync(TimeSpan.FromMilliseconds(started.Get<ushort>("castDuration") + 1), token);
			DecodedBotServerPacket result = await BotCastProtocol.WaitForCompletionAsync(
				session.WaitForPacketAsync, session.CharacterId, skill.Id, token);
			lastCancelledSkillId = result.PacketType == typeof(SM_SKILL_CANCEL) ? skill.Id : null;
			if (result.PacketType == typeof(SM_CASTSPELL_RESULT))
			{
				lastCastCompleted = true;
				int deciseconds = result.Get<int>("cooldown");
				if (deciseconds > 0)
					cooldowns[skill.CooldownId] = runtime.Epoch.AddMilliseconds(runtime.NowMillis + deciseconds * 100L);
				// NA-18: flag 32 is a successful chain step (Java SM_CASTSPELL_RESULT); anything else resets it.
				tableChain = tableChain.CastCompleted(skill, target, target == session.CharacterId, (result.Get<byte>("flags") & 32) != 0,
					runtime.Epoch.AddMilliseconds(runtime.NowMillis));
			}
			TimeSpan recovery = BotCastProtocol.RecoveryDelay(result);
			// CP-48: the animation's last hit is waited out as well, as a client would: after Gunshot from 18 m the bullet
			// lands 785 ms after the cast and the server takes no next skill for 819 ms.
			if (result.PacketType == typeof(SM_CASTSPELL_RESULT) &&
				TimeSpan.FromMilliseconds(runtime.AnimationLastHitMillis(session.Api.World, skill.Id) + 1) is var lastHit && lastHit > recovery)
				recovery = lastHit;
			await session.AdvanceAsync(recovery, token);
			return true;
		}
	}
}
