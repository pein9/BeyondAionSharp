using Aion.GameServer.Model;
using Aion.GameServer.Model.Account;
using Aion.GameServer.Model.Autogroup;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.Services;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Tests;

/// <summary>Java's auto-group queue (AutoGroupService, AutoInstance) where the port's C# collections behave differently.</summary>
public sealed class AutoGroupParityTests
{
	private const long Now = 1_791_626_400_000L; // Saturday 2026-10-10 10:00 UTC

	[Fact]
	public void QueuedPartiesThatCompareEqualKeepTheirOrderAsJavaListSortDoes()
	{
		SystemClock.UseSource(() => Now);
		try
		{
			// LookingForParty.compareTo: entry requests by descending ordinal, then more members first, then the earlier
			// registration. The first two register alone in the same millisecond, so they compare equal.
			var first = new LookingForParty(Player(1, "Firstqueue"), EntryRequestType.NEW_GROUP_ENTRY, 24);
			var second = new LookingForParty(Player(2, "Secondqueue"), EntryRequestType.NEW_GROUP_ENTRY, 24);
			var quick = new LookingForParty(Player(3, "Quickqueue"), EntryRequestType.QUICK_GROUP_ENTRY, 24);
			Assert.Equal(0, first.CompareTo(second));
			var queue = new List<LookingForParty> { first, second, quick };
			AutoGroupService.SortQueue(queue);
			// Java's List.sort(null) is stable: [quick, first, second]. List<T>.Sort gave [quick, second, first].
			Assert.Equal([quick, first, second], queue);
		}
		finally
		{
			SystemClock.UseSystemClock();
		}
	}

	private static Player Player(int objectId, string name)
	{
		var common = new PlayerCommonData(objectId);
		common.SetName(name);
		common.SetRace(Race.ASMODIANS);
		common.SetPlayerClass(PlayerClass.GLADIATOR);
		var account = new Account(objectId + 10_000);
		account.SetName(name.ToLowerInvariant());
		return new Player(new PlayerAccountData(common, new PlayerAppearance()), account);
	}
}
