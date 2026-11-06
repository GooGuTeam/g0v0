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
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Localisation;
using osu.Framework.Testing;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterface;
using osu.Game.Localisation;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Osu;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.HitSounding;
using osu.Game.Storyboards;
using osu.Game.Tests.Resources;
using osuTK;
using osuTK.Input;

namespace osu.Game.Tests.Visual.Editing
{
    public partial class TestSceneHitSoundingSampleGallery : EditorTestScene
    {
        private string sampleDirectory = null!;
        private WorkingBeatmap? working;

        protected override bool UseFreshStoragePerRun => true;
        protected override bool IsolateSavingFromDatabase => false;
        protected override Ruleset CreateEditorRuleset() => new OsuRuleset();

        protected override WorkingBeatmap CreateWorkingBeatmap(IBeatmap beatmap, Storyboard? storyboard = null)
        {
            var manager = Dependencies.Get<BeatmapManager>();
            var imported = manager.Import(new ImportTask(TestResources.GetTestBeatmapStream(), "gallery.osz")).GetResultSafely()!;
            return working = manager.GetWorkingBeatmap(imported.Value.Beatmaps.First());
        }

        private HitSoundingSampleLibrary library => Editor.ChildrenOfType<HitSoundingSampleLibrary>().Single();

        public override void SetUpSteps()
        {
            base.SetUpSteps();
            AddStep("use design viewport", () => this.ChildrenOfType<ScalingContainer.ScalingDrawSizePreservingFillContainer>().First().TargetDrawSize = new Vector2(1920, 1080));
            AddStep("open hitsounding", () => Editor.Mode.Value = EditorScreenMode.HitSounding);
            AddUntilStep("gallery loaded", () => Editor.ChildrenOfType<HitSoundingSampleGallery>().Any(g => g.IsLoaded));
            AddStep("open Sample Gallery tab", () => Editor.ChildrenOfType<TabControl<LocalisableString>>().Single(t => t.Items.Contains(HitSoundingStrings.SampleGallery)).Current.Value = HitSoundingStrings.SampleGallery);
            AddStep("prepare distinct sample files", () =>
            {
                sampleDirectory = Path.Combine(Path.GetTempPath(), "g0v0-gallery-" + Guid.NewGuid());
                Directory.CreateDirectory(sampleDirectory);
                copySample("Samples/hitsound-no-delay.wav", "impact_bell_03.wav");
                copySample("Samples/test-sample.ogg", "air_whistle_01.ogg");
                copySample("Samples/hitsound-delay.wav", "wood_tap_07.wav");
                copySample("Samples/hitsound-minor-delay.wav", "clap_dry_02.wav");
            });
        }

        [Test]
        public void TestAssignmentCopiesAudioAndPersistsAliasesAndEmptySets()
        {
            AddStep("import four samples", () => library.ImportFiles(Directory.GetFiles(sampleDirectory)));
            AddUntilStep("original samples available", () => library.Files.Contains("impact_bell_03.wav") && library.Files.Contains("air_whistle_01.ogg"));
            AddStep("name original sample", () => library.SetAlias("impact_bell_03.wav", "Bright finish"));
            AddStep("assign a sample to Soft Finish", () => library.Assign(1, HitSampleInfo.BANK_SOFT, HitSampleInfo.HIT_FINISH, "impact_bell_03.wav"));
            AddUntilStep("slot resolves to original", () => library.GetAssignment(1, HitSampleInfo.BANK_SOFT, HitSampleInfo.HIT_FINISH) == "impact_bell_03.wav");
            AddAssert("canonical audio equals original bytes", () => resourceBytes("soft-hitfinish.wav").SequenceEqual(File.ReadAllBytes(Path.Combine(sampleDirectory, "impact_bell_03.wav"))));
            AddAssert("original filename and alias preserved", () => library.GetAlias("impact_bell_03.wav") == "Bright finish" && library.Files.Contains("impact_bell_03.wav"));
            AddStep("replace with another audio format", () => library.Assign(1, HitSampleInfo.BANK_SOFT, HitSampleInfo.HIT_FINISH, "air_whistle_01.ogg"));
            AddAssert("replacement removes competing extension", () => working!.BeatmapSetInfo.GetFile("soft-hitfinish.wav") == null && resourceBytes("soft-hitfinish.ogg").SequenceEqual(File.ReadAllBytes(Path.Combine(sampleDirectory, "air_whistle_01.ogg"))));
            AddStep("add empty set", () => library.AddSet());
            AddStep("reload library from saved resources", () =>
            {
                var restored = new HitSoundingSampleLibrary();
                var dependencies = new DependencyProvidingContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    CachedDependencies =
                    [
                        (typeof(EditorBeatmap), EditorBeatmap),
                        (typeof(IBindable<WorkingBeatmap>), Beatmap)
                    ],
                    Child = restored
                };
                Add(dependencies);
            });
            AddUntilStep("metadata restored", () => this.ChildrenOfType<HitSoundingSampleLibrary>().Any(l => l != library && l.IsLoaded && l.GetAlias("impact_bell_03.wav") == "Bright finish" && l.Sets.SequenceEqual(library.Sets) && l.GetAssignment(1, HitSampleInfo.BANK_SOFT, HitSampleInfo.HIT_FINISH) == "air_whistle_01.ogg"));
            AddStep("clear assignment", () => library.RemoveAssignment(1, HitSampleInfo.BANK_SOFT, HitSampleInfo.HIT_FINISH));
            AddAssert("clearing preserves original source", () => working!.BeatmapSetInfo.GetFile("soft-hitfinish.ogg") == null && resourceBytes("air_whistle_01.ogg").SequenceEqual(File.ReadAllBytes(Path.Combine(sampleDirectory, "air_whistle_01.ogg"))));
        }

        [Test]
        public void TestDraggingAndAliasSearch()
        {
            HitSoundingSampleGallery gallery = null!;
            HitSoundingSampleFileTile tile = null!;
            HitSoundingSampleSlot slot = null!;

            AddStep("import samples", () => library.ImportFiles(Directory.GetFiles(sampleDirectory)));
            AddUntilStep("tiles available", () => Editor.ChildrenOfType<HitSoundingSampleFileTile>().Any(t => t.Filename == "impact_bell_03.wav"));
            AddStep("filter original filename", () => Editor.ChildrenOfType<OsuTextBox>().Single(t => t.Name == "Sample file search input").Text = "impact_bell");
            AddUntilStep("only matching file remains", () => Editor.ChildrenOfType<HitSoundingSampleFileTile>().Select(t => t.Filename).SequenceEqual(new[] { "impact_bell_03.wav" }));
            AddStep("hold file tile", () =>
            {
                gallery = Editor.ChildrenOfType<HitSoundingSampleGallery>().Single();
                tile = gallery.FileLibrary.Tiles.Single();
                slot = gallery.Tree.Slots.Single(s => s.SetIndex == 1 && s.Bank == HitSampleInfo.BANK_NORMAL && s.Sample == HitSampleInfo.HIT_NORMAL);
                InputManager.MoveMouseTo(tile.ChildrenOfType<Drawable>().Single(d => d.Name == "Sample audition: impact_bell_03.wav"));
                InputManager.PressButton(MouseButton.Left);
            });
            AddStep("start dragging", () => InputManager.MoveMouseTo(InputManager.CurrentState.Mouse.Position + new Vector2(-30, 0)));
            AddStep("move over Normal slot", () => InputManager.MoveMouseTo(slot));
            AddUntilStep("slot highlighted", () => gallery.IsDraggingFile && slot.IsDropTarget);
            AddStep("drop file", () => InputManager.ReleaseButton(MouseButton.Left));
            AddUntilStep("drop assigns canonical audio", () => library.GetAssignment(1, HitSampleInfo.BANK_NORMAL, HitSampleInfo.HIT_NORMAL) == "impact_bell_03.wav");
            AddAssert("canonical copy matches source", () => resourceBytes("normal-hitnormal.wav").SequenceEqual(File.ReadAllBytes(Path.Combine(sampleDirectory, "impact_bell_03.wav"))));
            AddStep("double click alias", () =>
            {
                tile = gallery.FileLibrary.Tiles.Single();
                InputManager.MoveMouseTo(tile.ChildrenOfType<Drawable>().Single(d => d.Name == "Sample alias: impact_bell_03.wav"));
                InputManager.Click(MouseButton.Left);
                InputManager.Click(MouseButton.Left);
            });
            AddUntilStep("alias editing", () => tile.IsEditingAlias);
            AddStep("commit alias", () =>
            {
                tile.ChildrenOfType<OsuTextBox>().Single().Text = "Bright finish";
                InputManager.Key(Key.Enter);
            });
            AddUntilStep("alias persisted without renaming source", () => library.GetAlias("impact_bell_03.wav") == "Bright finish" && working!.BeatmapSetInfo.GetFile("impact_bell_03.wav") != null);
            AddStep("search alias", () => Editor.ChildrenOfType<OsuTextBox>().Single(t => t.Name == "Sample file search input").Text = "bright");
            AddUntilStep("alias finds original file", () => gallery.FileLibrary.Tiles.Select(t => t.Filename).SequenceEqual(new[] { "impact_bell_03.wav" }));
        }

        [TestCase(180)]
        [TestCase(260)]
        public void TestTreeScrollAxesInShortGallery(float height)
        {
            HitSoundingSampleGallery gallery = null!;
            HitSoundingSampleSetColumn first = null!;
            Vector2 initial = default;
            Vector2 verticallyScrolled = default;

            AddStep("shorten gallery and add overflowing sets", () =>
            {
                gallery = Editor.ChildrenOfType<HitSoundingSampleGallery>().Single();
                gallery.RelativeSizeAxes = Axes.X;
                gallery.Height = height;
                for (int i = 0; i < 5; i++)
                    library.AddSet();
            });
            AddUntilStep("set columns ready", () => gallery.Tree.Columns.Count >= 6);
            AddStep("scroll down without modifier", () =>
            {
                var viewport = gallery.Tree.ChildrenOfType<Drawable>().Single(d => d.Name == "Sample sets tree");
                first = gallery.Tree.Columns[0];
                initial = first.ScreenSpaceDrawQuad.TopLeft;
                InputManager.MoveMouseTo(viewport);
                InputManager.ScrollVerticalBy(-10);
            });
            AddUntilStep("vertical wheel leaves horizontal position intact", () =>
                first.ScreenSpaceDrawQuad.TopLeft.Y < initial.Y
                && Math.Abs(first.ScreenSpaceDrawQuad.TopLeft.X - initial.X) < 1);
            AddAssert("bottom Drum slot is reachable", () =>
            {
                var slot = first.Slots.Last();
                return gallery.Tree.GetSlotAt(slot.ScreenSpaceDrawQuad.Centre) == slot;
            });
            AddStep("scroll horizontally with shift", () =>
            {
                verticallyScrolled = first.ScreenSpaceDrawQuad.TopLeft;
                InputManager.PressKey(Key.ShiftLeft);
                InputManager.ScrollVerticalBy(-3);
                InputManager.ReleaseKey(Key.ShiftLeft);
            });
            AddUntilStep("shift wheel leaves vertical position intact", () =>
                first.ScreenSpaceDrawQuad.TopLeft.X < verticallyScrolled.X
                && Math.Abs(first.ScreenSpaceDrawQuad.TopLeft.Y - verticallyScrolled.Y) < 1);
            AddStep("scroll back horizontally with right shift", () =>
            {
                InputManager.PressKey(Key.ShiftRight);
                InputManager.ScrollVerticalBy(20);
                InputManager.ReleaseKey(Key.ShiftRight);
            });
            AddUntilStep("first set returns without losing vertical scroll", () =>
                Math.Abs(first.ScreenSpaceDrawQuad.TopLeft.X - initial.X) < 1
                && Math.Abs(first.ScreenSpaceDrawQuad.TopLeft.Y - verticallyScrolled.Y) < 1);
        }

        private void copySample(string source, string target)
        {
            using var input = TestResources.OpenResource(source);
            using var output = File.Create(Path.Combine(sampleDirectory, target));
            input.CopyTo(output);
        }

        private byte[] resourceBytes(string filename)
        {
            using var input = working!.GetStream(working.BeatmapSetInfo.GetPathForFile(filename));
            using var output = new MemoryStream();
            input.CopyTo(output);
            return output.ToArray();
        }

        public override void TearDownSteps()
        {
            AddStep("remove temporary audio files", () =>
            {
                if (Directory.Exists(sampleDirectory))
                    Directory.Delete(sampleDirectory, true);
            });
            base.TearDownSteps();
        }
    }
}
