// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Timing;

namespace osu.Game.Recording.Export
{
    /// <summary>
    /// Output-time clock for visuals which normally use elapsed real time rather than beatmap time.
    /// </summary>
    public sealed class ReplayExportPresentationClock : FramedClock
    {
        private readonly ManualClock source;

        public ReplayExportPresentationClock()
            : this(new ManualClock { IsRunning = true })
        {
        }

        private ReplayExportPresentationClock(ManualClock source)
            : base(source)
        {
            this.source = source;
        }

        internal void AdvanceTo(double milliseconds)
        {
            source.CurrentTime = milliseconds;
            ProcessFrame();
        }
    }
}
