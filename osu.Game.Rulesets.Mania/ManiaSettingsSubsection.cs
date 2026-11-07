// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.Mania.Configuration;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Overlays.Settings.Entries;

namespace osu.Game.Rulesets.Mania
{
    public partial class ManiaSettingsSubsection : RulesetSettingsSubsection
    {
        public ManiaSettingsSubsection(ManiaRuleset ruleset)
            : base(ruleset)
        {
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            var config = (ManiaRulesetConfigManager)Config;

            Children = new Drawable[]
            {
                CreateItem(new EnumDropdownEntry<ManiaScrollingDirection>
                {
                    Caption = RulesetSettingsStrings.ScrollingDirection,
                    Current = config.GetBindable<ManiaScrollingDirection>(ManiaRulesetSetting.ScrollDirection)
                }),
                CreateItem(new SliderEntry<double>
                {
                    Caption = RulesetSettingsStrings.ScrollSpeed,
                    Current = config.GetBindable<double>(ManiaRulesetSetting.ScrollSpeed),
                    KeyboardStep = 1,
                    LabelFormat = v => RulesetSettingsStrings.ScrollSpeedTooltip((int)DrawableManiaRuleset.ComputeScrollTime(v), v),
                }),
                CreateItem(new CheckboxEntry
                {
                    Caption = RulesetSettingsStrings.TimingBasedColouring,
                    Current = config.GetBindable<bool>(ManiaRulesetSetting.TimingBasedNoteColouring),
                    Keywords = new[] { "color" },
                }),
            };

            Add(CreateItem(new CheckboxEntry
            {
                Caption = RulesetSettingsStrings.TouchOverlay,
                Current = config.GetBindable<bool>(ManiaRulesetSetting.TouchOverlay)
            }));

            if (RuntimeInfo.IsMobile)
            {
                Add(CreateItem(new EnumDropdownEntry<ManiaMobileLayout>
                {
                    Caption = RulesetSettingsStrings.MobileLayout,
                    Current = config.GetBindable<ManiaMobileLayout>(ManiaRulesetSetting.MobileLayout),
#pragma warning disable CS0618 // Type or member is obsolete
                    Items = Enum.GetValues<ManiaMobileLayout>().Where(l => l != ManiaMobileLayout.LandscapeWithOverlay),
#pragma warning restore CS0618 // Type or member is obsolete
                }));
            }
        }
    }
}
