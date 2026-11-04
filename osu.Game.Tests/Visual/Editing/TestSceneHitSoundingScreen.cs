// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Localisation;
using osu.Framework.Testing;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Screens.Edit;
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
                InputManager.MoveMouseTo(Editor.ChildrenOfType<HitSoundingSamplePoint>().First(p => !p.AdditionsOnly));
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
        public void TestLeftClickEditsAndRightClickMovesSet()
        {
            AddStep("left click sample", () =>
            {
                InputManager.MoveMouseTo(Editor.ChildrenOfType<HitSoundingSamplePoint>().First(p => !p.AdditionsOnly));
                InputManager.Click(MouseButton.Left);
            });
            AddUntilStep("sample editor visible", () => Editor.ChildrenOfType<HitSoundingSamplePopover>().Any(p => p.IsPresent));
            AddStep("dismiss editor", () => InputManager.Key(Key.Escape));
            AddStep("right click selected sample", () =>
            {
                InputManager.MoveMouseTo(Editor.ChildrenOfType<HitSoundingSamplePoint>().First(p => !p.AdditionsOnly));
                InputManager.Click(MouseButton.Right);
            });
            AddUntilStep("context menu open", () => Editor.ChildrenOfType<OsuContextMenu>().Any(m => m.State == MenuState.Open));
            AddStep("move down one set", () =>
            {
                InputManager.MoveMouseTo(Editor.ChildrenOfType<OsuContextMenu>().First(m => m.State == MenuState.Open).ChildrenOfType<DrawableOsuMenuItem>().Last());
                InputManager.Click(MouseButton.Left);
            });
            AddUntilStep("all node samples moved together", () => EditorBeatmap.HitObjects[0].Samples.All(s => s.UseBeatmapSamples && s.Suffix == null));
            AddAssert("banks retained", () => EditorBeatmap.HitObjects[0].Samples.Select(s => s.Bank), () => Is.EqualTo(new[] { HitSampleInfo.BANK_SOFT, HitSampleInfo.BANK_DRUM }));
            AddStep("undo move", () => Editor.Undo());
            AddUntilStep("skin set restored", () => EditorBeatmap.HitObjects[0].Samples.All(s => !s.UseBeatmapSamples));
        }

        [Test]
        public void TestRightClickBlankClearsMultipleThenSelectsNearest()
        {
            HitSoundingNode nearest = null!;
            AddStep("select two nodes", () => model.SelectedNodes.AddRange(model.Nodes.Take(2)));
            AddStep("right click nearby blank lane", () =>
            {
                var point = Editor.ChildrenOfType<HitSoundingSamplePoint>().First(p => !p.AdditionsOnly);
                nearest = point.Node;
                InputManager.MoveMouseTo(point.ScreenSpaceDrawQuad.Centre + new Vector2(0, -22));
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
        public void TestSmoothWheelAndPinnedSetScrolling()
        {
            Vector2 objectPosition = default;
            Vector2 volumePosition = default;
            double time = 0;
            AddStep("create enough sets to scroll", () =>
            {
                for (int i = 0; i < model.Nodes.Count; i++)
                    model.SetBank(new[] { model.Nodes[i] }, HitSampleInfo.BANK_SOFT, i + 1, false);
            });
            AddUntilStep("set rows loaded", () => Editor.ChildrenOfType<HitSoundingLaneHeader>().Count() >= 8);
            AddStep("record fixed lane positions", () =>
            {
                objectPosition = Editor.ChildrenOfType<HitSoundingObject>().First().ScreenSpaceDrawQuad.Centre;
                volumePosition = Editor.ChildrenOfType<HitSoundingVolumeTrack>().Single().ScreenSpaceDrawQuad.Centre;
                time = EditorClock.CurrentTime;
            });
            AddStep("shift scroll sets", () =>
            {
                InputManager.MoveMouseTo(Editor.ChildrenOfType<HitSoundingVolumeTrack>().Single());
                InputManager.PressKey(Key.ShiftLeft);
                InputManager.ScrollVerticalBy(-5);
                InputManager.ReleaseKey(Key.ShiftLeft);
            });
            AddUntilStep("sets scrolled", () => Editor.ChildrenOfType<HitSoundingSetScrollContainer>().Single().Current > 0);
            AddAssert("object and volume stay pinned", () => Editor.ChildrenOfType<HitSoundingObject>().First().ScreenSpaceDrawQuad.Centre == objectPosition
                && Editor.ChildrenOfType<HitSoundingVolumeTrack>().Single().ScreenSpaceDrawQuad.Centre == volumePosition);
            AddAssert("shift wheel did not seek", () => EditorClock.CurrentTime, () => Is.EqualTo(time));
            AddStep("ordinary wheel", () => InputManager.ScrollVerticalBy(-1));
            AddUntilStep("smooth seek in flight", () => EditorClock.CurrentTimeAccurate > EditorClock.CurrentTime);
            AddUntilStep("seek completes", () => !EditorClock.IsSeeking && EditorClock.CurrentTime > time);
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
                foreach (var marker in Editor.ChildrenOfType<HitSoundingSamplePoint>().Where(p => !p.AdditionsOnly))
                {
                    var obj = Editor.ChildrenOfType<HitSoundingObject>().SingleOrDefault(o => o.Item == marker.Node.HitObject);
                    if (obj == null || System.Math.Abs(obj.ScreenSpaceDrawQuad.Centre.X - marker.ScreenSpaceDrawQuad.Centre.X) > 1)
                        return false;
                }

                return true;
            }
        }

        [Test]
        public void TestDraggedSampleFollowsCursorThenSettlesIntoLane()
        {
            HitSoundingSamplePoint point = null!;
            Vector2 start = default;
            Vector2 drumLane = default;

            AddStep("grab normal sample", () =>
            {
                point = Editor.ChildrenOfType<HitSoundingSamplePoint>().First(p => !p.AdditionsOnly && p.Node == model.Nodes[0]);
                drumLane = Editor.ChildrenOfType<HitSoundingSamplePoint>().Single(p => p.AdditionsOnly && p.Node == model.Nodes[0]).ScreenSpaceDrawQuad.Centre;
                start = point.ScreenSpaceDrawQuad.Centre;
                InputManager.MoveMouseTo(start);
                InputManager.PressButton(MouseButton.Left);
                InputManager.MoveMouseTo(start + new Vector2(0, 10));
            });
            AddStep("drag towards drum lane", () => InputManager.MoveMouseTo(drumLane + new Vector2(30, HitSoundingLayout.BANK_HEIGHT / 4)));
            AddUntilStep("badge follows cursor", () => Vector2.Distance(point.ScreenSpaceDrawQuad.Centre, InputManager.CurrentState.Mouse.Position) < 1);
            float firstFrameDistance = float.NaN;
            AddStep("drop off-centre", () =>
            {
                point.OnUpdate += recordFirstFrame;
                InputManager.ReleaseButton(MouseButton.Left);
            });
            AddUntilStep("first frame after drop recorded", () => !float.IsNaN(firstFrameDistance));
            AddAssert("badge starts from release point", () => firstFrameDistance, () => Is.GreaterThan(10));
            AddUntilStep("bank changed", () => EditorBeatmap.HitObjects[0].Samples.Single(s => s.Name == HitSampleInfo.HIT_NORMAL).Bank == HitSampleInfo.BANK_DRUM);
            AddUntilStep("badge settles into drum lane", () => Vector2.Distance(point.ScreenSpaceDrawQuad.Centre, drumLane) < 1);

            AddStep("drag onto lane headers", () =>
            {
                start = point.ScreenSpaceDrawQuad.Centre;
                InputManager.MoveMouseTo(start);
                InputManager.PressButton(MouseButton.Left);
                InputManager.MoveMouseTo(start + new Vector2(0, 10));
            });
            AddStep("move outside lanes", () => InputManager.MoveMouseTo(Editor.ChildrenOfType<HitSoundingLaneHeader>().First()));
            AddStep("drop invalid", () => InputManager.ReleaseButton(MouseButton.Left));
            AddUntilStep("badge returns to its lane", () => Vector2.Distance(point.ScreenSpaceDrawQuad.Centre, start) < 1);
            AddAssert("bank unchanged", () => EditorBeatmap.HitObjects[0].Samples.Single(s => s.Name == HitSampleInfo.HIT_NORMAL).Bank, () => Is.EqualTo(HitSampleInfo.BANK_DRUM));

            void recordFirstFrame(Drawable d)
            {
                if (point.IsDragged)
                    return;

                firstFrameDistance = Vector2.Distance(point.ScreenSpaceDrawQuad.Centre, drumLane);
                point.OnUpdate -= recordFirstFrame;
            }
        }

        [Test]
        public void TestCollapsedSetShrinksMovesBelowExpandedAndExpandsOnClick()
        {
            HitSoundingRoll roll = null!;
            string[] samplesBefore = null!;
            int[] setIndicesBefore = null!;
            float[] markerXsBefore = null!;
            float collapseFirstHeight = float.NaN;
            float expandFirstHeight = float.NaN;

            AddStep("get roll", () => roll = Editor.ChildrenOfType<HitSoundingRoll>().Single());
            AddStep("move last four samples to custom set", () => model.SetBank(model.Nodes.Skip(4).ToArray(), HitSampleInfo.BANK_SOFT, 2, false));
            AddUntilStep("set rows loaded", () => headerOrder(roll).SequenceEqual(new[] { 0, 1, 2 }) && model.Nodes.Count(n => n.SetIndex == 2) == 4);

            AddStep("record samples and marker positions", () =>
            {
                samplesBefore = sampleSnapshot();
                setIndicesBefore = model.Nodes.Select(n => n.SetIndex).ToArray();
                markerXsBefore = model.Nodes.Where(n => n.SetIndex == 0)
                                               .Select(n => Editor.ChildrenOfType<HitSoundingSamplePoint>().Single(p => p.Node == n && !p.AdditionsOnly).ScreenSpaceDrawQuad.Centre.X)
                                               .OrderBy(x => x)
                                               .ToArray();
            });

            AddStep("click expanded set header", () =>
            {
                var header = headerFor(roll, 0);
                header.OnUpdate += recordCollapseFrame;
                clickHeader(roll, 0);
            });
            AddUntilStep("set collapsed", () => headerFor(roll, 0).IsCollapsed);
            AddUntilStep("first collapse frame recorded", () => !float.IsNaN(collapseFirstHeight));
            AddAssert("collapse does not jump to final height", () => collapseFirstHeight > HitSoundingLayout.COLLAPSED_SET_HEIGHT);
            AddUntilStep("collapse animation settles", () => rowsFollowLayout(roll));

            AddAssert("collapsed row is one text line high", () => headerFor(roll, 0).Height, () => Is.EqualTo(22f));
            AddAssert("expanded rows keep their height", () =>
                headerFor(roll, 1).Height == HitSoundingLayout.SET_HEIGHT && headerFor(roll, 2).Height == HitSoundingLayout.SET_HEIGHT);
            AddAssert("collapsed set moved below expanded sets", () => headerOrder(roll), () => Is.EqualTo(new[] { 1, 2, 0 }));
            AddAssert("display order matches headers", () => roll.SetLayout.DisplayOrder, () => Is.EqualTo(headerOrder(roll)));
            AddAssert("rows follow layout bounds", () => rowsFollowLayout(roll));
            AddUntilStep("collapsed set badges removed", () => !roll.ChildrenOfType<HitSoundingSamplePoint>().Any(p => p.Node.SetIndex == 0));
            AddUntilStep("expanded set badges remain", () => roll.ChildrenOfType<HitSoundingSamplePoint>().Any(p => p.Node.SetIndex == 2));
            AddUntilStep("sample dots rendered", () => collapsedDots(roll).Count() == 4);
            AddAssert("dots are pink and ten pixels wide", () =>
                collapsedDots(roll).All(d => d.Size == new Vector2(HitSoundingLayout.COLLAPSED_SAMPLE_SIZE) && d.Colour == new OsuColour().Pink1));
            AddAssert("dots sit in the collapsed row", () =>
                collapsedDots(roll).All(d => System.Math.Abs(d.Position.Y - (headerFor(roll, 0).Y + HitSoundingLayout.COLLAPSED_SET_HEIGHT / 2)) < 0.01f));
            AddAssert("dots line up with sample times", () =>
            {
                float[] xs = collapsedDots(roll).Select(d => d.ScreenSpaceDrawQuad.Centre.X).OrderBy(x => x).ToArray();
                return xs.Length == markerXsBefore.Length && xs.Zip(markerXsBefore, (a, b) => System.Math.Abs(a - b) < 1).All(match => match);
            });
            AddAssert("samples unchanged by collapse", () => sampleSnapshot(), () => Is.EqualTo(samplesBefore));
            AddAssert("set indices unchanged by collapse", () => model.Nodes.Select(n => n.SetIndex), () => Is.EqualTo(setIndicesBefore));

            AddStep("click collapsed set header", () =>
            {
                var header = headerFor(roll, 0);
                header.OnUpdate += recordExpandFrame;
                clickHeader(roll, 0);
            });
            AddUntilStep("set expanded", () => !headerFor(roll, 0).IsCollapsed);
            AddUntilStep("first expansion frame recorded", () => !float.IsNaN(expandFirstHeight));
            AddAssert("expansion does not jump to final height", () => expandFirstHeight < HitSoundingLayout.SET_HEIGHT);
            AddUntilStep("expansion animation settles", () => rowsFollowLayout(roll));

            AddAssert("expanded row is full height", () => headerFor(roll, 0).Height, () => Is.EqualTo(HitSoundingLayout.SET_HEIGHT));
            AddAssert("expanded set returns above collapsed space", () => headerOrder(roll), () => Is.EqualTo(new[] { 0, 1, 2 }));
            AddAssert("rows follow layout bounds after expanding", () => rowsFollowLayout(roll));
            AddUntilStep("badges restored", () => roll.ChildrenOfType<HitSoundingSamplePoint>().Any(p => p.Node.SetIndex == 0));
            AddAssert("collapsed dots removed", () => !collapsedDots(roll).Any());
            AddAssert("samples unchanged by expanding", () => sampleSnapshot(), () => Is.EqualTo(samplesBefore));

            void recordCollapseFrame(Drawable drawable)
            {
                collapseFirstHeight = drawable.Height;
                drawable.OnUpdate -= recordCollapseFrame;
            }

            void recordExpandFrame(Drawable drawable)
            {
                expandFirstHeight = drawable.Height;
                drawable.OnUpdate -= recordExpandFrame;
            }
        }

        [Test]
        public void TestSetOrderOnlyChangesPresentation()
        {
            HitSoundingRoll roll = null!;
            string[] samplesBefore = null!;

            AddStep("get roll", () => roll = Editor.ChildrenOfType<HitSoundingRoll>().Single());
            AddStep("move last four samples to custom set", () => model.SetBank(model.Nodes.Skip(4).ToArray(), HitSampleInfo.BANK_SOFT, 2, false));
            AddUntilStep("three sets loaded", () => headerOrder(roll).SequenceEqual(new[] { 0, 1, 2 }));
            AddStep("record samples", () => samplesBefore = sampleSnapshot());

            AddStep("open set management", () => clickManagementButton(roll));
            AddUntilStep("management open", () => Editor.ChildrenOfType<HitSoundingSetManagementPopover>().Any(p => p.IsPresent));
            AddUntilStep("management rows ready", () => Editor.ChildrenOfType<HitSoundingSetManagementPopover>().Any(p => p.IsPresent && p.ChildrenOfType<IconButton>().Any(b => b.IsLoaded)));

            AddStep("move custom set up from menu", () => clickManagementButton(managementPopover(), 2, HitSoundingStrings.MoveSetUp));
            AddUntilStep("headers reordered", () => headerOrder(roll).SequenceEqual(new[] { 0, 2, 1 }));
            AddUntilStep("management reflects new order", () => menuButtonOrDefault(managementPopover(), 1, HitSoundingStrings.MoveSetDown)?.Enabled.Value == false);
            AddUntilStep("rows settle into order", () => rowsFollowLayout(roll));
            AddAssert("move only changed presentation", () => sampleSnapshot(), () => Is.EqualTo(samplesBefore));

            AddStep("collapse custom set from menu", () => clickManagementButton(managementPopover(), 2, HitSoundingStrings.CollapseSet));
            AddUntilStep("collapsed set moves to bottom", () => headerOrder(roll).SequenceEqual(new[] { 0, 1, 2 }));
            AddAssert("collapsed set cannot move into expanded group", () => !roll.SetLayout.CanMove(2, -1));
            AddAssert("collapse only changed presentation", () => sampleSnapshot(), () => Is.EqualTo(samplesBefore));
        }

        private static IEnumerable<HitSoundingLaneHeader> orderedHeaders(HitSoundingRoll roll)
            => roll.ChildrenOfType<HitSoundingLaneHeader>().OrderBy(h => h.Y);

        private static int[] headerOrder(HitSoundingRoll roll) => orderedHeaders(roll).Select(h => h.SetIndex).ToArray();

        private static HitSoundingLaneHeader headerFor(HitSoundingRoll roll, int set)
            => roll.ChildrenOfType<HitSoundingLaneHeader>().Single(h => h.SetIndex == set);

        private static IEnumerable<Circle> collapsedDots(HitSoundingRoll roll)
            => roll.ChildrenOfType<Circle>().Where(c => c.Name == "collapsed sample");

        private bool seekSettled
            => !EditorClock.IsSeeking && System.Math.Abs(EditorClock.CurrentTimeAccurate - EditorClock.CurrentTime) < 0.5;

        private void scrollWheel(int notches)
        {
            InputManager.MoveMouseTo(Editor.ChildrenOfType<HitSoundingVolumeTrack>().Single());
            InputManager.ScrollVerticalBy(notches);
        }

        private static bool rowsFollowLayout(HitSoundingRoll roll)
        {
            HitSoundingLaneHeader[] headers = orderedHeaders(roll).ToArray();

            for (int i = 0; i < headers.Length; i++)
            {
                HitSoundingLaneHeader header = headers[i];

                if (System.Math.Abs(header.Y - roll.SetLayout.RowY(header.SetIndex)!.Value) > 0.01f)
                    return false;

                if (i < headers.Length - 1 && System.Math.Abs(header.Y + header.Height + HitSoundingLayout.SET_SPACING - headers[i + 1].Y) > 0.01f)
                    return false;
            }

            return headers.Length > 0
                   && System.Math.Abs(roll.SetLayout.TotalHeight - (headers[^1].Y + headers[^1].Height + HitSoundingLayout.SET_SPACING)) < 0.01f;
        }

        private string[] sampleSnapshot()
            => EditorBeatmap.HitObjects.Select(h => string.Join("/", h.Samples.Select(s => $"{s.Name}|{s.Bank}|{s.Suffix}|{s.Volume}|{s.UseBeatmapSamples}"))).ToArray();

        private void clickHeader(HitSoundingRoll roll, int set)
        {
            InputManager.MoveMouseTo(headerFor(roll, set));
            InputManager.Click(MouseButton.Left);
        }

        private HitSoundingSetManagementPopover managementPopover()
            => Editor.ChildrenOfType<HitSoundingSetManagementPopover>().Single(p => p.IsPresent);

        private void clickManagementButton(HitSoundingRoll roll)
        {
            InputManager.MoveMouseTo(roll.ChildrenOfType<HitSoundingSetManagementButton>().Single());
            InputManager.Click(MouseButton.Left);
        }

        private void clickManagementButton(HitSoundingSetManagementPopover popover, int set, LocalisableString tooltip)
        {
            InputManager.MoveMouseTo(menuButton(popover, set, tooltip));
            InputManager.Click(MouseButton.Left);
        }

        private static IconButton menuButton(HitSoundingSetManagementPopover popover, int set, LocalisableString tooltip)
            => menuButtonOrDefault(popover, set, tooltip) ?? throw new System.InvalidOperationException($"No {tooltip} button for set {set}.");

        private static IconButton? menuButtonOrDefault(HitSoundingSetManagementPopover popover, int set, LocalisableString tooltip)
        {
            string label = (set == 0 ? HitSoundingStrings.UserSkin : HitSoundingStrings.CustomSet(set)).ToString();

            return popover.ChildrenOfType<IconButton>().SingleOrDefault(b =>
                b.TooltipText.ToString() == tooltip.ToString()
                && b.Parent?.Parent?.ChildrenOfType<TruncatingSpriteText>().Any(t => t.Text.ToString() == label) == true);
        }
    }
}
