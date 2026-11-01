// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.IO;
using System.Threading;
using osu.Framework.Audio.Track;
using osu.Framework.Graphics.Textures;
using osu.Game.Beatmaps;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Skinning;
using osu.Game.Storyboards;

namespace osu.Game.Screens.Play
{
    /// <summary>
    /// A working beatmap wrapper representing the modded playable beatmap.
    /// Exposes mod-adjusted difficulty, star rating, timing points and hit objects to HUD/skin components.
    /// </summary>
    public sealed class PlayableWorkingBeatmap : WorkingBeatmap
    {
        private readonly IBeatmap playableBeatmap;
        private readonly WorkingBeatmap source;

        public override bool CanCalculateDifficulty => source.CanCalculateDifficulty;

        public PlayableWorkingBeatmap(IBeatmap playableBeatmap, WorkingBeatmap source, double? starRating = null)
            : base(playableBeatmap.BeatmapInfo, null)
        {
            this.playableBeatmap = playableBeatmap;
            this.source = source;

            // Ensure BeatmapInfo reflects mod-adjusted difficulty attributes
            playableBeatmap.BeatmapInfo.Difficulty = playableBeatmap.Difficulty.Clone();
            if (starRating != null)
                playableBeatmap.BeatmapInfo.StarRating = starRating.Value;
        }

        public override IBeatmap GetPlayableBeatmap(IRulesetInfo ruleset, IReadOnlyList<Mod> mods, CancellationToken cancellationToken)
            => playableBeatmap;

        protected override IBeatmap GetBeatmap() => playableBeatmap;

        public override Texture GetBackground() => source.GetBackground();

        public override Texture GetPanelBackground() => source.GetPanelBackground();

        protected override Track GetBeatmapTrack() => source.LoadTrack();

        protected internal override ISkin GetSkin() => source.Skin;

        public override Stream GetStream(string storagePath) => source.GetStream(storagePath);

        protected override Waveform GetWaveform() => source.Waveform;

        protected override Storyboard GetStoryboard() => source.Storyboard;
    }
}
