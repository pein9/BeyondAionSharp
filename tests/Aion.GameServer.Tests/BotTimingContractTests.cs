using Aion.Bots.Protocol;
using Aion.Bots.Timing;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.Tests;

public sealed class BotTimingContractTests
{
	[Fact]
	public void EnforcesJavaAttackCastTargetAndAnimationGates()
	{
		Assert.Equal("ce54b7931546cddafb970d20c9f71fec6d48c83b", BotTimingContract.Rules.JavaCommit);
		Assert.Equal(Enum.GetValues<BotBlockingActivity>().ToHashSet(), BotTimingContract.Rules.MovementBlockingActivities);
		var clock = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
		var timing = new BotTimingContract(clock);

		timing.RecordAttack(1000);
		Assert.Equal(TimeSpan.FromMilliseconds(700), timing.TimeUntilAttack(1000));
		Assert.Throws<InvalidOperationException>(() => timing.RecordAttack(1000));
		clock.Advance(TimeSpan.FromMilliseconds(700));
		timing.RecordAttack(1000);

		Assert.Throws<InvalidOperationException>(() => timing.RecordCastStarted(101, 77));
		timing.RecordTargetSelection(77);
		timing.RecordCastStarted(101, 77);
		Assert.Contains(BotBlockingActivity.Casting, timing.BlockingActivities);
		Assert.Throws<InvalidOperationException>(timing.EnsureCanMove);
		Assert.Equal(TimeSpan.FromMilliseconds(350), timing.TimeUntilCast(102));

		clock.Advance(TimeSpan.FromMilliseconds(100));
		timing.RecordCastResult(600);
		Assert.DoesNotContain(BotBlockingActivity.Casting, timing.BlockingActivities);
		Assert.Equal(TimeSpan.FromMilliseconds(600), timing.TimeUntilCast(102));
		clock.Advance(TimeSpan.FromMilliseconds(600));
		timing.RecordCastStarted(102, 77);
	}

	[Fact]
	public void UsesDecodedSkillCooldownsAndItemTemplateUseDelays()
	{
		var clock = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
		var timing = new BotTimingContract(clock);
		var cooldowns = new List<IReadOnlyDictionary<string, object?>>
		{
			new Dictionary<string, object?>
			{
				["skillId"] = (ushort)200,
				["remainingSeconds"] = 9,
				["durationMillis"] = 12000,
			},
		};
		timing.ApplySkillCooldowns(new DecodedBotServerPacket(typeof(SM_SKILL_COOLDOWN),
			new Dictionary<string, object?> { ["cooldowns"] = cooldowns }));
		Assert.Equal(TimeSpan.FromSeconds(9), timing.TimeUntilCast(200));

		var item = new ItemTemplate
		{
			itemId = 160000001,
			useLimits = new ItemUseLimits { useDelayId = 7, useDelay = 30_000 },
		};
		timing.RecordItemUse(item);
		Assert.Equal(TimeSpan.FromSeconds(30), timing.TimeUntilItemUse(item));
		Assert.Throws<InvalidOperationException>(() => timing.RecordItemUse(item));
		clock.Advance(TimeSpan.FromSeconds(30));
		timing.RecordItemUse(item);
	}

	[Fact]
	public void BlocksMovementDuringCastingGatheringAndCrafting()
	{
		var timing = new BotTimingContract();
		foreach (var activity in Enum.GetValues<BotBlockingActivity>())
		{
			timing.SetActivity(activity, true);
			Assert.Throws<InvalidOperationException>(timing.EnsureCanMove);
			timing.SetActivity(activity, false);
			timing.EnsureCanMove();
		}
	}

	[Fact]
	public void WaitsForConfiguredReentryAndCrashLeaveCompletion()
	{
		var clock = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
		var timing = new BotTimingContract(clock);

		timing.RecordLeftWorld(crashed: false);
		Assert.Equal(TimeSpan.FromSeconds(BotTimingContract.ConfiguredReentrySeconds), timing.TimeUntilEnterWorld());
		clock.Advance(TimeSpan.FromSeconds(10));
		timing.EnsureCanEnterWorld();

		timing.RecordLeftWorld(crashed: true);
		Assert.Equal(TimeSpan.FromSeconds(20), timing.TimeUntilEnterWorld());
		clock.Advance(TimeSpan.FromSeconds(19));
		Assert.Throws<InvalidOperationException>(timing.EnsureCanEnterWorld);
		clock.Advance(TimeSpan.FromSeconds(1));
		timing.EnsureCanEnterWorld();
		Assert.Equal(20, BotTimingContract.JavaDefaultReentrySeconds);
	}

	[Fact]
	public void ReentryHonorsRoundedPersistedLogoutWithoutShorteningCrashDelay()
	{
		var clock = new ManualTimeProvider(DateTimeOffset.UnixEpoch.AddMilliseconds(650));
		var timing = new BotTimingContract(clock);
		timing.RecordLeftWorld(crashed: false);
		var roundedLogout = DateTimeOffset.UnixEpoch.AddSeconds(1);
		Assert.Equal(TimeSpan.FromMilliseconds(10_350), timing.TimeUntilEnterWorld(roundedLogout));
		clock.Advance(TimeSpan.FromSeconds(10));
		Assert.Equal(TimeSpan.Zero, timing.TimeUntilEnterWorld());
		Assert.Equal(TimeSpan.FromMilliseconds(350), timing.TimeUntilEnterWorld(roundedLogout));
		clock.Advance(TimeSpan.FromMilliseconds(350));
		Assert.Equal(TimeSpan.Zero, timing.TimeUntilEnterWorld(roundedLogout));
		timing.RecordLeftWorld(crashed: true);
		Assert.Equal(TimeSpan.FromSeconds(20), timing.TimeUntilEnterWorld(clock.GetUtcNow()));
	}

	[Fact]
	public void ComputesClientHitAndLastHitTimesFromProductionMotionDataXml()
	{
		var motions = BotMotionTiming.Load(Path.Combine(RepoRoot(), "game-server", "data", "static_data", "skills", "motion_times.xml"));
		var skill = new SkillTemplate
		{
			motion = new Motion { Name = "20ebuff1", Speed = 100, Delay = 10 },
		};
		var profile = new BotMotionProfile(Race.ELYOS, Gender.FEMALE, BotWeaponMotionType.Dagger);

		Assert.Equal(877, motions.CalculateClientHitTime(skill, profile));
		var afterLastHit = Assert.IsType<BotAnimationTimes>(motions.CalculateAnimationTimesAfterLastHit(skill, profile));
		Assert.Equal(866, afterLastHit.LastHitMillis);
		Assert.Equal(1066, afterLastHit.FullDurationMillis);
	}

	private static string RepoRoot()
	{
		var directory = new DirectoryInfo(AppContext.BaseDirectory);
		while (directory != null)
		{
			if (File.Exists(Path.Combine(directory.FullName, "AionServer.slnx")))
				return directory.FullName;
			directory = directory.Parent;
		}
		throw new DirectoryNotFoundException("Could not find repository root.");
	}

	private sealed class ManualTimeProvider(DateTimeOffset now) : TimeProvider
	{
		private DateTimeOffset current = now;

		public override DateTimeOffset GetUtcNow() => current;

		public void Advance(TimeSpan amount) => current += amount;
	}
}
