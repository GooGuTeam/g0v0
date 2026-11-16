// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.IO.Archives;
using osu.Game.Rulesets;

namespace osu.Game.Scoring
{
    /// <summary>
    /// Application-level score/replay file codecs. Import, local storage, retrieval and export use the same registry.
    /// Rulesets declare their frame data through <see cref="Ruleset.CreateReplayFormat"/>; they do not handle database files.
    /// </summary>
    public abstract class ScoreReplayFormat
    {
        private static readonly List<ScoreReplayFormat> formats = new List<ScoreReplayFormat>
        {
            new NativeReplayFileFormat(),
            new LegacyReplayFileFormat()
        };

        public static IReadOnlyList<ScoreReplayFormat> Registered
        {
            get
            {
                lock (formats)
                    return formats.ToArray();
            }
        }

        public static void Register(ScoreReplayFormat format)
        {
            ArgumentNullException.ThrowIfNull(format);
            if (format.Extension.Length < 2 || !format.Extension.StartsWith('.') || format.Extension.Contains('/') || format.Extension.Contains('\\'))
                throw new ArgumentException("A replay extension must be a file suffix.", nameof(format));

            lock (formats)
            {
                if (formats.Any(f => f.Extension.Equals(format.Extension, StringComparison.OrdinalIgnoreCase)))
                    throw new ArgumentException("A codec is already registered for this replay extension.", nameof(format));

                formats.Insert(0, format);
            }
        }

        public static ScoreReplayFormat? ForFilename(string filename) => Registered.FirstOrDefault(f => f.Extension.Equals(Path.GetExtension(filename), StringComparison.OrdinalIgnoreCase));

        public static ScoreReplayFormat? ForRuleset(Ruleset ruleset) => Registered.FirstOrDefault(f => f.CanEncode(ruleset));

        public abstract string Extension { get; }

        public abstract bool CanEncode(Ruleset ruleset);

        public abstract void Encode(Score score, IBeatmap beatmap, Stream stream);

        public abstract Score Decode(Stream stream, IRulesetStore rulesets, BeatmapManager beatmaps);

        public ArchiveReader CreateArchive(Score score, IBeatmap beatmap)
        {
            using var stream = new MemoryStream();
            Encode(score, beatmap, stream);
            return new ByteArrayArchiveReader(stream.ToArray(), "replay" + Extension);
        }
    }
}
