using Aion.GameServer.Model.Geometry;
using Aion.GameServer.Model.Templates.Zone;
using Aion.GameServer.Utils;
using Aion.GameServer.World;
using Aion.GameServer.World.Zone;

namespace Aion.GameServer.Tests;

public sealed class JavaStringHashParityTests
{
	[Theory]
	[InlineData("", 0)]
	[InlineData("abc", 96354)]
	[InlineData("hello", 99162322)]
	[InlineData("Aa", 2112)]
	[InlineData("BB", 2112)]
	[InlineData("😀", 1772899)]
	public void HashCode_MatchesJavaUtf16Vectors(string value, int expected)
	{
		Assert.Equal(expected, JavaString.HashCode(value));
	}

	[Fact]
	public void ZoneTemplatePreservesNameForStableJavaHash()
	{
		var zone = CreateZone("aion_test_zone");
		Assert.Equal("aion_test_zone", zone.GetZoneTemplate().GetName());
		Assert.Equal(JavaString.HashCode("aion_test_zone"), JavaString.HashCode(zone.GetZoneTemplate().GetName()!));
	}

	[Fact]
	public void EqualTypeAndPriorityZones_AreOrderedByJavaHashNotProcessHash()
	{
		var names = Enumerable.Range(0, 256).Select(i => $"AUDIT_ZONE_{i:D3}").ToArray();
		var pair = (
			from left in names
			from right in names
			where string.CompareOrdinal(left, right) < 0
			let javaOrder = JavaString.HashCode(left).CompareTo(JavaString.HashCode(right))
			let processOrder = left.GetHashCode().CompareTo(right.GetHashCode())
			where javaOrder != 0 && Math.Sign(javaOrder) != Math.Sign(processOrder)
			select (left, right, javaOrder)).First();

		var leftZone = CreateZone(pair.left);
		var rightZone = CreateZone(pair.right);

		Assert.Equal(
			Math.Sign(pair.javaOrder),
			Math.Sign(MapRegion.CompareZonesForOrdering(leftZone, rightZone)));
	}

	private static ZoneInstance CreateZone(string name)
	{
		var template = new ZoneTemplate
		{
			XmlName = name,
			ZoneType = ZoneClassName.SUB,
			Priority = 1,
			AreaType = AreaType.Sphere,
			Sphere = new Sphere(0, 0, 0, 1),
		};
		return new ZoneInstance(1, new ZoneInfo(template));
	}
}
