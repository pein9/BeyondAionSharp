using System.Reflection;

namespace Aion.GameServer.Utils;

/// <summary>
/// Java <c>Enum.valueOf</c> semantics: exact, case-sensitive constant names only.
/// Unlike .NET <see cref="Enum.Parse{TEnum}(string)"/>, numeric strings and comma-separated
/// combinations are never accepted.
/// </summary>
internal static class JavaEnum
{
    internal static TEnum ValueOf<TEnum>(string value) where TEnum : struct, Enum
    {
        if (value is null)
            throw new NullReferenceException("Name is null");

        if (TryValueOf(value, out TEnum result))
            return result;

        throw new ArgumentException($"No enum constant {typeof(TEnum).FullName}.{value}");
    }

    internal static bool TryValueOf<TEnum>(string? value, out TEnum result) where TEnum : struct, Enum
    {
        if (value is not null)
        {
            foreach (string name in Enum.GetNames<TEnum>())
            {
                if (string.Equals(name, value, StringComparison.Ordinal))
                {
                    result = Enum.Parse<TEnum>(name);
                    return true;
                }
            }
        }

        result = default;
        return false;
    }

    /// <summary>
    /// Java <c>values()</c> semantics: the constants in declaration order. <see cref="Enum.GetValues{TEnum}"/> sorts them by
    /// underlying value instead, which differs for enums with explicit, non-sequential values.
    /// </summary>
    internal static TEnum[] Values<TEnum>() where TEnum : struct, Enum => Array.ConvertAll(Values(typeof(TEnum)), value => (TEnum)value);

    /// <summary>Non-generic <see cref="Values{TEnum}"/>, for Java <c>Class.getEnumConstants()</c>.</summary>
    internal static object[] Values(Type enumType)
    {
        if (!enumType.IsEnum)
            throw new ArgumentException("Type provided must be an Enum.", nameof(enumType));

        return enumType.GetFields(BindingFlags.Public | BindingFlags.Static)
            .OrderBy(field => field.MetadataToken) // GetFields guarantees no order; field metadata rows follow declaration order
            .Select(field => field.GetValue(null)!)
            .ToArray();
    }
}
