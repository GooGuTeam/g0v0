// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Configuration;
using osu.Framework.Platform;

namespace osu.Game.Graphics.UserInterface
{
    /// <summary>
    /// The frame rate an FPS counter measures against.
    /// </summary>
    public static class FrameRateTarget
    {
        /// <summary>
        /// Targets at or above this have no finite cap (<see cref="int.MaxValue"/> is used by the framework for VSync and Unlimited).
        /// </summary>
        public const double UNLIMITED_THRESHOLD = int.MaxValue;

        /// <summary>
        /// The frame rate to judge against a limiter's <paramref name="target"/>: the target itself, or the display's refresh rate once the
        /// target is effectively unlimited, as stable's <c>FpsDisplay</c> does. Without this, any frame rate is a tiny fraction of an
        /// unlimited target and reads as far too slow.
        /// </summary>
        public static double Effective(double target, GameHost host, FrameSync frameSync = FrameSync.Unlimited)
        {
            if (!IsUnlimited(target, frameSync))
                return target;

            float refreshRate = host.Window?.CurrentDisplayMode.Value.RefreshRate ?? 0;
            return refreshRate > 0 ? refreshRate : 60;
        }

        /// <summary>
        /// Whether a limiter's <paramref name="target"/> is effectively unlimited: uncapped (<c>&lt;= 0</c> or <see cref="int.MaxValue"/>,
        /// which VSync and multithreaded Unlimited set), or <see cref="FrameSync.Unlimited"/> on the unthrottled 1000 Hz input fallback.
        /// Explicit multipliers such as <see cref="FrameSync.Limit8x"/> remain finite targets even above 1000 fps on fast displays.
        /// </summary>
        public static bool IsUnlimited(double target, FrameSync frameSync = FrameSync.Unlimited)
            => target <= 0 || target >= UNLIMITED_THRESHOLD || (frameSync == FrameSync.Unlimited && target >= 1000);
    }
}
