// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;
using osu.Framework.Input.Bindings;
using osu.Game.Input.Bindings;
namespace osu.Game.Database
{
    /// <summary>
    /// SQLite-backed implementation of <see cref="IKeyBindingStore"/>.
    /// </summary>
    /// <remarks>
    /// This is intentionally a direct SQLite implementation rather than a general-purpose
    /// repository layer. It is a migration prototype for the key binding domain.
    /// </remarks>
    public sealed class SqliteKeyBindingStore : IKeyBindingStore, IDisposable
    {
        private readonly object sync = new object();
        private readonly SqliteConnection connection;
        private readonly List<Action> subscribers = new List<Action>();

        private bool disposed;

        public SqliteKeyBindingStore(string databasePath)
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
                CREATE TABLE IF NOT EXISTS key_bindings
                (
                    id TEXT PRIMARY KEY NOT NULL,
                    ruleset_name TEXT NULL,
                    variant INTEGER NULL,
                    action INTEGER NOT NULL,
                    key_combination TEXT NOT NULL
                );";
            command.ExecuteNonQuery();
        }

        public List<RealmKeyBinding> GetAllDetached()
        {
            lock (sync)
            {
                throwIfDisposed();

                using var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT id, ruleset_name, variant, action, key_combination
                    FROM key_bindings;";

                using SqliteDataReader reader = command.ExecuteReader();
                var result = new List<RealmKeyBinding>();

                while (reader.Read())
                    result.Add(readBinding(reader));

                return result;
            }
        }

        public void Add(RealmKeyBinding item)
        {
            List<Action> callbacks;

            lock (sync)
            {
                throwIfDisposed();

                using var command = connection.CreateCommand();
                command.CommandText = @"
                    INSERT INTO key_bindings (id, ruleset_name, variant, action, key_combination)
                    VALUES ($id, $ruleset_name, $variant, $action, $key_combination);";
                bind(command, item);
                command.ExecuteNonQuery();
                callbacks = getSubscribers();
            }

            notify(callbacks);
        }

        public void Update(Guid id, Action<RealmKeyBinding> update)
        {
            List<Action>? callbacks = null;

            lock (sync)
            {
                throwIfDisposed();

                RealmKeyBinding? item = find(id);
                if (item == null)
                    return;

                update(item);

                using var command = connection.CreateCommand();
                command.CommandText = @"
                    UPDATE key_bindings
                    SET ruleset_name = $ruleset_name,
                        variant = $variant,
                        action = $action,
                        key_combination = $key_combination
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
                throwIfDisposed();

                using var command = connection.CreateCommand();
                command.CommandText = "DELETE FROM key_bindings WHERE id = $id;";
                command.Parameters.AddWithValue("$id", id.ToString());
                deleted = command.ExecuteNonQuery() > 0;

                if (deleted)
                    callbacks = getSubscribers();
            }

            if (callbacks != null)
                notify(callbacks);

            return deleted;
        }

        public IDisposable Subscribe(Action onChanged)
        {
            lock (sync)
            {
                throwIfDisposed();
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

        private RealmKeyBinding? find(Guid id)
        {
            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT id, ruleset_name, variant, action, key_combination
                FROM key_bindings
                WHERE id = $id;";
            command.Parameters.AddWithValue("$id", id.ToString());

            using SqliteDataReader reader = command.ExecuteReader();
            return reader.Read() ? readBinding(reader) : null;
        }

        private static RealmKeyBinding readBinding(SqliteDataReader reader)
        {
            var binding = new RealmKeyBinding(0, new KeyCombination(InputKey.None))
            {
                ID = Guid.Parse(reader.GetString(0)),
                RulesetName = reader.IsDBNull(1) ? null : reader.GetString(1),
                Variant = reader.IsDBNull(2) ? null : reader.GetInt32(2),
                ActionInt = reader.GetInt32(3),
                KeyCombinationString = reader.GetString(4)
            };
            return binding;
        }

        private static void bind(SqliteCommand command, RealmKeyBinding item)
        {
            command.Parameters.AddWithValue("$id", item.ID.ToString());
            command.Parameters.AddWithValue("$ruleset_name", (object?)item.RulesetName ?? DBNull.Value);
            command.Parameters.AddWithValue("$variant", (object?)item.Variant ?? DBNull.Value);
            command.Parameters.AddWithValue("$action", item.ActionInt);
            command.Parameters.AddWithValue("$key_combination", item.KeyCombinationString);
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

        private void throwIfDisposed()
        {
            ObjectDisposedException.ThrowIf(disposed, this);
        }

        private sealed class Subscription : IDisposable
        {
            private readonly SqliteKeyBindingStore owner;
            private readonly Action callback;

            public Subscription(SqliteKeyBindingStore owner, Action callback)
            {
                this.owner = owner;
                this.callback = callback;
            }

            public void Dispose() => owner.unsubscribe(callback);
        }
    }
}
