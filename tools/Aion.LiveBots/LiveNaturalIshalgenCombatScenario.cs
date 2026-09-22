using System.Xml;
using System.Xml.Serialization;
using Aion.Bots.Api;
using Aion.Bots.Movement;
using Aion.Bots.Navigation;
using Aion.Bots.Protocol;
using Aion.Bots.Reflexes;
using Aion.Bots.Scenarios;
using Aion.Bots.Timing;
using Aion.Bots.World;
using Aion.GameServer.Model;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.LiveBots;

public static partial class LiveBotRunner
{
	private static async Task<int> RunNaturalIshalgenCombatAsync(LiveBotOptions options,
		LiveBotProblemWriter problems, CancellationToken token)
	{
		NaturalIshalgenIdentity identity = NaturalIshalgenIdentityScenario.Identity;
		await using var actor = new L0Actor(options, problems, 1, Race.ASMODIANS, bot: "b01",
			account: identity.AccountName, characterName: identity.CharacterName);
		actor.Trace.WriteAction("s00", "scenario:start", new Dictionary<string, object?> { ["scenario"] = "NI-04" });
		try
		{
			var identityDriver = new LiveNaturalIshalgenIdentityDriver(options, actor, identity);
			NaturalIshalgenIdentityResult subject = await NaturalIshalgenIdentityScenario.EnterAsync(identityDriver, token);
			await WriteNaturalIdentityReceiptAsync(options, identity, subject, token);
			string root = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ScenarioManifest.FindDefaultPath())!, "../.."));
			BotNavigationAssets assets = await BotNavigationAssets.LoadAsync(root,
				Path.Combine(options.OutputDirectory, "navigation-cache"), token);
			int channel = actor.Session.Api.World.ChannelInfo?.Index ?? 0;
			actor.Session.Navigation = assets.StarterRoute(Race.ASMODIANS, channel + 1);
			var driver = new NaturalPriestLiveDriver(options, actor, root);
			await actor.StepAsync("natural-priest-two-kills-and-recovery", driver.ProveAsync, token);
			if (options.DecisionViewSeconds > 0)
				await Task.Delay(TimeSpan.FromSeconds(options.DecisionViewSeconds), token);
			await actor.StepAsync("quit-without-deleting-character", actor.Session.QuitAsync, token);
			actor.Trace.WriteAction(actor.LastStep, "scenario:complete", new Dictionary<string, object?>
			{
				["scenario"] = "NI-04", ["kills"] = driver.Kills, ["characterId"] = subject.CharacterId,
			});
			Console.WriteLine($"LIVE NI-04: retained Priest {identity.CharacterName} completed {driver.Kills} ordinary Sprigg kills and recovered between pulls.");
			return 0;
		}
		catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
		catch (Exception exception)
		{
			Console.Error.WriteLine($"NI-04 failed: {exception}");
			return 1;
		}
	}

	private sealed class NaturalPriestLiveDriver(LiveBotOptions options, L0Actor actor, string root)
	{
		private const int SpriggWorker = 210363;
		private const int LifePotion = 162000002;
		private const int ManaPotion = 162000007;
		private const int SharedPotionCooldown = 11; // Both shipped starter potions use usedelayid=11.
		private readonly HashSet<int> defeated = [];
		private readonly Dictionary<int, DateTimeOffset> cooldowns = [];
		private readonly Dictionary<int, DateTimeOffset> itemCooldowns = [];
		private readonly BotMotionTiming motion = BotMotionTiming.Load(Path.Combine(root,
			"game-server/data/static_data/skills/motion_times.xml"));
		private readonly Dictionary<int, SkillTemplate> templates = LoadTemplates(root);
		private int decisionSequence;
		private int deaths;
		private int retreats;
		private bool aggro;
		private string? chain;
		private int? chainTarget;
		private DateTimeOffset? chainExpires;
		public int Kills { get; private set; }

		public async Task ProveAsync(CancellationToken token)
		{
			LiveBotSession session = actor.Session;
			BotWorldModel world = session.Api.World;
			if (world.MapId != 220010000 || world.Level is < 1 or > 9 || world.IsDead)
				throw new InvalidDataException("NI-04 requires a living pre-Ascension Priest in Ishalgen.");
			if (!world.Skills.ContainsKey(1838) || !world.Skills.ContainsKey(4012))
				throw new InvalidDataException("The client did not observe the Priest's starting heal and Smite.");
			DateTimeOffset enteredAt = TimeProvider.System.GetUtcNow();
			// SM_ITEM_COOLDOWN is not yet a structured bot observation. Both starter potions
			// have a 30-second shared delay, so wait that full bound after reentry.
			itemCooldowns[SharedPotionCooldown] = enteredAt.AddSeconds(30);
			foreach (BotSkillCooldown saved in world.Cooldowns.Values)
			{
				NaturalPriestSkill? known = NaturalPriestSkills.All.FirstOrDefault(skill => skill.Id == saved.SkillId);
				if (known != null && saved.RemainingSeconds > 0)
					cooldowns[known.CooldownId] = enteredAt.AddSeconds(saved.RemainingSeconds);
			}
			for (int kill = 0; kill < 2; kill++)
			{
				await RecoverAsync(token, forceRest: kill > 0);
				int target = await SelectSpriggAsync(token);
				if (aggro && Distance(session.CurrentPosition, world.Objects[target].Position) > 8)
					throw new InvalidDataException("Rest was interrupted by damage, but no nearby Sprigg aggressor is identifiable.");
				await session.MoveToNpcAsync(target, token);
				await session.SynchronizeAsync(token);
				long startExperience = world.CurrentExperience;
				bool revived = false;
				for (int turn = 0; turn < 24; turn++)
				{
					if (world.IsDead) { await ReviveAsync(token); revived = true; break; }
					if (world.CurrentExperience > startExperience || world.LootStatuses.ContainsKey(target))
					{
						actor.Trace.WriteAction(actor.LastStep, "natural:kill-observed", new Dictionary<string, object?>
						{
							["targetObjectId"] = target, ["experienceBefore"] = startExperience,
							["experienceAfter"] = world.CurrentExperience,
							["lootStatusObserved"] = world.LootStatuses.ContainsKey(target),
						});
						defeated.Add(target); Kills++; aggro = false; break;
					}
					if (!world.Objects.TryGetValue(target, out BotKnownObject? npc))
						throw new InvalidDataException("Engaged target disappeared without kill or loot evidence.");
					NaturalCombatChoice choice = Decide(target, npc.Position);
					Record(choice);
					switch (choice.Action)
					{
						case "cast-self": case "cast-target": await CastAsync(choice.Skill!, target, choice.Action == "cast-self", token); break;
						case "life-potion": await UsePotionAsync(LifePotion, token); break;
						case "mana-potion": await UsePotionAsync(ManaPotion, token); break;
						case "attack":
							await session.SendPacketAsync(session.Api.Target(target), token);
							// A deliberately slow fallback remains legal even after NI-05 equips a slower weapon.
							await session.SendPacketAsync(session.Api.Attack(target, 2500, checked((byte)turn)), token);
							await Task.Delay(TimeSpan.FromMilliseconds(2600), token);
							break;
						case "approach": await session.MoveToNpcAsync(target, token); break;
						case "retreat": await RetreatAsync(npc.Position, token); break;
						default: throw new InvalidDataException($"Combat policy could not act: {choice.Action}: {choice.Reason}");
					}
					int before = world.CurrentHp;
					await session.SynchronizeAsync(token);
					aggro = world.CurrentHp < before;
				}
				if (revived) { kill--; continue; }
				if (Kills != kill + 1) throw new InvalidDataException("NI-04 exceeded the bounded action budget without a proven kill.");
			}
			await RecoverAsync(token, forceRest: true);
		}

		private NaturalCombatChoice Decide(int? target, BotPosition? targetPosition)
		{
			BotWorldModel world = actor.Session.Api.World;
			DateTimeOffset now = TimeProvider.System.GetUtcNow();
			float? distance = targetPosition is BotPosition position ? Distance(actor.Session.CurrentPosition, position) : null;
			bool? blessing = world.VisibleEffects?.Any(effect => effect.SkillId == 1684);
			return NaturalPriestCombatPolicy.Decide(new NaturalCombatObservation(world.Level, world.CurrentHp,
				world.MaxHp, world.CurrentMp, world.MaxMp, world.IsDead, aggro, distance, target,
				world.Skills, cooldowns, chain, chainTarget, chainExpires, blessing,
				Potion(LifePotion) != null, Potion(ManaPotion) != null,
				!itemCooldowns.TryGetValue(SharedPotionCooldown, out var lifeDue) || lifeDue <= now,
				!itemCooldowns.TryGetValue(SharedPotionCooldown, out var manaDue) || manaDue <= now), now);
		}

		private void Record(NaturalCombatChoice choice)
		{
			BotWorldModel world = actor.Session.Api.World;
			var decision = new NaturalDecision(++decisionSequence, choice.Action, null,
				choice.Action == "blocked" ? "blocked" : "selected", choice.Reason,
				[
					new("observed-survival", "pass", $"HP {world.CurrentHp}/{world.MaxHp}, MP {world.CurrentMp}/{world.MaxMp}, aggro={aggro}."),
					.. choice.Checks,
				], []);
			actor.Trace.WriteAction(actor.LastStep, "natural:combat-decision", new Dictionary<string, object?>
			{
				["decision"] = decision, ["skillId"] = choice.Skill?.Id, ["targetObjectId"] = choice.TargetObjectId,
				["cooldowns"] = cooldowns, ["chain"] = chain,
			});
			options.Dashboard.PublishDecision(actor.Bot, decision);
		}

		private async Task<int> SelectSpriggAsync(CancellationToken token)
		{
			for (int attempt = 0; attempt < 5; attempt++)
			{
				BotPosition start = actor.Session.CurrentPosition;
				BotKnownObject? npc = actor.Session.Api.World.Objects.Values
					.Where(candidate => candidate.Kind == BotKnownObjectKind.Npc && candidate.TemplateId == SpriggWorker &&
						!defeated.Contains(candidate.ObjectId))
					.OrderBy(candidate => Distance(start, candidate.Position)).ThenBy(candidate => candidate.ObjectId).FirstOrDefault();
				if (npc != null) return npc.ObjectId;
				await Task.Delay(TimeSpan.FromSeconds(1), token);
				await actor.Session.SynchronizeAsync(token);
			}
			throw new InvalidDataException("No client-observed Sprigg Worker became available within five checks.");
		}

		private async Task CastAsync(NaturalPriestSkill skill, int enemyId, bool self, CancellationToken token)
		{
			LiveBotSession session = actor.Session;
			int target = self ? session.CharacterId : enemyId;
			if (!session.Api.World.Skills.TryGetValue(skill.Id, out BotSkill? learned))
				throw new InvalidDataException($"Skill {skill.Id} disappeared from the client-observed skill list.");
			BotPosition destination = self ? session.CurrentPosition : session.Api.World.Objects[enemyId].Position;
			SkillTemplate template = templates[skill.Id];
			int travel = template.GetAmmoSpeed() > 0
				? checked((int)Math.Ceiling(Distance(session.CurrentPosition, destination) / template.GetAmmoSpeed() * 1000)) : 0;
			int hitTime = motion.CalculateClientHitTime(template,
				new BotMotionProfile(Race.ASMODIANS, Gender.MALE, BotWeaponMotionType.Mace), travel);
			await session.SendPacketAsync(session.Api.Target(target), token);
			await session.SendPacketAsync(session.Api.Cast(new SpellCastData(skill.Id, checked((byte)learned.Level), 0)
			{ TargetObjectId = target, HitTime = checked((ushort)hitTime) }), token);
			DecodedBotServerPacket started = await BotCastProtocol.WaitForStartAsync(session.WaitForAnyPacketAsync,
				session.CharacterId, skill.Id, token);
			await Task.Delay(started.Get<ushort>("castDuration") + 1, token);
			DecodedBotServerPacket result = await BotCastProtocol.WaitForCompletionAsync(session.WaitForAnyPacketAsync,
				session.CharacterId, skill.Id, token);
			if (result.PacketType == typeof(SM_CASTSPELL_RESULT))
			{
				int deciseconds = result.Get<int>("cooldown");
				if (deciseconds > 0)
					cooldowns[skill.CooldownId] = TimeProvider.System.GetUtcNow().AddMilliseconds(deciseconds * 100L);
				if (skill.ChainCategory != null && (result.Get<byte>("flags") & 32) != 0)
				{
					chain = skill.ChainCategory; chainTarget = target;
					chainExpires = TimeProvider.System.GetUtcNow().AddMilliseconds(skill.ChainWindowMillis);
				}
			}
			else
				actor.Trace.WriteAction(actor.LastStep, "natural:cast-cancelled", new Dictionary<string, object?>
				{
					["skillId"] = skill.Id, ["targetObjectId"] = target,
					["reason"] = session.Api.World.SystemMessages.LastOrDefault()?.Name ?? "unclassified",
				});
			await Task.Delay(BotCastProtocol.RecoveryDelay(result), token);
		}

		private BotInventoryItem? Potion(int itemId) => actor.Session.Api.World.Inventory.Values
			.FirstOrDefault(item => item.ItemId == itemId && item.Count > 0);

		private async Task UsePotionAsync(int itemId, CancellationToken token)
		{
			BotInventoryItem item = Potion(itemId) ?? throw new InvalidDataException("Selected owned potion disappeared.");
			await actor.Session.SendPacketAsync(GameClientPackets.UseItem(item.ObjectId), token);
			itemCooldowns[SharedPotionCooldown] = TimeProvider.System.GetUtcNow().AddSeconds(30);
			await Task.Delay(TimeSpan.FromSeconds(2), token);
		}

		private async Task RecoverAsync(CancellationToken token, bool forceRest = false)
		{
			LiveBotSession session = actor.Session;
			BotWorldModel world = session.Api.World;
			await session.SynchronizeAsync(token);
			if (world.IsDead) await ReviveAsync(token);
			NaturalCombatChoice initial = Decide(null, null);
			for (int preparation = 0; preparation < 3 && initial.Action is "cast-self" or "life-potion" or "mana-potion"; preparation++)
			{
				Record(initial);
				if (initial.Action == "cast-self") await CastAsync(initial.Skill!, 0, true, token);
				else await UsePotionAsync(initial.Action == "life-potion" ? LifePotion : ManaPotion, token);
				await session.SynchronizeAsync(token);
				initial = Decide(null, null);
			}
			if (initial.Action == "ready" && !forceRest) return;
			Record(initial.Action == "ready"
				? new NaturalCombatChoice("rest", null, null, "Conservative post-kill pause before another pull.", [])
				: initial);
			await session.SendPacketAsync(session.Api.Rest(true), token);
			await session.WaitForPacketAsync(typeof(SM_EMOTION), token, packet =>
				packet.Get<int>("senderObjectId") == session.CharacterId &&
				packet.Get<byte>("emotionType") == (byte)EmotionType.SIT);
			try
			{
				for (int second = 0; second < 90; second++)
				{
					int before = world.CurrentHp;
					await Task.Delay(TimeSpan.FromSeconds(1), token);
					await session.SynchronizeAsync(token);
					if (world.IsDead) { await ReviveAsync(token); return; }
					if (world.CurrentHp < before) { aggro = true; break; }
					if (second >= (forceRest ? 1 : 0) && world.CurrentHp * 100 >= world.MaxHp * 90 &&
						world.CurrentMp * 100 >= world.MaxMp * 80) break;
					if (second == 89) throw new TimeoutException("Ordinary rest did not reach 90% HP/80% MP within 90 seconds.");
				}
			}
			finally
			{
				if (!world.IsDead)
				{
					await session.SendPacketAsync(session.Api.Rest(false), token);
					await session.WaitForPacketAsync(typeof(SM_EMOTION), token, packet =>
						packet.Get<int>("senderObjectId") == session.CharacterId &&
						packet.Get<byte>("emotionType") == (byte)EmotionType.STAND);
				}
			}
			if (aggro)
			{
				Record(new NaturalCombatChoice("defend", null, null,
					"Rest interrupted by HP loss; select only a nearby observed eligible Sprigg before attacking.", []));
				return;
			}
			Record(Decide(null, null));
		}

		private async Task RetreatAsync(BotPosition enemy, CancellationToken token)
		{
			if (++retreats > 2) throw new InvalidDataException("Critical-HP retreat budget exhausted.");
			LiveBotSession session = actor.Session;
			BotPosition start = session.CurrentPosition;
			float dx = start.X - enemy.X, dy = start.Y - enemy.Y;
			float length = MathF.Max(1, MathF.Sqrt(dx * dx + dy * dy));
			var away = new BotPosition(start.X + 8 * dx / length, start.Y + 8 * dy / length, start.Z, start.Heading);
			var navigation = session.Navigation ?? throw new InvalidDataException("Retreat needs checked navigation assets.");
			int map = session.Api.World.MapId ?? throw new InvalidDataException("Retreat map is unobserved.");
			IReadOnlyList<BotPosition> route = navigation.Geometry.FindLocalPath(map, start, away);
			if (route.Count == 0) throw new InvalidDataException("No collision-checked retreat route.");
			await session.ExecuteMovementAsync(new BotMover(session.Api.World, session.Api.Timing).CreateGroundPlan(route,
				start, session.Api.World.MovementSpeed ?? throw new InvalidDataException("Movement speed unobserved.")), token);
		}

		private async Task ReviveAsync(CancellationToken token)
		{
			if (++deaths > 1) throw new InvalidDataException("Bounded ordinary resurrection budget exhausted.");
			LiveBotSession session = actor.Session;
			await session.SendPacketAsync(session.Api.Revive(BotReviveType.Bind), token);
			session.Api.World.BeginWorldReload();
			await session.CompleteTeleportAsync(220010000, token);
			await session.SynchronizeAsync(token);
			if (session.Api.World.IsDead || session.Api.World.CurrentHp <= 0)
				throw new InvalidDataException("Bind revival did not restore observed life.");
			cooldowns.Clear(); chain = null; chainTarget = null; chainExpires = null;
		}

		private static float Distance(BotPosition a, BotPosition b) => MathF.Sqrt(
			(a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y) + (a.Z - b.Z) * (a.Z - b.Z));

		private static Dictionary<int, SkillTemplate> LoadTemplates(string root)
		{
			var wanted = NaturalPriestSkills.All.Select(skill => (int)skill.Id).ToHashSet();
			var found = new Dictionary<int, SkillTemplate>();
			using var reader = XmlReader.Create(Path.Combine(root, "game-server/data/static_data/skills/skill_templates.xml"));
			var serializer = new XmlSerializer(typeof(SkillTemplate), new XmlRootAttribute("skill_template"));
			while (reader.Read())
			{
				if (reader.NodeType != XmlNodeType.Element || reader.LocalName != "skill_template" ||
					!int.TryParse(reader.GetAttribute("skill_id"), out int id) || !wanted.Contains(id)) continue;
				using var subtree = reader.ReadSubtree();
				found[id] = (SkillTemplate?)serializer.Deserialize(subtree)
					?? throw new InvalidDataException($"Cannot read Priest skill template {id}.");
			}
			if (found.Count != wanted.Count) throw new InvalidDataException("Priest skill template set is incomplete.");
			return found;
		}
	}
}
