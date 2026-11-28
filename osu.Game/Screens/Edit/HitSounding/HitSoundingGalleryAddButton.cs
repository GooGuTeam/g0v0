// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.UserInterface;
using osu.Game.Overlays;
using osuTK;

namespace osu.Game.Screens.Edit.HitSounding
{
    internal partial class HitSoundingGalleryAddButton : OsuButton, IHasTooltip
    {
        public LocalisableString TooltipText { get; init; }

        private SpriteIcon icon = null!;

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colours)
        {
            BackgroundColour = colours.Background3;
            SpriteText.Font = OsuFont.Default.With(size: 14);
            SpriteText.Colour = colours.Content1;
            SpriteText.X = 8;
            Content.Add(icon = new SpriteIcon
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.CentreRight,
                Size = new Vector2(12),
                Icon = FontAwesome.Solid.Plus,
                Colour = colours.Content1,
            });
        }

        protected override void Update()
        {
            base.Update();
            icon.X = SpriteText.X - SpriteText.DrawWidth / 2 - 5;
        }
    }
}
