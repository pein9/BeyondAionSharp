using Aion.Bots.Navigation;
using Aion.Bots.World;

namespace Aion.GameServer.Tests;

public sealed class NaturalFightThroughTests
{
	private static BotPosition P(float x, float y) => new(x, y, 10, 0);

	private static NaturalObservedMonster Monster(int id, float x, float y, float radius = 8) =>
		new(new NaturalNavigationObject(id, 210000 + id, P(x, y)), radius);

	// A straight 100 m corridor sampled every 2 m.
	private static readonly BotPosition[] Route = Enumerable.Range(1, 50).Select(i => P(i * 2, 0)).ToArray();

	[Fact]
	public void PullsTheFirstCircleAlongTheRouteFromJustOutsideIt()
	{
		NaturalObservedMonster near = Monster(1, 40, 2), far = Monster(2, 80, -1), aside = Monster(3, 60, 30);
		NaturalFightThroughBlocker next = NaturalFightThrough.SelectNext(P(0, 0), Route, [far, aside, near])!;
		Assert.Same(near, next.Monster);
		// The staging prefix never enters any circle and ends within spell range of the monster.
		Assert.All(next.Staging, p => Assert.True(Distance(p, near.Npc.Position) >= near.Radius));
		Assert.True(Distance(next.FiringPosition, near.Npc.Position) <= NaturalFightThrough.FiringRange);
		Assert.Equal([near, far], NaturalFightThrough.BlockersInOrder(P(0, 0), Route, [far, aside, near]));
	}

	[Fact]
	public void SkipsRejectedAndAlreadyEngagedMonstersAndRefusesUnpullableCircles()
	{
		NaturalObservedMonster engaged = Monster(1, 1, 0), next = Monster(2, 50, 0);
		// The bot already stands inside the first circle: that is an engagement, not a blocker ahead.
		Assert.Same(next, NaturalFightThrough.SelectNext(P(0, 0), Route, [engaged, next])!.Monster);
		Assert.Null(NaturalFightThrough.SelectNext(P(0, 0), Route, [next], new HashSet<int> { 2 }));
		// A circle wider than spell range cannot be pulled from its edge.
		Assert.Null(NaturalFightThrough.SelectNext(P(0, 0), Route, [Monster(4, 60, 0, radius: 30)]));
		// A clean route has nothing to fight.
		Assert.Null(NaturalFightThrough.SelectNext(P(0, 0), Route, [Monster(5, 50, 40)]));
		// Height matters: a monster on a ledge far above the corridor does not block it.
		var above = new NaturalObservedMonster(new NaturalNavigationObject(6, 210006, new BotPosition(50, 0, 40, 0)), 8);
		Assert.Null(NaturalFightThrough.SelectNext(P(0, 0), Route, [above]));
	}

	private static float Distance(BotPosition a, BotPosition b) => MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
}
