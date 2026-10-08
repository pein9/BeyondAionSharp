namespace Aion.Bots.Scenarios.Classes;

/// <summary>
/// CP-39: the chain a table-driven class has open, kept the way the server keeps it (Java ChainSkills: the current and
/// the previous chain category and how often the current one was cast), with when the current step was cast, so a
/// follow-up can count its own chain time from it (CP-Q17). The fight loop moves it at three moments of a cast; the
/// table policy reads it from the observation. The Priest line keeps its own bookkeeping.
/// </summary>
/// <param name="Current">The chain category cast last; null for no open chain.</param>
/// <param name="Previous">The chain category before it.</param>
/// <param name="Target">The target the chain was opened on. A step cast on the bot itself leaves it.</param>
/// <param name="StepAt">When the current step was cast.</param>
/// <param name="UseCount">How often the current step was cast in a row.</param>
public sealed record NaturalChainState(string? Current, string? Previous, int? Target, DateTimeOffset? StepAt, int UseCount)
{
	public static NaturalChainState None { get; } = new(null, null, null, null, 0);

	/// <summary>A cast is sent. One without a chain category resets the chain (Java Skill.canUseSkill).</summary>
	public NaturalChainState CastSent(NaturalPriestSkill skill) => skill.ChainCategory == null ? None : this;

	/// <summary>
	/// The server started the cast. A chain's first step resets the open chain when it is another chain's, or its own
	/// used up to the skill's self count (Java ChainCondition.shouldReset); only the completed cast opens it again.
	/// </summary>
	public NaturalChainState CastStarted(NaturalPriestSkill skill)
	{
		if (skill.ChainCategory == null || skill.RequiresChainCategory != null || Current == null) return this;
		return Current != skill.ChainCategory || UseCount >= Math.Max(1, skill.SelfCount) ? None : this;
	}

	/// <summary>
	/// The cast completed. A chain skill whose result carries the chain flag becomes the current step: the same category
	/// again counts one more use, another category moves the old one to <see cref="Previous"/> (Java
	/// ChainSkills.updateChain). Without the flag the chain is gone.
	/// </summary>
	/// <param name="chained">Flag 32 of SM_CASTSPELL_RESULT.</param>
	/// <param name="onSelf">The skill was cast on the bot.</param>
	public NaturalChainState CastCompleted(NaturalPriestSkill skill, int target, bool onSelf, bool chained, DateTimeOffset now)
	{
		if (skill.ChainCategory == null) return this;
		if (!chained) return None;
		int? chainTarget = onSelf ? Target : target;
		return Current == skill.ChainCategory
			? this with { Target = chainTarget, StepAt = now, UseCount = UseCount + 1 }
			: new(skill.ChainCategory, Current, chainTarget, now, 1);
	}
}
