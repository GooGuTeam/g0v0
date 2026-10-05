// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;
using osu.Game.Collections;

namespace osu.Game.Database
{
    /// <summary>
    /// SQLite-backed implementation of <see cref="IBeatmapCollectionStore"/>.
    /// </summary>
    /// <remarks>
    /// Collections are stored as one row per collection; the contained beatmap hashes are
    /// serialised as a newline-separated list in a single column (hashes are always 32 hex
    /// characters and cannot contain newlines).
    /// </remarks>
    public sealed class SqliteBeatmapCollectionStore : IBeatmapCollectionStore, IDisposable
    {
        private const string hash_separator = "\n";

        private readonly object sync = new object();
        private readonly SqliteConnection connection;
        private readonly List<Action> subscribers = new List<Action>();

        private bool disposed;

        public SqliteBeatmapCollectionStore(string databasePath)
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
                CREATE TABLE IF NOT EXISTS beatmap_collections
                (
                    id TEXT NOT NULL PRIMARY KEY,
                    name TEXT NOT NULL,
                    beatmap_md5_hashes TEXT NOT NULL,
                    last_modified TEXT NOT NULL
                );";
            command.ExecuteNonQuery();
        }

        public BeatmapCollection? GetDetached(Guid id)
        {
            lock (sync)
            {
                ThrowIfDisposed();
                return find(id);
            }
        }

        public List<BeatmapCollection> GetAllDetached()
        {
            lock (sync)
            {
                ThrowIfDisposed();
                return query(null);
            }
        }

        public BeatmapCollection? FindByName(string name)
        {
            lock (sync)
            {
                ThrowIfDisposed();

                using var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT id, name, beatmap_md5_hashes, last_modified
                    FROM beatmap_collections
                    WHERE name = $name;";
                command.Parameters.AddWithValue("$name", name);

                using SqliteDataReader reader = command.ExecuteReader();
                return reader.Read() ? readCollection(reader) : null;
            }
        }

        public void Add(BeatmapCollection item)
        {
            List<Action> callbacks;

            lock (sync)
            {
                ThrowIfDisposed();

                using var command = connection.CreateCommand();
                command.CommandText = @"
                    INSERT INTO beatmap_collections
                        (id, name, beatmap_md5_hashes, last_modified)
                    VALUES
                        ($id, $name, $beatmap_md5_hashes, $last_modified);";
                bind(command, item);
                command.ExecuteNonQuery();
                callbacks = getSubscribers();
            }

            notify(callbacks);
        }

        public void Update(Guid id, Action<BeatmapCollection> update)
        {
            List<Action> callbacks;

            lock (sync)
            {
                ThrowIfDisposed();

                BeatmapCollection? item = find(id);
                if (item == null)
                    return;

                update(item);

                using var command = connection.CreateCommand();
                command.CommandText = @"
                    UPDATE beatmap_collections
                    SET name = $name,
                        beatmap_md5_hashes = $beatmap_md5_hashes,
                        last_modified = $last_modified
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
                    DELETE FROM beatmap_collections
                    WHERE id = $id;";
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

        private BeatmapCollection? find(Guid id)
        {
            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT id, name, beatmap_md5_hashes, last_modified
                FROM beatmap_collections
                WHERE id = $id;";
            command.Parameters.AddWithValue("$id", id.ToString());

            using SqliteDataReader reader = command.ExecuteReader();
            return reader.Read() ? readCollection(reader) : null;
        }

        private List<BeatmapCollection> query(string? whereClause)
        {
            using var command = connection.CreateCommand();
            command.CommandText = $@"
                SELECT id, name, beatmap_md5_hashes, last_modified
                FROM beatmap_collections
                {whereClause};";

            using SqliteDataReader reader = command.ExecuteReader();

            var result = new List<BeatmapCollection>();

            while (reader.Read())
                result.Add(readCollection(reader));

            return result;
        }

        private static BeatmapCollection readCollection(SqliteDataReader reader)
        {
            string hashes = reader.GetString(2);

            var collection = new BeatmapCollection(
                reader.GetString(1),
                hashes.Length == 0 ? new List<string>() : new List<string>(hashes.Split(hash_separator)))
            {
                ID = Guid.Parse(reader.GetString(0)),
                LastModified = DateTimeOffset.Parse(reader.GetString(3))
            };

            return collection;
        }

        private static void bind(SqliteCommand command, BeatmapCollection item)
        {
            command.Parameters.AddWithValue("$id", item.ID.ToString());
            command.Parameters.AddWithValue("$name", item.Name);
            command.Parameters.AddWithValue("$beatmap_md5_hashes", string.Join(hash_separator, item.BeatmapMD5Hashes));
            command.Parameters.AddWithValue("$last_modified", item.LastModified.ToString("O"));
        }

        private List<Action> getSubscribers()
        {
            return new List<Action>(subscribers);
        }

        private void notify(List<Action> callbacks)
        {
            foreach (var callback in callbacks)
            {
                try
                {
                    callback();
                }
                catch
                {
                    // Subscriber exceptions must not break store operations.
                }
            }
        }

        private void removeSubscriber(Action callback)
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
            private SqliteBeatmapCollectionStore? owner;
            private Action? callback;

            public Subscription(SqliteBeatmapCollectionStore owner, Action callback)
            {
                this.owner = owner;
                this.callback = callback;
            }

            public void Dispose()
            {
                var ownerInstance = owner;
                var callbackInstance = callback;

                owner = null;
                callback = null;

                if (ownerInstance != null && callbackInstance != null)
                    ownerInstance.removeSubscriber(callbackInstance);
            }
        }
    }
}
