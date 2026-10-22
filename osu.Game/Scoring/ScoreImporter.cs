// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using osu.Framework.Extensions;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.IO.Archives;
using osu.Game.Models;
using osu.Game.Overlays.Notifications;
using osu.Game.Rulesets;
using osu.Game.Scoring.Legacy;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Utils;
using Realms;

namespace osu.Game.Scoring
{
    public class ScoreImporter : RealmArchiveModelImporter<ScoreInfo>
    {
        public override IEnumerable<string> HandledExtensions => new[] { ".osr" };

        protected override string[] HashableFileTypes => new[] { ".osr" };

        private readonly RulesetStore rulesets;
        private readonly Func<BeatmapManager> beatmaps;

        private readonly IAPIProvider api;
        private readonly IScoreStore scoreStore;
        private readonly Lock importLock = new Lock();

        public ScoreImporter(RulesetStore rulesets, Func<BeatmapManager> beatmaps, Storage storage, RealmAccess realm, IAPIProvider api, IScoreStore? scoreStore = null)
            : base(storage, realm, scoreStore?.FileStorageDirectory ?? @"files")
        {
            this.rulesets = rulesets;
            this.beatmaps = beatmaps;
            this.api = api;
            this.scoreStore = scoreStore ?? new RealmScoreStore(realm);
        }

        public override Live<ScoreInfo>? ImportModel(ScoreInfo item, ArchiveReader? archive = null, ImportParameters parameters = default, CancellationToken cancellationToken = default)
        {
            if (scoreStore is RealmScoreStore)
                return base.ImportModel(item, archive, parameters, cancellationToken);

            // Both import schedulers can enter here. Duplicate detection and insertion must be serialised together.
            lock (importLock)
            {
                cancellationToken.ThrowIfCancellationRequested();
                item = item.Detach();

                if (archive != null)
                {
                    item.Files.Clear();
                    var filenames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    foreach (string filename in archive.Filenames)
                    {
                        string path = filename.ToStandardisedPath();

                        if (FilesystemSanityCheckHelpers.IncursPathTraversalRisk(path) || !filenames.Add(path))
                            throw new InvalidOperationException($"Invalid or duplicate replay filename: {filename}");

                        using var stream = archive.GetStream(filename);
                        item.Files.Add(new RealmNamedFileUsage(Files.AddUnmanaged(stream, parameters.PreferHardLinks), path));
                    }
                }

                item.Hash = ComputeHash(item);
                var existing = scoreStore.FindByHash(item.Hash);

                if (existing != null && CanReuseExisting(existing, item))
                {
                    scoreStore.Undelete(existing.ID);
                    return scoreStore.GetDetached(existing.ID)!.ToLiveUnmanaged();
                }

                Realm.Write(r =>
                {
                    Populate(item, archive, r, cancellationToken);
                    PostImport(item, r, parameters);
                    item.BeatmapInfo = item.BeatmapInfo!.Detach();
                    item.Ruleset = item.Ruleset.Detach();
                });
                cancellationToken.ThrowIfCancellationRequested();

                scoreStore.Add(item);
                if (existing != null)
                    scoreStore.Delete(existing.ID);

                return scoreStore.GetDetached(item.ID)!.ToLiveUnmanaged();
            }
        }

        public override async Task<Live<ScoreInfo>?> ImportAsUpdate(ProgressNotification notification, ImportTask task, ScoreInfo original)
        {
            var imported = (await Import(notification, new[] { task }).ConfigureAwait(false)).FirstOrDefault();
            if (imported != null && imported.ID != original.ID)
                scoreStore.Delete(original.ID);
            return imported;
        }

        protected override ScoreInfo? CreateModel(ArchiveReader archive, ImportParameters parameters)
        {
            string name = archive.Filenames.First(f => f.EndsWith(".osr", StringComparison.OrdinalIgnoreCase));

            using (var stream = archive.GetStream(name))
            {
                try
                {
                    return new DatabasedLegacyScoreDecoder(rulesets, beatmaps()).Parse(stream).ScoreInfo;
                }
                catch (LegacyScoreDecoder.BeatmapNotFoundException notFound)
                {
                    Logger.Log($@"Score '{archive.Name}' failed to import: no corresponding beatmap with the hash '{notFound.Hash}' could be found.", LoggingTarget.Database);

                    if (!parameters.Batch)
                    {
                        // In the case of a missing beatmap, let's attempt to resolve it and show a prompt to the user to download the required beatmap.
                        var req = new GetBeatmapRequest(new BeatmapInfo { MD5Hash = notFound.Hash });
                        req.Success += res => PostNotification?.Invoke(new MissingBeatmapNotification(res, notFound.Hash, archive));
                        api.Queue(req);
                    }

                    return null;
                }
                catch (Exception e)
                {
                    Logger.Log($@"Failed to parse headers of score '{archive.Name}': {e}.", LoggingTarget.Database);
                    return null;
                }
            }
        }

        public Score GetScore(ScoreInfo score) => new LegacyDatabasedScore(score, rulesets, beatmaps(), Files.Store);

        protected override void Populate(ScoreInfo model, ArchiveReader? archive, Realm realm, CancellationToken cancellationToken = default)
        {
            Debug.Assert(model.BeatmapInfo != null);

            // Ensure the beatmap is not detached.
            if (!model.BeatmapInfo.IsManaged)
                model.BeatmapInfo = realm.Find<BeatmapInfo>(model.BeatmapInfo.ID)!;

            if (!model.Ruleset.IsManaged)
                model.Ruleset = realm.Find<RulesetInfo>(model.Ruleset.ShortName)!;

            // These properties are known to be non-null, but these final checks ensure a null hasn't come from somewhere (or the refetch has failed).
            // Under no circumstance do we want these to be written to realm as null.
            ArgumentNullException.ThrowIfNull(model.BeatmapInfo);
            ArgumentNullException.ThrowIfNull(model.Ruleset);

            if (!ModUtils.CheckCompatibleSet(model.Mods))
                throw new InvalidOperationException(@"The score specifies an incompatible set of mods!");

            if (string.IsNullOrEmpty(model.StatisticsJson))
                model.StatisticsJson = JsonConvert.SerializeObject(model.Statistics);

            if (string.IsNullOrEmpty(model.MaximumStatisticsJson))
                model.MaximumStatisticsJson = JsonConvert.SerializeObject(model.MaximumStatistics);
        }

        // Very naive local caching to improve performance of large score imports (where the username is usually the same for most or all scores).

        // TODO: `UserLookupCache` cannot currently be used here because of async foibles.
        // It only supports lookups by user ID (username would require web changes), and even then the ID lookups cannot be used.
        // That is because that component provides an async interface, and async functions cannot be consumed safely here due to the rigid structure of `RealmArchiveModelImporter`.
        // The importer has two paths, one async and one sync; the async path runs the sync path in a task.
        // This means that sometimes `PostImport()` is called from a sync context, and sometimes from an async one, whilst itself being a sync method.
        // That in turn makes `.GetResultSafely()` not callable inside `PostImport()`, as it will throw when called from an async context,
        private readonly Dictionary<int, APIUser> idLookupCache = new Dictionary<int, APIUser>();
        private readonly Dictionary<string, APIUser> usernameLookupCache = new Dictionary<string, APIUser>();

        protected override void PostImport(ScoreInfo model, Realm realm, ImportParameters parameters)
        {
            base.PostImport(model, realm, parameters);

            populateUserDetails(model);

            Debug.Assert(model.BeatmapInfo != null);

            // This needs to be run after user detail population to ensure we have a valid user id.
            if (api.IsLoggedIn && api.LocalUser.Value.OnlineID == model.UserID && (model.BeatmapInfo.LastPlayed == null || model.Date > model.BeatmapInfo.LastPlayed))
                model.BeatmapInfo.LastPlayed = model.Date;
        }

        /// <summary>
        /// Legacy replays only store a username.
        /// This will populate a user ID during import.
        /// </summary>
        private void populateUserDetails(ScoreInfo model)
        {
            if (model.RealmUser.OnlineID == APIUser.SYSTEM_USER_ID)
                return;

            if (model.RealmUser.OnlineID > 1)
            {
                model.User = lookupUserById(model.RealmUser.OnlineID) ?? model.User;
                return;
            }

            if (model.OnlineID < 0 && model.LegacyOnlineID <= 0)
                return;

            model.User = lookupUserByName(model.RealmUser.Username) ?? model.User;
        }

        private APIUser? lookupUserById(int id)
        {
            if (idLookupCache.TryGetValue(id, out var existing))
            {
                return existing;
            }

            var userRequest = new GetUserRequest(id);

            api.Perform(userRequest);

            if (userRequest.Response is APIUser user)
            {
                APIUser cachedUser;

                idLookupCache.TryAdd(id, cachedUser = new APIUser
                {
                    // Because this is a permanent cache, let's only store the pieces we're interested in,
                    // rather than the full API response. If we start to store more than these three fields
                    // in realm, this should be undone.
                    Id = user.Id,
                    Username = user.Username,
                    CountryCode = user.CountryCode,
                });

                return cachedUser;
            }

            return null;
        }

        private APIUser? lookupUserByName(string username)
        {
            if (usernameLookupCache.TryGetValue(username, out var existing))
            {
                return existing;
            }

            var userRequest = new GetUserRequest(username);

            api.Perform(userRequest);

            if (userRequest.Response is APIUser user)
            {
                APIUser cachedUser;

                usernameLookupCache.TryAdd(username, cachedUser = new APIUser
                {
                    // Because this is a permanent cache, let's only store the pieces we're interested in,
                    // rather than the full API response. If we start to store more than these three fields
                    // in realm, this should be undone.
                    Id = user.Id,
                    Username = user.Username,
                    CountryCode = user.CountryCode,
                });

                return cachedUser;
            }

            return null;
        }
    }
}
