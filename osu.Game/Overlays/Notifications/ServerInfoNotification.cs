// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Extensions.LocalisationExtensions;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Localisation;
using osu.Game.Online;

namespace osu.Game.Overlays.Notifications
{
    /// <summary>
    /// A notification showing the current server information on startup.
    /// </summary>
    public partial class ServerInfoNotification : SimpleNotification
    {
        private readonly string serverUrl;

        public ServerInfoNotification(string serverUrl)
        {
            this.serverUrl = serverUrl;
        }

        [BackgroundDependencyLoader]
        private void load(OsuColour colours)
        {
            Icon = FontAwesome.Solid.Server;
            IconContent.Colour = colours.BlueLight;

            TextFlow.AddText(OnlineSettingsStrings.ServerInformation.ToUpper(), s =>
            {
                s.Font = OsuFont.Style.Caption2.With(weight: FontWeight.Bold);
                s.Colour = colours.BlueLight;
            });
            TextFlow.AddParagraph(getServerDisplayText(serverUrl));
        }

        private static LocalisableString getServerDisplayText(string serverUrl)
        {
            if (string.IsNullOrEmpty(serverUrl))
                return OnlineSettingsStrings.ConnectedToDefaultServer;

            var displayName = extractDisplayName(serverUrl);
            return OnlineSettingsStrings.CurrentServer(displayName);
        }

        private static LocalisableString extractDisplayName(string url)
        {
            if (string.IsNullOrEmpty(url))
                return OnlineSettingsStrings.DefaultServer.ToString();

            try
            {
                string cleanUrl = url.Replace(@"https://", "").Replace(@"http://", "");

                int pathIndex = cleanUrl.IndexOf('/');
                if (pathIndex > 0)
                    cleanUrl = cleanUrl[..pathIndex];

                cleanUrl = cleanUrl.ToLowerInvariant();

                return OfficialServer.IsOfficialHost(cleanUrl) ? OnlineSettingsStrings.OfficialServer : cleanUrl;
            }
            catch
            {
                return url;
            }
        }
    }
}
