// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Rulesets;
using osu.Game.Screens.Select;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Skinning.Select
{
    internal partial class LegacyModeMenu : OsuFocusedOverlayContainer
    {
        private const float row_height = 128;
        private const float menu_width = 448;

        protected override bool DimMainContent => false;

        [BackgroundDependencyLoader]
        private void load(RulesetStore rulesets, Bindable<RulesetInfo> ruleset, ISkinSource skin, SkinManager skins)
        {
            var available = rulesets.AvailableRulesets.Where(r => r.OnlineID is >= 0 and <= 3)
                                    .OrderByDescending(r => r.OnlineID)
                                    .ToArray();

            Size = new Vector2(menu_width, row_height * available.Length);

            list = new FillFlowContainer<ModeRow>
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
            };

            // stable draws all the rows' backgrounds (depth 0.99), then all the mode artwork (0.991), then all the names (0.992). a skin may put
            // the whole menu's artwork in one of the textures, spanning several rows, so every row's highlight has to stay below it.
            var icons = new Container { RelativeSizeAxes = Axes.Both };
            var labels = new Container { RelativeSizeAxes = Axes.Both };

            var content = new Container
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Children = new Drawable[] { list, icons, labels },
            };

            // Only the four standard modes belong here. Skin artwork may extend beyond the rows.
            InternalChild = content;

            foreach (var info in available)
            {
                var skinTexture = skin.GetStableTexture($"mode-{info.ShortName}-med");
                var texture = skinTexture ?? skins.DefaultClassicSkin.GetStableTexture($"mode-{info.ShortName}-small");
                Drawable icon = texture == null
                    ? info.CreateInstance().CreateIcon().With(d => d.Size = new Vector2(104))
                    : new Sprite { Texture = texture };

                var row = new ModeRow(info, icon, springsOnHover: skinTexture == null)
                {
                    Action = () =>
                    {
                        ruleset.Value = info;
                        Hide();
                    },
                };
                list.Add(row);
                icons.Add(row.CreateIconProxy());
                labels.Add(row.CreateTextProxy());
            }
        }

        private FillFlowContainer<ModeRow> list = null!;

        // stable's pSpriteMenu shows its items from the first (the bottom row) up, each 20ms after the one before, and hides them all at once.
        protected override void PopIn()
        {
            this.FadeIn();

            var rows = list.Children;

            for (int i = 0; i < rows.Count; i++)
                rows[i].Appear((rows.Count - 1 - i) * 20);
        }

        protected override void PopOut()
        {
            foreach (var row in list.Children)
                row.Disappear();

            this.Delay(100).FadeOut();
        }

        /// <summary>
        /// One of stable's mode menu items (<c>pSpriteMenuItem</c>): a dark background that turns purple while hovered, with the mode's icon
        /// and name. Hovering also springs the default icon up and slides the name right.
        /// </summary>
        private partial class ModeRow : ClickableContainer
        {
            private const float text_x = 144;

            // pSpriteMenuItem's background and highlight colours; their alpha applies on top of the 160/255 the background fades in to.
            private static readonly Color4 background_colour = new Color4(0, 0, 0, 160);
            private static readonly Color4 highlight_colour = new Color4(165, 90, 234, 230);
            private const float background_alpha = 160 / 255f;

            private readonly Box background;
            private readonly Container iconContainer;
            private readonly OsuSpriteText text;
            private readonly bool springsOnHover;

            public ModeRow(RulesetInfo info, Drawable icon, bool springsOnHover)
            {
                this.springsOnHover = springsOnHover;

                RelativeSizeAxes = Axes.X;
                Height = row_height;

                icon.Anchor = icon.Origin = Anchor.Centre;

                Children = new Drawable[]
                {
                    background = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = background_colour,
                        Alpha = 0,
                    },
                    iconContainer = new Container
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        Size = new Vector2(row_height),
                        Child = icon,
                    },
                    text = new OsuSpriteText
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        X = text_x,
                        Text = info.Name,
                        Font = LegacyFonts.Get(33, FontWeight.Bold),
                        Shadow = true,
                    },
                    new HoverSounds(),
                };
            }

            public Drawable CreateIconProxy() => iconContainer.CreateProxy();

            public Drawable CreateTextProxy() => text.CreateProxy();

            public void Appear(double delay)
            {
                iconContainer.FadeInFromZero(200);
                text.FadeInFromZero(200);
                background.FadeOut().Delay(delay).FadeTo(background_alpha, 200);
            }

            public void Disappear()
            {
                iconContainer.FadeOut(100);
                text.FadeOut(100);
                background.FadeOut(100);
            }

            protected override bool OnHover(HoverEvent e)
            {
                background.FadeColour(highlight_colour, 10);

                if (springsOnHover)
                    iconContainer.ScaleTo(1.1f, 1000, Easing.OutElastic);

                text.MoveToX(text_x + 10 * 1.6f, 300, Easing.Out);

                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                background.FadeColour(background_colour, 100);
                iconContainer.ScaleTo(1, 1000, Easing.OutElastic);
                text.MoveToX(text_x, 300, Easing.Out);

                base.OnHoverLost(e);
            }
        }
    }
}
