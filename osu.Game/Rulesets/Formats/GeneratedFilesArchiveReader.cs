// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using osu.Game.IO.Archives;

namespace osu.Game.Rulesets.Formats
{
    /// <summary>
    /// Overlays generated files on top of an existing archive. Disposing it disposes the source archive.
    /// </summary>
    public class GeneratedFilesArchiveReader : ArchiveReader
    {
        private readonly ArchiveReader source;
        private readonly IReadOnlyDictionary<string, byte[]> generated;

        public GeneratedFilesArchiveReader(ArchiveReader source, IReadOnlyDictionary<string, byte[]> generated)
            : base(source.Name)
        {
            this.source = source;
            this.generated = generated;
        }

        public override IEnumerable<string> Filenames => source.Filenames.Concat(generated.Keys);

        public override Stream GetStream(string name) => generated.TryGetValue(name, out byte[]? content) ? new MemoryStream(content, false) : source.GetStream(name);

        public override void Dispose() => source.Dispose();
    }
}
