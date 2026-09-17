using System.Xml.Linq;
using Aion.GameServer.Dataholders.LoadingUtils;

namespace Aion.GameServer.Tests;

public sealed class XmlMergerAtomicCacheTests
{
	[Fact]
	public async Task ConcurrentMergersPublishOneCompleteCache()
	{
		using var temp = new TempDirectory();
		string sourceDirectory = Path.Combine(temp.Path, "source");
		string importDirectory = Path.Combine(sourceDirectory, "parts");
		string sourceFile = Path.Combine(sourceDirectory, "static_data.xml");
		string cacheFile = Path.Combine(temp.Path, "cache", "static_data.xml");
		Directory.CreateDirectory(importDirectory);
		File.WriteAllText(sourceFile, "<static_data><import file=\"parts\" /></static_data>");
		for (var i = 0; i < 64; i++)
			File.WriteAllText(Path.Combine(importDirectory, $"part-{i:D2}.xml"), $"<part id=\"{i}\">{new string('x', 16_384)}</part>");

		using var start = new ManualResetEventSlim();
		Task<XmlMergeResult>[] merges = Enumerable.Range(0, 8)
			.Select(_ => Task.Run(() =>
			{
				start.Wait();
				return new XmlMerger(sourceFile, cacheFile).Merge();
			}))
			.ToArray();
		start.Set();

		XmlMergeResult[] results = await Task.WhenAll(merges);
		var document = XDocument.Load(cacheFile);

		Assert.Single(results, result => result.FileWasModified);
		Assert.Equal(64, document.Root!.Elements("part").Count());
		Assert.Empty(Directory.EnumerateFiles(Path.GetDirectoryName(cacheFile)!, "*.tmp"));
	}

	[Fact]
	public void FailedMergePreservesLastCompleteCacheAndMetadata()
	{
		using var temp = new TempDirectory();
		string sourceFile = Path.Combine(temp.Path, "static_data.xml");
		string importFile = Path.Combine(temp.Path, "part.xml");
		string cacheFile = Path.Combine(temp.Path, "cache", "static_data.xml");
		string metadataFile = cacheFile + ".properties";
		File.WriteAllText(sourceFile, "<static_data><import file=\"part.xml\" /></static_data>");
		File.WriteAllText(importFile, "<part id=\"good\" />");
		new XmlMerger(sourceFile, cacheFile).Merge();
		byte[] goodCache = File.ReadAllBytes(cacheFile);
		byte[] goodMetadata = File.ReadAllBytes(metadataFile);

		File.WriteAllText(importFile, "<part>");

		Assert.ThrowsAny<Exception>(() => new XmlMerger(sourceFile, cacheFile).Merge());
		Assert.Equal(goodCache, File.ReadAllBytes(cacheFile));
		Assert.Equal(goodMetadata, File.ReadAllBytes(metadataFile));
		Assert.Empty(Directory.EnumerateFiles(Path.GetDirectoryName(cacheFile)!, "*.tmp"));
	}

	private sealed class TempDirectory : IDisposable
	{
		public TempDirectory()
		{
			Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"aion-xml-cache-{Guid.NewGuid():N}");
			Directory.CreateDirectory(Path);
		}

		public string Path { get; }

		public void Dispose() => Directory.Delete(Path, recursive: true);
	}
}
