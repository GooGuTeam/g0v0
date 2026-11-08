// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Diagnostics;
using System.Text;
using NUnit.Framework;
using osu.Game.Beatmaps.Formats;
using osu.Game.IO;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Legacy;
using osu.Game.Rulesets.Objects.Types;
using osuTK;

namespace osu.Game.Tests.Beatmaps.Formats
{
    [TestFixture]
    public class LegacySliderCoordinatesTest
    {
        [TestCase("67142400", "33587596", 67142400f, 33587596f)]
        [TestCase("-67142400", "-33587596", -67142400f, -33587596f)]
        [TestCase("131073", "-131073", 131073f, -131073f)]
        [TestCase("255.999999", "-255.999999", 255f, -255f)]
        [TestCase("6.71424E7", "-3.3587596E7", 67142400f, -33587596f)]
        [TestCase(" +131073 ", " -131073 ", 131073f, -131073f)]
        [TestCase("2147483647", "-2147483647", 2147483648f, -2147483648f)]
        public void TestLegacyControlPointsUseDoubleBeforeIntegerTruncation(string x, string y, float expectedX, float expectedY)
        {
            var slider = decode($"L|{x}:{y}", 14);
            Assert.That(slider.Path.ControlPoints[1].Position, Is.EqualTo(new Vector2(expectedX, expectedY)));
        }

        [Test]
        public void TestLazerControlPointsRetainLargeCoordinatesAndFractions()
        {
            var slider = decode("L|67142400:33587596|255.75:-255.75", LegacyBeatmapEncoder.FIRST_LAZER_VERSION);
            Assert.That(slider.Path.ControlPoints[1].Position, Is.EqualTo(new Vector2(67142400, 33587596)));
            Assert.That(slider.Path.ControlPoints[2].Position, Is.EqualTo(new Vector2(255.75f, -255.75f)));
        }

        [Test]
        public void TestFoldedAspirePathRetainsLargeDeclaredLength()
        {
            var slider = decode("L|67142400:0|67142400:33587596|67142400:0|0:0|0:100729540", 14);
            Assert.That(slider.Path.ControlPoints, Has.Count.EqualTo(6));
            Assert.That(slider.Path.ExpectedDistance.Value, Is.EqualTo(3617087291392d));
        }

        [Test]
        public void TestControlPointChangesStillInvalidatePath()
        {
            var point = new PathControlPoint(new Vector2(100, 0));
            var path = new SliderPath(new[] { new PathControlPoint(Vector2.Zero, PathType.LINEAR), point });
            Assert.That(path.Distance, Is.EqualTo(100));
            point.Position = new Vector2(200, 0);
            Assert.That(path.Distance, Is.EqualTo(200));
            path.ControlPoints.Remove(point);
            int version = path.Version.Value;
            point.Position = new Vector2(300, 0);
            Assert.That(path.Version.Value, Is.EqualTo(version), "Removed points must unsubscribe the shared handler.");
        }

        [Test]
        public void TestLargeLinearPathAllocation()
        {
            const int point_count = 100000;
            var builder = new StringBuilder("L");
            for (int i = 0; i < point_count; i++)
                builder.Append(i % 2 == 0 ? "|67142400:33587596" : "|0:0");

            string path = builder.ToString();
            decode(path, 14); // Warm the parser and shared array pools before measuring transient allocations.
            long before = GC.GetAllocatedBytesForCurrentThread();
            var slider = decode(path, 14);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.That(slider.Path.ControlPoints, Has.Count.EqualTo(point_count + 1));
            // Shared pools may be trimmed by a GC during the surrounding decoder suite.
            // Leave room for cold buffers while still rejecting the previous ~48 MB allocation.
            Assert.That(allocated, Is.LessThan(36_000_000), $"Allocated {allocated:N0} bytes for {point_count:N0} points.");
            TestContext.WriteLine($"Allocated {allocated:N0} bytes for {point_count:N0} points.");
        }

        [Test, Explicit("Large synthetic import benchmark; not part of normal CI.")]
        public void TestHundredMegabyteDecodeTime()
        {
            const int slider_count = 1280;
            const int points_per_slider = 6800;
            using var stream = new MemoryStream();
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false), 65536, leaveOpen: true))
            {
                writer.WriteLine("osu file format v14\n[Difficulty]\nSliderMultiplier:1\nSliderTickRate:1\n[TimingPoints]\n0,500,4,1,0,100,1,0\n[HitObjects]");
                var path = new StringBuilder("L");
                for (int i = 0; i < points_per_slider; i++)
                    path.Append(i % 2 == 0 ? "|67142400:33587596" : "|430:174");
                string pathText = path.ToString();
                for (int i = 0; i < slider_count; i++)
                    writer.WriteLine($"430,174,{1000 + i},2,0,{pathText},1,3617087291392");
            }

            stream.Position = 0;
            decode("L|100:100", 14); // Exclude one-off ruleset discovery from the measured import.
            long before = GC.GetAllocatedBytesForCurrentThread();
            long started = Stopwatch.GetTimestamp();
            using var reader = new LineBufferedReader(stream);
            var beatmap = new LegacyBeatmapDecoder(14).Decode(reader);
            double elapsed = Stopwatch.GetElapsedTime(started).TotalSeconds;
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(beatmap.HitObjects, Has.Count.EqualTo(slider_count));
            long count = 0;
            foreach (ConvertSlider slider in beatmap.HitObjects)
                count += slider.Path.ControlPoints.Count;
            Assert.That(count, Is.EqualTo((long)slider_count * (points_per_slider + 1)));
            TestContext.WriteLine($"Input: {stream.Length:N0} bytes; points: {count:N0}; decode: {elapsed:F3} s; allocations: {allocated:N0} bytes.");
            TestContext.WriteLine("Excludes input generation, ZIP decompression, gameplay conversion and GPU upload; allocations are not peak working set.");
        }

        private static ConvertSlider decode(string path, int version)
        {
            string source = $"""
                osu file format v{version}
                [Difficulty]
                SliderMultiplier:1
                SliderTickRate:1
                [TimingPoints]
                0,500,4,1,0,100,1,0
                [HitObjects]
                0,0,1000,2,0,{path},1,3617087291392
                """;
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(source));
            using var reader = new LineBufferedReader(stream);
            var beatmap = new LegacyBeatmapDecoder(version).Decode(reader);
            Assert.That(beatmap.HitObjects, Has.Count.EqualTo(1), "The slider must not be silently discarded.");
            return (ConvertSlider)beatmap.HitObjects[0];
        }
    }
}
