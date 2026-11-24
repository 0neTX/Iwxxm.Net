using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Microsoft.Data.Sqlite;

namespace Iwxxm.Net.Export.GeoPackage.Tests
{
    /// <summary>Helpers to create a throwaway SQLite file and to read values back from it with plain SQL.</summary>
    internal static class GeoPackageTestDatabase
    {
        /// <summary>A path in the temp directory that does not exist yet (the file is created by the test).</summary>
        internal static string NewPath(string extension = ".gpkg") =>
            Path.Combine(Path.GetTempPath(), "iwxxm-gpkg-test-" + Guid.NewGuid().ToString("N") + extension);

        /// <summary>The first column of the first row as text, or <c>null</c> for no row or a SQL NULL.</summary>
        internal static string? Scalar(SqliteConnection connection, string sql)
        {
            using (SqliteCommand command = connection.CreateCommand())
            {
                command.CommandText = sql;
                object? value = command.ExecuteScalar();
                return value == null || value is DBNull ? null : Convert.ToString(value, CultureInfo.InvariantCulture);
            }
        }

        /// <summary>Every row of <paramref name="sql"/>, each column as text (<c>null</c> for SQL NULL, hex for a blob).</summary>
        internal static List<string?[]> Rows(SqliteConnection connection, string sql)
        {
            var rows = new List<string?[]>();
            using (SqliteCommand command = connection.CreateCommand())
            {
                command.CommandText = sql;
                using (SqliteDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var row = new string?[reader.FieldCount];
                        for (int i = 0; i < row.Length; i++)
                        {
                            object value = reader.GetValue(i);
                            row[i] = value is DBNull ? null
                                : value is byte[] bytes ? BitConverter.ToString(bytes).Replace("-", string.Empty)
                                : Convert.ToString(value, CultureInfo.InvariantCulture);
                        }

                        rows.Add(row);
                    }
                }
            }

            return rows;
        }

        /// <summary>The table names of the database, ordered, without the SQLite internal ones.</summary>
        internal static List<string> TableNames(SqliteConnection connection)
        {
            var names = new List<string>();
            foreach (string?[] row in Rows(connection, "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite\\_%' ESCAPE '\\' ORDER BY name"))
                names.Add(row[0]!);
            return names;
        }
    }
}
