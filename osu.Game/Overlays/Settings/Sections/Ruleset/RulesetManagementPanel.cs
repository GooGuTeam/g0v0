using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Rulesets;
using osuTK;

namespace osu.Game.Overlays.Settings.Sections.Ruleset
{
    public partial class RulesetManagementPanel : SettingsSubPanel
    {
        protected override Drawable CreateHeader() => new SettingsHeader("Ruleset management", "Manage loaded rulesets!");

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

        public override LocalisableString Header => "Local Rulesets";

        [BackgroundDependencyLoader(permitNulls: true)]
        private void load(RulesetStore rulesets)
        {
            Children = new Drawable[]
            {
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
    }
}
