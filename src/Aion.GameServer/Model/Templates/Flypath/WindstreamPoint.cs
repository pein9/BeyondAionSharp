using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Flypath;

/// <summary>Java 332d54885: one sample per second, with entry and exit enabled by default.</summary>
public class WindstreamPoint
{
    [XmlAttribute("x")] public float x;
    [XmlAttribute("y")] public float y;
    [XmlAttribute("z")] public float z;
    [XmlAttribute("entry")] public bool entry = true;
    [XmlAttribute("exit")] public bool exit = true;

    public float GetX() => x;
    public float GetY() => y;
    public float GetZ() => z;
    public bool CanEnter() => entry;
    public bool CanExit() => exit;
}
