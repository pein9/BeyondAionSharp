using System.Reflection;
using System.Reflection.Emit;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.Utils.Stats;

namespace Aion.GameServer.Tests;

public sealed class EquipmentRankParityTests
{
    [Theory]
    [InlineData("EquipItem")]
    [InlineData("VerifyRankLimits")]
    public void EquipmentUsesRankIdNotEnumOrdinal(string methodName)
    {
        // Java Equipment uses AbyssRankEnum.getId(), whose IDs are 1..18, not ordinals 0..17.
        // Check the compiled call: an omitted namespace import silently binds the global Enum fallback.
        var method = typeof(Equipment).GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!;
        var calls = Calls(method).Where(call => call.Name == "GetId").ToArray();
        Assert.Single(calls);
        Assert.Equal(typeof(AbyssRankEnumExtensions), calls[0].DeclaringType);
    }

    [Fact]
    public void UnrestrictedItemsAcceptEveryJavaRankAndExplicitLimitsKeepTheirBoundaries()
    {
        var ranks = Enum.GetValues<AbyssRankEnum>();
        Assert.Equal(18, ranks.Length);
        var unrestricted = new ItemUseLimits();
        var restricted = new ItemUseLimits { minRank = 5, maxRank = 9 };
        for (int ordinal = 0; ordinal < ranks.Length; ordinal++)
        {
            int id = ranks[ordinal].GetId();
            Assert.Equal(ordinal + 1, id);
            Assert.True(unrestricted.VerifyRank(id));
            Assert.Equal(id is >= 5 and <= 9, restricted.VerifyRank(id));
        }
        Assert.False(unrestricted.VerifyRank(0));
        Assert.False(unrestricted.VerifyRank(19));
    }

    private static IEnumerable<MethodBase> Calls(MethodInfo method)
    {
        var opcodes = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.FieldType == typeof(OpCode)).Select(field => (OpCode)field.GetValue(null)!)
            .ToDictionary(opcode => unchecked((ushort)opcode.Value));
        byte[] il = method.GetMethodBody()!.GetILAsByteArray()!;
        for (int offset = 0; offset < il.Length;)
        {
            ushort code = il[offset++];
            if (code == 0xfe) code = (ushort)(0xfe00 | il[offset++]);
            var opcode = opcodes[code];
            if (opcode == OpCodes.Call || opcode == OpCodes.Callvirt)
                yield return method.Module.ResolveMethod(BitConverter.ToInt32(il, offset))!;
            offset += opcode.OperandType switch
            {
                OperandType.InlineNone => 0,
                OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
                OperandType.InlineVar => 2,
                OperandType.InlineI8 or OperandType.InlineR => 8,
                OperandType.InlineSwitch => 4 + 4 * BitConverter.ToInt32(il, offset),
                _ => 4
            };
        }
    }
}
