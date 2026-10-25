// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.
// Ported from ShikkesoraSIM/torii-osu, commit 8d7f1f25b737263dd140bbe59b0d51b999a7fc24.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Screens.Select.Filter;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Select
{
    /// <summary>
    /// The osu!stable grouping tab strip (Collections / Recently Played / By Artist / By Difficulty /
    /// No Grouping). Each tab is the skinnable "selection-tab" texture (a rounded rectangle) painted
    /// crimson when unselected and white when selected. The backgrounds overlap (228.8 wide texture,
    /// 118.4 step, i.e. stable's 143/74 scaled by 1.6) so they look like one continuous band, and the
    /// labels live on a layer above so a neighbour's overlap can't cover the text. Drives the shared
    /// <see cref="GroupMode"/>.
    /// </summary>
    public partial class LegacyGroupTabs : CompositeDrawable
    {
        public Bindable<GroupMode> Current { get; } = new Bindable<GroupMode>();

        // Each tab is a rounded rectangle roughly one step wide, with a tiny overlap so the seams close
        // (a joined band, not the heavy overlap of the full 228px texture).
        private const float step = 124f;
        private const float bg_width = 150f;
        private const float tab_height = 24f;

        private static readonly Color4 crimson = Color4.Crimson;

        private static readonly (GroupMode mode, string text)[] tab_definitions =
        {
            (GroupMode.Collections, @"Collections"),
            (GroupMode.LastPlayed, @"Recently Played"),
            (GroupMode.Artist, @"By Artist"),
            (GroupMode.Difficulty, @"By Difficulty"),
            (GroupMode.None, @"No Grouping"),
        };

        private Container backgroundLayer = null!;
        private Sprite[] backgrounds = null!;
        private OsuSpriteText[] labels = null!;

        [BackgroundDependencyLoader]
        private void load(ISkinSource skin)
        {
            Height = tab_height;
            Width = tab_definitions.Length * step;

            Texture? tabTexture = skin.GetTexture(@"selection-tab");

            backgroundLayer = new Container { RelativeSizeAxes = Axes.Both };
            var foregroundLayer = new Container { RelativeSizeAxes = Axes.Both };

            backgrounds = new Sprite[tab_definitions.Length];
            labels = new OsuSpriteText[tab_definitions.Length];

            for (int i = 0; i < tab_definitions.Length; i++)
            {
                var def = tab_definitions[i];

                backgroundLayer.Add(backgrounds[i] = new Sprite
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.Centre,
                    X = i * step + step / 2,
                    Size = new Vector2(bg_width, tab_height),
                    Texture = tabTexture,
                    Colour = crimson,
                });

                var slot = new OsuClickableContainer
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    X = i * step,
                    Size = new Vector2(step, tab_height),
                    Action = () => Current.Value = def.mode,
                };
                slot.Add(labels[i] = new OsuSpriteText
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Text = def.text,
                    Font = LegacyFonts.Get(14, FontWeight.Bold),
                    Shadow = true,
                });
                foregroundLayer.Add(slot);
            }

            InternalChildren = new Drawable[] { backgroundLayer, foregroundLayer };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            Current.BindValueChanged(_ => updateSelection(), true);
        }

        private void updateSelection()
        {
            for (int i = 0; i < tab_definitions.Length; i++)
            {
                bool selected = tab_definitions[i].mode == Current.Value;

                backgrounds[i].FadeColour(selected ? Color4.White : crimson, 40, Easing.OutQuint);
                labels[i].FadeColour(selected ? Color4.Black : Color4.White, 40, Easing.OutQuint);
                labels[i].Shadow = !selected;

                // The selected tab draws above the neighbours that overlap it; the rest keep left-to-right order.
                backgroundLayer.ChangeChildDepth(backgrounds[i], selected ? -1 : 0);
            }
        }
    }
}
