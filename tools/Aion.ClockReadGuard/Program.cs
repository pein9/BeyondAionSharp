using System.Text.Json;

namespace Aion.ClockReadGuard;

internal static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public static int Main(string[] args)
    {
        try
        {
            string repo = Directory.GetCurrentDirectory(), source = "src/Aion.GameServer";
            string allowlistPath = "scripts/ci/clock-read-allowlist.json";
            bool inventory = false;
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--repo": repo = args[++i]; break;
                    case "--source": source = args[++i]; break;
                    case "--allowlist": allowlistPath = args[++i]; break;
                    case "--inventory": inventory = true; break;
                    default: throw new ArgumentException($"Unknown argument {args[i]}.");
                }
            }
            repo = Path.GetFullPath(repo);
            string sourcePath = Path.GetFullPath(source, repo);
            var files = Directory.EnumerateFiles(sourcePath, "*.cs", SearchOption.AllDirectories)
                .Where(path => !Path.GetRelativePath(sourcePath, path).Split(Path.DirectorySeparatorChar)
                    .Any(part => part is "bin" or "obj"));
            IReadOnlyList<ClockRead> reads = ClockReadGuard.Scan(files.Select(path =>
                KeyValuePair.Create(Path.GetRelativePath(repo, path), File.ReadAllText(path))));
            if (inventory)
            {
                Console.WriteLine(JsonSerializer.Serialize(reads, JsonOptions));
                return 0;
            }
            var allowlist = JsonSerializer.Deserialize<ClockAllowlist>(File.ReadAllText(Path.GetFullPath(allowlistPath, repo)), JsonOptions)
                ?? throw new InvalidDataException("Empty clock allowlist.");
            if (allowlist.Sites == null || allowlist.SourceRoot != Path.GetRelativePath(repo, sourcePath).Replace('\\', '/'))
                throw new InvalidDataException("Clock allowlist source root does not match the inspected source root.");
            IReadOnlyList<string> errors = ClockReadGuard.Check(reads, allowlist);
            foreach (string error in errors) Console.Error.WriteLine(error);
            if (errors.Count != 0) return 1;
            Console.WriteLine($"Clock-read guard passed: {reads.Count} reviewed infrastructure reads across {reads.Select(read => read.Site.Path).Distinct().Count()} files; no unapproved or stale sites.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }
}
