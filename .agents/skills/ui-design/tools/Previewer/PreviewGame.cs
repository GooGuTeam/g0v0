// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Imaging;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Framework.Testing;
using osu.Framework.Testing.Drawables.Steps;
using osu.Game;
using Logger = osu.Framework.Logging.Logger;

namespace Previewer
{
    public partial class PreviewGame(PreviewOptions options) : OsuGameBase
    {
        public const string NAME = "g0v0-previewer";

        public static string? Error;

        // cached for the runner and for the step helpers scenes use, so that both read the same run description.
        [Cached]
        private readonly PreviewOptions options = options;

        // previews are silent: test scenes play music and samples, which would otherwise come out of the speakers.
        private readonly BindableNumber<double> mute = new BindableDouble();

        protected override void LoadComplete()
        {
            base.LoadComplete();

            Audio.AddAdjustment(AdjustableProperty.Volume, mute);
            Add(new PreviewRunner());
        }
    }

    /// <summary>
    /// Loads the requested scene, runs its steps, captures where they end, then exits the host.
    /// </summary>
    public partial class PreviewRunner : CompositeDrawable
    {
        private readonly Container sceneContainer;

        private TestScene? scene;
        private int loadedFrames;
        private bool started;
        private bool offsetApplied;

        // When a scene that does not capture by itself is captured; null while its steps run.
        // Wall-clock time: offscreen, game time runs ahead of it, and background loads take real time.
        private long? captureAt;
        private Task<Image<Rgba32>>? frame;

        // Set while capturing before a --capture-before step: the step to resume after, and the file name suffix.
        private (StepButton? resumeAfter, string suffix)? intermediate;
        private readonly HashSet<StepButton> capturedBefore = [];
        private int intermediateCount;

        [Resolved]
        private GameHost host { get; set; } = null!;

        [Resolved]
        private PreviewOptions options { get; set; } = null!;

        public PreviewRunner()
        {
            RelativeSizeAxes = Axes.Both;
            InternalChild = sceneContainer = new Container { RelativeSizeAxes = Axes.Both };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            try
            {
                sceneContainer.Child = scene = options.Scene == "editor" ? new EditorPreviewScene(options) : (TestScene)Activator.CreateInstance(TestScenes.Find(options.Scene))!;
            }
            catch (Exception e)
            {
                finish(e.Message);
                return;
            }

            // added after the scene, so that it updates last on every frame.
            sceneContainer.Add(new SuppressTakeControlOverlay(scene));
        }

        protected override void Update()
        {
            base.Update();

            applyStepsColumnOffset();

            if (captureAt != null)
            {
                if (Environment.TickCount64 >= captureAt)
                {
                    captureAt = null;
                    frame = FrameCapture.Async(host);
                }

                return;
            }

            if (frame != null)
            {
                if (frame.IsCompleted)
                    saveCaptures();
                return;
            }

            if (started || scene?.IsLoaded != true || ++loadedFrames < 3)
                return;

            started = true;

            if (scene is EditorPreviewScene editor)
            {
                try
                {
                    editor.SetUpSteps();
                    editor.AddScripts();
                }
                catch (Exception e)
                {
                    finish(e.InnerException?.Message ?? e.Message);
                    return;
                }

                editor.RunAllSteps(() => finish(null), (step, e) => finish($"Step \"{step.Text}\" failed: {e}"));
                return;
            }

            try
            {
                TestScenes.AddSteps(scene, options.Test, options.Case);
            }
            catch (Exception e)
            {
                finish(e.InnerException?.Message ?? e.Message);
                return;
            }

            runSteps(null);
        }

        /// <summary>
        /// Moves the scene left by the width of the test browser's step column, so that captures show the frame the game
        /// would give the scene. Read from the drawn column rather than a constant, as the framework keeps it private.
        /// </summary>
        private void applyStepsColumnOffset()
        {
            if (offsetApplied || scene?.IsLoaded != true)
                return;

            if (scene.StepsContainer.Parent is not Drawable stepsColumn)
                return;

            float width = stepsColumn.DrawWidth;

            if (width <= 1)
                return;

            sceneContainer.X = -width;
            offsetApplied = true;
        }

        private void runSteps(StepButton? startAfter)
        {
            // Stopping skips the completion callback, so the stop condition starts the capture itself.
            scene!.RunAllSteps(
                () => captureAt = Environment.TickCount64 + options.Wait,
                (step, e) => finish($"Step \"{step.Text}\" failed: {e}"),
                step =>
                {
                    string text = step.Text.ToString();

                    if (!capturedBefore.Contains(step) && options.CaptureBefore.Any(c => text.Contains(c, StringComparison.OrdinalIgnoreCase)))
                    {
                        // capture here, then carry on from this step.
                        capturedBefore.Add(step);
                        int index = scene.StepsContainer.IndexOf(step);
                        intermediate = (index > 0 ? scene.StepsContainer.Children[index - 1] as StepButton : null, $"{++intermediateCount:00}-{slug(text)}");
                        captureAt = Environment.TickCount64 + options.Wait;
                        return true;
                    }

                    if (options.StopBefore == null || !text.Contains(options.StopBefore, StringComparison.OrdinalIgnoreCase))
                        return false;

                    captureAt = Environment.TickCount64 + options.Wait;
                    return true;
                },
                startAfter);
        }

        private static string slug(string text)
        {
            string s = new string(text.Select(c => char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '-').ToArray());

            while (s.Contains("--", StringComparison.Ordinal))
                s = s.Replace("--", "-", StringComparison.Ordinal);

            s = s.Trim('-');
            return s.Length > 40 ? s[..40].TrimEnd('-') : s;
        }

        private void saveCaptures()
        {
            string name = options.Test == null ? options.Scene : $"{options.Scene}-{options.Test}";
            var current = intermediate;

            if (current != null)
                name += "-" + current.Value.suffix;

            using (var image = frame!.GetAwaiter().GetResult())
            {
                FrameCapture.Save(image, Path.Combine(options.Output, name + ".png"), null);

                foreach (string type in options.Crops)
                {
                    var drawables = scene!.ChildrenOfType<Drawable>().Where(d => matchesType(d.GetType(), type) && d.IsPresent).ToArray();

                    if (drawables.Length == 0)
                        Logger.Log($"No {type} is present to crop.", level: LogLevel.Important);

                    for (int i = 0; i < drawables.Length; i++)
                    {
                        string cropName = $"{name}-{type}{(i > 0 ? $"-{i + 1}" : string.Empty)}";

                        try
                        {
                            FrameCapture.Save(image, Path.Combine(options.Output, cropName + ".png"), drawables[i].ScreenSpaceDrawQuad.AABBFloat.Inflate(6));
                        }
                        catch (ArgumentException e)
                        {
                            // a drawable can be present yet drawn outside the frame; there is nothing to crop from it.
                            Logger.Log($"Skipped {cropName}: {e.Message}", level: LogLevel.Important);
                        }
                    }
                }
            }

            frame = null;

            if (current != null)
            {
                intermediate = null;
                runSteps(current.Value.resumeAfter);
                return;
            }

            finish(null);
        }

        private static bool matchesType(Type drawableType, string targetName)
        {
            for (Type? t = drawableType; t != null && t != typeof(object); t = t.BaseType)
            {
                if (string.Equals(t.Name, targetName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private void finish(string? error)
        {
            PreviewGame.Error = error;

            if (error != null)
                Logger.Log(error, level: LogLevel.Error);

            Schedule(host.Exit);
        }
    }
}
