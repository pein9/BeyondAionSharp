using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Serialization;

namespace Aion.GameServer.Dataholders;

[XmlRoot("pvp_exp_mod_table")]
public class PvpExpModTable
{
    [XmlElement("level_diff_mod")] public List<Row> Rows = new();
    private int minLevelDiff;
    private int[][] modsByLevelDiff = Array.Empty<int[]>();
    public void AfterUnmarshal(object parent)
    {
        minLevelDiff = Rows.Min(r => r.LevelDiff);
        int max = Rows.Max(r => r.LevelDiff);
        modsByLevelDiff = new int[max - minLevelDiff + 1][];
        foreach (Row row in Rows) modsByLevelDiff[row.LevelDiff - minLevelDiff] = row.Mods;
        Rows = null!;
    }
    public float GetMultiplier(int killerLevel, int victimLevel)
    {
        int[] mods = modsByLevelDiff[Math.Clamp(unchecked(killerLevel - victimLevel - minLevelDiff), 0, modsByLevelDiff.Length - 1)];
        return mods[Math.Clamp(unchecked(killerLevel - 1) / 10, 0, mods.Length - 1)] / 100f;
    }
    public int Size() => modsByLevelDiff.Length;
    [XmlType("pvp_exp_mod_row")]
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
