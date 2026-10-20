using Aion.Bots.Protocol;
using Aion.Bots.Scenarios.Classes;
using Aion.Bots.World;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.Bots.Scenarios;

/// <summary>Support for an already observed group. Formation, following, loot and revival belong to its controller.</summary>
public sealed class NaturalGroupHealDriver(INaturalJourneySession session, NaturalJourneyRuntime runtime, NaturalClassProfile profile) : IDisposable
{
	private readonly IDisposable mode = session.Api.BeginHealingOnly(profile.SupportSkills.Select(skill => skill.Id),
		packet => session.TraceDiagnostic("group-support-offensive-action-blocked", new Dictionary<string, object?> { ["packet"] = packet }));
	private readonly Dictionary<int, DateTimeOffset> cooldowns = [];
	private readonly Dictionary<int, DateTimeOffset> resurrectionOffers = [];
	private int lastHotTarget;
	private int packetCursor;
	private DateTimeOffset lastDamage;
	private readonly HashSet<ushort> hotIds = profile.SupportSkills.Where(skill => skill.Role == "rejuvenation").Select(skill => skill.Id).ToHashSet();
	private readonly HashSet<ushort> blessingIds = profile.SupportSkills.Where(skill => skill.Role == "blessing").Select(skill => skill.Id).ToHashSet();
	private sealed record Member(int Id, double HpPercent, int MaxHp, BotPosition Position);

	/// <summary>M6: in the open field a healer far behind its leader catches up before any upkeep.</summary>
	public bool CatchUp { get; set; }

	public void Dispose() => mode.Dispose();

	public async Task RunAsync(CancellationToken token)
	{
		while (true)
		{
			token.ThrowIfCancellationRequested();
			await StepAsync(token);
			await session.AdvanceAsync(TimeSpan.FromMilliseconds(250), token);
		}
	}

	/// <summary>One observed support decision; usable by a future group controller without running a solo journey.</summary>
	public async Task<bool> StepAsync(CancellationToken token)
	{
		await session.SynchronizeAsync(token);
		BotWorldModel world = session.Api.World;
		DateTimeOffset now = session.Api.Timing.Now;
		foreach (var packet in session.PacketHistory.Skip(packetCursor))
			if (packet.PacketType == typeof(SM_ATTACK) && (packet.Get<int>("targetObjId") == session.CharacterId || world.GroupMembers.ContainsKey(packet.Get<int>("targetObjId"))) ||
				BotAttackStatus.CarriesHp(packet) && packet.Get<int>("writtenValue") < 0 &&
				(packet.Get<int>("objectId") == session.CharacterId || world.GroupMembers.ContainsKey(packet.Get<int>("objectId"))))
				lastDamage = packet.ReceivedAt ?? now;
		packetCursor = session.PacketHistory.Count;
		if (world.IsDead || world.MaxHp <= 0 || world.MaxMp <= 0) return false;
		var members = new List<Member> { new(session.CharacterId, world.CurrentHp * 100.0 / world.MaxHp, world.MaxHp, session.CurrentPosition) };
		members.AddRange(world.GroupMembers.Values.Where(member => member.ObjectId != session.CharacterId && member.Online && !member.Dead &&
			member.MaxHp > 0 && member.MapId == world.MapId &&
			(member.InstanceMapId == 0 || Math.Abs((long)member.InstanceMapId) == (long)member.MapId + (world.InstanceId ?? 1) - 1))
			.Select(member => new Member(member.ObjectId, member.HpPercent, member.MaxHp,
				world.Objects.GetValueOrDefault(member.ObjectId)?.SettledPosition ?? member.Position)));
		Member[] byNeed = members.OrderBy(member => member.Id == session.CharacterId ? 0 : 1).ThenBy(member => member.HpPercent).ThenBy(member => member.Id).ToArray();
		NaturalPriestSkill? Best(string role) => NaturalPriestSkills.Best(role, world.Level, world.Skills, profile.SupportSkills);
		int Mana(NaturalPriestSkill skill) => skill.ManaCostPercent > 0 ? world.MaxMp * skill.ManaCostPercent / 100 : skill.ManaCost;
		bool reportedRestriction = false;
		bool CanCast(NaturalPriestSkill skill)
		{
			if (runtime.CastingRestriction(world, skill.Id) is not { } reason) return true;
			if (!reportedRestriction)
				session.TraceDiagnostic("group-support-wait-disabled", new Dictionary<string, object?>
				{ ["skillId"] = skill.Id, ["abnormals"] = world.SelfAbnormals, ["reason"] = reason });
			reportedRestriction = true;
			return false;
		}
		bool Ready(NaturalPriestSkill? skill, Member member) => skill != null && world.CurrentMp >= Mana(skill) &&
			world.CurrentDp >= skill.DpCost && session.Api.Timing.TimeUntilCast(skill.Id) <= TimeSpan.Zero &&
			cooldowns.GetValueOrDefault(skill.CooldownId) <= now &&
			(skill.ReagentCount == 0 || world.Inventory.Values.Where(item => item.ItemId == skill.ReagentItemId).Sum(item => item.Count) >= skill.ReagentCount) &&
			(member.Id == session.CharacterId || Distance(member.Position, session.CurrentPosition) <= skill.Range &&
				runtime.CreateGeometry().HasLineOfSight(world.MapId!.Value, session.CurrentPosition, member.Position)) && CanCast(skill);
		IReadOnlyList<BotVisibleEffect> Effects(Member member) => member.Id == session.CharacterId ? world.VisibleEffects ?? [] : world.EffectsOf(member.Id);
		bool Covered(Member member, HashSet<ushort> ids)
		{
			foreach (var effect in Effects(member))
			{
				if (world.RemainingEffectMillis(member.Id, effect, now) <= 2000) continue;
				if (ids.Contains((ushort)effect.SkillId)) return true;
				var existing = runtime.Data.SkillDataDh.GetSkillTemplate(effect.SkillId);
				if (existing?.GetStack() is not { } stack) continue;
				foreach (ushort id in ids)
				{
					var own = runtime.Data.SkillDataDh.GetSkillTemplate(id);
					if (own?.GetStack() == stack && (existing.GetLvl() > own.GetLvl() || existing.GetLvl() == own.GetLvl() &&
						effect.SkillLevel >= (world.Skills.GetValueOrDefault(id)?.Level ?? own.GetLvl()))) return true;
				}
			}
			return false;
		}

		// M8 (the operator, 2026-10-14: "the cleric probably has more and better heals (a group heal?)"): Healing Wind heals
		// every member within its reach at once. It is cast when two or more members there are at 70% or less and none is
		// in the danger a single heal answers first (35% or less).
		// L3 (docs/level-31-profiles.md): Acquittal (31; 2,000 DP, half an hour) takes up to five harmful effects off every
		// member within its reach and heals each 20% of its health every 2 s for 10 s: cast when three or more are at 50% or
		// less. Noble Grace, the second stone (no cast; 15 s of +35% healing received and +1,200 health for the group), goes
		// before the heals when two or more there are at 50% or less.
		foreach ((string role, int count) in new[] { ("acquittal", 3), ("noble-grace", 2) })
		{
			if (Best(role) is not { } word || !Ready(word, members[0])) continue;
			float wordReach = runtime.Data.SkillDataDh.GetSkillTemplate(word.Id)?.GetProperties()?.GetEffectiveRange() ?? 0;
			Member[] low = [.. members.Where(member => member.HpPercent <= 50 && Distance(member.Position, session.CurrentPosition) <= wordReach)];
			if (low.Length >= count) return await CastAsync(word, session.CharacterId, role, low.Min(member => member.HpPercent), token);
		}
		NaturalPriestSkill? wind = Best("group-heal");
		if (wind != null && byNeed.All(member => member.HpPercent > 35))
		{
			float reach = runtime.Data.SkillDataDh.GetSkillTemplate(wind.Id)?.GetProperties()?.GetEffectiveRange() ?? 0;
			Member[] hurt = [.. members.Where(member => member.HpPercent <= 70 && Distance(member.Position, session.CurrentPosition) <= reach)];
			if (hurt.Length >= 2 && Ready(wind, members[0]))
				return await CastAsync(wind, session.CharacterId, "group-heal", hurt.Min(member => member.HpPercent), token);
		}

		// Its own needed healing wins even when another member is in more danger.
		foreach (Member member in byNeed.Where(member => member.HpPercent <= 70))
		{
			NaturalPriestSkill? flash = member.HpPercent <= 35 ? Best("flash-recovery") : null;
			// MR-g: the stigma's heal acts once its target is at half its health, and has no cast.
			NaturalPriestSkill? saving = member.HpPercent <= 50 ? Best("saving-grace") : null;
			NaturalPriestSkill? grace = Best("grace"), light = Best("heal"), quick = Best("quick-heal");
			int amount = grace == null ? 0 : runtime.Data.SkillDataDh.GetSkillTemplate(grace.Id)?.GetEffects()?.GetEffects().OfType<HealInstantEffect>().FirstOrDefault()?.Value ?? 0;
			if (member.HpPercent > 35 && member.MaxHp * (100 - member.HpPercent) / 100 < amount) grace = null;
			NaturalPriestSkill? heal = new[] { saving, flash, quick, grace, light }.FirstOrDefault(skill => Ready(skill, member));
			if (heal != null) return await CastAsync(heal, member.Id, "heal", member.HpPercent, token);
		}

		NaturalPriestSkill? cleanse = Best("cleanse");
		if (cleanse != null)
			foreach (Member member in byNeed)
				if (Ready(cleanse, member) && Effects(member).Any(effect => CanCleanse(cleanse, effect) &&
					world.RemainingEffectMillis(member.Id, effect, now) >= 3000))
					return await CastAsync(cleanse, member.Id, "cleanse", member.HpPercent, token);

		bool quiet = (now - lastDamage).TotalSeconds >= 5;
		Member self = members[0];
		// M6: a healer far behind its leader catches up before any upkeep. Kimssi's elites caught the Cleric alone 30 to 90 m
		// behind the group three times while it cast blessings and idle heals over time on the way.
		bool behind = CatchUp && world.GroupLeaderId is int leading && leading != session.CharacterId &&
			members.FirstOrDefault(member => member.Id == leading) is { } leader && Distance(leader.Position, session.CurrentPosition) > 20;
		if (world.CurrentMp < world.MaxMp / 2 && quiet && !behind)
		{
			NaturalPriestSkill? recovery = self.HpPercent >= 80 ? Best("penance") : null;
			if (!Ready(recovery, self)) recovery = Best("mp-recovery");
			if (Ready(recovery, self)) return await CastAsync(recovery!, self.Id, "mana-recovery", self.HpPercent, token);
		}
		if (quiet)
		{
			NaturalPriestSkill? resurrect = Best("resurrect");
			foreach (var fallen in world.GroupMembers.Values.Where(member => member.Online && member.Dead && member.MapId == world.MapId &&
				(member.InstanceMapId == 0 || Math.Abs((long)member.InstanceMapId) == (long)member.MapId + (world.InstanceId ?? 1) - 1)))
			{
				var member = new Member(fallen.ObjectId, 0, fallen.MaxHp, world.Objects.GetValueOrDefault(fallen.ObjectId)?.Position ?? fallen.Position);
				if (Ready(resurrect, member) && resurrectionOffers.GetValueOrDefault(member.Id) <= now)
				{
					bool offered = await CastAsync(resurrect!, member.Id, "resurrect", 0, token);
					if (offered) resurrectionOffers[member.Id] = session.Api.Timing.Now.AddSeconds(45);
					return offered;
				}
			}
			if (behind) { session.PublishDashboard("healing-only"); return false; }
			NaturalPriestSkill? blessing = Best("blessing");
			foreach (Member member in byNeed)
				if (Ready(blessing, member) && !Covered(member, blessingIds))
					return await CastAsync(blessing!, member.Id, "blessing", member.HpPercent, token);
			NaturalPriestSkill? light = Best("heal");
			foreach (Member member in byNeed.Where(member => member.HpPercent < 90))
				if (Ready(light, member)) return await CastAsync(light!, member.Id, "top-up", member.HpPercent, token);
		}
		if (behind) { session.PublishDashboard("healing-only"); return false; }
		// Idle upkeep visits every member, not just whoever was hit last. Preserve mana for a direct heal.
		NaturalPriestSkill? hot = Best("rejuvenation"), reserve = Best("heal");
		if (world.GroupId != null && hot != null && world.CurrentMp >= hot.ManaCost + (reserve?.ManaCost ?? 0))
		{
			Member[] cycle = members.OrderBy(member => member.Id).ToArray();
			int start = Array.FindIndex(cycle, member => member.Id == lastHotTarget) + 1;
			for (int offset = 0; offset < cycle.Length; offset++)
			{
				Member member = cycle[(start + offset) % cycle.Length];
				if (!Ready(hot, member) || Covered(member, hotIds)) continue;
				lastHotTarget = member.Id;
				return await CastAsync(hot, member.Id, "idle-hot", member.HpPercent, token);
			}
		}
		session.PublishDashboard("healing-only");
		return false;
	}

	private bool CanCleanse(NaturalPriestSkill cleanse, BotVisibleEffect effect)
	{
		// Packet slots are Java enum ordinals, not the C# enum's explicit bit values.
		if (effect.TargetSlot != Array.IndexOf(Enum.GetValues<SkillTargetSlot>(), SkillTargetSlot.DEBUFF)) return false;
		var dispel = runtime.Data.SkillDataDh.GetSkillTemplate(cleanse.Id)?.GetEffects()?.GetEffects().OfType<DispelDebuffEffect>().FirstOrDefault();
		var debuff = runtime.Data.SkillDataDh.GetSkillTemplate(effect.SkillId);
		return dispel != null && debuff != null && effect.RemainingMillis >= 0 &&
			debuff.GetDispelCategory() is DispelCategoryType.ALL or DispelCategoryType.DEBUFF_PHYSICAL or DispelCategoryType.DEBUFF_MENTAL &&
			debuff.GetReqDispelLevel() <= dispel.dispelLevel &&
			// Java reduces remaining resistance each time; a stronger eligible debuff can take several casts.
			dispel.power + dispel.dpower * session.Api.World.Skills[cleanse.Id].Level > 0 &&
			debuff.GetEffects()?.GetEffects().Any(part => part.GetDuration2() + (long)part.GetDuration1() * effect.SkillLevel >= 86400000) != true;
	}

	private async Task<bool> CastAsync(NaturalPriestSkill skill, int target, string rule, double hpPercent, CancellationToken token)
	{
		session.BeginStep("group-support", rule);
		session.TraceDiagnostic("group-support-decision", new Dictionary<string, object?>
		{
			["rule"] = rule, ["targetObjectId"] = target, ["skillId"] = skill.Id, ["hpPercent"] = hpPercent,
			["mp"] = session.Api.World.CurrentMp, ["groupMembers"] = session.Api.World.GroupMembers.Count,
		});
		await session.SendPacketAsync(session.Api.Target(target), token);
		// Targeting can drain a newer effect snapshot. Recheck before opening the local casting gate.
		if (runtime.CastingRestriction(session.Api.World, skill.Id) is { } reason)
		{
			session.TraceDiagnostic("group-support-wait-disabled", new Dictionary<string, object?>
			{ ["skillId"] = skill.Id, ["abnormals"] = session.Api.World.SelfAbnormals, ["reason"] = reason });
			return false;
		}
		var cast = runtime.CreateSpellCast(session.Api.World, session.CurrentPosition, skill.Id,
			checked((byte)session.Api.World.Skills[skill.Id].Level), target);
		await session.SendPacketAsync(session.Api.Cast(cast), token);
		DecodedBotServerPacket start = await BotCastProtocol.WaitForStartAsync(
			(predicate, ct) => session.WaitForPacketAsync(packet => predicate(packet) || BotCastProtocol.IsStartRejection(packet), ct),
			session.CharacterId, skill.Id, token);
		if (start.PacketType == typeof(SM_SYSTEM_MESSAGE))
		{
			session.Api.Timing.RecordCastCancelled();
			session.TraceDiagnostic("group-support-refused", new Dictionary<string, object?>
			{ ["skillId"] = skill.Id, ["targetObjectId"] = target, ["reason"] = start.Get<string>("name") });
			await session.AdvanceAsync(TimeSpan.FromMilliseconds(500), token);
			return false;
		}
		await session.AdvanceAsync(TimeSpan.FromMilliseconds(start.Get<ushort>("castDuration") + 1), token);
		DecodedBotServerPacket result = await BotCastProtocol.WaitForCompletionAsync(session.WaitForPacketAsync, session.CharacterId, skill.Id, token);
		if (result.PacketType == typeof(SM_CASTSPELL_RESULT))
			cooldowns[skill.CooldownId] = session.Api.Timing.Now.AddMilliseconds(result.Get<int>("cooldown") * 100L);
		await session.AdvanceAsync(TimeSpan.FromMilliseconds(Math.Max(BotCastProtocol.RecoveryDelay(result).TotalMilliseconds,
			runtime.AnimationLastHitMillis(session.Api.World, skill.Id) + 1)), token);
		return result.PacketType == typeof(SM_CASTSPELL_RESULT);
	}

	private static float Distance(BotPosition a, BotPosition b) => MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y) + (a.Z - b.Z) * (a.Z - b.Z));
}
