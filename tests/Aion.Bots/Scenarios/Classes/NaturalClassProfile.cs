using Aion.GameServer.Model;

namespace Aion.Bots.Scenarios.Classes;

/// <summary>
/// How one class plays (docs/natural-class-profiles.md, the seam, section 2). A profile holds pure decisions and data;
/// the shared executor in the journey sends every packet and never names a class.
/// </summary>
public sealed class NaturalClassProfile
{
	public required PlayerClass Class { get; init; }

	/// <summary>The class's skill catalog. The learned skill list is still the authority: a catalog never grants a skill.</summary>
	public required NaturalPriestSkill[] Skills { get; init; }

	/// <summary>Auto-learned active skills the class does not cast, each with its reason.</summary>
	public required IReadOnlyDictionary<int, string> Excluded { get; init; }

	public required INaturalCombatPolicy Combat { get; init; }
}

public static class NaturalClassProfiles
{
	private static readonly Dictionary<PlayerClass, NaturalClassProfile> Profiles = new()
	{
		[PlayerClass.PRIEST] = NaturalPriestProfile.Priest,
		[PlayerClass.CLERIC] = NaturalPriestProfile.Cleric,
	};

	/// <summary>
	/// The profile of the class the client observes now. It is looked up at every decision and never kept, because a
	/// starter becomes its second class inside one run. An unobserved class is the line's starter; a class outside the
	/// line, or one without a profile, is refused.
	/// </summary>
	public static NaturalClassProfile For(byte? observedClassId, NaturalClassLine line)
	{
		ArgumentNullException.ThrowIfNull(line);
		PlayerClass playerClass = observedClassId is byte classId
			? PlayerClassExtensions.GetPlayerClassById(classId, true)
				?? throw new InvalidDataException($"The client observed class id {classId}, which is no player class.")
			: line.Starter;
		if (!line.Holds(playerClass))
			throw new InvalidDataException($"The client observed {playerClass}, which is outside the class line {line.Id}.");
		return Profiles.TryGetValue(playerClass, out NaturalClassProfile? profile) ? profile
			: throw new InvalidDataException($"{playerClass} has no natural class profile.");
	}
}
