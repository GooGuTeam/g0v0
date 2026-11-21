// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Extensions.LocalisationExtensions;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Graphics;

namespace osu.Game.Overlays.Notifications
{
    /// <summary>
    /// A <see cref="SimpleNotification"/> which displays a small bold heading above a body of text.
    /// </summary>
    public abstract partial class HeadingNotification : SimpleNotification
    {
        /// <summary>
        /// The heading text displayed above the body.
        /// </summary>
        protected abstract LocalisableString Heading { get; }

        /// <summary>
        /// The colour applied to the heading text.
        /// </summary>
        protected abstract ColourInfo GetHeadingColour(OsuColour colours);

        protected HeadingNotification(IconUsage icon)
        {
            Icon = icon;
        }

        [BackgroundDependencyLoader]
        private void load(OsuColour colours)
        {
            TextFlow.Clear();
            TextFlow.AddText(Heading.ToUpper(), s =>
            {
                s.Font = OsuFont.Style.Caption2.With(weight: FontWeight.Bold);
                s.Colour = GetHeadingColour(colours);
            });

            AddBody();
        }

        /// <summary>
        /// Construct the body of the notification below the heading.
        /// </summary>
        protected abstract void AddBody();

        /// <summary>
        /// Adds a body paragraph with the standard caption font.
        /// </summary>
        protected void AddBodyParagraph(LocalisableString paragraph) =>
            TextFlow.AddParagraph(paragraph, s => s.Font = OsuFont.Style.Caption1);
    }
}
