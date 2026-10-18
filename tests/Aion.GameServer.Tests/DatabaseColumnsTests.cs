using System.Collections;
using System.Collections.ObjectModel;
using System.Data.Common;
using Aion.Commons.Database;

namespace Aion.GameServer.Tests;

/// <summary>
/// Java's DAOs read table-qualified labels (<c>rs.getInt("l.rank_pos")</c>, <c>rs.getString("p.name")</c>), which MySQL
/// Connector/J's <c>ResultSet.findColumn</c> resolves by the table's alias; MySqlConnector's <c>GetOrdinal</c> threw for them, so
/// the daily abyss ranking logged an error and lost the legion list as soon as a legion was ranked.
/// </summary>
public sealed class DatabaseColumnsTests
{
	// AbyssRankDAO's SELECT_RANKING_LIST_PLAYERS: two columns labelled "name", one from p and one from l.
	private static readonly (string Label, string Table)[] RankingListPlayers =
	[
		("rank_pos", "a"), ("old_rank_pos", "a"), ("id", "p"), ("name", "p"), ("race", "p"), ("exp", "p"), ("rank", "a"), ("ap", "a"),
		("gp", "a"), ("title_id", "p"), ("player_class", "p"), ("gender", "p"), ("name", "l"),
	];

	[Fact]
	public void TableQualifiedLabelsResolveByTheTablesAliasAsConnectorJDoes()
	{
		using var reader = new SchemaReader(RankingListPlayers);
		Assert.Equal(0, DatabaseColumns.Ordinal(reader, "a.rank_pos"));
		Assert.Equal(3, DatabaseColumns.Ordinal(reader, "p.name"));
		Assert.Equal(12, DatabaseColumns.Ordinal(reader, "l.name"));
		Assert.Equal(10, DatabaseColumns.Ordinal(reader, "P.Player_Class"));
		// A plain label is the label, as before; an unknown column still throws.
		Assert.Equal(8, DatabaseColumns.Ordinal(reader, "gp"));
		Assert.Throws<IndexOutOfRangeException>(() => DatabaseColumns.Ordinal(reader, "l.rank_pos"));
	}

	private sealed class Column : DbColumn
	{
		public Column(string name, string table, int ordinal)
		{
			ColumnName = name;
			BaseTableName = table;
			ColumnOrdinal = ordinal;
		}
	}

	/// <summary>A reader with labels and the tables (aliases) they come from, and no rows.</summary>
	private sealed class SchemaReader((string Label, string Table)[] columns) : DbDataReader, IDbColumnSchemaGenerator
	{
		public ReadOnlyCollection<DbColumn> GetColumnSchema() =>
			columns.Select((column, index) => (DbColumn)new Column(column.Label, column.Table, index)).ToList().AsReadOnly();

		public override int FieldCount => columns.Length;
		public override string GetName(int ordinal) => columns[ordinal].Label;
		public override int GetOrdinal(string name)
		{
			int index = Array.FindIndex(columns, column => string.Equals(column.Label, name, StringComparison.OrdinalIgnoreCase));
			return index >= 0 ? index : throw new IndexOutOfRangeException($"The column name '{name}' does not exist in the result set.");
		}

		public override object this[int ordinal] => throw new NotSupportedException();
		public override object this[string name] => throw new NotSupportedException();
		public override int Depth => 0;
		public override bool HasRows => false;
		public override bool IsClosed => false;
		public override int RecordsAffected => 0;
		public override bool GetBoolean(int ordinal) => throw new NotSupportedException();
		public override byte GetByte(int ordinal) => throw new NotSupportedException();
		public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length) => throw new NotSupportedException();
		public override char GetChar(int ordinal) => throw new NotSupportedException();
		public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length) => throw new NotSupportedException();
		public override string GetDataTypeName(int ordinal) => throw new NotSupportedException();
		public override DateTime GetDateTime(int ordinal) => throw new NotSupportedException();
		public override decimal GetDecimal(int ordinal) => throw new NotSupportedException();
		public override double GetDouble(int ordinal) => throw new NotSupportedException();
		public override IEnumerator GetEnumerator() => throw new NotSupportedException();
		public override Type GetFieldType(int ordinal) => throw new NotSupportedException();
		public override float GetFloat(int ordinal) => throw new NotSupportedException();
		public override Guid GetGuid(int ordinal) => throw new NotSupportedException();
		public override short GetInt16(int ordinal) => throw new NotSupportedException();
		public override int GetInt32(int ordinal) => throw new NotSupportedException();
		public override long GetInt64(int ordinal) => throw new NotSupportedException();
		public override string GetString(int ordinal) => throw new NotSupportedException();
		public override object GetValue(int ordinal) => throw new NotSupportedException();
		public override int GetValues(object[] values) => throw new NotSupportedException();
		public override bool IsDBNull(int ordinal) => throw new NotSupportedException();
		public override bool NextResult() => false;
		public override bool Read() => false;
	}
}
