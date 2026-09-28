using Aion.Bots.Navigation;
using Aion.Bots.Scenarios;

namespace Aion.GameServer.Tests;

/// <summary>NA-06: per-map navigation state for a journey that crosses maps.</summary>
public sealed class NaturalJourneyMapContextsTests
{
	private sealed class Context(NaturalMapKey key) { public NaturalMapKey Key { get; } = key; }

	[Fact]
	public void OpenMapsAreBuiltOnceAndReusedAcrossVisits()
	{
		var contexts = new NaturalJourneyMapContexts<Context>(key => new Context(key));
		Context pandaemonium = contexts.Enter(new NaturalMapKey(120010000, 1));
		Context altgard = contexts.Enter(new NaturalMapKey(220030000, 1));
		Assert.Same(pandaemonium, contexts.Enter(new NaturalMapKey(120010000, 1)));
		Assert.NotSame(pandaemonium, altgard);
		Assert.Equal(2, contexts.Built);
		Assert.Equal(new NaturalMapKey(120010000, 1), contexts.Current);
	}

	[Fact]
	public void TwinChannelsOfOneMapAreSeparateWorlds()
	{
		var contexts = new NaturalJourneyMapContexts<Context>(key => new Context(key));
		Context first = contexts.Enter(new NaturalMapKey(220030000, 1));
		Context second = contexts.Enter(new NaturalMapKey(220030000, 2));
		Assert.NotSame(first, second);
		Assert.Equal(2, second.Key.Channel);
	}

	[Fact]
	public void AnInstanceIsRebuiltOnEveryEntryButNotWithinOneStay()
	{
		var contexts = new NaturalJourneyMapContexts<Context>(key => new Context(key));
		var ataxiar = new NaturalMapKey(320020000, 1);
		Assert.True(ataxiar.IsInstance);
		Assert.False(new NaturalMapKey(220010000, 1).IsInstance);
		Context firstEntry = contexts.Enter(ataxiar);
		Assert.Same(firstEntry, contexts.Enter(ataxiar, newEntry: false));
		// A death in the trial resets Q2008 to var 4; Munin then opens a new instance with a new instance id.
		Context secondEntry = contexts.Enter(ataxiar);
		Assert.NotSame(firstEntry, secondEntry);
		Assert.Equal(2, contexts.Built);
	}

	[Fact]
	public void TheHomeContextIsWhateverTheFactoryReturnsForItsMap()
	{
		var home = new Context(new NaturalMapKey(220010000, 0));
		var contexts = new NaturalJourneyMapContexts<Context>(key => key.MapId == 220010000 ? home : new Context(key));
		Assert.Same(home, contexts.Enter(new NaturalMapKey(220010000, 1)));
		Assert.Same(home, contexts.Enter(new NaturalMapKey(220010000, 2)));
	}

	[Fact]
	public void OfflineGeometryAcceptsEveryNaturalJourneyMap()
	{
		Assert.Equal([220010000, 320010000, 320020000, 120010000, 220030000], BotNavigationAssets.NaturalJourneyMapIds);
		NaturalAscensionContract contract = NaturalAscensionContract.LoadDefault();
		Assert.All(contract.Steps.Select(step => step.MapId).Distinct(), map => Assert.Contains(map, BotNavigationAssets.NaturalJourneyMapIds));
	}
}
