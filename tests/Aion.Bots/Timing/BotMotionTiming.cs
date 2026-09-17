using System.Xml.Serialization;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.Bots.Timing;

/// <summary>Client animation timings read from the same <see cref="MotionData"/> XML model as the server.</summary>
public sealed class BotMotionTiming
{
	private readonly IReadOnlyDictionary<string, MotionTime> motions;

	private BotMotionTiming(MotionData rawData)
	{
		if (rawData.motionTimes == null)
			throw new ArgumentException("MotionData must be captured before AfterUnmarshal clears its XML rows.", nameof(rawData));
		motions = rawData.motionTimes.ToDictionary(motion => motion.GetName(), StringComparer.Ordinal);
	}

	public static BotMotionTiming Load(string path)
	{
		using var stream = File.OpenRead(path);
		var serializer = new XmlSerializer(typeof(MotionData));
		var data = serializer.Deserialize(stream) as MotionData
			?? throw new InvalidDataException($"Could not deserialize motion timing data from '{path}'.");
		return new BotMotionTiming(data);
	}

	public static BotMotionTiming FromRawMotionData(MotionData rawData) => new(rawData);

	public int CalculateClientHitTime(SkillTemplate skill, BotMotionProfile profile, int ammoTravelMillis = 0)
	{
		var motion = skill.GetMotion();
		if (motion?.GetName() == null || !motions.TryGetValue(motion.GetName()!, out var timing))
			return 0;
		var times = FindTimes(timing, profile, 1);
		if (times == null)
			return 0;

		var rate = profile.HitTimeBoosted
			? Math.Min(profile.AttackSpeedRate, CalculateCastSpeedRate(profile.HitTimeBoostCastSpeed))
			: profile.AttackSpeedRate;
		var firstHit = (profile.Robot ? times.GetAnimationLength() : times.GetMinTime()) * motion.GetSpeed() * 10 * rate;
		return motion.GetDelay() + JavaRound(firstHit) + Math.Max(0, ammoTravelMillis);
	}

	public BotAnimationTimes? CalculateAnimationTimesAfterLastHit(SkillTemplate skill, BotMotionProfile profile,
		int motionId = 1, bool allowAnimationBoostByCastSpeed = false, float castSpeedForAnimationBoost = 1)
	{
		var motion = skill.GetMotion();
		if (motion?.GetName() == null || !motions.TryGetValue(motion.GetName()!, out var timing))
			return null;
		var times = FindTimes(timing, profile, Math.Max(1, motionId));
		if (times == null)
			return null;

		var rate = allowAnimationBoostByCastSpeed
			? Math.Min(profile.AttackSpeedRate, CalculateCastSpeedRate(castSpeedForAnimationBoost))
			: profile.AttackSpeedRate;
		var motionSpeed = motion.GetSpeed() * 10;
		return new BotAnimationTimes(
			(int)(times.GetMaxTime() * motionSpeed * rate),
			(int)(times.GetAnimationLength() * motionSpeed * rate));
	}

	private static Times? FindTimes(MotionTime timing, BotMotionProfile profile, int motionId)
	{
		var rows = profile.Robot ? timing.robot : profile.Race switch
		{
			Race.ASMODIANS when profile.Gender == Gender.FEMALE => timing.asmodianFemale,
			Race.ASMODIANS => timing.asmodianMale,
			Race.ELYOS when profile.Gender == Gender.FEMALE => timing.elyosFemale,
			Race.ELYOS => timing.elyosMale,
			_ => null,
		};
		if (rows == null)
			return null;

		var weapon = profile.Robot ? null : profile.Weapon.ToXmlName();
		for (var id = motionId; id > 0; id--)
		{
			var match = rows.FirstOrDefault(row => row.GetId() == id && (profile.Robot || row.GetWeapon() == weapon));
			if (match != null)
				return match;
		}
		return null;
	}

	private static float CalculateCastSpeedRate(float castSpeed)
	{
		castSpeed = Math.Clamp(castSpeed, 0.5f, 1f);
		return castSpeed + (1 - castSpeed) / 2;
	}

	private static int JavaRound(float value) => (int)MathF.Floor(value + 0.5f);
}

public sealed record BotMotionProfile(Race Race, Gender Gender, BotWeaponMotionType Weapon,
	float AttackSpeedRate = 1, bool Robot = false, bool HitTimeBoosted = false, float HitTimeBoostCastSpeed = 1);

public sealed record BotAnimationTimes(int LastHitMillis, int FullDurationMillis);

public enum BotWeaponMotionType
{
	NoWeapon,
	OneHand,
	TwoHand,
	Keyblade,
	Polearm,
	Dagger,
	Mace,
	Staff,
	TwoWeapon,
	Book,
	Orb,
	OneGun,
	TwoGun,
	Cannon,
	Bow,
	Harp,
}

internal static class BotWeaponMotionTypeExtensions
{
	public static string ToXmlName(this BotWeaponMotionType weapon) => weapon switch
	{
		BotWeaponMotionType.NoWeapon => "noweapon",
		BotWeaponMotionType.OneHand => "1hand",
		BotWeaponMotionType.TwoHand => "2hand",
		BotWeaponMotionType.Keyblade => "keyblade",
		BotWeaponMotionType.Polearm => "polearm",
		BotWeaponMotionType.Dagger => "dagger",
		BotWeaponMotionType.Mace => "mace",
		BotWeaponMotionType.Staff => "staff",
		BotWeaponMotionType.TwoWeapon => "2weapon",
		BotWeaponMotionType.Book => "book",
		BotWeaponMotionType.Orb => "orb",
		BotWeaponMotionType.OneGun => "1gun",
		BotWeaponMotionType.TwoGun => "2gun",
		BotWeaponMotionType.Cannon => "cannon",
		BotWeaponMotionType.Bow => "bow",
		BotWeaponMotionType.Harp => "harp",
		_ => throw new ArgumentOutOfRangeException(nameof(weapon), weapon, null),
	};
}
