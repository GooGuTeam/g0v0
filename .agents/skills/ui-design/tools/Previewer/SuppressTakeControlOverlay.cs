// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Reflection;
using osu.Framework.Graphics;
using osu.Game.Tests.Visual;

namespace Previewer
{
    /// <summary>
    /// Keeps the test browser's "overriding local input" banner out of captures. The scene shows it again on every
    /// frame for as long as its input overrides input, and gives no way to turn that off, so a preview has to hide the
    /// banner rather than stop it being shown.
    /// Added after the scene it watches, so that it has the last say on each frame.
    /// </summary>
    public partial class SuppressTakeControlOverlay(Drawable scene) : Drawable
    {
        private static readonly FieldInfo? overlay_field =
            typeof(OsuManualInputManagerTestScene).GetField("takeControlOverlay", BindingFlags.NonPublic | BindingFlags.Instance);

        private Drawable? overlay;
        private bool resolved;

        protected override void Update()
        {
            base.Update();

            if (!resolved)
            {
                resolved = true;

                if (scene is OsuManualInputManagerTestScene)
                    overlay = overlay_field?.GetValue(scene) as Drawable;
            }

            if (overlay?.Alpha > 0)
                overlay.Alpha = 0;
        }
    }
}
