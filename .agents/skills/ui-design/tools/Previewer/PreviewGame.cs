// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using NUnit.Framework.Internal;
using osu.Framework.Allocation;
using osu.Framework.Extensions.IEnumerableExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Imaging;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Framework.Testing;
using osu.Game;
using Logger = osu.Framework.Logging.Logger;

namespace Previewer
{
    public partial class PreviewGame : OsuGameBase
    {
        public const string NAME = "g0v0-previewer";

        public static string? Error;

        private readonly PreviewOptions options;

        public PreviewGame(PreviewOptions options)
        {
            this.options = options;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            Add(new PreviewRunner(options));
        }
    }

    /// <summary>
    /// Loads the requested scene, runs its steps, captures where they end, then exits the host.
    /// </summary>
    public partial class PreviewRunner : CompositeDrawable
    {
        /// <summary>
        /// The width of a test scene's step list, which is kept off screen.
        /// </summary>
        private const float steps_width = 180;

        private readonly PreviewOptions options;
        private readonly Container sceneContainer;

        private TestScene? scene;
        private int loadedFrames;
        private bool started;

        // When a scene that does not capture by itself is captured; null while its steps run.
        // Wall-clock time: offscreen, game time runs ahead of it, and background loads take real time.
        private long? captureAt;
        private Task<Image<Rgba32>>? frame;

        [Resolved]
        private GameHost host { get; set; } = null!;

        public PreviewRunner(PreviewOptions options)
        {
            this.options = options;

            RelativeSizeAxes = Axes.Both;
            InternalChild = sceneContainer = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Padding = new MarginPadding { Left = -steps_width },
            };
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
            }
        }

        protected override void Update()
        {
            base.Update();

            if (captureAt != null)
            {
                if (Environment.TickCount64 >= captureAt)
                {
                    captureAt = null;
                    frame = ((OffscreenGameHost)host).CaptureAsync();
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

            // Stopping skips the completion callback, so the stop condition starts the capture itself.
            scene.RunAllSteps(
                () => captureAt = Environment.TickCount64 + options.Wait,
                (step, e) => finish($"Step \"{step.Text}\" failed: {e}"),
                step =>
                {
                    if (options.StopBefore == null || !step.Text.ToString().Contains(options.StopBefore, StringComparison.OrdinalIgnoreCase))
                        return false;

                    captureAt = Environment.TickCount64 + options.Wait;
                    return true;
                });
        }

        private void saveCaptures()
        {
            string name = options.Test == null ? options.Scene : $"{options.Scene}-{options.Test}";

            using (var image = frame!.GetAwaiter().GetResult())
            {
                Snapshot.Save(image, Path.Combine(options.Output, name + ".png"), null);

                foreach (string type in options.Crops)
                {
                    var drawables = scene!.ChildrenOfType<Drawable>().Where(d => matchesType(d.GetType(), type) && d.IsPresent).ToArray();

                    if (drawables.Length == 0)
                        Logger.Log($"No {type} is present to crop.", level: LogLevel.Important);

                    for (int i = 0; i < drawables.Length; i++)
                        Snapshot.Save(image, Path.Combine(options.Output, $"{name}-{type}{(i > 0 ? $"-{i + 1}" : string.Empty)}.png"), drawables[i].ScreenSpaceDrawQuad.AABBFloat.Inflate(6));
                }
            }

            frame = null;
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

    /// <summary>
    /// Finds the client's visual test scenes and adds their steps as the test browser would.
    /// </summary>
    public static class TestScenes
    {
        public static Type Find(string name)
        {
            foreach (string file in Directory.GetFiles(AppContext.BaseDirectory, "osu.Game*.Tests.dll"))
                Assembly.Load(Path.GetFileNameWithoutExtension(file));

            var matches = AppDomain.CurrentDomain.GetAssemblies()
                                   .Where(a => a.GetName().Name?.EndsWith(".Tests", StringComparison.Ordinal) == true)
                                   .SelectMany(a => a.GetTypes())
                                   .Where(t => t.IsSubclassOf(typeof(TestScene)) && !t.IsAbstract && t.IsPublic)
                                   .Where(t => t.FullName == name || t.Name == name || t.Name == "TestScene" + name)
                                   .ToArray();

            return matches.Length switch
            {
                1 => matches[0],
                0 => throw new ArgumentException($"No visual test scene named {name}."),
                _ => throw new ArgumentException($"{name} is ambiguous; use the full name: {string.Join(", ", matches.Select(t => t.FullName))}."),
            };
        }

        /// <summary>
        /// Adds the [SetUp] and [SetUpSteps] steps, then the steps of one test method (the <paramref name="testCase"/>th of its cases), if given.
        /// </summary>
        public static void AddSteps(TestScene scene, string? test, int testCase)
        {
            var setUps = methodsWith<SetUpAttribute>(scene.GetType());

            if (setUps.Length > 0)
                scene.AddStep("[SetUp]", () => setUps.ForEach(m => m.Invoke(scene, null)));

            foreach (var method in methodsWith<SetUpStepsAttribute>(scene.GetType()))
                method.Invoke(scene, null);

            if (test == null)
                return;

            var candidates = scene.GetType().GetMethods().Where(m => m.Name == test || m.Name == "Test" + test).ToArray();

            if (candidates.Length != 1)
                throw new ArgumentException($"{scene.GetType().Name} has {candidates.Length} methods named {test}.");

            var arguments = argumentsFor(candidates[0]).ToArray();

            if (testCase < 0 || testCase >= arguments.Length)
                throw new ArgumentException($"{candidates[0].Name} has {arguments.Length} cases; --case must be below that.");

            candidates[0].Invoke(scene, arguments[testCase]);
        }

        private static IEnumerable<object?[]> argumentsFor(MethodInfo method)
        {
            var wrapper = new MethodWrapper(method.DeclaringType!, method);
            var cases = method.GetCustomAttributes<TestCaseAttribute>(false).Select(tc => tc.BuildFrom(wrapper, null).Single().Arguments)
                              .Concat(method.GetCustomAttributes<TestCaseSourceAttribute>(false).SelectMany(tcs => tcs.BuildFrom(wrapper, null).Select(t => t.Arguments)))
                              .ToList();

            if (method.GetCustomAttribute<TestAttribute>(false) != null)
            {
                if (method.GetParameters().Length == 0)
                    cases.Add(Array.Empty<object?>());
                else
                {
                    var values = wrapper.GetParameters().Select(p => p.GetCustomAttributes<ValuesAttribute>(false).Single().GetData(p).Cast<object?>());
                    cases.AddRange(values.CartesianProduct().Select(c => c.ToArray()));
                }
            }

            return cases;
        }

        // Base classes first, as NUnit and the test browser run them.
        private static MethodInfo[] methodsWith<T>(Type type) where T : Attribute
        {
            var hierarchy = new List<Type>();

            for (var t = type; t != null; t = t.BaseType)
                hierarchy.Insert(0, t);

            return hierarchy.SelectMany(t => t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                            .Where(m => m.GetCustomAttribute<T>(false) != null)
                            .ToArray();
        }
    }

    public static class Snapshot
    {
        /// <summary>
        /// Saves <paramref name="image"/>, or the part of it inside <paramref name="area"/>, as a PNG.
        /// </summary>
        public static void Save(Image<Rgba32> image, string path, RectangleF? area)
        {
            if (area is not RectangleF rect)
            {
                using (var stream = File.Create(path))
                    image.SaveAsPng(stream);

                return;
            }

            int left = Math.Clamp((int)Math.Floor(rect.Left), 0, image.Width - 1);
            int top = Math.Clamp((int)Math.Floor(rect.Top), 0, image.Height - 1);
            int right = Math.Clamp((int)Math.Ceiling(rect.Right), left + 1, image.Width);
            int bottom = Math.Clamp((int)Math.Ceiling(rect.Bottom), top + 1, image.Height);

            using (var crop = new Image<Rgba32>(right - left, bottom - top))
            {
                for (int y = top; y < bottom; y++)
                {
                    for (int x = left; x < right; x++)
                        crop[x - left, y - top] = image[x, y];
                }

                using (var stream = File.Create(path))
                    crop.SaveAsPng(stream);
            }
        }
    }
}
