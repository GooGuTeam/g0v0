// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Testing;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Edit;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.Compose;
using osu.Game.Screens.Edit.Compose.Components;
using osu.Game.Screens.Edit.Compose.Components.Timeline;
using osu.Game.Screens.Edit.HitSounding;
using osu.Game.Skinning;
using osu.Game.Tests.Beatmaps;
using osuTK;
using osuTK.Input;

namespace osu.Game.Tests.Visual.Editing
{
    public partial class TestSceneHitSoundingScreen : EditorTestScene
    {
        protected override Ruleset CreateEditorRuleset() => new OsuRuleset();
        protected override IBeatmap CreateBeatmap(RulesetInfo ruleset) => new TestBeatmap(ruleset, false);
        private HitSoundingEditModel model => Editor.ChildrenOfType<HitSoundingEditModel>().Single();

        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        public override void SetUpSteps()
        {
            base.SetUpSteps();
            AddStep("add sample sequence", () =>
            {
                for (int i = 0; i < 8; i++)
                {
                    EditorBeatmap.Add(new HitCircle
                    {
                        StartTime = 500 + i * 500,
                        Samples =
                        [
                            new HitSampleInfo(HitSampleInfo.HIT_NORMAL, HitSampleInfo.BANK_SOFT, volume: i < 4 ? 70 : 90),
                            new HitSampleInfo(HitSampleInfo.HIT_CLAP, HitSampleInfo.BANK_DRUM, volume: i < 4 ? 70 : 90, editorAutoBank: false)
                        ]
                    });
                }

                Editor.Mode.Value = EditorScreenMode.HitSounding;
                EditorClock.Seek(2000);
                EditorClock.Stop();
            });
            AddUntilStep("roll loaded", () => Editor.ChildrenOfType<HitSoundingSamplePoint>().Any(p => p.IsLoaded));
        }

        [Test]
        public void TestNormalVisibilityConfigurationUpdatesLoadedPoints()
        {
            HitSoundingNormalVisibility original = default;
            AddStep("capture visibility preference", () => original = config.Get<HitSoundingNormalVisibility>(OsuSetting.EditorHitSoundingNormalVisibility));
            AddStep("assign Normal0 and Normal1", () =>
            {
                var nodes = model.Nodes.ToArray();
                model.SetBank(new[] { nodes[0] }, HitSampleInfo.BANK_NORMAL, 0, false);
                model.SetBank(new[] { nodes[1] }, HitSampleInfo.BANK_NORMAL, 1, false);
                System.GC.Collect();
                System.GC.WaitForPendingFinalizers();
            });

            foreach (var visibility in System.Enum.GetValues<HitSoundingNormalVisibility>())
            {
                AddStep("choose " + visibility, () => config.SetValue(OsuSetting.EditorHitSoundingNormalVisibility, visibility));
                AddUntilStep("badges follow " + visibility, () =>
                {
                    var nodes = model.Nodes.Take(2).ToArray();
                    var points = Editor.ChildrenOfType<HitSoundingSamplePoint>().Where(p => p.Lane == HitSoundingLane.SampleSet).ToArray();
                    return points.Single(p => p.Node == nodes[0]).IsEmpty == (visibility == HitSoundingNormalVisibility.HideNormal0)
                           && points.Single(p => p.Node == nodes[1]).IsEmpty == (visibility == HitSoundingNormalVisibility.HideNormal1);
                });
            }

            AddStep("restore visibility preference", () => config.SetValue(OsuSetting.EditorHitSoundingNormalVisibility, original));
        }

        [Test]
        public void TestSliderHighlightsHeadBodyRepeatAndTailSeparately()
        {
            Slider slider = null!;
            HitSoundingSliderPlaybackFlash[] flashes = [];
            AddStep("replace sequence with sounding slider", () =>
            {
                foreach (var hitObject in EditorBeatmap.HitObjects.ToArray())
                    EditorBeatmap.Remove(hitObject);
                EditorBeatmap.Add(slider = new Slider
                {
                    StartTime = 1000,
                    RepeatCount = 1,
                    Path = new SliderPath(new[] { new PathControlPoint(Vector2.Zero), new PathControlPoint(new Vector2(600, 0)) }),
                    Samples = [new HitSampleInfo(HitSampleInfo.HIT_NORMAL, HitSampleInfo.BANK_SOFT), new HitSampleInfo(HitSampleInfo.HIT_WHISTLE)],
                    NodeSamples =
                    [
                        new List<HitSampleInfo> { new HitSampleInfo(HitSampleInfo.HIT_NORMAL, HitSampleInfo.BANK_SOFT), new HitSampleInfo(HitSampleInfo.HIT_WHISTLE), new HitSampleInfo(HitSampleInfo.HIT_CLAP) },
                        new List<HitSampleInfo> { new HitSampleInfo(HitSampleInfo.HIT_NORMAL, HitSampleInfo.BANK_SOFT), new HitSampleInfo(HitSampleInfo.HIT_WHISTLE) },
                        new List<HitSampleInfo> { new HitSampleInfo(HitSampleInfo.HIT_NORMAL, HitSampleInfo.BANK_SOFT), new HitSampleInfo(HitSampleInfo.HIT_WHISTLE) }
                    ]
                });
                EditorClock.Seek(500);
            });
            AddUntilStep("slider outlines loaded", () => Editor.ChildrenOfType<HitSoundingSliderPlaybackFlash>().All(f => f.IsLoaded)
                                                         && model.Nodes.Any(n => n.HitObject == slider && n.IsBody));
            AddStep("retain slider highlights", () => flashes = Editor.ChildrenOfType<HitSoundingSliderPlaybackFlash>().OrderBy(f => f.Y).ToArray());
            AddAssert("all body fills reach the contour endpoints", () =>
            {
                var points = Editor.ChildrenOfType<HitSoundingSamplePoint>().Where(p => p.Node.HitObject == slider && p.Node.IsBody);
                return points.All(point =>
                {
                    var fill = point.ChildrenOfType<osu.Framework.Graphics.Containers.Container>().First();
                    return System.Math.Abs(fill.X + HitSoundingLayout.SAMPLE_WIDTH / 2) < 0.01f
                           && System.Math.Abs(fill.DrawWidth - point.DrawWidth - HitSoundingLayout.SAMPLE_WIDTH) < 0.01f;
                });
            });
            AddAssert("body overlay covers the complete head badge", () =>
            {
                var headPoint = Editor.ChildrenOfType<HitSoundingSamplePoint>().Single(p => p.Node.HitObject == slider && p.Node.NodeIndex == 0 && p.Lane == HitSoundingLane.SampleSet);
                var overlay = ((osu.Framework.Graphics.Containers.Container)highlight(@"Slider body highlight")).Children[0];
                return System.Math.Abs(overlay.ScreenSpaceDrawQuad.TopLeft.X - headPoint.ScreenSpaceDrawQuad.TopLeft.X) < 1
                       && overlay.ScreenSpaceDrawQuad.TopRight.X >= headPoint.ScreenSpaceDrawQuad.TopRight.X;
            });
            AddStep("play through slider", () => EditorClock.Start());
            AddUntilStep("head flashes independently", () => highlight(@"Slider head highlight").Alpha > highlight(@"Slider body highlight").Alpha);
            AddAssert("tail is dark before it is crossed", () => highlight(@"Slider tail highlight").Alpha == 0);
            AddAssert("head-only clap does not light an empty body", () => highlight(@"Slider body highlight", HitSoundingLane.Clap).Alpha == 0);
            AddAssert("head-only clap outline fits the badge", () =>
            {
                var head = (osu.Framework.Graphics.Containers.Container)highlight(@"Slider head highlight", HitSoundingLane.Clap);
                return head.Children[0].DrawWidth <= HitSoundingLayout.SAMPLE_WIDTH;
            });
            AddUntilStep("head extinguishes while body stays faint", () => EditorClock.CurrentTime > 1900 && highlight(@"Slider head highlight").Alpha == 0 && highlight(@"Slider body highlight").Alpha is > 0 and < 0.2f);
            AddUntilStep("repeat flashes", () =>
            {
                double repeatTime = model.Nodes.First(n => n.HitObject == slider && n.NodeIndex == 1).Time;
                return EditorClock.CurrentTime > repeatTime && highlight(@"Slider repeat highlight").Alpha > highlight(@"Slider body highlight").Alpha;
            });
            AddAssert("repeat sides are much fainter than top and bottom", () =>
            {
                var section = (osu.Framework.Graphics.Containers.Container)highlight(@"Slider repeat highlight");
                return section.Children[1].Alpha < section.Children[0].Alpha / 4;
            });
            AddUntilStep("tail and body brighten together", () => highlight(@"Slider tail highlight").Alpha > 0 && highlight(@"Slider body highlight").Alpha > 0.12f);
            AddAssert("missing clap tail never flashes", () => highlight(@"Slider tail highlight", HitSoundingLane.Clap).Alpha == 0);
            AddAssert("tail and body share their fade", () => System.Math.Abs(highlight(@"Slider body highlight").Alpha - highlight(@"Slider tail highlight").Alpha) < 0.01f);
            AddUntilStep("tail and body extinguish", () => highlight(@"Slider tail highlight").Alpha == 0 && highlight(@"Slider body highlight").Alpha == 0);
            AddStep("stop", () => EditorClock.Stop());

            Drawable highlight(string name, HitSoundingLane lane = HitSoundingLane.SampleSet)
            {
                var flash = flashes[(int)lane];
                return flash.ChildrenOfType<Drawable>().Single(d => d.Name == name);
            }
        }

        [Test]
        public void TestBadgesKeepSizeAfterComposeSixteenthDivisorAndZoom()
        {
            Vector2 badgeSize = Vector2.Zero;
            Vector2[] textScales = [];
            float originalSpacing = 0;
            float originalZoom = 0;

            AddStep("record badge and text size", () =>
            {
                badgeSize = cell(HitSoundingLane.SampleSet).DrawSize;
                textScales = cell(HitSoundingLane.SampleSet).ChildrenOfType<OsuSpriteText>().Select(t => t.Scale).ToArray();
                originalSpacing = pointSpacing();
                originalZoom = Editor.ChildrenOfType<HitSoundingScreen>().Single().ChildrenOfType<Timeline>().Single().Zoom;
            });
            AddStep("switch to Compose", () => Editor.Mode.Value = EditorScreenMode.Compose);
            AddUntilStep("Compose loaded", () => Editor.ChildrenOfType<ComposeScreen>().Any(s => s.IsLoaded && s.IsPresent));
            AddStep("choose 1/16 in Compose", () => Editor.Dependencies.Get<BindableBeatDivisor>().Value = 16);
            AddStep("return to Hitsounding", () => Editor.Mode.Value = EditorScreenMode.HitSounding);
            AddUntilStep("Hitsounding loaded", () => Editor.ChildrenOfType<HitSoundingScreen>().Any(s => s.IsLoaded && s.IsPresent));
            AddUntilStep("badges retain size at 1/16", badgesKeepSize);
            AddStep("zoom out", () => zoom(-8));
            AddUntilStep("spacing shrinks but badges keep size", () => pointSpacing() < originalSpacing && badgesKeepSize());
            AddStep("restore timeline zoom", () => Editor.ChildrenOfType<HitSoundingScreen>().Single().ChildrenOfType<Timeline>().Single().Zoom = originalZoom);
            AddUntilStep("spacing returns but badges keep size", () => System.Math.Abs(pointSpacing() - originalSpacing) < 0.1f && badgesKeepSize());
            AddStep("toggle whistle after zoom", () => clickCell(HitSoundingLane.Whistle));
            AddUntilStep("whistle remains editable", () => model.Nodes[0].HasAddition(HitSampleInfo.HIT_WHISTLE));
            AddStep("undo whistle", () => Editor.Undo());
            AddUntilStep("whistle undone", () => !model.Nodes[0].HasAddition(HitSampleInfo.HIT_WHISTLE));

            bool badgesKeepSize() => Editor.ChildrenOfType<HitSoundingSamplePoint>().All(p => p.DrawSize == badgeSize)
                                     && cell(HitSoundingLane.SampleSet).ChildrenOfType<OsuSpriteText>().Select(t => t.Scale).SequenceEqual(textScales);

            float pointSpacing()
            {
                var second = Editor.ChildrenOfType<HitSoundingSamplePoint>().Single(p => p.Node == model.Nodes[1] && p.Lane == HitSoundingLane.SampleSet);
                return second.X - cell(HitSoundingLane.SampleSet).X;
            }

            void zoom(int notches)
            {
                InputManager.MoveMouseTo(Editor.ChildrenOfType<HitSoundingVolumeTrack>().Single());
                InputManager.PressKey(Key.AltLeft);
                InputManager.ScrollVerticalBy(notches);
                InputManager.ReleaseKey(Key.AltLeft);
            }
        }

        [Test]
        public void TestBankMovePreservesAdditionAndUndo()
        {
            AddStep("move normal bank", () => model.SetBank(new[] { model.Nodes[0] }, HitSampleInfo.BANK_NORMAL, 2, false));
            AddUntilStep("normal moved independently", () => EditorBeatmap.HitObjects[0].Samples.Single(s => s.Name == HitSampleInfo.HIT_NORMAL).Bank == HitSampleInfo.BANK_NORMAL);
            AddAssert("addition bank preserved", () => EditorBeatmap.HitObjects[0].Samples.Single(s => s.Name == HitSampleInfo.HIT_CLAP).Bank == HitSampleInfo.BANK_DRUM);
            AddAssert("set shared by both samples", () => EditorBeatmap.HitObjects[0].Samples.All(s => s.Suffix == "2" && s.UseBeatmapSamples));
            AddStep("undo", () => Editor.Undo());
            AddUntilStep("normal bank restored", () => EditorBeatmap.HitObjects[0].Samples.Single(s => s.Name == HitSampleInfo.HIT_NORMAL).Bank == HitSampleInfo.BANK_SOFT);
        }

        [Test]
        public void TestSelectedPointHotkey()
        {
            AddStep("select sample", () =>
            {
                InputManager.MoveMouseTo(Editor.ChildrenOfType<HitSoundingSamplePoint>().First(p => p.Lane == HitSoundingLane.SampleSet));
                InputManager.PressKey(Key.ControlLeft);
                InputManager.Click(MouseButton.Left);
                InputManager.ReleaseKey(Key.ControlLeft);
            });
            AddStep("toggle whistle", () => InputManager.Key(Key.W));
            AddUntilStep("whistle added only to selected node", () => EditorBeatmap.HitObjects.Count(h => h.Samples.Any(s => s.Name == HitSampleInfo.HIT_WHISTLE)) == 1);
            AddStep("undo", () => Editor.Undo());
            AddUntilStep("whistle removed by undo", () => EditorBeatmap.HitObjects.All(h => h.Samples.All(s => s.Name != HitSampleInfo.HIT_WHISTLE)));
        }

        [Test]
        public void TestVolumeRunChangeIsAtomic()
        {
            AddStep("adjust first run", () => model.SetVolume(model.Nodes.Take(4).ToArray(), 90));
            AddUntilStep("adjacent runs equal", () => EditorBeatmap.HitObjects.All(h => h.Samples.All(s => s.Volume == 90)));
            AddStep("undo volume run", () => Editor.Undo());
            AddUntilStep("first run restored", () => EditorBeatmap.HitObjects.Take(4).All(h => h.Samples.All(s => s.Volume == 70)));
            AddAssert("second run untouched", () => EditorBeatmap.HitObjects.Skip(4).All(h => h.Samples.All(s => s.Volume == 90)));
        }

        [Test]
        public void TestCtrlSplitAndDoubleClickPrecision()
        {
            AddStep("ctrl click inside first run", () =>
            {
                var track = Editor.ChildrenOfType<HitSoundingVolumeTrack>().Single();
                InputManager.MoveMouseTo(track.ToScreenSpace(new Vector2(track.TimeToX(1500), track.YForVolume(70))));
                InputManager.PressKey(Key.ControlLeft);
                InputManager.Click(MouseButton.Left);
                InputManager.ReleaseKey(Key.ControlLeft);
            });
            AddUntilStep("precision popup opens", () => Editor.ChildrenOfType<HitSoundingVolumePopover>().Any(p => p.IsPresent));
            AddStep("set suffix volume", () => Editor.ChildrenOfType<HitSoundingVolumePopover>().Single(p => p.IsPresent).ChildrenOfType<FormSliderBar<int>>().Single().Current.Value = 50);
            AddUntilStep("only suffix changed", () => EditorBeatmap.HitObjects[2].Samples.All(s => s.Volume == 50) && EditorBeatmap.HitObjects[3].Samples.All(s => s.Volume == 50));
            AddAssert("prefix retained", () => EditorBeatmap.HitObjects.Take(2).All(h => h.Samples.All(s => s.Volume == 70)));
            AddStep("dismiss popup", () => InputManager.Key(Key.Escape));
            AddStep("double click constant run", () =>
            {
                var track = Editor.ChildrenOfType<HitSoundingVolumeTrack>().Single();
                InputManager.MoveMouseTo(track.ToScreenSpace(new Vector2(track.TimeToX(1750), track.YForVolume(50))));
                InputManager.Click(MouseButton.Left);
                InputManager.Click(MouseButton.Left);
            });
            AddUntilStep("whole run popup opens", () => Editor.ChildrenOfType<HitSoundingVolumePopover>().Any(p => p.IsPresent));
        }

        [Test]
        public void TestVolumeDragMergesAndUndoes()
        {
            AddStep("drag first run to neighbouring level", () =>
            {
                var track = Editor.ChildrenOfType<HitSoundingVolumeTrack>().Single();
                InputManager.MoveMouseTo(track.ToScreenSpace(new Vector2(track.TimeToX(1250), track.YForVolume(70))));
                InputManager.PressButton(MouseButton.Left);
                InputManager.MoveMouseTo(track.ToScreenSpace(new Vector2(track.TimeToX(1250), track.YForVolume(90))));
            });
            AddStep("release drag", () => InputManager.ReleaseButton(MouseButton.Left));
            AddUntilStep("volumes merge", () => EditorBeatmap.HitObjects.All(h => h.Samples.All(s => s.Volume == 90)));
            AddAssert("one constant run", () => Editor.ChildrenOfType<HitSoundingVolumeTrack>().Single().Segments.Count, () => Is.EqualTo(1));
            AddStep("undo drag", () => Editor.Undo());
            AddUntilStep("first run restored", () => EditorBeatmap.HitObjects.Take(4).All(h => h.Samples.All(s => s.Volume == 70)));
        }

        [Test]
        public void TestSliderTailEditDoesNotChangeHeadOrBody()
        {
            AddStep("add repeating slider", () => EditorBeatmap.Add(new Slider
            {
                StartTime = 1000,
                RepeatCount = 1,
                Path = new SliderPath(new[] { new PathControlPoint(Vector2.Zero), new PathControlPoint(new Vector2(250, 0)) }),
                Samples = [new HitSampleInfo(HitSampleInfo.HIT_NORMAL, volume: 70)],
                NodeSamples =
                [
                    new List<HitSampleInfo> { new HitSampleInfo(HitSampleInfo.HIT_NORMAL, volume: 60) },
                    new List<HitSampleInfo> { new HitSampleInfo(HitSampleInfo.HIT_NORMAL, volume: 60) },
                    new List<HitSampleInfo> { new HitSampleInfo(HitSampleInfo.HIT_NORMAL, volume: 60) }
                ]
            }));
            AddUntilStep("slider nodes loaded", () => model.Nodes.Count(n => n.HitObject is Slider) == 4);
            AddStep("edit slider tail", () => model.SetVolume(new[] { model.Nodes.Single(n => n.HitObject is Slider && n.NodeIndex == 2) }, 40));
            AddUntilStep("tail changed", () => EditorBeatmap.HitObjects.OfType<Slider>().Single().NodeSamples[2][0].Volume == 40);
            AddAssert("head and repeat unchanged", () => EditorBeatmap.HitObjects.OfType<Slider>().Single().NodeSamples.Take(2).All(s => s[0].Volume == 60));
            AddAssert("body unchanged", () => EditorBeatmap.HitObjects.OfType<Slider>().Single().Samples[0].Volume == 70);
        }

        [Test]
        public void TestWhistleUnderlayFollowsBodyAndEndpoints([Values] bool headWhistle, [Values] bool tailWhistle, [Values] bool bodyWhistle)
        {
            Slider slider = null!;
            AddStep("create slider with independent whistle samples", () =>
            {
                foreach (var hitObject in EditorBeatmap.HitObjects.ToArray())
                    EditorBeatmap.Remove(hitObject);

                slider = new Slider
                {
                    StartTime = 1000,
                    Path = new SliderPath(new[] { new PathControlPoint(Vector2.Zero), new PathControlPoint(new Vector2(500, 0)) }),
                    Samples = samples(bodyWhistle),
                    NodeSamples = [samples(headWhistle), samples(tailWhistle)]
                };
                EditorBeatmap.Add(slider);
                EditorClock.Seek(1500);
                InputManager.MoveMouseTo(Vector2.Zero);
            });
            AddUntilStep("whistle interval loaded", () => Editor.ChildrenOfType<HitSoundingSamplePoint>().Any(p => p.Node.HitObject == slider && p.Node.IsBody && p.IsLoaded));
            AddUntilStep("only relevant whistle underlay is visible", () =>
            {
                var point = Editor.ChildrenOfType<HitSoundingSamplePoint>().Single(p => p.Node.HitObject == slider && p.Node.IsBody && p.Lane == HitSoundingLane.Whistle);
                var fill = point.ChildrenOfType<osu.Framework.Graphics.Containers.Container>().First();
                float expectedAlpha = bodyWhistle ? 0.66f : headWhistle || tailWhistle ? 0.08f : 0;
                return System.Math.Abs(fill.Alpha - expectedAlpha) < 0.001f;
            });
            AddAssert("rendering preserves whistle assignments", () => slider.Samples.Any(s => s.Name == HitSampleInfo.HIT_WHISTLE) == bodyWhistle
                                                                       && slider.NodeSamples[0].Any(s => s.Name == HitSampleInfo.HIT_WHISTLE) == headWhistle
                                                                       && slider.NodeSamples[1].Any(s => s.Name == HitSampleInfo.HIT_WHISTLE) == tailWhistle);

            static List<HitSampleInfo> samples(bool whistle)
            {
                var result = new List<HitSampleInfo> { new HitSampleInfo(HitSampleInfo.HIT_NORMAL, HitSampleInfo.BANK_SOFT) };
                if (whistle)
                    result.Add(new HitSampleInfo(HitSampleInfo.HIT_WHISTLE));
                return result;
            }
        }

        [Test]
        public void TestAdditionUnderlayFollowsEndpoints(
            [Values(HitSampleInfo.HIT_WHISTLE, HitSampleInfo.HIT_FINISH, HitSampleInfo.HIT_CLAP)]
            string addition,
            [Values] bool headAddition, [Values] bool tailAddition)
        {
            Slider slider = null!;
            AddStep("create slider without body additions", () =>
            {
                foreach (var hitObject in EditorBeatmap.HitObjects.ToArray())
                    EditorBeatmap.Remove(hitObject);

                slider = new Slider
                {
                    StartTime = 1000,
                    Path = new SliderPath(new[] { new PathControlPoint(Vector2.Zero), new PathControlPoint(new Vector2(500, 0)) }),
                    Samples = samples(false),
                    NodeSamples = [samples(headAddition), samples(tailAddition)]
                };
                EditorBeatmap.Add(slider);
                EditorClock.Seek(1500);
                InputManager.MoveMouseTo(Vector2.Zero);
            });
            AddUntilStep("addition interval loaded", () => Editor.ChildrenOfType<HitSoundingSamplePoint>().Any(p => p.Node.HitObject == slider && p.Node.IsBody && p.IsLoaded));
            AddUntilStep("addition underlay follows endpoints", () => underlayVisible() == (headAddition || tailAddition));
            AddAssert("body addition bank remains disabled", () => !bodyPoint().IsEnabled);
            AddAssert("body addition labels remain hidden", () => bodyPoint().ChildrenOfType<OsuSpriteText>().All(t => t.Alpha == 0));
            AddStep("remove endpoint additions", () => model.ToggleAddition(model.Nodes.Where(n => n.HitObject == slider && !n.IsBody && n.HasAddition(addition)), addition));
            AddUntilStep("addition underlay clears", () => !underlayVisible());
            AddAssert("body gains no addition", () => !slider.Samples.Any(s => HitSampleInfo.ALL_ADDITIONS.Contains(s.Name)));

            HitSoundingSamplePoint bodyPoint() => Editor.ChildrenOfType<HitSoundingSamplePoint>().Single(p => p.Node.HitObject == slider && p.Node.IsBody && p.Lane == HitSoundingLane.Additions);
            bool underlayVisible() => bodyPoint().ChildrenOfType<osu.Framework.Graphics.Containers.Container>().First().Alpha > 0;

            List<HitSampleInfo> samples(bool enabled)
            {
                var result = new List<HitSampleInfo> { new HitSampleInfo(HitSampleInfo.HIT_NORMAL, HitSampleInfo.BANK_SOFT) };
                if (enabled)
                    result.Add(new HitSampleInfo(addition, HitSampleInfo.BANK_SOFT, editorAutoBank: false));
                return result;
            }
        }

        [Test]
        public void TestAdditionsUnderlayIgnoresBodyOnlyAdditions()
        {
            Slider slider = null!;
            AddStep("create slider whose only addition is on the body", () =>
            {
                foreach (var hitObject in EditorBeatmap.HitObjects.ToArray())
                    EditorBeatmap.Remove(hitObject);

                slider = new Slider
                {
                    StartTime = 1000,
                    Path = new SliderPath(new[] { new PathControlPoint(Vector2.Zero), new PathControlPoint(new Vector2(500, 0)) }),
                    Samples =
                    [
                        new HitSampleInfo(HitSampleInfo.HIT_NORMAL, HitSampleInfo.BANK_SOFT),
                        new HitSampleInfo(HitSampleInfo.HIT_WHISTLE, HitSampleInfo.BANK_SOFT, editorAutoBank: false)
                    ],
                    NodeSamples =
                    [
                        new List<HitSampleInfo> { new HitSampleInfo(HitSampleInfo.HIT_NORMAL, HitSampleInfo.BANK_SOFT) },
                        new List<HitSampleInfo> { new HitSampleInfo(HitSampleInfo.HIT_NORMAL, HitSampleInfo.BANK_SOFT) }
                    ]
                };
                EditorBeatmap.Add(slider);
                EditorClock.Seek(1500);
                InputManager.MoveMouseTo(Vector2.Zero);
            });
            AddUntilStep("addition interval loaded", () => Editor.ChildrenOfType<HitSoundingSamplePoint>().Any(p => p.Node.HitObject == slider && p.Node.IsBody && p.IsLoaded));
            AddAssert("body-only addition draws no underlay", () => !underlayVisible());
            AddStep("move pointer away from the interval", () => InputManager.MoveMouseTo(Vector2.Zero));
            AddAssert("hovering does not revive the underlay", () => !underlayVisible());
            AddStep("add whistle to the head", () => model.ToggleAddition(model.Nodes.Where(n => n.HitObject == slider && n.NodeIndex == 0), HitSampleInfo.HIT_WHISTLE));
            AddUntilStep("endpoint addition draws the underlay", () => underlayVisible());

            HitSoundingSamplePoint bodyPoint() => Editor.ChildrenOfType<HitSoundingSamplePoint>().Single(p => p.Node.HitObject == slider && p.Node.IsBody && p.Lane == HitSoundingLane.Additions);
            bool underlayVisible() => bodyPoint().ChildrenOfType<osu.Framework.Graphics.Containers.Container>().First().Alpha > 0;
        }

        [Test]
        public void TestSliderBodyIntervalEditsWithoutRetargetingHeadOrTail()
        {
            Slider slider = null!;
            AddStep("replace sequence with slider", () =>
            {
                foreach (var hitObject in EditorBeatmap.HitObjects.ToArray())
                    EditorBeatmap.Remove(hitObject);
                EditorBeatmap.Add(slider = new Slider
                {
                    StartTime = 1000,
                    Path = new SliderPath(new[] { new PathControlPoint(Vector2.Zero), new PathControlPoint(new Vector2(500, 0)) }),
                    Samples = [new HitSampleInfo(HitSampleInfo.HIT_NORMAL, HitSampleInfo.BANK_SOFT, volume: 70)],
                    NodeSamples =
                    [
                        new List<HitSampleInfo> { new HitSampleInfo(HitSampleInfo.HIT_NORMAL, HitSampleInfo.BANK_DRUM) },
                        new List<HitSampleInfo> { new HitSampleInfo(HitSampleInfo.HIT_NORMAL, HitSampleInfo.BANK_DRUM) }
                    ]
                });
            });
            AddUntilStep("body interval loaded", () => Editor.ChildrenOfType<HitSoundingSamplePoint>().Any(p => p.Node.HitObject == slider && p.Node.IsBody && p.IsLoaded));
            AddStep("click body away from its capsule", () => clickBody(HitSoundingLane.SampleSet));
            AddUntilStep("body bank popup opens", () => Editor.ChildrenOfType<HitSoundingSamplePopover>().Any(p => p.IsPresent));
            AddStep("change body bank", () =>
            {
                var popup = Editor.ChildrenOfType<HitSoundingSamplePopover>().Single(p => p.IsPresent);
                popup.ChildrenOfType<FormDropdown<string>>().Single().Current.Value = HitSampleInfo.BANK_NORMAL;
                popup.ChildrenOfType<RoundedButton>().Single().TriggerClick();
            });
            AddUntilStep("only body bank changes", () => slider.Samples.Single().Bank == HitSampleInfo.BANK_NORMAL && slider.NodeSamples.All(s => s.Single().Bank == HitSampleInfo.BANK_DRUM));
            AddStep("click body whistle interval", () => clickBody(HitSoundingLane.Whistle));
            AddUntilStep("body whistle enabled", () => slider.Samples.Any(s => s.Name == HitSampleInfo.HIT_WHISTLE));
            AddAssert("head and tail have no whistle", () => slider.NodeSamples.All(s => s.All(sample => sample.Name != HitSampleInfo.HIT_WHISTLE)));
            AddStep("open body addition bank", () => clickBody(HitSoundingLane.Additions));
            AddUntilStep("body addition popup opens", () => Editor.ChildrenOfType<HitSoundingSamplePopover>().Any(p => p.IsPresent));
            AddStep("dismiss popup", () => InputManager.Key(Key.Escape));
            AddStep("click head above the body fill", () =>
            {
                var head = Editor.ChildrenOfType<HitSoundingSamplePoint>().Single(p => p.Node.HitObject == slider && p.Node.NodeIndex == 0 && p.Lane == HitSoundingLane.Whistle);
                InputManager.MoveMouseTo(head);
                InputManager.Click(MouseButton.Left);
            });
            AddUntilStep("head receives click instead of body", () => slider.NodeSamples[0].Any(s => s.Name == HitSampleInfo.HIT_WHISTLE));
            AddAssert("body whistle remains enabled", () => slider.Samples.Any(s => s.Name == HitSampleInfo.HIT_WHISTLE));
            AddAssert("tail stays unchanged", () => slider.NodeSamples[1].All(s => s.Name != HitSampleInfo.HIT_WHISTLE));
            AddStep("box-select part of body away from capsule", () =>
            {
                model.SelectedNodes.Clear();
                var body = Editor.ChildrenOfType<HitSoundingSamplePoint>().Single(p => p.Node.HitObject == slider && p.Node.IsBody && p.Lane == HitSoundingLane.SampleSet);
                InputManager.MoveMouseTo(body.ToScreenSpace(new Vector2(body.Width * 0.7f, -6)));
                InputManager.PressButton(MouseButton.Left);
                InputManager.MoveMouseTo(body.ToScreenSpace(new Vector2(body.Width * 0.8f, body.Height + 3)));
            });
            AddStep("release body selection", () => InputManager.ReleaseButton(MouseButton.Left));
            AddAssert("body interval selected without head or tail", () => model.SelectedNodes.Single().IsBody);

            void clickBody(HitSoundingLane lane)
            {
                var body = Editor.ChildrenOfType<HitSoundingSamplePoint>().Single(p => p.Node.HitObject == slider && p.Node.IsBody && p.Lane == lane);
                InputManager.MoveMouseTo(body.ToScreenSpace(new Vector2(body.Width * 0.7f, body.Height / 2)));
                InputManager.Click(MouseButton.Left);
            }
        }

        [Test]
        public void TestRightClickBlankClearsMultipleThenSelectsNearest()
        {
            HitSoundingNode nearest = null!;
            AddStep("select two nodes", () => model.SelectedNodes.AddRange(model.Nodes.Take(2)));
            AddStep("right click nearby blank lane", () =>
            {
                var point = Editor.ChildrenOfType<HitSoundingSamplePoint>().First(p => p.Lane == HitSoundingLane.SampleSet);
                nearest = point.Node;
                InputManager.MoveMouseTo(point.ToScreenSpace(new Vector2(point.Width / 2, -3)));
                InputManager.Click(MouseButton.Right);
            });
            AddAssert("multi selection cleared", () => model.SelectedNodes, () => Is.Empty);
            AddStep("right click again", () => InputManager.Click(MouseButton.Right));
            AddAssert("nearest sample selected", () => model.SelectedNodes.Single(), () => Is.SameAs(nearest));
        }

        [Test]
        public void TestOldVolumeValuesLeaveViewport()
        {
            AddStep("make three volume runs", () =>
            {
                model.SetVolume(new[] { model.Nodes[0] }, 50);
                model.SetVolume(new[] { model.Nodes[1] }, 60);
                EditorClock.Seek(4750);
            });
            AddUntilStep("old values hidden", () =>
            {
                var track = Editor.ChildrenOfType<HitSoundingVolumeTrack>().Single();
                return track.Segments.Where(s => s.EndX <= 0).All(s => !s.IsPresent)
                       && !track.Segments.Any(s => s.IsPresent && s.Volume is 50 or 60);
            });
            AddAssert("current run still visible", () => Editor.ChildrenOfType<HitSoundingVolumeTrack>().Single().Segments.First(s => s.EndX > 0).IsPresent);
        }

        [Test]
        public void TestPausedWheelSeekSnapsWithoutSound()
        {
            // deliberately off the beat grid, and outside the times occupied by the setup's hit objects.
            const double first_off_grid = 4712.5;
            // far enough from the first point that a single notch only reaches it when forced past the point under the playhead.
            const double second_off_grid = 5440.5;
            const double slider_start = 5600.5;

            Slider slider = null!;
            double lastSoundPoint = 0;

            AddUntilStep("beatmap settled", () => !EditorBeatmap.UpdateInProgress.Value);
            AddStep("add off-grid sound points", () =>
            {
                EditorBeatmap.Add(new HitCircle
                {
                    StartTime = first_off_grid,
                    Samples = [new HitSampleInfo(HitSampleInfo.HIT_NORMAL)]
                });
                EditorBeatmap.Add(new HitCircle
                {
                    StartTime = second_off_grid,
                    Samples = [new HitSampleInfo(HitSampleInfo.HIT_NORMAL)]
                });
                EditorBeatmap.Add(slider = new Slider
                {
                    StartTime = slider_start,
                    RepeatCount = 2,
                    Path = new SliderPath(new[] { new PathControlPoint(Vector2.Zero), new PathControlPoint(new Vector2(250, 0)) }),
                    Samples = [new HitSampleInfo(HitSampleInfo.HIT_NORMAL)],
                    NodeSamples =
                    [
                        new List<HitSampleInfo> { new HitSampleInfo(HitSampleInfo.HIT_NORMAL) },
                        new List<HitSampleInfo> { new HitSampleInfo(HitSampleInfo.HIT_NORMAL) },
                        new List<HitSampleInfo> { new HitSampleInfo(HitSampleInfo.HIT_NORMAL) },
                        new List<HitSampleInfo> { new HitSampleInfo(HitSampleInfo.HIT_NORMAL) }
                    ]
                });
            });
            AddUntilStep("sound points loaded", () =>
            {
                bool firstPointLoaded = model.Nodes.Any(n => System.Math.Abs(n.Time - first_off_grid) < 0.5);
                bool secondPointLoaded = model.Nodes.Any(n => System.Math.Abs(n.Time - second_off_grid) < 0.5);
                bool thirdPointLoaded = model.Nodes.Count(n => n.HitObject == slider && n.Time > slider_start + 1) >= 3;

                return firstPointLoaded && secondPointLoaded && thirdPointLoaded;
            });

            AddStep("seek to the last setup sound point", () => EditorClock.Seek(4000));
            AddUntilStep("seek settled", () => seekSettled);
            AddStep("wheel forward once", () => scrollWheel(-1));
            AddUntilStep("seek settled", () => seekSettled);
            AddAssert("landed on the next off-grid sound point", () => EditorClock.CurrentTime, () => Is.EqualTo(first_off_grid).Within(0.5));
            AddAssert("landing is not on the beat grid", () => System.Math.Abs(EditorBeatmap.SnapTime(EditorClock.CurrentTime, null) - EditorClock.CurrentTime) > 0.5);

            AddStep("wheel backward once", () => scrollWheel(1));
            AddUntilStep("seek settled", () => seekSettled);
            AddAssert("returned to the previous sound point", () => EditorClock.CurrentTime, () => Is.EqualTo(4000d).Within(0.5));

            AddStep("seek onto a sound point", () => EditorClock.Seek(first_off_grid));
            AddUntilStep("seek settled", () => seekSettled);
            AddStep("wheel forward a small notch", () => scrollWheel(-1));
            AddUntilStep("seek settled", () => seekSettled);
            AddAssert("advanced to the next sound point rather than staying put", () => EditorClock.CurrentTime, () => Is.EqualTo(second_off_grid).Within(0.5));

            AddStep("wheel backward from the far point", () => scrollWheel(1));
            AddUntilStep("seek settled", () => seekSettled);
            AddAssert("went back to the previous sound point", () => EditorClock.CurrentTime, () => Is.EqualTo(first_off_grid).Within(0.5));

            AddStep("seek back to the setup point", () => EditorClock.Seek(4000));
            AddUntilStep("seek settled", () => seekSettled);
            AddStep("rapid wheel forward twice", () =>
            {
                InputManager.MoveMouseTo(Editor.ChildrenOfType<HitSoundingVolumeTrack>().Single());
                InputManager.ScrollVerticalBy(-1);
                InputManager.ScrollVerticalBy(-1);
            });
            AddUntilStep("seek settled", () => seekSettled);
            AddAssert("both notches accumulated onto sound points", () => EditorClock.CurrentTime, () => Is.EqualTo(second_off_grid).Within(0.5));

            AddStep("seek to the slider head", () => EditorClock.Seek(slider_start));
            AddUntilStep("seek settled", () => seekSettled);
            AddStep("wheel forward from the slider head", () => scrollWheel(-1));
            AddUntilStep("seek settled", () => seekSettled);
            AddAssert("landed on a nested slider sample time", () =>
            {
                bool withinTolerance(double time) => System.Math.Abs(time - EditorClock.CurrentTime) < 0.5;

                return model.Nodes.Any(n => n.HitObject == slider && n.Time > slider_start + 1 && withinTolerance(n.Time));
            });

            AddStep("record last sound point", () => lastSoundPoint = model.Nodes.Max(n => n.Time));
            AddStep("seek past the last sound point", () => EditorClock.Seek(lastSoundPoint + 200));
            AddUntilStep("seek settled", () => seekSettled);
            AddStep("wheel forward past the last sound point", () => scrollWheel(-1));
            AddUntilStep("seek settled", () => seekSettled);
            AddAssert("does not jump backward", () => EditorClock.CurrentTime, () => Is.GreaterThan(lastSoundPoint + 200));
            AddStep("wheel backward past the last sound point", () => scrollWheel(1));
            AddUntilStep("seek settled", () => seekSettled);
            AddAssert("returns to the last sound point", () => EditorClock.CurrentTime, () => Is.EqualTo(lastSoundPoint).Within(0.5));

            AddAssert("no sample played", () => !Editor.ChildrenOfType<HitSoundingSamplePlayback>().Single().ChildrenOfType<PoolableSkinnableSample>().Any(s => s.Played));
        }

        [Test]
        public void TestComposeObjectRemainsAlignedWithSampleAfterZoom()
        {
            AddUntilStep("object and sample agree", objectsAlign);
            AddStep("zoom timeline", () =>
            {
                InputManager.MoveMouseTo(Editor.ChildrenOfType<HitSoundingVolumeTrack>().Single());
                InputManager.PressKey(Key.AltLeft);
                InputManager.ScrollVerticalBy(5);
                InputManager.ReleaseKey(Key.AltLeft);
            });
            AddUntilStep("alignment survives zoom", objectsAlign);

            bool objectsAlign()
            {
                foreach (var marker in Editor.ChildrenOfType<HitSoundingSamplePoint>().Where(p => p.Lane == HitSoundingLane.SampleSet))
                {
                    var obj = Editor.ChildrenOfType<HitSoundingObject>().SingleOrDefault(o => o.Item == marker.Node.HitObject);
                    if (obj == null || System.Math.Abs(obj.ScreenSpaceDrawQuad.Centre.X - marker.ScreenSpaceDrawQuad.Centre.X) > 1)
                        return false;
                }

                return true;
            }
        }

        [Test]
        public void TestVisibleIntervalsRetainOverlappingSliderBodiesAfterSeek()
        {
            AddStep("add long overlapping slider and later circles", () =>
            {
                EditorBeatmap.Add(new Slider
                {
                    StartTime = 1000,
                    RepeatCount = 2,
                    Path = new SliderPath(new[] { new PathControlPoint(Vector2.Zero), new PathControlPoint(new Vector2(6000, 0)) }),
                    Samples = [new HitSampleInfo(HitSampleInfo.HIT_NORMAL, HitSampleInfo.BANK_SOFT)],
                });
                for (int i = 0; i < 100; i++)
                    EditorBeatmap.Add(new HitCircle { StartTime = 5000 + i * 500 });
            });

            foreach (double time in new[] { 2000.0, 15000.0, 40000.0, 2000.0 })
            {
                AddStep($"seek to {time}", () => EditorClock.Seek(time));
                AddUntilStep("visible nodes and objects match their full intervals", () =>
                {
                    var screen = Editor.ChildrenOfType<HitSoundingScreen>().Single();
                    var timeline = screen.ChildrenOfType<Timeline>().Single();
                    var points = screen.ChildrenOfType<HitSoundingSamplePoint>().ToArray();
                    if (points.Length == 0)
                        return false;

                    var bounds = points[0].Parent!.ScreenSpaceDrawQuad;
                    double start = timeline.TimeAtScreenSpacePosition(bounds.TopLeft) - 200;
                    double end = timeline.TimeAtScreenSpacePosition(bounds.TopRight) + 200;
                    var expectedNodes = model.Nodes.Where(n => (n.IsBody ? n.HitObject.GetEndTime() : n.Time) >= start
                                                              && (n.IsBody ? n.HitObject.StartTime : n.Time) <= end).ToHashSet();
                    var actualNodes = points.Where(p => p.Lane == HitSoundingLane.SampleSet).Select(p => p.Node).ToHashSet();
                    var expectedObjects = EditorBeatmap.HitObjects.Where(h => h.GetEndTime() >= start && h.StartTime <= end).ToHashSet();
                    var actualObjects = screen.ChildrenOfType<HitSoundingObject>().Select(o => o.Item).ToHashSet();
                    return actualNodes.SetEquals(expectedNodes) && actualObjects.SetEquals(expectedObjects);
                });
            }
        }

        [Test]
        public void TestRecycledTimelineControlsFollowNewNodesAfterSeek()
        {
            var originalPoints = new HashSet<HitSoundingSamplePoint>();
            var originalObjects = new HashSet<HitSoundingObject>();
            HitSoundingNode target = null!;
            AddStep("add separated windows", () =>
            {
                EditorBeatmap.BeginChange();
                for (int i = 0; i < 120; i++)
                {
                    EditorBeatmap.Add(new HitCircle
                    {
                        StartTime = 10000 + i * 500,
                        Samples = [new HitSampleInfo(HitSampleInfo.HIT_NORMAL, HitSampleInfo.BANK_DRUM, volume: 30 + i % 50)]
                    });
                }

                EditorBeatmap.EndChange();
                EditorClock.Seek(2000);
            });
            AddWaitStep("initial window settles", 3);
            AddStep("capture original controls", () =>
            {
                originalPoints.UnionWith(Editor.ChildrenOfType<HitSoundingSamplePoint>());
                originalObjects.UnionWith(Editor.ChildrenOfType<HitSoundingObject>());
                EditorClock.Seek(40000);
            });
            AddUntilStep("controls reused with new targets", () =>
                Editor.ChildrenOfType<HitSoundingSamplePoint>().Any(p => originalPoints.Contains(p) && p.Node.Time >= 30000)
                && Editor.ChildrenOfType<HitSoundingObject>().Any(o => originalObjects.Contains(o) && o.Item.StartTime >= 30000));
            AddStep("select rebound point", () =>
            {
                var point = Editor.ChildrenOfType<HitSoundingSamplePoint>()
                                  .First(p => p.Node.Time >= 40000 && p.Lane == HitSoundingLane.SampleSet);
                target = point.Node;
                InputManager.MoveMouseTo(point);
                InputManager.PressKey(Key.ControlLeft);
                InputManager.Click(MouseButton.Left);
                InputManager.ReleaseKey(Key.ControlLeft);
            });
            AddUntilStep("selection uses the new node", () => model.SelectedNodes.Contains(target));
            AddStep("edit new target", () => model.SetVolume([target], 17));
            AddUntilStep("visible volume reflects edit", () => Editor.ChildrenOfType<HitSoundingVolumeTrack>().Single().Segments
                .Any(s => s.Nodes.Contains(target) && s.DisplayVolume == 17));
            AddAssert("old nodes retain volume", () => model.Nodes.Where(n => n.Time < 10000).All(n => n.Volume is 70 or 90));
            AddStep("return to original window", () => EditorClock.Seek(2000));
            AddUntilStep("old window restored without stale targets", () => Editor.ChildrenOfType<HitSoundingSamplePoint>()
                .Where(p => p.Lane == HitSoundingLane.SampleSet).Any(p => p.Node.Time == 500 && p.Node.Volume == 70));
        }

        [Test]
        public void TestWorkspaceDividerResizesBothPanels()
        {
            HitSoundingRoll roll = null!;
            HitSoundingPreviewPanel panel = null!;
            float initialHeight = 0;
            float initialPanelHeight = 0;
            AddStep("drag workspace divider upwards", () =>
            {
                roll = Editor.ChildrenOfType<HitSoundingRoll>().Single();
                panel = Editor.ChildrenOfType<HitSoundingPreviewPanel>().Single();
                initialHeight = roll.Height;
                initialPanelHeight = panel.DrawHeight;
                var divider = Editor.ChildrenOfType<Drawable>().Single(d => d.Name == "Hitsounding workspace splitter");
                InputManager.MoveMouseTo(divider);
                InputManager.PressButton(MouseButton.Left);
                InputManager.MoveMouseTo(divider.ScreenSpaceDrawQuad.Centre - new Vector2(0, 100));
                InputManager.ReleaseButton(MouseButton.Left);
            });
            AddUntilStep("roll contracts and workspace grows", () => roll.Height < initialHeight - 50 && panel.DrawHeight > initialPanelHeight + 50);
            AddAssert("sample lanes remain accessible", () => Editor.ChildrenOfType<HitSoundingSamplePoint>()
                .Where(p => p.IsEnabled).All(p => p.ScreenSpaceDrawQuad.BottomLeft.Y <= panel.ScreenSpaceDrawQuad.TopLeft.Y));
        }

        [Test]
        public void TestVolumeGraphFollowsResizedRow()
        {
            HitSoundingVolumeTrack track = null!;
            float previousHeight = 0;
            AddStep("find volume graph", () => track = Editor.ChildrenOfType<HitSoundingVolumeTrack>().Single());
            AddAssert("graph fills initial row", followsRow);

            foreach (float offset in new[] { -100f, 160f, -80f })
            {
                AddStep("resize volume row " + offset, () =>
                {
                    previousHeight = track.DrawHeight;
                    var divider = Editor.ChildrenOfType<Drawable>().Single(d => d.Name == "Hitsounding workspace splitter");
                    InputManager.MoveMouseTo(divider);
                    InputManager.PressButton(MouseButton.Left);
                    InputManager.MoveMouseTo(divider.ScreenSpaceDrawQuad.Centre + new Vector2(0, offset));
                    InputManager.ReleaseButton(MouseButton.Left);
                });
                AddUntilStep("graph follows changed row", () => track.DrawHeight != previousHeight && followsRow());
            }

            TestVolumeDragMergesAndUndoes();

            bool followsRow()
            {
                float bottom = track.Parent!.ScreenSpaceDrawQuad.BottomLeft.Y;
                return System.Math.Abs(track.ScreenSpaceDrawQuad.BottomLeft.Y - bottom) < 0.1f
                       && System.Math.Abs(track.ToScreenSpace(new Vector2(0, track.YForVolume(0))).Y - bottom) < 0.1f
                       && track.Segments.Any(s => s.IsPresent)
                       && track.Segments.Where(s => s.IsPresent).All(s =>
                           System.Math.Abs(s.ChildrenOfType<Framework.Graphics.Shapes.Box>().Max(b => b.ScreenSpaceDrawQuad.BottomLeft.Y) - bottom) < 0.1f
                           && System.Math.Abs((track.DrawHeight - s.LineY) / (track.DrawHeight - track.YForVolume(100)) - s.DisplayVolume / 100f) < 0.001f);
            }
        }

        [Test]
        public void TestComposeRulerStaysAlignedThroughZoomSeekAndDivisorChanges()
        {
            Timeline timeline = null!;
            TimelineTickDisplay[] rulers = null!;
            TimelineTickDisplay ticks = null!;

            AddStep("find shared timeline", () =>
            {
                var screen = Editor.ChildrenOfType<HitSoundingScreen>().Single();
                timeline = screen.ChildrenOfType<Timeline>().Single();
                ticks = timeline.ChildrenOfType<TimelineTickDisplay>().Single();
                rulers = screen.ChildrenOfType<TimelineTickDisplay>().Where(t => t != ticks).ToArray();
            });
            AddUntilStep("ruler aligns with timeline", aligned);
            AddStep("zoom with original toolbar", () => Editor.ChildrenOfType<HitSoundingScreen>().Single().ChildrenOfType<TimelineButton>().First().TriggerClick());
            AddUntilStep("ruler and objects align after zoom", aligned);
            AddStep("seek shared timeline", () => EditorClock.Seek(3000));
            AddUntilStep("ruler aligns after seek", aligned);
            AddStep("open custom divisor", () =>
            {
                InputManager.MoveMouseTo(Editor.ChildrenOfType<HitSoundingScreen>().Single().ChildrenOfType<BeatDivisorControl.DivisorDisplay>().Single());
                InputManager.Click(MouseButton.Left);
            });
            AddUntilStep("custom divisor opens", () => Editor.ChildrenOfType<BeatDivisorControl.CustomDivisorPopover>().Any(p => p.IsPresent));
            AddStep("choose triplets", () => Editor.ChildrenOfType<BeatDivisorControl.CustomDivisorPopover>().Single(p => p.IsPresent).BeatDivisor.SetArbitraryDivisor(3));
            AddUntilStep("triplet ruler aligns", aligned);

            bool aligned()
            {
                float left = timeline.ScreenSpaceDrawQuad.TopLeft.X;
                float right = timeline.ScreenSpaceDrawQuad.TopRight.X;
                var visibleTicks = ticks.Children.Where(t => t.Alpha > 0 && t.ScreenSpaceDrawQuad.Centre.X >= left && t.ScreenSpaceDrawQuad.Centre.X <= right).ToArray();
                if (visibleTicks.Length == 0)
                    return false;

                foreach (var ruler in rulers)
                {
                    foreach (var tick in visibleTicks)
                    {
                        var line = ruler.Children.SingleOrDefault(l => l.Alpha > 0 && l.X == tick.X);
                        if (line == null || System.Math.Abs(line.ScreenSpaceDrawQuad.Centre.X - tick.ScreenSpaceDrawQuad.Centre.X) > 1)
                            return false;
                    }
                }

                var clap = Editor.ChildrenOfType<HitSoundingSamplePoint>().First(p => p.Lane == HitSoundingLane.Clap);
                return rulers.Any(r => r.ScreenSpaceDrawQuad.BottomLeft.Y > clap.ScreenSpaceDrawQuad.BottomLeft.Y)
                       && rulers.All(r => r.ScreenSpaceDrawQuad.BottomLeft.Y <= Editor.ChildrenOfType<HitSoundingPreviewPanel>().Single().ScreenSpaceDrawQuad.TopLeft.Y);
            }
        }

        [Test]
        public void TestBankPopupSharesSetAndResetPreservesAdditions()
        {
            AddStep("open normal bank editor", () => clickCell(HitSoundingLane.SampleSet));
            AddUntilStep("bank editor opens", () => Editor.ChildrenOfType<HitSoundingSamplePopover>().Any(p => p.IsPresent));
            AddStep("choose normal and custom set", () =>
            {
                var popup = Editor.ChildrenOfType<HitSoundingSamplePopover>().Single(p => p.IsPresent);
                popup.ChildrenOfType<FormDropdown<string>>().Single().Current.Value = HitSampleInfo.BANK_NORMAL;
                // The chooser includes sets already used by the map.
                popup.ChildrenOfType<FormDropdown<int>>().Single().Current.Value = 1;
                popup.ChildrenOfType<RoundedButton>().Single().TriggerClick();
            });
            AddUntilStep("normal bank and set applied", () => model.Nodes[0].GetBank(false) == HitSampleInfo.BANK_NORMAL && model.Nodes[0].SetIndex == 1);
            AddAssert("explicit addition remains", () => model.Nodes[0].GetBank(true), () => Is.EqualTo(HitSampleInfo.BANK_DRUM));
            AddAssert("index shared", () => model.Nodes[0].Samples.All(s => s.UseBeatmapSamples && s.Suffix == null));
            AddStep("change to custom normal two", () => model.SetBank(new[] { model.Nodes[0] }, HitSampleInfo.BANK_NORMAL, 2, false));
            AddUntilStep("custom normal two applied", () => model.Nodes[0].GetBank(false) == HitSampleInfo.BANK_NORMAL && model.Nodes[0].SetIndex == 2);
            AddStep("reset normal", () => clickCell(HitSoundingLane.SampleSet, MouseButton.Right));
            AddUntilStep("reset restores user skin normal", () => model.Nodes[0].GetBank(false) == HitSampleInfo.BANK_NORMAL && model.Nodes[0].SetIndex == 0);
            AddAssert("clap retained", () => model.Nodes[0].HasAddition(HitSampleInfo.HIT_CLAP));
            AddStep("undo reset", () => Editor.Undo());
            AddUntilStep("custom normal two restored", () => model.Nodes[0].GetBank(false) == HitSampleInfo.BANK_NORMAL && model.Nodes[0].SetIndex == 2);
            AddStep("reset addition bank", () => clickCell(HitSoundingLane.Additions, MouseButton.Right));
            AddUntilStep("addition follows normal", () => cell(HitSoundingLane.Additions).IsEmpty);
            AddAssert("addition reset keeps shared set", () => model.Nodes[0].SetIndex, () => Is.EqualTo(2));
        }

        [Test]
        public void TestIndependentAdditionCellsAndUndo()
        {
            AddStep("enable whistle", () => clickCell(HitSoundingLane.Whistle));
            AddUntilStep("whistle enabled", () => model.Nodes[0].HasAddition(HitSampleInfo.HIT_WHISTLE));
            AddStep("enable finish", () => clickCell(HitSoundingLane.Finish));
            AddUntilStep("finish enabled", () => model.Nodes[0].HasAddition(HitSampleInfo.HIT_FINISH));
            AddStep("disable clap", () => clickCell(HitSoundingLane.Clap));
            AddUntilStep("only clap removed", () =>
            {
                var node = model.Nodes[0];

                return !node.HasAddition(HitSampleInfo.HIT_CLAP)
                       && node.HasAddition(HitSampleInfo.HIT_WHISTLE)
                       && node.HasAddition(HitSampleInfo.HIT_FINISH);
            });
            AddAssert("other sound points untouched", () =>
            {
                var others = model.Nodes.Skip(1).ToArray();

                return others.All(n => n.HasAddition(HitSampleInfo.HIT_CLAP))
                       && others.All(n => !n.HasAddition(HitSampleInfo.HIT_WHISTLE))
                       && others.All(n => !n.HasAddition(HitSampleInfo.HIT_FINISH));
            });
            AddStep("undo clap toggle", () => Editor.Undo());
            AddUntilStep("clap restored", () => model.Nodes[0].HasAddition(HitSampleInfo.HIT_CLAP));
        }

        [Test]
        public void TestEmptyAdditionBankRequiresEnabledAddition()
        {
            AddStep("remove only addition", () => clickCell(HitSoundingLane.Clap));
            AddUntilStep("addition bank disabled", () => !cell(HitSoundingLane.Additions).IsEnabled);
            AddStep("try empty addition bank", () => clickCell(HitSoundingLane.Additions));
            AddAssert("no bank editor", () => !Editor.ChildrenOfType<HitSoundingSamplePopover>().Any(p => p.IsPresent));
            AddStep("enable whistle", () => clickCell(HitSoundingLane.Whistle));
            AddUntilStep("auto bank editable", () => cell(HitSoundingLane.Additions).IsEnabled && cell(HitSoundingLane.Additions).IsEmpty);
            AddStep("open auto bank", () => clickCell(HitSoundingLane.Additions));
            AddUntilStep("bank editor opens", () => Editor.ChildrenOfType<HitSoundingSamplePopover>().Any(p => p.IsPresent));
        }

        [Test]
        public void TestHoveredSetCyclePreservesSelectionAndUndo()
        {
            AddStep("hover unselected bank and cycle set", () =>
            {
                model.SelectedNodes.Clear();
                InputManager.MoveMouseTo(cell(HitSoundingLane.SampleSet));
                InputManager.PressKey(Key.ControlLeft);
                InputManager.ScrollVerticalBy(1);
                InputManager.ReleaseKey(Key.ControlLeft);
            });
            AddUntilStep("hovered set changes without selecting", () => model.Nodes[0].SetIndex == 1 && model.SelectedNodes.Count == 0);
            AddAssert("other points retain their sets", () => model.Nodes.Skip(1).All(n => n.SetIndex == 0));
            AddStep("undo set cycle", () => Editor.Undo());
            AddUntilStep("hovered set restored", () => model.Nodes[0].SetIndex == 0);
            AddStep("toggle hovered point with keyboard", () => InputManager.Key(Key.W));
            AddUntilStep("only hovered point gains whistle", () => model.Nodes[0].HasAddition(HitSampleInfo.HIT_WHISTLE) && model.Nodes.Skip(1).All(n => !n.HasAddition(HitSampleInfo.HIT_WHISTLE)));
        }

        [TestCase(HitSoundingLane.SampleSet, Key.ShiftLeft)]
        [TestCase(HitSoundingLane.Additions, Key.ShiftRight)]
        public void TestHoveredBankCyclePreservesSetsAndUndo(HitSoundingLane lane, Key shiftKey)
        {
            bool additionsOnly = lane == HitSoundingLane.Additions;
            string firstBank = additionsOnly ? HitObjectComposer.HIT_BANK_AUTO : HitSampleInfo.BANK_NORMAL;
            string selectedBank = additionsOnly ? HitSampleInfo.BANK_SOFT : HitSampleInfo.BANK_DRUM;
            double originalTime = 0;

            AddStep("prepare independent banks and sets", () =>
            {
                model.SelectedNodes.Clear();
                model.SetBank(model.Nodes, HitSampleInfo.BANK_DRUM, 0, false);
                model.SetBank(model.Nodes, HitSampleInfo.BANK_DRUM, -1, true);
                model.SetBank(new[] { model.Nodes[1] }, HitSampleInfo.BANK_DRUM, 2, false);
                originalTime = EditorClock.CurrentTime;
            });
            AddStep("cycle hovered bank forward", () => cycle(1));
            AddUntilStep("bank wraps without selecting", () => model.Nodes[0].GetBank(additionsOnly) == firstBank && model.SelectedNodes.Count == 0);
            AddAssert("other bank and other nodes unchanged", () =>
            {
                return model.Nodes[0].GetBank(!additionsOnly) == HitSampleInfo.BANK_DRUM
                       && model.Nodes.Skip(1).All(n => n.GetBank(additionsOnly) == HitSampleInfo.BANK_DRUM);
            });
            AddAssert("sets and playback position unchanged", () =>
            {
                return model.Nodes[0].SetIndex == 0 && model.Nodes[1].SetIndex == 2
                       && System.Math.Abs(EditorClock.CurrentTime - originalTime) < 0.5;
            });
            AddStep("cycle backward", () => cycle(-1));
            AddUntilStep("reverse wraps to drum", () => model.Nodes[0].GetBank(additionsOnly) == HitSampleInfo.BANK_DRUM);
            AddStep("undo backward cycle", () => Editor.Undo());
            AddUntilStep("undo restores first bank", () => model.Nodes[0].GetBank(additionsOnly) == firstBank);
            AddStep("select first two points", () =>
            {
                model.SelectedNodes.Add(model.Nodes[0]);
                model.SelectedNodes.Add(model.Nodes[1]);
            });
            AddStep("cycle selection two steps", () => cycle(2));
            AddUntilStep("both selected banks follow hovered bank", () =>
            {
                return model.Nodes.Take(2).All(n => n.GetBank(additionsOnly) == selectedBank)
                       && model.SelectedNodes.Count == 2;
            });
            AddAssert("heterogeneous sets preserved", () => model.Nodes[0].SetIndex == 0 && model.Nodes[1].SetIndex == 2);
            AddStep("undo selection cycle", () => Editor.Undo());
            AddUntilStep("single undo restores independent banks", () => model.Nodes[0].GetBank(additionsOnly) == firstBank
                                                                         && model.Nodes[1].GetBank(additionsOnly) == HitSampleInfo.BANK_DRUM);

            void cycle(int notches)
            {
                InputManager.MoveMouseTo(cell(lane));
                InputManager.PressKey(Key.ControlLeft);
                InputManager.PressKey(shiftKey);
                InputManager.ScrollVerticalBy(notches);
                InputManager.ReleaseKey(shiftKey);
                InputManager.ReleaseKey(Key.ControlLeft);
            }
        }

        private HitSoundingSamplePoint cell(HitSoundingLane lane)
            => Editor.ChildrenOfType<HitSoundingSamplePoint>().Single(p => p.Node == model.Nodes[0] && p.Lane == lane);

        private void clickCell(HitSoundingLane lane, MouseButton button = MouseButton.Left)
        {
            InputManager.MoveMouseTo(cell(lane));
            InputManager.Click(button);
        }

        private bool seekSettled
            => !EditorClock.IsSeeking && System.Math.Abs(EditorClock.CurrentTimeAccurate - EditorClock.CurrentTime) < 0.5;

        private void scrollWheel(int notches)
        {
            InputManager.MoveMouseTo(Editor.ChildrenOfType<HitSoundingVolumeTrack>().Single());
            InputManager.ScrollVerticalBy(notches);
        }
    }
}
