// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Testing;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Osu;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.HitSounding;
using osu.Game.Storyboards;
using osu.Game.Tests.Resources;

namespace osu.Game.Tests.Visual.Editing
{
    public partial class TestSceneHitSoundingSampleLibrary : EditorTestScene
    {
        private WorkingBeatmap working = null!;
        private string directory = null!;
        private HitSoundingSampleLibrary library => Editor.ChildrenOfType<HitSoundingSampleLibrary>().Single();

        protected override bool UseFreshStoragePerRun => true;
        protected override bool IsolateSavingFromDatabase => false;

        protected override Ruleset CreateEditorRuleset() => new OsuRuleset();

        protected override WorkingBeatmap CreateWorkingBeatmap(IBeatmap beatmap, Storyboard? storyboard = null)
        {
            var manager = Dependencies.Get<BeatmapManager>();
            var imported = manager.Import(new ImportTask(TestResources.GetTestBeatmapStream(), "library.osz")).GetResultSafely()!;
            return working = manager.GetWorkingBeatmap(imported.Value.Beatmaps.First());
        }

        public override void SetUpSteps()
        {
            base.SetUpSteps();
            AddStep("open hitsounding", () => Editor.Mode.Value = EditorScreenMode.HitSounding);
            AddUntilStep("library loaded", () => Editor.ChildrenOfType<HitSoundingSampleLibrary>().Any(l => l.IsLoaded));
            AddStep("prepare samples", () =>
            {
                directory = Path.Combine(Path.GetTempPath(), "g0v0-library-" + Guid.NewGuid());
                Directory.CreateDirectory(directory);
                using var stream = TestResources.OpenResource("Samples/hitsound-no-delay.wav");
                using var file = File.Create(Path.Combine(directory, new string('a', 110) + ".wav"));
                stream.CopyTo(file);
            });
        }

        [Test]
        public void TestImportReturnsStoredNames()
        {
            AddStep("import twice and skip missing file", () =>
            {
                string path = Directory.GetFiles(directory).Single();
                var names = library.ImportFiles(new[] { path, path, Path.Combine(directory, "missing.wav") });
                Assert.That(names, Is.EqualTo(new[] { new string('a', 100) + ".wav", new string('a', 100) + "-1.wav" }));
                Assert.That(names.All(name => working.BeatmapSetInfo.GetFile(name) != null), Is.True);
            });
        }

        [Test]
        public void TestReferencedCanonicalSourcesSurviveReplacementAndSetRemoval()
        {
            string original = null!;
            string preserved;
            AddStep("import sample and create canonical source", () =>
            {
                original = library.ImportFiles(Directory.GetFiles(directory)).Single();
                library.Assign(1, HitSampleInfo.BANK_NORMAL, HitSampleInfo.HIT_NORMAL, original);
                library.Assign(2, HitSampleInfo.BANK_SOFT, HitSampleInfo.HIT_FINISH, "normal-hitnormal.wav");
            });
            AddStep("replace source slot", () =>
            {
                library.Assign(1, HitSampleInfo.BANK_NORMAL, HitSampleInfo.HIT_NORMAL, original);
                preserved = library.GetAssignment(2, HitSampleInfo.BANK_SOFT, HitSampleInfo.HIT_FINISH)!;
                Assert.That(preserved, Is.Not.EqualTo("normal-hitnormal.wav"));
                Assert.That(working.BeatmapSetInfo.GetFile(preserved), Is.Not.Null);
            });
            AddStep("remove source slot with a dependent assignment", () =>
            {
                library.Assign(4, HitSampleInfo.BANK_SOFT, HitSampleInfo.HIT_CLAP, "normal-hitnormal.wav");
                library.RemoveAssignment(1, HitSampleInfo.BANK_NORMAL, HitSampleInfo.HIT_NORMAL);
                string source = library.GetAssignment(4, HitSampleInfo.BANK_SOFT, HitSampleInfo.HIT_CLAP)!;
                Assert.That(working.BeatmapSetInfo.GetFile(source), Is.Not.Null);
                Assert.That(working.BeatmapSetInfo.GetFile("normal-hitnormal.wav"), Is.Null);
            });
            AddStep("reference and remove set", () =>
            {
                library.Assign(3, HitSampleInfo.BANK_DRUM, HitSampleInfo.HIT_CLAP, "soft-hitfinish2.wav");
                library.RemoveSet(2);
                Assert.That(library.Sets, Does.Not.Contain(2));
                Assert.That(working.BeatmapSetInfo.GetFile("soft-hitfinish2.wav"), Is.Null);
                string source = library.GetAssignment(3, HitSampleInfo.BANK_DRUM, HitSampleInfo.HIT_CLAP)!;
                Assert.That(working.BeatmapSetInfo.GetFile(source), Is.Not.Null);
                Assert.That(library.Sets, Does.Contain(3));
            });
            AddStep("remove empty set", () =>
            {
                library.AddSet();
                int added = library.Sets.Last();
                library.RemoveSet(added);
                Assert.That(library.Sets, Does.Not.Contain(added));
            });
            AddStep("reload persisted library", () =>
            {
                var restored = new HitSoundingSampleLibrary();
                Add(new DependencyProvidingContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    CachedDependencies =
                    [
                        (typeof(EditorBeatmap), EditorBeatmap),
                        (typeof(IBindable<WorkingBeatmap>), Beatmap)
                    ],
                    Child = restored
                });
            });
            AddUntilStep("references restored",
                () => this.ChildrenOfType<HitSoundingSampleLibrary>().Any(l =>
                    l != library
                    && l.IsLoaded
                    && l.GetAssignment(3, HitSampleInfo.BANK_DRUM, HitSampleInfo.HIT_CLAP) == library.GetAssignment(3, HitSampleInfo.BANK_DRUM, HitSampleInfo.HIT_CLAP)
                    && l.GetAssignment(4, HitSampleInfo.BANK_SOFT, HitSampleInfo.HIT_CLAP) == library.GetAssignment(4, HitSampleInfo.BANK_SOFT, HitSampleInfo.HIT_CLAP)
                    && !l.Sets.Contains(2)));
        }

        public override void TearDownSteps()
        {
            AddStep("remove temporary audio files", () =>
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, true);
            });
            base.TearDownSteps();
        }
    }
}
