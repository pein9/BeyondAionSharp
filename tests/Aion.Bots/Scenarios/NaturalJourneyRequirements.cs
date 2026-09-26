using System.Diagnostics.CodeAnalysis;

namespace Aion.Bots.Scenarios;

/// <summary>Runtime acceptance requirements retained when the journey moved out of its xUnit host.</summary>
internal static class NaturalJourneyRequirements
{
	public static void True([DoesNotReturnIf(false)] bool condition, string? message = null)
	{
		if (!condition) throw new InvalidDataException(message ?? "Natural journey requirement failed.");
	}
	public static void Equal<T>(T expected, T actual) =>
		True(EqualityComparer<T>.Default.Equals(expected, actual), $"Natural journey expected {expected}, observed {actual}.");
	public static void Contains<T>(T expected, IEnumerable<T> items) =>
		True(items.Contains(expected), $"Natural journey did not observe required value {expected}.");
	public static void Contains<T>(IEnumerable<T> items, Func<T, bool> predicate) =>
		True(items.Any(predicate), "Natural journey did not observe a required packet/state transition.");
	public static void DoesNotContain<T>(T value, IEnumerable<T> items) =>
		True(!items.Contains(value), $"Natural journey crossed forbidden boundary {value}.");
	public static void All<T>(IEnumerable<T> items, Action<T> check) { foreach (T item in items) check(item); }
	public static T IsType<T>(object? value) => value is T typed ? typed :
		throw new InvalidDataException($"Natural journey expected {typeof(T).Name}, observed {value ?? "null"}.");
	public static void NotNull([NotNull] object? value) => True(value != null, "Natural journey required an observed object.");
	[DoesNotReturn] public static void Fail(string message) => throw new InvalidDataException(message);
}
