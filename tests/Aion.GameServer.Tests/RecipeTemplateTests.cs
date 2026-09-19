using System.Xml.Serialization;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.Templates.Recipe;

namespace Aion.GameServer.Tests;

public sealed class RecipeTemplateTests
{
	[Fact]
	public void AbsentXmlComboProductsHaveJavaNullSemanticsForLimitedRecipes()
	{
		// RecipeTemplate.java#getComboProduct and CraftService.java#finishCrafting at ce54b7931:
		// JAXB leaves the omitted list null; a non-critical limited recipe reports product 0 to OnFailCraft.
		using var xml = new StringReader("""
			<recipe_templates>
			  <recipe_template id="155001739" max_production_count="1" productid="100200835" quantity="1" />
			  <recipe_template id="2"><comboproduct itemid="100200836"/><comboproduct itemid="100200837"/></recipe_template>
			</recipe_templates>
			""");
		var data = Assert.IsType<RecipeData>(new XmlSerializer(typeof(RecipeData)).Deserialize(xml));
		data.AfterUnmarshal(null!);
		var absent = data.GetRecipeTemplateById(155001739);
		Assert.Equal(1, absent.GetMaxProductionCount());
		Assert.Equal(0, absent.GetComboProductSize());
		Assert.Null(absent.GetComboProduct(1));
		Assert.Null(new RecipeTemplate().GetComboProduct(1));
		var present = data.GetRecipeTemplateById(2);
		Assert.Equal(2, present.GetComboProductSize());
		Assert.Equal(100200836, present.GetComboProduct(1));
		Assert.Equal(100200837, present.GetComboProduct(2));
		Assert.Throws<ArgumentOutOfRangeException>(() => present.GetComboProduct(3));
	}
}
