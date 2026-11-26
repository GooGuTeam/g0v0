// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Imaging;
using osu.Framework.Platform;
using osu.Framework.Testing;
using osu.Framework.Testing.Drawables.Steps;

namespace Previewer
{
    /// <summary>
    /// Steps any previewed scene can add, without knowing how previews are captured: waiting on a condition, finding
    /// controls by type name, and saving named PNGs of the frame. The scene supplies what to look at; the mechanics
    /// live here.
    /// </summary>
    public static class SceneSteps
    {
        /// <summary>
        /// Every present drawable whose type is named <paramref name="typeName"/>, or derived from it.
        /// </summary>
        public static IEnumerable<Drawable> FindAll(this TestScene scene, string typeName)
            => scene.ChildrenOfType<Drawable>().Where(d => matchesType(d.GetType(), typeName) && d.IsPresent);

        /// <summary>
        /// Repeats the next steps' evaluation for the given number of frames.
        /// </summary>
        public static void Wait(this TestScene scene, string description, int frames)
            => scene.AddStep(new RepeatStepButton { Text = description, Count = frames, IsSetupStep = false });

        /// <summary>
        /// Advances once <paramref name="condition"/> holds, failing the run if it does not within the usual timeout.
        /// </summary>
        public static void WaitUntil(this TestScene scene, string description, Func<bool> condition)
            => scene.AddStep(new UntilStepButton { Text = description, Assertion = condition, CallStack = new StackTrace(true), IsSetupStep = false });

        /// <summary>
        /// Saves the whole frame.
        /// </summary>
        public static void Capture(this TestScene scene, string name, int wait = 3)
            => scene.capture(name, wait, () => null);

        /// <summary>
        /// Saves the area of the first present drawable of a type, with a margin.
        /// </summary>
        /// <exception cref="InvalidOperationException">no such drawable exists.</exception>
        public static void CaptureOf(this TestScene scene, string name, string typeName, int wait = 3, float margin = 6)
            => scene.capture(name, wait, () => (scene.FindAll(typeName).FirstOrDefault()
                                                ?? throw new InvalidOperationException($"No {typeName} is present to capture as {name}."))
                                              .ScreenSpaceDrawQuad.AABBFloat.Inflate(margin));

        /// <summary>
        /// Saves the area of every present drawable of a type, named by <paramref name="name"/>.
        /// </summary>
        /// <exception cref="InvalidOperationException">none of the drawable satisfies the condition.</exception>
        public static void CaptureEach(this TestScene scene, string typeName, Func<Drawable, string> name, int wait = 3, float margin = 6)
        {
            if (wait > 0)
                scene.Wait("settle", wait);

            Task<Image<Rgba32>>? frame = null;

            scene.AddStep($"capture each {typeName}", () => frame = FrameCapture.Async(scene.Dependencies.Get<GameHost>()));
            scene.WaitUntil($"save each {typeName}", () =>
            {
                if (frame?.IsCompleted != true)
                    return false;

                using var image = frame.GetAwaiter().GetResult();
                var drawables = scene.FindAll(typeName).ToArray();

                if (drawables.Length == 0)
                    throw new InvalidOperationException($"No {typeName} is present to capture.");

                foreach (var drawable in drawables)
                    scene.save(image, name(drawable), drawable.ScreenSpaceDrawQuad.AABBFloat.Inflate(margin));

                return true;
            });
        }

        /// <summary>
        /// Saves the area of the visible popover that covers the most of the frame, with a margin.
        /// </summary>
        /// <exception cref="InvalidOperationException">no popover is currently visible.</exception>
        public static void CapturePopover(this TestScene scene, string name, float margin = 8)
            => scene.capture(name, 0, () => (scene.ChildrenOfType<Popover>()
                                                  .Where(p => p.State.Value == Visibility.Visible)
                                                  .OrderByDescending(p => p.ScreenSpaceDrawQuad.AABBFloat.Width * p.ScreenSpaceDrawQuad.AABBFloat.Height)
                                                  .FirstOrDefault()
                                             ?? throw new InvalidOperationException($"No popover is visible to capture as {name}."))
                                           .ScreenSpaceDrawQuad.AABBFloat.Inflate(margin));

        private static void capture(this TestScene scene, string name, int wait, Func<RectangleF?> area)
        {
            if (wait > 0)
                scene.Wait("settle", wait);

            Task<Image<Rgba32>>? frame = null;

            scene.AddStep($"capture {name}", () => frame = FrameCapture.Async(scene.Dependencies.Get<GameHost>()));
            scene.WaitUntil($"save {name}", () =>
            {
                if (frame?.IsCompleted != true)
                    return false;

                using var image = frame.GetAwaiter().GetResult();
                scene.save(image, name, area());

                return true;
            });
        }

        private static void save(this TestScene scene, Image<Rgba32> image, string name, RectangleF? area)
            => FrameCapture.Save(image, Path.Combine(scene.Dependencies.Get<PreviewOptions>().Output, name + ".png"), area);

        private static bool matchesType(Type drawableType, string targetName)
        {
            for (Type? t = drawableType; t != null && t != typeof(object); t = t.BaseType)
            {
                if (string.Equals(t.Name, targetName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
    }
}
