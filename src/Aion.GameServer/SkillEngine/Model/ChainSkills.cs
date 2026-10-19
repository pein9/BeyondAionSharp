using Aion.GameServer.Utils;

namespace Aion.GameServer.SkillEngine.Model;

/// <summary>The last counted chain category, consecutive uses and hit time. Each follow-up checks its own window. Java parity: skillengine/model/ChainSkills.</summary>
public class ChainSkills
{
    private string _category = "";
    private int _useCount;
    private long _hitTimeMillis;

    public int GetCurrentChainCount(string? category) => _category.Equals(category, StringComparison.Ordinal) ? _useCount : 0;

    public bool IsCurrentCategory(string? category) => _category.Equals(category, StringComparison.Ordinal);

    public bool CanRepeat(string? category, int allowedActivations, int windowMillis) =>
        IsCurrentCategory(category) && _useCount < allowedActivations && IsWithinWindow(windowMillis);

    public bool CanContinue(string? preCategory, int preCount, int windowMillis) =>
        IsCurrentCategory(preCategory) && _useCount >= preCount && IsWithinWindow(windowMillis);

    private bool IsWithinWindow(int windowMillis) =>
        SystemClock.CurrentMillis() < unchecked(_hitTimeMillis + windowMillis);

    /// <summary>Counts a successful cast; an empty category ends the current chain. Hit time is relative to now.</summary>
    public void UpdateChain(string category, int hitTime)
    {
        if (IsCurrentCategory(category))
            _useCount = unchecked(_useCount + 1);
        else
        {
            _category = category;
            _useCount = 1;
        }
        _hitTimeMillis = unchecked(SystemClock.CurrentMillis() + hitTime);
    }

    public void ResetChain()
    {
        _category = "";
        _useCount = 0;
        _hitTimeMillis = 0;
    }
}
