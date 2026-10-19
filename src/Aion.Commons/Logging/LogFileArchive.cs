using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;

namespace Aion.Commons.Logging;

/// <summary>Native counterpart of commons.logging.Logging.archiveLogs, including rolled files.</summary>
internal static class LogFileArchive
{
    internal static void Archive(string logDirectory)
    {
        Directory.CreateDirectory(logDirectory);
        var marker = Path.Combine(logDirectory, "[server_start_marker]");
        DateTime? started = File.Exists(marker) ? File.GetLastWriteTimeUtc(marker) : null;
        if (!File.Exists(marker)) File.WriteAllBytes(marker, []);
        using (var process = Process.GetCurrentProcess())
            File.SetLastWriteTimeUtc(marker, process.StartTime.ToUniversalTime());
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            AttributesToSkip = FileAttributes.ReparsePoint,
            IgnoreInaccessible = false,
        };
        var files = Directory.EnumerateFiles(logDirectory, "*", options)
            .Where(f => f.EndsWith(".log", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (files.Length == 0) return;
        string Stamp(DateTime time) => time.ToLocalTime().ToString("yyyy-MM-dd HH.mm", CultureInfo.InvariantCulture);
        var stopped = files.Max(File.GetLastWriteTimeUtc);
        var archived = Path.Combine(logDirectory, "archived");
        Directory.CreateDirectory(archived);
        var path = Path.Combine(archived, $"{(started is { } start ? Stamp(start) : "Unknown")} to {Stamp(stopped)}.zip");
        using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write))
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create))
            foreach (var file in files)
                zip.CreateEntryFromFile(file, Path.GetRelativePath(logDirectory, file).Replace('\\', '/'), CompressionLevel.SmallestSize);
        // Delete only after the complete archive has closed successfully.
        foreach (var file in files) File.Delete(file);
        foreach (var parent in files.Select(Path.GetDirectoryName).Distinct())
            if (parent != null && !Directory.EnumerateFileSystemEntries(parent).Any()) Directory.Delete(parent);
    }
}
