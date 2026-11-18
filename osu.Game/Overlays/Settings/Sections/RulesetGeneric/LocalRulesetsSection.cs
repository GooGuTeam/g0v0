// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Localisation;
using osu.Game.Overlays.Settings.Entries;
using osu.Game.Overlays.Settings.Rendering.Stable;
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

        private CheckboxEntry trustedModeCheckbox = null!;

        [BackgroundDependencyLoader]
        private void load()
        {
            AddRange(new Drawable[]
            {
                CreateItem(trustedModeCheckbox = new CheckboxEntry
                {
                    Caption = RulesetSettingsStrings.TrustedMode,
                    HintText = RulesetSettingsStrings.TrustedModeTooltip,
                    Current =
                    {
                        BindTarget = rulesets.BlockUnseenRulesets
                    },
                    Note =
                    {
                        Value = new SettingsNote.Data(RulesetSettingsStrings.LocalRulesetRestartNote, SettingsNote.Type.Informational),
                    },
                }),
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
            var flow = new FillFlowContainer
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(5),
                Padding = Renderer.UseStableLayouts
                    ? new MarginPadding { Left = StableOptionsMetrics.ELEMENT_X, Right = StableSettingsItem.CONTENT_RIGHT }
                    : SettingsPanel.CONTENT_PADDING,
                ChildrenEnumerable = rulesets.AllRulesets.Select(r => new RulesetRow(r)),
            };

            Child = Renderer.UseStableLayouts ? new StableRulesetListContainer { Child = flow } : flow;
        }
    }

    public partial class BrokenRulesetsSettings : SettingsSubsection
    {
        protected override LocalisableString Header => RulesetSettingsStrings.BrokenRulesets;

        public override IEnumerable<LocalisableString> FilterTerms => [@"broken", @"restore", @"blacklisted"];

        [BackgroundDependencyLoader]
        private void load(RulesetStore rulesets)
        {
            var flow = new FillFlowContainer
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(5),
                Padding = Renderer.UseStableLayouts
                    ? new MarginPadding { Left = StableOptionsMetrics.ELEMENT_X, Right = StableSettingsItem.CONTENT_RIGHT }
                    : SettingsPanel.CONTENT_PADDING,
                ChildrenEnumerable = rulesets.BrokenRulesetFilenames.Select(name => new BrokenRulesetRow(name)),
            };

            Children = new Drawable[]
            {
                CreateItem(new NoteEntry
                {
                    Note =
                    {
                        Value = new SettingsNote.Data(RulesetSettingsStrings.BrokenRulesetsNote, SettingsNote.Type.Warning),
                    },
                }),
                Renderer.UseStableLayouts ? new StableRulesetListContainer { Child = flow } : flow,
            };
        }
    }

    internal partial class StableRulesetListContainer : Container
    {
        private readonly Box line;

        public StableRulesetListContainer()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;

            AddInternal(line = new Box
            {
                X = StableOptionsMetrics.SECTION_LINE_X,
                Width = StableOptionsMetrics.SECTION_LINE_WIDTH,
                Colour = StableOptionsMetrics.SECTION_LINE_COLOUR,
                Blending = BlendingParameters.Additive,
                BypassAutoSizeAxes = Axes.Both,
            });
        }

        protected override void Update()
        {
            base.Update();
            line.Height = DrawHeight > 0 ? DrawHeight + SettingsSection.ITEM_SPACING_V2 : 0;
        }
    }
}
