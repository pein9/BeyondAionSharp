using System.Reflection;
using System.Runtime.CompilerServices;
using Aion.GameServer.Handlers.AdminCommands;
using Aion.GameServer.Model.Enchants;
using Aion.GameServer.Model.Items;

namespace Aion.GameServer.Tests;

public sealed class StatCommandParityTests
{
	[Fact]
	public void StatFunctionInfo_OmitsSlotOfEnchantEffectWithoutAttackStat()
	{
		// Java: owner instanceof EnchantEffect e && e.getItemSlot() != null. EnchantEffect only sets the slot for attack stats,
		// so it stays null in Java and default(ItemSlot) here.
		var info = new Stat.StatFunctionInfo(5, false, 30, Enchant(null), "StatAddFunction");

		Assert.EndsWith(", owner: EnchantEffect", info.ToString());
	}

	[Fact]
	public void StatFunctionInfo_ShowsSlotOfAttackEnchantEffect()
	{
		var info = new Stat.StatFunctionInfo(5, false, 30, Enchant(ItemSlot.SUB_HAND), "StatAddFunction");

		Assert.EndsWith(", owner: EnchantEffect (SUB_HAND)", info.ToString());
	}

	private static EnchantEffect Enchant(ItemSlot? slot)
	{
		var effect = (EnchantEffect)RuntimeHelpers.GetUninitializedObject(typeof(EnchantEffect));
		if (slot != null)
			typeof(EnchantEffect).GetField("itemSlot", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(effect, slot.Value);
		return effect;
	}
}
