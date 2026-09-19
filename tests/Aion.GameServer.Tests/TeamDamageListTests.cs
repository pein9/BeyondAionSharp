using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Model.Account;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Team;
using Aion.GameServer.Model.Team.Group;
using Aion.GameServer.World.Knownlist;
using PlayerGroupMember = Aion.GameServer.Model.Team.Group.PlayerGroupMember;

namespace Aion.GameServer.Tests;

public sealed class TeamDamageListTests
{
    [Fact]
    public void GroupsPlayerDamageWithoutCastingInvariantGenericTypes()
    {
        Player owner = NewPlayer(1), first = NewPlayer(2), second = NewPlayer(3), solo = NewPlayer(4);
        var group = new PlayerGroup(new PlayerGroupMember(first), TeamType.GROUP, 100);
        first.SetPlayerGroup(group);
        second.SetPlayerGroup(group);
        owner.SetKnownlist(new DamageKnownList(owner, first, second, solo));
        var damages = new DamageList([Damage(first, 20), Damage(second, 50), Damage(solo, 60)], owner);

        TeamDamageList teams = damages.ToTeamDamages();

        Assert.Equal(3, damages.GetCreatureDamages().Count);
        Assert.Equal(2, teams.GetCreatureOrTeamDamages().Count);
        Assert.Equal(130, teams.GetTotalDamage());
        Assert.Same(group, teams.GetMostDamage().GetAttacker());
        Assert.Equal(70, teams.GetMostDamage().GetDamage());
        Assert.Same(second, teams.GetMostDamageByTeam(group).GetAttacker());
        Assert.Equal(50, teams.GetMostDamageByTeam(group).GetDamage());
    }

    [Fact]
    public void TiedTeamMembersKeepFirstContributorAndEmptyDamageHasNoWinner()
    {
        Player owner = NewPlayer(10), first = NewPlayer(11), second = NewPlayer(12);
        var group = new PlayerGroup(new PlayerGroupMember(first), TeamType.GROUP, 101);
        first.SetPlayerGroup(group);
        second.SetPlayerGroup(group);
        owner.SetKnownlist(new DamageKnownList(owner, first, second));
        var damages = new DamageList([Damage(first, 40), Damage(second, 40)], owner);
        Assert.Same(first, damages.ToTeamDamages().GetMostDamageByTeam(group).GetAttacker());

        TeamDamageList empty = new DamageList([], owner).ToTeamDamages();
        Assert.Empty(empty.GetCreatureOrTeamDamages());
        Assert.Equal(0, empty.GetTotalDamage());
        Assert.Null(empty.GetMostDamage());
        Assert.Null(empty.GetMostDamageByTeam(group));
    }

    private static AggroInfo Damage(Player player, int amount)
    {
        var info = new AggroInfo(player);
        info.AddDamage(amount);
        return info;
    }

    private static Player NewPlayer(int id) =>
        new(new PlayerAccountData(new PlayerCommonData(id), new PlayerAppearance()), new Account(id));

    // Isolate damage accounting from world visibility and packet delivery.
    private sealed class DamageKnownList : KnownList
    {
        public DamageKnownList(Creature owner, params Player[] players) : base(owner)
        {
            foreach (Player player in players)
                KnownObjects[player.ObjectId] = new KnownObject(player);
        }
    }
}
