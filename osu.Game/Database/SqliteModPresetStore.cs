// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Database
{
    /// <summary>
    /// SQLite-backed implementation of <see cref="IModPresetStore"/>.
    /// </summary>
    /// <remarks>
    /// Migration prototype mirroring <see cref="SqliteKeyBindingStore"/>; direct SQLite,
    /// not a general repository layer. The <see cref="ModPreset.Ruleset"/> association is
    /// denormalised into the preset row (its <see cref="RulesetInfo.InstantiationInfo"/> is
    /// required to deserialise <see cref="ModPreset.Mods"/>).
    /// </remarks>
    public sealed class SqliteModPresetStore : IModPresetStore, IDisposable
    {
        private readonly object sync = new object();
        private readonly SqliteConnection connection;
        private readonly RulesetStore rulesets;
        private readonly List<Action> subscribers = new List<Action>();

        private bool disposed;

        public SqliteModPresetStore(string databasePath, RulesetStore rulesets)
        {
            this.rulesets = rulesets;

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
                CREATE TABLE IF NOT EXISTS mod_presets
                (
                    id TEXT PRIMARY KEY NOT NULL,
                    ruleset_short_name TEXT NOT NULL,
                    ruleset_name TEXT NOT NULL,
                    ruleset_instantiation_info TEXT NOT NULL,
                    ruleset_online_id INTEGER NOT NULL,
                    name TEXT NOT NULL,
                    description TEXT NOT NULL,
                    mods TEXT NOT NULL,
                    delete_pending INTEGER NOT NULL
                );";
            command.ExecuteNonQuery();
        }

        public ModPreset? GetDetached(Guid id)
        {
            lock (sync)
            {
                ThrowIfDisposed();
                return find(id);
            }
        }

        public List<ModPreset> GetAllDetached()
        {
            lock (sync)
            {
                ThrowIfDisposed();
                return query(null);
            }
        }

        public List<ModPreset> GetAllUsableDetached()
        {
            lock (sync)
            {
                ThrowIfDisposed();
                return query("WHERE delete_pending = 0");
            }
        }

        public void Add(ModPreset item)
        {
            List<Action> callbacks;

            lock (sync)
            {
                ThrowIfDisposed();

                using var command = connection.CreateCommand();
                command.CommandText = @"
                    INSERT INTO mod_presets
                        (id, ruleset_short_name, ruleset_name, ruleset_instantiation_info, ruleset_online_id,
                         name, description, mods, delete_pending)
                    VALUES
                        ($id, $ruleset_short_name, $ruleset_name, $ruleset_instantiation_info, $ruleset_online_id,
                         $name, $description, $mods, $delete_pending);";
                bind(command, item);
                command.ExecuteNonQuery();
                callbacks = getSubscribers();
            }

            notify(callbacks);
        }

        public void Update(Guid id, Action<ModPreset> update)
        {
            List<Action>? callbacks = null;

            lock (sync)
            {
                ThrowIfDisposed();

                ModPreset? item = find(id);
                if (item == null)
                    return;

                update(item);

                using var command = connection.CreateCommand();
                command.CommandText = @"
                    UPDATE mod_presets
                    SET ruleset_short_name = $ruleset_short_name,
                        ruleset_name = $ruleset_name,
                        ruleset_instantiation_info = $ruleset_instantiation_info,
                        ruleset_online_id = $ruleset_online_id,
                        name = $name,
                        description = $description,
                        mods = $mods,
                        delete_pending = $delete_pending
                    WHERE id = $id;";
                bind(command, item);
                command.ExecuteNonQuery();
                callbacks = getSubscribers();
            }

            notify(callbacks);
        }

        public bool Delete(Guid id)
        {
            List<Action>? callbacks = null;
            bool deleted;

            lock (sync)
            {
                ThrowIfDisposed();

                using var command = connection.CreateCommand();
                command.CommandText = @"
                    UPDATE mod_presets
                    SET delete_pending = 1
                    WHERE id = $id AND delete_pending = 0;";
                command.Parameters.AddWithValue("$id", id.ToString());
                deleted = command.ExecuteNonQuery() > 0;

                if (deleted)
                    callbacks = getSubscribers();
            }

            if (callbacks != null)
                notify(callbacks);

            return deleted;
        }

        public bool Undelete(Guid id)
        {
            List<Action>? callbacks = null;
            bool restored;

            lock (sync)
            {
                ThrowIfDisposed();

                using var command = connection.CreateCommand();
                command.CommandText = @"
                    UPDATE mod_presets
                    SET delete_pending = 0
                    WHERE id = $id AND delete_pending = 1;";
                command.Parameters.AddWithValue("$id", id.ToString());
                restored = command.ExecuteNonQuery() > 0;

                if (restored)
                    callbacks = getSubscribers();
            }

            if (callbacks != null)
                notify(callbacks);

            return restored;
        }

        public IDisposable Subscribe(Action onChanged)
        {
            lock (sync)
            {
                ThrowIfDisposed();
                subscribers.Add(onChanged);
            }

            // Match the domain contract: subscriptions are invalidation hints and begin
            // with an initial snapshot notification.
            onChanged();
            return new Subscription(this, onChanged);
        }

        public void Dispose()
        {
            lock (sync)
            {
                if (disposed)
                    return;

                disposed = true;
                subscribers.Clear();
                connection.Dispose();
            }
        }

        private List<ModPreset> query(string? whereClause)
        {
            using var command = connection.CreateCommand();
            command.CommandText = $@"
                SELECT id, ruleset_short_name, ruleset_name, ruleset_instantiation_info, ruleset_online_id,
                       name, description, mods, delete_pending
                FROM mod_presets
                {whereClause};";

            using SqliteDataReader reader = command.ExecuteReader();
            var result = new List<ModPreset>();

            while (reader.Read())
                result.Add(readPreset(reader));

            return result;
        }

        private ModPreset? find(Guid id)
        {
            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT id, ruleset_short_name, ruleset_name, ruleset_instantiation_info, ruleset_online_id,
                       name, description, mods, delete_pending
                FROM mod_presets
                WHERE id = $id;";
            command.Parameters.AddWithValue("$id", id.ToString());

            using SqliteDataReader reader = command.ExecuteReader();
            return reader.Read() ? readPreset(reader) : null;
        }

        private ModPreset readPreset(SqliteDataReader reader)
        {
            string rulesetShortName = reader.GetString(1);

            // Rebuild the full ruleset association from the ruleset store so that
            // Mods deserialisation (which requires a live ruleset instance) works.
            var ruleset = rulesets.GetRuleset(rulesetShortName)
                          ?? throw new InvalidOperationException($"Mod preset references unknown ruleset \"{rulesetShortName}\".");

            return new ModPreset
            {
                ID = Guid.Parse(reader.GetString(0)),
                Ruleset = ruleset,
                Name = reader.GetString(5),
                Description = reader.GetString(6),
                ModsJson = reader.GetString(7),
                DeletePending = reader.GetInt32(8) != 0
            };
        }

        private static void bind(SqliteCommand command, ModPreset item)
        {
            command.Parameters.AddWithValue("$id", item.ID.ToString());
            command.Parameters.AddWithValue("$ruleset_short_name", item.Ruleset.ShortName);
            command.Parameters.AddWithValue("$ruleset_name", item.Ruleset.Name);
            command.Parameters.AddWithValue("$ruleset_instantiation_info", item.Ruleset.InstantiationInfo);
            command.Parameters.AddWithValue("$ruleset_online_id", item.Ruleset.OnlineID);
            command.Parameters.AddWithValue("$name", item.Name);
            command.Parameters.AddWithValue("$description", item.Description);
            command.Parameters.AddWithValue("$mods", item.ModsJson);
            command.Parameters.AddWithValue("$delete_pending", item.DeletePending ? 1 : 0);
        }

        private List<Action> getSubscribers() => new List<Action>(subscribers);

        private void notify(IEnumerable<Action> callbacks)
        {
            foreach (Action callback in callbacks)
                callback();
        }

        private void unsubscribe(Action callback)
        {
            lock (sync)
                subscribers.Remove(callback);
        }

        private void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(disposed, this);
        }

        private sealed class Subscription : IDisposable
        {
            private readonly SqliteModPresetStore owner;
            private readonly Action callback;

            public Subscription(SqliteModPresetStore owner, Action callback)
            {
                this.owner = owner;
                this.callback = callback;
            }

            public void Dispose() => owner.unsubscribe(callback);
        }
    }
}
