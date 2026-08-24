using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Rulesets;
using osuTK;

namespace osu.Game.Overlays.Settings.Sections.RulesetGeneric
{
    public partial class LocalRulesetsSection : SettingsSection
    {
        public override Drawable CreateIcon() => new SpriteIcon
        {
            Icon = OsuIcon.Rulesets,
        };

        public override LocalisableString Header => RulesetSettingsStrings.LocalRulesets;

        [Resolved]
        private RulesetStore rulesets { get; set; } = null!;

        private FormCheckBox trustedModeCheckbox = null!;

        [BackgroundDependencyLoader]
        private void load()
        {
            AddRange(new Drawable[]
            {
                new SettingsItemV2(trustedModeCheckbox = new FormCheckBox
                {
                    Caption = RulesetSettingsStrings.TrustedMode,
                    HintText = RulesetSettingsStrings.TrustedModeTooltip,
                    Current =
                    {
                        BindTarget = rulesets.BlockUnseenRulesets
                    },
                }),
                new Container
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Padding = SettingsPanel.CONTENT_PADDING,
                    Children = new Drawable[]
                    {
                        new SettingsNote
                        {
                            RelativeSizeAxes = Axes.X,
                            Margin = new MarginPadding
                            {
                                Top = -ITEM_SPACING_V2,
                                Bottom = ITEM_SPACING_V2,
                            },
                            Current =
                            {
                                Value = new SettingsNote.Data(RulesetSettingsStrings.LocalRulesetRestartNote, SettingsNote.Type.Informational),
                            },
                        },
                    },
                },
                new LoadedRulesetsSettings(),
                new BrokenRulesetsSettings(),
            });
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            trustedModeCheckbox.Current.BindValueChanged(_ => rulesets.SaveConfiguration());
        }
    }

    public partial class LoadedRulesetsSettings : SettingsSubsection
    {
        protected override LocalisableString Header => RulesetSettingsStrings.LoadedRulesets;

        public override IEnumerable<LocalisableString> FilterTerms => [@"active", @"inactive", @"enable", @"disable"];

        [BackgroundDependencyLoader]
        private void load(RulesetStore rulesets)
        {
            Child = new FillFlowContainer
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(5),
                Padding = SettingsPanel.CONTENT_PADDING,
                ChildrenEnumerable = rulesets.AllRulesets.Select(r => new RulesetRow(r)),
            };
        }
    }

    public partial class BrokenRulesetsSettings : SettingsSubsection
    {
        protected override LocalisableString Header => RulesetSettingsStrings.BrokenRulesets;

        public override IEnumerable<LocalisableString> FilterTerms => [@"broken", @"restore", @"blacklisted"];

        [BackgroundDependencyLoader]
        private void load(RulesetStore rulesets)
        {
            Children = new Drawable[]
            {
                new Container
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Padding = SettingsPanel.CONTENT_PADDING,
                    Children = new Drawable[]
                    {
                        new SettingsNote
                        {
                            RelativeSizeAxes = Axes.X,
                            Current =
                            {
                                Value = new SettingsNote.Data(RulesetSettingsStrings.BrokenRulesetsNote, SettingsNote.Type.Warning),
                            },
                        },
                    },
                },
                new FillFlowContainer
                {
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(5),
                    Padding = SettingsPanel.CONTENT_PADDING,
                    ChildrenEnumerable = rulesets.BrokenRulesetFilenames.Select(name => new BrokenRulesetRow(name)),
                }
            };
        }
    }
}
