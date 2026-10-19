using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Xml.Serialization;
using Aion.Commons.Nio;
using Aion.GameServer.Controllers;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Animations;
using Aion.GameServer.Model.Broker;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.House;
using Aion.GameServer.Model.Items.Storage;
using Aion.GameServer.Model.Team.Legion;
using Aion.GameServer.Model.Templates.Housing;
using Aion.GameServer.Model.Templates.Pet;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Items;
using Aion.GameServer.Services.Mail;
using Aion.GameServer.Services.Players;
using Aion.GameServer.Taskmanager.Tasks;
using Aion.GameServer.TestKit;
using Aion.GameServer.Utils;
using Aion.GameServer.World;
using Aion.GameServer.World.Knownlist;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class MailStoragePortTests
{
    [Fact]
    public void StorageMatchingNamesAndPetBagIdsMatchExactJavaIncludingOverflow()
    {
        using var fixture = Fixture();
        foreach (var c in fixture.RootElement.GetProperty("storageMatches").EnumerateArray())
        {
            var type = StorageType.GetStorageTypeById(c.GetProperty("opened").GetInt32());
            Assert.Equal(c.GetProperty("name").GetString(), type.ToString());
            var manager = new StorageManager(type, 17);
            var allowed = c.GetProperty("allowed").EnumerateArray().Select(v => v.GetInt32()).ToHashSet();
            foreach (var request in StorageType.Values()) Assert.Equal(allowed.Contains(request.GetId()), manager.Matches(request));
        }
        foreach (var c in fixture.RootElement.GetProperty("petCases").EnumerateArray())
            Assert.Equal(c.GetProperty("storage").GetInt32(), new PetFunction { id = c.GetProperty("id").GetInt32() }.GetPetBagStorageId());
        Assert.False(Enum.IsDefined(typeof(TaskId), "EXPRESS_MAIL_USE"));
    }

    [Fact]
    public void ManagerMustStillBeKnownAndOnlyRegularWarehouseAlsoOpensAccountWarehouse()
    {
        using var s = new Scope(); var npc = s.Npc(); var other = s.Npc();
        s.P.SetStorageManager(StorageType.REGULAR_WAREHOUSE, npc);
        Assert.Same(npc, s.P.GetStorageManager(StorageType.ACCOUNT_WAREHOUSE)); Assert.Null(s.P.GetStorageManager(StorageType.LEGION_WAREHOUSE));
        s.P.UnsetStorageManager(other); Assert.Same(npc, s.P.GetStorageManager(StorageType.REGULAR_WAREHOUSE));
        s.P.SetStorageManager(StorageType.ACCOUNT_WAREHOUSE, npc); Assert.Null(s.P.GetStorageManager(StorageType.REGULAR_WAREHOUSE));
        s.Known.Remove(npc.GetObjectId(), out _); Assert.Null(s.P.GetStorageManager(StorageType.ACCOUNT_WAREHOUSE));
        s.Know(npc); s.P.UnsetStorageManager(npc); Assert.Null(s.P.GetStorageManager(StorageType.ACCOUNT_WAREHOUSE));
    }

    [Fact]
    public void WarehouseRequiresOpeningTheMatchingNpcAndRemainingInTalkRange()
    {
        using var s = new Scope(); var npc = s.Npc();
        Assert.False(ItemRestrictionService.IsStorageOpen(s.P, StorageType.REGULAR_WAREHOUSE));
        s.P.SetStorageManager(StorageType.REGULAR_WAREHOUSE, npc);
        Assert.True(ItemRestrictionService.IsStorageOpen(s.P, StorageType.REGULAR_WAREHOUSE)); Assert.True(ItemRestrictionService.IsStorageOpen(s.P, StorageType.ACCOUNT_WAREHOUSE));
        Assert.False(ItemRestrictionService.IsStorageOpen(s.P, StorageType.LEGION_WAREHOUSE));
        npc.GetPosition().SetXYZH(s.P.GetX() + 100, null, null, null);
        Assert.False(ItemRestrictionService.IsStorageOpen(s.P, StorageType.REGULAR_WAREHOUSE));
        Assert.True(ItemRestrictionService.IsItemRestrictedFrom(s.P, null!, StorageType.REGULAR_WAREHOUSE));
        Assert.True(ItemRestrictionService.IsItemRestrictedTo(s.P, null!, StorageType.REGULAR_WAREHOUSE));
        Assert.True(ItemRestrictionService.IsStorageOpen(s.P, StorageType.CUBE)); Assert.True(ItemRestrictionService.IsStorageOpen(s.P, StorageType.MAILBOX));
    }

    [Fact]
    public void LegionWarehouseLockBelongsToOneMember()
    {
        using var s = new Scope(); var legion = new Legion(987611, "PortFixture"); var member = new LegionMember(s.P.GetObjectId(), legion); member.SetRank(LegionRank.BRIGADE_GENERAL);
        Set(s.P, "legionMember", member); var warehouse = legion.GetLegionWarehouse();
        Assert.False(ItemRestrictionService.IsLegionWarehouseUser(s.P)); Set(warehouse, "currentUser", s.P.GetObjectId());
        Assert.True(ItemRestrictionService.IsLegionWarehouseUser(s.P)); Set(warehouse, "currentUser", s.Other.GetObjectId());
        Assert.False(ItemRestrictionService.IsLegionWarehouseUser(s.P));
    }

    [Fact]
    public void PetBagRequiresTheCurrentlySummonedPetThatOwnsThatStorageId()
    {
        using var s = new Scope(); Assert.False(ItemRestrictionService.IsStorageOpen(s.P, StorageType.PET_BAG_6));
        var pet = (Pet)RuntimeHelpers.GetUninitializedObject(typeof(Pet));
        Set(pet, "_objectTemplate", new PetTemplate { petFunctions = [new PetFunction { type = PetFunctionType.WAREHOUSE, id = 3 }] }); s.P.SetPet(pet);
        Assert.True(ItemRestrictionService.IsStorageOpen(s.P, StorageType.PET_BAG_6)); Assert.False(ItemRestrictionService.IsStorageOpen(s.P, StorageType.PET_BAG_12));
        s.P.SetPet(null!); Assert.False(ItemRestrictionService.IsStorageOpen(s.P, StorageType.PET_BAG_6));
    }

    [Fact]
    public void CabinetRequiresActiveHouseMatchingWarehouseCurrentOccupantAndTalkRange()
    {
        using var s = new Scope(); Set(s.P, "houses", new List<House>()); Assert.False(ItemRestrictionService.IsStorageOpen(s.P, StorageType.HOUSE_STORAGE_01));
        var house = (House)RuntimeHelpers.GetUninitializedObject(typeof(House)); var registry = new HouseRegistry(house); Set(house, "houseRegistry", registry); Set(s.P, "houses", new List<House> { house });
        var cabinet = s.HouseObject<StorageObject>(new HousingStorage { warehouseId = 1, talkingDistance = 3 });
        ((Dictionary<int, HouseObject>)Get(registry, "objects")).Add(cabinet.GetObjectId(), cabinet);
        Assert.False(ItemRestrictionService.IsStorageOpen(s.P, StorageType.HOUSE_STORAGE_01));
        Assert.True(cabinet.SetOccupant(s.Other)); Assert.False(cabinet.IsOccupiedBy(s.P)); Assert.False(ItemRestrictionService.IsStorageOpen(s.P, StorageType.HOUSE_STORAGE_01));
        Assert.True(cabinet.ReleaseOccupant(s.Other)); Assert.True(cabinet.SetOccupant(s.P)); Assert.True(cabinet.IsOccupiedBy(s.P));
        Assert.True(ItemRestrictionService.IsStorageOpen(s.P, StorageType.HOUSE_STORAGE_01)); Assert.False(ItemRestrictionService.IsStorageOpen(s.P, StorageType.HOUSE_STORAGE_02));
        cabinet.GetPosition().SetXYZH(s.P.GetX() + 100, null, null, null); Assert.False(ItemRestrictionService.IsStorageOpen(s.P, StorageType.HOUSE_STORAGE_01));
        house.SetInactive(true); Assert.False(ItemRestrictionService.IsStorageOpen(s.P, StorageType.HOUSE_STORAGE_01));
    }

    [Fact]
    public void MailboxOpenersBindNpcAndHousePostboxAndClosingUnsetsOnlyMatchingManager()
    {
        using var s = new Scope(); var npc = s.Npc();
        Invoke(npc.GetAi(), "HandleDialogStart", s.P); Assert.Equal(PlayerMailboxState.REGULAR, s.P.GetMailbox().mailBoxState); Assert.Same(npc, s.P.GetStorageManager(StorageType.MAILBOX));
        var houseBox = s.HouseObject<PostboxObject>(new HousingPostbox()); s.Know(houseBox); houseBox.OnUse(s.P);
        Assert.Same(houseBox, s.P.GetStorageManager(StorageType.MAILBOX)); Assert.True(houseBox.IsOccupiedBy(s.P));
        s.P.GetMailbox().Open(true, npc); Assert.Equal(PlayerMailboxState.EXPRESS, s.P.GetMailbox().mailBoxState);
        DialogService.OnCloseDialog(s.P, npc); Assert.Equal(PlayerMailboxState.CLOSED, s.P.GetMailbox().mailBoxState); Assert.Null(s.P.GetStorageManager(StorageType.MAILBOX));
        DialogService.OnCloseDialog(s.P, null!); // No target and no open manager is allowed, as in Java.
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void MailOperationsRefuseRemoteOrUnopenedMailboxBeforeTouchingLettersOrDatabase(int operation)
    {
        using var s = new Scope(); var letter = s.Letter(); s.P.GetMailbox().PutLetterToMailbox(letter);
        if (operation == 0) MailService.ReadMail(s.P, letter.GetObjectId());
        else if (operation == 1) MailService.GetAttachments(s.P, letter.GetObjectId(), 1);
        else if (operation == 2) MailService.DeleteMail(s.P, [letter.GetObjectId()]);
        else MailService.SendMail(s.P, "Recipient", "title", "body", 0, 0, 0, LetterType.NORMAL);
        Assert.True(letter.IsUnread()); Assert.Equal(17, letter.GetAttachedKinah()); Assert.Same(letter, s.P.GetMailbox().GetLetterFromMailbox(letter.GetObjectId()));
        Assert.Single(s.Packets); Assert.IsType<SM_SYSTEM_MESSAGE>(s.Packets[0]);
    }

    [Fact]
    public void ForeignLetterDeletionIsRejectedBeforeDaoAndOwnedReadStillWorks()
    {
        using var s = new Scope(); var npc = s.Npc(); s.P.GetMailbox().Open(false, npc); var letter = s.Letter(); s.P.GetMailbox().PutLetterToMailbox(letter); s.ClearPackets();
        MailService.DeleteMail(s.P, [letter.GetObjectId() + 100]); Assert.Empty(s.Packets); Assert.Same(letter, s.P.GetMailbox().GetLetterFromMailbox(letter.GetObjectId()));
        MailService.ReadMail(s.P, letter.GetObjectId()); Assert.False(letter.IsUnread()); Assert.IsType<SM_MAIL_SERVICE>(Assert.Single(s.Packets));
        npc.GetPosition().SetXYZH(s.P.GetX() + 100, null, null, null); s.ClearPackets(); MailService.DeleteMail(s.P, [letter.GetObjectId()]);
        Assert.Same(letter, s.P.GetMailbox().GetLetterFromMailbox(letter.GetObjectId())); Assert.IsType<SM_SYSTEM_MESSAGE>(Assert.Single(s.Packets));
    }

    [Fact]
    public void OnlySuccessfulActiveLetterRemovalRefillsOverflowAndUpdatesMailboxCount()
    {
        using var s = new Scope(); for (int i = 0; i < 101; i++) s.P.GetMailbox().PutLetterToMailbox(s.Letter());
        var mailbox = s.P.GetMailbox(); var active = mailbox.GetLetters().First();
        s.P.GetCommonData().SetMailboxLetters(101); Assert.False(mailbox.RemoveLetter(-98765)); Assert.Equal(101, s.P.GetCommonData().GetMailboxLetters()); Assert.Equal(100, mailbox.GetLetters().Count);
        Assert.True(mailbox.RemoveLetter(active.GetObjectId())); Assert.Null(mailbox.GetLetterFromMailbox(active.GetObjectId())); Assert.Equal(100, mailbox.GetLetters().Count); Assert.Equal(100, s.P.GetCommonData().GetMailboxLetters());
    }

    [Fact]
    public void CubeSplitsKeepAbsoluteExpiryRegisterTimerAndRetainSourceAndMergeBehavior()
    {
        using var s = new Scope(); var source = s.Item(10, true); source.SetExpireTime(1234567);
        ItemSplitService.SplitItem(s.P, source.GetObjectId(), 0, 3, 2, 0, 0);
        var split = Assert.Single(s.P.GetInventory().GetItemsByItemId(source.GetItemId()), i => i != source);
        Assert.Equal(3, split.GetItemCount()); Assert.Equal(7, source.GetItemCount()); Assert.Equal(1234567, split.GetExpireTime()); Assert.Equal(2, split.GetEquipmentSlot());
        Assert.True(s.Expirables.ContainsKey(split)); ItemSplitService.SplitItem(s.P, split.GetObjectId(), source.GetObjectId(), 2, 0, 0, 0);
        Assert.Equal(9, source.GetItemCount()); Assert.Equal(1, split.GetItemCount()); Assert.Equal(1234567, source.GetExpireTime());
    }

    [Fact]
    public void ClosedWarehouseSameStorageSplitLeavesStacksUntouched()
    {
        using var s = new Scope(); var warehouse = new PlayerStorage(s.P, StorageType.REGULAR_WAREHOUSE); warehouse.SetOwner(s.P); Set(s.P, "regularWarehouse", warehouse); var item = s.Item(10); warehouse.OnLoadHandler(item);
        ItemSplitService.SplitItem(s.P, item.GetObjectId(), 0, 3, 2, 1, 1); Assert.Equal(10, item.GetItemCount()); Assert.Single(warehouse.GetItems());
        var npc = s.Npc(); s.P.SetStorageManager(StorageType.REGULAR_WAREHOUSE, npc);
        ItemSplitService.SplitItem(s.P, item.GetObjectId(), 0, 3, 2, 1, 1); Assert.Equal(7, item.GetItemCount()); Assert.Equal(2, warehouse.GetItems().Count);
    }

    [Fact]
    public void KinahSplitCannotReachTheAccountWarehouseWithoutItsManagerEvenWhenPacketClaimsCubeToCube()
    {
        using var s = new Scope(); var kinah = new Item(987699, DataManager.ITEM_DATA.GetItemTemplate(182400001)); kinah.SetItemCount(100); s.P.GetInventory().OnLoadHandler(kinah);
        var account = new PlayerStorage(s.P, StorageType.ACCOUNT_WAREHOUSE); s.P.GetAccount().SetAccountWarehouse(account);
        var stored = new Item(987698, kinah.GetItemTemplate()); stored.SetItemCount(0); account.OnLoadHandler(stored);
        ItemSplitService.SplitItem(s.P, kinah.GetObjectId(), 0, 10, 0, 0, 0); Assert.Equal(100, kinah.GetItemCount()); Assert.Equal(0, stored.GetItemCount());
        var npc = s.Npc(); s.P.SetStorageManager(StorageType.REGULAR_WAREHOUSE, npc);
        ItemSplitService.SplitItem(s.P, kinah.GetObjectId(), 0, 10, 0, 0, 0); Assert.Equal(90, kinah.GetItemCount()); Assert.Equal(10, stored.GetItemCount());
    }

    [Fact]
    public void LegionKinahReaderMatchesExactJavaSignedAmountsAndUnsignedActionIds()
    {
        using var s = new Scope(); using var fixture = Fixture();
        foreach (var c in fixture.RootElement.GetProperty("kinahPackets").EnumerateArray())
        {
            var packet = new CM_LEGION_WH_KINAH(0, new HashSet<AionConnection.State>());
            packet.SetConnection(s.P.GetClientConnection()); packet.SetBuffer(ByteBuffer.Wrap(Convert.FromHexString(c.GetProperty("bodyHex").GetString()!)).Order(ByteOrder.LITTLE_ENDIAN));
            Assert.True(packet.Read()); Assert.Equal(0, packet.GetRemainingBytes()); Assert.Equal(c.GetProperty("amount").GetInt64(), (long)Get(packet, "amount"));
            Assert.Equal(c.GetProperty("action").GetString(), Get(packet, "action")?.ToString());
        }
    }

    [Theory]
    [InlineData(0)] [InlineData(1)]
    public void LegionKinahPacketsCannotUseAnotherMembersWarehouseLock(byte action)
    {
        using var s = new Scope(); var legion = new Legion(987611, "PortFixture"); var member = new LegionMember(s.P.GetObjectId(), legion); member.SetRank(LegionRank.BRIGADE_GENERAL); Set(s.P, "legionMember", member);
        var warehouse = legion.GetLegionWarehouse(); Set(warehouse, "currentUser", s.Other.GetObjectId());
        var carried = new Item(987699, DataManager.ITEM_DATA.GetItemTemplate(182400001)); carried.SetItemCount(100); s.P.GetInventory().OnLoadHandler(carried);
        var stored = new Item(987698, carried.GetItemTemplate()); stored.SetItemCount(100); warehouse.OnLoadHandler(stored);
        var packet = new CM_LEGION_WH_KINAH(0, new HashSet<AionConnection.State>()); packet.SetConnection(s.P.GetClientConnection());
        var body = new byte[9]; System.Buffers.Binary.BinaryPrimitives.WriteInt64LittleEndian(body, 10); body[8] = action;
        packet.SetBuffer(ByteBuffer.Wrap(body).Order(ByteOrder.LITTLE_ENDIAN)); Assert.True(packet.Read()); Invoke(packet, "RunImpl");
        Assert.Equal(100, carried.GetItemCount()); Assert.Equal(100, stored.GetItemCount());
    }

    [Fact]
    public void BrokerQuantityGateMatchesJavaBeforeInventoryFullOrAnyDebits()
    {
        using var s = new Scope(); using var fixture = Fixture(); s.P.GetInventory().SetLimit(0);
        foreach (var c in fixture.RootElement.GetProperty("brokerCases").EnumerateArray())
        {
            var broker = (BrokerService)RuntimeHelpers.GetUninitializedObject(typeof(BrokerService)); var item = s.Item(10);
            var listing = new BrokerItem(item, 10, s.Other.GetObjectId(), c.GetProperty("split").GetBoolean(), BrokerRace.ELYOS);
            Set(broker, "elyosBrokerItems", new ConcurrentDictionary<int, BrokerItem>([new(item.GetObjectId(), listing)])); s.ClearPackets();
            broker.BuyBrokerItem(s.P, item.GetObjectId(), c.GetProperty("count").GetInt64());
            Assert.Equal(c.GetProperty("rejected").GetBoolean() ? 0 : 1, s.Packets.Length); Assert.Equal(10, listing.GetItemCount()); Assert.False(listing.IsSold());
        }
    }

    [Fact]
    public void CooldownUsesJavaSignedMillisAndPrunesOnlyStrictlyExpiredEntries()
    {
        using var s = new Scope(); using var fixture = Fixture();
        foreach (var c in fixture.RootElement.GetProperty("cooldowns").EnumerateArray())
        {
            s.Cooldowns.Clear(); long now = c.GetProperty("now").GetInt64(); s.Common.Now = now;
            s.Cooldowns[2] = unchecked(now - 1); s.Cooldowns[3] = now; s.Cooldowns[4] = unchecked(now + 1);
            CM_READ_EXPRESS_MAIL.StartPostmanCooldown(s.P);
            foreach (var expected in c.GetProperty("expected").EnumerateObject())
                Assert.Equal(expected.Value.GetInt64(), s.Cooldowns[expected.Name == "1" ? s.P.GetObjectId() : int.Parse(expected.Name)]);
            Assert.False(s.Cooldowns.ContainsKey(2)); Assert.Equal(c.GetProperty("expected").EnumerateObject().Count(), s.Cooldowns.Count);
        }
    }

    [Fact]
    public void AttachedKinahCannotOverflowCommissionArithmeticPastTheActualWallet()
    {
        using var s = new Scope(); var npc = s.Npc(); s.P.GetMailbox().Open(false, npc); s.ClearPackets();
        Assert.Same(s.Other, s.Common.H.World.GetPlayer(s.Other.GetName()));
        MailService.SendMail(s.P, s.Other.GetName(), "title", "body", 0, 0, long.MaxValue, LetterType.NORMAL);
        Assert.IsType<SM_SYSTEM_MESSAGE>(Assert.Single(s.Packets)); Assert.Equal(0, s.P.GetInventory().GetKinah());
    }

    [Fact]
    public void ActualBrokerSplitRetainsExpiryAndQueuesPersistenceWithoutResettingTheOriginalStack()
    {
        using var s = new Scope(); var source = s.Item(10); source.SetExpireTime(1234567);
        var kinah = new Item(987699, DataManager.ITEM_DATA.GetItemTemplate(182400001)); kinah.SetItemCount(1000); s.P.GetInventory().OnLoadHandler(kinah);
        var broker = (BrokerService)RuntimeHelpers.GetUninitializedObject(typeof(BrokerService));
        var active = new ConcurrentDictionary<int, BrokerItem>(); var settled = new ConcurrentDictionary<int, BrokerItem>();
        var listing = new BrokerItem(source, 10, s.Other.GetObjectId(), true, BrokerRace.ELYOS); active[source.GetObjectId()] = listing;
        Set(broker, "elyosBrokerItems", active); Set(broker, "elyosSettledItems", settled);
        Set(broker, "playerBrokerCache", new ConcurrentDictionary<int, BrokerPlayerCache>());
        var saves = new BrokerService.BrokerPeriodicTaskManager(6000); Set(broker, "saveManager", saves);
        broker.BuyBrokerItem(s.P, source.GetObjectId(), 3);
        Assert.Equal(7, listing.GetItemCount()); Assert.Same(listing, active[source.GetObjectId()]); Assert.Equal(1234567, source.GetExpireTime());
        var bought = Assert.Single(s.P.GetInventory().GetItemsByItemId(source.GetItemId())); Assert.Equal(3, bought.GetItemCount()); Assert.Equal(1234567, bought.GetExpireTime());
        Assert.Equal(970, s.P.GetInventory().GetKinah()); Assert.True(Assert.Single(settled).Value.IsSold());
        Assert.Equal(4, ((ConcurrentQueue<BrokerService.BrokerOpSaveTask>)Get(saves, "tasks")).Count);
    }

    [Fact]
    public void CourierWaitsTwoMinutesAndOwnerDialogRenewsServiceForTenMinutes()
    {
        using var s = new Scope(); var courier = s.Courier(); var ai = courier.GetAi();
        var tasks = (ConcurrentDictionary<int, ScheduledTask>)Get(courier.GetController(), "tasks"); var wait = tasks[(int)TaskId.DESPAWN];
        Invoke(ai, "HandleDialogStart", s.Other); Assert.Same(wait, tasks[(int)TaskId.DESPAWN]); Assert.Equal(PlayerMailboxState.CLOSED, s.P.GetMailbox().mailBoxState);
        Invoke(ai, "HandleDialogStart", s.P); Assert.True(wait.IsCancelled); Assert.NotSame(wait, tasks[(int)TaskId.DESPAWN]);
        Assert.Equal(PlayerMailboxState.EXPRESS, s.P.GetMailbox().mailBoxState); Assert.Same(courier, s.P.GetStorageManager(StorageType.MAILBOX));
        s.Common.H.Clock.Advance(TimeSpan.FromMinutes(2)); Assert.Same(courier, s.P.GetPostman());
        Invoke(ai, "HandleDialogStart", s.P); // Talking again renews the entire ten-minute service period.
        s.Common.H.Clock.Advance(TimeSpan.FromMilliseconds(599999)); Assert.Same(courier, s.P.GetPostman());
        s.Common.H.Clock.Advance(TimeSpan.FromMilliseconds(1)); Assert.Null(s.P.GetPostman()); Assert.Null(s.P.GetStorageManager(StorageType.MAILBOX));
        Assert.Null(Get(s.P, "storageManager"));
        Assert.Equal(390268, Get(ai, "despawnMessageId"));
        Assert.Equal(s.Common.Now + 600000, s.Cooldowns[s.P.GetObjectId()]); Assert.Empty(s.Common.H.Clock.Faults);
    }

    [Fact]
    public void CourierLeavesAfterInitialWaitAndOnlyLosingItsOwnerDeletesIt()
    {
        using var s = new Scope(); var courier = s.Courier();
        Invoke(courier.GetAi(), "HandleCreatureNotSee", s.Other); Assert.Same(courier, s.P.GetPostman());
        s.Common.H.Clock.Advance(TimeSpan.FromMilliseconds(119999)); Assert.Same(courier, s.P.GetPostman());
        s.Common.H.Clock.Advance(TimeSpan.FromMilliseconds(1)); Assert.Null(s.P.GetPostman());
        courier = s.Courier(); Invoke(courier.GetAi(), "HandleCreatureNotSee", s.P); Assert.Null(s.P.GetPostman());
        Assert.Empty(s.Common.H.Clock.Faults);
    }

    [Fact]
    public void NativeCourierDialogCloseFallbackStillDeletesAndStartsCooldownAndOldCourierCannotClearReplacement()
    {
        using var s = new Scope(); var old = s.Courier(); var current = s.Courier(); s.P.GetMailbox().Open(true, current);
        old.GetController().Delete(); Assert.Same(current, s.P.GetPostman()); Assert.Same(current, s.P.GetStorageManager(StorageType.MAILBOX)); Assert.False(s.Cooldowns.ContainsKey(s.P.GetObjectId()));
        DialogService.OnCloseDialog(s.P, current); Assert.Null(s.P.GetPostman()); Assert.Null(s.P.GetStorageManager(StorageType.MAILBOX));
        Assert.Null(Get(s.P, "storageManager"));
        Assert.Equal(PlayerMailboxState.CLOSED, s.P.GetMailbox().mailBoxState); Assert.Equal(s.Common.Now + 600000, s.Cooldowns[s.P.GetObjectId()]);
    }

    [Theory]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    [InlineData(true, true, true)]
    public void ExpressRequestHonorsStrictCooldownBoundaryAndBlackcloudBypass(bool blackcloud, bool cooling, bool spawned)
    {
        using var s = new Scope(); var letter = new Letter(987697, s.P.GetObjectId(), null!, 0, "Title", "Body", "Sender", DateTime.UnixEpoch, true, blackcloud ? LetterType.BLACKCLOUD : LetterType.EXPRESS);
        s.P.GetMailbox().PutLetterToMailbox(letter); s.Cooldowns[s.P.GetObjectId()] = s.Common.Now + (cooling ? 1 : 0);
        var packet = new CM_READ_EXPRESS_MAIL(0, new HashSet<AionConnection.State>()); packet.SetConnection(s.P.GetClientConnection()); packet.SetBuffer(ByteBuffer.Wrap([1]).Order(ByteOrder.LITTLE_ENDIAN));
        Assert.True(packet.Read()); Invoke(packet, "RunImpl");
        Assert.Equal(spawned, s.P.GetPostman() != null);
        if (spawned)
        {
            Assert.IsType<DeliveryManAI>(s.P.GetPostman().GetAi()); Assert.Same(s.P, s.P.GetPostman().GetTarget()); Assert.Equal(s.Common.Now + 600000, s.Cooldowns[s.P.GetObjectId()]);
            s.Common.Now += 500; packet.SetBuffer(ByteBuffer.Wrap([0]).Order(ByteOrder.LITTLE_ENDIAN)); Assert.True(packet.Read()); Invoke(packet, "RunImpl");
            Assert.Null(s.P.GetPostman()); Assert.Equal(s.Common.Now + 600000, s.Cooldowns[s.P.GetObjectId()]);
        }
        else Assert.IsType<SM_SYSTEM_MESSAGE>(Assert.Single(s.Packets));
    }

    [Fact]
    public void NoUnreadMailDoesNotSpawnCourierAndWarehouseDialogBindsRegularAndAccountAccess()
    {
        using var s = new Scope(); var packet = new CM_READ_EXPRESS_MAIL(0, new HashSet<AionConnection.State>()); packet.SetConnection(s.P.GetClientConnection());
        packet.SetBuffer(ByteBuffer.Wrap([1]).Order(ByteOrder.LITTLE_ENDIAN)); Assert.True(packet.Read()); Invoke(packet, "RunImpl");
        Assert.Null(s.P.GetPostman()); Assert.Empty(s.Packets); Assert.False(s.Cooldowns.ContainsKey(s.P.GetObjectId()));
        var npc = s.Npc(); DialogService.OnDialogSelect(DialogAction.DEPOSIT_CHAR_WAREHOUSE, s.P, npc, 0, 0);
        Assert.Same(npc, s.P.GetStorageManager(StorageType.REGULAR_WAREHOUSE)); Assert.Same(npc, s.P.GetStorageManager(StorageType.ACCOUNT_WAREHOUSE));
    }

    private static JsonDocument Fixture() => JsonDocument.Parse(File.ReadAllText(Path.Combine(RealStaticData.RepoRoot(), "parity-artifacts/golden/formulas/MailStorage.a17e66f10.json")));
    private static object Get(object obj, string name) => Field(obj.GetType(), name).GetValue(obj)!;
    private static void Set(object obj, string name, object? value) => Field(obj.GetType(), name).SetValue(obj, value);
    private static FieldInfo Field(Type type, string name) { for (Type? t = type; t != null; t = t.BaseType) { var f = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly); if (f != null) return f; } throw new MissingFieldException(type.Name, name); }
    private static void Invoke(object obj, string name, params object[] args) => obj.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(obj, args);

    private sealed class Scope : IDisposable
    {
        internal PvpExpPortTests.Scope Common { get; } = new(); internal Player P { get; } internal Player Other { get; }
        private readonly FieldInfo shutdown = typeof(ShutdownHook).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic)!;
        private readonly object? previousShutdown; private readonly KeyValuePair<int, long>[] previousCooldowns; private int id = 987700;
        internal ConcurrentDictionary<int, long> Cooldowns => (ConcurrentDictionary<int, long>)typeof(CM_READ_EXPRESS_MAIL).GetField("cooldownByPlayer", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        internal ConcurrentDictionary<int, KnownObject> Known => (ConcurrentDictionary<int, KnownObject>)Get(P.GetKnownList(), "KnownObjects");
        internal ConcurrentDictionary<IExpirable, Player> Expirables => (ConcurrentDictionary<IExpirable, Player>)Get(ExpireTimerTask.GetInstance(), "expirables");
        internal AionServerPacket[] Packets => ((PvpExpPortTests.Connection)P.GetClientConnection()).Packets;
        internal Scope()
        {
            previousShutdown = shutdown.GetValue(null); shutdown.SetValue(null, null); previousCooldowns = Cooldowns.ToArray();
            using var input = new StringReader("<item_templates><item_template id='162000052' name='Stack' level='1' quality='COMMON' max_stack_count='10' mask='6' price='100'/><item_template id='182400001' name='Kinah' level='1' quality='COMMON' max_stack_count='2147483647'/></item_templates>");
            var items = (ItemData)new XmlSerializer(typeof(ItemData)).Deserialize(input)!; items.AfterUnmarshal(null!); Set(Common.H.Data.StaticData, "<ItemDataDh>k__BackingField", items);
            P = Common.Player(65); Other = Common.Player(65); P.SetMailbox(new Mailbox(P)); Other.SetBlockList(new BlockList()); Common.Now = 1000; ClearPackets();
        }
        internal void ClearPackets() => ((PvpExpPortTests.Connection)P.GetClientConnection()).Clear();
        internal void Know(VisibleObject obj) => Known[obj.GetObjectId()] = new KnownObject(obj);
        internal Npc Npc() { var npc = Common.H.SpawnWithAi(203620, "postbox", P.GetX(), P.GetY(), P.GetZ()); Know(npc); ClearPackets(); return npc; }
        internal Npc Courier() { var npc = Common.H.SpawnWithAi(203620, "deliveryman", P.GetX(), P.GetY(), P.GetZ()); npc.SetCreatorId(P.GetObjectId()); P.SetPostman(npc); Know(npc); ClearPackets(); return npc; }
        internal Item Item(int count, bool owned = false) { var item = new Item(id++, DataManager.ITEM_DATA.GetItemTemplate(162000052)); item.SetItemCount(count); if (owned) P.GetInventory().OnLoadHandler(item); return item; }
        internal Letter Letter() => new(id++, P.GetObjectId(), null!, 17, "Title", "Body", "Sender", DateTime.UnixEpoch, true, LetterType.EXPRESS);
        internal T HouseObject<T>(PlaceableHouseObject template) where T : UseableHouseObject
        {
            var obj = (T)RuntimeHelpers.GetUninitializedObject(typeof(T)); Set(obj, "_objectId", id++); Set(obj, "_objectTemplate", template); Set(obj, "usingPlayer", new AtomicInteger());
            Set(obj, "Position", new WorldPosition(P.GetWorldId(), P.GetX(), P.GetY(), P.GetZ(), 0, P.GetPosition().GetMapRegion())); Set(obj, "x", P.GetX());
            return obj;
        }
        public void Dispose()
        {
            Cooldowns.Clear(); foreach (var entry in previousCooldowns) Cooldowns[entry.Key] = entry.Value;
            ExpireTimerTask.GetInstance().UnregisterExpirables(P); ExpireTimerTask.GetInstance().UnregisterExpirables(Other); Common.Dispose(); shutdown.SetValue(null, previousShutdown);
        }
    }
}
