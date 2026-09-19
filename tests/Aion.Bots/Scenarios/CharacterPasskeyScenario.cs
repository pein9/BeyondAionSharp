using System.Text;
using Aion.Bots.Protocol;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.Bots.Scenarios;

public interface ICharacterPasskeyDriver
{
	Task StepAsync(string action, Func<CancellationToken, Task> operation, CancellationToken token);
	Task PrepareAsync(CancellationToken token);
	Task<DecodedBotServerPacket> RequestEntryAsync(CancellationToken token);
	Task<DecodedBotServerPacket> ExchangeAsync(BotClientPacket packet, CancellationToken token);
	Task FinishEntryAsync(CancellationToken token);
	Task ReloginAsync(CancellationToken token);
	Task VerifyNoBanAsync(CancellationToken token);
	Task LockoutAsync(byte[] wrongValue, CancellationToken token);
	Task ResetAsync(string digits, CancellationToken token);
	Task LoginAfterResetAsync(CancellationToken token);
}

public static class CharacterPasskeyScenario
{
	public const int WrongAttemptLimit = 5;
	public const string ResetDigits = "736291";
	public static byte[] WireValue(string digits)
	{
		if (digits.Length is < 6 or > 8 || !digits.All(char.IsAsciiDigit))
			throw new ArgumentException("Use six to eight ASCII digits.", nameof(digits));
		return Encoding.Unicode.GetBytes(digits.PadRight(24, '\0'));
	}

	public static async Task RunAsync(ICharacterPasskeyDriver driver, CancellationToken token)
	{
		byte[] original = WireValue("123456"), replacement = WireValue("87654321"), wrong = WireValue("111111");
		await driver.StepAsync("prepare-passkey-enabled-ordinary-account", driver.PrepareAsync, token);
		await driver.StepAsync("set-passkey-through-new-window", async ct =>
		{
			VerifyWindow(await driver.RequestEntryAsync(ct), 0);
			VerifyResult(await driver.ExchangeAsync(GameClientPackets.SetCharacterPasskey(original), ct), 0, 0);
			VerifyWindow(await driver.RequestEntryAsync(ct), 1);
			VerifyResult(await driver.ExchangeAsync(GameClientPackets.SubmitCharacterPasskey(original), ct), 3, 0);
			await driver.FinishEntryAsync(ct);
		}, token);
		await driver.ReloginAsync(token);
		await driver.StepAsync("update-passkey-and-reject-old-value", async ct =>
		{
			VerifyWindow(await driver.RequestEntryAsync(ct), 1);
			VerifyResult(await driver.ExchangeAsync(GameClientPackets.UpdateCharacterPasskey(wrong, replacement), ct), 2, 1);
			VerifyResult(await driver.ExchangeAsync(GameClientPackets.UpdateCharacterPasskey(original, replacement), ct), 2, 0);
			VerifyResult(await driver.ExchangeAsync(GameClientPackets.SubmitCharacterPasskey(original), ct), 3, 1);
			VerifyResult(await driver.ExchangeAsync(GameClientPackets.SubmitCharacterPasskey(replacement), ct), 3, 0);
			await driver.FinishEntryAsync(ct);
		}, token);
		await driver.ReloginAsync(token);
		await driver.StepAsync("wrong-attempts-below-threshold-do-not-ban", async ct =>
		{
			VerifyWindow(await driver.RequestEntryAsync(ct), 1);
			for (int count = 1; count < WrongAttemptLimit; count++)
			{
				VerifyResult(await driver.ExchangeAsync(GameClientPackets.SubmitCharacterPasskey(wrong), ct), 3, count);
				await driver.VerifyNoBanAsync(ct);
			}
		}, token);
		await driver.StepAsync("fifth-wrong-attempt-lockout", ct => driver.LockoutAsync(wrong, ct), token);
		await driver.StepAsync("director-resets-passkey-and-unbans", ct => driver.ResetAsync(ResetDigits, ct), token);
		await driver.StepAsync("fresh-login-with-reset-passkey", async ct =>
		{
			await driver.LoginAfterResetAsync(ct);
			VerifyWindow(await driver.RequestEntryAsync(ct), 1);
			VerifyResult(await driver.ExchangeAsync(GameClientPackets.SubmitCharacterPasskey(replacement), ct), 3, 1);
			VerifyResult(await driver.ExchangeAsync(GameClientPackets.SubmitCharacterPasskey(WireValue(ResetDigits)), ct), 3, 0);
			await driver.FinishEntryAsync(ct);
		}, token);
	}

	public static void VerifyWindow(DecodedBotServerPacket packet, byte type)
	{
		if (packet.PacketType != typeof(SM_CHARACTER_SELECT) || packet.Get<byte>("type") != type)
			throw new InvalidDataException($"Expected passkey window {type}.");
	}
	public static void VerifyResult(DecodedBotServerPacket packet, short operation, int wrongCount)
	{
		VerifyWindow(packet, 2);
		if (packet.Get<short>("messageType") != operation || packet.Get<int>("wrongCount") != wrongCount
			|| packet.Get<int>("maxWrongCount") != WrongAttemptLimit || packet.Get<bool>("accepted") != (wrongCount == 0))
			throw new InvalidDataException($"Passkey operation {operation} expected wrong-count {wrongCount}, got {packet.Get<int>("wrongCount")}.");
	}
}
