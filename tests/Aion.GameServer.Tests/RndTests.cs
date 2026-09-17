using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Model.Templates.Spawns;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class RndTests
{
    [Fact]
    public async Task ScopedSeedWinsAndProcessSeedCrossesSuppressedExecutionContext()
    {
        const int processSeed = 17;
        const int scopedSeed = 29;
        Rnd.SetProcessSeed(processSeed);
        Rnd.UseSeed(scopedSeed);
        try
        {
            var expectedScoped = new Random(scopedSeed);
            Assert.Equal(expectedScoped.NextInt64(int.MinValue, (long)int.MaxValue + 1), Rnd.NextInt());
            Assert.Equal(scopedSeed, Rnd.ActiveSeed);
            Assert.Equal($"Rnd seed: {scopedSeed}", Rnd.SeedDiagnostic);

            Task<int> withoutExecutionContext;
            using (ExecutionContext.SuppressFlow())
                withoutExecutionContext = Task.Run(Rnd.NextInt);

            var expectedProcess = new Random(processSeed);
            Assert.Equal(expectedProcess.NextInt64(int.MinValue, (long)int.MaxValue + 1), await withoutExecutionContext);

            Rnd.UseProductionRandom();
            Assert.Equal(processSeed, Rnd.ActiveSeed);
        }
        finally
        {
            Rnd.UseProductionRandom();
            Rnd.UseProductionRandomProcessWide();
        }
    }

    [Fact]
    public void SameSeedReplaysSpawnPoolChoices()
    {
        const int seed = 8675309;
        try
        {
            int[] first = ReservePoolOrder(seed);
            int[] second = ReservePoolOrder(seed);

            Assert.True(first.SequenceEqual(second),
                $"{Rnd.SeedDiagnostic}; first=[{string.Join(',', first)}], second=[{string.Join(',', second)}]");
        }
        finally
        {
            Rnd.UseProductionRandom();
        }
    }

    private static int[] ReservePoolOrder(int seed)
    {
        Rnd.UseSeed(seed);
        var group = new SpawnGroup(1, 2, 0, null);
        SpawnTemplate[] spots = Enumerable.Range(0, 5)
            .Select(index => new SpawnTemplate(group, new SpawnSpotTemplate { X = index }))
            .ToArray();
        foreach (SpawnTemplate spot in spots)
            group.AddSpawnTemplate(spot);

        return Enumerable.Range(0, spots.Length)
            .Select(_ => Array.IndexOf(spots, group.ReserveRandomFreePoolSpot(1)!))
            .ToArray();
    }
}
