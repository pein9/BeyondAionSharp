using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aion.Bots.Scenarios;

/// <summary>Small Phase 2 search space. Null options keep the existing deterministic baseline.</summary>
public sealed record NaturalMauPolicyParameters(
	float PullDistanceMeters = 22f,
	int PatrolWaitCycles = 3,
	int HealSinglePercent = 55,
	int HealMultiplePercent = 70,
	int HotPotionPercent = 90,
	int ManaReserveExtra = 0,
	int FinishTargetHpPercent = 15,
	bool PreferWoundedWhenTwoAttackers = false)
{
	public const string Version = "mau-parameter-v1";
	public static NaturalMauPolicyParameters Baseline { get; } = new();
	[JsonIgnore]
	public string Id => $"{Version}:{Convert.ToHexString(SHA256.HashData(
		Encoding.UTF8.GetBytes(JsonSerializer.Serialize(this)))).ToLowerInvariant()[..16]}";

	public void Validate()
	{
		if (PullDistanceMeters is < 18f or > 22f ||
			PatrolWaitCycles is < 0 or > 4 ||
			HealSinglePercent is < 45 or > 70 ||
			HealMultiplePercent is < 60 or > 85 || HealMultiplePercent < HealSinglePercent ||
			HotPotionPercent is < 75 or > 95 ||
			ManaReserveExtra is < 0 or > 30 ||
			FinishTargetHpPercent is < 0 or > 25)
			throw new ArgumentOutOfRangeException(nameof(NaturalMauPolicyParameters),
				"Mau policy is outside the frozen Phase 2 bounds.");
	}
}
