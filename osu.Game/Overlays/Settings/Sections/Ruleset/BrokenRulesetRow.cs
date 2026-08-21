using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Rulesets;

namespace osu.Game.Overlays.Settings.Sections.Ruleset
{
    public partial class BrokenRulesetRow : Container
    {
        private readonly string filename;

        private FormControlBackground background = null!;

        [Resolved]
        private RulesetStore rulesets { get; set; } = null!;

        public BrokenRulesetRow(string filename)
        {
            this.filename = filename;

            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;

            InternalChildren = new Drawable[]
            {
                background = new FormControlBackground(),
                new GridContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Padding = new MarginPadding
                    {
                        Vertical = 5,
                        Horizontal = 9,
                    },
                    ColumnDimensions = new[]
                    {
                        new Dimension(GridSizeMode.AutoSize),
                        new Dimension(),
                    },
                    RowDimensions = new[]
                    {
                        new Dimension(GridSizeMode.AutoSize),
                    },
                    Content = new[]
                    {
                        new Drawable[]
                        {
                            new OsuSpriteText
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                Font = OsuFont.Style.Caption1,
                                Text = filename,
                            },
                            new IconButton
                            {
                                Anchor = Anchor.CentreRight,
                                Origin = Anchor.CentreRight,
                                Icon = FontAwesome.Solid.Redo,
                                Action = removeAndExpire,
                                TooltipText = RulesetSettingsStrings.Restore,
                            },
                        },
                    },
                },
            };
        }

        private void removeAndExpire()
        {
            rulesets.TryRestoreBrokenRuleset(filename);
            this.FadeOut(300, Easing.OutQuint).Then().Expire();
        }

        protected override bool OnHover(HoverEvent e)
        {
            updateState();
            return base.OnHover(e);
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            base.OnHoverLost(e);
            updateState();
        }

        private void updateState()
            => background.VisualStyle = IsHovered ? VisualStyle.Hovered : VisualStyle.Normal;
    }
}
