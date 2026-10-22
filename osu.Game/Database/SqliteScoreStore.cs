// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;
using osu.Game.Beatmaps;
using osu.Game.Models;
using osu.Game.Rulesets;
using osu.Game.Scoring;

namespace osu.Game.Database
{
    /// <summary>
    /// SQLite-backed implementation of <see cref="IScoreStore"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Mirrors the other SQLite stores; direct SQLite, not a general repository layer.
    /// Every persisted <see cref="ScoreInfo"/> field has an explicit column, with file
    /// references and pauses stored in child tables.
    /// </para>
    /// <para>
    /// The <see cref="BeatmapInfo"/> association is not persisted as a link. Only
    /// <see cref="ScoreInfo.BeatmapHash"/> is stored, and the current local beatmap is resolved
    /// through <see cref="RealmAccess"/> on every read so edits and removals are reflected.
    /// That resolution intentionally happens outside the store's lock, as the importer may hold
    /// realm write access while calling into this store.
    /// </para>
    /// </remarks>
    public sealed class SqliteScoreStore : IScoreStore, IDisposable
    {
        private const string score_columns = @"id, beatmap_hash, client_version, score_hash, delete_pending,
                                               total_score, total_score_without_mods, total_score_version, legacy_total_score,
                                               background_reprocessing_failed, max_combo, accuracy, date, pp,
                                               online_id, legacy_online_id,
                                               ruleset_short_name, ruleset_name, ruleset_instantiation_info, ruleset_online_id,
                                               ruleset_last_applied_difficulty_version, ruleset_available,
                                               user_online_id, user_username, user_country,
                                               mods_json, statistics_json, maximum_statistics_json, rank_int, combo, is_legacy_score";

        private const string score_parameters = @"$id, $beatmap_hash, $client_version, $score_hash, $delete_pending,
                                                  $total_score, $total_score_without_mods, $total_score_version, $legacy_total_score,
                                                  $background_reprocessing_failed, $max_combo, $accuracy, $date, $pp,
                                                  $online_id, $legacy_online_id,
                                                  $ruleset_short_name, $ruleset_name, $ruleset_instantiation_info, $ruleset_online_id,
                                                  $ruleset_last_applied_difficulty_version, $ruleset_available,
                                                  $user_online_id, $user_username, $user_country,
                                                  $mods_json, $statistics_json, $maximum_statistics_json, $rank_int, $combo, $is_legacy_score";

        private readonly object sync = new object();
        private readonly RealmAccess realm;
        private readonly SqliteConnection connection;
        private readonly List<Action> subscribers = new List<Action>();

        private bool disposed;

        public string FileStorageDirectory => @"score-files";

        public SqliteScoreStore(string databasePath, RealmAccess realm)
        {
            ArgumentNullException.ThrowIfNull(databasePath);
            this.realm = realm ?? throw new ArgumentNullException(nameof(realm));

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
                CREATE TABLE IF NOT EXISTS scores
                (
                    id TEXT PRIMARY KEY NOT NULL,
                    beatmap_hash TEXT NOT NULL,
                    client_version TEXT NOT NULL,
                    score_hash TEXT NOT NULL,
                    delete_pending INTEGER NOT NULL,
                    total_score INTEGER NOT NULL,
                    total_score_without_mods INTEGER NOT NULL,
                    total_score_version INTEGER NOT NULL,
                    legacy_total_score INTEGER NULL,
                    background_reprocessing_failed INTEGER NOT NULL,
                    max_combo INTEGER NOT NULL,
                    accuracy REAL NOT NULL,
                    date TEXT NOT NULL,
                    pp REAL NULL,
                    online_id INTEGER NOT NULL,
                    legacy_online_id INTEGER NOT NULL,
                    ruleset_short_name TEXT NOT NULL,
                    ruleset_name TEXT NOT NULL,
                    ruleset_instantiation_info TEXT NOT NULL,
                    ruleset_online_id INTEGER NOT NULL,
                    ruleset_last_applied_difficulty_version INTEGER NOT NULL,
                    ruleset_available INTEGER NOT NULL,
                    user_online_id INTEGER NOT NULL,
                    user_username TEXT NOT NULL,
                    user_country TEXT NOT NULL,
                    mods_json TEXT NOT NULL,
                    statistics_json TEXT NOT NULL,
                    maximum_statistics_json TEXT NOT NULL,
                    rank_int INTEGER NOT NULL,
                    combo INTEGER NOT NULL,
                    is_legacy_score INTEGER NOT NULL
                );

                CREATE INDEX IF NOT EXISTS idx_scores_beatmap_hash ON scores (beatmap_hash);
                CREATE INDEX IF NOT EXISTS idx_scores_score_hash ON scores (score_hash);
                CREATE INDEX IF NOT EXISTS idx_scores_online_id ON scores (online_id);
                CREATE INDEX IF NOT EXISTS idx_scores_legacy_online_id ON scores (legacy_online_id);

                CREATE TABLE IF NOT EXISTS score_files
                (
                    score_id TEXT NOT NULL,
                    ordinal INTEGER NOT NULL,
                    file_hash TEXT NOT NULL,
                    filename TEXT NOT NULL,
                    PRIMARY KEY (score_id, ordinal)
                );

                CREATE TABLE IF NOT EXISTS score_pauses
                (
                    score_id TEXT NOT NULL,
                    ordinal INTEGER NOT NULL,
                    pause INTEGER NOT NULL,
                    PRIMARY KEY (score_id, ordinal)
                );";
            command.ExecuteNonQuery();
        }

        public ScoreInfo? GetDetached(Guid id)
        {
            ScoreInfo? score;

            lock (sync)
            {
                throwIfDisposed();
                score = find(id);
            }

            return hydrateBeatmap(score);
        }

        public List<ScoreInfo> GetAllDetached()
        {
            List<ScoreInfo> scores;

            lock (sync)
            {
                throwIfDisposed();
                scores = query(null);
            }

            hydrateBeatmaps(scores);
            return scores;
        }

        public ScoreInfo? FindByHash(string hash)
        {
            if (string.IsNullOrEmpty(hash))
                return null;

            ScoreInfo? score;

            lock (sync)
            {
                throwIfDisposed();

                // Deleted scores are included so the importer can reuse and restore an existing
                // row; active scores are preferred via the ordering.
                score = query("WHERE score_hash = $score_hash",
                    command => command.Parameters.AddWithValue("$score_hash", hash),
                    "ORDER BY delete_pending, rowid", 1).FirstOrDefault();
            }

            return hydrateBeatmap(score);
        }

        public ScoreInfo? FindByOnlineId(long onlineId)
        {
            ScoreInfo? score;

            lock (sync)
            {
                throwIfDisposed();
                score = query("WHERE delete_pending = 0 AND online_id = $online_id",
                    command => command.Parameters.AddWithValue("$online_id", onlineId), limit: 1).FirstOrDefault();
            }

            return hydrateBeatmap(score);
        }

        public ScoreInfo? FindByLegacyOnlineId(long onlineId)
        {
            ScoreInfo? score;

            lock (sync)
            {
                throwIfDisposed();
                score = query("WHERE delete_pending = 0 AND legacy_online_id = $legacy_online_id",
                    command => command.Parameters.AddWithValue("$legacy_online_id", onlineId), limit: 1).FirstOrDefault();
            }

            return hydrateBeatmap(score);
        }

        public List<ScoreInfo> GetForBeatmap(string beatmapHash)
        {
            if (string.IsNullOrEmpty(beatmapHash))
                return new List<ScoreInfo>();

            List<ScoreInfo> scores;

            lock (sync)
            {
                throwIfDisposed();
                scores = query("WHERE delete_pending = 0 AND beatmap_hash = $beatmap_hash",
                    command => command.Parameters.AddWithValue("$beatmap_hash", beatmapHash));
            }

            hydrateBeatmaps(scores);
            return scores;
        }

        public void Add(ScoreInfo item)
        {
            ArgumentNullException.ThrowIfNull(item);

            List<Action> callbacks;

            lock (sync)
            {
                throwIfDisposed();

                normaliseBeatmapHash(item);

                using SqliteTransaction transaction = connection.BeginTransaction();

                using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction;
                    command.CommandText = $"INSERT INTO scores ({score_columns}) VALUES ({score_parameters});";
                    bindScore(command, item);
                    command.ExecuteNonQuery();
                }

                insertFiles(transaction, item);
                insertPauses(transaction, item);

                transaction.Commit();
                callbacks = getSubscribers();
            }

            notify(callbacks);
        }

        public void Update(Guid id, Action<ScoreInfo> update)
        {
            ArgumentNullException.ThrowIfNull(update);

            List<Action> callbacks;

            lock (sync)
            {
                throwIfDisposed();

                ScoreInfo? item = find(id);

                if (item == null)
                    return;

                // Invoke before opening the transaction so a throwing mutation leaves storage untouched.
                update(item);
                normaliseBeatmapHash(item);

                using SqliteTransaction transaction = connection.BeginTransaction();

                using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction;
                    command.CommandText = @"
                        UPDATE scores
                        SET beatmap_hash = $beatmap_hash,
                            client_version = $client_version,
                            score_hash = $score_hash,
                            delete_pending = $delete_pending,
                            total_score = $total_score,
                            total_score_without_mods = $total_score_without_mods,
                            total_score_version = $total_score_version,
                            legacy_total_score = $legacy_total_score,
                            background_reprocessing_failed = $background_reprocessing_failed,
                            max_combo = $max_combo,
                            accuracy = $accuracy,
                            date = $date,
                            pp = $pp,
                            online_id = $online_id,
                            legacy_online_id = $legacy_online_id,
                            ruleset_short_name = $ruleset_short_name,
                            ruleset_name = $ruleset_name,
                            ruleset_instantiation_info = $ruleset_instantiation_info,
                            ruleset_online_id = $ruleset_online_id,
                            ruleset_last_applied_difficulty_version = $ruleset_last_applied_difficulty_version,
                            ruleset_available = $ruleset_available,
                            user_online_id = $user_online_id,
                            user_username = $user_username,
                            user_country = $user_country,
                            mods_json = $mods_json,
                            statistics_json = $statistics_json,
                            maximum_statistics_json = $maximum_statistics_json,
                            rank_int = $rank_int,
                            combo = $combo,
                            is_legacy_score = $is_legacy_score
                        WHERE id = $id;";
                    bindScore(command, item);
                    command.ExecuteNonQuery();
                }

                deleteChildren(transaction, id);
                insertFiles(transaction, item);
                insertPauses(transaction, item);

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
                    UPDATE scores
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
                throwIfDisposed();

                using var command = connection.CreateCommand();
                command.CommandText = @"
                    UPDATE scores
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
        /// Loads scores matching <paramref name="whereClause"/>, together with their file
        /// references and pauses. Must only be called while holding <see cref="sync"/>.
        /// </summary>
        private List<ScoreInfo> query(string? whereClause, Action<SqliteCommand>? bind = null, string orderBy = "ORDER BY rowid", int? limit = null)
        {
            var scores = new List<ScoreInfo>();

            using (var command = connection.CreateCommand())
            {
                string limitClause = limit == null ? string.Empty : $" LIMIT {limit.Value}";
                command.CommandText = $"SELECT {score_columns} FROM scores {whereClause} {orderBy}{limitClause};";
                bind?.Invoke(command);

                using SqliteDataReader reader = command.ExecuteReader();

                while (reader.Read())
                    scores.Add(readScore(reader));
            }

            if (scores.Count == 0)
                return scores;

            Dictionary<Guid, ScoreInfo> scoresById = scores.ToDictionary(score => score.ID);

            loadFiles(whereClause, bind, scoresById);
            loadPauses(whereClause, bind, scoresById);

            return scores;
        }

        /// <summary>
        /// Loads the score with the given primary key, without resolving its beatmap.
        /// Must only be called while holding <see cref="sync"/>.
        /// </summary>
        private ScoreInfo? find(Guid id)
            => query("WHERE id = $id", command => command.Parameters.AddWithValue("$id", id.ToString()), limit: 1).FirstOrDefault();

        private void loadFiles(string? whereClause, Action<SqliteCommand>? bind, Dictionary<Guid, ScoreInfo> scores)
        {
            string filter = whereClause == null ? string.Empty : $"WHERE score_id IN (SELECT id FROM scores {whereClause})";

            using var command = connection.CreateCommand();
            command.CommandText = $@"
                SELECT score_id, file_hash, filename
                FROM score_files
                {filter}
                ORDER BY score_id, ordinal;";
            bind?.Invoke(command);

            using SqliteDataReader reader = command.ExecuteReader();

            while (reader.Read())
            {
                if (!scores.TryGetValue(Guid.Parse(reader.GetString(0)), out ScoreInfo? score))
                    continue;

                score.Files.Add(new RealmNamedFileUsage(new RealmFile { Hash = reader.GetString(1) }, reader.GetString(2)));
            }
        }

        private void loadPauses(string? whereClause, Action<SqliteCommand>? bind, Dictionary<Guid, ScoreInfo> scores)
        {
            string filter = whereClause == null ? string.Empty : $"WHERE score_id IN (SELECT id FROM scores {whereClause})";

            using var command = connection.CreateCommand();
            command.CommandText = $@"
                SELECT score_id, pause
                FROM score_pauses
                {filter}
                ORDER BY score_id, ordinal;";
            bind?.Invoke(command);

            using SqliteDataReader reader = command.ExecuteReader();

            while (reader.Read())
            {
                if (scores.TryGetValue(Guid.Parse(reader.GetString(0)), out ScoreInfo? score))
                    score.Pauses.Add(reader.GetInt32(1));
            }
        }

        private static ScoreInfo readScore(SqliteDataReader reader)
        {
            return new ScoreInfo
            {
                ID = Guid.Parse(reader.GetString(0)),
                BeatmapHash = reader.GetString(1),
                ClientVersion = reader.GetString(2),
                Hash = reader.GetString(3),
                DeletePending = reader.GetInt32(4) != 0,
                TotalScore = reader.GetInt64(5),
                TotalScoreWithoutMods = reader.GetInt64(6),
                TotalScoreVersion = reader.GetInt32(7),
                LegacyTotalScore = reader.IsDBNull(8) ? null : reader.GetInt64(8),
                BackgroundReprocessingFailed = reader.GetInt32(9) != 0,
                MaxCombo = reader.GetInt32(10),
                Accuracy = reader.GetDouble(11),
                Date = DateTimeOffset.Parse(reader.GetString(12), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                PP = reader.IsDBNull(13) ? null : reader.GetDouble(13),
                OnlineID = reader.GetInt64(14),
                LegacyOnlineID = reader.GetInt64(15),
                Ruleset = new RulesetInfo
                {
                    ShortName = reader.GetString(16),
                    Name = reader.GetString(17),
                    InstantiationInfo = reader.GetString(18),
                    OnlineID = reader.GetInt32(19),
                    LastAppliedDifficultyVersion = reader.GetInt32(20),
                    Available = reader.GetInt32(21) != 0
                },
                RealmUser = new RealmUser
                {
                    OnlineID = reader.GetInt32(22),
                    Username = reader.GetString(23),
                    CountryString = reader.GetString(24)
                },
                ModsJson = reader.GetString(25),
                StatisticsJson = reader.GetString(26),
                MaximumStatisticsJson = reader.GetString(27),
                RankInt = reader.GetInt32(28),
                Combo = reader.GetInt32(29),
                IsLegacyScore = reader.GetInt32(30) != 0,
                // Resolved separately, outside the store lock, from the stored beatmap hash.
                BeatmapInfo = null
            };
        }

        /// <summary>
        /// The stored link is <see cref="ScoreInfo.BeatmapHash"/>; keep it consistent when a
        /// linked beatmap is supplied without one.
        /// </summary>
        private static void normaliseBeatmapHash(ScoreInfo item)
        {
            if (string.IsNullOrEmpty(item.BeatmapHash) && item.BeatmapInfo != null)
                item.BeatmapHash = item.BeatmapInfo.Hash;
        }

        private static void bindScore(SqliteCommand command, ScoreInfo item)
        {
            command.Parameters.AddWithValue("$id", item.ID.ToString());
            command.Parameters.AddWithValue("$beatmap_hash", item.BeatmapHash);
            command.Parameters.AddWithValue("$client_version", item.ClientVersion);
            command.Parameters.AddWithValue("$score_hash", item.Hash);
            command.Parameters.AddWithValue("$delete_pending", item.DeletePending ? 1 : 0);
            command.Parameters.AddWithValue("$total_score", item.TotalScore);
            command.Parameters.AddWithValue("$total_score_without_mods", item.TotalScoreWithoutMods);
            command.Parameters.AddWithValue("$total_score_version", item.TotalScoreVersion);
            command.Parameters.AddWithValue("$legacy_total_score", (object?)item.LegacyTotalScore ?? DBNull.Value);
            command.Parameters.AddWithValue("$background_reprocessing_failed", item.BackgroundReprocessingFailed ? 1 : 0);
            command.Parameters.AddWithValue("$max_combo", item.MaxCombo);
            command.Parameters.AddWithValue("$accuracy", item.Accuracy);
            command.Parameters.AddWithValue("$date", item.Date.ToString("O", CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("$pp", (object?)item.PP ?? DBNull.Value);
            command.Parameters.AddWithValue("$online_id", item.OnlineID);
            command.Parameters.AddWithValue("$legacy_online_id", item.LegacyOnlineID);
            command.Parameters.AddWithValue("$ruleset_short_name", item.Ruleset.ShortName);
            command.Parameters.AddWithValue("$ruleset_name", item.Ruleset.Name);
            command.Parameters.AddWithValue("$ruleset_instantiation_info", item.Ruleset.InstantiationInfo);
            command.Parameters.AddWithValue("$ruleset_online_id", item.Ruleset.OnlineID);
            command.Parameters.AddWithValue("$ruleset_last_applied_difficulty_version", item.Ruleset.LastAppliedDifficultyVersion);
            command.Parameters.AddWithValue("$ruleset_available", item.Ruleset.Available ? 1 : 0);
            command.Parameters.AddWithValue("$user_online_id", item.RealmUser.OnlineID);
            command.Parameters.AddWithValue("$user_username", item.RealmUser.Username);
            command.Parameters.AddWithValue("$user_country", item.RealmUser.CountryString);
            command.Parameters.AddWithValue("$mods_json", item.ModsJson);
            command.Parameters.AddWithValue("$statistics_json", item.StatisticsJson);
            command.Parameters.AddWithValue("$maximum_statistics_json", item.MaximumStatisticsJson);
            command.Parameters.AddWithValue("$rank_int", item.RankInt);
            command.Parameters.AddWithValue("$combo", item.Combo);
            command.Parameters.AddWithValue("$is_legacy_score", item.IsLegacyScore ? 1 : 0);
        }

        private void insertFiles(SqliteTransaction transaction, ScoreInfo item)
        {
            for (int i = 0; i < item.Files.Count; i++)
            {
                RealmNamedFileUsage usage = item.Files[i];

                using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = @"
                    INSERT INTO score_files (score_id, ordinal, file_hash, filename)
                    VALUES ($score_id, $ordinal, $file_hash, $filename);";
                command.Parameters.AddWithValue("$score_id", item.ID.ToString());
                command.Parameters.AddWithValue("$ordinal", i);
                command.Parameters.AddWithValue("$file_hash", usage.File.Hash);
                command.Parameters.AddWithValue("$filename", usage.Filename);
                command.ExecuteNonQuery();
            }
        }

        private void insertPauses(SqliteTransaction transaction, ScoreInfo item)
        {
            for (int i = 0; i < item.Pauses.Count; i++)
            {
                using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = @"
                    INSERT INTO score_pauses (score_id, ordinal, pause)
                    VALUES ($score_id, $ordinal, $pause);";
                command.Parameters.AddWithValue("$score_id", item.ID.ToString());
                command.Parameters.AddWithValue("$ordinal", i);
                command.Parameters.AddWithValue("$pause", item.Pauses[i]);
                command.ExecuteNonQuery();
            }
        }

        private void deleteChildren(SqliteTransaction transaction, Guid id)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = @"
                DELETE FROM score_files WHERE score_id = $score_id;
                DELETE FROM score_pauses WHERE score_id = $score_id;";
            command.Parameters.AddWithValue("$score_id", id.ToString());
            command.ExecuteNonQuery();
        }

        private ScoreInfo? hydrateBeatmap(ScoreInfo? score)
        {
            if (score == null)
                return null;

            if (string.IsNullOrEmpty(score.BeatmapHash))
            {
                score.BeatmapInfo = null;
                return score;
            }

            string hash = score.BeatmapHash;

            // Deliberately outside the store lock. No intermediate collection: the single
            // result is detached straight from the query.
            score.BeatmapInfo = realm.Run(r => r.All<BeatmapInfo>()
                                                  .Where(b => b.Hash == hash)
                                                  .AsEnumerable()
                                                  .FirstOrDefault(b => b.BeatmapSet != null && !b.BeatmapSet.DeletePending)?.Detach());

            return score;
        }

        private void hydrateBeatmaps(List<ScoreInfo> scores)
        {
            if (scores.Count == 0)
                return;

            var hashes = new HashSet<string>();

            foreach (ScoreInfo score in scores)
            {
                if (!string.IsNullOrEmpty(score.BeatmapHash))
                    hashes.Add(score.BeatmapHash);
            }

            if (hashes.Count == 0)
                return;

            // Deliberately outside the store lock: the caller may already hold a realm write
            // transaction while invoking this store. A single scan resolves every score rather
            // than issuing a query per row.
            realm.Run(r =>
            {
                var resolved = new Dictionary<string, BeatmapInfo>();

                foreach (BeatmapInfo beatmap in r.All<BeatmapInfo>())
                {
                    if (!hashes.Contains(beatmap.Hash))
                        continue;

                    if (beatmap.BeatmapSet == null || beatmap.BeatmapSet.DeletePending)
                        continue;

                    if (!resolved.ContainsKey(beatmap.Hash))
                        resolved[beatmap.Hash] = beatmap.Detach();
                }

                foreach (ScoreInfo score in scores)
                    score.BeatmapInfo = string.IsNullOrEmpty(score.BeatmapHash) ? null : resolved.GetValueOrDefault(score.BeatmapHash);
            });
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
            private readonly SqliteScoreStore owner;
            private readonly Action callback;

            public Subscription(SqliteScoreStore owner, Action callback)
            {
                this.owner = owner;
                this.callback = callback;
            }

            public void Dispose() => owner.unsubscribe(callback);
        }
    }
}
