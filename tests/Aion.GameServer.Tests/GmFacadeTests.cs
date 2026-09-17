using Aion.Bots.Gm;
using Aion.Bots.Protocol;
using Aion.GameServer.Model.Account;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ClientPackets;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils.ChatHandlers;
using Aion.GameServer.World.Knownlist;

namespace Aion.GameServer.Tests;

public sealed class GmFacadeTests
{
	[Fact]
	public async Task SimulationFacadeExecutesRegisteredHandlerDirectlyAgainstRegularSubject()
	{
		Player director = Player(9000, "Director", accessLevel: 9);
		Player subject = Player(1001, "Subject", accessLevel: 0);
		var handler = new RecordingAdminCommand();
		var facade = new SimulationGmFacade(
			director,
			commands: () => [handler],
			resolvePlayer: objectId => objectId == 1001 ? subject : null);
		var command = new GmCommand("probe", ["one", "two"], "completed");

		GmCommandResult result = await facade.ExecuteAsync(command, new GmSubject(1001, "Subject"));

		Assert.Null(result.Reply);
		Assert.Same(director, handler.Director);
		Assert.Same(subject, handler.Subject);
		Assert.Equal(["one", "two"], handler.Arguments);
		Assert.Equal((byte)0, subject.AccessLevel);
	}

	[Fact]
	public async Task SimulationFacadeRejectsPrivilegedScenarioSubject()
	{
		Player director = Player(9000, "Director", accessLevel: 9);
		Player subject = Player(1001, "Subject", accessLevel: 1);
		var facade = new SimulationGmFacade(
			director,
			commands: () => [new RecordingAdminCommand()],
			resolvePlayer: _ => subject);

		var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
			facade.ExecuteAsync(new GmCommand("probe", [], "completed"), new GmSubject(1001, "Subject")));

		Assert.Contains("access level 0", exception.Message, StringComparison.Ordinal);
	}

	[Fact]
	public async Task LiveFacadeUsesDirectorChatAndWaitsForMatchingMessage()
	{
		var sent = new List<BotClientPacket>();
		var received = new Queue<DecodedBotServerPacket>(
		[
			new(typeof(SM_KEY), new Dictionary<string, object?> { ["encodedKey"] = 7 }),
			new(typeof(SM_MESSAGE), new Dictionary<string, object?> { ["message"] = "unrelated" }),
			new(typeof(SM_MESSAGE), new Dictionary<string, object?> { ["message"] = "Probe completed for Subject" }),
		]);
		var facade = new LiveGmFacade(
			LiveGmFacade.DirectorAccount,
			(packet, _) =>
			{
				sent.Add(packet);
				return Task.CompletedTask;
			},
			_ => Task.FromResult(received.Dequeue()));

		GmCommandResult result = await facade.ExecuteAsync(
			new GmCommand("probe", ["one", "two"], "Probe completed"),
			new GmSubject(1001, "Subject"));

		Assert.Equal("Probe completed for Subject", result.Reply);
		Assert.Collection(sent,
			packet =>
			{
				Assert.Equal(typeof(CM_TARGET_SELECT), packet.PacketType);
				var reader = new PacketBodyReader(packet.Body);
				Assert.Equal(1001, reader.ReadInt32());
			},
			packet =>
			{
				Assert.Equal(typeof(CM_CHAT_MESSAGE_PUBLIC), packet.PacketType);
				var reader = new PacketBodyReader(packet.Body);
				Assert.Equal(0, reader.ReadByte());
				Assert.Equal("//probe one two", reader.ReadString());
			});
	}

	[Fact]
	public void LiveFacadeRejectsAnyAccountOtherThanSeededDirector()
	{
		var exception = Assert.Throws<ArgumentException>(() => new LiveGmFacade(
			"ordinary-bot",
			(_, _) => Task.CompletedTask,
			_ => Task.FromException<DecodedBotServerPacket>(new EndOfStreamException())));

		Assert.Contains("director", exception.Message, StringComparison.Ordinal);
	}

	private static Player Player(int objectId, string name, sbyte accessLevel)
	{
		var common = new PlayerCommonData(objectId);
		common.SetName(name);
		var account = new Account(objectId + 10_000);
		account.SetName(name.ToLowerInvariant());
		account.SetAccessLevel(accessLevel);
		var player = new Player(new PlayerAccountData(common, new PlayerAppearance()), account);
		player.SetKnownlist(new KnownList(player));
		return player;
	}

	private sealed class RecordingAdminCommand : AdminCommand
	{
		public RecordingAdminCommand() : base("probe") { }

		public Player? Director { get; private set; }
		public Player? Subject { get; private set; }
		public string[] Arguments { get; private set; } = [];

		public override bool ValidateAccess(Player player) => player.IsStaff();

		public override void Execute(Player player, params string[] paramsArr)
		{
			Director = player;
			Subject = player.GetTarget() as Player;
			Arguments = paramsArr;
		}
	}
}
