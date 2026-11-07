// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Platform;

namespace osu.Game.Graphics.UserInterface
{
    /// <summary>
    /// The frame rate an FPS counter measures against.
    /// </summary>
    public static class FrameRateTarget
    {
        /// <summary>
        /// Targets above this are effectively unlimited (VSync's and Unlimited's limiters are <see cref="int.MaxValue"/>, and 8x can pass it).
        /// </summary>
        public const double UNLIMITED_THRESHOLD = 1000;

        /// <summary>
        /// The frame rate to judge against a limiter's <paramref name="target"/>: the target itself, or the display's refresh rate once the
        /// target is effectively unlimited, as stable's <c>FpsDisplay</c> does. Without this, any frame rate is a tiny fraction of an
        /// unlimited target and reads as far too slow.
        /// </summary>
        public static double Effective(double target, GameHost host)
        {
            if (!IsUnlimited(target))
                return target;

            float refreshRate = host.Window?.CurrentDisplayMode.Value.RefreshRate ?? 0;
            return refreshRate > 0 ? refreshRate : 60;
        }

        /// <summary>
        /// Whether a limiter's <paramref name="target"/> is effectively unlimited: above the threshold, or 0, which the framework uses for no limit.
        /// </summary>
        public static bool IsUnlimited(double target) => target <= 0 || target > UNLIMITED_THRESHOLD;
    }
}
