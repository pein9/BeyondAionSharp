using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Aion.Commons.Logging;

public readonly record struct LogFingerprintResult(string Value, string NormalizedTemplate, string Frame);

/// <summary>Creates stable problem identities from log templates, exception types, and source locations.</summary>
public static class LogFingerprint
{
	private static readonly Regex ObjectDumpStart = new(
		@"(?<type>[A-Z][A-Za-z0-9_.+`]*)\s*\[",
		RegexOptions.Compiled | RegexOptions.CultureInvariant);
	private static readonly Regex BracketedHexDump = new(
		@"\[(?:[0-9A-Fa-f]{2}(?:[\s,:-]+|(?=\]))){2,}\]",
		RegexOptions.Compiled | RegexOptions.CultureInvariant);
	private static readonly Regex HexDump = new(
		@"(?<![0-9A-Fa-f])(?:[0-9A-Fa-f]{2}[\s:-]+){2,}[0-9A-Fa-f]{2}(?![0-9A-Fa-f])",
		RegexOptions.Compiled | RegexOptions.CultureInvariant);
	private static readonly Regex ContinuousHexDump = new(
		@"\b[0-9A-Fa-f]{16,}\b",
		RegexOptions.Compiled | RegexOptions.CultureInvariant);
	private static readonly Regex HexNumber = new(
		@"\b0[xX][0-9A-Fa-f]+\b",
		RegexOptions.Compiled | RegexOptions.CultureInvariant);
	private static readonly Regex DigitRun = new(
		@"\d+",
		RegexOptions.Compiled | RegexOptions.CultureInvariant);
	private static readonly Regex Whitespace = new(
		@"\s+",
		RegexOptions.Compiled | RegexOptions.CultureInvariant);
	private static readonly Regex LambdaMember = new(
		@"^<(?<owner>[^>]+)>b__\d+(?:_\d+)?$",
		RegexOptions.Compiled | RegexOptions.CultureInvariant);
	private static readonly Regex StateMachineType = new(
		@"<(?<owner>[^>]+)>d__\d+",
		RegexOptions.Compiled | RegexOptions.CultureInvariant);
	private static readonly Regex LocalFunctionMember = new(
		@"^<(?<owner>[^>]+)>g__(?<local>[^|]+)\|\d+_\d+$",
		RegexOptions.Compiled | RegexOptions.CultureInvariant);

	public static LogFingerprintResult Create<TState>(TState state, Exception? exception, string formattedMessage)
	{
		var template = GetTemplate(state) ?? formattedMessage;
		var normalizedTemplate = NormalizeTemplate(template);
		var frame = exception == null ? GetCallerLocation(state) : GetExceptionLocation(exception);
		var exceptionType = exception?.GetType().FullName ?? "<none>";
		var input = $"{exceptionType}\n{normalizedTemplate}\n{frame}";
		var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
		return new LogFingerprintResult(Convert.ToHexString(hash.AsSpan(0, 4)).ToLowerInvariant(), normalizedTemplate, frame);
	}

	internal static string NormalizeTemplate(string template)
	{
		var normalized = NormalizeObjectDumps(template);
		normalized = BracketedHexDump.Replace(normalized, "[<hex>]");
		normalized = HexDump.Replace(normalized, "<hex>");
		normalized = ContinuousHexDump.Replace(normalized, "<hex>");
		normalized = HexNumber.Replace(normalized, "0x#");
		normalized = DigitRun.Replace(normalized, "#");
		return Whitespace.Replace(normalized, " ").Trim();
	}

	internal static string NormalizeCodeLocation(string typeName, string memberName)
	{
		var stateMachine = StateMachineType.Match(typeName);
		var lambda = LambdaMember.Match(memberName);
		var localFunction = LocalFunctionMember.Match(memberName);
		if (stateMachine.Success && memberName == "MoveNext")
			memberName = stateMachine.Groups["owner"].Value;
		else if (lambda.Success)
			memberName = lambda.Groups["owner"].Value;
		else if (localFunction.Success)
			memberName = $"{localFunction.Groups["owner"].Value}.{localFunction.Groups["local"].Value}";

		var generatedTypeSeparator = typeName.IndexOf("+<", StringComparison.Ordinal);
		if (generatedTypeSeparator >= 0)
			typeName = typeName[..generatedTypeSeparator];
		return $"{typeName}.{memberName}";
	}

	private static string NormalizeObjectDumps(string value)
	{
		var output = new StringBuilder(value.Length);
		var offset = 0;
		while (offset < value.Length)
		{
			var match = ObjectDumpStart.Match(value, offset);
			if (!match.Success)
			{
				output.Append(value, offset, value.Length - offset);
				break;
			}

			var openBracket = value.IndexOf('[', match.Index, match.Length);
			var depth = 0;
			var closeBracket = -1;
			for (var index = openBracket; index < value.Length; index++)
			{
				if (value[index] == '[')
					depth++;
				else if (value[index] == ']' && --depth == 0)
				{
					closeBracket = index;
					break;
				}
			}

			if (closeBracket < 0)
			{
				output.Append(value, offset, value.Length - offset);
				break;
			}

			output.Append(value, offset, match.Index - offset);
			var typeName = match.Groups["type"].Value;
			var simpleNameSeparator = Math.Max(typeName.LastIndexOf('.'), typeName.LastIndexOf('+'));
			output.Append('<').Append(typeName[(simpleNameSeparator + 1)..]).Append('>');
			offset = closeBracket + 1;
		}
		return output.ToString();
	}

	private static string GetExceptionLocation(Exception exception)
	{
		var frames = new StackTrace(exception, fNeedFileInfo: false).GetFrames();
		if (frames == null)
			return "<unknown>";

		MethodBase? fallback = null;
		foreach (var frame in frames)
		{
			var method = frame.GetMethod();
			var type = method?.DeclaringType;
			if (method == null || type == null)
				continue;
			fallback ??= method;
			var typeName = type.FullName ?? type.Name;
			if (typeName.StartsWith("Aion.", StringComparison.Ordinal) &&
				!typeName.StartsWith("Aion.Commons.Logging.", StringComparison.Ordinal))
				return NormalizeCodeLocation(typeName, method.Name);
		}

		return fallback?.DeclaringType is { } fallbackType
			? NormalizeCodeLocation(fallbackType.FullName ?? fallbackType.Name, fallback.Name)
			: "<unknown>";
	}

	private static string GetCallerLocation<TState>(TState state)
	{
		string? type = null;
		string? member = null;
		if (state is IEnumerable<KeyValuePair<string, object?>> values)
		{
			foreach (var value in values)
			{
				if (value.Key == AionLog.CallerTypeKey)
					type = value.Value?.ToString();
				else if (value.Key == AionLog.CallerMemberKey)
					member = value.Value?.ToString();
			}
		}
		return type != null && member != null ? NormalizeCodeLocation(type, member) : "<unknown>";
	}

	private static string? GetTemplate<TState>(TState state)
	{
		if (state is IEnumerable<KeyValuePair<string, object?>> values)
		{
			foreach (var value in values)
			{
				if (value.Key == "{OriginalFormat}")
					return value.Value?.ToString();
			}
		}
		return null;
	}
}
