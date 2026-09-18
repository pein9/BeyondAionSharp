using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Aion.QuestPlanExtractor;

internal static partial class Program
{
	private const string GeneratedBy = "tools/Aion.QuestPlanExtractor";

	public static int Main(string[] args)
	{
		try
		{
			Options options = Options.Parse(args);
			IReadOnlyDictionary<int, QuestMetadata> quests = LoadCustomQuests(options.ClassifierPath);
			Dictionary<int, string> handlers = FindHandlers(options.HandlerRoot);
			var drafts = new List<QuestDraft>(quests.Count);

			foreach ((int questId, QuestMetadata metadata) in quests.OrderBy(entry => entry.Key))
			{
				if (!handlers.TryGetValue(questId, out string? path))
					throw new InvalidOperationException($"No C# handler found for obtainable custom quest {questId}.");
				drafts.Add(Extract(questId, metadata, path, options.RepositoryRoot));
			}

			var document = new ExtractorDocument(
				SchemaVersion: 1,
				GeneratedBy,
				options.JavaCommit,
				new ExtractorCounts(
					drafts.Count,
					drafts.Count(draft => draft.DialogCandidateActions.Count > 0),
					drafts.Count(draft => draft.SpecialOperations.Count > 0),
					drafts.Sum(draft => draft.Registrations.Count),
					drafts.Sum(draft => draft.Decisions.Count)),
				drafts);

			Directory.CreateDirectory(Path.GetDirectoryName(options.OutputPath)!);
			File.WriteAllText(options.OutputPath, JsonSerializer.Serialize(document, JsonOptions));
			Console.WriteLine(
				$"Extracted {drafts.Count} custom quest drafts, {document.Counts.Registrations} registrations, " +
				$"{document.Counts.Decisions} dialog decisions, and {document.Counts.SpecialHandlers} special handlers.");
			return 0;
		}
		catch (Exception exception)
		{
			Console.Error.WriteLine(exception.Message);
			return 1;
		}
	}

	private static IReadOnlyDictionary<int, QuestMetadata> LoadCustomQuests(string path)
	{
		using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
		var result = new Dictionary<int, QuestMetadata>();
		foreach (JsonElement quest in document.RootElement.GetProperty("quests").EnumerateArray())
		{
			if (quest.GetProperty("availability").GetString() != "obtainable" ||
				quest.GetProperty("handlerKind").GetString() != "custom")
				continue;
			int id = quest.GetProperty("id").GetInt32();
			result.Add(id, new QuestMetadata(
				quest.GetProperty("zone").GetString()!,
				quest.GetProperty("race").GetString()!));
		}
		return result;
	}

	private static Dictionary<int, string> FindHandlers(string root)
	{
		var result = new Dictionary<int, string>();
		foreach (string path in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories).Order())
		{
			Match match = HandlerFileName().Match(Path.GetFileNameWithoutExtension(path));
			if (!match.Success)
				continue;
			int questId = int.Parse(match.Groups[1].Value);
			if (!result.TryAdd(questId, Path.GetFullPath(path)))
				throw new InvalidOperationException($"Multiple C# handlers found for quest {questId}.");
		}
		return result;
	}

	private static QuestDraft Extract(int questId, QuestMetadata metadata, string path, string repositoryRoot)
	{
		SyntaxTree tree = CSharpSyntaxTree.ParseText(File.ReadAllText(path), path: path);
		Diagnostic? error = tree.GetDiagnostics().FirstOrDefault(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
		if (error != null)
			throw new InvalidOperationException($"Cannot parse {path}: {error}");

		CompilationUnitSyntax root = tree.GetCompilationUnitRoot();
		MethodDeclarationSyntax? register = FindMethod(root, "Register");
		MethodDeclarationSyntax? dialog = FindMethod(root, "OnDialogEvent");
		IReadOnlyDictionary<string, IReadOnlyList<int>> integerCollections = FindIntegerCollections(register);
		IReadOnlyList<RegistrationDraft> registrations = ExtractRegistrations(register, integerCollections, tree);
		IReadOnlyList<DialogDecisionDraft> decisions = ExtractDecisions(dialog, tree);
		IReadOnlyList<string> actions = dialog == null
			? []
			: dialog.DescendantNodes()
				.OfType<MemberAccessExpressionSyntax>()
				.Where(IsDialogAction)
				.Select(member => member.Name.Identifier.ValueText)
				.Distinct(StringComparer.Ordinal)
				.ToArray();
		IReadOnlyList<SpecialOperationDraft> specialOperations = ExtractSpecialOperations(root, tree);

		return new QuestDraft(
			questId,
			metadata.Zone,
			metadata.Race,
			Path.GetRelativePath(repositoryRoot, path).Replace('\\', '/'),
			registrations,
			actions,
			decisions,
			specialOperations);
	}

	private static MethodDeclarationSyntax? FindMethod(CompilationUnitSyntax root, string name) =>
		root.DescendantNodes().OfType<MethodDeclarationSyntax>()
			.SingleOrDefault(method => method.Identifier.ValueText == name);

	private static IReadOnlyDictionary<string, IReadOnlyList<int>> FindIntegerCollections(MethodDeclarationSyntax? method)
	{
		var result = new Dictionary<string, IReadOnlyList<int>>(StringComparer.Ordinal);
		if (method == null)
			return result;

		foreach (VariableDeclaratorSyntax variable in method.DescendantNodes().OfType<VariableDeclaratorSyntax>())
		{
			if (variable.Initializer?.Value is not ExpressionSyntax initializer)
				continue;
			int[] values = initializer.DescendantNodesAndSelf()
				.OfType<LiteralExpressionSyntax>()
				.Where(literal => literal.IsKind(SyntaxKind.NumericLiteralExpression) && literal.Token.Value is int)
				.Select(literal => (int)literal.Token.Value!)
				.ToArray();
			if (values.Length > 0)
				result[variable.Identifier.ValueText] = values;
		}
		foreach (ForEachStatementSyntax loop in method.DescendantNodes().OfType<ForEachStatementSyntax>())
		{
			if (loop.Expression is IdentifierNameSyntax collection &&
				result.TryGetValue(collection.Identifier.ValueText, out IReadOnlyList<int>? values))
				result[loop.Identifier.ValueText] = values;
		}
		return result;
	}

	private static IReadOnlyList<RegistrationDraft> ExtractRegistrations(
		MethodDeclarationSyntax? method,
		IReadOnlyDictionary<string, IReadOnlyList<int>> integerCollections,
		SyntaxTree tree)
	{
		if (method == null)
			return [];
		var result = new List<RegistrationDraft>();
		foreach (ExpressionStatementSyntax statement in method.DescendantNodes().OfType<ExpressionStatementSyntax>())
		{
			if (statement.Expression is not InvocationExpressionSyntax invocation)
				continue;
			InvocationExpressionSyntax[] chain = invocation.DescendantNodesAndSelf()
				.OfType<InvocationExpressionSyntax>()
				.OrderBy(call => call.SpanStart)
				.ToArray();
			string[] methods = chain.Select(InvocationName).Where(name => name != null).Cast<string>().ToArray();
			if (!methods.Any(name => name.StartsWith("Register", StringComparison.Ordinal) || name.StartsWith("AddOn", StringComparison.Ordinal)))
				continue;

			InvocationExpressionSyntax? npcRegistration = chain.FirstOrDefault(call => InvocationName(call) == "RegisterQuestNpc");
			int[] npcIds = npcRegistration == null
				? []
				: ResolveIntegers(npcRegistration.ArgumentList.Arguments.Select(argument => argument.Expression), integerCollections);
			result.Add(new RegistrationDraft(
				methods,
				npcIds,
				Normalize(statement.Expression),
				LineOf(tree, statement)));
		}
		return result;
	}

	private static int[] ResolveIntegers(
		IEnumerable<ExpressionSyntax> expressions,
		IReadOnlyDictionary<string, IReadOnlyList<int>> integerCollections)
	{
		var result = new List<int>();
		foreach (ExpressionSyntax expression in expressions)
		{
			if (expression is LiteralExpressionSyntax literal && literal.Token.Value is int value)
				result.Add(value);
			else if (expression is IdentifierNameSyntax identifier && integerCollections.TryGetValue(identifier.Identifier.ValueText, out IReadOnlyList<int>? values))
				result.AddRange(values);
		}
		return result.Distinct().Order().ToArray();
	}

	private static IReadOnlyList<DialogDecisionDraft> ExtractDecisions(MethodDeclarationSyntax? method, SyntaxTree tree)
	{
		if (method == null)
			return [];
		var result = new List<DialogDecisionDraft>();
		foreach (ReturnStatementSyntax statement in method.DescendantNodes().OfType<ReturnStatementSyntax>())
		{
			ExpressionSyntax? expression = statement.Expression;
			if (expression == null)
				continue;
			string? resultMethod = expression is InvocationExpressionSyntax invocation ? InvocationName(invocation) : null;
			string[] conditions = AncestorConditions(statement).ToArray();
			string[] actions = ContextMemberNames(statement, "DialogAction");
			string[] statuses = ContextMemberNames(statement, "QuestStatus");
			int[] targetNpcIds = ContextTargetIds(statement);
			result.Add(new DialogDecisionDraft(
				targetNpcIds,
				statuses,
				actions,
				conditions,
				resultMethod,
				Normalize(expression),
				LineOf(tree, statement)));
		}
		return result;
	}

	private static IEnumerable<string> AncestorConditions(SyntaxNode node)
	{
		foreach (SyntaxNode ancestor in node.Ancestors().Reverse())
		{
			if (ancestor is IfStatementSyntax condition)
			{
				bool inElse = condition.Else?.Span.Contains(node.Span) == true;
				yield return inElse ? $"else !({Normalize(condition.Condition)})" : Normalize(condition.Condition);
			}
			else if (ancestor is SwitchSectionSyntax section && section.Parent is SwitchStatementSyntax @switch)
			{
				foreach (SwitchLabelSyntax label in section.Labels)
					yield return $"{Normalize(@switch.Expression)} {Normalize(label)}";
			}
		}
	}

	private static string[] ContextMemberNames(SyntaxNode node, string owner)
	{
		return node.Ancestors()
			.SelectMany(ancestor => ancestor switch
			{
				IfStatementSyntax condition => condition.Condition.DescendantNodesAndSelf(),
				SwitchSectionSyntax section => section.Labels.SelectMany(label => label.DescendantNodesAndSelf()),
				_ => []
			})
			.OfType<MemberAccessExpressionSyntax>()
			.Where(member => member.Expression.ToString() == owner)
			.Select(member => member.Name.Identifier.ValueText)
			.Distinct(StringComparer.Ordinal)
			.ToArray();
	}

	private static int[] ContextTargetIds(SyntaxNode node)
	{
		var result = new HashSet<int>();
		foreach (SyntaxNode ancestor in node.Ancestors())
		{
			IEnumerable<SyntaxNode> candidates = ancestor switch
			{
				IfStatementSyntax condition when ContainsTargetReference(condition.Condition) => [condition.Condition],
				SwitchSectionSyntax section when section.Parent is SwitchStatementSyntax @switch && ContainsTargetReference(@switch.Expression) =>
					section.Labels,
				_ => []
			};
			foreach (LiteralExpressionSyntax literal in candidates.SelectMany(candidate => candidate.DescendantNodesAndSelf()).OfType<LiteralExpressionSyntax>())
			{
				if (literal.Token.Value is int value)
					result.Add(value);
			}
		}
		return result.Order().ToArray();
	}

	private static bool ContainsTargetReference(SyntaxNode node)
	{
		string text = node.ToString();
		return text.Contains("targetId", StringComparison.OrdinalIgnoreCase) ||
			text.Contains("GetTargetId", StringComparison.Ordinal) ||
			text.Contains("GetNpcId", StringComparison.Ordinal);
	}

	private static IReadOnlyList<SpecialOperationDraft> ExtractSpecialOperations(CompilationUnitSyntax root, SyntaxTree tree)
	{
		var result = new List<SpecialOperationDraft>();
		foreach (InvocationExpressionSyntax invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
		{
			string expression = Normalize(invocation);
			string invokedExpression = Normalize(invocation.Expression);
			string method = InvocationName(invocation) ?? string.Empty;
			string? kind = method.Contains("Spawn", StringComparison.OrdinalIgnoreCase)
				? "spawn"
				: method.Contains("Teleport", StringComparison.OrdinalIgnoreCase) || invokedExpression.Contains("TeleportService", StringComparison.Ordinal)
					? "teleport"
					: invokedExpression.Contains("InstanceService", StringComparison.Ordinal)
						? "instance"
						: null;
			if (kind != null)
				result.Add(new SpecialOperationDraft(kind, method, expression, LineOf(tree, invocation)));
		}
		return result;
	}

	private static bool IsDialogAction(MemberAccessExpressionSyntax member) =>
		member.Expression.ToString() == "DialogAction";

	private static string? InvocationName(InvocationExpressionSyntax invocation) => invocation.Expression switch
	{
		IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
		MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText,
		_ => null,
	};

	private static int LineOf(SyntaxTree tree, SyntaxNode node) =>
		tree.GetLineSpan(node.Span).StartLinePosition.Line + 1;

	private static string Normalize(SyntaxNode node) => Whitespace().Replace(node.ToString(), " ").Trim();

	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		WriteIndented = true,
	};

	[GeneratedRegex(@"^_(\d+)")]
	private static partial Regex HandlerFileName();

	[GeneratedRegex(@"\s+")]
	private static partial Regex Whitespace();
}

internal sealed record Options(
	string RepositoryRoot,
	string HandlerRoot,
	string ClassifierPath,
	string OutputPath,
	string JavaCommit)
{
	public static Options Parse(string[] args)
	{
		var values = new Dictionary<string, string>(StringComparer.Ordinal);
		for (int index = 0; index < args.Length; index += 2)
		{
			if (index + 1 >= args.Length || !args[index].StartsWith("--", StringComparison.Ordinal))
				throw new ArgumentException("Arguments must be --name value pairs.");
			values[args[index][2..]] = args[index + 1];
		}

		string repositoryRoot = Path.GetFullPath(values.GetValueOrDefault("repo", Directory.GetCurrentDirectory()));
		return new Options(
			repositoryRoot,
			Path.GetFullPath(values.GetValueOrDefault("handlers", Path.Combine(repositoryRoot, "src/Aion.GameServer/Handlers/Quest"))),
			Path.GetFullPath(values.GetValueOrDefault("classifier", Path.Combine(repositoryRoot, "parity-artifacts/e2e/obtainable-quests.json"))),
			Path.GetFullPath(values.GetValueOrDefault("output", Path.Combine(repositoryRoot, "parity-artifacts/e2e/custom-quest-handler-drafts.json"))),
			values.GetValueOrDefault("java-commit", "ce54b7931546cddafb970d20c9f71fec6d48c83b"));
	}
}

internal sealed record QuestMetadata(string Zone, string Race);

internal sealed record ExtractorDocument(
	int SchemaVersion,
	string GeneratedBy,
	string JavaCommit,
	ExtractorCounts Counts,
	IReadOnlyList<QuestDraft> Quests);

internal sealed record ExtractorCounts(
	int CustomQuests,
	int WithDialogActions,
	int SpecialHandlers,
	int Registrations,
	int Decisions);

internal sealed record QuestDraft(
	int QuestId,
	string Zone,
	string Race,
	string Source,
	IReadOnlyList<RegistrationDraft> Registrations,
	IReadOnlyList<string> DialogCandidateActions,
	IReadOnlyList<DialogDecisionDraft> Decisions,
	IReadOnlyList<SpecialOperationDraft> SpecialOperations);

internal sealed record RegistrationDraft(
	IReadOnlyList<string> Methods,
	IReadOnlyList<int> NpcIds,
	string Expression,
	int Line);

internal sealed record DialogDecisionDraft(
	IReadOnlyList<int> TargetNpcIds,
	IReadOnlyList<string> QuestStatuses,
	IReadOnlyList<string> DialogActions,
	IReadOnlyList<string> Conditions,
	string? ResultMethod,
	string Result,
	int Line);

internal sealed record SpecialOperationDraft(string Kind, string Method, string Expression, int Line);
