// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Graphics.Sprites;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Screens.Select;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Tests.Visual.SongSelect
{
    [TestFixture]
    public partial class TestSceneLegacyBeatmapInfoPanel : OsuTestScene
    {
        private static readonly Color4 harder_colour = new Color4(246, 154, 161, 255);
        private static readonly Color4 easier_colour = Color4.LightBlue;

        private LegacyBeatmapInfoPanel panel = null!;

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("create panel", () =>
            {
                Ruleset.Value = new OsuRuleset().RulesetInfo;
                SelectedMods.Value = Array.Empty<Mod>();

                var beatmap = new Beatmap
                {
                    BeatmapInfo =
                    {
                        Ruleset = Ruleset.Value,
                        DifficultyName = "Insane",
                        BPM = 180,
                        Length = 120_000,
                        TotalObjectCount = 250,
                        Metadata = new BeatmapMetadata
                        {
                            Artist = "Test Artist",
                            Title = "Test Title",
                            Author = { Username = "Test Mapper" },
                        },
                        Difficulty = new BeatmapDifficulty
                        {
                            CircleSize = 4,
                            ApproachRate = 9,
                            OverallDifficulty = 8,
                            DrainRate = 6,
                        },
                    },
                };

                Beatmap.Value = CreateWorkingBeatmap(beatmap);

                Child = new Container
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Size = new Vector2(620, 120),
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = new Color4(20, 20, 25, 255),
                        },
                        panel = new LegacyBeatmapInfoPanel(),
                    },
                };
            });
        }

        private OsuSpriteText statSegment(string acronym) =>
            panel.StatsLine.ChildrenOfType<OsuSpriteText>().Single(t => t.Text.ToString().TrimStart().StartsWith($"{acronym}:", StringComparison.Ordinal));

        private OsuSpriteText lengthSegment() =>
            panel.ChildrenOfType<OsuSpriteText>().Single(t => t.Text.ToString().StartsWith("Length:", StringComparison.Ordinal));

        [Test]
        public void TestEasyAndHardRockStatColours()
        {
            AddAssert("unmodded stats are white without arrows", () =>
                new[] { "CS", "AR", "OD", "HP" }.All(a =>
                {
                    var s = statSegment(a);
                    string text = s.Text.ToString();
                    return s.Colour == Color4.White && !text.Contains('▴') && !text.Contains('▾');
                }));

            AddStep("apply Easy", () => SelectedMods.Value = new Mod[] { new OsuModEasy() });
            AddUntilStep("stats tinted LightBlue without arrows", () =>
                new[] { "CS", "AR", "OD", "HP" }.All(a =>
                {
                    var s = statSegment(a);
                    string text = s.Text.ToString();
                    return s.Colour == easier_colour && !text.Contains('▴') && !text.Contains('▾');
                }));

            AddStep("apply HardRock", () => SelectedMods.Value = new Mod[] { new OsuModHardRock() });
            AddUntilStep("stats tinted stable harder pink without arrows", () =>
                new[] { "CS", "AR", "OD", "HP" }.All(a =>
                {
                    var s = statSegment(a);
                    string text = s.Text.ToString();
                    return s.Colour == harder_colour && !text.Contains('▴') && !text.Contains('▾');
                }));
        }

        [Test]
        public void TestRateAdjustColoursAndArrows()
        {
            AddStep("apply DoubleTime", () => SelectedMods.Value = new Mod[] { new OsuModDoubleTime() });
            AddUntilStep("length/BPM tinted harder pink", () => lengthSegment().Colour == harder_colour);
            AddAssert("CS unchanged without arrow", () =>
            {
                var cs = statSegment("CS");
                return cs.Colour == Color4.White && cs.Text.ToString() == "CS:4";
            });
            AddAssert("AR, OD, HP have up arrows", () =>
                statSegment("AR").Text.ToString().EndsWith('▴')
                && statSegment("AR").Colour == harder_colour
                && statSegment("OD").Text.ToString().EndsWith('▴')
                && statSegment("OD").Colour == harder_colour
                && statSegment("HP").Text.ToString().EndsWith('▴'));

            AddStep("apply HalfTime", () => SelectedMods.Value = new Mod[] { new OsuModHalfTime() });
            AddUntilStep("length/BPM tinted LightBlue", () => lengthSegment().Colour == easier_colour);
            AddAssert("CS unchanged without arrow", () =>
            {
                var cs = statSegment("CS");
                return cs.Colour == Color4.White && cs.Text.ToString() == "CS:4";
            });
            AddAssert("AR, OD, HP have down arrows and LightBlue AR/OD", () =>
                statSegment("AR").Text.ToString().EndsWith('▾')
                && statSegment("AR").Colour == easier_colour
                && statSegment("OD").Text.ToString().EndsWith('▾')
                && statSegment("OD").Colour == easier_colour
                && statSegment("HP").Text.ToString().EndsWith('▾'));

            AddStep("apply HardRock + HalfTime", () => SelectedMods.Value = new Mod[] { new OsuModHardRock(), new OsuModHalfTime() });
            AddUntilStep("AR shows down arrow relative to non-rate HR difficulty", () =>
                statSegment("AR").Text.ToString().EndsWith('▾'));
        }
    }
}
