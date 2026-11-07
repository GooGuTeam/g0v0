// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Overlays.Settings.Entries;
using osu.Game.Overlays.Settings.Rendering;

namespace osu.Game.Overlays.Settings.Sections.Torii
{
    /// <summary>
    /// Appearance options for the song select interface.
    /// </summary>
    public partial class ToriiSongSelectSettings : SettingsSubsection
    {
        protected override LocalisableString Header => UserInterfaceStrings.SongSelectAppearanceHeader;

        // el toggle stable (que ademas trae el footer legacy bundleado). cuando esta prendido, el footer
        // legacy va si o si, asi que el toggle de abajo queda forzado prendido y deshabilitado.
        // se asignan en load() con el bindable del config DIRECTO (no new + BindTo a un temporal): las
        // bindings de osu!framework son weak-ref y un bound-copy temporal sin guardar se lo come el GC,
        // y ahi el toggle dejaba de reaccionar (quedaba grisado aunque apagaras el stable).
        private Bindable<bool> stableSongSelect = null!;

        // la preferencia REAL del usuario para el footer legacy standalone (lo que se guarda en config).
        private Bindable<bool> footerPref = null!;
        private Bindable<bool> modSelectPref = null!;
        private readonly BindableBool modSelectDisplay = new BindableBool();

        private CheckboxEntry legacyOptions = null!;

        // lo que ve el checkbox: refleja la pref cuando stable esta apagado, o queda checked+disabled
        // cuando stable esta prendido. el writeback a footerPref solo persiste clicks REALES del usuario.
        private readonly BindableBool footerDisplay = new BindableBool();

        // suppress flag (mismo patron que PotatoModeToggleAndRestart / ToriiAuraSettings): cuando
        // syncDisplay() escribe footerDisplay.Value programaticamente, el evento de cambio se dispara
        // SINCRONO. sin esta bandera, el writeback de abajo correria con Disabled todavia en false
        // (lo seteamos despues) y pisaria footerPref. con la bandera solo guardamos clicks del usuario.
        private bool syncingDisplay;

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            stableSongSelect = config.GetBindable<bool>(OsuSetting.ToriiLegacyFooterUseSkin);
            footerPref = config.GetBindable<bool>(OsuSetting.ToriiLegacySongSelectFooter);
            modSelectPref = config.GetBindable<bool>(OsuSetting.ToriiLegacyModSelect);

            Children = new Drawable[]
            {
                CreateItem(new CheckboxEntry
                {
                    Caption = UserInterfaceStrings.LegacyMainMenu,
                    Current = config.GetBindable<bool>(OsuSetting.ToriiLegacyMainMenu),
                    HintText = UserInterfaceStrings.LegacyMainMenuDescription,
                    Keywords = new[] { @"main", @"menu", @"legacy", @"stable", @"torii", @"cookie", @"buttons" },
                }),
                CreateItem(new CheckboxEntry
                {
                    Caption = UserInterfaceStrings.LegacySongSelect,
                    Current = config.GetBindable<bool>(OsuSetting.ToriiLegacyFooterUseSkin),
                    HintText = UserInterfaceStrings.LegacySongSelectDescription,
                    Keywords = new[] { @"footer", @"skin", @"song", @"select", @"legacy", @"bottom", @"buttons", @"torii", @"stable" },
                }),
                CreateItem(new EnumDropdownEntry<LegacySongSelectAspectRatio>
                {
                    Caption = UserInterfaceStrings.LegacySongSelectAspectRatio,
                    Current = config.GetBindable<LegacySongSelectAspectRatio>(OsuSetting.ToriiLegacySongSelectAspectRatio),
                    HintText = UserInterfaceStrings.LegacySongSelectAspectRatioDescription,
                    Keywords = new[] { @"skin", @"aspect", @"ratio", @"16:9", @"16:10", @"letterbox", @"gameplay", @"loading", @"results", @"torii" },
                }),
                CreateItem(new CheckboxEntry
                {
                    Caption = UserInterfaceStrings.LegacyModSelect,
                    Current = modSelectDisplay,
                    HintText = UserInterfaceStrings.LegacyModSelectDescription,
                    Keywords = new[] { @"mod", @"mods", @"legacy", @"stable", @"torii", @"selection", @"dialog" },
                }),
                CreateItem(legacyOptions = new CheckboxEntry
                {
                    Caption = UserInterfaceStrings.LegacyOptions,
                    Current = config.GetBindable<bool>(OsuSetting.ToriiLegacyOptions),
                    HintText = UserInterfaceStrings.LegacyOptionsDescription,
                    Keywords = new[] { @"options", @"settings", @"legacy", @"stable", @"sidebar", @"dialog", @"torii" },
                }),
                CreateItem(new CheckboxEntry
                {
                    Caption = UserInterfaceStrings.LegacyFpsCounter,
                    Current = config.GetBindable<bool>(OsuSetting.ToriiLegacyFpsCounter),
                    HintText = UserInterfaceStrings.LegacyFpsCounterDescription,
                    Keywords = new[] { @"fps", @"frame", @"rate", @"counter", @"legacy", @"stable", @"torii" },
                }),
                CreateItem(new CheckboxEntry
                {
                    Caption = UserInterfaceStrings.LegacyGameplayScreens,
                    Current = config.GetBindable<bool>(OsuSetting.ToriiLegacyGameplayScreens),
                    HintText = UserInterfaceStrings.LegacyGameplayScreensDescription,
                    Keywords = new[] { @"pause", @"fail", @"failed", @"loading", @"loader", @"gameplay", @"legacy", @"stable", @"torii" },
                }),
                CreateItem(new CheckboxEntry
                {
                    Caption = UserInterfaceStrings.UseInterFont,
                    Current = config.GetBindable<bool>(OsuSetting.ToriiLegacyFont),
                    HintText = UserInterfaceStrings.UseInterFontDescription,
                    Keywords = new[] { @"font", @"inter", @"typeface", @"stable", @"legacy", @"song", @"select", @"torii" },
                }),
                CreateItem(new CheckboxEntry
                {
                    Caption = UserInterfaceStrings.LegacySongSelectFooter,
                    Current = footerDisplay,
                    HintText = UserInterfaceStrings.LegacySongSelectFooterDescription,
                    Keywords = new[] { @"footer", @"legacy", @"stable", @"bottom", @"buttons", @"song", @"select", @"torii" },
                }),
                CreateItem(new CheckboxEntry
                {
                    Caption = UserInterfaceStrings.UnslantedSongSelectUI,
                    HintText = UserInterfaceStrings.UnslantedSongSelectUIDescription,
                    Current = config.GetBindable<bool>(OsuSetting.UnslantedSongSelectUI),
                    Keywords = new[] { @"slant", @"unslant", @"vertical", @"straight", @"shear", @"song", @"select", @"torii" },
                }),
            };

            stableSongSelect.BindValueChanged(_ => syncDisplay(), true);
            footerPref.BindValueChanged(_ => syncDisplay());
            modSelectPref.BindValueChanged(_ => syncDisplay());
            modSelectDisplay.BindValueChanged(e =>
            {
                if (!syncingDisplay)
                    modSelectPref.Value = e.NewValue;
            });

            // the settings panels pick their style once, when the game starts.
            bool stableOptionsShown = Renderer is StableSettingsRenderer;
            legacyOptions.Current.BindValueChanged(e => legacyOptions.Note.Value = e.NewValue != stableOptionsShown
                ? new SettingsNote.Data(UserInterfaceStrings.LegacyOptionsRestartRequired, SettingsNote.Type.Informational)
                : null, true);

            // writeback: solo persistimos cuando el cambio vino de un click real del usuario
            // (no durante syncDisplay, y solo posible cuando el checkbox no esta disabled).
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

            // limpiamos Disabled antes de tocar Value asi nunca escribimos sobre un bindable bloqueado.
            footerDisplay.Disabled = false;
            footerDisplay.Value = stableOn || footerPref.Value;
            // con stable prendido el footer va si o si: checked y bloqueado.
            footerDisplay.Disabled = stableOn;

            modSelectDisplay.Disabled = false;
            modSelectDisplay.Value = stableOn || modSelectPref.Value;
            modSelectDisplay.Disabled = stableOn;

            syncingDisplay = false;
        }
    }
}
