using System.Text;
using System.Xml.Serialization;
using Aion.GameServer.Utils.Xml;

namespace Aion.GameServer.Tests;

public sealed class JAXBUtilTests
{
	[Fact]
	public void SerializeDeclaresUtf8AndCanBeReadAfterWriteAllText()
	{
		string xml = JAXBUtil.Serialize(new SampleDocument { Value = "Poppy" });
		Assert.StartsWith("<?xml version=\"1.0\" encoding=\"utf-8\"?>", xml, StringComparison.Ordinal);

		string path = Path.Combine(Path.GetTempPath(), $"aion-jaxb-{Guid.NewGuid():N}.xml");
		try
		{
			File.WriteAllText(path, xml);
			var roundTrip = JAXBUtil.Deserialize<SampleDocument>(new FileInfo(path));
			Assert.Equal("Poppy", roundTrip.Value);
		}
		finally
		{
			File.Delete(path);
		}
	}

	[XmlRoot("sample")]
	public sealed class SampleDocument
	{
		[XmlElement("value")]
		public string Value { get; set; } = string.Empty;
	}
}
