// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Graphics;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Testing;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Catch;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Taiko;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.UI;
using osu.Game.Rulesets.Edit;
using osu.Game.Screens.Edit;
using osu.Game.Tests.Visual;
using osuTK;
using osuTK.Input;

namespace Previewer
{
    /// <summary>
    /// The editor screens, captured by the scripts in <see cref="AddScripts"/>. Each script only names what to show;
    /// looking controls up and saving frames is <see cref="SceneSteps"/>' and <see cref="FrameCapture"/>'s work.
    /// </summary>
    public partial class EditorPreviewScene(PreviewOptions options) : EditorTestScene
    {
        protected override Ruleset CreateEditorRuleset() => options.Ruleset switch
        {
            "taiko" => new TaikoRuleset(),
            "catch" => new CatchRuleset(),
            "mania" => new ManiaRuleset(),
            _ => new OsuRuleset(),
        };

        /// <summary>
        /// Adds the steps for every requested script. Runs started without one still get an editor at rest.
        /// </summary>
        public void AddScripts()
        {
            parkMouse();

            if (options.Seek > 0)
                AddStep($"seek to {options.Seek}ms", () => EditorClock.Seek(options.Seek));

            foreach (string script in options.Scripts)
                addScript(script);
        }

        private void addScript(string script)
        {
            switch (script)
            {
                case "compose":
                    this.Capture("compose");
                    this.CaptureEach("ExpandingToolboxContainer", d => d.ScreenSpaceDrawQuad.Centre.X < InputManager.ScreenSpaceDrawQuad.Centre.X ? "compose-toolbox-left" : "compose-toolbox-right");
                    this.CaptureOf("compose-bottom-bar", "BottomBar");
                    this.CaptureOf("compose-timeline", "TimelineArea");
                    this.CaptureOf("compose-menu-bar", "EditorMenuBar");
                    break;

                case "toolbox-hover":
                    // Expanded toolboxes, as when hovered.
                    AddStep("hover right toolbox", () => InputManager.MoveMouseTo(rightToolbox()));
                    this.Capture("toolbox-hover", wait: 6);
                    AddStep("hover left toolbox", () => InputManager.MoveMouseTo(leftToolbox()));
                    this.Capture("toolbox-hover-left", wait: 6);
                    parkMouse();
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
                    this.Capture("context-menu");
                    AddStep("hover menu item", () =>
                    {
                        var item = this.ChildrenOfType<Menu.DrawableMenuItem>().LastOrDefault(i => i.IsPresent);

                        if (item != null)
                            InputManager.MoveMouseTo(item);
                    });
                    this.Capture("context-menu-item", wait: 4);
                    AddStep("close menu", () =>
                    {
                        InputManager.MoveMouseTo(Editor.ChildrenOfType<Playfield>().First().ScreenSpaceDrawQuad.BottomLeft + new Vector2(60, -60));
                        InputManager.Click(MouseButton.Left);
                        EditorBeatmap.SelectedHitObjects.Clear();
                    });
                    parkMouse();
                    break;

                case "timestamp-input":
                    clickType("TimestampControl");
                    parkMouse();
                    this.CaptureOf("timestamp-input", "BottomBar", wait: 3);
                    AddStep("unfocus", () => InputManager.Key(Key.Enter));
                    break;

                case "timing":
                    AddStep("timing screen", () => Editor.Mode.Value = EditorScreenMode.Timing);
                    waitForScreen("ControlPointList");
                    this.Capture("timing");
                    this.CaptureOf("timing-control-points", "ControlPointList", wait: 0);
                    this.CaptureOf("timing-settings", "ControlPointSettings", wait: 0);
                    break;

                case "verify":
                    AddStep("verify screen", () => Editor.Mode.Value = EditorScreenMode.Verify);
                    waitForScreen("IssueList");
                    AddWaitStep("wait for results", 10);
                    this.Capture("verify");
                    AddStep("show every severity", () => Editor.ChildrenOfType<osu.Game.Screens.Edit.Verify.VerifyScreen>().First().HiddenIssueTypes.Clear());
                    this.Capture("verify-all");
                    AddStep("select first issue", () =>
                    {
                        var row = this.FindAll("DrawableIssue").OrderBy(d => d.ScreenSpaceDrawQuad.TopLeft.Y).FirstOrDefault();

                        if (row != null)
                        {
                            InputManager.MoveMouseTo(row);
                            InputManager.Click(MouseButton.Left);
                        }
                    });
                    parkMouse();
                    this.Capture("verify-selected", wait: 4);
                    this.CaptureOf("verify-list-selected", "IssueList", wait: 0);
                    break;

                case "design":
                    AddStep("design screen", () => Editor.Mode.Value = EditorScreenMode.Design);
                    waitForScreen("DesignScreen");
                    this.Capture("design");
                    break;

                case "setup":
                    AddStep("setup screen", () => Editor.Mode.Value = EditorScreenMode.SongSetup);
                    waitForScreen("SetupScreen");
                    this.Capture("setup");
                    this.CaptureOf("setup-metadata", "MetadataSection", wait: 0);

                    // Each ruleset brings its own difficulty section (OsuDifficultySection, TaikoDifficultySection, ...)
                    // and none of them derive from the generic DifficultySection, so the name comes from the ruleset in use.
                    string difficultySection = CreateEditorRuleset().GetType().Name.Replace("Ruleset", string.Empty, StringComparison.Ordinal) + "DifficultySection";
                    this.CaptureOf("setup-difficulty", difficultySection, wait: 0);
                    break;

                case "compose-screen":
                    AddStep("compose screen", () => Editor.Mode.Value = EditorScreenMode.Compose);
                    AddWaitStep("settle", 6);
                    break;

                default:
                    throw new ArgumentException($"Unknown script {script}");
            }
        }

        private Drawable rightToolbox() => this.FindAll("ExpandingToolboxContainer").OrderByDescending(d => d.ScreenSpaceDrawQuad.Centre.X).First();

        private Drawable leftToolbox() => this.FindAll("ExpandingToolboxContainer").OrderBy(d => d.ScreenSpaceDrawQuad.Centre.X).First();

        // Mouse and screen-wait helpers stay on the scene: driving input needs the manager this scene owns, which the
        // framework only exposes to subclasses.
        private void waitForScreen(string typeName)
        {
            this.WaitUntil($"wait for {typeName}", () => this.FindAll(typeName).Any(d => d.IsLoaded));
            AddWaitStep("fade in", 6);
        }

        private void parkMouse() => AddStep("park mouse", () => InputManager.MoveMouseTo(InputManager.ScreenSpaceDrawQuad.BottomRight + new Vector2(40)));

        private void clickType(string typeName)
        {
            AddStep($"click {typeName}", () =>
            {
                Drawable target = this.FindAll(typeName).FirstOrDefault() ?? throw new InvalidOperationException($"No {typeName} is present.");
                InputManager.MoveMouseTo(target);
                InputManager.Click(MouseButton.Left);
            });
        }
    }
}
