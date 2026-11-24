// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Game.IO;
using osu.Game.IO.Archives;
using osu.Game.Models;

namespace osu.Game.Database
{
    /// <summary>
    /// An <see cref="ImportTask"/> for a model which lives in another g0v0!/lazer installation.
    /// </summary>
    /// <remarks>
    /// The file list is "frozen" on construction, as the source realm context is thread-confined and short-lived.
    /// </remarks>
    public class LazerImportTask : ImportTask
    {
        private readonly IReadOnlyList<(string Filename, string Hash)> files;
        private readonly LazerStorage storage;

        public LazerImportTask(string name, IEnumerable<RealmNamedFileUsage> files, LazerStorage storage)
            : base(name)
        {
            this.files = files.Select(f => (f.Filename, f.File.Hash)).ToArray();
            this.storage = storage;
        }

        public override ArchiveReader GetReader() => new LazerStorageArchiveReader(Path, files, storage);

        public override void DeleteFile()
        {
            // The file store of the source installation is not ours to modify.
        }
    }
}
