// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using osu.Framework.Bindables;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Beatmaps.Formats;
using osu.Game.Beatmaps.Legacy;
using osu.Game.IO;
using osu.Game.IO.Serialization;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Catch;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Difficulty;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Taiko;
using osu.Game.Screens.Edit;

namespace osu.Game.Tests.Beatmaps.Formats
{
    [TestFixture]
    public class LegacyTimingControlPointTest
    {
        [TestCase(0.01)]
        [TestCase(6)]
        [TestCase(60000)]
        [TestCase(60001)]
        [TestCase(0)]
        [TestCase(-50)]
        [TestCase(2147483647)]
        public void TestImportedRedlineAndStandardSlider(double rawBeatLength)
        {
            var beatmap = decode(rawBeatLength);
            var point = beatmap.ControlPointInfo.TimingPoints.Single();
            double boundedBeatLength = Math.Clamp(rawBeatLength, 6, 60000);
            double effectiveBeatLength = rawBeatLength > 0 ? rawBeatLength : 1000;

            Assert.That(point.LegacyBeatLength, Is.EqualTo(rawBeatLength));
            Assert.That(point.BeatLength, Is.EqualTo(boundedBeatLength));
            Assert.That(point.BPM, Is.EqualTo(60000 / boundedBeatLength));
            Assert.That(beatmap.ControlPointInfo.BPMMinimum, Is.EqualTo(point.BPM));
            Assert.That(beatmap.ControlPointInfo.BPMMaximum, Is.EqualTo(point.BPM));
            Assert.That(beatmap.GetMostCommonBeatLength(), Is.EqualTo(boundedBeatLength));
            Assert.That(((LegacyControlPointInfo)beatmap.ControlPointInfo).DifficultyPointAt(1000).SliderVelocity, Is.EqualTo(1));

            var slider = standardSlider(beatmap);
            Assert.That(slider.Velocity, Is.EqualTo(100 / effectiveBeatLength).Within(1e-10));
            Assert.That(slider.Duration, Is.EqualTo(2 * effectiveBeatLength).Within(1e-8));
            Assert.That(slider.TickDistance, Is.EqualTo(100).Within(1e-10));
            Assert.That(slider.NestedHitObjects.OfType<SliderTick>().Count(), Is.EqualTo(rawBeatLength == 0.01 || rawBeatLength == 6 ? 0 : 1));
        }

        [TestCase(500, 250)]
        [TestCase(0.01, 0.005)]
        [TestCase(60001, 30000.5)]
        [TestCase(0, 1000)]
        [TestCase(-50, 1000)]
        public void TestInheritedVelocityAppliedBeforeNonPositiveFallback(double redline, double effectiveBeatLength)
        {
            var slider = standardSlider(decode(redline, "500,-50,4,1,0,100,0,0"));
            Assert.That(slider.SliderVelocityMultiplier, Is.EqualTo(2));
            Assert.That(slider.Velocity, Is.EqualTo(100 / effectiveBeatLength).Within(1e-10));
            Assert.That(slider.Duration, Is.EqualTo(2 * effectiveBeatLength).Within(1e-8));
            Assert.That(slider.TickDistance, Is.EqualTo(redline > 0 ? 200 : 100).Within(1e-10));
        }

        [Test]
        public void TestNaNGreenDisablesTicksAndLaterGreenRestoresThem()
        {
            string content = source(500, "500,NaN,4,1,0,100,0,0\n3000,-100,4,1,0,100,0,0")
                             + "\n256,192,3000,2,0,L|456:192,1,200";
            var beatmap = decodeText(content);
            var sliders = new TestWorkingBeatmap(beatmap).GetPlayableBeatmap(new OsuRuleset().RulesetInfo).HitObjects.Cast<Slider>().ToArray();
            Assert.That(sliders, Has.Length.EqualTo(2));
            Assert.That(sliders[0].GenerateTicks, Is.False);
            Assert.That(sliders[0].SliderVelocityMultiplier, Is.EqualTo(1));
            Assert.That(sliders[0].Velocity, Is.EqualTo(0.2));
            Assert.That(sliders[0].NestedHitObjects.OfType<SliderTick>(), Is.Empty);
            Assert.That(sliders[1].GenerateTicks, Is.True);
            Assert.That(sliders[1].NestedHitObjects.OfType<SliderTick>().Count(), Is.EqualTo(1));
        }

        [TestCase(0.01)]
        [TestCase(60001)]
        [TestCase(0)]
        [TestCase(-50)]
        public void TestRateModsDoNotDoubleApplyTiming(double rawBeatLength)
        {
            foreach (ModRateAdjust mod in new ModRateAdjust[] { new OsuModDoubleTime(), new OsuModHalfTime() })
            {
                var slider = standardSlider(decode(rawBeatLength), mod);
                double duration = 2 * (rawBeatLength > 0 ? rawBeatLength : 1000);
                Assert.That(slider.Duration, Is.EqualTo(duration).Within(1e-8));
                double clockRate = mod.ApplyToRate(slider.StartTime, 1);
                Assert.That(clockRate, Is.EqualTo(mod is OsuModDoubleTime ? 1.5 : 0.75));
                Assert.That(slider.Duration / clockRate, Is.EqualTo(duration / clockRate).Within(1e-8));
            }
        }

        [TestCase(0.01)]
        [TestCase(60001)]
        [TestCase(0)]
        [TestCase(-50)]
        public void TestOtherModesKeepBoundedTiming(double rawBeatLength)
        {
            foreach (Ruleset ruleset in new Ruleset[] { new TaikoRuleset(), new CatchRuleset(), new ManiaRuleset() })
            {
                var imported = decode(rawBeatLength, mode: ruleset.RulesetInfo.OnlineID);
                // HEAD interpreted a negative native redline as SV as well as bounded timing.
                var bounded = decode(Math.Clamp(rawBeatLength, 6, 60000), rawBeatLength < 0 ? "0,-50,4,1,0,100,0,0" : "", ruleset.RulesetInfo.OnlineID);
                var playable = new TestWorkingBeatmap(imported).GetPlayableBeatmap(ruleset.RulesetInfo);
                var reference = new TestWorkingBeatmap(bounded).GetPlayableBeatmap(ruleset.RulesetInfo);
                Assert.That(playable.HitObjects, Is.Not.Empty);
                Assert.That(playable.HitObjects.Select(h => h.GetType()), Is.EqualTo(reference.HitObjects.Select(h => h.GetType())));
                Assert.That(playable.HitObjects.Select(h => h.GetEndTime()), Is.EqualTo(reference.HitObjects.Select(h => h.GetEndTime())));
                Assert.That(playable.HitObjects.Select(h => h.NestedHitObjects.Count), Is.EqualTo(reference.HitObjects.Select(h => h.NestedHitObjects.Count)));
                Assert.That(imported.ControlPointInfo.EffectPointAt(1000).ScrollSpeed,
                    Is.EqualTo(rawBeatLength < 0 && ruleset.RulesetInfo.OnlineID != 2 ? 2 : 1));
            }
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void TestNativeNegativeRedlinePreservesHeadBehaviour(int mode)
        {
            var beatmap = decode(-50, mode: mode);
            Assert.That(((LegacyControlPointInfo)beatmap.ControlPointInfo).DifficultyPointAt(1000).SliderVelocity, Is.EqualTo(2));
            Assert.That(((IHasDuration)beatmap.HitObjects.Single()).Duration, Is.EqualTo(6));
            Ruleset ruleset = mode == 1 ? new TaikoRuleset() : mode == 2 ? new CatchRuleset() : new ManiaRuleset();
            var playable = new TestWorkingBeatmap(beatmap).GetPlayableBeatmap(ruleset.RulesetInfo);
            Assert.That(playable.HitObjects.Single().GetEndTime(), Is.EqualTo(1006));
            Assert.That(playable.ControlPointInfo.EffectPointAt(1000).ScrollSpeed, Is.EqualTo(mode == 2 ? 1 : 2));
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void TestStandardNegativeRedlineConvertedToOtherModes(int mode)
        {
            var beatmap = decode(-50);
            Assert.That(((LegacyControlPointInfo)beatmap.ControlPointInfo).DifficultyPointAt(1000).SliderVelocity, Is.EqualTo(1));
            Assert.That(((IHasDuration)beatmap.HitObjects.Single()).Duration, Is.EqualTo(12));
            Ruleset ruleset = mode == 1 ? new TaikoRuleset() : mode == 2 ? new CatchRuleset() : new ManiaRuleset();
            var playable = new TestWorkingBeatmap(beatmap).GetPlayableBeatmap(ruleset.RulesetInfo);
            Assert.That(playable.HitObjects, Is.Not.Empty);
            // Mania converts a sub-80ms span to one instantaneous note (SliderPatternGenerator).
            Assert.That(playable.HitObjects.Max(h => h.GetEndTime()), Is.EqualTo(mode == 3 ? 1000 : 1012));
            Assert.That(playable.ControlPointInfo.EffectPointAt(1000).ScrollSpeed, Is.EqualTo(1));
        }

        [TestCase(0.01, 1008, "normal", 100)]
        [TestCase(120000, 150000, "soft", 20)]
        [TestCase(0, 1500, "soft", 20)]
        [TestCase(-50, 1500, "soft", 20)]
        public void TestNodeSamplesUseStandardPlaybackTimeline(double redline, double changeTime, string repeatBank, int repeatVolume)
        {
            string timing = FormattableString.Invariant($"{changeTime},-100,4,2,0,20,0,0");
            var beatmap = decodeText(source(redline, timing).Replace(",1,200", ",2,200", StringComparison.Ordinal));
            var slider = standardSlider(beatmap);
            double beatLength = redline > 0 ? redline : 1000;
            Assert.That(slider.HeadCircle.StartTime, Is.EqualTo(1000));
            Assert.That(slider.NestedHitObjects.OfType<SliderRepeat>().Single().StartTime, Is.EqualTo(1000 + 2 * beatLength).Within(1e-8));
            Assert.That(slider.EndTime, Is.EqualTo(1000 + 4 * beatLength).Within(1e-8));
            assertSample(slider.HeadCircle.Samples.Single(), "normal", 100);
            assertSample(slider.NestedHitObjects.OfType<SliderRepeat>().Single().Samples.Single(), repeatBank, repeatVolume);
            assertSample(slider.TailSamples.Single(), repeatBank, repeatVolume);
            var decodedNodes = ((IHasRepeats)beatmap.HitObjects.Single()).NodeSamples;
            assertSample(decodedNodes[1].Single(), repeatBank, repeatVolume);
            assertSample(decodedNodes[2].Single(), repeatBank, repeatVolume);
        }

        [TestCase(7, false)]
        [TestCase(7, true)]
        [TestCase(14, false)]
        [TestCase(14, true)]
        public void TestNodeSamplePrecisionAndSameTimeOrdering(int version, bool afterRepeatLeniency)
        {
            const double raw = 120000;
            const double inherited = -33.333333333;
            double effectiveBeatLength = raw * ((float)-inherited / 100.0);
            double repeatTime = 1000 + 2 * effectiveBeatLength;
            double changeTime = repeatTime + LegacyBeatmapDecoder.CONTROL_POINT_LENIENCY + (afterRepeatLeniency ? 0.000001 : 0);
            string timing = FormattableString.Invariant($"500,{inherited},4,1,0,100,0,0\n{changeTime},500,4,3,0,70,1,0\n{changeTime},-100,4,2,0,20,0,0");
            var slider = standardSlider(decodeText(source(raw, timing).Replace(",1,200", ",2,200", StringComparison.Ordinal), version));
            Assert.That(slider.NestedHitObjects.OfType<SliderRepeat>().Single().StartTime, Is.EqualTo(repeatTime));
            Assert.That(slider.EndTime, Is.EqualTo(1000 + 4 * effectiveBeatLength));
            Assert.That(slider.TickDistance, Is.EqualTo(version < 8 ? 100 : 100 * raw / effectiveBeatLength).Within(0.00001));
            assertSample(slider.HeadCircle.Samples.Single(), "normal", 100);
            assertSample(slider.NestedHitObjects.OfType<SliderRepeat>().Single().Samples.Single(), afterRepeatLeniency ? "normal" : "soft", afterRepeatLeniency ? 100 : 20);
            assertSample(slider.TailSamples.Single(), "soft", 20);
        }

        [TestCase(0)]
        [TestCase(200)]
        public void TestCatmullNodeSamplesAndSourcePathImmutability(int length)
        {
            string content = source(120000).Replace("L|456:192,1,200", $"C|356:192|356:192|456:192,2,{length}", StringComparison.Ordinal);
            var baseline = standardSlider(decodeText(content));
            double changeTime = baseline.NestedHitObjects.OfType<SliderRepeat>().Single().StartTime + LegacyBeatmapDecoder.CONTROL_POINT_LENIENCY;
            string timing = FormattableString.Invariant($"{changeTime},-100,4,2,0,20,0,0");
            var beatmap = decodeText(content.Replace("[HitObjects]", timing + Environment.NewLine + "[HitObjects]", StringComparison.Ordinal));
            var path = ((IHasPath)beatmap.HitObjects.Single()).Path;
            var sourcePath = ((IHasPath)decodeText(content.Replace("Mode:0", "Mode:2", StringComparison.Ordinal)).HitObjects.Single()).Path;
            Assert.That(path.OptimiseCatmull, Is.False);
            Assert.That(path.ExpectedDistance.Value, Is.EqualTo(length == 0 ? null : (double?)length));
            Assert.That(path.Distance, Is.EqualTo(sourcePath.Distance));
            Assert.That(path.ControlPoints.Select(p => (p.Position, p.Type)), Is.EqualTo(sourcePath.ControlPoints.Select(p => (p.Position, p.Type))));
            var slider = standardSlider(beatmap);
            Assert.That(slider.EndTime, Is.EqualTo(baseline.EndTime));
            assertSample(slider.HeadCircle.Samples.Single(), "normal", 100);
            assertSample(slider.NestedHitObjects.OfType<SliderRepeat>().Single().Samples.Single(), "soft", 20);
            assertSample(slider.TailSamples.Single(), "soft", 20);
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void TestNativeNodeSamplesRemainOnBoundedTimeline(int mode)
        {
            var beatmap = decodeText(source(0.01, "1008,-100,4,2,0,20,0,0", mode).Replace(",1,200", ",2,200", StringComparison.Ordinal));
            var nodes = ((IHasRepeats)beatmap.HitObjects.Single()).NodeSamples;
            assertSample(nodes[0].Single(), "normal", 100);
            assertSample(nodes[1].Single(), "soft", 20);
            assertSample(nodes[2].Single(), "soft", 20);
        }

        [Test]
        public void TestStandardSourceSamplesRemainSourceResolvedWhenConvertedToCatch()
        {
            var beatmap = decodeText(source(0.01, "1008,-100,4,2,0,20,0,0").Replace(",1,200", ",2,200", StringComparison.Ordinal));
            var stream = new TestWorkingBeatmap(beatmap).GetPlayableBeatmap(new CatchRuleset().RulesetInfo).HitObjects.Single();
            Assert.That(stream.GetEndTime(), Is.EqualTo(1024));
            var nodes = ((IHasRepeats)stream).NodeSamples;
            assertSample(nodes[1].Single(), "normal", 100);
            assertSample(nodes[2].Single(), "normal", 100);
        }

        [TestCase(1e-14)]
        [TestCase(1e-300)]
        public void TestRepresentableTinyTimingRetained(double beatLength)
        {
            var beatmap = decode(beatLength);
            Assert.That(beatmap.ControlPointInfo.TimingPoints.Single().LegacyBeatLength, Is.EqualTo(beatLength));
            var slider = standardSlider(beatmap);
            Assert.That(slider.Velocity, Is.EqualTo(100 / beatLength));
            Assert.That(slider.Duration, Is.Zero);
            Assert.That(slider.NestedHitObjects.All(h => double.IsFinite(h.StartTime)), Is.True);
            Assert.That(slider.NestedHitObjects.OfType<SliderTick>(), Is.Empty);
            var attributes = new OsuRuleset().CreateDifficultyCalculator(new TestWorkingBeatmap(beatmap)).Calculate();
            Assert.That(double.IsFinite(attributes.StarRating), Is.True);
        }

        [TestCase(1e-14)]
        [TestCase(1e-300)]
        public void TestTinyTimingSliderParticipatesInDifficultyProcessing(double beatLength)
        {
            string content = source(beatLength).Replace("[HitObjects]", "[HitObjects]\n64,64,500,1,0", StringComparison.Ordinal)
                             + "\n448,320,1500,1,0";
            var beatmap = decodeText(content);
            var playable = new TestWorkingBeatmap(beatmap).GetPlayableBeatmap(new OsuRuleset().RulesetInfo);
            Assert.That(playable.HitObjects, Has.Count.EqualTo(3));
            Assert.That(playable.HitObjects[0], Is.TypeOf<HitCircle>());
            Assert.That(playable.HitObjects[1], Is.TypeOf<Slider>());
            Assert.That(((Slider)playable.HitObjects[1]).Duration, Is.Zero);

            // Difficulty construction starts at index 1, so this slider is preprocessed rather
            // than omitted as it is in the single-object decode/nested regression above.
            var attributes = (OsuDifficultyAttributes)new OsuRuleset().CreateDifficultyCalculator(new TestWorkingBeatmap(beatmap)).Calculate();
            Assert.That(attributes.SliderCount, Is.EqualTo(1));
            Assert.That(attributes.HitCircleCount, Is.EqualTo(2));
            Assert.That(double.IsFinite(attributes.StarRating), Is.True);
            Assert.That(double.IsFinite(attributes.AimDifficulty), Is.True);
            Assert.That(double.IsFinite(attributes.SpeedDifficulty), Is.True);
            Assert.That(double.IsFinite(attributes.ReadingDifficulty), Is.True);
            Assert.That(double.IsFinite(attributes.SliderFactor), Is.True);
        }

        private static void assertSample(HitSampleInfo sample, string bank, int volume)
        {
            Assert.That(sample.Bank, Is.EqualTo(bank));
            Assert.That(sample.Volume, Is.EqualTo(volume));
        }

        [TestCase(0.01)]
        [TestCase(60001)]
        [TestCase(0)]
        [TestCase(-50)]
        public void TestBoundedBarlineConsumer(double rawBeatLength)
        {
            var beatmap = decode(rawBeatLength);
            var bars = new BarLineGenerator<osu.Game.Rulesets.Mania.Objects.BarLine>(beatmap).BarLines;
            var reference = new BarLineGenerator<osu.Game.Rulesets.Mania.Objects.BarLine>(decode(Math.Clamp(rawBeatLength, 6, 60000))).BarLines;
            Assert.That(bars, Is.Not.Empty);
            Assert.That(bars.Count, Is.LessThan(100));
            Assert.That(bars.Select(b => b.StartTime), Is.EqualTo(reference.Select(b => b.StartTime)));
        }

        [TestCase(0.01)]
        [TestCase(60001)]
        [TestCase(0)]
        [TestCase(-50)]
        public void TestCloneJsonAndEditReset(double rawBeatLength)
        {
            var point = decode(rawBeatLength).ControlPointInfo.TimingPoints.Single();
            var clone = (TimingControlPoint)point.DeepClone();
            Assert.That(clone, Is.EqualTo(point));
            Assert.That(clone.GetHashCode(), Is.EqualTo(point.GetHashCode()));
            clone.CopyFrom(clone);
            Assert.That(clone, Is.EqualTo(point));
            clone.BeatLengthBindable.Value = clone.BeatLength;
            Assert.That(clone.LegacyBeatLength, Is.EqualTo(rawBeatLength));
            var restored = point.Serialize().Deserialize<TimingControlPoint>();
            Assert.That(restored, Is.EqualTo(point));

            int changes = 0;
            clone.Changed += _ => changes++;
            clone.BeatLength = clone.BeatLength;
            Assert.That(clone.LegacyBeatLength, Is.Null);
            Assert.That(changes, Is.EqualTo(1));
            Assert.That(clone, Is.Not.EqualTo(point));
            Assert.That(point.LegacyBeatLength, Is.EqualTo(rawBeatLength));
            clone.CopyFrom(point);
            Assert.That(clone, Is.EqualTo(point));
            clone.BeatLengthBindable.Value = 500;
            Assert.That(clone.LegacyBeatLength, Is.Null);
        }

        [TestCase("-100", 1, true)]
        [TestCase("-50", 2, true)]
        [TestCase("NaN", 1, false)]
        public void TestSameTimeInheritedPointAfterNegativeRedline(string inheritedBeatLength, double velocityMultiplier, bool generateTicks)
        {
            var beatmap = decode(-50, $"0,{inheritedBeatLength},4,1,0,100,0,0");
            var slider = standardSlider(beatmap);
            Assert.That(slider.SliderVelocityMultiplier, Is.EqualTo(velocityMultiplier));
            Assert.That(slider.GenerateTicks, Is.EqualTo(generateTicks));
            Assert.That(slider.Velocity, Is.EqualTo(0.1));
            Assert.That(slider.Duration, Is.EqualTo(2000));
            Assert.That(slider.NestedHitObjects.OfType<SliderTick>().Count(), Is.EqualTo(generateTicks ? 1 : 0));
        }

        [Test]
        public void TestNegativeRedlineResetsPreviousInheritedVelocity()
        {
            var beatmap = decodeText(source(500, "500,-25,4,1,0,100,0,0" + Environment.NewLine + "750,-50,4,1,0,100,1,0"));
            var slider = standardSlider(beatmap);
            Assert.That(slider.SliderVelocityMultiplier, Is.EqualTo(1));
            Assert.That(slider.Velocity, Is.EqualTo(0.1));
            Assert.That(slider.Duration, Is.EqualTo(2000));
            Assert.That(beatmap.ControlPointInfo.TimingPointAt(1000).LegacyBeatLength, Is.EqualTo(-50));
        }

        [Test]
        public void TestUnchangedBpmCommitClearsOverride()
        {
            var point = decode(0.01).ControlPointInfo.TimingPoints.Single();
            var bpm = new BindableDouble(60000 / point.BeatLength);
            // TapTimingControl.setBpm assigns BeatLength; FormDiscreteAdjustmentControl triggers
            // this callback even when committing the same displayed number.
            bpm.BindValueChanged(change => point.BeatLength = 60000 / change.NewValue);
            Assert.That(point.LegacyBeatLength, Is.EqualTo(0.01));
            bpm.TriggerChange();
            Assert.That(point.BeatLength, Is.EqualTo(6));
            Assert.That(point.LegacyBeatLength, Is.Null);
        }

        [TestCase(0.01)]
        [TestCase(60001)]
        [TestCase(0)]
        [TestCase(-50)]
        public void TestEditorUndoPatcherRestoresRawTiming(double rawBeatLength)
        {
            var original = source(rawBeatLength);
            var edited = source(500);
            using var editor = new EditorBeatmap(new TestWorkingBeatmap(decodeText(original)).GetPlayableBeatmap(new OsuRuleset().RulesetInfo));
            var patcher = new LegacyEditorBeatmapPatcher(editor);
            patcher.Patch(Encoding.UTF8.GetBytes(original), Encoding.UTF8.GetBytes(edited));
            Assert.That(editor.ControlPointInfo.TimingPoints.Single().LegacyBeatLength, Is.EqualTo(500));
            patcher.Patch(Encoding.UTF8.GetBytes(edited), Encoding.UTF8.GetBytes(original));
            Assert.That(editor.ControlPointInfo.TimingPoints.Single().LegacyBeatLength, Is.EqualTo(rawBeatLength));
        }

        [TestCase("∞")]
        [TestCase("+∞")]
        [TestCase("-∞")]
        [TestCase("Infinity")]
        [TestCase("-Infinity")]
        public void TestInfiniteRedlinesStillRejected(string value)
        {
            var beatmap = decodeText(source(500).Replace("0,500,4", $"0,{value},4", StringComparison.Ordinal));
            Assert.That(beatmap.ControlPointInfo.TimingPoints, Is.Empty);
            Assert.That(standardSlider(beatmap).Velocity, Is.EqualTo(0.1));
        }

        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        public void TestRawPropertyRejectsNonFiniteValues(double value)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new TimingControlPoint { LegacyBeatLength = value });
        }

        [Test]
        public void TestNaNRedlinePreservesStateAndUsesStableVelocity()
        {
            var beatmap = decodeText(source(500, "500,NaN,4,2,3,46,1,9\n3000,400,4,1,0,100,1,0"));
            var points = (LegacyControlPointInfo)beatmap.ControlPointInfo;
            var point = points.TimingPointAt(1000);
            Assert.That(point.LegacyBeatLength, Is.NaN);
            Assert.That(point.BeatLength, Is.EqualTo(TimingControlPoint.DEFAULT_BEAT_LENGTH));
            Assert.That(point.OmitFirstBarLine, Is.True);
            Assert.That(points.SamplePointAt(1000).SampleBank, Is.EqualTo("soft"));
            Assert.That(points.SamplePointAt(1000).SampleVolume, Is.EqualTo(46));
            Assert.That(points.SamplePointAt(1000).ApplyTo(new osu.Game.Rulesets.Objects.Legacy.ConvertHitObjectParser.LegacyHitSampleInfo("hitnormal")).LookupNames.First(), Does.Contain("3"));
            Assert.That(points.EffectPointAt(1000).KiaiMode, Is.True);
            Assert.That(points.DifficultyPointAt(1000).GenerateTicks, Is.False);
            Assert.That(points.DifficultyPointAt(3000).GenerateTicks, Is.True);
            Assert.That(points.TimingPointAt(3000).LegacyBeatLength, Is.EqualTo(400));

            var slider = standardSlider(beatmap);
            Assert.That(slider.Velocity, Is.EqualTo(0.1));
            Assert.That(slider.Duration, Is.EqualTo(2000));
            Assert.That(slider.NestedHitObjects.OfType<SliderTick>(), Is.Empty);
            Assert.That(slider.NestedHitObjects.All(h => double.IsFinite(h.StartTime)), Is.True);

            var clone = (TimingControlPoint)point.DeepClone();
            Assert.That(clone, Is.EqualTo(point));
            Assert.That(clone.GetHashCode(), Is.EqualTo(point.GetHashCode()));
            var restored = point.Serialize().Deserialize<TimingControlPoint>();
            restored.Time = point.Time;
            Assert.That(restored, Is.EqualTo(point));
            int changes = 0;
            clone.Changed += _ => changes++;
            clone.LegacyBeatLength = double.NaN;
            Assert.That(changes, Is.Zero);
            clone.BeatLength = 500;
            Assert.That(clone.LegacyBeatLength, Is.Null);

            using var output = new StringWriter();
            new LegacyBeatmapEncoder(beatmap, null, null).Encode(output);
            var roundtrip = decodeText(output.ToString());
            Assert.That(roundtrip.ControlPointInfo.TimingPointAt(1000).LegacyBeatLength, Is.NaN);
            Assert.That(((LegacyControlPointInfo)roundtrip.ControlPointInfo).DifficultyPointAt(1000).GenerateTicks, Is.False);
        }

        [TestCase("NaN", float.NaN)]
        [TestCase("-1", 0)]
        [TestCase("2", 1)]
        [TestCase("0.7", 0.7f)]
        public void TestStableStackLeniency(string value, float expected)
        {
            var beatmap = decodeText(source(500).Replace("[General]", $"[General]\nStackLeniency: {value}", StringComparison.Ordinal));
            Assert.That(beatmap.StackLeniency, Is.EqualTo(expected));
            var playable = new TestWorkingBeatmap(beatmap).GetPlayableBeatmap(new OsuRuleset().RulesetInfo);
            Assert.That(playable.StackLeniency, Is.EqualTo(expected));
        }

        [Test]
        public void TestMissingTimingUsesDefault()
        {
            var beatmap = decodeText(source(500).Replace("0,500,4,1,0,100,1,0", string.Empty, StringComparison.Ordinal));
            Assert.That(beatmap.ControlPointInfo.TimingPoints, Is.Empty);
            Assert.That(TimingControlPoint.DEFAULT.LegacyBeatLength, Is.Null);
            Assert.That(standardSlider(beatmap).Duration, Is.EqualTo(2000));
            Assert.Throws<InvalidOperationException>(() => TimingControlPoint.DEFAULT.LegacyBeatLength = 0.01);
        }

        [TestCase("1e-320", "")]
        [TestCase("5e-324", "500,-10,4,1,0,100,0,0")]
        public void TestExtremePositiveTimingDoesNotRejectBeatmap(string rawBeatLength, string inherited)
        {
            string content = source(500, inherited).Replace("0,500,4", $"0,{rawBeatLength},4", StringComparison.Ordinal);
            var beatmap = decodeText(content);
            Assert.That(beatmap.HitObjects, Has.Count.EqualTo(1));
            Assert.That(beatmap.ControlPointInfo.TimingPoints.Single().LegacyBeatLength,
                Is.EqualTo(double.Parse(rawBeatLength, System.Globalization.CultureInfo.InvariantCulture)));
        }

        private static Slider standardSlider(Beatmap beatmap, params Mod[] mods) =>
            (Slider)new TestWorkingBeatmap(beatmap).GetPlayableBeatmap(new OsuRuleset().RulesetInfo, mods).HitObjects.Single();

        private static Beatmap decode(double rawBeatLength, string inherited = "", int mode = 0) => decodeText(source(rawBeatLength, inherited, mode));

        private static Beatmap decodeText(string content, int version = 14)
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
            using var reader = new LineBufferedReader(stream);
            return new LegacyBeatmapDecoder(version).Decode(reader);
        }

        private static string source(double rawBeatLength, string inherited = "", int mode = 0) => FormattableString.Invariant($@"osu file format v14
[General]
Mode:{mode}
[Difficulty]
CircleSize:4
OverallDifficulty:5
SliderMultiplier:1
SliderTickRate:1
[TimingPoints]
0,{rawBeatLength},4,1,0,100,1,0
{inherited}
[HitObjects]
256,192,1000,2,0,L|456:192,1,200");
    }
}
