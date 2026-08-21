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

namespace osu.Game.Overlays.Settings.Sections.Ruleset
{
    public partial class RulesetManagementPanel : SettingsSubPanel
    {
        protected override Drawable CreateHeader()
            => new SettingsHeader(RulesetSettingsStrings.RulesetManagement, RulesetSettingsStrings.RulesetManagementSubheading);

        [BackgroundDependencyLoader]
        private void load()
        {
            AddSection(new LocalRulesetsSection());
        }
    }

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
            Padding = SettingsPanel.CONTENT_PADDING;

            Children = new Drawable[]
            {
                trustedModeCheckbox = new FormCheckBox
                {
                    Caption = RulesetSettingsStrings.TrustedMode,
                    HintText = RulesetSettingsStrings.TrustedModeTooltip,
                    Current = { BindTarget = rulesets.BlockUnseenRulesets },
                },
                new Container
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
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
                new FillFlowContainer
                {
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(5),
                    ChildrenEnumerable = rulesets.AllRulesets.Select(r => new RulesetRow(r)),
                }
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            trustedModeCheckbox.Current.BindValueChanged(_ => rulesets.SaveConfiguration());
        }
    }
}
