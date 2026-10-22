// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using osu.Framework.Extensions;
using osu.Framework.IO.Stores;
using osu.Game.Database;
using osu.Game.Extensions;

namespace osu.Game.Skinning
{
    /// <summary>
    /// Maps a skin's filenames to its content store, refreshing mappings after persisted edits.
    /// </summary>
    internal sealed class SkinStoreResourceStore : ResourceStore<byte[]>
    {
        private Dictionary<string, string> filePaths;
        private IDisposable? subscription;

        public SkinStoreResourceStore(SkinInfo skin, IResourceStore<byte[]> files)
            : base(files)
        {
            filePaths = createMapping(skin);
        }

        public void BindStore(ISkinStore store, Guid id)
        {
            subscription?.Dispose();
            subscription = store.Subscribe(() =>
            {
                var skin = store.GetDetached(id);
                if (skin != null)
                    Volatile.Write(ref filePaths, createMapping(skin));
            });
        }

        private static Dictionary<string, string> createMapping(SkinInfo skin) =>
            skin.Files.ToDictionary(f => f.Filename, f => f.File.GetStoragePath(), StringComparer.OrdinalIgnoreCase);

        protected override IEnumerable<string> GetFilenames(string name)
        {
            var paths = Volatile.Read(ref filePaths);

            foreach (string filename in base.GetFilenames(name))
            {
                if (paths.TryGetValue(filename.ToStandardisedPath(), out string? path))
                    yield return path;
            }
        }

        public override IEnumerable<string> GetAvailableResources() => Volatile.Read(ref filePaths).Keys;

        protected override void Dispose(bool disposing)
        {
            subscription?.Dispose();
            base.Dispose(disposing);
        }
    }
}
