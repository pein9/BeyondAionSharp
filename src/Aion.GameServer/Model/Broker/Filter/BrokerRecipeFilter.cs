using Aion.GameServer.Model.Templates.Items.Actions;
using System.Linq;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.Model.Templates.Recipe;

namespace Aion.GameServer.Model.Broker.Filter;

/// <summary>Java parity: model/broker/filter/BrokerRecipeFilter.</summary>
public class BrokerRecipeFilter : BrokerContainsFilter
{
    private readonly int craftSkillId;

    public BrokerRecipeFilter(int craftSkillId, params int[] masks)
        : base(masks)
    {
        this.craftSkillId = craftSkillId;
    }

    public override bool Accept(ItemTemplate template)
    {
        CraftLearnAction craftAction = template.GetActions() == null ? null : template.GetActions().GetCraftLearnAction();
        if (craftAction == null)
            return false;
        if (!base.Accept(template))
            return false;
        int id = craftAction.GetRecipeId();
        RecipeTemplate recipeTemplate = DataManager.RECIPE_DATA.GetRecipeTemplateById(id);
        return recipeTemplate != null && recipeTemplate.GetSkillId() == craftSkillId;
    }
}
