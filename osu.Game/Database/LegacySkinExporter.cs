// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

using System.IO;
using osu.Framework.Platform;
using osu.Game.Extensions;
using osu.Game.Skinning;

namespace osu.Game.Database
{
    public class LegacySkinExporter : LegacyArchiveExporter<SkinInfo>
    {
        private readonly Storage skinFileStorage;

        public LegacySkinExporter(Storage storage, string fileStorageDirectory = @"files")
            : base(storage)
        {
            skinFileStorage = storage.GetStorageForDirectory(fileStorageDirectory);
        }

        protected override bool UseFixedEncoding => false;

        protected override string FileExtension => @".osk";

        protected override Stream? GetFileContents(SkinInfo model, INamedFileUsage file) => skinFileStorage.GetStream(file.File.GetStoragePath());
    }
}
