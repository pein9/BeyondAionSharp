using Aion.Commons.Logging;

namespace Aion.Commons.Tests;

public sealed class LogFingerprintTests
{
	[Fact]
	public void NormalizesConcatenatedObjectIdsAndHexDumps()
	{
		const string first = "Error parsing input from AionConnection [state=IN_GAME, account=Account [id=42]], packet size: 193, content: AA BB CC DD";
		const string second = "Error parsing input from AionConnection [state=IN_GAME, account=Account [id=999]], packet size: 7, content: 01 02 03 04";

		var normalizedFirst = LogFingerprint.NormalizeTemplate(first);
		var normalizedSecond = LogFingerprint.NormalizeTemplate(second);

		Assert.Equal("Error parsing input from <AionConnection>, packet size: #, content: <hex>", normalizedFirst);
		Assert.Equal(normalizedFirst, normalizedSecond);
	}

	[Fact]
	public void LambdaOrdinalDoesNotChangeExceptionFingerprint()
	{
		var first = CaptureLambdaException(firstLambda: true);
		var second = CaptureLambdaException(firstLambda: false);
		var state = Template("Failure in callback 17");

		var firstFingerprint = LogFingerprint.Create(state, first, first.Message);
		var secondFingerprint = LogFingerprint.Create(state, second, second.Message);

		Assert.Equal(firstFingerprint.Frame, secondFingerprint.Frame);
		Assert.EndsWith(".CaptureLambdaException", firstFingerprint.Frame);
		Assert.Equal(firstFingerprint.Value, secondFingerprint.Value);
	}

	[Fact]
	public void CallerMetadataProvidesLocationWithoutException()
	{
		var state = Template(
			"Unknown packet 123",
			new(AionLog.CallerTypeKey, "Aion.GameServer.Handler+<>c__DisplayClass12_3"),
			new(AionLog.CallerMemberKey, "<Process>b__12_4"));

		var fingerprint = LogFingerprint.Create(state, exception: null, "Unknown packet 123");

		Assert.Equal("Aion.GameServer.Handler.Process", fingerprint.Frame);
		Assert.Equal("Unknown packet #", fingerprint.NormalizedTemplate);
		Assert.Matches("^[0-9a-f]{8}$", fingerprint.Value);
	}

	[Theory]
	[InlineData("Aion.GameServer.Worker+<RunAsync>d__42", "MoveNext", "Aion.GameServer.Worker.RunAsync")]
	[InlineData("Aion.GameServer.Worker+<>c__DisplayClass2_1", "<Run>b__3", "Aion.GameServer.Worker.Run")]
	public void NormalizesCompilerGeneratedLocations(string type, string member, string expected)
	{
		Assert.Equal(expected, LogFingerprint.NormalizeCodeLocation(type, member));
	}

	private static Exception CaptureLambdaException(bool firstLambda)
	{
		Action first = () => throw new InvalidOperationException("lambda exploded");
		Action second = () => throw new InvalidOperationException("lambda exploded");
		try
		{
			(firstLambda ? first : second)();
		}
		catch (Exception exception)
		{
			return exception;
		}
		throw new InvalidOperationException("Unreachable after the throwing callback.");
	}

	private static IReadOnlyList<KeyValuePair<string, object?>> Template(
		string template,
		params KeyValuePair<string, object?>[] metadata)
	{
		var values = new List<KeyValuePair<string, object?>>(metadata)
		{
			new("{OriginalFormat}", template),
		};
		return values;
	}
}
