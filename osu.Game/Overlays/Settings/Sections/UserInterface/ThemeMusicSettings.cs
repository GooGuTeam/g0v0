// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Overlays.Dialog;

namespace osu.Game.Overlays.Settings.Sections.UserInterface
{
    public partial class ThemeMusicSettings : SettingsSubsection
    {
        protected override LocalisableString Header => ThemeMusicStrings.ThemeMusicHeader;

        private readonly Bindable<SettingsNote.Data?> note = new Bindable<SettingsNote.Data?>();

        private Bindable<string> beatmapSetId = null!;

        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        [Resolved]
        private IDialogOverlay? dialogOverlay { get; set; }

        [BackgroundDependencyLoader]
        private void load()
        {
            beatmapSetId = config.GetBindable<string>(OsuSetting.ThemeMusicBeatmapSetId);

            Children = new Drawable[]
            {
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = ThemeMusicStrings.UseCustomThemeMusic,
                    Current = config.GetBindable<bool>(OsuSetting.UseCustomThemeMusic),
                    HintText = ThemeMusicStrings.UseCustomThemeMusicDescription,
                })
                {
                    Keywords = [@"theme", @"music", @"startup", @"intro", @"menu", @"beatmap"],
                },
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = ThemeMusicStrings.StartFromPreviewPoint,
                    Current = config.GetBindable<bool>(OsuSetting.ThemeMusicStartFromPreview),
                    HintText = ThemeMusicStrings.StartFromPreviewPointDescription,
                })
                {
                    Keywords = [@"theme", @"music", @"preview", @"startup", @"intro"],
                    Note = { BindTarget = note },
                },
                new SettingsButtonV2
                {
                    Text = ThemeMusicStrings.ResetThemeMusic,
                    Action = () => dialogOverlay?.Push(new ConfirmDialog(ThemeMusicStrings.ResetThemeMusic, () => ThemeMusic.Clear(config))
                    {
                        BodyText = ThemeMusicStrings.ResetThemeMusicConfirmation,
                    }),
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            beatmapSetId.BindValueChanged(_ => updateNote(), true);
        }

        private void updateNote()
        {
            if (ThemeMusic.GetConfiguredBeatmapSetId(config) == null)
            {
                note.Value = new SettingsNote.Data(ThemeMusicStrings.NoThemeMusic, SettingsNote.Type.Informational);
                return;
            }

            var beatmapSet = ThemeMusic.GetBeatmapSet(config, realm, out bool isAvailable);

            if (beatmapSet == null)
            {
                note.Value = new SettingsNote.Data(ThemeMusicStrings.ThemeMusicNotFound, SettingsNote.Type.Warning);
                return;
            }

            string title = beatmapSet.Metadata.GetDisplayTitle();

            note.Value = isAvailable
                ? new SettingsNote.Data(ThemeMusicStrings.CurrentThemeMusic(title), SettingsNote.Type.Informational)
                : new SettingsNote.Data(ThemeMusicStrings.ThemeMusicUnavailable(title), SettingsNote.Type.Warning);
        }
    }
}
