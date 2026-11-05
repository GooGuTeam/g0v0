// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Audio;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.HitSounding;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.Tests.Visual.Editing
{
    public partial class TestSceneHitSoundingSamplePlayback : EditorTestScene
    {
        private HitSoundingSamplePlayback playback = null!;
        private EditorSkinProvidingContainer skinProvider = null!;

        protected override Ruleset CreateEditorRuleset() => new OsuRuleset();

        [Test]
        public void TestPreviewSeekPlaysSkinnableSamples()
        {
            createPlayback();

            AddUntilStep("beatmap settled", () => !EditorBeatmap.UpdateInProgress.Value);
            AddStep("preview across hit", () => playback.PreviewSeek(900, 1100));
            AddUntilStep("sample played", hasPlayedSample);
        }

        [Test]
        public void TestClockPlaybackPlaysSkinnableSamples()
        {
            createPlayback();

            AddUntilStep("beatmap settled", () => !EditorBeatmap.UpdateInProgress.Value);
            AddStep("seek before hit", () => EditorClock.Seek(500));
            AddStep("start playback", () => EditorClock.Start());
            AddUntilStep("sample played", hasPlayedSample);
            AddStep("stop playback", () => EditorClock.Stop());
        }

        [TestCase(-2)]
        [TestCase(-1)]
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void TestSetPreviewOnlyPlaysTargetNode(int nodeIndex)
        {
            Slider slider = null!;
            HitSoundingNode target = null!;
            var heard = new List<HitSoundingNode>();
            double end = 0;

            AddStep("add surrounding hits and repeating slider", () =>
            {
                foreach (var hitObject in EditorBeatmap.HitObjects.ToArray())
                    EditorBeatmap.Remove(hitObject);

                EditorBeatmap.Add(new HitCircle { StartTime = 750, Samples = [new HitSampleInfo(HitSampleInfo.HIT_NORMAL)] });
                EditorBeatmap.Add(slider = new Slider
                {
                    StartTime = 1000,
                    RepeatCount = 1,
                    Path = new SliderPath(new[] { new PathControlPoint(Vector2.Zero), new PathControlPoint(new Vector2(100, 0)) }),
                    Samples = [new HitSampleInfo(HitSampleInfo.HIT_NORMAL), new HitSampleInfo(HitSampleInfo.HIT_WHISTLE)],
                    NodeSamples =
                    [
                        new List<HitSampleInfo> { new HitSampleInfo(HitSampleInfo.HIT_NORMAL) },
                        new List<HitSampleInfo> { new HitSampleInfo(HitSampleInfo.HIT_NORMAL) },
                        new List<HitSampleInfo> { new HitSampleInfo(HitSampleInfo.HIT_NORMAL) }
                    ]
                });
                playback.NodePlayed += heard.Add;
            });
            AddUntilStep("beatmap settled", () => !EditorBeatmap.UpdateInProgress.Value);
            AddStep("preview selected point with music", () =>
            {
                var model = Editor.ChildrenOfType<HitSoundingEditModel>().Single();
                target = nodeIndex == -2 ? model.Nodes.Single(n => n.HitObject is HitCircle) : model.Nodes.Single(n => n.HitObject == slider && n.NodeIndex == nodeIndex);
                end = slider.EndTime + 250;
                playback.StartSetPreview(0, 500, end, new[] { target });
            });
            AddUntilStep("music clock plays inside preview range", () => EditorClock.IsRunning && EditorClock.CurrentTime > 550 && EditorClock.CurrentTime < end);
            AddUntilStep("preview reaches range end", () => playback.PreviewSetIndex == null && !EditorClock.IsRunning);
            AddAssert("only selected node sounded", () => heard, () => Is.EqualTo(new[] { target }));
            AddAssert("selected sample actually played", hasPlayedSample);
            AddAssert("playhead stops at range boundary", () => EditorClock.CurrentTime, () => Is.EqualTo(end).Within(1));
            AddStep("resume normal playback", () =>
            {
                heard.Clear();
                EditorClock.Seek(500);
                EditorClock.Start();
            });
            AddUntilStep("normal playback reaches slider tail", () => EditorClock.CurrentTime > slider.EndTime + 100);
            AddStep("stop playback", () => EditorClock.Stop());
            AddAssert("normal playback restores all five nodes", () => heard.Count == 5 && heard.Distinct().Count() == 5);
            AddStep("detach playback observer", () => playback.NodePlayed -= heard.Add);
        }

        private void createPlayback()
        {
            AddStep("add hit object", () => EditorBeatmap.Add(new HitCircle
            {
                StartTime = 1000,
                Samples = [new HitSampleInfo(HitSampleInfo.HIT_NORMAL)],
            }));

            AddStep("open hitsounding screen", () =>
            {
                Editor.Mode.Value = EditorScreenMode.HitSounding;
                EditorClock.Seek(0);
            });

            AddUntilStep("playback loaded", () => playback.IsLoaded);
            AddUntilStep("hitnormal sample available", () => skinProvider.GetSample(new HitSampleInfo(HitSampleInfo.HIT_NORMAL)) != null);
        }

        public override void SetUpSteps()
        {
            base.SetUpSteps();

            AddStep("resolve hitsounding playback", () =>
            {
                Editor.Mode.Value = EditorScreenMode.HitSounding;
            });

            AddUntilStep("playback available", () => Editor.ChildrenOfType<HitSoundingSamplePlayback>().SingleOrDefault()?.IsLoaded == true);

            AddStep("capture playback", () =>
            {
                playback = Editor.ChildrenOfType<HitSoundingSamplePlayback>().Single();
                skinProvider = Editor.ChildrenOfType<EditorSkinProvidingContainer>().First(p => p.ChildrenOfType<HitSoundingSamplePlayback>().Any());
            });
        }

        private bool hasPlayedSample()
            => playback.ChildrenOfType<PoolableSkinnableSample>().Any(sample => sample.Played);
    }
}
