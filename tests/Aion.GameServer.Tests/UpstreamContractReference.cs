using System.Text.Json;

namespace Aion.GameServer.Tests;

/// <summary>A journey's Java reference is historical evidence, not the moving upstream queue cursor.</summary>
internal static class UpstreamContractReference
{
    internal static void AssertCompleted(string root, string reference)
    {
        using var state = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "docs/upstream-port-state.json")));
        string completed = state.RootElement.GetProperty("lastCompletedJavaCommit").GetString()!;
        string baseline = state.RootElement.GetProperty("baselineJavaCommit").GetString()!;
        Assert.Matches("^[0-9a-f]{40}$", reference);
        Assert.Matches("^[0-9a-f]{40}$", completed);
        var rows = File.ReadLines(Path.Combine(root, "docs/upstream-port-log.md"))
            .Where(line => line.StartsWith("| "))
            .Select(line => line.Split('|').Select(cell => cell.Trim().Trim('`')).ToArray())
            .Where(cells => cells.Length > 4 && cells[1].Length == 9 && cells[1].All(Uri.IsHexDigit))
            .ToArray();
        int referenceAt = Find(rows, reference, baseline);
        int completedAt = Find(rows, completed, baseline);
        Assert.True(referenceAt <= completedAt, "Journey reference is newer than the completed port cursor.");
        for (int index = Math.Max(0, referenceAt); index <= completedAt; index++)
            Assert.Contains(rows[index][4], new[] { "Ported", "Direct data carryover", "Not applicable" });
    }

    private static int Find(string[][] rows, string sha, string baseline)
    {
        if (sha == baseline)
            return -1;
        var matches = rows.Select((row, index) => (row, index)).Where(pair => pair.row[1] == sha[..9]).ToArray();
        Assert.Single(matches);
        return matches[0].index;
    }
}
