// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Collections;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Game.Online;
using osu.Game.Rulesets;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Screens;
using osu.Game.Screens.Footer;
using osu.Game.Screens.Select;
using osu.Game.Screens.Select.Filter;
using osu.Game.Skinning;
using osu.Game.Skinning.Components;
using osu.Game.Skinning.Select;
using osu.Game.Tests.Resources;
using osu.Game.Users;
using osuTK;
using osuTK.Input;

namespace osu.Game.Tests.Visual.SongSelect
{
    public partial class TestSceneLegacySongSelect : SongSelectTestScene
    {
        [Cached(typeof(LocalUserStatisticsProvider))]
        private readonly TestUserStatisticsProvider statisticsProvider = new TestUserStatisticsProvider();

        private ScreenFrontLayer frontLayer => ScreenFrontLayer;

        [Resolved]
        private SkinManager skins { get; set; } = null!;

        private void loadLegacySongSelect()
        {
            AddStep("load user statistics", () =>
            {
                if (statisticsProvider.Parent == null)
                    Add(statisticsProvider);
            });
            AddStep("enable legacy song select", () => Config.SetValue(OsuSetting.ToriiLegacyFooterUseSkin, true));
            ImportBeatmapForRuleset(0);
            LoadSongSelect();
            AddUntilStep("a difficulty is selected", () => Carousel.CurrentBeatmap != null);
        }

        [Test]
        public void TestLegacyHudAboveFooter()
        {
            loadLegacySongSelect();
            var hud = new BoxElement
            {
                Anchor = Anchor.BottomLeft,
                Origin = Anchor.BottomLeft,
                Position = new Vector2(260, -25),
                Size = new Vector2(260, 36),
                AccentColour = { Value = Colour4.FromHex("#FF33CC") },
            };

            AddUntilStep("song select HUD loaded", () => SongSelect.ChildrenOfType<SkinnableContainer>().Single().ComponentsLoaded);
            AddStep("place a HUD component over the footer", () => SongSelect.ChildrenOfType<SkinnableContainer>().Single().Add(hud));
            AddUntilStep("HUD drawn above all legacy chrome", () => frontLayer.Children.Count > 2 && frontLayer.Children[^1].Depth == ScreenFrontLayer.Depths.HUD);
            AddAssert("logo stays below the HUD", () => frontLayer.HoldLogoBelow.Value);
            AddStep("disable legacy", () => Config.SetValue(OsuSetting.ToriiLegacyFooterUseSkin, false));
            AddUntilStep("HUD back in the screen", () => frontLayer.Children.All(c => c.Depth != ScreenFrontLayer.Depths.HUD));
        }

        [Test]
        public void TestLegacyCollectionDialog()
        {
            loadLegacySongSelect();
            AddStep("add collections", () => Realm.Write(r =>
            {
                r.Add(new BeatmapCollection("Favourites", new List<string> { Carousel.CurrentBeatmap!.MD5Hash }));
                r.Add(new BeatmapCollection("Tournament pool"));
            }));
            AddStep("open collections", () => SongSelect.ManageCollections());
            AddUntilStep("dialog shown", () => this.ChildrenOfType<LegacyCollectionDialog>().Single().Alpha == 1);
        }

        [Test]
        public void TestLegacyHoldRevealsBackground()
        {
            loadLegacySongSelect();
            AddWaitStep("let panels settle", 5);
            AddStep("hold on empty space", () =>
            {
                var quad = SongSelect.ScreenSpaceDrawQuad;
                InputManager.MoveMouseTo(quad.TopLeft + new Vector2(quad.Width * 0.45f, quad.Height * 0.5f));
                InputManager.PressButton(MouseButton.Left);
            });
            AddUntilStep("legacy chrome hidden", () => SongSelect.ChildrenOfType<LegacySongSelectTop>().Single().FindClosestParent<DrawSizePreservingFillContainer>()!.Alpha == 0);
            AddStep("release", () => InputManager.ReleaseButton(MouseButton.Left));
            AddUntilStep("legacy chrome restored", () => SongSelect.ChildrenOfType<LegacySongSelectTop>().Single().FindClosestParent<DrawSizePreservingFillContainer>()!.Alpha == 1);
        }

        [Test]
        public void TestLegacyLayout()
        {
            AddStep("load user statistics", () =>
            {
                if (statisticsProvider.Parent == null)
                    Add(statisticsProvider);
            });
            AddStep("enable legacy song select", () => Config.SetValue(OsuSetting.ToriiLegacyFooterUseSkin, true));
            ImportBeatmapForRuleset(0);
            LoadSongSelect();
            AddAssert("legacy top loaded", () => SongSelect.ChildrenOfType<LegacySongSelectTop>().Single().IsLoaded);
            AddAssert("global footer owns legacy presentation", () => SongSelect.FooterContent.Value.Mode == ScreenFooterContent.FooterMode.Argon && SongSelect.AllowLegacyFooterSkinning);
            AddUntilStep("modern filter hidden", () => !SongSelect.ChildrenOfType<FilterControl>().Single().IsPresent);
            AddStep("disable legacy live", () => Config.SetValue(OsuSetting.ToriiLegacyFooterUseSkin, false));
            AddUntilStep("modern filter restored", () => SongSelect.ChildrenOfType<FilterControl>().Single().Alpha == 1);
            AddStep("enable legacy live", () => Config.SetValue(OsuSetting.ToriiLegacyFooterUseSkin, true));
            AddUntilStep("legacy top rebuilt", () => SongSelect.ChildrenOfType<LegacySongSelectTop>().Single().IsLoaded);
            AddUntilStep("modern filter hidden again", () => !SongSelect.ChildrenOfType<FilterControl>().Single().IsPresent);
        }

        /// <summary>
        /// Opens the beatmap options with the legacy skin folder named by <c>G0V0_PREVIEW_SKIN</c>, whose dialog buttons use its button textures.
        /// </summary>
        [Test]
        public void TestLegacyOptionsWithSkin()
        {
            string? directory = Environment.GetEnvironmentVariable(@"G0V0_PREVIEW_SKIN");

            if (string.IsNullOrEmpty(directory))
                Assert.Ignore(@"Set G0V0_PREVIEW_SKIN to a legacy skin folder to run this test.");

            AddStep("switch skin", () => skins.CurrentSkinInfo.Value = skins.Import(new ImportTask(directory)).GetResultSafely()!);
            loadLegacySongSelect();
            AddStep("open options", () => SongSelect.ToggleLegacyBeatmapOptions());
            AddWaitStep("let it appear", 10);
        }

        /// <summary>
        /// Shows real beatmaps' panels with the legacy skin folder named by <c>G0V0_PREVIEW_SKIN</c>: its stars and song select text colours.
        /// </summary>
        [Test]
        public void TestLegacyPanelsWithSkin()
        {
            string? directory = Environment.GetEnvironmentVariable(@"G0V0_PREVIEW_SKIN");

            if (string.IsNullOrEmpty(directory))
                Assert.Ignore(@"Set G0V0_PREVIEW_SKIN to a legacy skin folder to run this test.");

            AddStep("switch skin", () => skins.CurrentSkinInfo.Value = skins.Import(new ImportTask(directory)).GetResultSafely()!);
            BeatmapSetInfo? imported = null;
            AddStep("import real beatmap", () => imported = Beatmaps.Import(new ImportTask(TestResources.GetTestBeatmapForImport())).GetResultSafely()?.Value);
            loadLegacySongSelect();
            AddStep("select it", () => Beatmap.Value = Beatmaps.GetWorkingBeatmap(imported!.Beatmaps.OrderBy(b => b.StarRating).Last()));
            AddStep("fill user statistics", () => statisticsProvider.SetStatistics(new UserStatistics
            {
                PP = 4321,
                Accuracy = 98.76,
                GlobalRank = 12345,
                Level = new UserStatistics.LevelInfo { Current = 100, Progress = 60 },
            }, Ruleset.Value));
            AddWaitStep("let stars settle", 20);
        }

        /// <summary>
        /// Opens the mode menu with the legacy skin folder named by <c>G0V0_PREVIEW_SKIN</c>, whose mode icons may be blank placeholders.
        /// </summary>
        [TestCase(-1)]
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void TestLegacyModeMenuWithSkin(int hoveredRow)
        {
            string? directory = Environment.GetEnvironmentVariable(@"G0V0_PREVIEW_SKIN");

            if (string.IsNullOrEmpty(directory))
                Assert.Ignore(@"Set G0V0_PREVIEW_SKIN to a legacy skin folder to run this test.");

            AddStep("switch skin", () => skins.CurrentSkinInfo.Value = skins.Import(new ImportTask(directory)).GetResultSafely()!);
            loadLegacySongSelect();
            AddUntilStep("mode button loaded", () => this.ChildrenOfType<LegacyRulesetFooterButton>().Any(b => b.IsLoaded));
            AddStep("open mode menu", () => this.ChildrenOfType<LegacyRulesetFooterButton>().First(b => b.IsLoaded).TriggerClick());
            AddWaitStep("let it appear", 10);
            AddStep("hover row", () =>
            {
                if (hoveredRow >= 0)
                    InputManager.MoveMouseTo(this.ChildrenOfType<LegacyModeMenu>().Single().ChildrenOfType<ClickableContainer>().ElementAt(hoveredRow));
            });
            AddWaitStep("let it highlight", 10);
        }

        /// <summary>
        /// Hovers the footer's buttons with the legacy skin folder named by <c>G0V0_PREVIEW_SKIN</c>, showing their <c>-over</c> glyphs.
        /// </summary>
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void TestLegacyFooterHoverWithSkin(int button)
        {
            string? directory = Environment.GetEnvironmentVariable(@"G0V0_PREVIEW_SKIN");

            if (string.IsNullOrEmpty(directory))
                Assert.Ignore(@"Set G0V0_PREVIEW_SKIN to a legacy skin folder to run this test.");

            AddStep("switch skin", () => skins.CurrentSkinInfo.Value = skins.Import(new ImportTask(directory)).GetResultSafely()!);
            loadLegacySongSelect();
            AddUntilStep("buttons loaded", () => this.ChildrenOfType<LegacyFooterButton>().Count(b => b.IsLoaded) >= 4);
            AddStep("hover button", () => InputManager.MoveMouseTo(this.ChildrenOfType<LegacyFooterButton>().Where(b => b.IsLoaded).ElementAt(button)));
            AddWaitStep("let it fade in", 10);
        }

        /// <summary>
        /// Stable's keyboard traversal: Down onto a difficulty of the selected set selects it, Down onto another set only key-hovers it, and
        /// activating that opens the set.
        /// </summary>
        [Test]
        public void TestLegacyKeyboardTraversal()
        {
            ImportBeatmapForRuleset(0);
            loadLegacySongSelect();

            BeatmapInfo? first = null;
            AddStep("select first difficulty", () =>
            {
                first = Carousel.CurrentBeatmap!.BeatmapSet!.Beatmaps.OrderBy(b => b.StarRating).First();
                Beatmap.Value = Beatmaps.GetWorkingBeatmap(first);
            });
            AddUntilStep("first selected", () => Carousel.CurrentBeatmap?.Equals(first) == true);

            AddStep("press down", () => InputManager.Key(Key.Down));
            AddUntilStep("next difficulty selected directly", () => Carousel.CurrentBeatmap?.BeatmapSet?.Equals(first!.BeatmapSet) == true
                                                                  && !Carousel.CurrentBeatmap.Equals(first));

            BeatmapInfo? lastOfSet = null;
            AddStep("select last difficulty", () =>
            {
                lastOfSet = first!.BeatmapSet!.Beatmaps.OrderBy(b => b.StarRating).Last();
                Beatmap.Value = Beatmaps.GetWorkingBeatmap(lastOfSet);
            });
            AddUntilStep("last selected", () => Carousel.CurrentBeatmap?.Equals(lastOfSet) == true);

            AddStep("press down", () => InputManager.Key(Key.Down));
            AddWaitStep("wait", 5);
            AddAssert("another set only key-hovered", () => Carousel.CurrentBeatmap?.Equals(lastOfSet) == true);

            AddStep("press enter", () => InputManager.Key(Key.Enter));
            AddUntilStep("other set opened", () => Carousel.CurrentBeatmap?.BeatmapSet?.Equals(first!.BeatmapSet) == false);
        }

        [Test]
        public void TestLegacyHoverMovesPanels()
        {
            AddStep("load user statistics", () =>
            {
                if (statisticsProvider.Parent == null)
                    Add(statisticsProvider);
            });
            AddStep("enable legacy song select", () => Config.SetValue(OsuSetting.ToriiLegacyFooterUseSkin, true));
            AddStep("no grouping", () => Config.SetValue(OsuSetting.SongSelectGroupMode, GroupMode.None));
            SortBy(SortMode.Title);

            for (int i = 0; i < 4; i++)
                ImportBeatmapForRuleset(0);

            LoadSongSelect();
            AddUntilStep("a difficulty is selected", () => Carousel.CurrentBeatmap != null);
            AddWaitStep("let panels settle", 10);

            Panel hovered = null!;
            float restingX = 0;

            AddStep("hover a collapsed set", () =>
            {
                float middle = Carousel.ScreenSpaceDrawQuad.Centre.Y;
                hovered = Carousel.ChildrenOfType<PanelBeatmapStandalone>().Where(p => !p.Expanded.Value && !p.Selected.Value)
                                  .OrderBy(p => Math.Abs(p.ScreenSpaceDrawQuad.Centre.Y - middle)).First();
                restingX = hovered.X;
                InputManager.MoveMouseTo(hovered.ScreenSpaceDrawQuad.Centre + new Vector2(hovered.ScreenSpaceDrawQuad.Width * 0.3f, 0));
            });
            AddUntilStep("hovered panel moved left", () => hovered.X < restingX - 50);
        }

        [Test]
        public void TestLegacySetsCollapse()
        {
            AddStep("load user statistics", () =>
            {
                if (statisticsProvider.Parent == null)
                    Add(statisticsProvider);
            });
            AddStep("enable legacy song select", () => Config.SetValue(OsuSetting.ToriiLegacyFooterUseSkin, true));
            AddStep("no grouping", () => Config.SetValue(OsuSetting.SongSelectGroupMode, GroupMode.None));
            SortBy(SortMode.Title);

            for (int i = 0; i < 4; i++)
                ImportBeatmapForRuleset(0);

            LoadSongSelect();
            AddUntilStep("a difficulty is selected", () => Carousel.CurrentBeatmap != null);
            AddWaitStep("let panels settle", 10);

            AddAssert("sets collapse", () => Carousel.LegacySetsCollapsed);
            AddAssert("only the selected set shows every difficulty", () =>
            {
                var visible = Carousel.GetCarouselItems()!.Where(i => i.IsVisible).Select(i => ((GroupedBeatmap)i.Model).Beatmap).ToArray();
                var selectedSet = Carousel.CurrentBeatmap!.BeatmapSet!;

                return visible.Count(b => b.BeatmapSet!.Equals(selectedSet)) == selectedSet.Beatmaps.Count
                       && visible.Where(b => !b.BeatmapSet!.Equals(selectedSet)).GroupBy(b => b.BeatmapSet!.ID).All(g => g.Count() == 1);
            });
        }

        private partial class TestUserStatisticsProvider : LocalUserStatisticsProvider
        {
            public void SetStatistics(UserStatistics statistics, RulesetInfo ruleset) => UpdateStatistics(statistics, ruleset);
        }
    }
}
