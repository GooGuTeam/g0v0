// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;

namespace osu.Game.Overlays.Settings.Sections.Torii
{
    /// <summary>
    /// Appearance options for the song select interface.
    /// </summary>
    public partial class ToriiSongSelectSettings : SettingsSubsection
    {
        protected override LocalisableString Header => UserInterfaceStrings.SongSelectAppearanceHeader;

        // The stable toggle (which also brings the bundled legacy footer with it). When it's on the legacy
        // footer is mandatory, so the toggle below is forced on and disabled.
        // They're assigned in load() straight from the config bindable (no new + BindTo to a temporary):
        // osu!framework bindings are weak-ref, so an unheld temporary bound copy gets eaten by the GC and
        // the toggle then stopped reacting (it stayed greyed out even after turning stable off).
        private Bindable<bool> stableSongSelect = null!;

        // The user's REAL preference for the standalone legacy footer (what gets saved to config).
        private Bindable<bool> footerPref = null!;

        // What the checkbox shows: mirrors the preference while stable is off, or stays checked+disabled
        // while stable is on. The writeback to footerPref only persists REAL user clicks.
        private readonly BindableBool footerDisplay = new BindableBool();

        // Suppress flag (same pattern as PotatoModeToggleAndRestart / ToriiAuraSettings): when syncDisplay()
        // writes footerDisplay.Value programmatically the change event fires SYNCHRONOUSLY. Without this
        // flag the writeback below would run while Disabled is still false (we set it afterwards) and
        // clobber footerPref. With the flag we only save user clicks.
        private bool syncingDisplay;

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            stableSongSelect = config.GetBindable<bool>(OsuSetting.ToriiLegacyFooterUseSkin);
            footerPref = config.GetBindable<bool>(OsuSetting.ToriiLegacySongSelectFooter);

            Children = new Drawable[]
            {
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = UserInterfaceStrings.LegacySongSelect,
                    Current = config.GetBindable<bool>(OsuSetting.ToriiLegacyFooterUseSkin),
                    HintText = UserInterfaceStrings.LegacySongSelectDescription,
                })
                {
                    Keywords = new[] { @"footer", @"skin", @"song", @"select", @"legacy", @"bottom", @"buttons", @"torii", @"stable" },
                },
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = UserInterfaceStrings.UseInterFont,
                    Current = config.GetBindable<bool>(OsuSetting.ToriiLegacyFont),
                    HintText = UserInterfaceStrings.UseInterFontDescription,
                })
                {
                    Keywords = new[] { @"font", @"inter", @"typeface", @"stable", @"legacy", @"song", @"select", @"torii" },
                },
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = UserInterfaceStrings.LegacySongSelectFooter,
                    Current = footerDisplay,
                    HintText = UserInterfaceStrings.LegacySongSelectFooterDescription,
                })
                {
                    Keywords = new[] { @"footer", @"legacy", @"stable", @"bottom", @"buttons", @"song", @"select", @"torii" },
                },
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = UserInterfaceStrings.UnslantedSongSelectUI,
                    HintText = UserInterfaceStrings.UnslantedSongSelectUIDescription,
                    Current = config.GetBindable<bool>(OsuSetting.UnslantedSongSelectUI),
                })
                {
                    Keywords = new[] { @"slant", @"unslant", @"vertical", @"straight", @"shear", @"song", @"select", @"torii" },
                },
            };

            stableSongSelect.BindValueChanged(_ => syncDisplay(), true);
            footerPref.BindValueChanged(_ => syncDisplay());

            // Writeback: only persist when the change came from a real user click (not during
            // syncDisplay, and only possible while the checkbox isn't disabled).
            footerDisplay.BindValueChanged(e =>
            {
                if (!syncingDisplay)
                    footerPref.Value = e.NewValue;
            });
        }

        private void syncDisplay()
        {
            syncingDisplay = true;

            bool stableOn = stableSongSelect.Value;

            // Clear Disabled before touching Value so we never write to a blocked bindable.
            footerDisplay.Disabled = false;
            footerDisplay.Value = stableOn || footerPref.Value;
            // With stable on the footer is mandatory: checked and locked.
            footerDisplay.Disabled = stableOn;

            syncingDisplay = false;
        }
    }
}
