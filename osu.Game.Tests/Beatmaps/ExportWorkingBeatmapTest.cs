// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Framework.Audio.Sample;
using osu.Framework.Audio.Track;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Textures;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Recording;
using osu.Game.Recording.Export;
using osu.Game.Audio;
using osu.Game.Skinning;

namespace osu.Game.Tests.Beatmaps
{
    [TestFixture]
    public class ExportWorkingBeatmapTest
    {
        [Test]
        public void TestGameplayAppliesItsOwnMods()
        {
            var ruleset = new OsuRuleset();
            var rawBeatmap = new TestBeatmap(ruleset.RulesetInfo)
            {
                Difficulty = new BeatmapDifficulty { ApproachRate = 8, CircleSize = 4, DrainRate = 5, OverallDifficulty = 7 },
            };
            rawBeatmap.BeatmapInfo.Difficulty = rawBeatmap.Difficulty.Clone();

            var track = new TrackVirtual(1000);
            var skin = new EmptySkin();
            var exportBeatmap = new ExportWorkingBeatmap(new TestWorkingBeatmap(rawBeatmap), track, skin, null);

            // The beatmap stays unmodded, as in the game, so that components applying mods to it do not apply them twice.
            Assert.That(exportBeatmap.BeatmapInfo.Difficulty.CircleSize, Is.EqualTo(4));

            var playable = exportBeatmap.GetPlayableBeatmap(ruleset.RulesetInfo, new Mod[] { new OsuModHardRock() });
            Assert.That(playable.Difficulty.CircleSize, Is.GreaterThan(4));
            Assert.That(exportBeatmap.BeatmapInfo.Difficulty.CircleSize, Is.EqualTo(4));

            Assert.That(exportBeatmap.LoadTrack(), Is.SameAs(track));
            Assert.That(exportBeatmap.Skin, Is.SameAs(skin));
            Assert.That(exportBeatmap.GetBackground(), Is.Null);
        }

        private class EmptySkin : ISkin
        {
            public Drawable? GetDrawableComponent(ISkinComponentLookup lookup) => null;
            public Texture? GetTexture(string componentName, WrapMode wrapModeS, WrapMode wrapModeT) => null;
            public ISample? GetSample(ISampleInfo sampleInfo) => null;
            public IBindable<TValue>? GetConfig<TLookup, TValue>(TLookup lookup) where TLookup : notnull where TValue : notnull => null;
        }
    }
}
