using System.Data.Common;
using MySqlConnector;

namespace Aion.Commons.Database;

/// <summary>
/// Column lookup by label as Java's JDBC driver does it. MySQL Connector/J's <c>ResultSet.findColumn</c> takes a column's label,
/// or, for <c>table.column</c>, the column of that name from the table of that alias (<c>Field.getFullName</c>), so Java's DAOs
/// read <c>rs.getInt("l.rank_pos")</c> and tell <c>p.name</c> from <c>l.name</c>. MySqlConnector's <c>GetOrdinal</c> knows only
/// labels and throws for <c>l.rank_pos</c>.
/// </summary>
public static class DatabaseColumns
{
	/// <summary>The ordinal of <paramref name="label"/>: a column of that label, else the column named after the dot from the
	/// table aliased before it; otherwise the reader's own <c>GetOrdinal</c> (which throws, as Connector/J does).</summary>
	public static int Ordinal(DbDataReader reader, string label)
	{
		for (int i = 0; i < reader.FieldCount; i++)
		{
			if (string.Equals(reader.GetName(i), label, StringComparison.OrdinalIgnoreCase))
				return i;
		}
		int dot = label.IndexOf('.');
		if (dot > 0 && reader.CanGetColumnSchema())
		{
			string table = label[..dot], column = label[(dot + 1)..];
			foreach (DbColumn candidate in reader.GetColumnSchema())
			{
				string? alias = candidate is MySqlDbColumn mysql ? mysql.TableName : candidate.BaseTableName;
				if (candidate.ColumnOrdinal is int ordinal && string.Equals(alias, table, StringComparison.OrdinalIgnoreCase) &&
					string.Equals(candidate.ColumnName, column, StringComparison.OrdinalIgnoreCase))
					return ordinal;
			}
		}
		return reader.GetOrdinal(label);
	}
}
