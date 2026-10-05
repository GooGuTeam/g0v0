// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;

namespace osu.Game.Database
{
    /// <summary>
    /// SQLite-backed implementation of <see cref="IRulesetSettingStore"/>.
    /// </summary>
    /// <remarks>
    /// Settings are keyed by the (ruleset, variant, key) composite; no identity of their own.
    /// </remarks>
    public sealed class SqliteRulesetSettingStore : IRulesetSettingStore, IDisposable
    {
        private readonly object sync = new object();
        private readonly SqliteConnection connection;

        private bool disposed;

        public SqliteRulesetSettingStore(string databasePath)
        {
            string? directory = Path.GetDirectoryName(databasePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            connection = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = databasePath,
                Mode = SqliteOpenMode.ReadWriteCreate
            }.ToString());
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS ruleset_settings
                (
                    ruleset_name TEXT NOT NULL,
                    variant INTEGER NOT NULL,
                    key TEXT NOT NULL,
                    value TEXT NOT NULL,
                    PRIMARY KEY (ruleset_name, variant, key)
                );";
            command.ExecuteNonQuery();
        }

        public string? GetValue(string rulesetName, int variant, string key)
        {
            lock (sync)
            {
                throwIfDisposed();

                using var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT value
                    FROM ruleset_settings
                    WHERE ruleset_name = $ruleset_name AND variant = $variant AND key = $key;";
                bindKey(command, rulesetName, variant, key);

                object? result = command.ExecuteScalar();
                return result as string;
            }
        }

        public List<(string Key, string Value)> GetAll(string rulesetName, int variant)
        {
            lock (sync)
            {
                throwIfDisposed();

                using var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT key, value
                    FROM ruleset_settings
                    WHERE ruleset_name = $ruleset_name AND variant = $variant;";
                command.Parameters.AddWithValue("$ruleset_name", rulesetName);
                command.Parameters.AddWithValue("$variant", variant);

                using SqliteDataReader reader = command.ExecuteReader();
                var result = new List<(string, string)>();

                while (reader.Read())
                    result.Add((reader.GetString(0), reader.GetString(1)));

                return result;
            }
        }

        public void SetValue(string rulesetName, int variant, string key, string value)
        {
            lock (sync)
            {
                throwIfDisposed();

                using var command = connection.CreateCommand();
                command.CommandText = @"
                    INSERT INTO ruleset_settings (ruleset_name, variant, key, value)
                    VALUES ($ruleset_name, $variant, $key, $value)
                    ON CONFLICT (ruleset_name, variant, key)
                    DO UPDATE SET value = $value;";
                bindKey(command, rulesetName, variant, key);
                command.Parameters.AddWithValue("$value", value);
                command.ExecuteNonQuery();
            }
        }

        public bool Delete(string rulesetName, int variant, string key)
        {
            lock (sync)
            {
                throwIfDisposed();

                using var command = connection.CreateCommand();
                command.CommandText = @"
                    DELETE FROM ruleset_settings
                    WHERE ruleset_name = $ruleset_name AND variant = $variant AND key = $key;";
                bindKey(command, rulesetName, variant, key);
                return command.ExecuteNonQuery() > 0;
            }
        }

        public void Dispose()
        {
            lock (sync)
            {
                if (disposed)
                    return;

                disposed = true;
                connection.Dispose();
            }
        }

        private static void bindKey(SqliteCommand command, string rulesetName, int variant, string key)
        {
            command.Parameters.AddWithValue("$ruleset_name", rulesetName);
            command.Parameters.AddWithValue("$variant", variant);
            command.Parameters.AddWithValue("$key", key);
        }

        private void throwIfDisposed()
        {
            ObjectDisposedException.ThrowIf(disposed, this);
        }
    }
}
