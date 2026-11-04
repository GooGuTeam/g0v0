// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.UserInterface;
using osu.Game.Graphics;
using osu.Game.Graphics.UserInterface;
using osu.Game.Localisation;
using osu.Game.Overlays;

namespace osu.Game.Screens.Edit.HitSounding
{
    /// <summary>
    /// The button which opens the set management popover, where set lanes can be reordered and collapsed.
    /// </summary>
    public partial class HitSoundingSetManagementButton : OsuButton, IHasPopover
    {
        private readonly HitSoundingSetLayout layout;

        public HitSoundingSetManagementButton(HitSoundingSetLayout layout)
        {
            this.layout = layout;

            Width = 200;
            Height = 36;
            Text = HitSoundingStrings.ManageSets;
            SpriteText.Font = OsuFont.Default.With(size: 14, weight: FontWeight.Bold);
            Action = this.ShowPopover;
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colours)
        {
            BackgroundColour = colours.Background3;
            SpriteText.Colour = colours.Light3;
        }

        public Popover GetPopover() => new HitSoundingSetManagementPopover(layout);
    }
}
