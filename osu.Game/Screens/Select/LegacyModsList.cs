// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.
// Ported from ShikkesoraSIM/torii-osu, commit 8d7f1f25b737263dd140bbe59b0d51b999a7fc24.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Rulesets.Mods;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Select
{
    /// <summary>
    /// osu!stable's selected mod readout in song select: the names of the active mods in
    /// semi-transparent white text above the footer. stable: pText size 30 at (60, 397) in 480 space,
    /// Color(255, 255, 255, 128), with a text outline.
    /// </summary>
    public partial class LegacyModsList : CompositeDrawable
    {
        [Resolved]
        private Bindable<IReadOnlyList<Mod>> mods { get; set; } = null!;

        private OsuSpriteText text = null!;

        [BackgroundDependencyLoader]
        private void load()
        {
            RelativeSizeAxes = Axes.Both;

            InternalChild = text = new OsuSpriteText
            {
                Position = new Vector2(60, 397) * 1.6f,
                Font = LegacyFonts.Get(30 * 1.6f),
                Colour = new Color4(255, 255, 255, 128),
                Shadow = true,
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            mods.BindValueChanged(m => text.Text = string.Join(@",", m.NewValue.Select(mod => mod.Name)), true);
        }
    }
}
