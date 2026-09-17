using Aion.Commons.Logging;
using Microsoft.Extensions.Logging;

namespace Aion.Commons.Tests;

public sealed class CapturingLoggerProviderTests
{
	[Fact]
	public void CapturesTemplateFingerprintExceptionAndNestedScopes()
	{
		using var provider = new CapturingLoggerProvider();
		using var factory = LoggerFactory.Create(builder =>
		{
			builder.ClearProviders();
			builder.SetMinimumLevel(LogLevel.Trace);
			builder.AddProvider(provider);
		});
		var logger = factory.CreateLogger("TEST_LOG");
		using var scenario = logger.BeginScope(new Dictionary<string, object?> { ["run"] = "r1", ["scenario"] = "S0" });
		using var bot = logger.BeginScope(new Dictionary<string, object?> { ["bot"] = "b01", ["step"] = "s03" });
		var exception = new InvalidOperationException("boom");

		logger.LogError(exception, "Failed object {ObjectId}", 42);

		CapturedLogEntry entry = Assert.Single(provider.Entries);
		Assert.Equal("TEST_LOG", entry.Category);
		Assert.Equal(LogLevel.Error, entry.Level);
		Assert.Equal("Failed object {ObjectId}", entry.Template);
		Assert.Equal("Failed object 42", entry.Message);
		Assert.Same(exception, entry.Exception);
		Assert.Matches("^[0-9a-f]{8}$", entry.Fingerprint.Value);
		Assert.Equal("r1", entry.Scopes["run"]);
		Assert.Equal("S0", entry.Scopes["scenario"]);
		Assert.Equal("b01", entry.Scopes["bot"]);
		Assert.Equal("s03", entry.Scopes["step"]);
	}
}
