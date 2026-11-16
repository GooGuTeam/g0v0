// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Formats;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Beatmaps;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Screens.Edit;

namespace osu.Game.Tests.Editing
{
    [TestFixture]
    public class NativeBeatmapEditorChangeHandlerTest
    {
        [TestCase(false)]
        [TestCase(true)]
        public void TestBookmarksUndoRedo(bool formatIncludesBookmarks)
        {
            using var beatmap = new EditorBeatmap(new OsuBeatmap
            {
                BeatmapInfo = { Ruleset = new OsuRuleset().RulesetInfo },
                Bookmarks = new[] { 1000 },
            });
            using var handler = new NativeBeatmapEditorChangeHandler(beatmap, new TestFormat(formatIncludesBookmarks));

            // No explicit SaveState: the first bookmark transaction must capture the original values.
            beatmap.Bookmarks[0] = 2000;
            Assert.That(handler.CanUndo.Value, Is.True);

            beatmap.Add(new HitCircle { StartTime = 3000 });
            handler.RestoreState(-1);
            Assert.That(beatmap.HitObjects, Is.Empty);
            assertBookmarks(2000);

            handler.RestoreState(-1);
            assertBookmarks(1000);
            Assert.That(handler.CanUndo.Value, Is.False);

            handler.RestoreState(1);
            assertBookmarks(2000);
            handler.RestoreState(1);
            assertBookmarks(2000);
            Assert.That(beatmap.HitObjects.Single().StartTime, Is.EqualTo(3000));
            Assert.That(handler.CanRedo.Value, Is.False);

            void assertBookmarks(int expected)
            {
                Assert.That(beatmap.Bookmarks, Is.EqualTo(new[] { expected }));
                Assert.That(beatmap.PlayableBeatmap.Bookmarks, Is.EqualTo(new[] { expected }));
            }
        }

        private class TestFormat : RulesetBeatmapFormat
        {
            private readonly bool includesBookmarks;

            public TestFormat(bool includesBookmarks)
            {
                this.includesBookmarks = includesBookmarks;
            }

            public override string RulesetShortName => "test";

            public override JObject Encode(IBeatmap beatmap)
            {
                var data = new JObject { ["objects"] = new JArray(beatmap.HitObjects.Select(h => h.StartTime)) };
                if (includesBookmarks)
                    data["bookmarks"] = new JArray(beatmap.Bookmarks);
                return data;
            }

            public override void Decode(JObject data, Beatmap output)
            {
                foreach (double time in data["objects"]!.Values<double>())
                    output.HitObjects.Add(new HitCircle { StartTime = time });

                if (includesBookmarks)
                    output.Bookmarks = data["bookmarks"]!.Values<int>().ToArray();
            }
        }
    }
}
