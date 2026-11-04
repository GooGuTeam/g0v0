// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osu.Game.Audio;
using osu.Game.Graphics.Sprites;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Beatmaps;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.HitSounding;
using osuTK;

namespace osu.Game.Tests.Visual.Editing
{
    [TestFixture]
    public partial class TestSceneHitSoundingSamplePoint : OsuTestScene
    {
        private EditorBeatmap beatmap = null!;
        private HitSoundingEditModel model = null!;
        private HitSoundingSamplePoint single = null!;
        private HitSoundingSamplePoint multiple = null!;
        private int transactionsBegan;
        private int transactionsEnded;

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("create markers", () =>
            {
                transactionsBegan = 0;
                transactionsEnded = 0;

                beatmap = new EditorBeatmap(new OsuBeatmap
                {
                    BeatmapInfo =
                    {
                        Ruleset = new OsuRuleset().RulesetInfo,
                    },
                    HitObjects =
                    {
                        new HitCircle
                        {
                            StartTime = 1000,
                            Samples =
                            [
                                new HitSampleInfo(HitSampleInfo.HIT_NORMAL, HitSampleInfo.BANK_SOFT, volume: 70),
                                new HitSampleInfo(HitSampleInfo.HIT_CLAP, HitSampleInfo.BANK_DRUM, volume: 70, editorAutoBank: false),
                            ]
                        },
                        new HitCircle
                        {
                            StartTime = 2000,
                            Samples =
                            [
                                new HitSampleInfo(HitSampleInfo.HIT_NORMAL, HitSampleInfo.BANK_DRUM, "2", volume: 80, useBeatmapSamples: true),
                            ]
                        },
                    },
                });

                beatmap.TransactionBegan += () => transactionsBegan++;
                beatmap.TransactionEnded += () => transactionsEnded++;

                model = new HitSoundingEditModel(beatmap);

                Child = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Children = new Drawable[]
                    {
                        model,
                        new FillFlowContainer
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            AutoSizeAxes = Axes.Both,
                            Spacing = new Vector2(20, 0),
                            Children = new Drawable[]
                            {
                                single = createMarker(HitSampleInfo.HIT_NORMAL),
                                multiple = createMarker(HitSampleInfo.HIT_NORMAL, HitSampleInfo.HIT_WHISTLE, HitSampleInfo.HIT_CLAP),
                            }
                        }
                    }
                };
            });

            AddUntilStep("markers loaded", () => single.IsLoaded && multiple.IsLoaded);
        }

        [Test]
        public void TestBadgeWidthAndFontScaling()
        {
            AddStep("use wide cells", () =>
            {
                single.UpdateLayout(60);
                multiple.UpdateLayout(60);
            });

            AddUntilStep("layout applied", () => single.DrawWidth > 0 && multiple.DrawWidth > 0);

            AddAssert("single letter uses base width", () => single.DrawWidth, () => Is.EqualTo(HitSoundingLayout.SAMPLE_HEIGHT).Within(0.5f));
            AddAssert("letters grow width", () => multiple.DrawWidth, () => Is.GreaterThan(single.DrawWidth + 0.5f));

            AddStep("use narrow cell", () => multiple.UpdateLayout(20));

            AddUntilStep("width capped to cell", () => multiple.DrawWidth <= 20.5f);
            AddUntilStep("label fits badge", () => multiple.ChildrenOfType<OsuSpriteText>().SingleOrDefault()?.DrawWidth <= multiple.DrawWidth + 0.5f);
            AddAssert("font shrunk", () => multiple.ChildrenOfType<OsuSpriteText>().Single().Font.Size, () => Is.LessThan(HitSoundingLayout.SAMPLE_FONT_SIZE));
        }

        [Test]
        public void TestRestoreDefaultsMixedSelection()
        {
            AddStep("reset mixed selection", () => model.RestoreDefaults(model.Nodes.ToArray()));

            AddAssert("all samples reset", () => beatmap.HitObjects.All(h => h.Samples.Count == 1 && isDefaultSample(h.Samples[0])));
            AddAssert("objects retained", () => beatmap.HitObjects.Count, () => Is.EqualTo(2));
            AddAssert("times unchanged", () => beatmap.HitObjects.Select(h => h.StartTime), () => Is.EqualTo(new[] { 1000d, 2000d }));
            AddAssert("single transaction", () => transactionsBegan == 1 && transactionsEnded == 1);

            AddStep("reset again", () => model.RestoreDefaults(model.Nodes.ToArray()));
            AddAssert("no spurious transaction", () => transactionsBegan == 1 && transactionsEnded == 1);
        }

        [Test]
        public void TestMoveSetMixedSelectionPreservesSampleData()
        {
            AddStep("move down one set", () => model.MoveSet(model.Nodes.ToArray(), 1));

            AddAssert("sets moved relatively", () => model.Nodes.Select(n => n.SetIndex), () => Is.EqualTo(new[] { 1, 3 }));
            AddAssert("banks retained", () => beatmap.HitObjects.Single(h => h.StartTime == 1000).Samples.Select(s => s.Bank), () => Is.EqualTo(new[] { HitSampleInfo.BANK_SOFT, HitSampleInfo.BANK_DRUM }));
            AddAssert("volumes retained", () => beatmap.HitObjects.Single(h => h.StartTime == 1000).Samples.Select(s => s.Volume), () => Is.EqualTo(new[] { 70, 70 }));
            AddAssert("addition retained", () => beatmap.HitObjects.Single(h => h.StartTime == 1000).Samples.Any(s => s.Name == HitSampleInfo.HIT_CLAP));
            AddAssert("second node moved", () => beatmap.HitObjects.Single(h => h.StartTime == 2000).Samples[0].Suffix, () => Is.EqualTo("3"));
            AddAssert("single transaction", () => transactionsBegan == 1 && transactionsEnded == 1);
            AddAssert("bounds respected", () => model.CanMoveSet(new[] { model.Nodes[0] }, -2), () => Is.False);
        }

        private static bool isDefaultSample(HitSampleInfo sample)
            => sample.Name == HitSampleInfo.HIT_NORMAL
               && sample.Bank == HitSampleInfo.BANK_NORMAL
               && sample.Suffix == null
               && sample.Volume == 100
               && !sample.UseBeatmapSamples;

        private HitSoundingSamplePoint createMarker(params string[] sampleNames)
        {
            var hitObject = new HitCircle
            {
                Samples = sampleNames.Select(name => new HitSampleInfo(name)).ToList()
            };

            return new HitSoundingSamplePoint(new HitSoundingNode(hitObject, -1), model, HitSampleInfo.BANK_NORMAL, false, (_, _) => { });
        }
    }
}
