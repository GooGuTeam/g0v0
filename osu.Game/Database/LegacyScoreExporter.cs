// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

using System.IO;
using System.Linq;
using System.Threading;
using osu.Framework.Platform;
using osu.Game.Extensions;
using osu.Game.Overlays.Notifications;
using osu.Game.Scoring;

namespace osu.Game.Database
{
    public class LegacyScoreExporter : LegacyExporter<ScoreInfo>
    {
        private readonly Storage replayStorage;

        public LegacyScoreExporter(Storage storage, string fileStorageDirectory = @"files")
            : base(storage)
        {
            replayStorage = storage.GetStorageForDirectory(fileStorageDirectory);
        }

        protected override string GetFilename(ScoreInfo score)
        {
            string scoreString = score.GetDisplayString();
            string filename = $"{scoreString} ({score.Date.LocalDateTime:yyyy-MM-dd_HH-mm})";

            return filename;
        }

        protected override string FileExtension => @".osr";

        public override void ExportToStream(ScoreInfo model, Stream outputStream, ProgressNotification? notification, CancellationToken cancellationToken = default)
        {
            var file = model.Files.SingleOrDefault();
            if (file == null)
                return;

            using (var inputStream = replayStorage.GetStream(file.File.GetStoragePath()))
                inputStream.CopyTo(outputStream);
        }
    }
}
