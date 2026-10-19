namespace Aion.Bots.Scenarios.Classes;

/// <summary>Observed chain state under Java b55aaf32c: current category, consecutive uses and the advertised hit time.</summary>
/// <param name="Previous">Legacy observation field; the server no longer keeps a previous category.</param>
public sealed record NaturalChainState(string? Current, string? Previous, int? Target, DateTimeOffset? StepAt, int UseCount)
{
    public static NaturalChainState None { get; } = new(null, null, null, null, 0);

    // A request or a rejected cast does not change the server's chain.
    public NaturalChainState CastSent(NaturalPriestSkill skill) => this;

    public NaturalChainState CastStarted(NaturalPriestSkill skill, DateTimeOffset now)
    {
        bool repeat = skill.SelfCount > 1 && Current == skill.ChainCategory && UseCount < skill.SelfCount &&
            StepAt is DateTimeOffset hit && now < hit.AddMilliseconds(ActivationWindow(skill));
        if (repeat) return this;
        return skill.RequiresChainCategory != null || skill.SelfCount > 1 && Current == skill.ChainCategory ? None : this;
    }

    public static int ActivationWindow(NaturalPriestSkill skill) =>
        skill.ChainWindowMillis == 0 ? 3000 : unchecked(skill.ChainWindowMillis + 1500);

    /// <summary>Flag 32 counts a successful cast. A failed result leaves the state reached at cast start unchanged.</summary>
    /// <param name="now">The hit time advertised by the server, including its delay after the result.</param>
    public NaturalChainState CastCompleted(NaturalPriestSkill skill, int target, bool onSelf, bool chained, DateTimeOffset now)
    {
        if (!chained) return this;
        if (skill.ChainCategory == null) return None;
        int? chainTarget = onSelf ? Target : target;
        return Current == skill.ChainCategory
            ? this with { Previous = null, Target = chainTarget, StepAt = now, UseCount = UseCount + 1 }
            : new(skill.ChainCategory, null, chainTarget, now, 1);
    }
}
