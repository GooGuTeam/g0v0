// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using osu.Game.Beatmaps;
using osu.Game.IO;
using Realms;

namespace osu.Game.Database
{
    /// <summary>
    /// Imports beatmaps from another g0v0!/lazer installation.
    /// </summary>
    public class LazerBeatmapImporter
    {
        private readonly IModelImporter<BeatmapSetInfo> importer;

        public LazerBeatmapImporter(IModelImporter<BeatmapSetInfo> importer)
        {
            this.importer = importer;
        }

        public Task<int> GetAvailableCount(LazerStorage storage) => Task.Run(() =>
        {
            using Realm realm = storage.OpenRealm();
            return getImportableSets(realm, storage).Count();
        });

        public Task ImportFromLazerAsync(LazerStorage storage) => Task.Run(async () =>
        {
            // The importer runs on its own threads, so the import tasks carry everything they need.
            ImportTask[] tasks = getImportTasks(storage);

            if (tasks.Length == 0)
                return;

            await importer.Import(tasks, new ImportParameters { Batch = true, PreferHardLinks = true }).ConfigureAwait(false);
        });

        private static ImportTask[] getImportTasks(LazerStorage storage)
        {
            using Realm realm = storage.OpenRealm();

            // Cast up front so the resulting array is not a co-variant view of a LazerImportTask array.
            return getImportableSets(realm, storage)
                   .Select(ImportTask (set) => new LazerImportTask($@"{set.OnlineID} {set.Metadata.Title}".Trim(), set.Files, storage))
                   .ToArray();
        }

        private static IEnumerable<BeatmapSetInfo> getImportableSets(Realm realm, LazerStorage storage)
        {
            if (!storage.FileStoreAvailable)
                return Enumerable.Empty<BeatmapSetInfo>();

            return realm.All<BeatmapSetInfo>()
                        .Where(s => !s.DeletePending)
                        .AsEnumerable()
                        .Where(hasTopLevelBeatmap)
                        // A set with a single pruned file would fail to import as a whole, after its other files were already copied.
                        // Checked here as well so that the count reported to the user matches what will actually be imported.
                        .Where(set => set.Files.All(f => storage.HasFile(f.File.Hash)));
        }

        /// <summary>
        /// Beatmaps are only picked up by the importer when their <c>.osu</c> file is at the top level of the set.
        /// </summary>
        private static bool hasTopLevelBeatmap(BeatmapSetInfo set)
            => set.Files.Any(f => !f.Filename.Contains('/') && f.Filename.EndsWith(@".osu", StringComparison.OrdinalIgnoreCase));
    }
}
