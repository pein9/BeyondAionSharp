using Aion.GameServer.Services;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Skill;
using Aion.GameServer.Tests.Ai;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class SkillLearnServicePortTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    public void LearningAPassiveAppliesTheLearnedLevelNotTheTemplateLevel(int learnedLevel)
    {
        // Java SkillLearnService.onLearnSkill passes skillLevel through to ApplyEffectDirectly.
        // This isolated hook regression supplies the already-learned entry, not a DB/client journey.
        using var harness = BossAiHarness.For().Build();
        var player = harness.SpawnPlayer();
        player.SetSkillList(new PlayerSkillList([new PlayerSkillEntry(40, learnedLevel, 0, IPersistable.PersistentState.NEW)]));

        SkillLearnService.OnLearnSkill(player, 40, learnedLevel, isNew: true);

        var passive = Assert.Single(player.GetEffectController().GetAllEffects(), e => e.GetSkillId() == 40);
        Assert.Equal(learnedLevel, passive.GetSkillLevel());
        Assert.True(passive.IsPassive());
    }

    [Fact]
    public void SkillRemovalEndsOnlyEffectsThatDependOnTheLearnedSkill()
    {
        Assert.True(SkillLearnService.ShouldRemoveEffectOnSkillRemoval(new SkillTemplate
        {
            activationAttribute = ActivationAttribute.PASSIVE,
            stack = "PASSIVE_SKILL"
        }));
        Assert.True(SkillLearnService.ShouldRemoveEffectOnSkillRemoval(new SkillTemplate
        {
            activationAttribute = ActivationAttribute.TOGGLE,
            stack = "TOGGLE_SKILL"
        }));
        Assert.True(SkillLearnService.ShouldRemoveEffectOnSkillRemoval(new SkillTemplate
        {
            activationAttribute = ActivationAttribute.ACTIVE,
            isDeityAvatar = true,
            stack = "AVATAR_SKILL"
        }));
        Assert.True(SkillLearnService.ShouldRemoveEffectOnSkillRemoval(new SkillTemplate
        {
            activationAttribute = ActivationAttribute.ACTIVE,
            stack = "WS_BOOSTATKSPEED"
        }));
        Assert.False(SkillLearnService.ShouldRemoveEffectOnSkillRemoval(new SkillTemplate
        {
            activationAttribute = ActivationAttribute.ACTIVE,
            stack = "STIGMA_BUFF"
        }));
    }
}
