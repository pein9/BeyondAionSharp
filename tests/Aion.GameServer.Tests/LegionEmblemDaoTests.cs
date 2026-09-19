using System.Reflection;
using Aion.Commons.Database;
using Aion.GameServer.Dao;
using Aion.GameServer.Model.Team.Legion;
using MySqlConnector;

namespace Aion.GameServer.Tests;

public sealed class LegionEmblemDaoTests
{
	[Theory]
	[InlineData(false, 0)] [InlineData(true, 0)]
	[InlineData(false, 1)] [InlineData(true, 1)]
	[InlineData(false, 2)] [InlineData(true, 2)]
	[InlineData(false, 3)] [InlineData(true, 3)]
	public void CreateAndUpdateBindJavaSignedBytesForEveryColorChannel(bool update, int rotation)
	{
		byte[] boundary = [0, 127, 128, 255];
		byte[] colors = Enumerable.Range(0, 4).Select(i => boundary[(i + rotation) % 4]).ToArray();
		var emblem = new LegionEmblem();
		emblem.SetEmblem(3, colors[0], colors[1], colors[2], colors[3], LegionEmblemType.DEFAULT, []);
		var handlerType = typeof(LegionDAO).GetNestedType(update ? "UpdateEmblemHandler" : "CreateEmblemHandler", BindingFlags.NonPublic)!;
		var handler = (IUStH)Activator.CreateInstance(handlerType, BindingFlags.Instance | BindingFlags.NonPublic,
			binder: null, args: [42, emblem], culture: null)!;
		using var command = new MySqlCommand();
		// ExecuteNonQuery cannot run without a connection. Parameter binding happens first and is inspectable.
		Assert.Throws<InvalidOperationException>(() => handler.HandleInsertUpdate(command));
		Assert.Equal(8, command.Parameters.Count);
		int offset = update ? 1 : 2;
		for (int i = 0; i < colors.Length; i++) Assert.Equal(unchecked((sbyte)colors[i]), Assert.IsType<sbyte>(command.Parameters[offset + i].Value));
		Assert.Equal("DEFAULT", command.Parameters[update ? 5 : 6].Value);
		Assert.Equal(42, command.Parameters[update ? 7 : 0].Value);
	}
}
