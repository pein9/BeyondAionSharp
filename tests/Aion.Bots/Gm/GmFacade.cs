using Aion.Bots.Api;
using Aion.Bots.Protocol;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.Bots.Gm;

/// <summary>A regular-player scenario subject. GM setup must never require elevating the subject account.</summary>
public sealed record GmSubject
{
	public GmSubject(int objectId, string name)
	{
		if (objectId <= 0)
			throw new ArgumentOutOfRangeException(nameof(objectId));
		if (string.IsNullOrWhiteSpace(name))
			throw new ArgumentException("A GM subject must have a character name.", nameof(name));
		ObjectId = objectId;
		Name = name;
	}

	public int ObjectId { get; }
	public string Name { get; }
}

/// <summary>A structured admin command plus the acknowledgement that proves LIVE execution completed.</summary>
public sealed record GmCommand
{
	public GmCommand(string alias, IReadOnlyList<string> arguments, string expectedReplyFragment)
	{
		if (string.IsNullOrWhiteSpace(alias) || alias.StartsWith('/') || alias.Any(char.IsWhiteSpace))
			throw new ArgumentException("Use an admin-command alias without slashes or whitespace.", nameof(alias));
		ArgumentNullException.ThrowIfNull(arguments);
		if (arguments.Any(argument => argument == null || argument.ContainsAny('\r', '\n')))
			throw new ArgumentException("GM command arguments cannot be null or contain line breaks.", nameof(arguments));
		if (string.IsNullOrWhiteSpace(expectedReplyFragment))
			throw new ArgumentException("LIVE GM commands require a non-empty acknowledgement fragment.", nameof(expectedReplyFragment));

		Alias = alias;
		Arguments = arguments.ToArray();
		ExpectedReplyFragment = expectedReplyFragment;
	}

	public string Alias { get; }
	public IReadOnlyList<string> Arguments { get; }
	public string ExpectedReplyFragment { get; }
	public string ChatText => Arguments.Count == 0
		? AdminCommand.PREFIX + Alias
		: AdminCommand.PREFIX + Alias + " " + string.Join(' ', Arguments);
}

public sealed record GmCommandResult(string? Reply);

/// <summary>Mode-neutral setup facade used by shared player scenarios.</summary>
public interface IGmFacade
{
	Task<GmCommandResult> ExecuteAsync(
		GmCommand command,
		GmSubject? subject = null,
		CancellationToken cancellationToken = default);
}

/// <summary>
/// SIM path: resolve the same registered command class used by chat and invoke its <see cref="ChatCommand.Execute"/>
/// method directly. The director is privileged; a player subject must remain an ordinary account.
/// </summary>
public sealed class SimulationGmFacade : IGmFacade
{
	private readonly Player director;
	private readonly Func<IEnumerable<ChatCommand>> commands;
	private readonly Func<int, Player?> resolvePlayer;

	public SimulationGmFacade(
		Player director,
		Func<IEnumerable<ChatCommand>>? commands = null,
		Func<int, Player?>? resolvePlayer = null)
	{
		this.director = director ?? throw new ArgumentNullException(nameof(director));
		if (!director.IsStaff())
			throw new ArgumentException("The SIM GM facade requires a privileged director player.", nameof(director));
		this.commands = commands ?? (() => ChatProcessor.GetInstance().GetCommandList());
		this.resolvePlayer = resolvePlayer ?? (objectId => Aion.GameServer.World.World.GetInstance().GetPlayer(objectId));
	}

	public Task<GmCommandResult> ExecuteAsync(
		GmCommand command,
		GmSubject? subject = null,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(command);
		cancellationToken.ThrowIfCancellationRequested();

		Player? target = null;
		if (subject != null)
		{
			target = resolvePlayer(subject.ObjectId)
				?? throw new InvalidOperationException($"GM subject {subject.Name} ({subject.ObjectId}) is not online.");
			if (!string.Equals(target.GetName(), subject.Name, StringComparison.OrdinalIgnoreCase))
				throw new InvalidOperationException(
					$"GM subject id {subject.ObjectId} belongs to {target.GetName()}, not {subject.Name}.");
			if (target.IsStaff())
				throw new InvalidOperationException($"GM subject {subject.Name} must keep access level 0.");
		}

		director.SetTarget(target);
		string handlerAlias = AdminCommand.PREFIX + command.Alias;
		ChatCommand handler = commands().SingleOrDefault(candidate =>
			string.Equals(candidate.GetAliasWithPrefix(), handlerAlias, StringComparison.OrdinalIgnoreCase))
			?? throw new InvalidOperationException($"Admin command {handlerAlias} is not registered.");
		if (!handler.ValidateAccess(director))
			throw new UnauthorizedAccessException($"Director cannot execute {handlerAlias}.");

		handler.Execute(director, command.Arguments.ToArray());
		return Task.FromResult(new GmCommandResult(null));
	}
}

/// <summary>
/// LIVE path: the seeded director targets the regular player over the public protocol, sends the real admin chat
/// command and consumes packets until its acknowledgement arrives.
/// </summary>
public sealed class LiveGmFacade : IGmFacade
{
	public const string DirectorAccount = "director";

	private readonly BotApi api;
	private readonly Func<BotClientPacket, CancellationToken, Task> send;
	private readonly Func<CancellationToken, Task<DecodedBotServerPacket>> receive;

	public LiveGmFacade(
		string accountName,
		Func<BotClientPacket, CancellationToken, Task> send,
		Func<CancellationToken, Task<DecodedBotServerPacket>> receive,
		BotApi? api = null)
	{
		if (!string.Equals(accountName, DirectorAccount, StringComparison.Ordinal))
			throw new ArgumentException($"LIVE GM commands must use the seeded '{DirectorAccount}' account.", nameof(accountName));
		this.send = send ?? throw new ArgumentNullException(nameof(send));
		this.receive = receive ?? throw new ArgumentNullException(nameof(receive));
		this.api = api ?? new BotApi();
	}

	public async Task<GmCommandResult> ExecuteAsync(
		GmCommand command,
		GmSubject? subject = null,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(command);
		if (subject != null)
			await send(api.Target(subject.ObjectId), cancellationToken);
		await send(api.Say(command.ChatText), cancellationToken);

		while (true)
		{
			DecodedBotServerPacket packet = await receive(cancellationToken);
			BotClientPacket? reflex = api.Observe(packet);
			if (reflex != null)
				await send(reflex, cancellationToken);
			if (packet.PacketType != typeof(SM_MESSAGE))
				continue;
			string reply = packet.Get<string>("message");
			if (reply.Contains(command.ExpectedReplyFragment, StringComparison.Ordinal))
				return new GmCommandResult(reply);
		}
	}
}
