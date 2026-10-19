using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.World;

/// <summary>Physical weather conditions shown by the client's particle effect.</summary>
[XmlType("WeatherState")]
public enum WeatherState { RAIN, SNOW, WIND }
