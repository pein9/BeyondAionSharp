using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Aion.ClockReadGuard;

public sealed record ClockSite(string Path, string Member, string Api, string Context);
public sealed record ClockRead(ClockSite Site, int Line, int Column);
public sealed record ClockException(string Path, string Member, string Api, string Context, int Count, string Reason)
{
    public ClockSite Site => new(Path, Member, Api, Context);
}
public sealed record ClockAllowlist(int SchemaVersion, string SourceRoot, List<ClockException> Sites);

/// <summary>Bind framework symbols, then approve each read's member and statement rather than a count or whole file.</summary>
public static class ClockReadGuard
{
    private static readonly HashSet<string> CandidateNames =
        ["UtcNow", "Now", "Today", "TickCount", "TickCount64", "GetTimestamp", "StartNew", "Start", "Restart", "System"];

    private static readonly MetadataReference[] References =
        ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(System.IO.Path.PathSeparator)
        .Select(path => MetadataReference.CreateFromFile(path)).ToArray();

    public static IReadOnlyList<ClockRead> Scan(IEnumerable<KeyValuePair<string, string>> sources)
    {
        var inputs = sources.ToArray();
        // Inspect both build configurations. Shared reads are deduplicated by location, not by statement count.
        return ScanConfiguration(inputs, debug: true).Concat(ScanConfiguration(inputs, debug: false)).Distinct().ToArray();
    }

    private static IReadOnlyList<ClockRead> ScanConfiguration(IEnumerable<KeyValuePair<string, string>> sources, bool debug)
    {
        var symbols = new List<string> { "TRACE", "NET", "NET10_0", "NETCOREAPP", "NETCOREAPP1_0_OR_GREATER",
            "NETCOREAPP1_1_OR_GREATER", "NETCOREAPP2_0_OR_GREATER", "NETCOREAPP2_1_OR_GREATER", "NETCOREAPP2_2_OR_GREATER",
            "NETCOREAPP3_0_OR_GREATER", "NETCOREAPP3_1_OR_GREATER" };
        symbols.AddRange(Enumerable.Range(5, 6).Select(version => $"NET{version}_0_OR_GREATER"));
        if (debug) symbols.Add("DEBUG");
        var options = new CSharpParseOptions(LanguageVersion.Latest, preprocessorSymbols: symbols);
        SyntaxTree[] trees = sources.OrderBy(source => source.Key, StringComparer.Ordinal)
            .Select(source => CSharpSyntaxTree.ParseText(source.Value, options, source.Key.Replace('\\', '/'))).ToArray();
        // Match the game project's implicit SDK usings. Project global usings are present in trees as well.
        SyntaxTree implicitUsings = CSharpSyntaxTree.ParseText("""
            global using System;
            global using System.Collections.Generic;
            global using System.IO;
            global using System.Linq;
            global using System.Net.Http;
            global using System.Threading;
            global using System.Threading.Tasks;
            """, options);
        var compilation = CSharpCompilation.Create("ClockReadInspection", trees.Append(implicitUsings), References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var result = new List<ClockRead>();
        foreach (SyntaxTree tree in trees)
        {
            SyntaxNode root = tree.GetRoot();
            var syntaxError = tree.GetDiagnostics().FirstOrDefault(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
            if (syntaxError != null)
                throw new InvalidDataException($"Cannot inspect malformed C#: {syntaxError}");
            SemanticModel model = compilation.GetSemanticModel(tree);
            foreach (IdentifierNameSyntax name in root.DescendantNodes().OfType<IdentifierNameSyntax>())
            {
                if (!CandidateNames.Contains(name.Identifier.ValueText) || IsNameOf(name, model)) continue;
                SymbolInfo info = model.GetSymbolInfo(name);
                ISymbol? symbol = info.Symbol;
                if (symbol == null && info.CandidateSymbols is [ISymbol candidate]) symbol = candidate;
                string? api = ClockApi(symbol);
                if (api == null) continue;
                ISymbol member = model.GetEnclosingSymbol(name.SpanStart)
                    ?? throw new InvalidDataException($"Cannot identify owner of {tree.FilePath}:{name.SpanStart}");
                var site = new ClockSite(tree.FilePath, MemberId(member), api, Tokens(Context(name)));
                var position = tree.GetLineSpan(name.Span).StartLinePosition;
                result.Add(new ClockRead(site, position.Line + 1, position.Character + 1));
            }
        }
        return result;
    }

    public static IReadOnlyList<string> Check(IReadOnlyList<ClockRead> reads, ClockAllowlist allowlist)
    {
        var errors = new List<string>();
        if (allowlist.SchemaVersion != 1) return [$"Unsupported clock allowlist schema {allowlist.SchemaVersion}."];
        var approved = new Dictionary<ClockSite, ClockException>();
        foreach (ClockException entry in allowlist.Sites)
        {
            if (entry.Count < 1 || string.IsNullOrWhiteSpace(entry.Reason) || string.IsNullOrWhiteSpace(entry.Member) ||
                string.IsNullOrWhiteSpace(entry.Api) || string.IsNullOrWhiteSpace(entry.Context) ||
                string.IsNullOrWhiteSpace(entry.Path) || entry.Path.Contains('\\') ||
                System.IO.Path.IsPathRooted(entry.Path) || entry.Path.Split('/').Any(part => part is ".." or "." or "bin" or "obj"))
                errors.Add($"Invalid clock exception for {entry.Path} / {entry.Member}: require an exact site, positive count and review reason.");
            if (!approved.TryAdd(entry.Site, entry)) errors.Add($"Duplicate clock exception: {entry.Path} / {entry.Member} / {entry.Api}.");
        }
        Dictionary<ClockSite, ClockRead[]> actual = reads.GroupBy(read => read.Site).ToDictionary(group => group.Key, group => group.ToArray());
        foreach ((ClockSite site, ClockRead[] occurrences) in actual)
        {
            string location = $"{site.Path}:{occurrences[0].Line} ({site.Member})";
            if (!approved.TryGetValue(site, out ClockException? exception))
                errors.Add($"Unapproved clock read {site.Api} at {location}. Use SystemClock for gameplay; infrastructure needs a reviewed exact-site exception.");
            else if (occurrences.Length != exception.Count)
                errors.Add($"Clock site count changed at {location}: expected {exception.Count}, found {occurrences.Length}.");
        }
        foreach (ClockSite site in approved.Keys.Where(site => !actual.ContainsKey(site)))
            errors.Add($"Stale clock exception: {site.Path} / {site.Member} / {site.Api}. Remove it or review the changed statement.");
        return errors;
    }

    private static string? ClockApi(ISymbol? symbol)
    {
        string? type = symbol?.ContainingType?.ToDisplayString();
        string? name = symbol?.Name;
        bool clock = (type, symbol) switch
        {
            ("System.DateTimeOffset", IPropertySymbol) => name is "UtcNow" or "Now",
            ("System.DateTime", IPropertySymbol) => name is "UtcNow" or "Now" or "Today",
            ("System.Environment", IPropertySymbol) => name is "TickCount" or "TickCount64",
            ("System.Diagnostics.Stopwatch", IMethodSymbol) => name is "GetTimestamp" or "StartNew" or "Start" or "Restart",
            ("System.TimeProvider", IPropertySymbol) => name == "System",
            _ => false
        };
        return clock ? type + "." + name : null;
    }

    private static bool IsNameOf(SyntaxNode name, SemanticModel model) => name.Ancestors().OfType<InvocationExpressionSyntax>()
        .Any(call => call.Expression is IdentifierNameSyntax { Identifier.ValueText: "nameof" } &&
            model.GetOperation(call) is Microsoft.CodeAnalysis.Operations.INameOfOperation);

    private static string MemberId(ISymbol symbol)
    {
        while (symbol is IMethodSymbol { MethodKind: MethodKind.AnonymousFunction }) symbol = symbol.ContainingSymbol;
        string id = symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
        return symbol is IMethodSymbol { MethodKind: MethodKind.LocalFunction }
            ? MemberId(symbol.ContainingSymbol) + "/" + id : id;
    }

    private static SyntaxNode Context(SyntaxNode name)
    {
        foreach (SyntaxNode ancestor in name.Ancestors())
        {
            if (ancestor is VariableDeclaratorSyntax or ArrowExpressionClauseSyntax) return ancestor;
            if (ancestor is IfStatementSyntax conditional) return conditional.Condition;
            if (ancestor is WhileStatementSyntax loop) return loop.Condition;
            if (ancestor is StatementSyntax and not BlockSyntax) return ancestor;
        }
        throw new InvalidDataException($"Cannot identify clock statement: {name}");
    }

    private static string Tokens(SyntaxNode node) => string.Join(" ", node.DescendantTokens().Select(token => token.Text));
}
