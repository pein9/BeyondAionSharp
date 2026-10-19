using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace Aion.GameServer.Dataholders;

[XmlRoot("mentee_exp_limit_table")]
public class MenteeExpLimitTable
{
    [XmlElement("level")] public List<Row> Rows = new();
    private long[] limitsByLevel = Array.Empty<long>();
    public void AfterUnmarshal(object parent)
    {
        limitsByLevel = new long[Rows.Count];
        foreach (Row row in Rows) limitsByLevel[row.Level - 1] = row.ExpLimit;
        Rows = null!;
    }
    public long GetExpLimit(int level) => limitsByLevel[Math.Clamp(unchecked(level - 1), 0, limitsByLevel.Length - 1)];
    public int Size() => limitsByLevel.Length;
    [XmlType("mentee_exp_limit_row")]
    public class Row
    {
        [XmlAttribute("lvl")] public int Level;
        [XmlAttribute("exp_limit")] public long ExpLimit;
    }
}
