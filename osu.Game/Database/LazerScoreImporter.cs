// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using osu.Framework.Logging;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.IO;
using osu.Game.Localisation;
using osu.Game.Models;
using osu.Game.Overlays.Notifications;
using osu.Game.Rulesets;
using osu.Game.Scoring;
using Realms;

namespace osu.Game.Database
{
    /// <summary>
    /// Imports scores from another g0v0!/lazer installation.
    /// </summary>
    /// <remarks>
    /// Scores cannot be expressed as an archive, so they are copied over as database rows along with their replay
    /// files. Scores whose beatmap / ruleset is not available locally are skipped.
    /// </remarks>
    public class LazerScoreImporter
    {
        private static readonly LocalisableString model_name = NotificationsStrings.ModelName(@"score");

        private readonly ScoreManager scores;
        private readonly RealmAccess realm;

        public Action<Notification>? PostNotification { get; set; }

        public LazerScoreImporter(ScoreManager scores, RealmAccess realm)
        {
            this.scores = scores;
            this.realm = realm;
        }

        public Task<int> GetAvailableCount(LazerStorage storage) => Task.Run(() =>
        {
            using Realm sourceRealm = storage.OpenRealm();
            return getImportableScores(sourceRealm).Length;
        });

        public Task<int> ImportFromLazerAsync(LazerStorage storage) => Task.Run(() =>
        {
            var notification = new ProgressNotification
            {
                State = ProgressNotificationState.Active,
                Text = NotificationsStrings.ImportInitialising(model_name),
            };

            PostNotification?.Invoke(notification);

            int imported;

            try
            {
                using Realm sourceRealm = storage.OpenRealm();

                ScoreInfo[] importableScores = getImportableScores(sourceRealm);
                imported = realm.Write(target => importScores(importableScores, storage, target, notification));
            }
            catch (Exception e)
            {
                Logger.Error(e, @"Failed to import scores from another installation.");
                notification.State = ProgressNotificationState.Cancelled;
                throw;
            }

            notification.CompletionText = NotificationsStrings.ImportedCount(imported, model_name);
            notification.State = ProgressNotificationState.Completed;

            return imported;
        });

        private int importScores(ScoreInfo[] importableScores, LazerStorage storage, Realm target, ProgressNotification notification)
        {
            Dictionary<string, BeatmapInfo> beatmapsByHash = target.All<BeatmapInfo>()
                                                                   .AsEnumerable()
                                                                   .GroupBy(b => b.Hash)
                                                                   .ToDictionary(g => g.Key, g => g.First());

            int imported = 0;

            for (int i = 0; i < importableScores.Length; i++)
            {
                if (i % 20 == 0)
                {
                    notification.Text = NotificationsStrings.ImportedXOfY(i, importableScores.Length, model_name);
                    notification.Progress = (float)i / importableScores.Length;
                }

                if (importScore(importableScores[i], storage, target, beatmapsByHash))
                    imported++;
            }

            return imported;
        }

        private bool importScore(ScoreInfo source, LazerStorage storage, Realm target, IReadOnlyDictionary<string, BeatmapInfo> beatmapsByHash)
        {
            // Imported scores keep their original IDs, so a repeated import is a no-op.
            if (target.Find<ScoreInfo>(source.ID) != null)
            {
                Logger.Log($"A score with id #{source.ID} already exists, skipping.");
                return false;
            }

            if (!beatmapsByHash.TryGetValue(source.BeatmapHash, out BeatmapInfo? beatmap))
            {
                Logger.Log($"The beatmap with hash {source.BeatmapHash} (required by import score #{source.ID}) cannot be found, skipping.");
                return false;
            }

            RulesetInfo? ruleset = target.Find<RulesetInfo>(source.Ruleset.ShortName);

            if (ruleset == null)
            {
                Logger.Log($"The ruleset {source.Ruleset.ShortName} (required by import score #{source.ID}) cannot be found, skipping.");
                return false;
            }

            var score = new ScoreInfo(beatmap, ruleset, new RealmUser
            {
                OnlineID = source.RealmUser.OnlineID,
                Username = source.RealmUser.Username,
                CountryString = source.RealmUser.CountryString,
            })
            {
                ID = source.ID,
                BeatmapHash = source.BeatmapHash,
                ClientVersion = source.ClientVersion,
                Hash = source.Hash,
                Date = source.Date,
                TotalScore = source.TotalScore,
                TotalScoreWithoutMods = source.TotalScoreWithoutMods,
                TotalScoreVersion = source.TotalScoreVersion,
                LegacyTotalScore = source.LegacyTotalScore,
                BackgroundReprocessingFailed = source.BackgroundReprocessingFailed,
                MaxCombo = source.MaxCombo,
                Accuracy = source.Accuracy,
                PP = source.PP,
                OnlineID = source.OnlineID,
                LegacyOnlineID = source.LegacyOnlineID,
                ModsJson = source.ModsJson,
                StatisticsJson = source.StatisticsJson,
                MaximumStatisticsJson = source.MaximumStatisticsJson,
            };

            foreach (int pause in source.Pauses)
                score.Pauses.Add(pause);

            foreach (RealmNamedFileUsage file in source.Files)
            {
                string path = storage.GetFilePath(file.File.Hash);

                if (!File.Exists(path))
                    continue;

                using Stream stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                scores.AddFile(score, stream, file.Filename, target);
            }

            target.Add(score);

            return true;
        }

        private static ScoreInfo[] getImportableScores(Realm sourceRealm) => sourceRealm.All<ScoreInfo>()
                                                                                        .Where(s => !s.DeletePending)
                                                                                        .AsEnumerable()
                                                                                        // Scores without a locally stored replay cannot be reconstructed.
                                                                                        .Where(s => s.Files.Count > 0 && !string.IsNullOrEmpty(s.BeatmapHash))
                                                                                        .ToArray();
    }
}
