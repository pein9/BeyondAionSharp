using System.IO.Compression;
using Aion.Commons.Logging;
using Microsoft.Extensions.Logging;

namespace Aion.Commons.Tests;

public sealed class LogRolloverPortTests
{
    [Theory]
    [InlineData(127, false)]
    [InlineData(128, true)]
    public void ExistingByteLengthTriggersAtTheLimitBeforeTheNextEvent(int bytes, bool rolls)
    {
        using var dir = new Scratch();
        using var provider = new AionFileLoggerProvider(dir.Path, 128, timeProvider: new Clock());
        File.WriteAllBytes(dir.File("craft.log"), Enumerable.Repeat((byte)'x', bytes).ToArray());
        provider.CreateLogger("CRAFT_LOG").LogInformation("next");
        Assert.Equal(rolls, File.Exists(dir.File("craft.1.log")));
        if (rolls) Assert.Equal(bytes, new FileInfo(dir.File("craft.1.log")).Length);
        Assert.Contains("next", File.ReadAllText(dir.File("craft.log")));
    }

    [Fact]
    public void FiveOlderFilesAreShiftedInOrderAndTheOldestIsDiscarded()
    {
        using var dir = new Scratch(); var clock = new Clock();
        using var provider = new AionFileLoggerProvider(dir.Path, 1, timeProvider: clock);
        var logger = provider.CreateLogger("CRAFT_LOG");
        for (int i = 0; i < 8; i++) { clock.Millis += 60000; logger.LogInformation("event-{Number}", i); }
        Assert.Equal(6, Directory.GetFiles(dir.Path, "craft*.log").Length);
        Assert.Contains("event-7", File.ReadAllText(dir.File("craft.log")));
        for (int i = 1; i <= 5; i++) Assert.Contains("event-" + (7 - i), File.ReadAllText(dir.File($"craft.{i}.log")));
        Assert.DoesNotContain("event-1", string.Join("", Directory.GetFiles(dir.Path, "craft*.log").Select(File.ReadAllText)));
    }

    [Fact]
    public void SizeGateIsSharedByLoggersPerFileAndOpensAtSixtySeconds()
    {
        using var dir = new Scratch(); var clock = new Clock();
        using var provider = new AionFileLoggerProvider(dir.Path, 1, timeProvider: clock);
        provider.CreateLogger("CHAT_LOG").LogInformation("first");
        clock.Millis += 59999;
        provider.CreateLogger("CHAT_LOG").LogInformation("second");
        Assert.False(File.Exists(dir.File("chat.1.log")));
        // Another file has its own gate, independent of chat.log's last check.
        File.WriteAllText(dir.File("craft.log"), "seed");
        provider.CreateLogger("CRAFT_LOG").LogInformation("craft");
        Assert.Equal("seed", File.ReadAllText(dir.File("craft.1.log")));
        clock.Millis++;
        provider.CreateLogger("CHAT_LOG").LogInformation("third");
        Assert.Contains("first", File.ReadAllText(dir.File("chat.1.log")));
        Assert.Contains("second", File.ReadAllText(dir.File("chat.1.log")));
        Assert.DoesNotContain("third", File.ReadAllText(dir.File("chat.1.log")));
        Assert.Contains("third", File.ReadAllText(dir.File("chat.log")));
    }

    [Theory]
    [InlineData(LogLevel.Warning, "server_warnings.log")]
    [InlineData(LogLevel.Error, "server_errors.log")]
    public void RootAndSeverityFilesBothRoll(LogLevel level, string file)
    {
        using var dir = new Scratch(); var clock = new Clock();
        using var provider = new AionFileLoggerProvider(dir.Path, 1, timeProvider: clock);
        var logger = provider.CreateLogger("ordinary");
        logger.Log(level, "first"); clock.Millis += 60000; logger.Log(level, "second");
        Assert.Contains("first", File.ReadAllText(dir.File(file.Replace(".log", ".1.log"))));
        Assert.Contains("first", File.ReadAllText(dir.File("server_console.1.log")));
        Assert.Contains("second", File.ReadAllText(dir.File(file)));
    }

    [Theory]
    [InlineData("CM_LOGIN")]
    [InlineData("com.aionemu.loginserver.network.aion.clientpackets.CM_LOGIN")]
    public void LoginAuditUsesItsNonAdditiveRollingSink(string category)
    {
        using var dir = new Scratch(); var clock = new Clock();
        using var provider = new AionFileLoggerProvider(dir.Path, 1, timeProvider: clock);
        var logger = provider.CreateLogger(category);
        logger.LogDebug("first"); clock.Millis += 60000; logger.LogDebug("second");
        Assert.Contains("first", File.ReadAllText(dir.File("cm_login.1.log")));
        Assert.False(File.Exists(dir.File("server_console.log")));
    }

    [Theory]
    [InlineData("game-server", "gameserver", "mygs.properties")]
    [InlineData("chat-server", "chatserver", "mycs.properties")]
    [InlineData("login-server", "loginserver", "myls.properties")]
    public void ModulePropertiesOverrideDefaultsAndUnimportedFilesCannotOverrideLogging(string module, string prefix, string overrides)
    {
        using var dir = new Scratch(); string log = dir.File("log");
        using (var defaults = AionFileLoggerProvider.FromJavaConfig(log, module))
        { Assert.Equal(100L * 1024 * 1024, defaults.MaxFileSizeBytes); Assert.Equal(5, defaults.MaxRolledFiles); }
        Directory.CreateDirectory(dir.File("config/main")); Directory.CreateDirectory(dir.File("config/network"));
        File.WriteAllText(dir.File("config/main/logging.properties"), $"{prefix}.log.max_file_size=2 kb\n{prefix}.log.max_rolled_files=7\n");
        File.WriteAllText(dir.File("config/network/zz.properties"), $"{prefix}.log.max_file_size=999MB\n{prefix}.log.max_rolled_files=19\n");
        using (var configured = AionFileLoggerProvider.FromJavaConfig(log, module))
        { Assert.Equal(2048, configured.MaxFileSizeBytes); Assert.Equal(7, configured.MaxRolledFiles); }
        File.WriteAllText(dir.File("config/" + overrides), $"roll.size=1 GB\n{prefix}.log.max_file_size=${{roll.size}}\n{prefix}.log.max_rolled_files=${{unset.retention:-2}}\n");
        using var custom = AionFileLoggerProvider.FromJavaConfig(log, module);
        Assert.Equal(1024L * 1024 * 1024, custom.MaxFileSizeBytes); Assert.Equal(2, custom.MaxRolledFiles);
    }

    [Theory]
    [InlineData("010", 10)]
    [InlineData("+3 ", 3)]
    [InlineData("0", 1)]
    [InlineData("-1", 1)]
    [InlineData("99", 21)]
    [InlineData("0x10", 7)]
    [InlineData("2147483648", 7)]
    [InlineData(@"\u0661\u0660", 10)]
    public void RetentionUsesDecimalIntegerValueOfAndTheFixedWindowBounds(string value, int expected)
    {
        using var dir = new Scratch(); Directory.CreateDirectory(dir.File("config/main"));
        File.WriteAllText(dir.File("config/main/logging.properties"), "chatserver.log.max_rolled_files=" + value + "\n");
        using var provider = AionFileLoggerProvider.FromJavaConfig(dir.File("log"), "chat-server");
        Assert.Equal(expected, provider.MaxRolledFiles);
    }

    [Theory]
    [InlineData("0", 0L)]
    [InlineData("128", 128L)]
    [InlineData("2 kbs", 2048L)]
    [InlineData(" 1 MB ", 1048576L)]
    [InlineData("1.5MB", 10485760L)]
    [InlineData("-1MB", 10485760L)]
    [InlineData("9223372036854775807KB", -1024L)]
    public void FileSizesUseBinaryUnitsJavaLongOverflowAndPolicyFallback(string value, long expected)
    {
        using var dir = new Scratch(); Directory.CreateDirectory(dir.File("config/main"));
        File.WriteAllText(dir.File("config/main/logging.properties"), "chatserver.log.max_file_size=" + value + "\n");
        using var provider = AionFileLoggerProvider.FromJavaConfig(dir.File("log"), "chat-server");
        Assert.Equal(expected, provider.MaxFileSizeBytes);
    }

    [Fact]
    public void StartupArchivesActiveAndRolledFilesIncludingNestedLogsWithOriginalBytes()
    {
        using var dir = new Scratch();
        using (var first = new AionFileLoggerProvider(dir.Path)) first.CreateLogger("CRAFT_LOG").LogInformation("prior run");
        var active = File.ReadAllBytes(dir.File("craft.log"));
        var rolled = new byte[] {0, 255, 17, 64}; File.WriteAllBytes(dir.File("craft.1.log"), rolled);
        Directory.CreateDirectory(dir.File("nested")); File.WriteAllText(dir.File("nested/old.LOG"), "nested");
        File.WriteAllText(dir.File("events.jsonl"), "keep");
        using var restarted = new AionFileLoggerProvider(dir.Path);
        string archive = Assert.Single(Directory.GetFiles(dir.File("archived"), "*.zip"));
        using var zip = ZipFile.OpenRead(archive);
        Assert.Equal(3, zip.Entries.Count);
        byte[] Read(string name) { using var input = zip.GetEntry(name)!.Open(); using var output = new MemoryStream(); input.CopyTo(output); return output.ToArray(); }
        Assert.Equal(active, Read("craft.log")); Assert.Equal(rolled, Read("craft.1.log"));
        Assert.Equal("nested", System.Text.Encoding.UTF8.GetString(Read("nested/old.LOG")));
        Assert.Empty(Directory.EnumerateFiles(dir.Path, "*.log", SearchOption.AllDirectories));
        Assert.Equal("keep", File.ReadAllText(dir.File("events.jsonl")));
        Assert.True(File.Exists(dir.File("[server_start_marker]")));
        restarted.CreateLogger("CRAFT_LOG").LogInformation("new run");
        Assert.DoesNotContain("prior run", File.ReadAllText(dir.File("craft.log")));
    }

    [Fact]
    public void ArchiveReadFailureLeavesTheOriginalLogInPlace()
    {
        using var dir = new Scratch(); File.WriteAllText(dir.File("old.log"), "retain me");
        using (var locked = new FileStream(dir.File("old.log"), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            Assert.ThrowsAny<IOException>(() => new AionFileLoggerProvider(dir.Path));
        Assert.Equal("retain me", File.ReadAllText(dir.File("old.log")));
    }

    [Fact]
    public void ConcurrentCategoryLoggersSerializeRotationAndKeepCompleteEvents()
    {
        using var dir = new Scratch();
        using var provider = new AionFileLoggerProvider(dir.Path, 1, timeProvider: new AdvancingClock());
        Parallel.For(0, 100, i => provider.CreateLogger("CRAFT_LOG").LogInformation("entry-{Index}", i));
        var files = Directory.GetFiles(dir.Path, "craft*.log"); Assert.Equal(6, files.Length);
        foreach (var file in files) Assert.Contains("entry-", Assert.Single(File.ReadAllLines(file)));
    }

    private sealed class Clock : TimeProvider
    {
        public long Millis = 1000;
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.FromUnixTimeMilliseconds(Millis);
    }
    private sealed class AdvancingClock : TimeProvider
    {
        private long millis = 1000;
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.FromUnixTimeMilliseconds(Interlocked.Add(ref millis, 60000));
    }
    private sealed class Scratch : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "aion-roll-test-" + Guid.NewGuid().ToString("N"));
        public Scratch() => Directory.CreateDirectory(Path);
        public string File(string name) => System.IO.Path.Combine(Path, name);
        public void Dispose()
        {
            string resolved = System.IO.Path.GetFullPath(Path);
            if (!resolved.StartsWith(System.IO.Path.GetFullPath(System.IO.Path.GetTempPath()), StringComparison.OrdinalIgnoreCase)
                || !System.IO.Path.GetFileName(resolved).StartsWith("aion-roll-test-", StringComparison.Ordinal)) throw new InvalidOperationException("Unexpected test folder.");
            Directory.Delete(resolved, recursive: true);
        }
    }
}
