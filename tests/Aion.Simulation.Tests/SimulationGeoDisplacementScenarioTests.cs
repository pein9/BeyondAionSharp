using System.Buffers.Binary;
using Aion.Bots.Navigation;
using Aion.Bots.Protocol;
using Aion.Bots.Scenarios;
using Aion.Bots.Timing;
using Aion.Bots.World;
using Aion.GameServer.Dataholders;
using Aion.GameServer.GeoEngine.Collision;
using Aion.GameServer.GeoEngine.Math;
using Aion.GameServer.GeoEngine.Models;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Items;
using Aion.GameServer.Model.Templates.Items.Enums;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.TestKit;
using Aion.GameServer.Utils;
using Aion.GameServer.World.Geo;

namespace Aion.Simulation.Tests;

public sealed partial class SimulationFastScenarioTests
{
		private async Task RunGeoDisplacementAsync(ScenarioDefinition scenario, bool includeHistory, bool fear)
		{
				using var policy = NewPolicy(scenario.Id, includeHistory);
				using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
				var token = timeout.Token;
				int account = fear ? 91 : 93;
				await using var casterSession = await EnterCombatWorldAsync(policy, account, fear ? "Asimfeara" : "Asimknocka",
						Race.ASMODIANS, fear ? PlayerClass.MAGE : PlayerClass.ENGINEER, token);
				await using var targetSession = await EnterCombatWorldAsync(policy, account + 1, fear ? "Asimfearb" : "Asimknockb",
						Race.ASMODIANS, PlayerClass.WARRIOR, token, "b02");
				Player caster = fixture.World.GetPlayer(casterSession.CharacterId), target = fixture.World.GetPlayer(targetSession.CharacterId);
				try
				{
						foreach (var player in new[] { caster, target }) Assert.Equal(0, player.GetClientConnection().GetAccount().GetAccessLevel());
						Assert.True(ClassChangeService.SetClass(caster, fear ? PlayerClass.SPIRIT_MASTER : PlayerClass.GUNNER, true, true));
						caster.GetCommonData().SetLevel(28); target.GetCommonData().SetLevel(28);
						int skillId = fear ? 3775 : 2058;
						Assert.True(caster.GetSkillList().IsSkillPresent(skillId));
						if (!fear)
						{
								foreach (var slot in new[] { ItemSlot.MAIN_HAND, ItemSlot.SUB_HAND })
								{
										var template = DataManager.ITEM_DATA.GetItemTemplates().Where(i => i.GetItemGroup() == ItemGroup.GUN && CanEquipSkillSweepItem(caster, i))
												.OrderBy(i => i.GetLevel()).ThenBy(i => i.GetTemplateId()).First();
										var item = GetOrGrantSkillSweepItem(caster, template.GetTemplateId());
										Assert.NotNull(caster.GetEquipment().EquipItem(item.GetObjectId(), slot.GetSlotIdMask()));
								}
						}
						var map = GeoService.GetInstance().GetMap(220010000);
						var motion = BotMotionTiming.Load(Path.Combine(RealStaticData.RepoRoot(), "game-server/data/static_data/skills/motion_times.xml"));
						foreach (bool wall in new[] { false, true })
						{
								var site = FindDisplacementSite(map, target.GetInstanceId(), wall);
								Console.WriteLine($"{scenario.Id} {(wall ? "wall" : "open")}: {site}");
								bool applied = false;
								for (int attempt = 0; attempt < 3 && !applied; attempt++)
								{
										// Director setup is finished before each duel/cast; no direct effect application or motion forcing.
										await TeleportForSetupAsync(casterSession, caster, 220010000, site.Caster.X, site.Caster.Y, site.Caster.Z, token);
										await TeleportForSetupAsync(targetSession, target, 220010000,
												site.Target.X - site.Dx * 0.5f, site.Target.Y - site.Dy * 0.5f, site.Target.Z, token);
										await targetSession.MoveToPositionAsync(site.Target, token);
										caster.GetLifeStats().SetCurrentHp(caster.GetLifeStats().GetMaxHp());
										caster.GetLifeStats().SetCurrentMp(caster.GetLifeStats().GetMaxMp());
										target.GetLifeStats().SetCurrentHp(target.GetLifeStats().GetMaxHp());
										Assert.True(GeoService.GetInstance().CanSee(caster, target));
										await casterSession.SendPacketAsync(casterSession.Api.Duel(target.GetObjectId()), token);
										await targetSession.DrainServerPacketsAsync(token);
										await targetSession.WaitForPacketAsync(typeof(SM_QUESTION_WINDOW), token, p => p.Get<int>("code") == 50028);
										await targetSession.SendPacketAsync(targetSession.Api.Answer(1), token);
										await casterSession.DrainServerPacketsAsync(token);
										await casterSession.WaitForPacketAsync(typeof(SM_DUEL), token, p => p.Get<byte>("type") == 0);
										await targetSession.WaitForPacketAsync(typeof(SM_DUEL), token, p => p.Get<byte>("type") == 0);
										await casterSession.SendPacketAsync(casterSession.Api.Target(target.GetObjectId()), token);
										var skill = caster.GetSkillList().GetSkillEntry(skillId);
										int travel = skill.GetSkillTemplate().GetAmmoSpeed() > 0
												? (int)Math.Ceiling(PositionUtil.GetDistance(caster, target) / skill.GetSkillTemplate().GetAmmoSpeed() * 1000) : 0;
										int hit = motion.CalculateClientHitTime(skill.GetSkillTemplate(), new BotMotionProfile(Race.ASMODIANS, Gender.MALE,
												fear ? BotWeaponMotionType.Book : BotWeaponMotionType.TwoGun, caster.GetGameStats().GetAttackSpeedRate()), travel);
										int packetStart = targetSession.PacketHistory.Count;
										casterSession.BeginStep(wall ? "s03" : "s02", $"{(wall ? "wall" : "open")}-cast-{attempt + 1}");
										await casterSession.SendPacketAsync(casterSession.Api.Cast(new SpellCastData((ushort)skillId, (byte)skill.GetSkillLevel(), 0)
										{ TargetObjectId = target.GetObjectId(), HitTime = checked((ushort)hit) }), token);
										await casterSession.AdvanceAsync(TimeSpan.FromMilliseconds(skill.GetSkillTemplate().GetDuration() + hit + 1), token);
										await casterSession.WaitForPacketAsync(typeof(SM_CASTSPELL_RESULT), token, p => p.Get<ushort>("skillId") == skillId);
										applied = target.GetEffectController().IsAbnormalSet(fear ? AbnormalState.FEAR : AbnormalState.STAGGER);
										if (applied)
										{
												float maximum = 0;
												for (int sample = 0; sample < 20; sample++)
												{
														await targetSession.AdvanceAsync(TimeSpan.FromMilliseconds(100), token);
														float displacement = (target.GetX() - site.Target.X) * site.Dx + (target.GetY() - site.Target.Y) * site.Dy;
														maximum = MathF.Max(maximum, displacement);
														Assert.InRange(displacement, -0.05f, wall ? site.WallDistance - 0.35f : 20);
														Assert.False(target.IsDead());
												}
												Assert.InRange(maximum, wall ? 0.05f : fear ? 3 : 1.9f, wall ? 1.5f : 20);
												await targetSession.SynchronizeAsync(token);
												if (fear)
														Assert.Contains(targetSession.PacketHistory.Skip(packetStart), p => p.PacketType == typeof(SM_MOVE) && p.Get<int>("objectId") == target.GetObjectId());
												else
												{
														var forced = Assert.Single(targetSession.PacketHistory.Skip(packetStart), p => p.PacketType == typeof(SM_FORCED_MOVE));
														byte[] body = Convert.FromHexString(forced.Get<string>("bodyHex"));
														Assert.Equal(target.GetObjectId(), BinaryPrimitives.ReadInt32LittleEndian(body.AsSpan(4)));
														Assert.Equal(target.GetX(), BinaryPrimitives.ReadSingleLittleEndian(body.AsSpan(9)), 3);
														Assert.Equal(target.GetY(), BinaryPrimitives.ReadSingleLittleEndian(body.AsSpan(13)), 3);
												}
										}
										// Let effects and cooldowns expire normally. The next director setup teleport ends the duel.
										await casterSession.AdvanceAsync(TimeSpan.FromSeconds(35), token);
										await TeleportForSetupAsync(casterSession, caster, 220010000, site.Caster.X, site.Caster.Y, site.Caster.Z, token);
										await targetSession.DrainServerPacketsAsync(token);
										Assert.False(DuelService.GetInstance().IsDueling(caster));
								}
								Assert.True(applied, $"{scenario.Id} did not apply in three ordinary casts at the {(wall ? "wall" : "open")} site.");
						}
						policy.AssertClean();
				}
				catch (Exception exception)
				{
						string Packets(SimulationL0Session session) => string.Join('\n', session.PacketHistory.TakeLast(25)
								.Select(p => p.PacketType.Name + " " + System.Text.Json.JsonSerializer.Serialize(p.Fields)));
						throw new InvalidOperationException($"{scenario.Id} failed. Caster packets:\n{Packets(casterSession)}\nTarget packets:\n{Packets(targetSession)}", exception);
				}
		}

		private sealed record DisplacementSite(BotPosition Caster, BotPosition Target, float Dx, float Dy, float WallDistance, string Geometry);

		private static DisplacementSite FindDisplacementSite(GeoMap map, int instance, bool wall)
		{
				var geometry = new BotNavigationGeometry(_ => map, instance, IgnoreProperties.ASMODIANS);
				// Search around the starter village, not a manufactured collision fixture. Require ground, a clear
				// approach and, for the wall case, a mesh hit at both chest and head height (not a terrain incline).
				for (int x = -16; x <= 16; x += 2) for (int y = -16; y <= 16; y += 2)
				{
						float px = 546 + x, py = 2780 + y, z = map.GetZ(px, py, 307, 289, instance, true);
						if (!float.IsFinite(z)) continue;
						foreach (var (dx, dy) in new[] { (1f, 0f), (0f, 1f), (-1f, 0f), (0f, -1f) })
						{
								var target = new BotPosition(px, py, z, 0);
								float cz = map.GetZ(px - dx * 3, py - dy * 3, z + 2, z - 2, instance, true);
								var caster = new BotPosition(px - dx * 3, py - dy * 3, cz, 0);
								if (!float.IsFinite(cz) || geometry.TraceEdge(map.GetMapId(), caster, target) == null) continue;
								var collision = map.GetCollisions(px, py, z + 1, px + dx * 15, py + dy * 15, z + 1, instance,
										CollisionIntention.DEFAULT_COLLISIONS.GetId(), IgnoreProperties.ASMODIANS).GetClosestCollision();
								if (wall)
								{
										if (collision?.GetGeometry() == null || collision.GetDistance() < 0.7f || collision.GetDistance() > 1.8f) continue;
										var upper = map.GetCollisions(px, py, z + 2, px + dx * 3, py + dy * 3, z + 2, instance,
												CollisionIntention.DEFAULT_COLLISIONS.GetId(), IgnoreProperties.ASMODIANS).GetClosestCollision();
										if (upper?.GetGeometry() == null || MathF.Abs(upper.GetDistance() - collision.GetDistance()) > 0.2f) continue;
										return new(caster, target, dx, dy, collision.GetDistance(), collision.GetGeometry()!.GetName() ?? "mesh");
								}
								var end = map.FindMovementCollision(new Vector3f(px, py, z), px + dx * 15, py + dy * 15, instance);
								if (collision == null && (end.X - px) * dx + (end.Y - py) * dy > 12)
										return new(caster, target, dx, dy, float.PositiveInfinity, "open ground");
						}
				}
				throw new InvalidDataException($"No {(wall ? "wall" : "open")} displacement site in the bounded starter village search.");
		}
}