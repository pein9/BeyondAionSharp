using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Serialization;

namespace Aion.GameServer.Dataholders;

[XmlRoot("party_exp_mod_table")]
public class PartyExpModTable
{
    [XmlElement("level_diff_mod")] public List<Row> Rows = new();
    private int columns;
    private int[] mods = Array.Empty<int>();
    public void AfterUnmarshal(object parent)
    {
        columns = Rows[0].Mods.Length;
        mods = new int[Rows.Count * columns];
        foreach (Row row in Rows) Array.Copy(row.Mods, 0, mods, row.LevelDiff * columns, columns);
        Rows = null!;
    }
    public float GetWeight(int memberLevel, int highestLevel)
    {
        int diff = unchecked(highestLevel - memberLevel);
        if (diff < 0) return 0;
        // Java deliberately indexes a flat table: the tens column can run into the following row.
        int index = unchecked(Math.Min(diff, Size() - 1) * columns + highestLevel / 10 + 1);
        return mods[Math.Min(index, mods.Length - 1)] / 100f;
    }
    // Native StaticData permits absent undeclared extension holders in synthetic boot fixtures.
    public int Size() => columns == 0 ? 0 : mods.Length / columns;
    [XmlType("party_exp_mod_row")]
    public class Row
    {
        [XmlAttribute("level_diff")] public int LevelDiff;
        [XmlIgnore] public int[] Mods = Array.Empty<int>();
        [XmlAttribute("mods")] public string ModsRaw
        {
            get => string.Join(" ", Mods);
            set => Mods = value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Select(v => int.Parse(v, CultureInfo.InvariantCulture)).ToArray();
        }
    }
}
