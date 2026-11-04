// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

using System;
using System.IO;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Formats;
using osu.Game.IO;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Rulesets.Osu.Tests
{
    [TestFixture]
    public class TwoBRenderingTest
    {
        [Test]
        public void TestStandardStackOrdering()
        {
            // Standard stack: Circle 1 at 1000, Circle 2 at 1200, Circle 3 at 1400.
            // Circle 1 must be on top of Circle 2, and Circle 2 on top of Circle 3.
            var c1 = new HitCircle { StartTime = 1000 };
            var c2 = new HitCircle { StartTime = 1200 };
            var c3 = new HitCircle { StartTime = 1400 };

            var beatmap = new Beatmap<OsuHitObject>
            {
                HitObjects = { c1, c2, c3 }
            };

            var order = computeDrawOrder(beatmap);
            Assert.That(order[c1], Is.GreaterThan(order[c2]));
            Assert.That(order[c2], Is.GreaterThan(order[c3]));
        }

        [Test]
        public void TestInnerSliderInsideOuterSlider()
        {
            // 2B-Lab 01 Window: Outer slider @ 4000..6000, Inner slider @ 4375..4875.
            // Inner slider must be on top of outer slider.
            var outerSlider = createSlider(4000, 2000);
            var innerSlider = createSlider(4375, 500);

            var beatmap = new Beatmap<OsuHitObject>
            {
                HitObjects = { outerSlider, innerSlider }
            };

            var order = computeDrawOrder(beatmap);
            Assert.That(order[innerSlider], Is.GreaterThan(order[outerSlider]));
        }

        [Test]
        public void TestCircleInsideSlider()
        {
            // 2B-Lab 02 Markers: Outer slider @ 4000..6000, Circle @ 4375.
            // Circle must be on top of outer slider.
            var slider = createSlider(4000, 2000);
            var circle = new HitCircle { StartTime = 4375 };

            var beatmap = new Beatmap<OsuHitObject>
            {
                HitObjects = { slider, circle }
            };

            var order = computeDrawOrder(beatmap);
            Assert.That(order[circle], Is.GreaterThan(order[slider]));
        }

        [Test]
        public void TestSimultaneousObjectsFileOrder()
        {
            // 2B-Lab 03 SameTime AB vs BA
            // When two objects start at the exact same time, the later one in the file is on top.
            var objA = new HitCircle { StartTime = 4000 };
            var objB = new HitCircle { StartTime = 4000 };

            // AB order
            var beatmapAB = new Beatmap<OsuHitObject>
            {
                HitObjects = { objA, objB }
            };
            var orderAB = computeDrawOrder(beatmapAB);
            Assert.That(orderAB[objB], Is.GreaterThan(orderAB[objA]));

            // BA order
            var beatmapBA = new Beatmap<OsuHitObject>
            {
                HitObjects = { objB, objA }
            };
            var orderBA = computeDrawOrder(beatmapBA);
            Assert.That(orderBA[objA], Is.GreaterThan(orderBA[objB]));
        }

        [Test]
        public void TestSimultaneousCircleAndSlider()
        {
            // Section 2: Circle @ 10000, Slider @ 10000..10500.
            var circle = new HitCircle { StartTime = 10000 };
            var slider = createSlider(10000, 500);

            // Circle then Slider
            var beatmap1 = new Beatmap<OsuHitObject>
            {
                HitObjects = { circle, slider }
            };
            var order1 = computeDrawOrder(beatmap1);
            Assert.That(order1[slider], Is.GreaterThan(order1[circle]));

            // Slider then Circle
            var beatmap2 = new Beatmap<OsuHitObject>
            {
                HitObjects = { slider, circle }
            };
            var order2 = computeDrawOrder(beatmap2);
            Assert.That(order2[circle], Is.GreaterThan(order2[slider]));
        }

        [Test]
        public void TestTestBeatmapFiles()
        {
            string songsFolder = @"C:\Users\ASUS\AppData\Local\osu!\Songs\beatmap-639284171832554788-audio";
            if (!Directory.Exists(songsFolder))
                return;

            foreach (string file in Directory.GetFiles(songsFolder, "*.osu"))
            {
                using var stream = File.OpenRead(file);
                using var reader = new LineBufferedReader(stream);
                var beatmap = Decoder.GetDecoder<Beatmap>(reader).Decode(reader);

                var order = computeDrawOrder(beatmap);
                Assert.That(order.Count, Is.EqualTo(beatmap.HitObjects.Count));
            }
        }

        private Slider createSlider(double startTime, double duration)
        {
            var slider = new Slider
            {
                StartTime = startTime,
                Path = new SliderPath(PathType.LINEAR, new[]
                {
                    osuTK.Vector2.Zero,
                    new osuTK.Vector2(100, 0)
                }, 100)
            };
            return slider;
        }

        private System.Collections.Generic.Dictionary<HitObject, int> computeDrawOrder(IBeatmap beatmap)
        {
            var drawOrderMap = new System.Collections.Generic.Dictionary<HitObject, int>();
            var rawObjects = beatmap.HitObjects;
            var indexed = new (HitObject obj, int index)[rawObjects.Count];
            for (int i = 0; i < rawObjects.Count; i++)
                indexed[i] = (rawObjects[i], i);

            Array.Sort(indexed, (a, b) =>
            {
                if (a.index == b.index) return 0;
                if (a.index < b.index)
                {
                    bool bOnTop = b.obj.StartTime <= a.obj.GetEndTime();
                    return bOnTop ? -1 : 1;
                }
                else
                {
                    bool aOnTop = a.obj.StartTime <= b.obj.GetEndTime();
                    return aOnTop ? 1 : -1;
                }
            });

            for (int i = 0; i < indexed.Length; i++)
                drawOrderMap[indexed[i].obj] = i;

            return drawOrderMap;
        }

        [Test]
        public void TestFontResourceStream()
        {
            var store = new osu.Framework.IO.Stores.NamespacedResourceStore<byte[]>(
                new osu.Framework.IO.Stores.DllResourceStore(typeof(osu.Game.OsuGameBase).Assembly), "Resources");
            using (var stream = store.GetStream("Fonts/MapleMono/MapleMono-Bold-ASCII-HD_0.png"))
            {
                Assert.That(stream, Is.Not.Null, "PNG stream should not be null!");
                var img = osu.Framework.Imaging.Image.Load<osu.Framework.Imaging.Rgba32>(stream);
                Assert.That(img, Is.Not.Null);
                Assert.That(img.Width, Is.GreaterThan(0));
                Assert.That(img.Height, Is.GreaterThan(0));
            }
        }
    }
}
