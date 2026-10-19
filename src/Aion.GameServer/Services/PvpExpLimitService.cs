using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Services;

/// <summary>Java's per-victim cooldown and persisted PvP XP accumulator, reduced only for online time.</summary>
public class PvpExpLimitService
{
    private const long CLEANUP_INTERVAL_MILLIS = 3 * 60 * 1000;
    private readonly ConcurrentDictionary<int, PvpExpLimits> limitsByPlayer = new();
    private PvpExpLimitService()
    {
        ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(_ =>
        {
            RemoveUnusedLimits(); return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(CLEANUP_INTERVAL_MILLIS), TimeSpan.FromMilliseconds(CLEANUP_INTERVAL_MILLIS));
    }
    public static PvpExpLimitService GetInstance() => SingletonHolder.Instance;
    public bool TryGainPvpExp(Player killer, Player victim, long xp)
    {
        PvpExpTable table = DataManager.PVP_EXP_TABLE;
        PlayerCommonData pcd = killer.GetCommonData();
        long now = SystemClock.CurrentMillis();
        var limits = limitsByPlayer.GetOrAdd(killer.GetObjectId(), _ => new PvpExpLimits(now));
        bool belowCap, cooldownExpired;
        lock (limits)
        {
            ReduceAccumulatedXp(killer, limits, now);
            belowCap = pcd.GetPvpExp() <= table.GetMaxFromAllUser(killer.GetLevel());
            if (belowCap) pcd.SetPvpExp(unchecked(pcd.GetPvpExp() + xp));
            bool hadCooldown = limits.CooldownEndByVictim.TryGetValue(victim.GetObjectId(), out long previous);
            limits.CooldownEndByVictim[victim.GetObjectId()] = unchecked(now + table.GetDelayTimeMillis(killer.GetLevel()));
            cooldownExpired = !hadCooldown || previous < now;
        }
        if (!belowCap) PacketSendUtility.SendPacket(killer, SM_SYSTEM_MESSAGE.STR_CANNOT_GET_PVP_EXP_TIMEBASE_LIMIT());
        if (!cooldownExpired) PacketSendUtility.SendPacket(killer, SM_SYSTEM_MESSAGE.STR_CANNOT_GET_PVP_EXP_TARGET_LIMIT());
        return belowCap && cooldownExpired;
    }
    public void OnEnterWorld(Player player)
    {
        long now = SystemClock.CurrentMillis();
        var limits = limitsByPlayer.GetOrAdd(player.GetObjectId(), _ => new PvpExpLimits(now));
        lock (limits) limits.LastReduceTime = now;
    }
    public void OnLeaveWorld(Player player)
    {
        if (limitsByPlayer.TryGetValue(player.GetObjectId(), out var limits))
            lock (limits) ReduceAccumulatedXp(player, limits, SystemClock.CurrentMillis());
    }
    private void ReduceAccumulatedXp(Player player, PvpExpLimits limits, long now)
    {
        var table = DataManager.PVP_EXP_TABLE;
        long interval = table.GetReduceIntervalMillis(player.GetLevel());
        if (interval <= 0) return;
        long intervals = unchecked(now - limits.LastReduceTime) / interval;
        if (intervals > 0)
        {
            var pcd = player.GetCommonData();
            pcd.SetPvpExp(Math.Max(0, unchecked(pcd.GetPvpExp() - intervals * table.GetReduceAmount(player.GetLevel()))));
            limits.LastReduceTime = unchecked(limits.LastReduceTime + intervals * interval);
        }
    }
    private void RemoveUnusedLimits()
    {
        long now = SystemClock.CurrentMillis();
        foreach (var entry in limitsByPlayer)
        {
            var limits = entry.Value;
            lock (limits)
            {
                foreach (int id in limits.CooldownEndByVictim.Where(e => e.Value < now).Select(e => e.Key).ToArray())
                    limits.CooldownEndByVictim.Remove(id);
                if (limits.CooldownEndByVictim.Count == 0 && Aion.GameServer.World.World.GetInstance().GetPlayer(entry.Key) == null)
                    ((ICollection<KeyValuePair<int, PvpExpLimits>>)limitsByPlayer).Remove(entry);
            }
        }
    }
    private sealed class PvpExpLimits(long now)
    {
        internal readonly Dictionary<int, long> CooldownEndByVictim = new();
        internal long LastReduceTime = now;
    }
    private static class SingletonHolder { internal static readonly PvpExpLimitService Instance = new(); }
}
