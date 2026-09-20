using Aion.Bots.Scenarios;
using Aion.LiveBots;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class LivePortIsolationTests
{
    [Fact]
    public void LauncherPreservesAllFourComposeEnvironmentPortsInsteadOfOverridingGamePort()
    {
        string[] names = ["AION_BOT_LOGIN_PORT", "AION_BOT_GAME_PORT", "AION_BOT_CHAT_PORT", "AION_BOT_ADMIN_PORT"];
        int[] ports = [22106, 27777, 21241, 27780];
        var before = names.Select(Environment.GetEnvironmentVariable).ToArray();
        try
        {
            for (int i = 0; i < names.Length; i++) Environment.SetEnvironmentVariable(names[i], ports[i].ToString());
            var options = LiveBotOptions.Parse(["--run", "ports", "--output", "run/ports", "--scenario", "connect", "--git-sha", "test"]);
            Assert.Equal(ports, new[] { options.LoginEndPoint.Port, options.GameEndPoint.Port, options.ChatEndPoint.Port, options.AdminBaseUri.Port });
            string root = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ScenarioManifest.FindDefaultPath())!, "../.."));
            var launcher = File.ReadAllText(Path.Combine(root, "scripts/live/run-live.ps1"));
            foreach (string flag in new[] { "--login-port", "--game-port", "--chat-port", "--admin-port" })
                Assert.DoesNotContain(flag, launcher); // Options resolve exactly the environment used by Compose/readiness.
        }
        finally { for (int i = 0; i < names.Length; i++) Environment.SetEnvironmentVariable(names[i], before[i]); }
    }
}
