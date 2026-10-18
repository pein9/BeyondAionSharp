using System.Reflection;
using System.Reflection.Emit;
using Aion.GameServer.Utils.Extensions;

namespace Aion.GameServer.Tests;

/// <summary>
/// The enum ordinal gap of CLAUDE.md, in one of its C# forms: an enum whose Java id differs from its C# value has its own
/// <c>GetId(this TEnum)</c> extension, but a file that does not import that extension's namespace binds <c>GetId()</c>
/// to the global fallback <see cref="EnumGetIdExtension.GetId(Enum)"/> (Convert.ToInt32) and writes the C# value instead
/// of Java's id. SM_HOUSE_BIDS did so with HouseType (HOUSE went out as 2, Java's id is 1). This scans the compiled game
/// server for a box of such an enum followed by a call of the fallback.
/// </summary>
public sealed class EnumGetIdBindingTests
{
	private static readonly Dictionary<short, OpCode> OpCodesByValue = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
		.Select(field => (OpCode)field.GetValue(null)!).ToDictionary(code => code.Value);

	[Fact]
	public void NoEnumWithItsOwnJavaIdUsesTheGlobalGetIdFallback()
	{
		Assembly assembly = typeof(EnumGetIdExtension).Assembly;
		MethodInfo fallback = typeof(EnumGetIdExtension).GetMethod(nameof(EnumGetIdExtension.GetId))!;
		HashSet<Type> divergent = DivergentEnums(assembly);
		Assert.Contains(typeof(Aion.GameServer.Model.Templates.Housing.HouseType), divergent);

		var offenders = new List<string>();
		foreach (Type type in assembly.GetTypes())
		{
			foreach (MethodBase method in type.GetMethods(AllDeclared).Cast<MethodBase>().Concat(type.GetConstructors(AllDeclared)))
			{
				byte[]? il = method.GetMethodBody()?.GetILAsByteArray();
				if (il == null)
					continue;
				Type? boxed = null;
				foreach ((OpCode code, int operand) in Instructions(il))
				{
					if (code == OpCodes.Call && boxed != null && divergent.Contains(boxed) && Resolve(method, operand) == fallback)
						offenders.Add($"{type.FullName}.{method.Name} calls EnumGetIdExtension.GetId on {boxed.Name}");
					boxed = code == OpCodes.Box ? ResolveType(method, operand) : null;
				}
			}
		}
		Assert.True(offenders.Count == 0, string.Join(Environment.NewLine, offenders));
	}

	private const BindingFlags AllDeclared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

	/// <summary>Enums with their own GetId extension that gives another number than the enum's value for some member.</summary>
	private static HashSet<Type> DivergentEnums(Assembly assembly)
	{
		var divergent = new HashSet<Type>();
		foreach (MethodInfo method in assembly.GetTypes().Where(type => type.IsAbstract && type.IsSealed)
			.SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)))
		{
			ParameterInfo[] parameters = method.GetParameters();
			if (method.Name != "GetId" || parameters.Length != 1 || !parameters[0].ParameterType.IsEnum || !method.IsDefined(typeof(System.Runtime.CompilerServices.ExtensionAttribute)))
				continue;
			Type enumType = parameters[0].ParameterType;
			foreach (object value in Enum.GetValues(enumType))
			{
				object? id;
				try { id = method.Invoke(null, [value]); }
				catch (TargetInvocationException) { continue; }
				if (Convert.ToInt64(id) != Convert.ToInt64(value))
				{
					divergent.Add(enumType);
					break;
				}
			}
		}
		return divergent;
	}

	private static IEnumerable<(OpCode Code, int Operand)> Instructions(byte[] il)
	{
		int position = 0;
		while (position < il.Length)
		{
			short value = il[position++];
			if (value == 0xFE)
				value = (short)(0xFE00 | il[position++]);
			if (!OpCodesByValue.TryGetValue(value, out OpCode code))
				yield break;
			int operand = 0;
			switch (code.OperandType)
			{
				case OperandType.InlineNone:
					break;
				case OperandType.ShortInlineBrTarget:
				case OperandType.ShortInlineI:
				case OperandType.ShortInlineVar:
					position += 1;
					break;
				case OperandType.InlineVar:
					position += 2;
					break;
				case OperandType.InlineI8:
				case OperandType.InlineR:
					position += 8;
					break;
				case OperandType.InlineSwitch:
					int count = BitConverter.ToInt32(il, position);
					position += 4 + 4 * count;
					break;
				default:
					operand = BitConverter.ToInt32(il, position);
					position += 4;
					break;
			}
			yield return (code, operand);
		}
	}

	private static MethodBase? Resolve(MethodBase context, int token)
	{
		try { return context.Module.ResolveMethod(token, GenericArguments(context.DeclaringType), GenericArguments(context)); }
		catch (ArgumentException) { return null; }
	}

	private static Type? ResolveType(MethodBase context, int token)
	{
		try { return context.Module.ResolveType(token, GenericArguments(context.DeclaringType), GenericArguments(context)); }
		catch (ArgumentException) { return null; }
	}

	private static Type[]? GenericArguments(Type? type) => type is { IsGenericType: true } ? type.GetGenericArguments() : null;

	private static Type[]? GenericArguments(MethodBase method) => method is MethodInfo { IsGenericMethod: true } ? method.GetGenericArguments() : null;
}
