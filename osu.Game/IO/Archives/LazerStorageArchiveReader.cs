// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace osu.Game.IO.Archives
{
    /// <summary>
    /// An <see cref="ArchiveReader"/> exposing the files of a model stored in another g0v0!/lazer installation.
    /// </summary>
    /// <remarks>
    /// The file list is a snapshot, so the reader can outlive the realm context it was created from.
    /// </remarks>
    public class LazerStorageArchiveReader : ArchiveReader
    {
        private readonly IReadOnlyList<(string Filename, string Hash)> files;
        private readonly LazerStorage storage;

        public LazerStorageArchiveReader(string name, IReadOnlyList<(string Filename, string Hash)> files, LazerStorage storage)
            : base(name)
        {
            this.files = files;
            this.storage = storage;
        }

        public override IEnumerable<string> Filenames => files.Select(f => f.Filename);

        public override Stream? GetStream(string name)
        {
            string? hash = files.FirstOrDefault(f => f.Filename == name).Hash;

            if (hash == null)
                return null;

            // Returned as a `FileStream` so that the file store can hard link rather than copy where possible.
            return File.Open(storage.GetFilePath(hash), FileMode.Open, FileAccess.Read, FileShare.Read);
        }

        public override void Dispose()
        {
        }
    }
}
