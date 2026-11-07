// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.IO;
using System.Text;
using NUnit.Framework;
using osu.Framework.Audio.Track;
using osu.Framework.Graphics.Containers;
using osu.Framework.Timing;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Beatmaps.Formats;
using osu.Game.IO;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;

namespace osu.Game.Tests.Visual.Gameplay
{
    public partial class TestSceneNightcoreSignedMeter : OsuTestScene
    {
        [TestCase(-536870912)]
        [TestCase(-int.MaxValue)]
        [TestCase(-3)]
        [TestCase(-4)]
        [TestCase(-5)]
        [TestCase(3)]
        [TestCase(4)]
        public void TestSegmentAlignmentOnSyncedBeats(int meter)
        {
            TestBeatSyncProvider provider = null!;
            TestNightcoreBeatContainer nightcore = null!;

            AddStep("load imported meter and running clock", () =>
            {
                using var stream = new MemoryStream(Encoding.UTF8.GetBytes($"[TimingPoints]\n3552,400,{meter},1,3,60,1,0"));
                using var reader = new LineBufferedReader(stream);
                var beatmap = new LegacyBeatmapDecoder(14).Decode(reader);
                Assert.That(beatmap.ControlPointInfo.TimingPoints.Count, Is.EqualTo(1));
                var point = beatmap.ControlPointInfo.TimingPoints[0];
                Assert.That(point.LegacyTimeSignature ?? point.TimeSignature.Numerator, Is.EqualTo(meter));

                Child = provider = new TestBeatSyncProvider(beatmap.ControlPointInfo)
                {
                    Child = nightcore = new TestNightcoreBeatContainer()
                };

                // First callback aligns at the largest positive int beat, not merely segment zero.
                provider.ManualClock.CurrentTime = 3552 + (double)int.MaxValue * 200;
            });

            AddUntilStep("large beat completes actual Nightcore scheduling", () => nightcore.LastCompletedBeat == int.MaxValue);
            AddAssert("callback used running beat source", () => nightcore.CompletedWhileSynced);
            AddAssert("default half-beat divisor", () => nightcore.Divisor == 2);

            AddStep("seek backwards to realign segment", () => provider.ManualClock.CurrentTime = 3552 + 33 * 200);
            AddUntilStep("backward alignment completes", () => nightcore.LastCompletedBeat == 33);
            AddStep("seek to first beat", () => provider.ManualClock.CurrentTime = 3552);
            AddUntilStep("zero beat selection completes", () => nightcore.LastCompletedBeat == 0);
            AddStep("advance to clap beat", () => provider.ManualClock.CurrentTime = 3552 + 2 * 200);
            AddUntilStep("clap beat selection completes", () => nightcore.LastCompletedBeat == 2);
        }

        private partial class TestBeatSyncProvider : Container, IBeatSyncProvider
        {
            public readonly ManualClock ManualClock = new ManualClock { IsRunning = true, Rate = 1 };

            public ChannelAmplitudes CurrentAmplitudes { get; } = new ChannelAmplitudes();
            public ControlPointInfo ControlPoints { get; }
            IClock IBeatSyncProvider.Clock => ManualClock;

            public TestBeatSyncProvider(ControlPointInfo controlPoints)
            {
                ControlPoints = controlPoints;
            }
        }

        private partial class TestNightcoreBeatContainer : ModNightcore<HitObject>.NightcoreBeatContainer
        {
            public int? LastCompletedBeat { get; private set; }
            public bool CompletedWhileSynced { get; private set; }

            protected override void OnNewBeat(int beatIndex, TimingControlPoint timingPoint, EffectControlPoint effectPoint, ChannelAmplitudes amplitudes)
            {
                // Run production alignment and sample selection before recording completion.
                base.OnNewBeat(beatIndex, timingPoint, effectPoint, amplitudes);
                LastCompletedBeat = beatIndex;
                CompletedWhileSynced = IsBeatSyncedWithTrack;
            }
        }
    }
}
