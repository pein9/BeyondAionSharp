using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Dao;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Summons;
using Aion.GameServer.Model.Team.Alliance;
using Aion.GameServer.Model.Team.Group;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.ChatServer;
using Aion.GameServer.QuestEngine;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;
using Aion.GameServer.Services.ConquerorAndProtectorSystem;
using Aion.GameServer.Services.Findgroup;
using Aion.GameServer.Services.Instance;
using Aion.GameServer.Services.Summons;
using Aion.GameServer.Taskmanager.Tasks;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.Audit;
using Aion.GameServer.World;
using LocationData = Aion.GameServer.Dataholders.PlayerInitialData.LocationData;

namespace Aion.GameServer.Services.Players;

/// <summary>Java parity: services/player/PlayerLeaveWorldService. Registers idempotent logout tasks against the last player action and drains them before shutdown saves. The full logout retains the port's SIM capture and companion cleanup.</summary>
public class PlayerLeaveWorldService
{
    private static readonly ILogger log = AionLog.For(nameof(PlayerLeaveWorldService));
    private static readonly AsyncLocal<Action<Player, bool>?> ScopedCapture = new();
    private static readonly AsyncLocal<bool> CaptureRegistration = new();
    private static readonly ConcurrentDictionary<Player, LeaveWorldTask> leaveWorldTaskByPlayer = new();

    internal static IDisposable CaptureForCurrentContext(Action<Player, bool> capture, bool captureRegistration = true)
    {
        ArgumentNullException.ThrowIfNull(capture);
        var previous = ScopedCapture.Value;
        bool previousRegistration = CaptureRegistration.Value;
        ScopedCapture.Value = capture;
        CaptureRegistration.Value = captureRegistration;
        return new CaptureScope(previous, previousRegistration);
    }

    /// <summary>
    /// Leave ten seconds after the last movement/combat action, immediately if that interval has elapsed.
    /// Repeated registrations keep the existing task and deadline.
    /// </summary>
    public static void RegisterLeaveWorld(Player player)
    {
        if (CaptureRegistration.Value && ScopedCapture.Value is { } capture)
        {
            capture(player, true);
            return;
        }
        long lastActionTime = Math.Max(player.GetMoveController().GetLastMoveUpdate(), player.GetController().GetLastCombatTime());
        long delay = Math.Max(0L, 10_000L - (SystemClock.CurrentMillis() - lastActionTime));
        leaveWorldTaskByPlayer.GetOrAdd(player, value => new LeaveWorldTask(value)).ScheduleOrRun(delay);
    }

    public static bool IsLeavingWorld(int playerObjectId) => leaveWorldTaskByPlayer.Keys.Any(player => player.GetObjectId() == playerObjectId);

    public static bool IsLeavingWorld(Player player) => leaveWorldTaskByPlayer.ContainsKey(player);

    public static void ProcessPendingLeaveWorldTasks()
    {
        foreach (LeaveWorldTask task in leaveWorldTaskByPlayer.Values)
            task.Run();
    }

    /// <summary>
    /// Saves the player and removes him from the world once the registered task is due.
    /// </summary>
    private static void LeaveWorld(Player player)
    {
        if (ScopedCapture.Value is { } capture)
        {
            capture(player, false);
            return;
        }
        AionConnection con = player.GetClientConnection();
        player.SetClientConnection(null); // this sets the player semi-offline, PacketSendUtility will not send packets anymore

        WorldPosition pos = player.GetPosition();
        if (pos == null || pos.GetMapRegion() == null)
        { // ensure safe logout
            log.LogWarning("{Player} had invalid position: {Pos} so he was reset to bind point", player, pos);
            BindPointPosition bp = player.GetBindPoint();
            if (bp != null)
                pos = Aion.GameServer.World.World.GetInstance().CreatePosition(bp.GetMapId(), bp.GetX(), bp.GetY(), bp.GetZ(), bp.GetHeading(), 1);
            else
            {
                LocationData ld = DataManager.PLAYER_INITIAL_DATA.GetSpawnLocation(player.GetRace());
                pos = Aion.GameServer.World.World.GetInstance().CreatePosition(ld.GetMapId(), ld.GetX(), ld.GetY(), ld.GetZ(), (byte)ld.GetHeading(), 1);
            }
            player.SetPosition(pos);
        }

        FindGroupService.GetInstance().OnLogout(player);
        RecallService.GetInstance().Cancel(player, RecallService.CancelReason.CANCELLED);
        player.GetResponseRequester().DenyAll();
        player.GetFriendList().SetStatus(FriendList.Status.OFFLINE, player.GetCommonData());
        BrokerService.GetInstance().RemovePlayerCache(player);
        ExchangeService.GetInstance().CancelExchange(player);
        RepurchaseService.GetInstance().RemoveRepurchaseItems(player);
        if (AutoGroupConfig.AUTO_GROUP_ENABLE)
            AutoGroupService.GetInstance().OnLogout(player);
        ConquerorAndProtectorService.GetInstance().OnLeaveMap(player);
        MultiClientService.OnLeaveWorld(player);
        InstanceService.OnLogout(player);
        GMService.GetInstance().OnPlayerLogout(player);
        KiskService.GetInstance().OnLogout(player);
        Aion.GameServer.Services.PvpExpLimitService.GetInstance().OnLeaveWorld(player);

        if (player.IsDead())
        {
            if (player.IsInInstance() || player.GetWorldId() == 400030000)
                PlayerReviveService.InstanceRevive(player);
            else
                PlayerReviveService.BindRevive(player);
        }
        else if (DuelService.GetInstance().IsDueling(player))
        {
            DuelService.GetInstance().LoseDuel(player);
        }
        player.GetEffectController().RemoveNonStorableEffectsForLogout();
        PlayerEffectsDAO.StorePlayerEffects(player);
        ItemCooldownsDAO.StoreItemCooldowns(player);
        PlayerLifeStatsDAO.UpdatePlayerLifeStat(player);

        PlayerGroupService.OnPlayerLogout(player);
        PlayerAllianceService.OnPlayerLogout(player);
        // fix legion warehouse exploits
        LegionService.GetInstance().LegionWhUpdate(player);
        player.GetEffectController().RemoveAllEffects(true);
        player.GetLifeStats().CancelAllTasks();

        Summon summon = player.GetSummon();
        if (summon != null)
            SummonsService.Release(summon, UnsummonType.LOGOUT); // puts the summoning skill on cooldown, so store cooldowns afterwards
        PlayerCooldownsDAO.StorePlayerCooldowns(player);
        if (player.GetPet() != null)
            player.GetPet().GetController().Delete();
        if (player.GetPostman() != null)
            player.GetPostman().GetController().Delete();

        ExpireTimerTask.GetInstance().UnregisterExpirables(player);
        if (player.GetInteractionTask() != null)
            player.GetInteractionTask().Abort();

        Aion.GameServer.QuestEngine.QuestEngine.GetInstance().OnLogOut(new QuestEnv(null, player, 0));
        DateTime lastOnline = SystemClock.UtcNow().UtcDateTime;
        player.GetController().Delete();
        player.GetCommonData().SetOnline(false);
        player.GetCommonData().SetLastOnline(lastOnline);
        if (player.IsLegionMember()) // must be called after setOnline and setLastOnline
            LegionService.GetInstance().OnLogout(player);
        var flightPath = player.GetFlightPathTemplate();
        if (flightPath != null)
            player.GetPosition().SetXYZH(flightPath.GetEndX(), flightPath.GetEndY(), flightPath.GetEndZ(), (byte)0);
        player.GetCommonData().SetXYZH(player.GetX(), player.GetY(), player.GetZ(), player.GetHeading());

        ChatServer.GetInstance().SendPlayerLogout(player);

        PlayerService.StorePlayer(player);

        player.GetInventory().SetOwner(null);
        player.GetWarehouse().SetOwner(null);
        player.GetAccount().GetAccountWarehouse().SetOwner(null);

        PlayerDAO.StoreOldCharacterLevel(player.GetObjectId(), player.GetLevel());
        PlayerDAO.StoreLastOnlineTime(player.GetObjectId(), lastOnline);
        PlayerDAO.OnlinePlayer(player, false); // marks that player was fully saved and may enter world again

        con.SetActivePlayer(null);

        // D41, no Java counterpart: a companion that has left the world is deleted, and a player's companions end with him.
        Aion.GameServer.Custom.Companion.CompanionService.GetInstance().OnLeftWorld(player);
    }

    private sealed class LeaveWorldTask(Player player)
    {
        private ScheduledTask? future;
        private bool finished;

        public void ScheduleOrRun(long delayMillis)
        {
            lock (this)
            {
                if (finished || future != null)
                    return;
                if (delayMillis <= 0)
                    Run();
                else
                    future = ThreadPoolManager.GetInstance().Schedule(_ => { Run(); return ValueTask.CompletedTask; }, TimeSpan.FromMilliseconds(delayMillis));
            }
        }

        public void Run()
        {
            lock (this)
            {
                if (finished)
                    return;
                finished = true;
                try
                {
                    if (player.IsOnline())
                        LeaveWorld(player);
                    future?.Cancel(false);
                }
                catch (Exception exception)
                {
                    log.LogError(exception, "Error while processing leave world task for {Player}", player);
                }
                finally
                {
                    leaveWorldTaskByPlayer.TryRemove(new KeyValuePair<Player, LeaveWorldTask>(player, this));
                }
            }
        }
    }

    private sealed class CaptureScope(Action<Player, bool>? previous, bool previousRegistration) : IDisposable
    {
        private int disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) == 0)
            {
                ScopedCapture.Value = previous;
                CaptureRegistration.Value = previousRegistration;
            }
        }
    }
}
