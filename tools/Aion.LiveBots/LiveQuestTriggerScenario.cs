using Aion.Bots.Gm;
using Aion.Bots.Protocol;
using Aion.Bots.Scenarios;
using Aion.Bots.World;
using Aion.GameServer.Model;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.LiveBots;

public static partial class LiveBotRunner
{
	private static async Task<int> RunQ3Async(LiveBotOptions options, LiveBotProblemWriter problems,
		CancellationToken cancellationToken)
	{
		await using var subject = new L0Actor(options, problems, 1, Race.ELYOS, characterName: "Aeliveat");
		await using var director = new L0Actor(options, problems, 99, Race.ELYOS,
			bot: "gm", account: LiveGmFacade.DirectorAccount, characterName: "Director");
		subject.Trace.WriteAction("s00", "scenario:start", new Dictionary<string, object?> { ["scenario"] = "Q3" });
		director.Trace.WriteAction("s00", "scenario:start", new Dictionary<string, object?> { ["scenario"] = "Q3-director" });
		try
		{
			DecodedBotServerPacket? prologueMovie = null;
			foreach (L0Actor actor in new[] { subject, director })
			{
				await actor.StepAsync("login-game-auth", actor.Session.LoginAndAuthenticateAsync, cancellationToken);
				await actor.StepAsync("create-character", actor.Session.CreateCharacterAsync, cancellationToken);
				await actor.StepAsync("enter-world", async token =>
				{
					await actor.Session.EnterWorldAsync(token);
					DecodedBotServerPacket movie = await actor.Session.WaitForPacketAsync(typeof(SM_PLAY_MOVIE), token);
					if (ReferenceEquals(actor, subject))
						prologueMovie = movie;
				}, cancellationToken);
			}

			LiveGmFacade gm = director.Session.CreateLiveGmFacade();
			var gmSubject = new GmSubject(subject.Session.CharacterId, subject.Session.CharacterName);
			await director.StepAsync("move-director-to-subject", async token =>
			{
				await gm.ExecuteAsync(new GmCommand("moveto", [subject.Session.CharacterName], "Teleported to"),
					cancellationToken: token);
				await director.Session.CompleteTeleportAsync(210010000, token);
			}, cancellationToken);

			await subject.StepAsync("duplicate-movie-end-is-idempotent", async token =>
			{
				DecodedBotServerPacket movie = prologueMovie
					?? throw new InvalidDataException("Q3 did not observe the Elyos prologue movie.");
				await subject.Session.SendPacketAsync(GameClientPackets.PlayMovieEnd(
					movie.Get<bool>("isMovie") ? (byte)1 : (byte)0,
					movie.Get<int>("objectId"), movie.Get<int>("questId"), movie.Get<int>("cutsceneId"),
					movie.Get<bool>("canSkip")), token);
				await subject.Session.SendPacketAsync(subject.Session.Api.Say("q3 movie probe complete"), token);
				await subject.Session.WaitForPacketAsync(typeof(SM_MESSAGE), token);
				if (!subject.Session.Api.World.Quests.TryGetValue(1000, out BotQuestState? prologue) || prologue.Status != 5)
					throw new InvalidDataException("Duplicate CM_PLAY_MOVIE_END changed the completed prologue.");
			}, cancellationToken);

			await director.StepAsync("level-subject-to-three", token =>
				gm.ExecuteAsync(new GmCommand("set", ["level", "3"], "level to 3"), gmSubject, token), cancellationToken);
			await subject.StepAsync("level-up-starts-1100", token =>
				subject.Session.WaitForQuestStatusAsync(1100, 3, token), cancellationToken);

			await MoveSubjectWithDirectorAsync(director, subject, gm, 210010000,
				819.9f, 1241.05f, 118.682f, "setup-kalio-negatives", cancellationToken);
			int kalio = await subject.Session.WaitForNpcAsync(203067, cancellationToken);
			await subject.StepAsync("reject-premature-reward-and-cannot-give-up", async token =>
			{
				await subject.Session.SendPacketAsync(subject.Session.Api.TalkTo(kalio), token);
				await subject.Session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token);
				await subject.Session.SendPacketAsync(subject.Session.Api.SelectDialogExpectRejection(
					kalio, DialogAction.SELECTED_QUEST_REWARD1, questId: 1100), token);
				await subject.Session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token,
					packet => packet.Get<int>("targetObjectId") == kalio &&
						packet.Get<ushort>("dialogPageId") == DialogAction.SELECTED_QUEST_REWARD1);
				if (subject.Session.Api.QuestDialogEchoes.ConsumeExpectedRejection().QuestId != 1100)
					throw new InvalidDataException("Premature reward rejection was not attributed to Q1100.");
				await subject.Session.SendPacketAsync(subject.Session.Api.DeleteQuest(1100), token);
				await subject.Session.SendPacketAsync(subject.Session.Api.TalkTo(kalio), token);
				await subject.Session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token);
				if (!subject.Session.Api.World.Quests.TryGetValue(1100, out BotQuestState? state) || state.Status != 3)
					throw new InvalidDataException("The cannot_giveup mission Q1100 was abandoned.");
			}, cancellationToken);

			await subject.StepAsync("replay-sim-learned-1100", async token =>
			{
				string knowledgeRoot = Path.GetDirectoryName(ScenarioManifest.FindDefaultPath())!;
				QuestDialogLearnedScript script = QuestDialogLearnedScript.LoadForLive(
					Path.Combine(knowledgeRoot, "learned-custom-quests", "1100.json"));
				await ReplayLearnedQuestDialogAsync(subject.Session, kalio, script, token);
			}, cancellationToken);

			await director.StepAsync("level-subject-to-seven", token =>
				gm.ExecuteAsync(new GmCommand("set", ["level", "7"], "level to 7"), gmSubject, token), cancellationToken);
			await MoveSubjectWithDirectorAsync(director, subject, gm, 210010000,
				240.1f, 1639.46f, 100.375f, "setup-pernos", cancellationToken);
			int pernos = await subject.Session.WaitForNpcAsync(790001, cancellationToken);
			await subject.StepAsync("refuse-then-accept-1123", async token =>
			{
				await subject.Session.SendPacketAsync(subject.Session.Api.TalkTo(pernos), token);
				await subject.Session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token);
				await subject.Session.SendPacketAsync(subject.Session.Api.SelectDialog(pernos, DialogAction.QUEST_SELECT,
					questId: 1123), token);
				await subject.Session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token);
				await subject.Session.SendPacketAsync(subject.Session.Api.SelectDialog(pernos, DialogAction.ASK_QUEST_ACCEPT,
					questId: 1123), token);
				await subject.Session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token,
					packet => packet.Get<ushort>("dialogPageId") == 4);
				await subject.Session.SendPacketAsync(subject.Session.Api.SelectDialog(pernos, DialogAction.QUEST_REFUSE_1,
					questId: 1123), token);
				await subject.Session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token,
					packet => packet.Get<ushort>("dialogPageId") == 1004);
				if (subject.Session.Api.World.Quests.ContainsKey(1123))
					throw new InvalidDataException("Refused Q1123 was started.");
				await subject.Session.StartQuestAsync(pernos, 1123, token);
			}, cancellationToken);

			await MoveSubjectWithDirectorAsync(director, subject, gm, 210010000,
				210f, 1900f, 170f, "setup-1123-zone-edge", cancellationToken);
			await subject.StepAsync("enter-1123-zone", async token =>
			{
				await subject.Session.MoveToPositionAsync(new BotPosition(222.69f, 1900f, 170f, 0), token);
				DecodedBotServerPacket movie = await subject.Session.WaitForPacketAsync(typeof(SM_PLAY_MOVIE), token,
					packet => packet.Get<int>("questId") == 1123);
				if (movie.Get<int>("cutsceneId") != 11)
					throw new InvalidDataException("Q1123 did not play cutscene 11.");
				await subject.Session.WaitForQuestStatusAsync(1123, 4, token);
			}, cancellationToken);

			await director.StepAsync("give-quest-diary", token => gm.ExecuteAsync(
				new GmCommand("add", [subject.Session.CharacterName, "182200214", "1"], "You gave"),
				cancellationToken: token), cancellationToken);
			await subject.StepAsync("item-starts-1114", async token =>
			{
				await subject.Session.WaitForInventoryItemAsync(182200214, token);
				BotInventoryItem diary = subject.Session.Api.World.Inventory.Values.Single(item => item.ItemId == 182200214);
				await subject.Session.SendPacketAsync(GameClientPackets.UseItem(diary.ObjectId, 0, 0), token);
				await subject.Session.WaitForQuestStatusAsync(1114, 3, token);
			}, cancellationToken);

			await director.StepAsync("make-subject-level-twelve-daeva", async token =>
			{
				await gm.ExecuteAsync(new GmCommand("set", ["level", "9"], "level to 9"), gmSubject, token);
				await gm.ExecuteVerifiedAsync(new GmCommand("set", ["class", "gladiator"], "replyless class change"),
					new GmCommand("set", ["level", "12"], "level to 12"), gmSubject, token);
			}, cancellationToken);
			await MoveSubjectWithDirectorAsync(director, subject, gm, 210030000,
				1364.77f, 1842.96f, 121.471f, "setup-gano", cancellationToken);
			int gano = await subject.Session.WaitForNpcAsync(203123, cancellationToken);
			await subject.StepAsync("arm-and-abandon-1146-timer", async token =>
			{
				await subject.Session.StartQuestAsync(gano, 1146, token);
				if (!subject.Session.Api.World.Quests.TryGetValue(1146, out BotQuestState? timed) || timed.TimerSeconds != 900)
					throw new InvalidDataException("Q1146 did not arm its 900-second timer.");
				await subject.Session.WaitForInventoryItemAsync(182200519, token);
				RequireItemCount(subject.Session.Api.World, 182200519, 1);
				await subject.Session.SendPacketAsync(subject.Session.Api.DeleteQuest(1146), token);
				await subject.Session.WaitForPacketAsync(typeof(SM_QUEST_ACTION), token,
					packet => packet.Get<int>("questId") == 1146 && packet.Get<byte>("action") == 3);
				RequireItemCount(subject.Session.Api.World, 182200519, 0);
			}, cancellationToken);

			await director.StepAsync("level-subject-to-fourteen", token =>
				gm.ExecuteAsync(new GmCommand("set", ["level", "14"], "level to 14"), gmSubject, token), cancellationToken);
			await MoveSubjectWithDirectorAsync(director, subject, gm, 210030000,
				1254.72f, 2223.85f, 144.875f, "setup-cannon", cancellationToken);
			int cannon = await subject.Session.WaitForNpcAsync(203145, cancellationToken);
			await subject.StepAsync("accept-1149", token => subject.Session.StartQuestAsync(cannon, 1149, token),
				cancellationToken);
			await MoveSubjectWithDirectorAsync(director, subject, gm, 210030000,
				950.3f, 2480.79f, 187.75f, "setup-poppy", cancellationToken);
			int poppy = await subject.Session.WaitForNpcAsync(203191, cancellationToken);
			await subject.StepAsync("start-poppy-follow", async token =>
			{
				await subject.Session.SendPacketAsync(subject.Session.Api.TalkTo(poppy), token);
				await subject.Session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token);
				await subject.Session.SendPacketAsync(subject.Session.Api.SelectDialog(poppy, DialogAction.QUEST_SELECT,
					questId: 1149), token);
				await subject.Session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token,
					packet => packet.Get<ushort>("dialogPageId") == 1352);
				await subject.Session.SendPacketAsync(subject.Session.Api.SelectDialog(poppy, DialogAction.SETPRO1,
					questId: 1149), token);
				await subject.Session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token,
					packet => packet.Get<ushort>("dialogPageId") == 0);
			}, cancellationToken);
			await director.StepAsync("place-escort-at-cannon", async token =>
			{
				int packetStart = director.Session.PacketHistory.Count;
				await director.Session.SendPacketAsync(director.Session.Api.Target(poppy), token);
				foreach (string command in new[]
				{
					"//spawnu x 1254.72",
					"//spawnu y 2223.85",
					"//spawnu z 144.875",
					"//moveto 210030000 1254.72 2223.85 144.875",
					$"//movetome {subject.Session.CharacterName}"
				})
					await director.Session.SendPacketAsync(director.Session.Api.Say(command), token);

				await subject.Session.CompleteTeleportAsync(210030000, token);
				await director.Session.CompleteTeleportAsync(210030000, token);
				foreach (string expected in new[]
				{
					"X:1254.72", "Y:2223.85", "Z:144.875", "Teleported to", "Teleported [charname:"
				})
				{
					if (!director.Session.PacketHistory.Skip(packetStart).Any(packet =>
						packet.PacketType == typeof(SM_MESSAGE) &&
						packet.Get<string>("message").Contains(expected, StringComparison.Ordinal)))
					{
						await director.Session.WaitForPacketAsync(typeof(SM_MESSAGE), token,
							packet => packet.Get<string>("message").Contains(expected, StringComparison.Ordinal));
					}
				}
			}, cancellationToken);
			await subject.StepAsync("escort-poppy-reaches-cannon", async token =>
			{
				DecodedBotServerPacket movie = await subject.Session.WaitForPacketAsync(typeof(SM_PLAY_MOVIE), token,
					packet => packet.Get<int>("questId") == 1149);
				if (movie.Get<int>("cutsceneId") != 12)
					throw new InvalidDataException("Q1149 did not play cutscene 12.");
				await subject.Session.WaitForQuestStatusAsync(1149, 4, token);
			}, cancellationToken);

			QuestCoverageReceipt.SaveFromEnvironment("LIVE", "Q3", subject.Session.Api.World);
			await subject.StepAsync("quit", subject.Session.QuitAsync, cancellationToken);
			await director.StepAsync("quit", director.Session.QuitAsync, cancellationToken);
			subject.Trace.WriteAction(subject.LastStep, "scenario:complete", new Dictionary<string, object?> { ["scenario"] = "Q3" });
			director.Trace.WriteAction(director.LastStep, "scenario:complete", new Dictionary<string, object?> { ["scenario"] = "Q3-director" });
			Console.WriteLine("LIVE Q3 completed.");
			return 0;
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			throw;
		}
		catch (Exception ex)
		{
			Console.Error.WriteLine($"Q3 failed: {ex}");
			return 1;
		}
	}

	private static async Task ReplayLearnedQuestDialogAsync(
		LiveBotSession session,
		int targetObjectId,
		QuestDialogLearnedScript script,
		CancellationToken cancellationToken)
	{
		for (int index = 0; index < script.Steps.Count; index++)
		{
			QuestDialogLearnedStep step = script.Steps[index];
			if (!session.Api.World.Objects.TryGetValue(targetObjectId, out BotKnownObject? target) ||
				target.TemplateId != step.TargetNpcId)
				throw new InvalidDataException($"Learned Q{script.QuestId} step {index + 1} expected NPC {step.TargetNpcId}.");
			if (!session.Api.World.Quests.TryGetValue(script.QuestId, out BotQuestState? quest) ||
				quest.Status != step.QuestStatus)
				throw new InvalidDataException(
					$"Learned Q{script.QuestId} step {index + 1} expected status {step.QuestStatus}, got {quest?.Status}.");

			await session.SendPacketAsync(session.Api.SelectDialog(
				targetObjectId, checked((ushort)step.ActionId), questId: script.QuestId), cancellationToken);
			if (index + 1 < script.Steps.Count)
			{
				await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), cancellationToken,
					packet => packet.Get<int>("targetObjectId") == targetObjectId);
				await session.WaitForQuestStatusAsync(
					script.QuestId, checked((byte)script.Steps[index + 1].QuestStatus), cancellationToken);
			}
			else
			{
				await session.WaitForQuestStatusAsync(script.QuestId, 5, cancellationToken);
				await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), cancellationToken,
					packet => packet.Get<int>("targetObjectId") == targetObjectId);
			}
		}
	}
}
