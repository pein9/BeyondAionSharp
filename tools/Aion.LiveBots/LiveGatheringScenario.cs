using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Aion.Bots.Api;
using Aion.Bots.Gm;
using Aion.Bots.Movement;
using Aion.Bots.Protocol;
using Aion.Bots.Scenarios;
using Aion.Bots.Timing;
using Aion.Bots.World;
using Aion.GameServer.Model;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.LiveBots;

public static partial class LiveBotRunner
{
	private static async Task<int> RunGatheringAsync(LiveBotOptions options, LiveBotProblemWriter problems, bool negative,
		CancellationToken cancellationToken)
	{
		string scenarioId = negative ? "E2" : "E1";
		await using var elyos = new L0Actor(options, problems, 1, Race.ELYOS, characterName: "Aelivegather");
		await using var asmodian = new L0Actor(options, problems, 2, negative ? Race.ELYOS : Race.ASMODIANS,
			characterName: "Aslivegather");
		await using var director = new L0Actor(options, problems, 99, Race.ELYOS,
			bot: "gm", account: LiveGmFacade.DirectorAccount, characterName: "Director");
		L0Actor[] actors = [elyos, asmodian, director];
		foreach (L0Actor actor in actors)
			actor.Trace.WriteAction("s00", "scenario:start", new Dictionary<string, object?> { ["scenario"] = scenarioId });
		try
		{
			foreach (L0Actor actor in actors)
			{
				await actor.StepAsync("login-game-auth", actor.Session.LoginAndAuthenticateAsync, cancellationToken);
				await actor.StepAsync("create-character", actor.Session.CreateCharacterAsync, cancellationToken);
				await actor.StepAsync("enter-world", actor.Session.EnterWorldAsync, cancellationToken);
			}
			LiveGmFacade gm = director.Session.CreateLiveGmFacade();
			// Serialize director setup, then let each ordinary subject observe its own node's real-time respawn.
			using var setupGate = new SemaphoreSlim(1, 1);
			var first = new LiveGatheringDriver(elyos, director, gm, setupGate);
			var second = new LiveGatheringDriver(asmodian, director, gm, setupGate);
			if (negative)
				await GatheringNegativeScenario.RunAsync(first, second, cancellationToken);
			else
				await Task.WhenAll(
					GatheringScenario.RunAsync(first, GatheringTarget.YoungAria, cancellationToken),
					GatheringScenario.RunAsync(second, GatheringTarget.YoungAzpha, cancellationToken));
			foreach (L0Actor actor in actors)
			{
				if (actor != director)
					await actor.StepAsync("verify-inventory-oracle", actor.Session.VerifyInventoryAsync, cancellationToken);
				await actor.StepAsync("quit", actor.Session.QuitAsync, cancellationToken);
				actor.Trace.WriteAction(actor.LastStep, "scenario:complete", new Dictionary<string, object?> { ["scenario"] = scenarioId });
			}
			Console.WriteLine($"LIVE {scenarioId} gathering scenario passed.");
			return 0;
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			throw;
		}
		catch (Exception exception)
		{
			Console.Error.WriteLine($"{scenarioId} failed: {exception}");
			return 1;
		}
	}

	private sealed class LiveGatheringDriver(L0Actor subject, L0Actor director, LiveGmFacade gm,
		SemaphoreSlim setupGate) : IGatheringNegativeDriver
	{
		private long depletedAt;
		public BotApi Api => subject.Session.Api;
		public Task StepAsync(string action, Func<CancellationToken, Task> operation, CancellationToken token) =>
			subject.StepAsync(action, operation, token);

		public async Task<int> PrepareAsync(GatheringTarget target, CancellationToken token)
		{
			await setupGate.WaitAsync(token);
			try
			{
				await MoveSubjectWithDirectorAsync(director, subject, gm, target.MapId,
					target.Position.X - 5, target.Position.Y, target.Position.Z, "setup-gather-position", token);
			}
			finally
			{
				setupGate.Release();
			}
			int id = await subject.Session.WaitForNearestObjectExceptAsync(BotKnownObjectKind.Gatherable,
				target.TemplateId, new HashSet<int>(), token);
			BotPosition position = Api.World.Objects[id].Position;
			if (Math.Abs(position.X - target.Position.X) > 0.01f || Math.Abs(position.Y - target.Position.Y) > 0.01f)
				throw new InvalidDataException("E1 did not find its declared gatherable spawn.");
			await subject.Session.MoveToKnownObjectAsync(id, token);
			return id;
		}

		public Task SendAsync(BotClientPacket packet, CancellationToken token) => subject.Session.SendPacketAsync(packet, token);
		public Task MoveAsync(GatheringTarget target, float distance, bool interrupt, CancellationToken token)
		{
			BotPosition start = subject.Session.CurrentPosition;
			BotPosition end = target.Position with { X = target.Position.X - distance };
			float speed = Api.World.MovementSpeed ?? throw new InvalidDataException("Missing movement speed.");
			// The interruption probe deliberately sends ordinary movement while the gathering activity is active.
			var mover = new BotMover(Api.World, interrupt ? null : Api.Timing);
			return subject.Session.ExecuteMovementAsync(mover.CreateGroundPlan([end], start, speed), token);
		}
		public async Task FillCubeAsync(CancellationToken token)
		{
			int free = await subject.Session.ReadCubeFreeSlotsAsync(token);
			if (free is < 1 or > 150)
				throw new InvalidDataException($"Unexpected free cube slots: {free}.");
			await director.StepAsync("fill-subject-cube", cancellation => gm.ExecuteAsync(
				new GmCommand("add", [subject.Session.CharacterName, "100000001", free.ToString(CultureInfo.InvariantCulture)], "You gave"),
				cancellationToken: cancellation), token);
			await SynchronizeAsync(token);
			if (await subject.Session.ReadCubeFreeSlotsAsync(token) != 0)
				throw new InvalidDataException("Director setup did not fill the cube.");
		}
		public Task VerifyOccupiedAsync(int gathererId, CancellationToken token)
		{
			bool isGathering = Api.Timing.BlockingActivities.Contains(BotBlockingActivity.Gathering);
			if (isGathering != (subject.Session.CharacterId == gathererId))
				throw new InvalidDataException("Gatherable's accepted/rejected interaction belongs to the wrong bot.");
			return Task.CompletedTask;
		}
		public async Task<DecodedBotServerPacket> WaitAsync(Type type, Func<DecodedBotServerPacket, bool> predicate,
			CancellationToken token)
		{
			DecodedBotServerPacket packet = await subject.Session.WaitForPacketAsync(type, token, predicate);
			if (type == typeof(SM_DELETE))
				depletedAt = Stopwatch.GetTimestamp();
			return packet;
		}
		public Task AwaitHarvestAsync(CancellationToken token) => Task.CompletedTask;
		public async Task SynchronizeAsync(CancellationToken token)
		{
			await subject.Session.SendPacketAsync(GameClientPackets.TimeCheck(unchecked((int)Environment.TickCount64)), token);
			await subject.Session.WaitForPacketAsync(typeof(SM_TIME_CHECK), token);
		}
		public Task VerifyReleasedAsync(int objectId, bool depleted, CancellationToken token)
		{
			if (Api.Timing.BlockingActivities.Contains(BotBlockingActivity.Gathering))
				throw new InvalidDataException("Gathering completion left the bot's interaction active.");
			return Task.CompletedTask;
		}

		public async Task VerifyRespawnAsync(GatheringTarget target, int oldObjectId, CancellationToken token)
		{
			DecodedBotServerPacket packet = await subject.Session.WaitForPacketAsync(typeof(SM_GATHERABLE_INFO), token,
				candidate => candidate.Get<int>("templateId") == target.TemplateId &&
					Math.Abs(candidate.Get<float>("x") - target.Position.X) < 0.01f &&
					Math.Abs(candidate.Get<float>("y") - target.Position.Y) < 0.01f);
			TimeSpan elapsed = Stopwatch.GetElapsedTime(depletedAt);
			// SIM pins the exact millisecond. LIVE allows packet delivery/scheduling jitter, never a shortened respawn.
			if (elapsed < GatheringTarget.RespawnDelay - TimeSpan.FromSeconds(2) ||
				elapsed > GatheringTarget.RespawnDelay + TimeSpan.FromSeconds(10))
				throw new InvalidDataException($"Gatherable respawn arrived after {elapsed.TotalSeconds:F3}s, expected 295s.");
			subject.Trace.WriteAction(subject.LastStep, "gatherable:respawn", new Dictionary<string, object?>
			{
				["templateId"] = target.TemplateId, ["oldObjectId"] = oldObjectId,
				["objectId"] = packet.Get<int>("objectId"), ["elapsedSeconds"] = elapsed.TotalSeconds,
			});
		}
	}
}

internal sealed partial class LiveBotSession
{
	public async Task<int> ReadCubeFreeSlotsAsync(CancellationToken token)
	{
		using var request = new HttpRequestMessage(HttpMethod.Get,
			$"admin/player-storage-state?recipientCharacterId={characterId}");
		request.Headers.Add("X-Admin-Token", options.AdminToken);
		using HttpResponseMessage response = await AdminClient.SendAsync(request, token);
		response.EnsureSuccessStatusCode();
		await using Stream content = await response.Content.ReadAsStreamAsync(token);
		using JsonDocument document = await JsonDocument.ParseAsync(content, cancellationToken: token);
		return document.RootElement.GetProperty("inventory").GetProperty("cubeFreeSlots").GetInt32();
	}
}
