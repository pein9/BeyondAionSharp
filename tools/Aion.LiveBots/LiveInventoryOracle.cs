using System.Text.Json;
using Aion.Bots.Protocol;
using Aion.Bots.World;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.LiveBots;

internal sealed partial class LiveBotSession
{
    public async Task VerifyInventoryAsync(CancellationToken token)
    {
        // Drain preceding action replies before taking either view. Do not request an inventory refresh:
        // that would overwrite the packet-derived state whose correctness this check is meant to test.
        await SendPacketAsync(GameClientPackets.TimeCheck(unchecked((int)Environment.TickCount64)), token);
        await WaitForPacketAsync(typeof(SM_TIME_CHECK), token);
        var items = Api.World.Inventory.Values.ToArray();
        long kinah = Api.World.Kinah;
        using var client = new HttpClient { BaseAddress = options.AdminBaseUri };
        using var request = new HttpRequestMessage(HttpMethod.Get, $"admin/player-storage-state?recipientCharacterId={characterId}");
        request.Headers.Add("X-Admin-Token", options.AdminToken);
        using var response = await client.SendAsync(request, token);
        response.EnsureSuccessStatusCode();
        await using var content = await response.Content.ReadAsStreamAsync(token);
        using var document = await JsonDocument.ParseAsync(content, cancellationToken: token);
        // Save both views even when verification fails, without saving credentials or request headers.
        string evidence = Path.Combine(options.OutputDirectory, "bots", $"{bot}.{currentStep}.inventory-oracle.json");
        await File.WriteAllTextAsync(evidence, JsonSerializer.Serialize(new
        {
            characterId, clientKinah = kinah, clientItems = items, server = document.RootElement
        }, new JsonSerializerOptions { WriteIndented = true }), token);
        BotInventoryOracle.Verify(characterId, items, kinah, document.RootElement);
        trace.WriteAction(currentStep, "inventory:verified", new Dictionary<string, object?>
        {
            ["characterId"] = characterId, ["itemCount"] = items.Length, ["kinah"] = kinah,
            ["evidence"] = Path.GetFileName(evidence)
        });
    }
}
