using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using Aion.GameServer.Configs.Administration;
using Aion.GameServer.Dao;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Skill;
using Aion.GameServer.Model.Team.Common.Legacy;
using Aion.GameServer.Model.Team.Group;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Items;
using Aion.GameServer.Services.Players;
using Aion.GameServer.Utils.ChatHandlers;
using Aion.GameServer.Utils.IdFactory;

namespace Aion.GameServer.Custom.Companion;

/// <summary>What a host needs to play a companion: the account to log in on and the character to choose there.</summary>
public sealed record CompanionRequest(int AccountId, string AccountName, int CharacterId, string Name, PlayerClass PlayerClass,
    int LeaderId, string LeaderName);

/// <summary>Requested: made, and no host has taken it. Playing: a host took it. Ending: sent away; its rows go when it has left the world.</summary>
public enum CompanionState
{
    Requested,
    Playing,
    Ending,
}

public sealed record CompanionInfo(int CharacterId, string Name, PlayerClass PlayerClass, int LeaderId, int AccountId, CompanionState State);

/// <summary>
/// Companion bots (decision D41, docs/companion-bots.md): a feature Java does not have, kept apart from the ported
/// code and off unless <c>AION_COMPANIONS=true</c> or a host switches it on.
///
/// A player who is alone or leads a group asks for a companion of a class. The server makes a throwaway character of
/// that class from its generated profile, where the player stands, on a seat. A host is the program that plays
/// companions as ordinary clients, with nothing but the client protocol: it logs in the accounts named as seats and
/// leaves them at character selection, sees a companion appear in a seat's character list, and enters the world with
/// it. The server never plays a character itself. A companion that leaves the world is deleted, and so is one that
/// nobody came to play.
/// </summary>
public sealed class CompanionService
{
    public const string Switch = "AION_COMPANIONS";
    public const string DirectoryVariable = "AION_COMPANION_DIR";
    /// <summary>The names of the accounts that are seats, separated by commas.</summary>
    public const string AccountsVariable = "AION_COMPANION_ACCOUNTS";
    /// <summary>What a companion's character has for its note: how one is told from any other character.</summary>
    public const string Note = "companion";
    public const string Alias = "companion";
    /// <summary>The one level a companion is made at (CB-Q4).</summary>
    public const int Level = 26;
    /// <summary>How often the server looks at its companions.</summary>
    public const int WatchSeconds = 10;
    /// <summary>A companion that nobody has logged in this long after it was made is deleted.</summary>
    public const int UnplayedSeconds = 120;
    /// <summary>A companion that was sent away and is still in the world this long after is disconnected.</summary>
    public const int LingerSeconds = 30;
    /// <summary>A companion is brought to its player no sooner than this after the last time.</summary>
    public const int BringSeconds = 20;
    /// <summary>A companion's supplies are filled up no sooner than this after the last time.</summary>
    public const int RestockSeconds = 300;

    private const int ElyosAscension = 1006;
    private const int AsmodianAscension = 2008;

    private static readonly ILogger log = AionLog.For(nameof(CompanionService));
    private static readonly CompanionService instance = new();

    private readonly object gate = new();
    private readonly Dictionary<int, Seat> seats = new();
    private readonly HashSet<string> seatNames = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<int> refusedSeats = new();
    private readonly Dictionary<int, Entry> companions = new();
    private Dictionary<(Race Race, PlayerClass PlayerClass), CompanionProfile> profiles = new();
    private volatile bool enabled;
    private bool watching;

    /// <summary>
    /// An account a companion can be made on. One with a connection is a client that waits at character selection
    /// and sees the companion appear in its character list; one without is a host inside the server's own process
    /// that takes its requests from memory.
    /// </summary>
    private sealed record Seat(int AccountId, string Name, AionConnection? Connection);

    private sealed class Entry(CompanionRequest request)
    {
        public CompanionRequest Request { get; } = request;
        public CompanionState State { get; set; } = CompanionState.Requested;
        public long MadeAtMillis { get; } = SystemClock.CurrentMillis();
        /// <summary>It has entered the world and been put into its player's group.</summary>
        public bool Joined { get; set; }
        public long EndingAtMillis { get; set; }
        public long BroughtAtMillis { get; set; } = long.MinValue / 2;
        public long RestockedAtMillis { get; set; } = long.MinValue / 2;
    }

    private CompanionService()
    {
    }

    public static CompanionService GetInstance()
    {
        return instance;
    }

    public bool Enabled => enabled;

    /// <summary>Reads <c>AION_COMPANIONS</c> and <c>AION_COMPANION_DIR</c>. Unset or false leaves the feature off.</summary>
    public void Start()
    {
        string? setting = Environment.GetEnvironmentVariable(Switch);
        if (string.IsNullOrWhiteSpace(setting))
            return;
        if (!bool.TryParse(setting, out bool on))
            throw new InvalidOperationException(Switch + " must be true or false.");
        if (on)
            Enable(Environment.GetEnvironmentVariable(DirectoryVariable),
                (Environment.GetEnvironmentVariable(AccountsVariable) ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    /// <summary>
    /// Loads the profiles and registers <c>.companion</c>. A host may call it instead of setting the switch.
    /// <paramref name="seatAccounts"/> names the accounts that are seats when they wait at character selection.
    /// </summary>
    public void Enable(string? profileDirectory = null, IEnumerable<string>? seatAccounts = null)
    {
        string directory = string.IsNullOrWhiteSpace(profileDirectory) ? DefaultDirectory() : profileDirectory!;
        Dictionary<(Race, PlayerClass), CompanionProfile> loaded = CompanionProfile.LoadAll(directory)
            .Where(profile => profile.Level == Level).ToDictionary(profile => (profile.Race, profile.PlayerClass));
        if (loaded.Count == 0)
            throw new InvalidDataException("No level " + Level + " companion profile is at " + directory + ".");
        lock (gate)
        {
            profiles = loaded;
            seatNames.Clear();
            foreach (string seatName in seatAccounts ?? [])
                seatNames.Add(seatName);
            ChatProcessor processor = ChatProcessor.GetInstance();
            if (!processor.IsCommandExists(PlayerCommand.PREFIX + Alias))
            {
                // The loader takes public commands only, so this one is in no server that did not switch it on. Its
                // level is read once, when it is registered; who may use it afterwards is the command's own rule.
                CommandsConfig.ACCESS_LEVELS ??= new Dictionary<string, sbyte>();
                CommandsConfig.ACCESS_LEVELS.TryAdd(Alias, 0);
                processor.RegisterCommand(new CompanionCommand());
            }
            if (!watching)
            {
                watching = true;
                ThreadPoolManager.GetInstance().ScheduleAtFixedRate(_ => { Watch(); return ValueTask.CompletedTask; }, WatchSeconds * 1000L, WatchSeconds * 1000L);
            }
            enabled = true;
        }
        log.LogInformation("Companions are on: " + loaded.Count + " profiles of level " + Level + " from " + directory
            + "; seat accounts: " + (seatNames.Count == 0 ? "none named" : string.Join(", ", seatNames.Order())));
    }

    /// <summary>The command stops answering. Companions that are there stay until they leave or are sent away.</summary>
    public void Disable()
    {
        enabled = false;
    }

    /// <summary>A seat with no client behind it, for a test in the server's own process that wants a companion made
    /// without playing it. A host's seats are found among the logged-in accounts (<see cref="FindSeats"/>).</summary>
    internal void OfferSeat(int accountId, string accountName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountName);
        lock (gate)
            seats[accountId] = new Seat(accountId, accountName, null);
    }

    /// <summary>The test's seat is taken away. A companion on it ends.</summary>
    internal void WithdrawSeat(int accountId)
    {
        lock (gate)
        {
            seats.Remove(accountId);
            foreach (Entry entry in companions.Values.Where(companion => companion.Request.AccountId == accountId).ToList())
            {
                if (Aion.GameServer.World.World.GetInstance().GetPlayer(entry.Request.CharacterId) == null)
                    Delete(entry);
                else
                    End(entry);
            }
        }
    }

    /// <summary>The request made on a test's seat that was not taken yet, once; null when there is none.</summary>
    internal CompanionRequest? TakeRequest(int accountId)
    {
        lock (gate)
        {
            Entry? entry = companions.Values.FirstOrDefault(companion => companion.Request.AccountId == accountId && companion.State == CompanionState.Requested);
            if (entry == null)
                return null;
            entry.State = CompanionState.Playing;
            return entry.Request;
        }
    }

    public bool IsCompanion(int characterId)
    {
        lock (gate)
            return companions.ContainsKey(characterId);
    }

    /// <summary>The companions of a player, in the order they were asked for.</summary>
    public IReadOnlyList<CompanionInfo> Of(Player leader)
    {
        lock (gate)
            return companions.Values.Where(companion => companion.Request.LeaderId == leader.GetObjectId()).OrderBy(companion => companion.Request.CharacterId)
                .Select(Info).ToArray();
    }

    public IReadOnlyList<CompanionInfo> All()
    {
        lock (gate)
            return companions.Values.OrderBy(companion => companion.Request.CharacterId).Select(Info).ToArray();
    }

    /// <summary>Makes a companion of a class for a player, or says in plain words why not. The answer is for the player.</summary>
    public string Request(Player leader, string className)
    {
        if (!TryParseClass(className, out PlayerClass playerClass))
            return "There is no class called " + className + ".";
        lock (gate)
        {
            if (!enabled)
                return "Companions are switched off.";
            if (!profiles.TryGetValue((leader.GetRace(), playerClass), out CompanionProfile? profile))
                return "No " + Word(playerClass) + " companion is ready for your race.";
            string? refusal = Refusal(leader);
            if (refusal != null)
                return refusal;
            FindSeats();
            Seat? seat = seats.Values.OrderBy(candidate => candidate.AccountId).FirstOrDefault(candidate => Waiting(candidate)
                && companions.Values.All(companion => companion.Request.AccountId != candidate.AccountId));
            if (seat == null)
                return "No companion is free right now.";
            int accountId = seat.AccountId;
            string? name = NewName(playerClass);
            if (name == null)
                return "No name is free for a " + Word(playerClass) + " companion.";
            CompanionRequest request;
            try
            {
                request = Make(leader, profile, seat, name);
            }
            catch (Exception e)
            {
                log.LogError(e, "The companion " + name + " could not be made for " + leader.GetName());
                return "The companion could not be made.";
            }
            companions[request.CharacterId] = new Entry(request);
            log.LogInformation("Companion " + name + " (" + playerClass + ", " + request.CharacterId + ") was made for " + leader.GetName()
                + " on account " + accountId);
            return name + " the " + Word(playerClass) + " is on its way to you.";
        }
    }

    /// <summary>Sends one companion of a player away, or all of them when no name is given.</summary>
    public string Dismiss(Player leader, string? name)
    {
        lock (gate)
        {
            List<Entry> chosen = companions.Values.Where(companion => companion.Request.LeaderId == leader.GetObjectId()
                && companion.State != CompanionState.Ending
                && (name == null || companion.Request.Name.Equals(name, StringComparison.OrdinalIgnoreCase))).ToList();
            if (chosen.Count == 0)
                return name == null ? "You have no companion." : "You have no companion called " + name + ".";
            foreach (Entry entry in chosen)
                End(entry);
            return string.Join(", ", chosen.Select(entry => entry.Request.Name)) + (chosen.Count == 1 ? " leaves you." : " leave you.");
        }
    }

    /// <summary>
    /// The emergency rule (the operator, 2026-10-13): a companion that has found no way to its player for half a
    /// minute asks to be put beside it. Only a companion can ask, and only for itself; where it is put is where its
    /// player stands, in its player's instance. A player in the air is not followed this way: the companion would
    /// be left hanging there.
    /// </summary>
    public string BringToLeader(Player companion)
    {
        Player leader;
        lock (gate)
        {
            if (!companions.TryGetValue(companion.GetObjectId(), out Entry? entry) || entry.State != CompanionState.Playing)
                return "Only a companion that is with its player can be brought to it.";
            long now = SystemClock.CurrentMillis();
            if (now - entry.BroughtAtMillis < BringSeconds * 1000L)
                return "You were brought to your player a moment ago.";
            leader = Aion.GameServer.World.World.GetInstance().GetPlayer(entry.Request.LeaderId);
            if (leader == null || !leader.IsSpawned())
                return "Your player is not in the world.";
            if (!leader.IsDead() && (leader.IsInState(Aion.GameServer.Model.GameObjects.State.CreatureState.FLYING) || leader.IsInState(Aion.GameServer.Model.GameObjects.State.CreatureState.GLIDING)))
                return "Your player is in the air.";
            entry.BroughtAtMillis = now;
        }
        Aion.GameServer.World.WorldMapInstance where = leader.GetWorldMapInstance();
        // Into its player's instance as a member of the group is taken in (PortalService.Transfer registers it).
        if (leader.IsInInstance() && !where.IsRegistered(companion.GetObjectId()))
            where.Register(companion.GetObjectId());
        log.LogInformation("Companion {Name} ({Id}) is brought to {Leader}: map {Map}, instance {Instance}, ({X:0.0}, {Y:0.0}, {Z:0.0})",
            companion.GetName(), companion.GetObjectId(), leader.GetName(), where.GetMapId(), where.GetInstanceId(), leader.GetX(), leader.GetY(), leader.GetZ());
        Aion.GameServer.Services.Teleport.TeleportService.TeleportTo(companion, where, leader.GetX(), leader.GetY(), leader.GetZ(), leader.GetHeading(),
            Aion.GameServer.Model.Animations.TeleportAnimation.FADE_OUT_BEAM);
        return "You are brought to " + leader.GetName() + ".";
    }

    /// <summary>
    /// The stock check (the operator, 2026-10-13): a companion asks for its supplies every half hour, and what it
    /// carries of each thing its profile gave it is filled up to what the profile gave. Only a companion can ask, and
    /// only for itself. Kinah and what it wears are not touched.
    /// </summary>
    public string Restock(Player companion)
    {
        CompanionProfile? profile;
        lock (gate)
        {
            if (!companions.TryGetValue(companion.GetObjectId(), out Entry? entry) || entry.State != CompanionState.Playing)
                return "Only a companion that is with its player has supplies to fill up.";
            long now = SystemClock.CurrentMillis();
            if (now - entry.RestockedAtMillis < RestockSeconds * 1000L)
                return "Your supplies were filled up a moment ago.";
            if (!profiles.TryGetValue((companion.GetRace(), entry.Request.PlayerClass), out profile))
                return "There is no profile to fill your supplies from.";
            entry.RestockedAtMillis = now;
        }
        var filled = new List<string>();
        foreach (CompanionProfile.Carried carried in profile.Inventory)
        {
            long has = companion.GetInventory().GetItemCountByItemId(carried.ItemId);
            if (has >= carried.Count)
                continue;
            long left = ItemService.AddItem(companion, carried.ItemId, carried.Count - has);
            filled.Add(carried.ItemId + "x" + (carried.Count - has - left));
        }
        if (filled.Count == 0)
            return "Your supplies are complete.";
        log.LogInformation("Companion {Name} ({Id}) has its supplies filled up: {Items}", companion.GetName(), companion.GetObjectId(), string.Join(", ", filled));
        return "Your supplies are filled up: " + string.Join(", ", filled) + ".";
    }

    /// <summary>
    /// A player has entered the world. A companion joins its player's group here, the way an invitation that was
    /// accepted does (PlayerGroupInvite.AcceptRequest): the player leads a new group, or its group takes one more.
    /// </summary>
    public void OnEnteredWorld(Player player)
    {
        lock (gate)
        {
            if (!companions.TryGetValue(player.GetObjectId(), out Entry? entry) || entry.State == CompanionState.Ending)
                return;
            Player leader = Aion.GameServer.World.World.GetInstance().GetPlayer(entry.Request.LeaderId);
            if (leader == null || !Aion.GameServer.Restrictions.PlayerRestrictions.CanInviteToGroup(leader, player))
            {
                // Nobody leads it, or the group cannot take it (the restriction told the player why).
                End(entry);
                return;
            }
            PlayerGroup group = leader.GetPlayerGroup();
            if (group != null)
                PlayerGroupService.AddPlayer(group, player);
            else
                group = PlayerGroupService.CreateGroup(leader, player, Aion.GameServer.Model.Team.TeamType.GROUP, 0);
            // CB-Q11: with its first companion a group's loot is free for all, and every quality is picked up as it is.
            // A companion opens no corpse, so every corpse is the player's at once and no roll opens for a companion.
            // The rule is set once: a player who changes it afterwards keeps what he chose.
            entry.Joined = true;
            if (entry.State == CompanionState.Requested)
                entry.State = CompanionState.Playing;
            int joinedId = player.GetObjectId();
            if (!group.GetMembers().Any(member => member.GetObjectId() != joinedId && companions.ContainsKey(member.GetObjectId())))
                PlayerGroupService.ChangeGroupRules(group, new LootGroupRules(LootRuleType.FREEFORALL, 0, 0, 0, 0, 0, 0, 0));
        }
    }

    /// <summary>
    /// A player has left the world and is saved. A companion is deleted here, whatever made it leave. When it is a
    /// player who has companions, they end: nobody leads them any more.
    /// </summary>
    public void OnLeftWorld(Player player)
    {
        lock (gate)
        {
            if (companions.Count == 0)
                return;
            int id = player.GetObjectId();
            if (companions.TryGetValue(id, out Entry? own))
            {
                Delete(own);
                return;
            }
            foreach (Entry entry in companions.Values.Where(companion => companion.Request.LeaderId == id).ToList())
                End(entry);
        }
    }

    /// <summary>
    /// The server's own look at its companions, every <see cref="WatchSeconds"/> seconds: a companion's end does not
    /// depend on the program that plays it. One that nobody logged in is deleted. One whose player is gone, or no
    /// longer leads the group it is in, ends. One that was sent away and stays is disconnected, and is deleted when it
    /// has left the world like any other.
    /// </summary>
    public void Watch()
    {
        lock (gate)
        {
            FindSeats();
            long now = SystemClock.CurrentMillis();
            foreach (Entry entry in companions.Values.ToList())
            {
                Player companion = Aion.GameServer.World.World.GetInstance().GetPlayer(entry.Request.CharacterId);
                if (companion == null)
                {
                    if (now - entry.MadeAtMillis > UnplayedSeconds * 1000L)
                        Delete(entry);
                    continue;
                }
                if (entry.State != CompanionState.Ending)
                {
                    Player leader = Aion.GameServer.World.World.GetInstance().GetPlayer(entry.Request.LeaderId);
                    PlayerGroup group = companion.GetPlayerGroup();
                    bool led = leader != null && group != null && group.IsLeader(leader);
                    if (entry.Joined ? !led : now - entry.MadeAtMillis > UnplayedSeconds * 1000L)
                        End(entry);
                }
                if (entry.State == CompanionState.Ending && now - entry.EndingAtMillis > LingerSeconds * 1000L)
                {
                    log.LogInformation("Companion " + entry.Request.Name + " (" + entry.Request.CharacterId + ") stayed after it was sent away and is disconnected");
                    companion.GetClientConnection()?.Close(Aion.GameServer.Network.Aion.ServerPackets.SM_SYSTEM_MESSAGE.STR_KICK_CHARACTER());
                }
            }
        }
    }

    private static bool Open(AionConnection connection)
    {
        return !connection.IsClosed() && !connection.IsPendingClose();
    }

    /// <summary>The seat can take a companion now: its client is at character selection, or its host is in this process.</summary>
    private static bool Waiting(Seat seat)
    {
        return seat.Connection == null || Open(seat.Connection) && seat.Connection.GetState() == AionConnection.State.AUTHED
            && seat.Connection.GetActivePlayer() == null;
    }

    /// <summary>
    /// Looks at who is logged in. An account named as a seat that waits at character selection becomes one. A seat
    /// whose client is gone is one no longer, and a companion on it ends. Characters with the companion's note on a
    /// new seat are what a server left behind that stopped with companions alive: they are deleted. An account that
    /// holds any other character is no seat, so that a player's account named by mistake loses nothing.
    /// </summary>
    private void FindSeats()
    {
        IReadOnlyDictionary<int, AionConnection> loggedIn = Aion.GameServer.Network.LoginServer.LoginServer.LoggedInAccounts;
        foreach (Seat seat in seats.Values.Where(known => known.Connection != null).ToList())
        {
            if (loggedIn.TryGetValue(seat.AccountId, out AionConnection? current) && ReferenceEquals(current, seat.Connection) && Open(seat.Connection!))
                continue;
            seats.Remove(seat.AccountId);
            log.LogInformation("Companion seat " + seat.Name + " (account " + seat.AccountId + ") is gone");
            foreach (Entry entry in companions.Values.Where(companion => companion.Request.AccountId == seat.AccountId).ToList())
            {
                if (Aion.GameServer.World.World.GetInstance().GetPlayer(entry.Request.CharacterId) == null)
                    Delete(entry);
                else
                    End(entry);
            }
        }
        if (seatNames.Count == 0)
            return;
        foreach ((int accountId, AionConnection connection) in loggedIn)
        {
            Aion.GameServer.Model.Account.Account account = connection.GetAccount();
            if (account == null || seats.ContainsKey(accountId) || !seatNames.Contains(account.GetName()) || !Open(connection)
                || connection.GetState() != AionConnection.State.AUTHED || connection.GetActivePlayer() != null)
                continue;
            bool others = false;
            foreach (Aion.GameServer.Model.Account.PlayerAccountData listed in account.GetPlayerAccDataList())
            {
                PlayerCommonData left = listed.GetPlayerCommonData();
                if (left.GetNote() != Note)
                {
                    others = true;
                    continue;
                }
                account.Remove(listed);
                account.DecrementCountOf(left.GetRace());
                PlayerService.DeletePlayerFromDB(left.GetPlayerObjId());
                log.LogInformation("Companion " + left.GetName() + " (" + left.GetPlayerObjId() + ") was left behind on seat " + account.GetName() + " and is deleted");
            }
            if (others)
            {
                if (refusedSeats.Add(accountId))
                    log.LogWarning("The account " + account.GetName() + " is named as a companion seat and holds a character that is no companion: it is not used");
                continue;
            }
            refusedSeats.Remove(accountId);
            seats[accountId] = new Seat(accountId, account.GetName(), connection);
            log.LogInformation("Companion seat " + account.GetName() + " (account " + accountId + ") is waiting at character selection");
        }
    }

    /// <summary>The seats that could take a companion now, by account name. For a host's and a test's eyes.</summary>
    public IReadOnlyList<string> WaitingSeats()
    {
        lock (gate)
        {
            FindSeats();
            return seats.Values.Where(seat => Waiting(seat) && companions.Values.All(companion => companion.Request.AccountId != seat.AccountId))
                .Select(seat => seat.Name).Order(StringComparer.Ordinal).ToArray();
        }
    }

    /// <summary>What the service knows is dropped and nothing is deleted: a server that stopped, for a test of what it leaves.</summary>
    internal void ForgetForTests()
    {
        lock (gate)
        {
            companions.Clear();
            seats.Clear();
            refusedSeats.Clear();
        }
    }

    private string? Refusal(Player leader)
    {
        int leaderId = leader.GetObjectId();
        if (companions.ContainsKey(leaderId))
            return "A companion cannot bring companions.";
        if (leader.IsInAlliance() || leader.IsInLeague())
            return "Companions join a group, not an alliance.";
        PlayerGroup group = leader.GetPlayerGroup();
        if (group != null && !group.IsLeader(leader))
            return "Only the leader of your group can bring a companion.";
        int members = group?.Size() ?? 1;
        int coming = companions.Values.Count(companion => companion.Request.LeaderId == leaderId
            && (group == null || group.GetMember(companion.Request.CharacterId) == null));
        if (members + coming >= (group?.GetMaxMemberCount() ?? 6))
            return "Your group is full.";
        if (leader.IsInInstance())
            return "Bring your companions before you go into an instance.";
        return null;
    }

    /// <summary>
    /// The character and its rows. It is made the way CM_CREATE_CHARACTER makes one (PlayerService.StoreNewPlayer), with
    /// its profile in place of the starter's skills and items: its class and level, the Ascension that made it a Daeva,
    /// the leader's place and the leader's bind point.
    /// </summary>
    private static CompanionRequest Make(Player leader, CompanionProfile profile, Seat seat, string name)
    {
        int accountId = seat.AccountId;
        string accountName = seat.Name;
        int id = IDFactory.GetInstance().NextId();
        try
        {
            var common = new PlayerCommonData(id);
            common.SetName(name);
            common.SetGender(profile.Gender);
            common.SetRace(profile.Race);
            common.SetPlayerClass(profile.PlayerClass);
            common.SetDaeva(true); // a level of 10 and above is only set for a Daeva
            common.SetLevel(profile.Level); // level (exp) must be set after class
            common.SetMapId(leader.GetWorldId());
            common.SetX(leader.GetX());
            common.SetY(leader.GetY());
            common.SetZ(leader.GetZ());
            common.SetHeading(leader.GetHeading());
            common.SetNote(Note);

            var accountData = new Aion.GameServer.Model.Account.PlayerAccountData(common, profile.NewAppearance());
            // A seat that waits at character selection has its account in its connection, and the character goes into
            // that account as one made by CM_CREATE_CHARACTER does: its client sees it in the list it asks for next.
            Aion.GameServer.Model.Account.Account account = seat.Connection?.GetAccount() ?? AccountService.LoadAccount(accountId);
            account.SetName(accountName);
            var companion = new Player(accountData, account);
            companion.SetPosition(Aion.GameServer.World.World.GetInstance()
                .CreatePosition(common.GetMapId(), common.GetX(), common.GetY(), common.GetZ(), common.GetHeading(), 0));

            // The skills come first: an item is only worn by a character that has the skill to wear it.
            companion.SetSkillList(new PlayerSkillList(profile.Skills
                .Select(skill => new PlayerSkillEntry(companion, skill.Id, skill.Level, IPersistable.PersistentState.NEW)).ToList()));
            foreach (CompanionProfile.Worn worn in profile.Equipment)
            {
                Item item = NewItem(worn.ItemId, worn.Count);
                if (worn.Enchant > 0)
                    item.SetEnchantLevel(worn.Enchant);
                item.SetEquipped(true);
                item.SetEquipmentSlot(worn.Slot);
                companion.GetInventory().OnLoadHandler(item);
                if (!item.IsEquipped())
                    throw new InvalidDataException("A " + profile.PlayerClass + " cannot wear the profile's item " + worn.ItemId + ".");
            }
            foreach (CompanionProfile.Carried carried in profile.Inventory)
                companion.GetInventory().OnLoadHandler(NewItem(carried.ItemId, carried.Count));
            companion.SetMailbox(new Mailbox(companion));
            companion.GetInventory().SetPersistentState(IPersistable.PersistentState.UPDATE_REQUIRED);
            companion.GetEquipment().SetPersistentState(IPersistable.PersistentState.UPDATE_REQUIRED);
            int ascension = profile.Race == Race.ELYOS ? ElyosAscension : AsmodianAscension;
            companion.GetQuestStateList().AddQuest(ascension, new QuestState(ascension, QuestStatus.COMPLETE));

            if (!PlayerService.StoreNewPlayer(companion, accountName, accountId))
                throw new InvalidOperationException("The character's rows could not be written.");
            PlayerDAO.StorePlayer(companion); // the experience of its level, which the insert does not write
            PlayerQuestListDAO.Store(companion);
            PlayerService.StoreCreationTime(id, DateTimeOffset.FromUnixTimeMilliseconds(SystemClock.CurrentMillis()));
            BindPointPosition bind = leader.GetBindPoint();
            if (bind != null)
            {
                companion.SetBindPoint(new BindPointPosition(bind.GetMapId(), bind.GetX(), bind.GetY(), bind.GetZ(), bind.GetHeading()));
                if (!PlayerBindPointDAO.InsertBindPoint(companion))
                    throw new InvalidOperationException("The character's bind point could not be written.");
            }
            if (seat.Connection != null)
            {
                accountData.SetVisibleItems(companion.GetEquipment().GetEquippedForAppearance());
                accountData.SetCreationDate(DateTimeOffset.FromUnixTimeMilliseconds(SystemClock.CurrentMillis()));
                account.AddPlayerAccountData(accountData);
            }
            return new CompanionRequest(accountId, accountName, id, name, profile.PlayerClass, leader.GetObjectId(), leader.GetName());
        }
        catch
        {
            PlayerService.DeletePlayerFromDB(id, false);
            IDFactory.GetInstance().ReleaseId(id);
            throw;
        }
    }

    private static Item NewItem(int itemId, long count)
    {
        if (Aion.GameServer.Dataholders.DataManager.ITEM_DATA.GetItemTemplate(itemId) == null)
            throw new InvalidDataException("The profile's item " + itemId + " does not exist.");
        return ItemFactory.NewItem(itemId, count);
    }

    /// <summary>The companion leaves its group and is marked. One that nobody plays is deleted at once.</summary>
    private void End(Entry entry)
    {
        Player companion = Aion.GameServer.World.World.GetInstance().GetPlayer(entry.Request.CharacterId);
        if (companion == null && entry.State == CompanionState.Requested)
        {
            Delete(entry);
            return;
        }
        if (entry.State != CompanionState.Ending)
            entry.EndingAtMillis = SystemClock.CurrentMillis();
        entry.State = CompanionState.Ending;
        if (companion != null && companion.IsInGroup())
            PlayerGroupService.RemovePlayer(companion);
    }

    private void Delete(Entry entry)
    {
        companions.Remove(entry.Request.CharacterId);
        if (seats.TryGetValue(entry.Request.AccountId, out Seat? seat) && seat.Connection?.GetAccount() is { } account
            && account.GetPlayerAccountData(entry.Request.CharacterId) is { } listed)
        {
            account.Remove(listed);
            account.DecrementCountOf(listed.GetPlayerCommonData().GetRace());
        }
        PlayerService.DeletePlayerFromDB(entry.Request.CharacterId);
        log.LogInformation("Companion " + entry.Request.Name + " (" + entry.Request.CharacterId + ") of " + entry.Request.LeaderName + " is deleted");
    }

    /// <summary>The class's word and a letter: ClericA, ClericB. A name a character already has is passed over.</summary>
    private string? NewName(PlayerClass playerClass)
    {
        string word = Word(playerClass);
        word = char.ToUpperInvariant(word[0]) + word[1..];
        for (int index = 0; index < 26 * 27; index++)
        {
            string name = word + (index < 26 ? "" : ((char)('A' + index / 26 - 1)).ToString()) + (char)('A' + index % 26);
            if (companions.Values.Any(companion => companion.Request.Name == name))
                continue;
            if (!NameRestrictionService.IsValidName(name) || NameRestrictionService.IsForbidden(name) || PlayerService.IsNameUsedOrReserved(null!, name))
                continue;
            return name;
        }
        return null;
    }

    private static CompanionInfo Info(Entry entry)
    {
        return new CompanionInfo(entry.Request.CharacterId, entry.Request.Name, entry.Request.PlayerClass, entry.Request.LeaderId,
            entry.Request.AccountId, entry.State);
    }

    /// <summary>The class a player typed: its name in any case, with or without the space, or the name the 4.8 client shows.</summary>
    public static bool TryParseClass(string? text, out PlayerClass playerClass)
    {
        string word = new string((text ?? "").Where(char.IsLetter).ToArray()).ToLowerInvariant();
        switch (word)
        {
            case "aethertech":
                playerClass = PlayerClass.RIDER;
                return true;
            case "gunslinger":
                playerClass = PlayerClass.GUNNER;
                return true;
            case "songweaver":
                playerClass = PlayerClass.BARD;
                return true;
        }
        foreach (PlayerClass candidate in Enum.GetValues<PlayerClass>())
        {
            if (Word(candidate) == word)
            {
                playerClass = candidate;
                return true;
            }
        }
        playerClass = default;
        return false;
    }

    /// <summary>cleric, spiritmaster: the class's name as one lower-case word.</summary>
    public static string Word(PlayerClass playerClass)
    {
        return playerClass.ToString().Replace("_", "").ToLowerInvariant();
    }

    private static string DefaultDirectory()
    {
        for (DirectoryInfo? directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
        {
            string candidate = Path.Combine(directory.FullName, "game-server", "data", "companions");
            if (Directory.Exists(candidate))
                return candidate;
        }
        return Path.Combine(".", "data", "companions");
    }
}
