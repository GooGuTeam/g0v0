// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Overlays.Settings.Entries;

namespace osu.Game.Overlays.Settings.Sections.UserInterface
{
    public partial class MainMenuSettings : SettingsSubsection
    {
        protected override LocalisableString Header => UserInterfaceStrings.MainMenuHeader;

        private IBindable<APIUser> user = null!;

        private readonly Bindable<SettingsNote.Data?> backgroundSourceNote = new Bindable<SettingsNote.Data?>();

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config, IAPIProvider api)
        {
            user = api.LocalUser.GetBoundCopy();

            Children = new Drawable[]
            {
                CreateItem(new CheckboxEntry
                {
                    Caption = UserInterfaceStrings.ShowMenuTips,
                    Current = config.GetBindable<bool>(OsuSetting.MenuTips)
                }),
                CreateItem(new CheckboxEntry
                {
                    Caption = UserInterfaceStrings.InterfaceVoices,
                    Current = config.GetBindable<bool>(OsuSetting.MenuVoice),
                    Keywords = new[] { "intro", "welcome" },
                }),
                CreateItem(new CheckboxEntry
                {
                    Caption = UserInterfaceStrings.OsuMusicTheme,
                    Current = config.GetBindable<bool>(OsuSetting.MenuMusic),
                    Keywords = new[] { "intro", "welcome" },
                }),
                CreateItem(new EnumDropdownEntry<BackgroundSource>
                {
                    Caption = UserInterfaceStrings.BackgroundSource,
                    Current = config.GetBindable<BackgroundSource>(OsuSetting.MenuBackgroundSource),
                    Note = { BindTarget = backgroundSourceNote },
                }),
                CreateItem(new EnumDropdownEntry<SeasonalBackgroundMode>
                {
                    Caption = UserInterfaceStrings.SeasonalBackgrounds,
                    Current = config.GetBindable<SeasonalBackgroundMode>(OsuSetting.SeasonalBackgroundMode),
                })
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            user.BindValueChanged(u =>
            {
                if (u.NewValue?.IsSupporter != true)
                    backgroundSourceNote.Value = new SettingsNote.Data(UserInterfaceStrings.NotSupporterNote, SettingsNote.Type.Informational);
                else
                    backgroundSourceNote.Value = null;
            }, true);
        }
    }
}
