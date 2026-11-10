// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Imaging;
using osu.Framework.Platform;
using osu.Framework.Testing;
using osu.Game.Rulesets;
using osu.Framework.Extensions.IEnumerableExtensions;
using osu.Game.Rulesets.Catch;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Taiko;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.UI;
using osu.Game.Screens.Edit.Compose.Components;
// using osu.Game.Screens.Edit.Mapping;
using osu.Game.Rulesets.Edit;
using osu.Game.Screens.Edit;
using osu.Game.Graphics.Containers;
using osu.Game.Tests.Visual;
using osuTK;
using osuTK.Input;

namespace Previewer
{
    public partial class EditorPreviewScene : EditorTestScene
    {
        private readonly PreviewOptions options;

        [Resolved]
        private GameHost host { get; set; } = null!;

        private Drawable? takeControlOverlay;

        public EditorPreviewScene(PreviewOptions options)
        {
            this.options = options;
        }

        protected override Ruleset CreateEditorRuleset() => options.Ruleset switch
        {
            "taiko" => new TaikoRuleset(),
            "catch" => new CatchRuleset(),
            "mania" => new ManiaRuleset(),
            _ => new OsuRuleset(),
        };

        protected override void Update()
        {
            base.Update();

            // The manual input overlay would cover the bottom bar.
            takeControlOverlay ??= (Drawable?)typeof(OsuManualInputManagerTestScene).GetField("takeControlOverlay", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(this);
            takeControlOverlay?.Hide();
        }

        public void AddScripts()
        {
            AddStep("park mouse", () => InputManager.MoveMouseTo(InputManager.ScreenSpaceDrawQuad.BottomRight + new Vector2(40)));
            AddStep("seek", () => EditorClock.Seek(13000));

            foreach (string script in options.Scripts)
            {
                switch (script)
                {
                    case "compose":
                        Capture("compose");
                        CaptureEach("ExpandingToolboxContainer", d => d.ScreenSpaceDrawQuad.Centre.X < InputManager.ScreenSpaceDrawQuad.Centre.X ? "compose-toolbox-left" : "compose-toolbox-right");
                        CaptureOf("compose-bottom-bar", "BottomBar");
                        CaptureOf("compose-timeline", "TimelineArea");
                        CaptureOf("compose-menu-bar", "EditorMenuBar");
                        break;

                    case "toolbox-hover":
                        // Expanded toolboxes, as when hovered.
                        AddStep("hover right toolbox", () => InputManager.MoveMouseTo(rightToolbox()));
                        Capture("toolbox-hover", wait: 6);
                        AddStep("hover left toolbox", () => InputManager.MoveMouseTo(leftToolbox()));
                        Capture("toolbox-hover-left", wait: 6);
                        AddStep("park mouse", () => InputManager.MoveMouseTo(InputManager.ScreenSpaceDrawQuad.BottomRight + new Vector2(40)));
                        break;

                    case "context-menu":
                        AddStep("select object at cursor time", () =>
                        {
                            var target = EditorBeatmap.HitObjects.OrderBy(h => Math.Abs(h.StartTime - EditorClock.CurrentTime)).First();
                            EditorBeatmap.SelectedHitObjects.Clear();
                            EditorBeatmap.SelectedHitObjects.Add(target);
                        });
                        AddStep("right click selection", () =>
                        {
                            var blueprint = Editor.ChildrenOfType<SelectionBlueprint<HitObject>>().First(b => b.IsSelected);
                            InputManager.MoveMouseTo(blueprint.ScreenSpaceSelectionPoint);
                            InputManager.Click(MouseButton.Right);
                        });
                        Capture("context-menu");
                        AddStep("hover AI item", () =>
                        {
                            var item = this.ChildrenOfType<Menu.DrawableMenuItem>().LastOrDefault(i => i.IsPresent);

                            if (item != null)
                                InputManager.MoveMouseTo(item);
                        });
                        Capture("context-menu-ai", wait: 4);
                        AddStep("close menu", () =>
                        {
                            InputManager.MoveMouseTo(Editor.ChildrenOfType<Playfield>().First().ScreenSpaceDrawQuad.BottomLeft + new Vector2(60, -60));
                            InputManager.Click(MouseButton.Left);
                            EditorBeatmap.SelectedHitObjects.Clear();
                        });
                        parkMouse();
                        break;

#if false
                    case "ai-settings":
                        AddStep("open AI settings", () => clickType("MapperatorSettingsButton"));
                        Capture("ai-settings", wait: 5);
                        CapturePopover("ai-settings-popover");
                        AddStep("close popover", () => this.ChildrenOfType<Popover>().ForEach(p => p.Hide()));
                        parkMouse();
                        break;

                    case "generation":
                        AddStep("enter generation", () => clickType("MapperatorProgressButton"));
                        parkMouse();
                        Capture("generation", wait: 5);
                        CaptureEach("ExpandingToolboxContainer", d => d.ScreenSpaceDrawQuad.Centre.X < InputManager.ScreenSpaceDrawQuad.Centre.X ? "generation-toolbox-left" : "generation-toolbox-right", wait: 0);
                        CaptureOf("generation-timeline", "TimelineArea", wait: 0);
                        CaptureOf("generation-summary", "SummaryTimeline", wait: 0);
                        AddStep("leave generation", () => candidateSet().GenerationMode.Value = false);
                        break;

                    case "gen-mode":
                        AddStep("generation mode", () => candidateSet().GenerationMode.Value = true);
                        CaptureEach("ExpandingToolboxContainer", d => d.ScreenSpaceDrawQuad.Centre.X < InputManager.ScreenSpaceDrawQuad.Centre.X ? "gen-mode-left" : "gen-mode-right");
                        AddStep("hover right toolbox", () => InputManager.MoveMouseTo(rightToolbox().ScreenSpaceDrawQuad.TopLeft + new Vector2(60, 120)));
                        CaptureEach("ExpandingToolboxContainer", d => d.ScreenSpaceDrawQuad.Centre.X < InputManager.ScreenSpaceDrawQuad.Centre.X ? "gen-mode-left-hover" : "gen-mode-right-hover", wait: 8);
                        AddStep("leave generation", () => candidateSet().GenerationMode.Value = false);
                        parkMouse();
                        break;
#endif

                    case "bar-popover":
                        AddStep("click bar label", () => clickType("BarPropertiesButton"));
                        parkMouse();
                        Capture("bar-popover", wait: 5);
                        CapturePopover("bar-popover-crop");
                        AddStep("close popover", () => this.ChildrenOfType<Popover>().ForEach(p => p.Hide()));
                        break;

                    case "timestamp-input":
                        AddStep("click timestamp", () => clickType("TimestampControl"));
                        parkMouse();
                        CaptureOf("timestamp-input", "BottomBar", wait: 3);
                        AddStep("unfocus", () => InputManager.Key(Key.Enter));
                        break;

#if false
                    case "timing-signature":
                        AddStep("timing screen", () => Editor.Mode.Value = EditorScreenMode.Timing);
                        waitForScreen("ControlPointList");
                        AddStep("scroll to signature", () =>
                        {
                            var signature = findAll("ControlPointSettings").First().ChildrenOfType<osu.Game.Screens.Edit.Timing.LabelledTimeSignature>().First();
                            signature.FindClosestParent<osu.Framework.Graphics.Containers.ScrollContainer<Drawable>>()!.ScrollTo(signature, false);
                        });
                        CaptureOf("timing-signature", "ControlPointSettings", wait: 4);
                        AddStep("7/8 with accents", () =>
                        {
                            var point = EditorBeatmap.ControlPointInfo.TimingPoints[0];
                            point.TimeSignature = new osu.Game.Beatmaps.Timing.TimeSignature(7, 8);
                            point.EditorBeatDivisor = 4;
                            point.EditorAccents = "3021";
                        });
                        AddStep("scroll to signature again", () =>
                        {
                            var signature = findAll("ControlPointSettings").First().ChildrenOfType<osu.Game.Screens.Edit.Timing.LabelledTimeSignature>().First();
                            signature.FindClosestParent<osu.Framework.Graphics.Containers.ScrollContainer<Drawable>>()!.ScrollTo(signature, false);
                        });
                        CaptureOf("timing-signature-78", "ControlPointSettings", wait: 4);
                        AddStep("open grouping dropdown", () =>
                        {
                            var dropdown = findAll("ControlPointSettings").First().ChildrenOfType<Drawable>().First(d => d.GetType().Name == "GroupingDropdown");
                            InputManager.MoveMouseTo(dropdown.ChildrenOfType<Drawable>().First(d => d.GetType().Name == "FormDropdownHeader"));
                            InputManager.Click(MouseButton.Left);
                        });
                        CaptureOf("timing-signature-grouping", "ControlPointSettings", wait: 4);
                        AddStep("close dropdown", () => InputManager.Key(Key.Escape));
                        break;
#endif

                    case "timing":
                        AddStep("timing screen", () => Editor.Mode.Value = EditorScreenMode.Timing);
                        waitForScreen("ControlPointList");
                        Capture("timing");
                        CaptureOf("timing-control-points", "ControlPointList", wait: 0);
                        CaptureOf("timing-settings", "ControlPointSettings", wait: 0);
                        break;

                    case "verify":
                        AddStep("verify screen", () => Editor.Mode.Value = EditorScreenMode.Verify);
                        waitForScreen("IssueList");
                        AddWaitStep("wait for results", 10);
                        Capture("verify");
                        AddStep("show every severity", () => Editor.ChildrenOfType<osu.Game.Screens.Edit.Verify.VerifyScreen>().First().HiddenIssueTypes.Clear());
                        Capture("verify-all");
                        AddStep("select first issue", () =>
                        {
                            var row = findAll("DrawableIssue").OrderBy(d => d.ScreenSpaceDrawQuad.TopLeft.Y).FirstOrDefault();

                            if (row != null)
                            {
                                InputManager.MoveMouseTo(row);
                                InputManager.Click(MouseButton.Left);
                            }
                        });
                        parkMouse();
                        Capture("verify-selected", wait: 4);
                        CaptureOf("verify-list-selected", "IssueList", wait: 0);
                        break;

#if false
                    case "ticks":
                        AddStep("timing screen", () => Editor.Mode.Value = EditorScreenMode.Timing);
                        waitForScreen("ControlPointList");

                        foreach (var (name, numerator, denominator, groups, divisor, globalDivisor) in new (string, int, int, int[], int, int)[]
                                 {
                                     ("ticks-4-4-d4", 4, 4, Array.Empty<int>(), 4, 4),
                                     ("ticks-7-8-223-d4", 7, 8, new[] { 2, 2, 3 }, 4, 4),
                                     ("ticks-7-8-auto-d2", 7, 8, Array.Empty<int>(), 2, 4),
                                     ("ticks-6-8-auto-d2", 6, 8, Array.Empty<int>(), 2, 4),
                                     ("ticks-5-4-d5", 5, 4, Array.Empty<int>(), 5, 4),
                                     ("ticks-4-4-d10", 4, 4, Array.Empty<int>(), 10, 4),
                                     ("ticks-4-4-d12", 4, 4, Array.Empty<int>(), 12, 4),
                                     ("ticks-4-4-global7", 4, 4, Array.Empty<int>(), 0, 7),
                                     ("ticks-6-8-33-d3", 6, 8, new[] { 3, 3 }, 3, 4),
                                 })
                        {
                            AddStep($"set {name}", () =>
                            {
                                var point = EditorBeatmap.ControlPointInfo.TimingPoints[0];
                                point.TimeSignature = new osu.Game.Beatmaps.Timing.TimeSignature(numerator, denominator, groups);
                                point.EditorBeatDivisor = divisor;
                                ((BindableBeatDivisor)typeof(Editor).GetField("beatDivisor", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(Editor)!).SetArbitraryDivisor(globalDivisor);
                            });
                            CaptureOf(name, "TimelineArea", wait: 2);
                        }

                        break;
#endif

#if false
                    case "design-populated":
                        AddStep("storyboard preview fixture", () =>
                        {
                            for (int i = 0; i < 6; i++)
                            {
                                var sprite = new osu.Game.Storyboards.StoryboardSprite(osu.Game.Storyboards.StoryboardElementSource.Shared,
                                    $"sb/scene-{i + 1}.jpg", Anchor.Centre, new Vector2(320, 240));
                                var image = System.IO.Path.GetFullPath("../../../../../../g0v0-resources/osu.Game.Resources/Textures/Menu/menu-background-1.jpg");
                                EditorBeatmap.Storyboard.PendingResources[sprite.Path] = System.IO.File.ReadAllBytes(image);
                                sprite.InitialPosition = new Vector2(160 + i * 60, 160 + i * 20);
                                sprite.Commands.AddAlpha(Easing.None, 0, 45000, 1, 1);
                                sprite.Commands.AddScale(Easing.OutQuad, 0, 45000, 0.1f, 0.15f);
                                EditorBeatmap.Storyboard.GetLayer(i < 3 ? "Background" : "Foreground").Add(sprite);
                            }
                            EditorBeatmap.NotifyStoryboardChanged();
                        });
                        AddStep("design screen", () => Editor.Mode.Value = EditorScreenMode.Design);
                        waitForScreen("StoryboardWorkspace");
                        AddUntilStep("wait for storyboard images", () => this.ChildrenOfType<osu.Game.Storyboards.Drawables.DrawableStoryboardSprite>()
                            .Count(d => d.IsLoaded && d.Texture != null && d.IsPresent) >= 6);
                        AddStep("select storyboard element", () => this.ChildrenOfType<osu.Game.Screens.Edit.Design.StoryboardWorkspace>().Single().Session.Select(EditorBeatmap.Storyboard.GetLayer("Background").Elements.First()));
                        Capture("design-populated");
                        AddStep("open storyboard import options", () => this.ChildrenOfType<osu.Game.Screens.Edit.Design.StoryboardPopoverButton>().First(b => b.Text.Equals(osu.Game.Localisation.StoryboardEditorStrings.ImportImage)).TriggerClick());
                        Capture("design-import-options");
                        AddStep("close storyboard import options", () => { InputManager.PressKey(Key.Escape); InputManager.ReleaseKey(Key.Escape); });
                        AddStep("maximise composition", () => this.ChildrenOfType<osu.Game.Graphics.UserInterface.IconButton>().First(b => b.TooltipText.Equals(osu.Game.Localisation.StoryboardEditorStrings.FocusCanvas)).TriggerClick());
                        Capture("design-composition-focus");
                        AddStep("restore storyboard panes", () => this.ChildrenOfType<osu.Game.Graphics.UserInterface.IconButton>().First(b => b.TooltipText.Equals(osu.Game.Localisation.StoryboardEditorStrings.FocusCanvas)).TriggerClick());
                        AddStep("record storyboard fixture", () => EditorBeatmap.SaveState());
                        AddStep("edit transform position", () =>
                        {
                            this.ChildrenOfType<osu.Game.Screens.Edit.Design.StoryboardPropertiesPanel>().Single().ChildrenOfType<osu.Game.Graphics.UserInterfaceV2.FormNumberBox>().First(n => n.Caption.Equals((osu.Framework.Localisation.LocalisableString)"X")).Current.Value = "180";
                            this.ChildrenOfType<osu.Game.Screens.Edit.Design.StoryboardButton>().First(b => b.Text.Equals(osu.Game.Localisation.StoryboardEditorStrings.AddKeyframe)).TriggerClick();
                        });
                        Capture("design-transform-keyframe");
                        AddStep("undo transform keyframe", () => Editor.Undo());
                        Capture("design-transform-undo");
                        AddUntilStep("wait for restored composition", () =>
                        {
                            var workspace = this.ChildrenOfType<osu.Game.Screens.Edit.Design.StoryboardWorkspace>().Single();
                            return this.ChildrenOfType<osu.Game.Storyboards.Drawables.DrawableStoryboardSprite>().Any(d => ReferenceEquals(d.Sprite, workspace.Session.Selected));
                        });
                        AddStep("press storyboard image", () =>
                        {
                            var workspace = this.ChildrenOfType<osu.Game.Screens.Edit.Design.StoryboardWorkspace>().Single();
                            var drawable = this.ChildrenOfType<osu.Game.Storyboards.Drawables.DrawableStoryboardSprite>().First(d => ReferenceEquals(d.Sprite, workspace.Session.Selected));
                            InputManager.MoveMouseTo(drawable.ScreenSpaceDrawQuad.TopLeft + new Vector2(15));
                            InputManager.PressButton(MouseButton.Left);
                        });
                        AddStep("drag storyboard image", () => InputManager.MoveMouseTo(InputManager.CurrentState.Mouse.Position + new Vector2(50, 25)));
                        AddStep("release storyboard image", () => InputManager.ReleaseButton(MouseButton.Left));
                        Capture("design-dragged");
                        AddStep("undo storyboard drag", () => Editor.Undo());
                        Capture("design-drag-undo");
                        AddStep("select keyframe", () =>
                        {
                            EditorBeatmap.SaveState();
                            var workspace = this.ChildrenOfType<osu.Game.Screens.Edit.Design.StoryboardWorkspace>().Single();
                            var sprite = (osu.Game.Storyboards.StoryboardSprite)workspace.Session.Selected!;
                            workspace.Session.SelectCommand(sprite.Commands, sprite.Commands.Scale.First(), false);
                        });
                        Capture("design-keyframes");
                        AddStep("select multiple keyframes", () =>
                        {
                            var workspace = this.ChildrenOfType<osu.Game.Screens.Edit.Design.StoryboardWorkspace>().Single();
                            var sprite = (osu.Game.Storyboards.StoryboardSprite)workspace.Session.Selected!;
                            workspace.Session.SelectCommand(sprite.Commands, sprite.Commands.Alpha.First(), true);
                        });
                        Capture("design-keyframes-multiple");
                        AddStep("move selected keyframes", () => this.ChildrenOfType<osu.Game.Screens.Edit.Design.StoryboardWorkspace>().Single().Session.MoveCommands(1000));
                        Capture("design-keyframes-moved");
                        AddStep("undo storyboard edit", () => Editor.Undo());
                        Capture("design-keyframes-undo");
                        AddStep("redo storyboard edit", () => Editor.Redo());
                        Capture("design-keyframes-redo");
                        break;
#endif

                    case "design":
                        AddStep("design screen", () => Editor.Mode.Value = EditorScreenMode.Design);
                        waitForScreen("DesignScreen");
                        Capture("design");
                        break;

                    case "setup":
                        AddStep("setup screen", () => Editor.Mode.Value = EditorScreenMode.SongSetup);
                        waitForScreen("SetupScreen");
                        Capture("setup");
                        CaptureOf("setup-metadata", "MetadataSection", wait: 0);
                        CaptureOf("setup-difficulty", "DifficultySection", wait: 0);
                        break;

                    case "compose-screen":
                        AddStep("compose screen", () => Editor.Mode.Value = EditorScreenMode.Compose);
                        AddWaitStep("settle", 6);
                        break;

                    default:
                        throw new ArgumentException($"Unknown script {script}");
                }
            }
        }

        private void waitForScreen(string typeName)
        {
            AddUntilStep($"wait for {typeName}", () => findAll(typeName).Any(d => d.IsLoaded));
            AddWaitStep("fade in", 6);
        }

#if false
        private MapperatorCandidateSet candidateSet()
            => (MapperatorCandidateSet)typeof(MapperatorToolboxGroup).GetField("candidates", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(this.ChildrenOfType<MapperatorToolboxGroup>().First())!;
#endif

        private void parkMouse() => AddStep("park mouse", () => InputManager.MoveMouseTo(InputManager.ScreenSpaceDrawQuad.BottomRight + new Vector2(40)));

        private Drawable rightToolbox() => findAll("ExpandingToolboxContainer").OrderByDescending(d => d.ScreenSpaceDrawQuad.Centre.X).First();

        private Drawable leftToolbox() => findAll("ExpandingToolboxContainer").OrderBy(d => d.ScreenSpaceDrawQuad.Centre.X).First();

        private IEnumerable<Drawable> findAll(string typeName)
            => this.ChildrenOfType<Drawable>().Where(d => d.GetType().Name == typeName && d.IsPresent);

        private void clickType(string typeName)
        {
            var target = findAll(typeName).FirstOrDefault() ?? throw new InvalidOperationException($"No {typeName} is present.");
            InputManager.MoveMouseTo(target);
            InputManager.Click(MouseButton.Left);
        }

        private void clickButton(string textContains)
        {
            var button = this.ChildrenOfType<osu.Game.Graphics.UserInterfaceV2.RoundedButton>()
                             .FirstOrDefault(b => b.IsPresent && b.Text.ToString().Contains(textContains, StringComparison.OrdinalIgnoreCase))
                         ?? throw new InvalidOperationException($"No button containing \"{textContains}\" is present.");
            InputManager.MoveMouseTo(button);
            InputManager.Click(MouseButton.Left);
        }

        /// <summary>
        /// Saves the whole frame.
        /// </summary>
        protected void Capture(string name, int wait = 3) => addCapture(name, wait, () => null);

        /// <summary>
        /// Saves the area of the first present drawable of a type, with a margin.
        /// </summary>
        protected void CapturePopover(string name, float margin = 8)
            => addCapture(name, 0, () => this.ChildrenOfType<Popover>().Where(p => p.State.Value == Visibility.Visible).OrderByDescending(p => p.ScreenSpaceDrawQuad.AABBFloat.Width * p.ScreenSpaceDrawQuad.AABBFloat.Height).FirstOrDefault()?.ScreenSpaceDrawQuad.AABBFloat.Inflate(margin));

        protected void CaptureOf(string name, string typeName, int wait = 3, float margin = 6)
            => addCapture(name, wait, () => findAll(typeName).FirstOrDefault()?.ScreenSpaceDrawQuad.AABBFloat.Inflate(margin));

        /// <summary>
        /// Saves the area of every present drawable of a type, named by <paramref name="name"/>.
        /// </summary>
        protected void CaptureEach(string typeName, Func<Drawable, string> name, int wait = 3, float margin = 6)
        {
            if (wait > 0)
                AddWaitStep("settle", wait);

            Task<Image<Rgba32>>? frame = null;

            AddStep($"capture each {typeName}", () => frame = ((OffscreenGameHost)host).CaptureAsync());
            AddUntilStep($"save each {typeName}", () =>
            {
                if (frame?.IsCompleted != true)
                    return false;

                using (var image = frame.GetAwaiter().GetResult())
                {
                    foreach (var drawable in findAll(typeName))
                        save(image, name(drawable), drawable.ScreenSpaceDrawQuad.AABBFloat.Inflate(margin));
                }

                return true;
            });
        }

        private void addCapture(string name, int wait, Func<RectangleF?> area)
        {
            if (wait > 0)
                AddWaitStep("settle", wait);

            Task<Image<Rgba32>>? frame = null;

            AddStep($"capture {name}", () => frame = ((OffscreenGameHost)host).CaptureAsync());
            AddUntilStep($"save {name}", () =>
            {
                if (frame?.IsCompleted != true)
                    return false;

                using (var image = frame.GetAwaiter().GetResult())
                    save(image, name, area());

                return true;
            });
        }

        private void save(Image<Rgba32> image, string name, RectangleF? area) => Snapshot.Save(image, Path.Combine(options.Output, name + ".png"), area);
    }
}
