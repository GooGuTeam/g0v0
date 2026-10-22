// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Models;
using osu.Game.Rulesets;
using osu.Game.Scoring;
using Realms;

namespace osu.Game.Database
{
    /// <summary>
    /// Realm-backed implementation of <see cref="IScoreStore"/>.
    /// </summary>
    public class RealmScoreStore : RealmSoftDeletableStore<ScoreInfo>, IScoreStore
    {
        public RealmScoreStore(RealmAccess realm)
            : base(realm)
        {
        }

        public string FileStorageDirectory => @"files";

        /// <summary>
        /// Add a score, reusing the realm-managed <see cref="RulesetInfo"/>, <see cref="BeatmapInfo"/>
        /// and <see cref="RealmFile"/> instances referenced by it rather than inserting duplicate copies.
        /// </summary>
        /// <remarks>
        /// A linked <see cref="BeatmapInfo"/> which no longer exists in realm is cleared, while
        /// <see cref="ScoreInfo.BeatmapHash"/> is retained so the link can be restored later.
        /// </remarks>
        public override void Add(ScoreInfo item)
        {
            ArgumentNullException.ThrowIfNull(item);

            Realm.Write(r =>
            {
                resolveRuleset(r, item);
                resolveBeatmap(r, item);
                resolveFiles(r, item);
                r.Add(item);
            });
        }

        public ScoreInfo? FindByHash(string hash)
        {
            if (string.IsNullOrEmpty(hash))
                return null;

            // Deleted scores are included so the importer can reuse and restore an existing row.
            return Realm.Run(r => r.All<ScoreInfo>()
                                   .OrderBy(s => s.DeletePending)
                                   .FirstOrDefault(s => s.Hash == hash)?.Detach());
        }

        public ScoreInfo? FindByOnlineId(long onlineId) => Realm.Run(r =>
            r.All<ScoreInfo>()
             .FirstOrDefault(s => !s.DeletePending && s.OnlineID == onlineId)?.Detach());

        public ScoreInfo? FindByLegacyOnlineId(long onlineId) => Realm.Run(r =>
            r.All<ScoreInfo>()
             .FirstOrDefault(s => !s.DeletePending && s.LegacyOnlineID == onlineId)?.Detach());

        public List<ScoreInfo> GetForBeatmap(string beatmapHash)
        {
            if (string.IsNullOrEmpty(beatmapHash))
                return new List<ScoreInfo>();

            return Realm.Run(r => r.All<ScoreInfo>()
                                   .Where(s => !s.DeletePending && s.BeatmapHash == beatmapHash)
                                   .AsEnumerable()
                                   .Detach());
        }

        private static void resolveRuleset(Realm realm, ScoreInfo score)
        {
            RulesetInfo ruleset = score.Ruleset;

            if (ruleset.IsManaged)
                return;

            RulesetInfo? resolved = realm.Find<RulesetInfo>(ruleset.ShortName);

            if (resolved == null)
            {
                realm.Add(ruleset);
                resolved = ruleset;
            }

            score.Ruleset = resolved;
        }

        private static void resolveBeatmap(Realm realm, ScoreInfo score)
        {
            BeatmapInfo? beatmap = score.BeatmapInfo;

            if (beatmap == null)
            {
                if (!string.IsNullOrEmpty(score.BeatmapHash))
                    score.BeatmapInfo = findBeatmapByHash(realm, score.BeatmapHash);

                return;
            }

            if (beatmap.IsManaged)
            {
                if (string.IsNullOrEmpty(score.BeatmapHash))
                    score.BeatmapHash = beatmap.Hash;

                return;
            }

            BeatmapInfo? resolved = realm.Find<BeatmapInfo>(beatmap.ID);

            if (resolved == null && !string.IsNullOrEmpty(score.BeatmapHash))
                resolved = findBeatmapByHash(realm, score.BeatmapHash);

            if (string.IsNullOrEmpty(score.BeatmapHash) && resolved != null)
                score.BeatmapHash = resolved.Hash;

            score.BeatmapInfo = resolved;
        }

        private static BeatmapInfo? findBeatmapByHash(Realm realm, string hash)
            => realm.All<BeatmapInfo>()
                    .Where(b => b.Hash == hash)
                    .AsEnumerable()
                    .FirstOrDefault(b => b.BeatmapSet != null && !b.BeatmapSet.DeletePending);

        private static void resolveFiles(Realm realm, ScoreInfo score)
        {
            foreach (RealmNamedFileUsage usage in score.Files)
            {
                if (usage.File.IsManaged)
                    continue;

                RealmFile? existing = realm.Find<RealmFile>(usage.File.Hash);

                if (existing != null)
                    usage.File = existing;
            }
        }
    }
}
