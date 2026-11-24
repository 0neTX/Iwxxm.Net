using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Microsoft.Data.Sqlite;
using static Iwxxm.Net.Export.GeoPackage.Tests.GeoPackageTestDatabase;

namespace Iwxxm.Net.Export.GeoPackage.Tests
{
    /// <summary>
    /// A text form of a whole GeoPackage that is the same for the same content on every machine: it holds nothing that
    /// depends on the SQLite version or on the clock (not the file header, no page counts), and writes every geometry as
    /// well-known text instead of a blob. It is what the golden files under <c>Golden/</c> contain.
    /// </summary>
    /// <remarks>
    /// Layout: the two header pragmas and the integrity check, one tab-separated line per schema object (<c>sqlite_master</c>
    /// ordered by type and name, with its DDL), then for every table, ordered by name, a heading with its columns and one line per row
    /// in rowid order with tab-separated values. NULL is <c>NULL</c>, reals are written with 17 significant digits, and a geometry column holds the WKT with 17
    /// significant digits followed by <c>@</c> and the envelope of the blob header (<c>minx maxx miny maxy</c>), decoded by
    /// the independent reader of the tests, never by the encoder.
    /// </remarks>
    internal static class GeoPackageCanonicalDump
    {
        internal const string NumberFormat = "G17";

        internal static string Dump(SqliteConnection connection)
        {
            var dump = new StringBuilder();
            dump.Append("application_id=").Append(Scalar(connection, "PRAGMA application_id")).Append('\n');
            dump.Append("user_version=").Append(Scalar(connection, "PRAGMA user_version")).Append('\n');
            dump.Append("integrity_check=").Append(Scalar(connection, "PRAGMA integrity_check")).Append('\n');

            dump.Append("# schema\n");
            foreach (string?[] schema in Rows(connection, "SELECT type, name, tbl_name, sql FROM sqlite_master ORDER BY type, name"))
                dump.Append(string.Join("\t", Array.ConvertAll(schema, v => v == null ? "NULL" : Escape(v)))).Append('\n');

            // The geometry columns, from the table that registers them (not from the column names).
            var geometryColumns = new HashSet<string>(StringComparer.Ordinal);
            foreach (string?[] row in Rows(connection, "SELECT table_name, column_name FROM gpkg_geometry_columns"))
                geometryColumns.Add(row[0] + "." + row[1]);

            foreach (string table in TableNames(connection))
            {
                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = $"SELECT * FROM \"{table}\" ORDER BY rowid";
                    using (SqliteDataReader reader = command.ExecuteReader())
                    {
                        var names = new string[reader.FieldCount];
                        for (int i = 0; i < names.Length; i++)
                            names[i] = reader.GetName(i);

                        dump.Append("# ").Append(table).Append(": ").Append(string.Join(", ", names)).Append('\n');
                        while (reader.Read())
                        {
                            for (int i = 0; i < names.Length; i++)
                            {
                                if (i > 0)
                                    dump.Append('\t');
                                dump.Append(Value(reader.GetValue(i), geometryColumns.Contains(table + "." + names[i])));
                            }

                            dump.Append('\n');
                        }
                    }
                }
            }

            return dump.ToString();
        }

        private static string Value(object value, bool isGeometry)
        {
            switch (value)
            {
                case DBNull _:
                    return "NULL";
                case byte[] blob when isGeometry:
                    var geometry = GeoPackageGeometryEncoderTests.Decode(blob, out var envelope);
                    string wkt = TestWkt.Format(geometry, NumberFormat);
                    return envelope == null ? wkt : wkt + " @ " + string.Join(" ", TestWkt.Number(envelope.Value.MinX, NumberFormat), TestWkt.Number(envelope.Value.MaxX, NumberFormat), TestWkt.Number(envelope.Value.MinY, NumberFormat), TestWkt.Number(envelope.Value.MaxY, NumberFormat));
                case byte[] blob:
                    return "x'" + BitConverter.ToString(blob).Replace("-", string.Empty) + "'";
                case double real:
                    return TestWkt.Number(real, NumberFormat);
                case string text:
                    return Escape(text);
                default:
                    return Convert.ToString(value, CultureInfo.InvariantCulture)!;
            }
        }

        // One value per tab-separated field and one row per line: the characters that would break that are written escaped.
        private static string Escape(string text) =>
            text.Replace("\\", "\\\\").Replace("\t", "\\t").Replace("\r", "\\r").Replace("\n", "\\n");
    }
}
