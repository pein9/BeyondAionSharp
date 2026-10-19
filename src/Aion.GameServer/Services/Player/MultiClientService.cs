using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using Msg = Aion.GameServer.Network.Aion.ServerPackets.SM_ENTER_WORLD_CHECK.Msg;
using Aion.GameServer.World;

namespace Aion.GameServer.Services.Players;

/// <summary>Java parity: services/player/MultiClientService. Independent per-computer limit and household faction cooldown; native locking, bounded identifier history and SystemClock retained.</summary>
public class MultiClientService
{
    private static readonly ILogger log = AionLog.For(nameof(MultiClientService));
    private static readonly ConcurrentDictionary<int, AccountSession> sessionsByAccountId = new ConcurrentDictionary<int, AccountSession>();

    public static Msg? TryEnterWorld(Player player, AionConnection con)
    {
        int maxClients = SecurityConfig.MULTI_CLIENT_MAX_CLIENTS_PER_COMPUTER;
        if (maxClients > 0 && !SecurityConfig.MULTI_CLIENT_IGNORED_MAC_ADDRESSES.Contains(con.GetMacAddress()))
        {
            List<Player> playersOnSameComputer = FindPlayersOnSameComputer(con);
            if (playersOnSameComputer.Count >= maxClients)
            {
                log.LogInformation("Blocked {Player} from logging on (multi-client limit of {MaxClients} reached by {Players})", player, maxClients, playersOnSameComputer);
                return Msg.CONNECTION_ERROR;
            }
        }
        if (!SecurityConfig.MULTI_CLIENT_ALLOW_BOTH_FACTIONS)
        {
            foreach (KeyValuePair<int, AccountSession> kv in sessionsByAccountId.ToArray())
                if (kv.Value.IsExpired())
                    sessionsByAccountId.TryRemove(kv.Key, out _);
            lock (sessionsByAccountId)
            {
                int? matchedAccountId = CheckForFactionSwitchCooldownTime(player.GetRace(), con);
                if (matchedAccountId != null)
                {
                    log.LogInformation("Blocked {Player} from logging on (faction switch cooldown from account ID {AccountId})", player, matchedAccountId);
                    return Msg.BOTH_FACTIONS;
                }
                AccountSession accountSession = sessionsByAccountId.GetOrAdd(player.GetAccount().GetId(), k => new AccountSession(k));
                accountSession.PutIdentifiers(con);
                accountSession.EnterWorld(player);
            }
        }
        return null;
    }

    // HDD serials are intentionally excluded: the client reads them unreliably.
    private static List<Player> FindPlayersOnSameComputer(AionConnection con)
    {
        string ip = con.GetIP();
        string mac = con.GetMacAddress();
        return Aion.GameServer.World.World.GetInstance().GetAllPlayers().Where(onlinePlayer =>
        {
            AionConnection onlineCon = onlinePlayer.GetClientConnection();
            return onlineCon != null && ip.Equals(onlineCon.GetIP()) && mac.Equals(onlineCon.GetMacAddress());
        }).ToList();
    }

    public static void OnLeaveWorld(Player player)
    {
        AccountSession session = sessionsByAccountId.GetValueOrDefault(player.GetAccount().GetId());
        if (session != null)
            session.LeaveWorld(player);
    }

    public static int? CheckForFactionSwitchCooldownTime(Race race, AionConnection con)
    {
        if (SecurityConfig.MULTI_CLIENT_IGNORED_MAC_ADDRESSES.Contains(con.GetMacAddress()))
            return null;
        Race oppositeRace = race == Race.ELYOS ? Race.ASMODIANS : Race.ELYOS;
        long minLastOnlineMillis = SystemClock.CurrentMillis() - SecurityConfig.MULTI_CLIENT_FACTION_SWITCH_COOLDOWN_MINUTES * 60000L;
        return sessionsByAccountId.Values
            .Where(s => !s.IsIgnored() && s.WasPlayingOnSameIpOrMac(oppositeRace, minLastOnlineMillis, con))
            .Select(s => (int?)s.accountId)
            .FirstOrDefault();
    }

    private class AccountSession
    {
        internal readonly int accountId;
        private readonly ConcurrentDictionary<Race, long> lastCharOnlineTimeMillis = new ConcurrentDictionary<Race, long>();
        private readonly List<Identifiers> identifiers = new List<Identifiers>(2);

        public AccountSession(int accountId)
        {
            this.accountId = accountId;
        }

        internal bool IsIgnored()
        {
            lock (this)
            {
                return identifiers.Count != 0 && SecurityConfig.MULTI_CLIENT_IGNORED_MAC_ADDRESSES.Contains(identifiers[0].mac);
            }
        }

        internal void PutIdentifiers(AionConnection connection)
        {
            lock (this)
            {
                Identifiers ids = new Identifiers(connection.GetIP(), connection.GetMacAddress());
                if (!identifiers.Contains(ids))
                {
                    identifiers.Insert(0, ids);
                    while (identifiers.Count > 3)
                        identifiers.RemoveAt(identifiers.Count - 1);
                }
            }
        }

        internal bool HasAny(string ip, string mac)
        {
            lock (this)
            {
                return identifiers.Any(identifier => identifier.ip.Equals(ip) || identifier.mac.Equals(mac));
            }
        }

        internal bool IsExpired()
        {
            long minLastOnline = SystemClock.CurrentMillis() - SecurityConfig.MULTI_CLIENT_FACTION_SWITCH_COOLDOWN_MINUTES * 60000L;
            return !lastCharOnlineTimeMillis.Values.Any(t => t > minLastOnline);
        }

        internal void EnterWorld(Player player)
        {
            lastCharOnlineTimeMillis[player.GetRace()] = long.MaxValue;
        }

        internal void LeaveWorld(Player player)
        {
            lastCharOnlineTimeMillis[player.GetRace()] = SystemClock.CurrentMillis();
        }

        internal bool WasPlayingOnSameIpOrMac(Race race, long minLastOnlineMillis, AionConnection con)
        {
            if (!lastCharOnlineTimeMillis.TryGetValue(race, out long lastOnlineMillis))
                return false;
            return lastOnlineMillis > minLastOnlineMillis && HasAny(con.GetIP(), con.GetMacAddress());
        }
    }

    private record Identifiers(string ip, string mac);
}
