using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Rulesets;
using osuTK;

namespace osu.Game.Overlays.Settings.Sections.RulesetGeneric
{
    public partial class RulesetRow : Container
    {
        private readonly RulesetInfo ruleset;
        private readonly Ruleset? instance;

        private FormControlBackground background = null!;
        private OsuTextFlowContainer titleFlow = null!;
        private OsuTextFlowContainer descriptionFlow = null!;
        private SwitchButton enabledSwitch = null!;

        [Resolved]
        private RulesetStore rulesets { get; set; } = null!;

        private const float spacing = 15;

        public RulesetRow(RulesetInfo ruleset)
        {
            this.ruleset = ruleset;

            try
            {
                instance = ruleset.CreateInstance();
            }
            catch (Exception)
            {
                // We believe the exception has been recorded during setup.
            }

            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colours)
        {
            string name = ruleset.Name;
            string shortName = ruleset.ShortName;

            var sourceEvents = rulesets.Events.Where(e => ruleset.Equals(e.RulesetInfo)).ToList();

            var loadEvent = sourceEvents.OfType<RulesetLoadEvent>().FirstOrDefault();
            var errorEvent = sourceEvents.OfType<RulesetErrorEvent>().FirstOrDefault();

            var assembly = loadEvent?.Assembly ?? errorEvent?.Assembly;

            string? version = assembly?.GetName().Version?.ToString();
            bool allowManageActions = loadEvent?.Source is RulesetSource.User || errorEvent != null;
            bool isDisabled = rulesets.DisabledRulesets.Contains(ruleset);

            var description = version ?? RulesetSettingsStrings.UnknownVersionPlaceholder;

            var icon = instance?.CreateIcon()
                       ?? new SpriteIcon
                       {
                           Anchor = Anchor.Centre,
                           Origin = Anchor.Centre,
                           Icon = FontAwesome.Regular.QuestionCircle,
                       };

            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;

            InternalChildren = new Drawable[]
            {
                background = new FormControlBackground(),
                new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(spacing),
                    Padding = new MarginPadding
                    {
                        Vertical = 5,
                        Left = 9,
                        Right = 5,
                    },
                    Children = new Drawable[]
                    {
                        new GridContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            ColumnDimensions = new[]
                            {
                                new Dimension(GridSizeMode.AutoSize),
                                new Dimension(),
                                new Dimension(GridSizeMode.AutoSize),
                            },
                            RowDimensions = new[]
                            {
                                new Dimension(GridSizeMode.AutoSize),
                            },
                            Content = new[]
                            {
                                new Drawable?[]
                                {
                                    new ConstrainedIconContainer
                                    {
                                        Anchor = Anchor.CentreLeft,
                                        Origin = Anchor.CentreLeft,
                                        Icon = icon,
                                        Size = new Vector2(32),
                                        Margin = new MarginPadding
                                        {
                                            Vertical = 4,
                                            Right = 8,
                                        },
                                    },
                                    new FillFlowContainer
                                    {
                                        Anchor = Anchor.CentreLeft,
                                        Origin = Anchor.CentreLeft,
                                        RelativeSizeAxes = Axes.X,
                                        AutoSizeAxes = Axes.Y,
                                        Direction = FillDirection.Vertical,
                                        Spacing = new Vector2(4),
                                        Children = new Drawable[]
                                        {
                                            titleFlow = new OsuTextFlowContainer
                                            {
                                                Anchor = Anchor.CentreLeft,
                                                Origin = Anchor.CentreLeft,
                                                RelativeSizeAxes = Axes.X,
                                                AutoSizeAxes = Axes.Y,
                                            },
                                            descriptionFlow = new OsuTextFlowContainer
                                            {
                                                Anchor = Anchor.CentreLeft,
                                                Origin = Anchor.CentreLeft,
                                            },
                                        },
                                    },
                                    new FillFlowContainer
                                    {
                                        Anchor = Anchor.CentreRight,
                                        Origin = Anchor.CentreRight,
                                        RelativeSizeAxes = Axes.Y,
                                        AutoSizeAxes = Axes.X,
                                        Direction = FillDirection.Horizontal,
                                        Spacing = new Vector2(5),
                                        Margin = new MarginPadding
                                        {
                                            Left = 5,
                                        },
                                        Children = new Drawable[]
                                        {
                                            new IconButton
                                            {
                                                Anchor = Anchor.CentreRight,
                                                Origin = Anchor.CentreRight,
                                                Icon = FontAwesome.Solid.Folder,
                                                Alpha = allowManageActions ? 1 : 0,
                                                Action = () => rulesets.PresentRulesetExternally(ruleset),
                                                TooltipText = RulesetSettingsStrings.ShowAssemblyInFileManager,
                                            },
                                            enabledSwitch = new SwitchButton
                                            {
                                                Name = @"Enabled switch",
                                                Anchor = Anchor.CentreRight,
                                                Origin = Anchor.CentreRight,
                                                Width = 32,
                                                Alpha = allowManageActions ? 1 : 0,
                                                Current =
                                                {
                                                    Value = !isDisabled,
                                                    Disabled = !allowManageActions,
                                                },
                                            },
                                        },
                                    },
                                },
                            },
                        },
                    },
                },
            };

            if (loadEvent?.Source is RulesetSource.Builtin)
            {
                titleFlow.AddText(RulesetSettingsStrings.BuiltinPrefix, t => t.Colour = colours.Colour0);
                titleFlow.AddText(@" ");
            }

            titleFlow.AddText($"{name} ", t => t.Font = OsuFont.Style.Heading2);
            titleFlow.AddText($"[{shortName}]", t =>
            {
                t.Font = OsuFont.Style.Caption1;
                t.Colour = colours.Colour0;
            });

            descriptionFlow.AddText(description, t => t.Font = OsuFont.Style.Caption1);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            enabledSwitch.Current.BindValueChanged(e => rulesets.SetRulesetEnabled(ruleset, e.NewValue));
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
