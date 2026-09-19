using System.Reflection;
using System.Runtime.CompilerServices;
using Aion.GameServer.Controllers;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates.Npc;
using Aion.GameServer.Model.Templates.Spawns;
using Aion.GameServer.Model.Templates.Stats;
using Aion.GameServer.Services;
using Aion.GameServer.Utils.IdFactory;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class NullableEnumEdgeParityTests : IDisposable
{
    private const int UnmappedNpcId = 920015;
    private readonly DataManager? previousDataManager = DataManager.GetRegisteredInstance();

    [Fact]
    public void CanonicalCraftServiceReturnsNullForUnmappedNpc()
    {
        Assert.Null(Aion.GameServer.Services.Craft.CraftSkillUpdateService.GetInstance().GetProfessionByNpc(BuildUnmappedCraftNpc()));
    }

    [Theory]
    [InlineData(204096, 830150, Aion.GameServer.Model.Craft.Profession.ESSENCETAPPING)]
    [InlineData(204257, 830148, Aion.GameServer.Model.Craft.Profession.AETHERTAPPING)]
    [InlineData(204100, 830142, Aion.GameServer.Model.Craft.Profession.COOKING)]
    [InlineData(204104, 830146, Aion.GameServer.Model.Craft.Profession.WEAPONSMITHING)]
    [InlineData(204106, 830144, Aion.GameServer.Model.Craft.Profession.ARMORSMITHING)]
    [InlineData(204110, 830136, Aion.GameServer.Model.Craft.Profession.TAILORING)]
    [InlineData(204102, 830138, Aion.GameServer.Model.Craft.Profession.ALCHEMY)]
    [InlineData(204108, 830140, Aion.GameServer.Model.Craft.Profession.HANDICRAFTING)]
    [InlineData(798452, 798456, Aion.GameServer.Model.Craft.Profession.CONSTRUCTION)]
    [InlineData(203780, 830066, Aion.GameServer.Model.Craft.Profession.ESSENCETAPPING)]
    [InlineData(203782, 830064, Aion.GameServer.Model.Craft.Profession.AETHERTAPPING)]
    [InlineData(203784, 830058, Aion.GameServer.Model.Craft.Profession.COOKING)]
    [InlineData(203788, 830062, Aion.GameServer.Model.Craft.Profession.WEAPONSMITHING)]
    [InlineData(203790, 830060, Aion.GameServer.Model.Craft.Profession.ARMORSMITHING)]
    [InlineData(203793, 830052, Aion.GameServer.Model.Craft.Profession.TAILORING)]
    [InlineData(203786, 830054, Aion.GameServer.Model.Craft.Profession.ALCHEMY)]
    [InlineData(203792, 830056, Aion.GameServer.Model.Craft.Profession.HANDICRAFTING)]
    [InlineData(798450, 798454, Aion.GameServer.Model.Craft.Profession.CONSTRUCTION)]
    public void CanonicalCraftServiceRetainsEveryJavaTrainer(int original, int alternate, Aion.GameServer.Model.Craft.Profession profession)
    {
        var service = Aion.GameServer.Services.Craft.CraftSkillUpdateService.GetInstance();
        Assert.Equal(profession, service.GetProfessionByNpc(BuildUnmappedCraftNpc(original)));
        Assert.Equal(profession, service.GetProfessionByNpc(BuildUnmappedCraftNpc(alternate)));
    }

    [Theory]
    [InlineData(DialogAction.GIVEUP_CRAFT_EXPERT, false)]
    [InlineData(DialogAction.GIVEUP_CRAFT_MASTER, true)]
    public void AdvertisedRelinquishActionWithNoProfessionMappingIsIgnored(int action, bool master)
    {
        Npc npc = BuildUnmappedCraftNpc();

        Assert.True(npc.GetObjectTemplate().SupportsAction(action));
        Assert.False(DialogService.RelinquishCraftStatusForNpc(null!, npc, master));
    }

    [Fact]
    public void WellFormedAuctionMailWithUnknownResultIdIsIgnored()
    {
        var letter = new Letter(
            1,
            2,
            null!,
            0,
            "999,auction",
            "unused,1011",
            "$$HS_AUCTION_MAIL",
            DateTime.UtcNow,
            true,
            LetterType.NORMAL);

        Assert.False(HousingAuctionMailNotification.Handle(null!, letter));
    }

    private static Npc BuildUnmappedCraftNpc(int npcId = UnmappedNpcId)
    {
        EnsureInfrastructure();
        var spawn = new SpawnTemplate(new SpawnGroup(1, npcId, 0, null), new SpawnSpotTemplate());
        var template = new NpcTemplate
        {
            npcId = npcId,
            level = 1,
            rank = NpcRank.DISCIPLINED,
            rating = NpcRating.NORMAL,
            statsTemplate = new StatsTemplate { MaxHp = 100 },
            talkInfo = new TalkInfo
            {
                HasDialog = true,
                FuncDialogIdsRaw = $"{DialogAction.GIVEUP_CRAFT_EXPERT} {DialogAction.GIVEUP_CRAFT_MASTER}",
            },
        };
        return new Npc(new NpcController(), spawn, template);
    }

    private static void EnsureInfrastructure()
    {
        try
        {
            _ = IDFactory.GetInstance();
        }
        catch (InvalidOperationException)
        {
            IDFactory.RegisterInstance(new IDFactory());
        }

        if (DataManager.GetRegisteredInstance()?.StaticData.NpcSkillDataDh is not null)
            return;

        var staticData = (StaticData)RuntimeHelpers.GetUninitializedObject(typeof(StaticData));
        typeof(StaticData).GetProperty(nameof(StaticData.NpcSkillDataDh))!
            .SetValue(staticData, new NpcSkillData());
        ConstructorInfo constructor = typeof(DataManager).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            [typeof(StaticData)],
            modifiers: null)!;
        DataManager.RegisterInstance((DataManager)constructor.Invoke([staticData]));
    }

    public void Dispose()
    {
        DataManager.RestoreInstance(previousDataManager);
    }
}
