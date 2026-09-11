using System.Reflection;
using System.Runtime.CompilerServices;
using Aion.GameServer.Handlers.AdminCommands;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class SpawnNpcParityTests
{
	[Fact]
	public void DummyHouseObject_ReportsItsWorldPositionThroughHouseObject()
	{
		// Java parity: SpawnNpc.DummyHouseObject overrides getX/getY/getZ/getHeading to read getPosition(), and SM_HOUSE_OBJECT(S)
		// call them through a HouseObject reference, so "//spawn <house item>" places the object where the admin stands.
		Type dummyType = typeof(SpawnNpc).GetNestedType("DummyHouseObject", BindingFlags.NonPublic)!;
		var houseObject = (HouseObject)RuntimeHelpers.GetUninitializedObject(dummyType);
		typeof(VisibleObject).GetField("Position", BindingFlags.Instance | BindingFlags.NonPublic)!
			.SetValue(houseObject, new WorldPosition(700010000, 1234.5f, 2345.25f, 123.75f, 200));

		Assert.Equal(1234.5f, houseObject.GetX());
		Assert.Equal(2345.25f, houseObject.GetY());
		Assert.Equal(123.75f, houseObject.GetZ());
		Assert.Equal(unchecked((sbyte)200), houseObject.GetHeading());
	}
}
