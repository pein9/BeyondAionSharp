using Aion.ClockReadGuard;
using Guard = Aion.ClockReadGuard.ClockReadGuard;

namespace Aion.GameServer.Tests;

public sealed class ClockReadGuardTests
{
    private static IReadOnlyList<ClockRead> Scan(string source, string path = "src/Worker.cs") =>
        Guard.Scan([KeyValuePair.Create(path, source)]);

    private static string Worker(string members) => "using System; using System.Diagnostics; namespace Fixture; class Worker { " + members + " }";

    private static ClockAllowlist Approve(IReadOnlyList<ClockRead> reads) => new(1, "src", reads.GroupBy(read => read.Site)
        .Select(group => new ClockException(group.Key.Path, group.Key.Member, group.Key.Api, group.Key.Context,
            group.Count(), "Reviewed real operation duration; never a gameplay deadline.")).ToList());

    [Theory]
    [InlineData("DateTimeOffset.UtcNow", "System.DateTimeOffset.UtcNow")]
    [InlineData("DateTimeOffset.Now", "System.DateTimeOffset.Now")]
    [InlineData("DateTime.UtcNow", "System.DateTime.UtcNow")]
    [InlineData("DateTime.Now", "System.DateTime.Now")]
    [InlineData("DateTime.Today", "System.DateTime.Today")]
    [InlineData("Environment.TickCount", "System.Environment.TickCount")]
    [InlineData("Environment.TickCount64", "System.Environment.TickCount64")]
    [InlineData("Stopwatch.GetTimestamp()", "System.Diagnostics.Stopwatch.GetTimestamp")]
    [InlineData("Stopwatch.StartNew()", "System.Diagnostics.Stopwatch.StartNew")]
    [InlineData("TimeProvider.System", "System.TimeProvider.System")]
    public void RejectsUnapprovedFrameworkClockReads(string expression, string api)
    {
        var reads = Scan(Worker("object Gameplay() => " + expression + ";"));
        Assert.Equal(api, Assert.Single(reads).Site.Api);
        Assert.Contains(Guard.Check(reads, new ClockAllowlist(1, "src", [])), error => error.StartsWith("Unapproved clock read"));
    }

    [Theory]
    [InlineData("using Clock = System.DateTime; class C { object Read() => Clock.UtcNow; }")]
    [InlineData("using static System.DateTime; class C { object Read() => UtcNow; }")]
    [InlineData("class C { object Read() => global::System.DateTime.UtcNow; }")]
    [InlineData("global using Clock = System.DateTime; class C { object Read() => Clock.UtcNow; }")]
    public void AliasesStaticImportsAndQualifiedNamesCannotBypassTheGuard(string source) =>
        Assert.Equal("System.DateTime.UtcNow", Assert.Single(Scan(source)).Site.Api);

    [Theory]
    [InlineData("Start")]
    [InlineData("Restart")]
    public void DetectsInstanceStopwatchStarts(string method)
    {
        var reads = Scan(Worker("void Gameplay() { var watch = new Stopwatch(); watch." + method + "(); }"));
        Assert.Equal("System.Diagnostics.Stopwatch." + method, Assert.Single(reads).Site.Api);
    }

    [Fact]
    public void IgnoresCommentsStringsNameofAndUnrelatedMembers()
    {
        Assert.Empty(Scan("""
            // DateTime.UtcNow Stopwatch.StartNew()
            class DateTime { public static int UtcNow => 3; }
            class C {
                string Text => "System.DateTime.UtcNow";
                string Name => nameof(System.DateTime.UtcNow);
                int Read() => DateTime.UtcNow;
            }
            """));
    }

    [Fact]
    public void EscapedMethodNamedNameofCannotHideAnExecutedClockRead()
    {
        var reads = Scan("""
            class C {
                void @nameof(object value) {}
                void Read() { @nameof(System.DateTime.UtcNow); }
            }
            """);
        Assert.Equal("System.DateTime.UtcNow", Assert.Single(reads).Site.Api);
    }

    [Fact]
    public void DiagnosticSuppressionDoesNotApproveAClockSite()
    {
        var reads = Scan("""
            #pragma warning disable RS0030
            class Gameplay { object Deadline() => System.DateTime.UtcNow; }
            #pragma warning restore RS0030
            """);
        Assert.Contains(Guard.Check(reads, new ClockAllowlist(1, "src", [])), error => error.StartsWith("Unapproved"));
    }

    [Fact]
    public void ReviewedSiteSurvivesWhitespaceCommentsAndLineNumberChanges()
    {
        var reads = Scan(Worker("long Duration() { var start = Stopwatch.GetTimestamp(); return start; }"));
        var changed = Scan(Worker("\n\nlong Duration() { var start = Stopwatch /*comment*/ . GetTimestamp ( ); return start; }"));
        Assert.Empty(Guard.Check(changed, Approve(reads)));
    }

    [Fact]
    public void DeletingApprovedReadDoesNotMakeRoomForGameplayRead()
    {
        var before = Scan(Worker("long Duration() => Stopwatch.GetTimestamp();"));
        var after = Scan(Worker("long Gameplay() => Stopwatch.GetTimestamp();"));
        Assert.Equal(before.Count, after.Count);
        var errors = Guard.Check(after, Approve(before));
        Assert.Contains(errors, error => error.StartsWith("Unapproved"));
        Assert.Contains(errors, error => error.StartsWith("Stale"));
    }

    [Fact]
    public void ChangedStatementInApprovedMemberRequiresReview()
    {
        var before = Scan(Worker("long Duration() { var start = Stopwatch.GetTimestamp(); return start; }"));
        var after = Scan(Worker("long Duration() { var gameplayDeadline = Stopwatch.GetTimestamp(); return gameplayDeadline; }"));
        Assert.Contains(Guard.Check(after, Approve(before)), error => error.StartsWith("Unapproved"));
    }

    [Fact]
    public void DuplicatedApprovedStatementCannotShareOneException()
    {
        var before = Scan(Worker("void Duration() { { var start = Stopwatch.GetTimestamp(); } }"));
        var after = Scan(Worker("void Duration() { { var start = Stopwatch.GetTimestamp(); } { var start = Stopwatch.GetTimestamp(); } }"));
        Assert.Equal(2, after.Count);
        Assert.Contains(Guard.Check(after, Approve(before)), error => error.StartsWith("Clock site count changed"));
    }

    [Fact]
    public void MovingReadToAnotherFileRequiresReview()
    {
        var source = Worker("long Duration() => Stopwatch.GetTimestamp();");
        Assert.Contains(Guard.Check(Scan(source, "src/Gameplay.cs"), Approve(Scan(source))), error => error.StartsWith("Unapproved"));
    }

    [Fact]
    public void RejectsStaleDuplicateUnexplainedAndInvalidExceptions()
    {
        var reads = Scan(Worker("long Duration() => Stopwatch.GetTimestamp();"));
        var allowlist = Approve(reads);
        Assert.Contains(Guard.Check([], allowlist), error => error.StartsWith("Stale"));
        var entry = allowlist.Sites[0];
        Assert.Contains(Guard.Check(reads, allowlist with { Sites = [entry, entry] }), error => error.StartsWith("Duplicate"));
        Assert.Contains(Guard.Check(reads, allowlist with { Sites = [entry with { Reason = "" }] }), error => error.StartsWith("Invalid"));
        Assert.Contains(Guard.Check(reads, allowlist with { Sites = [entry with { Count = 0 }] }), error => error.StartsWith("Invalid"));
        Assert.Contains(Guard.Check(reads, allowlist with { SchemaVersion = 2 }), error => error.StartsWith("Unsupported"));
    }

    [Fact]
    public void ScansDebugAndReleaseBranchesWithoutDoubleCountingSharedReads()
    {
        var reads = Scan("""
            class C {
                object Read() {
            #if DEBUG
                    var debug = System.DateTime.UtcNow;
            #else
                    var release = System.DateTime.Now;
            #endif
                    return System.DateTime.Today;
                }
            }
            """);
        Assert.Equal(3, reads.Count);
    }

    [Fact]
    public void MalformedSourceFailsClosed() => Assert.Throws<InvalidDataException>(() => Scan("class Broken {"));
}
