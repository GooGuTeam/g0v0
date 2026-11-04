// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Testing;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.Compose;
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

        public override void SetUpSteps()
        {
            base.SetUpSteps();
            AddStep("add sample sequence", () =>
            {
                for (int i = 0; i < 8; i++)
                    EditorBeatmap.Add(new HitCircle
                    {
                        StartTime = 500 + i * 500,
                        Samples =
                        [
                            new HitSampleInfo(HitSampleInfo.HIT_NORMAL, HitSampleInfo.BANK_SOFT, volume: i < 4 ? 70 : 90),
                            new HitSampleInfo(HitSampleInfo.HIT_CLAP, HitSampleInfo.BANK_DRUM, volume: i < 4 ? 70 : 90, editorAutoBank: false)
                        ]
                    });
                Editor.Mode.Value = EditorScreenMode.HitSounding;
                EditorClock.Seek(2000);
                EditorClock.Stop();
            });
            AddUntilStep("roll loaded", () => Editor.ChildrenOfType<HitSoundingSamplePoint>().Any(p => p.IsLoaded));
        }

        [Test]
        public void TestBadgesKeepSizeAfterComposeSixteenthDivisorAndZoom()
        {
            Vector2 badgeSize = Vector2.Zero;
            Vector2[] textScales = [];
            float originalSpacing = 0;

            AddStep("record badge and text size", () =>
            {
                badgeSize = cell(HitSoundingLane.SampleSet).DrawSize;
                textScales = cell(HitSoundingLane.SampleSet).ChildrenOfType<OsuSpriteText>().Select(t => t.Scale).ToArray();
                originalSpacing = pointSpacing();
            });
            AddStep("switch to Compose", () => Editor.Mode.Value = EditorScreenMode.Compose);
            AddUntilStep("Compose loaded", () => Editor.ChildrenOfType<ComposeScreen>().Any(s => s.IsLoaded && s.IsPresent));
            AddStep("choose 1/16 in Compose", () => Editor.Dependencies.Get<BindableBeatDivisor>().Value = 16);
            AddStep("return to Hitsounding", () => Editor.Mode.Value = EditorScreenMode.HitSounding);
            AddUntilStep("Hitsounding loaded", () => Editor.ChildrenOfType<HitSoundingScreen>().Any(s => s.IsLoaded && s.IsPresent));
            AddUntilStep("badges retain size at 1/16", badgesKeepSize);
            AddStep("zoom out", () => zoom(-8));
            AddUntilStep("spacing shrinks but badges keep size", () => pointSpacing() < originalSpacing && badgesKeepSize());
            AddStep("zoom back in", () => zoom(8));
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
                    new List<HitSampleInfo> { new(HitSampleInfo.HIT_NORMAL, volume: 60) },
                    new List<HitSampleInfo> { new(HitSampleInfo.HIT_NORMAL, volume: 60) },
                    new List<HitSampleInfo> { new(HitSampleInfo.HIT_NORMAL, volume: 60) }
                ]
            }));
            AddUntilStep("slider nodes loaded", () => model.Nodes.Count(n => n.HitObject is Slider) == 4);
            AddStep("edit slider tail", () => model.SetVolume(new[] { model.Nodes.Single(n => n.HitObject is Slider && n.NodeIndex == 2) }, 40));
            AddUntilStep("tail changed", () => EditorBeatmap.HitObjects.OfType<Slider>().Single().NodeSamples[2][0].Volume == 40);
            AddAssert("head and repeat unchanged", () => EditorBeatmap.HitObjects.OfType<Slider>().Single().NodeSamples.Take(2).All(s => s[0].Volume == 60));
            AddAssert("body unchanged", () => EditorBeatmap.HitObjects.OfType<Slider>().Single().Samples[0].Volume == 70);
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
                return track.Segments.Count(s => s.EndX <= 0) == 2 && track.Segments.Where(s => s.EndX <= 0).All(s => !s.IsPresent);
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
                        new List<HitSampleInfo> { new(HitSampleInfo.HIT_NORMAL) },
                        new List<HitSampleInfo> { new(HitSampleInfo.HIT_NORMAL) },
                        new List<HitSampleInfo> { new(HitSampleInfo.HIT_NORMAL) },
                        new List<HitSampleInfo> { new(HitSampleInfo.HIT_NORMAL) }
                    ]
                });
            });
            AddUntilStep("sound points loaded", () => model.Nodes.Any(n => System.Math.Abs(n.Time - first_off_grid) < 0.5)
                && model.Nodes.Any(n => System.Math.Abs(n.Time - second_off_grid) < 0.5)
                && model.Nodes.Count(n => n.HitObject == slider && n.Time > slider_start + 1) >= 3);

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
            AddAssert("landed on a nested slider sample time", () => model.Nodes.Any(n => n.HitObject == slider && n.Time > slider_start + 1
                && System.Math.Abs(n.Time - EditorClock.CurrentTime) < 0.5));

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
            AddUntilStep("normal set one has a badge without custom samples", () => !cell(HitSoundingLane.SampleSet).IsEmpty);
            AddAssert("explicit addition remains", () => model.Nodes[0].GetBank(true), () => Is.EqualTo(HitSampleInfo.BANK_DRUM));
            AddAssert("index shared", () => model.Nodes[0].Samples.All(s => s.UseBeatmapSamples && s.Suffix == null));
            AddStep("change to custom normal two", () => model.SetBank(new[] { model.Nodes[0] }, HitSampleInfo.BANK_NORMAL, 2, false));
            AddUntilStep("normal two has a badge", () => !cell(HitSoundingLane.SampleSet).IsEmpty && model.Nodes[0].SetIndex == 2);
            AddStep("reset normal", () => clickCell(HitSoundingLane.SampleSet, MouseButton.Right));
            AddUntilStep("reset restores blank user skin normal", () => cell(HitSoundingLane.SampleSet).IsEmpty && model.Nodes[0].SetIndex == 0);
            AddAssert("clap retained", () => model.Nodes[0].HasAddition(HitSampleInfo.HIT_CLAP));
            AddStep("undo reset", () => Editor.Undo());
            AddUntilStep("custom normal two restored", () => model.Nodes[0].SetIndex == 2 && !cell(HitSoundingLane.SampleSet).IsEmpty);
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
            AddUntilStep("only clap removed", () => !model.Nodes[0].HasAddition(HitSampleInfo.HIT_CLAP)
                && model.Nodes[0].HasAddition(HitSampleInfo.HIT_WHISTLE) && model.Nodes[0].HasAddition(HitSampleInfo.HIT_FINISH));
            AddAssert("other sound points untouched", () => model.Nodes.Skip(1).All(n => n.HasAddition(HitSampleInfo.HIT_CLAP)
                && !n.HasAddition(HitSampleInfo.HIT_WHISTLE) && !n.HasAddition(HitSampleInfo.HIT_FINISH)));
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
