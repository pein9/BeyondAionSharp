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
using Aion.GameServer.Model.Team.Group;
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
/// that class from its generated profile, where the player stands, and leaves a request for a host. A host is the
/// program that plays companions as ordinary clients: it offers the accounts it can log in on (its seats), takes the
/// requests made on them, and logs in. The server never plays a character itself. A companion that leaves the world
/// is deleted, and so is one that nobody came to play.
/// </summary>
public sealed class CompanionService
{
    public const string Switch = "AION_COMPANIONS";
    public const string DirectoryVariable = "AION_COMPANION_DIR";
    public const string Alias = "companion";
    /// <summary>The one level a companion is made at (CB-Q4).</summary>
    public const int Level = 26;

    private const int ElyosAscension = 1006;
    private const int AsmodianAscension = 2008;

    private static readonly ILogger log = AionLog.For(nameof(CompanionService));
    private static readonly CompanionService instance = new();

    private readonly object gate = new();
    private readonly Dictionary<int, string> seats = new();
    private readonly Dictionary<int, Entry> companions = new();
    private Dictionary<(Race Race, PlayerClass PlayerClass), CompanionProfile> profiles = new();
    private volatile bool enabled;

    private sealed class Entry(CompanionRequest request)
    {
        public CompanionRequest Request { get; } = request;
        public CompanionState State { get; set; } = CompanionState.Requested;
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
            Enable(Environment.GetEnvironmentVariable(DirectoryVariable));
    }

    /// <summary>Loads the profiles and registers <c>.companion</c>. A host may call it instead of setting the switch.</summary>
    public void Enable(string? profileDirectory = null)
    {
        string directory = string.IsNullOrWhiteSpace(profileDirectory) ? DefaultDirectory() : profileDirectory!;
        Dictionary<(Race, PlayerClass), CompanionProfile> loaded = CompanionProfile.LoadAll(directory)
            .Where(profile => profile.Level == Level).ToDictionary(profile => (profile.Race, profile.PlayerClass));
        if (loaded.Count == 0)
            throw new InvalidDataException("No level " + Level + " companion profile is at " + directory + ".");
        lock (gate)
        {
            profiles = loaded;
            ChatProcessor processor = ChatProcessor.GetInstance();
            if (!processor.IsCommandExists(PlayerCommand.PREFIX + Alias))
            {
                // The loader takes public commands only, so this one is in no server that did not switch it on. Its
                // level is read once, when it is registered; who may use it afterwards is the command's own rule.
                CommandsConfig.ACCESS_LEVELS ??= new Dictionary<string, sbyte>();
                CommandsConfig.ACCESS_LEVELS.TryAdd(Alias, 0);
                processor.RegisterCommand(new CompanionCommand());
            }
            enabled = true;
        }
        log.LogInformation("Companions are on: " + loaded.Count + " profiles of level " + Level + " from " + directory);
    }

    /// <summary>The command stops answering. Companions that are there stay until they leave or are sent away.</summary>
    public void Disable()
    {
        enabled = false;
    }

    /// <summary>A host can play a companion on this account. An account is a seat for one companion at a time.</summary>
    public void OfferSeat(int accountId, string accountName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountName);
        lock (gate)
            seats[accountId] = accountName;
    }

    /// <summary>The host no longer plays on this account. A companion on it ends.</summary>
    public void WithdrawSeat(int accountId)
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

    /// <summary>The request made on this seat that no host has taken yet, once; null when there is none.</summary>
    public CompanionRequest? TakeRequest(int accountId)
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
            int accountId = seats.Keys.Order().FirstOrDefault(seat => companions.Values.All(companion => companion.Request.AccountId != seat));
            if (accountId == 0)
                return "No companion is free right now.";
            string? name = NewName(playerClass);
            if (name == null)
                return "No name is free for a " + Word(playerClass) + " companion.";
            CompanionRequest request;
            try
            {
                request = Make(leader, profile, accountId, seats[accountId], name);
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
    private static CompanionRequest Make(Player leader, CompanionProfile profile, int accountId, string accountName, string name)
    {
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

            var accountData = new Aion.GameServer.Model.Account.PlayerAccountData(common, profile.NewAppearance());
            Aion.GameServer.Model.Account.Account account = AccountService.LoadAccount(accountId);
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
        entry.State = CompanionState.Ending;
        if (companion != null && companion.IsInGroup())
            PlayerGroupService.RemovePlayer(companion);
    }

    private void Delete(Entry entry)
    {
        companions.Remove(entry.Request.CharacterId);
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
