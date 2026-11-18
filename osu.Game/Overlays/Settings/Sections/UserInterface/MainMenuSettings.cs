// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests.Responses;

namespace osu.Game.Overlays.Settings.Sections.UserInterface
{
    public partial class MainMenuSettings : SettingsSubsection
    {
        protected override LocalisableString Header => UserInterfaceStrings.MainMenuHeader;

        private IBindable<APIUser> user = null!;

        private readonly Bindable<SettingsNote.Data?> backgroundSourceNote = new Bindable<SettingsNote.Data?>();

        private readonly BindableInt pinnedIndex = new BindableInt
        {
            MinValue = 0,
            MaxValue = 0,
        };

        private Bindable<APISeasonalBackgrounds> seasonalBackgrounds = null!;

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config, IAPIProvider api, SessionStatics sessionStatics)
        {
            user = api.LocalUser.GetBoundCopy();
            seasonalBackgrounds = sessionStatics.GetBindable<APISeasonalBackgrounds>(Static.SeasonalBackgrounds);

            config.BindWith(OsuSetting.PinnedSeasonalBackgroundIndex, pinnedIndex);

            Children = new Drawable[]
            {
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = UserInterfaceStrings.ShowMenuTips,
                    Current = config.GetBindable<bool>(OsuSetting.MenuTips)
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = UserInterfaceStrings.InterfaceVoices,
                    Current = config.GetBindable<bool>(OsuSetting.MenuVoice)
                })
                {
                    Keywords = new[] { "intro", "welcome" },
                },
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = UserInterfaceStrings.OsuMusicTheme,
                    Current = config.GetBindable<bool>(OsuSetting.MenuMusic)
                })
                {
                    Keywords = new[] { "intro", "welcome" },
                },
                new SettingsItemV2(new FormEnumDropdown<BackgroundSource>
                {
                    Caption = UserInterfaceStrings.BackgroundSource,
                    Current = config.GetBindable<BackgroundSource>(OsuSetting.MenuBackgroundSource),
                })
                {
                    Note = { BindTarget = backgroundSourceNote },
                },
                new SettingsItemV2(new FormEnumDropdown<SeasonalBackgroundMode>
                {
                    Caption = UserInterfaceStrings.SeasonalBackgrounds,
                    Current = config.GetBindable<SeasonalBackgroundMode>(OsuSetting.SeasonalBackgroundMode),
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = UserInterfaceStrings.PinnedSeasonalBackground,
                    Current = config.GetBindable<bool>(OsuSetting.PinnedSeasonalBackground),
                }),
                new SettingsItemV2(new FormSliderBar<int>
                {
                    Caption = UserInterfaceStrings.PinnedSeasonalBackgroundIndex,
                    Current = pinnedIndex,
                    KeyboardStep = 1,
                }),
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            seasonalBackgrounds.BindValueChanged(_ => updatePinnedIndexRange(), true);
            user.BindValueChanged(u =>
            {
                backgroundSourceNote.Value = u.NewValue?.IsSupporter != true ? new SettingsNote.Data(UserInterfaceStrings.NotSupporterNote, SettingsNote.Type.Informational) : null;
            }, true);
        }

        private void updatePinnedIndexRange()
        {
            int count = seasonalBackgrounds.Value?.Backgrounds?.Count ?? 0;
            pinnedIndex.MaxValue = Math.Max(0, count - 1);

            if (pinnedIndex.Value > pinnedIndex.MaxValue)
                pinnedIndex.Value = pinnedIndex.MaxValue;
        }
    }
}
