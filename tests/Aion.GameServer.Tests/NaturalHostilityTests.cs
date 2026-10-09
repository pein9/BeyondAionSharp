using Aion.Bots.Navigation;
using Aion.GameServer.Model;
using Aion.GameServer.TestKit;

namespace Aion.GameServer.Tests;

/// <summary>Which monsters the bot counts as aggressive. NR-18: this is the one test of NaturalPriestRotationTests that was
/// not of the removed static fight rule.</summary>
public sealed class NaturalHostilityTests
{
	[Fact]
	public async Task UntypedGrayManeStalkersCountAsAggressiveLikeTheServerSaysTheyAre()
	{
		var data = (await RealStaticData.LoadAsync()).StaticData;
		// 210750 and 211284 carry no type attribute (NpcTemplateType.NONE); the server aggroes by tribe.
		foreach (int stalker in new[] { 210750, 211284, 210407, 210408 })
			Assert.True(NaturalHostility.IsAggressive(data.NpcDataDh.GetNpcTemplate(stalker), data.TribeRelations, TribeClass.PC_DARK), stalker.ToString());
		Assert.Equal(9f, NaturalHostility.AggroRadius(data.NpcDataDh.GetNpcTemplate(211284), data.TribeRelations, TribeClass.PC_DARK));
		// Nalto (a quest NPC) and a null template are not.
		Assert.False(NaturalHostility.IsAggressive(data.NpcDataDh.GetNpcTemplate(203552), data.TribeRelations, TribeClass.PC_DARK));
		Assert.False(NaturalHostility.IsAggressive(null, data.TribeRelations, TribeClass.PC_DARK));
	}
}
