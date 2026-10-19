using System.Collections.Generic;
using Aion.GameServer.Configs.Main;

namespace Aion.GameServer.Model.GameObjects.Players;

/// <summary>Java parity: model/gameobjects/player/Macros.</summary>
public class Macros
{
    private readonly SortedDictionary<int, Macro> macrosById = new SortedDictionary<int, Macro>();
    private readonly object _lock = new object();

    public List<Macro> GetAll()
    {
        lock (_lock)
        {
            return new List<Macro>(macrosById.Values);
        }
    }

    /// <summary>true if given macro ID was not used before.</summary>
    public bool Add(int macroId, string macroXML)
    {
        lock (_lock)
        {
            if (!IsValidId(macroId))
                throw new System.ArgumentException("Invalid macro ID: " + macroId);
            bool existed = macrosById.ContainsKey(macroId);
            macrosById[macroId] = new Macro(macroId, macroXML);
            return !existed;
        }
    }

    public static bool IsValidId(int macroId) => macroId >= 1 && macroId <= CustomConfig.MACROS_LIMIT;

    public bool Remove(int macroId)
    {
        lock (_lock)
        {
            return macrosById.Remove(macroId);
        }
    }

    public record Macro(int Id, string Xml);
}
