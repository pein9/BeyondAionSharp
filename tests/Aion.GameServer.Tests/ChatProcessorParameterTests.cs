using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Tests;

public sealed class ChatProcessorParameterTests
{
	[Fact]
	public void SplitsOrdinaryParametersWithoutReturningRegexCaptures()
	{
		Assert.Equal(new[] { "level", "9" }, ChatProcessor.GetParamsFromString(" level 9"));
	}

	[Fact]
	public void KeepsSpacesInsideLinkedParameters()
	{
		Assert.Equal(new[] { "add", "[item:123 A linked item;1]", "2" },
			ChatProcessor.GetParamsFromString("add [item:123 A linked item;1] 2"));
	}
}
