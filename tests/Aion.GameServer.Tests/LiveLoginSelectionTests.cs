using Aion.LiveBots;

namespace Aion.GameServer.Tests;

public sealed class LiveLoginSelectionTests
{
	[Fact]
	public async Task ServerListUpdatesBetweenSelectionAndPlayAreObservedWithoutHidingThePlayReply()
	{
		byte[] play = new byte[24]; play[0] = 7; play[9] = 1;
		var replies = new Queue<byte[]>([List(2, 1), List(1, 2), play]);
		var observed = new List<byte[]>();
		Assert.Same(play, await LiveLoginSelection.ReadPlayOkAsync(_ => Task.FromResult(replies.Dequeue()), 1, observed.Add, default));
		Assert.Equal(2, observed.Count);
		Assert.Empty(replies);
	}

	[Theory]
	[InlineData(1)]
	[InlineData(6)]
	[InlineData(9)]
	[InlineData(7)]
	public async Task UnexpectedResponsesAreNeverSkipped(int opcode)
	{
		byte[] reply = new byte[24]; reply[0] = (byte)opcode; reply[1] = 7; reply[9] = 2;
		var error = await Assert.ThrowsAsync<InvalidDataException>(() => LiveLoginSelection.ReadPlayOkAsync(
			_ => Task.FromResult(reply), 1, _ => throw new InvalidOperationException("Not a list"), default));
		Assert.Contains($"0x{opcode:X2}", error.Message, StringComparison.Ordinal);
	}

	[Fact]
	public async Task ListFloodIsBounded()
	{
		int reads = 0;
		await Assert.ThrowsAsync<InvalidDataException>(() => LiveLoginSelection.ReadPlayOkAsync(
			_ => { reads++; return Task.FromResult(List(1)); }, 1, _ => { }, default));
		Assert.Equal(17, reads);
	}

	[Theory]
	[InlineData("short")]
	[InlineData("truncated")]
	[InlineData("duplicate")]
	[InlineData("missing")]
	[InlineData("offline")]
	public void InvalidOrUnavailableSelectionFailsClosed(string defect)
	{
		byte[] payload = List(2, 1);
		switch (defect)
		{
			case "short": payload = [4]; break;
			case "truncated": payload = payload[..^1]; break;
			case "duplicate": payload[24] = 2; break;
			case "missing": payload[24] = 3; break;
			case "offline": payload[39] = 0; break;
		}
		Assert.Throws<InvalidDataException>(() => LiveLoginSelection.RequireOnlineServer(payload, 1));
	}

	private static byte[] List(params byte[] ids)
	{
		byte[] payload = new byte[3 + ids.Length * 21 + 16]; payload[0] = 4; payload[1] = (byte)ids.Length;
		for (int index = 0; index < ids.Length; index++) { payload[3 + 21 * index] = ids[index]; payload[18 + 21 * index] = 1; }
		return payload;
	}
}
