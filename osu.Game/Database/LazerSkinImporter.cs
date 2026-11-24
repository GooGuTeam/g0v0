// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using osu.Game.IO;
using osu.Game.Skinning;
using Realms;

namespace osu.Game.Database
{
    /// <summary>
    /// Imports skins from another g0v0!/lazer installation.
    /// </summary>
    public class LazerSkinImporter
    {
        private readonly IModelImporter<SkinInfo> importer;

        public LazerSkinImporter(IModelImporter<SkinInfo> importer)
        {
            this.importer = importer;
        }

        public Task<int> GetAvailableCount(LazerStorage storage) => Task.Run(() =>
        {
            using Realm realm = storage.OpenRealm();
            return getImportableSkins(realm, storage).Count();
        });

        public Task ImportFromLazerAsync(LazerStorage storage) => Task.Run(async () =>
        {
            ImportTask[] tasks = getImportTasks(storage);

            if (tasks.Length == 0)
                return;

            await importer.Import(tasks, new ImportParameters { Batch = true, PreferHardLinks = true }).ConfigureAwait(false);
        });

        private static ImportTask[] getImportTasks(LazerStorage storage)
        {
            using Realm realm = storage.OpenRealm();

            // Cast up front so the resulting array is not a co-variant view of a LazerImportTask array.
            return getImportableSkins(realm, storage)
                   .Select(ImportTask (skin) => new LazerImportTask(getName(skin), skin.Files, storage))
                   .ToArray();
        }

        // Skins shipped with the other installation are shipped with this one too.
        private static IEnumerable<SkinInfo> getImportableSkins(Realm realm, LazerStorage storage)
        {
            if (!storage.FileStoreAvailable)
                return Enumerable.Empty<SkinInfo>();

            return realm.All<SkinInfo>()
                        .Where(s => !s.DeletePending && !s.Protected)
                        .AsEnumerable()
                        .Where(s => s.Files.Count > 0)
                        // A skin with a single pruned file would fail to import as a whole, after its other files were already copied.
                        // Checked here as well so that the count reported to the user matches what will actually be imported.
                        .Where(skin => skin.Files.All(f => storage.HasFile(f.File.Hash)));
        }

        private static string getName(SkinInfo skin) => string.IsNullOrWhiteSpace(skin.Name) ? skin.ID.ToString() : skin.Name;
    }
}
