// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;
using osu.Game.Models;
using osu.Game.Skinning;

namespace osu.Game.Database
{
    /// <summary>
    /// SQLite-backed implementation of <see cref="ISkinStore"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Mirrors the other SQLite stores; direct SQLite, not a general repository layer.
    /// Every <see cref="SkinInfo"/> scalar has an explicit column, with the ordered named-file
    /// references stored in a child table.
    /// </para>
    /// <para>
    /// File contents are not owned by this store. Callers write them under
    /// <see cref="FileStorageDirectory"/> and this store only persists the resulting hashes.
    /// Keeping skin content out of the Realm-managed <c>files</c> directory means Realm's file
    /// cleanup cannot remove it.
    /// </para>
    /// </remarks>
    public sealed class SqliteSkinStore : ISkinStore, IDisposable
    {
        private const string skin_columns = @"id, name, creator, instantiation_info, hash, is_protected, delete_pending";

        private const string skin_parameters = @"$id, $name, $creator, $instantiation_info, $hash, $is_protected, $delete_pending";

        private readonly object sync = new object();
        private readonly SqliteConnection connection;
        private readonly List<Action> subscribers = new List<Action>();

        private bool disposed;

        public string FileStorageDirectory => @"skin-files";

        public SqliteSkinStore(string databasePath)
        {
            ArgumentNullException.ThrowIfNull(databasePath);

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
                CREATE TABLE IF NOT EXISTS skins
                (
                    id TEXT PRIMARY KEY NOT NULL,
                    name TEXT NOT NULL,
                    creator TEXT NOT NULL,
                    instantiation_info TEXT NOT NULL,
                    hash TEXT NOT NULL,
                    is_protected INTEGER NOT NULL,
                    delete_pending INTEGER NOT NULL
                );

                CREATE INDEX IF NOT EXISTS idx_skins_hash ON skins (hash);

                CREATE TABLE IF NOT EXISTS skin_files
                (
                    skin_id TEXT NOT NULL,
                    ordinal INTEGER NOT NULL,
                    file_hash TEXT NOT NULL,
                    filename TEXT NOT NULL,
                    PRIMARY KEY (skin_id, ordinal)
                );";
            command.ExecuteNonQuery();
        }

        public SkinInfo? GetDetached(Guid id)
        {
            lock (sync)
            {
                throwIfDisposed();
                return find(id);
            }
        }

        public List<SkinInfo> GetAllDetached()
        {
            lock (sync)
            {
                throwIfDisposed();
                return query(null);
            }
        }

        public List<SkinInfo> GetAllUsableDetached()
        {
            lock (sync)
            {
                throwIfDisposed();
                return query("WHERE delete_pending = 0 AND is_protected = 0");
            }
        }

        public SkinInfo? FindByHash(string hash)
        {
            if (string.IsNullOrEmpty(hash))
                return null;

            lock (sync)
            {
                throwIfDisposed();

                // Deleted skins are included so the importer can reuse and restore an existing
                // row; active skins are preferred via the ordering.
                return query("WHERE hash = $hash",
                    command => command.Parameters.AddWithValue("$hash", hash),
                    "ORDER BY delete_pending, rowid", 1).FirstOrDefault();
            }
        }

        public void Add(SkinInfo item)
        {
            ArgumentNullException.ThrowIfNull(item);

            List<Action> callbacks;

            lock (sync)
            {
                throwIfDisposed();

                using SqliteTransaction transaction = connection.BeginTransaction();

                using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction;
                    command.CommandText = $"INSERT INTO skins ({skin_columns}) VALUES ({skin_parameters});";
                    bindSkin(command, item);
                    command.ExecuteNonQuery();
                }

                insertFiles(transaction, item);

                transaction.Commit();
                callbacks = getSubscribers();
            }

            notify(callbacks);
        }

        public void Update(Guid id, Action<SkinInfo> update)
        {
            ArgumentNullException.ThrowIfNull(update);

            List<Action> callbacks;

            lock (sync)
            {
                throwIfDisposed();

                SkinInfo? item = find(id);

                if (item == null)
                    return;

                // Invoke before opening the transaction so a throwing mutation leaves storage untouched.
                update(item);

                // The primary key identifies the row; keep it stable even if the callback changed it.
                item.ID = id;

                using SqliteTransaction transaction = connection.BeginTransaction();

                using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction;
                    command.CommandText = @"
                        UPDATE skins
                        SET name = $name,
                            creator = $creator,
                            instantiation_info = $instantiation_info,
                            hash = $hash,
                            is_protected = $is_protected,
                            delete_pending = $delete_pending
                        WHERE id = $id;";
                    bindSkin(command, item);
                    command.ExecuteNonQuery();
                }

                deleteFiles(transaction, id);
                insertFiles(transaction, item);

                transaction.Commit();
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
                command.CommandText = @"
                    UPDATE skins
                    SET delete_pending = 1
                    WHERE id = $id AND delete_pending = 0 AND is_protected = 0;";
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
                throwIfDisposed();

                using var command = connection.CreateCommand();
                command.CommandText = @"
                    UPDATE skins
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
            ArgumentNullException.ThrowIfNull(onChanged);

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

        /// <summary>
        /// Loads skins matching <paramref name="whereClause"/>, together with their file
        /// references. Must only be called while holding <see cref="sync"/>.
        /// </summary>
        private List<SkinInfo> query(string? whereClause, Action<SqliteCommand>? bind = null, string orderBy = "ORDER BY rowid", int? limit = null)
        {
            var skins = new List<SkinInfo>();

            using (var command = connection.CreateCommand())
            {
                string limitClause = limit == null ? string.Empty : $" LIMIT {limit.Value}";
                command.CommandText = $"SELECT {skin_columns} FROM skins {whereClause} {orderBy}{limitClause};";
                bind?.Invoke(command);

                using SqliteDataReader reader = command.ExecuteReader();

                while (reader.Read())
                    skins.Add(readSkin(reader));
            }

            if (skins.Count == 0)
                return skins;

            Dictionary<Guid, SkinInfo> skinsById = skins.ToDictionary(skin => skin.ID);

            loadFiles(whereClause, bind, skinsById);

            return skins;
        }

        /// <summary>
        /// Loads the skin with the given primary key. Must only be called while holding
        /// <see cref="sync"/>.
        /// </summary>
        private SkinInfo? find(Guid id)
            => query("WHERE id = $id", command => command.Parameters.AddWithValue("$id", id.ToString()), limit: 1).FirstOrDefault();

        private void loadFiles(string? whereClause, Action<SqliteCommand>? bind, Dictionary<Guid, SkinInfo> skins)
        {
            string filter = whereClause == null ? string.Empty : $"WHERE skin_id IN (SELECT id FROM skins {whereClause})";

            using var command = connection.CreateCommand();
            command.CommandText = $@"
                SELECT skin_id, file_hash, filename
                FROM skin_files
                {filter}
                ORDER BY skin_id, ordinal;";
            bind?.Invoke(command);

            using SqliteDataReader reader = command.ExecuteReader();

            while (reader.Read())
            {
                if (!skins.TryGetValue(Guid.Parse(reader.GetString(0)), out SkinInfo? skin))
                    continue;

                skin.Files.Add(new RealmNamedFileUsage(new RealmFile { Hash = reader.GetString(1) }, reader.GetString(2)));
            }
        }

        private static SkinInfo readSkin(SqliteDataReader reader)
        {
            return new SkinInfo
            {
                ID = Guid.Parse(reader.GetString(0)),
                Name = reader.GetString(1),
                Creator = reader.GetString(2),
                InstantiationInfo = reader.GetString(3),
                Hash = reader.GetString(4),
                Protected = reader.GetInt32(5) != 0,
                DeletePending = reader.GetInt32(6) != 0
            };
        }

        private static void bindSkin(SqliteCommand command, SkinInfo item)
        {
            command.Parameters.AddWithValue("$id", item.ID.ToString());
            command.Parameters.AddWithValue("$name", item.Name);
            command.Parameters.AddWithValue("$creator", item.Creator);
            command.Parameters.AddWithValue("$instantiation_info", item.InstantiationInfo);
            command.Parameters.AddWithValue("$hash", item.Hash);
            command.Parameters.AddWithValue("$is_protected", item.Protected ? 1 : 0);
            command.Parameters.AddWithValue("$delete_pending", item.DeletePending ? 1 : 0);
        }

        private void insertFiles(SqliteTransaction transaction, SkinInfo item)
        {
            for (int i = 0; i < item.Files.Count; i++)
            {
                RealmNamedFileUsage usage = item.Files[i];

                using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = @"
                    INSERT INTO skin_files (skin_id, ordinal, file_hash, filename)
                    VALUES ($skin_id, $ordinal, $file_hash, $filename);";
                command.Parameters.AddWithValue("$skin_id", item.ID.ToString());
                command.Parameters.AddWithValue("$ordinal", i);
                command.Parameters.AddWithValue("$file_hash", usage.File.Hash);
                command.Parameters.AddWithValue("$filename", usage.Filename);
                command.ExecuteNonQuery();
            }
        }

        private void deleteFiles(SqliteTransaction transaction, Guid id)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = @"DELETE FROM skin_files WHERE skin_id = $skin_id;";
            command.Parameters.AddWithValue("$skin_id", id.ToString());
            command.ExecuteNonQuery();
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
            private readonly SqliteSkinStore owner;
            private readonly Action callback;

            public Subscription(SqliteSkinStore owner, Action callback)
            {
                this.owner = owner;
                this.callback = callback;
            }

            public void Dispose() => owner.unsubscribe(callback);
        }
    }
}
