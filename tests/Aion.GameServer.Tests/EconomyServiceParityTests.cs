using System.Reflection;
using Aion.Commons.Database;
using Aion.GameServer.Dao;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services.Craft;
using MySqlConnector;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class EconomyServiceParityTests
{
    [Fact]
    public void CraftServiceHasOnlyTheJavaPackageImplementation()
    {
        var types = typeof(CraftSkillUpdateService).Assembly.GetTypes().Where(t => t.Name == nameof(CraftSkillUpdateService)).ToArray();
        Assert.Equal(typeof(CraftSkillUpdateService), Assert.Single(types));
        Assert.Same(CraftSkillUpdateService.GetInstance(), CraftSkillUpdateService.GetInstance());
    }

    [Fact]
    public void ProductionAssemblyDoesNotContainUnusedWorkOrderRecipeTable()
    {
        Assert.Null(typeof(CraftSkillUpdateService).Assembly.GetType("Aion.GameServer.Dataholders.WorkOrderRecipeTable"));
    }

    [Fact]
    public void InventoryStoreCatchesOnlySqlFailuresLikeJava()
    {
        var method = typeof(InventoryDAO).GetMethod(nameof(InventoryDAO.Store), [typeof(List<Item>), typeof(int?), typeof(int?), typeof(int?)])!;
        var catches = method.GetMethodBody()!.ExceptionHandlingClauses.Where(c => c.Flags == ExceptionHandlingClauseOptions.Clause).ToArray();
        Assert.Equal(typeof(MySqlException), Assert.Single(catches).CatchType);
    }

    [Fact]
    public void InventoryStoreDoesNotSwallowAnUninitializedFactory()
    {
        // No connection is attempted. Preserve the process-global factory used by other test collections.
        var field = typeof(DatabaseFactory).GetField("_dataSource", BindingFlags.Static | BindingFlags.NonPublic)!;
        var previous = field.GetValue(null);
        try
        {
            field.SetValue(null, null);
            var error = Assert.Throws<InvalidOperationException>(() => InventoryDAO.Store([], 42, null, null));
            Assert.Contains("not initialized", error.Message);
        }
        finally { field.SetValue(null, previous); }
    }
}
