using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace Aion.GameServer.Dataholders;

/// <summary>Java's victim-level XP rewards and killer-level acquisition limits.</summary>
[XmlRoot("pvp_exp_table")]
public class PvpExpTable
{
    [XmlElement("level")] public List<Row> Rows = new();
    private Row[] rowsByLevel = Array.Empty<Row>();
    public void AfterUnmarshal(object parent)
    {
        rowsByLevel = new Row[Rows.Count];
        foreach (Row row in Rows) rowsByLevel[row.Level - 1] = row;
        Rows = null!;
    }
    private Row GetRow(int level) => rowsByLevel[Math.Clamp(unchecked(level - 1), 0, rowsByLevel.Length - 1)];
    public int GetExp(int victimLevel) => GetRow(victimLevel).Exp;
    public int GetMaxFromAllUser(int level) => GetRow(level).MaxFromAllUser;
    public int GetReduceAmount(int level) => GetRow(level).ReduceAmount;
    public long GetReduceIntervalMillis(int level) => GetRow(level).ReduceInterval * 1000L;
    public long GetDelayTimeMillis(int level) => GetRow(level).DelayTime * 1000L;
    public int GetMaxLevel() => rowsByLevel.Length;

    [XmlType("pvp_exp_row")]
    public class Row
    {
        [XmlAttribute("lvl")] public int Level;
        [XmlAttribute("exp")] public int Exp;
        [XmlAttribute("get_max_from_all_user")] public int MaxFromAllUser;
        [XmlAttribute("get_max_reduce_amount")] public int ReduceAmount;
        [XmlAttribute("get_max_reduce_interval")] public int ReduceInterval;
        [XmlAttribute("delay_time")] public int DelayTime;
    }
}
