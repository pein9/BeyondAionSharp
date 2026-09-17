using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Aion.GameServer.Tests;

public sealed partial class SourceIsolationArchitectureTests
{
	[Fact]
	public void ProductionProjectsDoNotReferenceTestOrToolProjects()
	{
		var root = FindRepositoryRoot();
		var sourceRoot = Path.GetFullPath(Path.Combine(root, "src")) + Path.DirectorySeparatorChar;
		var violations = Directory.EnumerateFiles(sourceRoot, "*.csproj", SearchOption.AllDirectories)
			.SelectMany(project => XDocument.Load(project)
				.Descendants("ProjectReference")
				.Select(reference => (Project: project, Include: (string?)reference.Attribute("Include"))))
			.Where(reference => !string.IsNullOrWhiteSpace(reference.Include))
			.Select(reference => (
				reference.Project,
				Target: Path.GetFullPath(Path.Combine(Path.GetDirectoryName(reference.Project)!, reference.Include!))))
			.Where(reference => !reference.Target.StartsWith(sourceRoot, StringComparison.OrdinalIgnoreCase))
			.Select(reference => $"{Path.GetRelativePath(root, reference.Project)} -> {Path.GetRelativePath(root, reference.Target)}")
			.Order(StringComparer.Ordinal)
			.ToArray();

		Assert.True(violations.Length == 0,
			"Production projects must only reference projects under src/:" + Environment.NewLine + string.Join(Environment.NewLine, violations));
	}

	[Fact]
	public void ProductionSourcesDoNotDeclareBotOrSimulationNamespaces()
	{
		var root = FindRepositoryRoot();
		var sourceRoot = Path.Combine(root, "src");
		var violations = Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
			.Where(file => BotNamespaceDeclaration().IsMatch(File.ReadAllText(file)))
			.Select(file => Path.GetRelativePath(root, file))
			.Order(StringComparer.Ordinal)
			.ToArray();

		Assert.True(violations.Length == 0,
			"Production sources must not declare Aion.Bots or Aion.Simulation namespaces:" + Environment.NewLine +
			string.Join(Environment.NewLine, violations));
	}

	private static string FindRepositoryRoot()
	{
		DirectoryInfo? directory = new(AppContext.BaseDirectory);
		while (directory != null)
		{
			if (File.Exists(Path.Combine(directory.FullName, "AionServer.slnx")))
				return directory.FullName;
			directory = directory.Parent;
		}

		throw new DirectoryNotFoundException("Could not locate AionServer.slnx above " + AppContext.BaseDirectory);
	}

	[GeneratedRegex(@"(?m)^\s*namespace\s+Aion\.(?:Bots|Simulation)(?:\.|\s*[;{])")]
	private static partial Regex BotNamespaceDeclaration();
}
