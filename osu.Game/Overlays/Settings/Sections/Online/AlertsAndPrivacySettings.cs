// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Extensions.LocalisationExtensions;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Resources.Localisation.Web;
using osu.Game.Overlays.Settings.Entries;

namespace osu.Game.Overlays.Settings.Sections.Online
{
    public partial class AlertsAndPrivacySettings : SettingsSubsection
    {
        protected override LocalisableString Header => OnlineSettingsStrings.AlertsAndPrivacyHeader;

        public override IEnumerable<LocalisableString> FilterTerms => base.FilterTerms.Concat(new LocalisableString[] { "notifications" });

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            Children = new Drawable[]
            {
                CreateItem(new CheckboxEntry
                {
                    Caption = OnlineSettingsStrings.NotifyOnMentioned,
                    Current = config.GetBindable<bool>(OsuSetting.NotifyOnUsernameMentioned)
                }),
                CreateItem(new CheckboxEntry
                {
                    Caption = OnlineSettingsStrings.NotifyOnPrivateMessage,
                    Current = config.GetBindable<bool>(OsuSetting.NotifyOnPrivateMessage)
                }),
                CreateItem(new CheckboxEntry
                {
                    Caption = OnlineSettingsStrings.NotifyOnFriendPresenceChange,
                    HintText = OnlineSettingsStrings.NotifyOnFriendPresenceChangeTooltip,
                    Current = config.GetBindable<bool>(OsuSetting.NotifyOnFriendPresenceChange),
                }),
                CreateItem(new CheckboxEntry
                {
                    Caption = AccountsStrings.PrivacyFriendsOnly.ToSentence(),
                    HintText = AccountsStrings.PrivacyFriendsOnlyInfo.ToSentence(),
                    Current = config.GetBindable<bool>(OsuSetting.PMFriendsOnly),
                }),
            };
        }
    }
}
