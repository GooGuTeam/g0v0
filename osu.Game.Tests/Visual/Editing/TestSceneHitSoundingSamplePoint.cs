// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using Moq;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Rendering.Dummy;
using osu.Framework.IO.Stores;
using osu.Framework.Testing;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Graphics.Sprites;
using osu.Game.IO;
using osu.Game.Overlays;
using osu.Game.Rulesets.Edit;
using osu.Game.Rulesets.Objects.Legacy;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Beatmaps;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.HitSounding;
using osu.Game.Skinning;
using osu.Game.Tests.Resources;
using osuTK;

namespace osu.Game.Tests.Visual.Editing
{
    /// <summary>
    /// Model-level regressions for the five-lane hitsounding layout: normal bank, addition bank and the three additions
    /// share one sample set index while the addition bank remains independent of the normal bank.
    /// </summary>
    [TestFixture]
    public partial class TestSceneHitSoundingSamplePoint : OsuTestScene
    {
        private EditorBeatmap beatmap = null!;
        private HitSoundingEditModel model = null!;
        private int transactionsBegan;
        private int transactionsEnded;

        [TestCase("normal-hitnormal.wav")]
        [TestCase("soft-hitclap2.ogg")]
        public void TestNormalVisibilityRemainsUserControlledWhenSamplesChange(string filename)
        {
            var filenames = new List<string>();
            LegacyBeatmapSkin skin = null!;
            EditorBeatmapSkin editorSkin = null!;
            HitSoundingSamplePoint[] cells = [];

            AddStep("create normal zero, normal one and soft zero cells", () =>
            {
                var sampleStore = new Mock<IResourceStore<byte[]>>();
                sampleStore.Setup(s => s.GetAvailableResources()).Returns(() => filenames);
                sampleStore.Setup(s => s.GetStream(It.IsAny<string>())).Returns((string name) =>
                    filenames.Contains(name) ? TestResources.OpenResource("Samples/blank.wav") : null!);
                var resources = new Mock<IStorageResourceProvider>();
                resources.Setup(r => r.AudioManager).Returns(Audio);
                resources.Setup(r => r.Renderer).Returns(new DummyRenderer());
                skin = new SampleSkin(resources.Object, sampleStore.Object);
                var playable = new OsuBeatmap { BeatmapInfo = { Ruleset = new OsuRuleset().RulesetInfo } };

                for (int i = 0; i < 3; i++)
                {
                    playable.HitObjects.Add(new HitCircle
                    {
                        StartTime = 1000 + i * 500,
                        Samples = [new HitSampleInfo(HitSampleInfo.HIT_NORMAL, i == 2 ? HitSampleInfo.BANK_SOFT : HitSampleInfo.BANK_NORMAL, useBeatmapSamples: i == 1)]
                    });
                }

                beatmap = new EditorBeatmap(playable, skin);
                editorSkin = beatmap.BeatmapSkin!;
                model = new HitSoundingEditModel(beatmap);
                cells = model.Nodes.Select((n, i) => new HitSoundingSamplePoint(n, model, HitSoundingLane.SampleSet)
                {
                    Position = new Vector2(100 + i * 80, 100)
                }).ToArray();
                model.Changed += () =>
                {
                    foreach (var cell in cells)
                        cell.Refresh();
                };
                Child = new DependencyProvidingContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    CachedDependencies =
                    [
                        (typeof(EditorBeatmap), beatmap),
                        (typeof(OverlayColourProvider), new OverlayColourProvider(OverlayColourScheme.Aquamarine))
                    ],
                    Child = new PopoverContainer
                    {
                        RelativeSizeAxes = Axes.Both,
                        Children = new Drawable[] { model }.Concat(cells).ToArray()
                    }
                };
            });
            AddStep("hide Normal0", () => model.NormalVisibility.Value = HitSoundingNormalVisibility.HideNormal0);
            AddAssert("only N0 badge hidden", () => cells[0].IsEmpty && !cells[1].IsEmpty && !cells[2].IsEmpty);
            AddStep("show all", () => model.NormalVisibility.Value = HitSoundingNormalVisibility.ShowAll);
            AddAssert("all badges visible", () => cells.All(c => !c.IsEmpty && c.ChildrenOfType<OsuSpriteText>().All(t => t.Alpha == 1)));
            AddStep("hide Normal1", () => model.NormalVisibility.Value = HitSoundingNormalVisibility.HideNormal1);
            AddAssert("only N1 badge hidden", () => !cells[0].IsEmpty && cells[1].IsEmpty && !cells[2].IsEmpty);
            AddAssert("N1 labels hidden", () => cells[1].ChildrenOfType<OsuSpriteText>().All(t => t.Alpha == 0));
            AddStep("add custom hitsound file", () =>
            {
                filenames.Add(filename);
                skin.RecycleSamples();
                editorSkin.InvokeSkinChanged();
            });
            AddUntilStep("N1 remains hidden after import", () => !cells[0].IsEmpty && cells[1].IsEmpty && !cells[2].IsEmpty);
            AddStep("remove custom hitsound file", () =>
            {
                filenames.Clear();
                skin.RecycleSamples();
                editorSkin.InvokeSkinChanged();
            });
            AddUntilStep("N1 remains hidden after removal", () => !cells[0].IsEmpty && cells[1].IsEmpty && !cells[2].IsEmpty);
            AddStep("hide Normal0 again", () => model.NormalVisibility.Value = HitSoundingNormalVisibility.HideNormal0);
            AddAssert("N0 hidden and N1 labels restored", () => cells[0].IsEmpty && !cells[1].IsEmpty && cells[1].ChildrenOfType<OsuSpriteText>().All(t => t.Alpha == 1));
            AddAssert("sample assignments are unchanged", () => model.Nodes.Select(n => n.SetIndex), () => Is.EqualTo(new[] { 0, 1, 0 }));
            AddStep("dispose sample resources", () =>
            {
                Clear();
                editorSkin.Dispose();
            });
        }

        [Test]
        public void TestNormalBankAndSharedSet()
        {
            AddStep("create node on set 1", () => createModel(new HitCircle
            {
                StartTime = 1000,
                Samples =
                [
                    new HitSampleInfo(HitSampleInfo.HIT_NORMAL, HitSampleInfo.BANK_NORMAL, volume: 60, useBeatmapSamples: true),
                ]
            }));

            AddAssert("normal lane shows normal bank", () => singleNode.GetBank(false), () => Is.EqualTo(HitSampleInfo.BANK_NORMAL));
            AddAssert("set index is 1", () => singleNode.SetIndex, () => Is.EqualTo(1));

            AddStep("choose custom set", () => model.SetBank(model.Nodes, HitSampleInfo.BANK_NORMAL, 2, false));
            AddAssert("custom set keeps normal bank", () => singleNode.GetBank(false), () => Is.EqualTo(HitSampleInfo.BANK_NORMAL));
            AddAssert("custom set index", () => singleNode.SetIndex, () => Is.EqualTo(2));
            AddAssert("custom set suffix", () => sample(HitSampleInfo.HIT_NORMAL).Suffix, () => Is.EqualTo("2"));
            AddAssert("custom set uses beatmap lookup", () => sample(HitSampleInfo.HIT_NORMAL).UseBeatmapSamples, () => Is.True);

            AddStep("choose skin set", () => model.SetBank(model.Nodes, HitSampleInfo.BANK_NORMAL, 0, false));
            AddAssert("skin set index", () => singleNode.SetIndex, () => Is.EqualTo(0));
            AddAssert("skin set suffix", () => sample(HitSampleInfo.HIT_NORMAL).Suffix, () => Is.Null);
            AddAssert("skin set does not use beatmap lookup", () => sample(HitSampleInfo.HIT_NORMAL).UseBeatmapSamples, () => Is.False);

            AddStep("choose soft on set 1", () => model.SetBank(model.Nodes, HitSampleInfo.BANK_SOFT, 1, false));
            AddAssert("soft bank shown", () => singleNode.GetBank(false), () => Is.EqualTo(HitSampleInfo.BANK_SOFT));
            AddAssert("set index restored", () => singleNode.SetIndex, () => Is.EqualTo(1));

            AddAssert("one transaction per change", () => transactionsBegan == 3 && transactionsEnded == 3);
        }

        [Test]
        public void TestSharedSetAndIndependentAdditionBanks()
        {
            AddStep("create node with auto and explicit additions", () => createModel(new HitCircle
            {
                StartTime = 1000,
                Samples =
                [
                    new HitSampleInfo(HitSampleInfo.HIT_NORMAL, HitSampleInfo.BANK_SOFT, volume: 70),
                    new HitSampleInfo(HitSampleInfo.HIT_WHISTLE, HitSampleInfo.BANK_SOFT, volume: 70),
                    new HitSampleInfo(HitSampleInfo.HIT_CLAP, HitSampleInfo.BANK_DRUM, volume: 70, editorAutoBank: false),
                ]
            }));

            AddAssert("addition lane shows the explicit bank", () => singleNode.GetBank(true), () => Is.EqualTo(HitSampleInfo.BANK_DRUM));
            AddAssert("normal lane shows normal bank", () => singleNode.GetBank(false), () => Is.EqualTo(HitSampleInfo.BANK_SOFT));

            AddStep("choose drum and custom set", () => model.SetBank(model.Nodes, HitSampleInfo.BANK_DRUM, 2, false));
            AddAssert("normal bank changed", () => sample(HitSampleInfo.HIT_NORMAL).Bank, () => Is.EqualTo(HitSampleInfo.BANK_DRUM));
            AddAssert("auto addition follows normal bank", () => sample(HitSampleInfo.HIT_WHISTLE).Bank, () => Is.EqualTo(HitSampleInfo.BANK_DRUM));
            AddAssert("auto addition stays auto", () => sample(HitSampleInfo.HIT_WHISTLE).EditorAutoBank, () => Is.True);
            AddAssert("explicit addition bank untouched", () => sample(HitSampleInfo.HIT_CLAP).Bank, () => Is.EqualTo(HitSampleInfo.BANK_DRUM));
            AddAssert("explicit addition stays explicit", () => sample(HitSampleInfo.HIT_CLAP).EditorAutoBank, () => Is.False);
            AddAssert("all samples share the suffix", () => beatmap.HitObjects.Single().Samples.All(s => s.Suffix == "2"), () => Is.True);
            AddAssert("all samples use beatmap lookup", () => beatmap.HitObjects.Single().Samples.All(s => s.UseBeatmapSamples), () => Is.True);
            AddAssert("one shared set index", () => singleNode.SetIndex, () => Is.EqualTo(2));

            AddStep("choose soft addition bank", () => model.SetBank(model.Nodes, HitSampleInfo.BANK_SOFT, -1, true));
            AddAssert("addition bank changed", () => singleNode.GetBank(true), () => Is.EqualTo(HitSampleInfo.BANK_SOFT));
            AddAssert("normal bank untouched by addition edit", () => singleNode.GetBank(false), () => Is.EqualTo(HitSampleInfo.BANK_DRUM));
            AddAssert("shared set untouched", () => singleNode.SetIndex, () => Is.EqualTo(2));

            AddStep("return additions to auto", () => model.SetBank(model.Nodes, HitObjectComposer.HIT_BANK_AUTO, -1, true));
            AddAssert("addition lane is auto", () => singleNode.GetBank(true), () => Is.EqualTo(HitObjectComposer.HIT_BANK_AUTO));
            AddAssert("all additions follow normal bank", () => sample(HitSampleInfo.HIT_WHISTLE).Bank, () => Is.EqualTo(HitSampleInfo.BANK_DRUM));
            AddAssert("clap also follows normal bank", () => sample(HitSampleInfo.HIT_CLAP).Bank, () => Is.EqualTo(HitSampleInfo.BANK_DRUM));
            AddAssert("all additions are auto", () => sample(HitSampleInfo.HIT_WHISTLE).EditorAutoBank && sample(HitSampleInfo.HIT_CLAP).EditorAutoBank, () => Is.True);
            AddAssert("shared set still untouched", () => singleNode.SetIndex, () => Is.EqualTo(2));
            AddAssert("normal bank still untouched", () => singleNode.GetBank(false), () => Is.EqualTo(HitSampleInfo.BANK_DRUM));

            AddStep("clear normal lane", () => model.SetBank(model.Nodes, HitSampleInfo.BANK_NORMAL, 1, false));
            AddAssert("normal bank cleared", () => singleNode.GetBank(false), () => Is.EqualTo(HitSampleInfo.BANK_NORMAL));
            AddAssert("set index cleared to 1", () => singleNode.SetIndex, () => Is.EqualTo(1));
            AddAssert("additions follow the cleared normal bank", () => sample(HitSampleInfo.HIT_CLAP).Bank, () => Is.EqualTo(HitSampleInfo.BANK_NORMAL));
            AddAssert("one transaction per change", () => transactionsBegan == 4 && transactionsEnded == 4);
        }

        [Test]
        public void TestAdditionBankDoesNothingWithoutAnAddition()
        {
            AddStep("create normal-only node", () => createModel(new HitCircle
            {
                StartTime = 1000,
                Samples =
                [
                    new HitSampleInfo(HitSampleInfo.HIT_NORMAL, HitSampleInfo.BANK_SOFT, volume: 70),
                ]
            }));

            AddAssert("no addition exists", () => HitSampleInfo.ALL_ADDITIONS.All(a => !singleNode.HasAddition(a)), () => Is.True);
            AddAssert("addition lane is auto", () => singleNode.GetBank(true), () => Is.EqualTo(HitObjectComposer.HIT_BANK_AUTO));

            AddStep("configure a missing addition bank", () => model.SetBank(model.Nodes, HitSampleInfo.BANK_DRUM, 2, true));

            AddAssert("sample bank unchanged", () => sample(HitSampleInfo.HIT_NORMAL).Bank, () => Is.EqualTo(HitSampleInfo.BANK_SOFT));
            AddAssert("shared set unchanged", () => singleNode.SetIndex, () => Is.EqualTo(0));
            AddAssert("no addition invented", () => beatmap.HitObjects.Single().Samples.Count, () => Is.EqualTo(1));
            AddAssert("no transaction opened", () => transactionsBegan == 0 && transactionsEnded == 0);
        }

        [Test]
        public void TestExplicitNormalAdditionBankIsNotAuto()
        {
            AddStep("create node with explicit normal addition", () => createModel(new HitCircle
            {
                StartTime = 1000,
                Samples =
                [
                    new HitSampleInfo(HitSampleInfo.HIT_NORMAL, HitSampleInfo.BANK_SOFT, volume: 70),
                    new HitSampleInfo(HitSampleInfo.HIT_CLAP, HitSampleInfo.BANK_NORMAL, volume: 70, editorAutoBank: false),
                ]
            }));

            AddAssert("explicit normal is not blank", () => singleNode.GetBank(true), () => Is.EqualTo(HitSampleInfo.BANK_NORMAL));

            AddStep("clear addition bank", () => model.SetBank(model.Nodes, HitObjectComposer.HIT_BANK_AUTO, -1, true));
            AddAssert("auto addition is blank", () => singleNode.GetBank(true), () => Is.EqualTo(HitObjectComposer.HIT_BANK_AUTO));

            AddStep("choose explicit normal again", () => model.SetBank(model.Nodes, HitSampleInfo.BANK_NORMAL, -1, true));
            AddAssert("explicit normal remains distinct", () => singleNode.GetBank(true), () => Is.EqualTo(HitSampleInfo.BANK_NORMAL));
            AddAssert("two transactions", () => transactionsBegan == 2 && transactionsEnded == 2);
        }

        [Test]
        public void TestEmptyNodeMaterialisesNormalOnlyForNormalEdits()
        {
            AddStep("create node without samples", () => createModel(new HitCircle
            {
                StartTime = 1000,
                Samples = [],
            }));

            AddAssert("node is editable", () => singleNode.CanEditSamples, () => Is.True);
            AddAssert("node has no samples", () => singleNode.Samples.Count, () => Is.EqualTo(0));
            AddAssert("addition lane is auto", () => singleNode.GetBank(true), () => Is.EqualTo(HitObjectComposer.HIT_BANK_AUTO));

            AddStep("configure addition bank on empty node", () => model.SetBank(model.Nodes, HitSampleInfo.BANK_DRUM, 2, true));
            AddAssert("no samples materialised for additions", () => singleNode.Samples.Count, () => Is.EqualTo(0));
            AddAssert("no transaction opened", () => transactionsBegan == 0 && transactionsEnded == 0);

            AddStep("configure normal bank on empty node", () => model.SetBank(model.Nodes, HitSampleInfo.BANK_DRUM, 2, false));
            AddAssert("normal sample materialised", () => singleNode.Samples.Count, () => Is.EqualTo(1));
            AddAssert("materialised name", () => sample(HitSampleInfo.HIT_NORMAL).Name, () => Is.EqualTo(HitSampleInfo.HIT_NORMAL));
            AddAssert("materialised bank", () => sample(HitSampleInfo.HIT_NORMAL).Bank, () => Is.EqualTo(HitSampleInfo.BANK_DRUM));
            AddAssert("materialised set", () => singleNode.SetIndex, () => Is.EqualTo(2));
            AddAssert("materialised volume", () => sample(HitSampleInfo.HIT_NORMAL).Volume, () => Is.EqualTo(100));
            AddAssert("single transaction", () => transactionsBegan == 1 && transactionsEnded == 1);

            AddStep("repeat normal edit", () => model.SetBank(model.Nodes, HitSampleInfo.BANK_DRUM, 2, false));
            AddAssert("repeat edit is a no-op", () => transactionsBegan == 1 && transactionsEnded == 1);

            AddStep("change volume", () => model.SetVolume(model.Nodes, 40));
            AddAssert("volume applied to materialised sample", () => sample(HitSampleInfo.HIT_NORMAL).Volume, () => Is.EqualTo(40));
            AddAssert("second transaction", () => transactionsBegan == 2 && transactionsEnded == 2);
        }

        [Test]
        public void TestEmptyBodyDoesNotMaterialiseSamples()
        {
            AddStep("create empty slider body", () => createModel(new Slider
            {
                StartTime = 1000,
                RepeatCount = 1,
                Samples = [],
                NodeSamples = new List<IList<HitSampleInfo>>(),
            }));

            AddAssert("body is editable", () => getBodyNode().CanEditSamples, () => Is.True);
            AddAssert("body has no samples", () => getBodyNode().Samples.Count, () => Is.EqualTo(0));

            AddStep("configure body normal bank", () => model.SetBank(new[] { getBodyNode() }, HitSampleInfo.BANK_DRUM, 2, false));
            AddAssert("bank edit does not materialise a sample", () => getBodyNode().Samples.Count, () => Is.EqualTo(0));

            AddStep("change body volume", () => model.SetVolume(new[] { getBodyNode() }, 50));
            AddAssert("volume edit does not materialise a sample", () => getBodyNode().Samples.Count, () => Is.EqualTo(0));
            AddAssert("no transaction opened", () => transactionsBegan == 0 && transactionsEnded == 0);
        }

        [Test]
        public void TestToggleAdditionInheritsSampleData()
        {
            AddStep("create node on custom set", () => createModel(new HitCircle
            {
                StartTime = 1000,
                Samples =
                [
                    new HitSampleInfo(HitSampleInfo.HIT_NORMAL, HitSampleInfo.BANK_DRUM, "2", volume: 65, useBeatmapSamples: true),
                ]
            }));

            AddStep("toggle clap on", () => model.ToggleAddition(model.Nodes, HitSampleInfo.HIT_CLAP));

            AddAssert("clap added", () => singleNode.HasAddition(HitSampleInfo.HIT_CLAP), () => Is.True);
            AddAssert("clap inherits bank", () => sample(HitSampleInfo.HIT_CLAP).Bank, () => Is.EqualTo(HitSampleInfo.BANK_DRUM));
            AddAssert("clap inherits suffix", () => sample(HitSampleInfo.HIT_CLAP).Suffix, () => Is.EqualTo("2"));
            AddAssert("clap inherits volume", () => sample(HitSampleInfo.HIT_CLAP).Volume, () => Is.EqualTo(65));
            AddAssert("clap inherits lookup", () => sample(HitSampleInfo.HIT_CLAP).UseBeatmapSamples, () => Is.True);
            AddAssert("clap inherits auto bank", () => sample(HitSampleInfo.HIT_CLAP).EditorAutoBank, () => Is.True);
            AddAssert("normal sample retained", () => sample(HitSampleInfo.HIT_NORMAL).Bank, () => Is.EqualTo(HitSampleInfo.BANK_DRUM));
            AddAssert("single transaction", () => transactionsBegan == 1 && transactionsEnded == 1);

            AddStep("toggle clap off", () => model.ToggleAddition(model.Nodes, HitSampleInfo.HIT_CLAP));
            AddAssert("clap removed", () => singleNode.HasAddition(HitSampleInfo.HIT_CLAP), () => Is.False);
            AddAssert("only normal remains", () => beatmap.HitObjects.Single().Samples.Count, () => Is.EqualTo(1));
            AddAssert("second transaction", () => transactionsBegan == 2 && transactionsEnded == 2);
        }

        [Test]
        public void TestFileSampleNodesDisableBankAndAdditionCells()
        {
            AddStep("create node with file sample", () => createModel(new HitCircle
            {
                StartTime = 1000,
                Samples =
                [
                    new ConvertHitObjectParser.FileHitSampleInfo("hitnormal.wav", 70),
                ]
            }));

            AddAssert("cells are disabled", () => singleNode.CanEditSamples, () => Is.False);
            AddAssert("addition lane is auto", () => singleNode.GetBank(true), () => Is.EqualTo(HitObjectComposer.HIT_BANK_AUTO));
            AddAssert("no additions", () => HitSampleInfo.ALL_ADDITIONS.All(a => !singleNode.HasAddition(a)), () => Is.True);

            AddStep("configure bank and set", () => model.SetBank(model.Nodes, HitSampleInfo.BANK_DRUM, 2, false));
            AddAssert("file sample retained", () => beatmap.HitObjects.Single().Samples.Single() is ConvertHitObjectParser.FileHitSampleInfo, () => Is.True);
            AddAssert("filename retained", () => ((ConvertHitObjectParser.FileHitSampleInfo)beatmap.HitObjects.Single().Samples.Single()).Filename, () => Is.EqualTo("hitnormal.wav"));
            AddAssert("bank edit is a no-op", () => transactionsBegan == 0 && transactionsEnded == 0);

            AddStep("toggle addition", () => model.ToggleAddition(model.Nodes, HitSampleInfo.HIT_WHISTLE));
            AddAssert("addition edit is a no-op", () => beatmap.HitObjects.Single().Samples.Count, () => Is.EqualTo(1));
            AddAssert("still no transaction", () => transactionsBegan == 0 && transactionsEnded == 0);

            AddStep("change volume", () => model.SetVolume(model.Nodes, 40));
            AddAssert("file sample retained after volume", () => beatmap.HitObjects.Single().Samples.Single() is ConvertHitObjectParser.FileHitSampleInfo, () => Is.True);
            AddAssert("filename retained after volume", () => ((ConvertHitObjectParser.FileHitSampleInfo)beatmap.HitObjects.Single().Samples.Single()).Filename, () => Is.EqualTo("hitnormal.wav"));
            AddAssert("volume applied", () => beatmap.HitObjects.Single().Samples.Single().Volume, () => Is.EqualTo(40));
            AddAssert("volume transaction", () => transactionsBegan == 1 && transactionsEnded == 1);
        }

        [Test]
        public void TestUnmaterialisedSliderNodesAreReadOnly()
        {
            AddStep("create slider with unmaterialised node samples", () => createModel(new Slider
            {
                StartTime = 1000,
                RepeatCount = 1,
                Samples =
                [
                    new HitSampleInfo(HitSampleInfo.HIT_NORMAL, HitSampleInfo.BANK_SOFT, "2", volume: 70, useBeatmapSamples: true),
                ],
                NodeSamples = new List<IList<HitSampleInfo>>(),
            }));

            AddAssert("body node is editable", () => node(-1).CanEditSamples, () => Is.True);
            AddAssert("head node is not editable", () => node(0).CanEditSamples, () => Is.False);
            AddAssert("repeat node is not editable", () => node(1).CanEditSamples, () => Is.False);

            AddStep("edit unmaterialised nodes", () =>
            {
                model.SetVolume(new[] { node(0), node(1) }, 30);
                model.ToggleAddition(new[] { node(0), node(1) }, HitSampleInfo.HIT_WHISTLE);
                model.SetBank(new[] { node(0), node(1) }, HitSampleInfo.BANK_DRUM, 0, false);
                model.SetBank(new[] { node(0), node(1) }, HitSampleInfo.BANK_DRUM, 0, true);
            });

            AddAssert("object samples untouched", () => beatmap.HitObjects.Single().Samples.Count, () => Is.EqualTo(1));
            AddAssert("object bank untouched", () => sample(HitSampleInfo.HIT_NORMAL).Bank, () => Is.EqualTo(HitSampleInfo.BANK_SOFT));
            AddAssert("object suffix untouched", () => sample(HitSampleInfo.HIT_NORMAL).Suffix, () => Is.EqualTo("2"));
            AddAssert("object volume untouched", () => sample(HitSampleInfo.HIT_NORMAL).Volume, () => Is.EqualTo(70));
            AddAssert("no additions invented on the body", () => HitSampleInfo.ALL_ADDITIONS.All(a => !node(-1).HasAddition(a)), () => Is.True);
            AddAssert("no transaction opened", () => transactionsBegan == 0 && transactionsEnded == 0);
        }

        [Test]
        public void TestBodySupportsWhistleOnlyAndKeepsNormalBank()
        {
            AddStep("create slider body", () => createModel(new Slider
            {
                StartTime = 1000,
                RepeatCount = 1,
                Samples =
                [
                    new HitSampleInfo(HitSampleInfo.HIT_NORMAL, HitSampleInfo.BANK_DRUM, volume: 60),
                ],
                NodeSamples = new List<IList<HitSampleInfo>>(),
            }));

            AddAssert("body is editable", () => getBodyNode().CanEditSamples, () => Is.True);

            AddStep("toggle unsupported body additions", () =>
            {
                model.ToggleAddition(new[] { getBodyNode() }, HitSampleInfo.HIT_FINISH);
                model.ToggleAddition(new[] { getBodyNode() }, HitSampleInfo.HIT_CLAP);
            });

            AddAssert("finish and clap ignored", () => beatmap.HitObjects.Single().Samples.Count, () => Is.EqualTo(1));
            AddAssert("no transaction opened", () => transactionsBegan == 0 && transactionsEnded == 0);

            AddStep("configure addition bank without addition", () => model.SetBank(new[] { getBodyNode() }, HitSampleInfo.BANK_SOFT, 2, true));
            AddAssert("normal bank unchanged", () => getBodyNode().GetBank(false), () => Is.EqualTo(HitSampleInfo.BANK_DRUM));
            AddAssert("normal set unchanged", () => getBodyNode().SetIndex, () => Is.EqualTo(0));
            AddAssert("still no transaction", () => transactionsBegan == 0 && transactionsEnded == 0);

            AddStep("toggle whistle on", () => model.ToggleAddition(new[] { getBodyNode() }, HitSampleInfo.HIT_WHISTLE));
            AddAssert("whistle added", () => getBodyNode().HasAddition(HitSampleInfo.HIT_WHISTLE), () => Is.True);
            AddAssert("whistle inherits bank", () => sample(HitSampleInfo.HIT_WHISTLE).Bank, () => Is.EqualTo(HitSampleInfo.BANK_DRUM));
            AddAssert("whistle inherits volume", () => sample(HitSampleInfo.HIT_WHISTLE).Volume, () => Is.EqualTo(60));
            AddAssert("single transaction", () => transactionsBegan == 1 && transactionsEnded == 1);

            AddStep("choose explicit body addition bank", () => model.SetBank(new[] { getBodyNode() }, HitSampleInfo.BANK_SOFT, -1, true));
            AddAssert("whistle bank changed", () => sample(HitSampleInfo.HIT_WHISTLE).Bank, () => Is.EqualTo(HitSampleInfo.BANK_SOFT));
            AddAssert("whistle is explicit", () => sample(HitSampleInfo.HIT_WHISTLE).EditorAutoBank, () => Is.False);
            AddAssert("normal bank untouched", () => sample(HitSampleInfo.HIT_NORMAL).Bank, () => Is.EqualTo(HitSampleInfo.BANK_DRUM));
            AddAssert("addition lane shows soft", () => getBodyNode().GetBank(true), () => Is.EqualTo(HitSampleInfo.BANK_SOFT));
            AddAssert("second transaction", () => transactionsBegan == 2 && transactionsEnded == 2);

            AddStep("return body addition bank to auto", () => model.SetBank(new[] { getBodyNode() }, HitObjectComposer.HIT_BANK_AUTO, -1, true));
            AddAssert("whistle follows normal bank", () => sample(HitSampleInfo.HIT_WHISTLE).Bank, () => Is.EqualTo(HitSampleInfo.BANK_DRUM));
            AddAssert("whistle is auto", () => sample(HitSampleInfo.HIT_WHISTLE).EditorAutoBank, () => Is.True);
            AddAssert("normal bank still untouched", () => sample(HitSampleInfo.HIT_NORMAL).Bank, () => Is.EqualTo(HitSampleInfo.BANK_DRUM));
            AddAssert("addition lane is auto", () => getBodyNode().GetBank(true), () => Is.EqualTo(HitObjectComposer.HIT_BANK_AUTO));
            AddAssert("third transaction", () => transactionsBegan == 3 && transactionsEnded == 3);
        }

        [Test]
        public void TestRestoreDefaultsMixedSelection()
        {
            AddStep("create mixed nodes", () => createModel(
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
                }));

            AddStep("restore defaults", () => model.RestoreDefaults(model.Nodes));

            AddAssert("all samples reset", () => beatmap.HitObjects.All(h => h.Samples.Count == 1 && isDefaultSample(h.Samples[0])));
            AddAssert("objects retained", () => beatmap.HitObjects.Count, () => Is.EqualTo(2));
            AddAssert("times unchanged", () => beatmap.HitObjects.Select(h => h.StartTime), () => Is.EqualTo(new[] { 1000d, 2000d }));
            AddAssert("single transaction", () => transactionsBegan == 1 && transactionsEnded == 1);

            AddStep("restore defaults again", () => model.RestoreDefaults(model.Nodes));
            AddAssert("no spurious transaction", () => transactionsBegan == 1 && transactionsEnded == 1);
        }

        private HitSoundingNode singleNode => model.Nodes.Single(n => n.NodeIndex == -1);

        private HitSoundingNode node(int nodeIndex) => model.Nodes.Single(n => n.NodeIndex == nodeIndex);

        private HitSoundingNode getBodyNode() => model.Nodes.Single(n => n.IsBody);

        private HitSampleInfo sample(string name) => beatmap.HitObjects.Single().Samples.Single(s => s.Name == name);

        private static bool isDefaultSample(HitSampleInfo sample)
            => sample.Name == HitSampleInfo.HIT_NORMAL
               && sample.Bank == HitSampleInfo.BANK_NORMAL
               && sample.Suffix == null
               && sample.Volume == 100
               && !sample.UseBeatmapSamples;

        private class SampleSkin : LegacyBeatmapSkin
        {
            public SampleSkin(IStorageResourceProvider resources, IResourceStore<byte[]> samples)
                : base(new BeatmapInfo(), resources)
            {
                ((ResourceStore<byte[]>)FallbackStore!).AddStore(samples);
                RecycleSamples();
            }
        }

        private void createModel(params OsuHitObject[] hitObjects)
        {
            transactionsBegan = 0;
            transactionsEnded = 0;

            beatmap = new EditorBeatmap(new OsuBeatmap
            {
                BeatmapInfo =
                {
                    Ruleset = new OsuRuleset().RulesetInfo,
                },
                HitObjects = hitObjects.ToList(),
            });

            beatmap.TransactionBegan += () => transactionsBegan++;
            beatmap.TransactionEnded += () => transactionsEnded++;

            model = new HitSoundingEditModel(beatmap);
        }
    }
}
