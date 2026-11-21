// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Framework.IO.Stores;
using osu.Game.Beatmaps;
using osu.Game.Extensions;
using osu.Game.Rulesets;

namespace osu.Game.Scoring
{
    /// <summary>
    /// Loads a stored replay using the codec registered for its file extension.
    /// </summary>
    public class DatabasedScore : Score
    {
        public DatabasedScore(ScoreInfo score, IRulesetStore rulesets, BeatmapManager beatmaps, IResourceStore<byte[]> store)
        {
            ScoreInfo = score;
            var file = score.Files.FirstOrDefault(f => ScoreReplayFormat.ForFilename(f.Filename) != null);
            if (file == null)
                return;

            using var stream = store.GetStream(file.File.GetStoragePath());
            if (stream == null)
                return;

            var decoded = ScoreReplayFormat.ForFilename(file.Filename)!.Decode(stream, rulesets, beatmaps);
            Replay = decoded.Replay;
            ScoreInfo.HealthGraph = decoded.ScoreInfo.HealthGraph;
        }
    }
}
