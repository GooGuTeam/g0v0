// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Audio;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.HitSounding;
using osu.Game.Skinning;

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
            AddUntilStep("sample played", () => hasPlayedSample());
        }

        [Test]
        public void TestClockPlaybackPlaysSkinnableSamples()
        {
            createPlayback();

            AddUntilStep("beatmap settled", () => !EditorBeatmap.UpdateInProgress.Value);
            AddStep("seek before hit", () => EditorClock.Seek(500));
            AddStep("start playback", () => EditorClock.Start());
            AddUntilStep("sample played", () => hasPlayedSample());
            AddStep("stop playback", () => EditorClock.Stop());
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

            AddUntilStep("playback loaded", () => playback?.IsLoaded == true);
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
