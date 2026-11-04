// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.IO;
using osu.Framework.Audio.Track;
using osu.Framework.Graphics.Textures;
using osu.Game.Beatmaps;
using osu.Game.Skinning;
using osu.Game.Storyboards;

namespace osu.Game.Recording.Export
{
    /// <summary>
    /// The beatmap as offline replay export presents it to gameplay. Beatmap data, storyboard and files come from the source,
    /// as in the game, so gameplay applies its own mods to it. The track, skin and background are the export's private ones,
    /// because the source's belong to the game's audio and renderer.
    /// </summary>
    public sealed class ExportWorkingBeatmap : WorkingBeatmap
    {
        private readonly WorkingBeatmap source;
        private readonly Track track;
        private readonly ISkin skin;
        private readonly Texture? background;

        public ExportWorkingBeatmap(WorkingBeatmap source, Track track, ISkin skin, Texture? background)
            : base(source.BeatmapInfo, null)
        {
            this.source = source;
            this.track = track;
            this.skin = skin;
            this.background = background;
        }

        protected override IBeatmap GetBeatmap() => source.Beatmap;

        public override Texture? GetBackground() => background;

        public override Texture? GetPanelBackground() => background;

        protected override Track GetBeatmapTrack() => track;

        protected internal override ISkin GetSkin() => skin;

        public override Stream GetStream(string storagePath) => source.GetStream(storagePath);

        protected override Waveform GetWaveform() => source.Waveform;

        protected override Storyboard GetStoryboard() => source.Storyboard;
    }
}
