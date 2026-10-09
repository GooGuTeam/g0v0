// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Cursor;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests.Responses;

namespace osu.Game.Users.Drawables
{
    internal partial class ClickableUsername : OsuHoverContainer, IHasCustomTooltip<APIUser>
    {
        public ITooltip<APIUser?> GetCustomTooltip() => new ClickableAvatar.NoCardTooltip();

        public APIUser? TooltipContent { get; }

        private readonly APIUser user;

        [Resolved]
        private OsuGame? game { get; set; }

        public ClickableUsername(APIUser? user, bool useV2Style = false)
        {
            TooltipContent = this.user = user ?? new GuestUser();

            AutoSizeAxes = Axes.Both;

            Child = new OsuSpriteText
            {
                Text = this.user.Username,
                Font = OsuFont.Default.With(size: 16, weight: FontWeight.SemiBold),
                Shadow = !useV2Style,
            };

            if (this.user.Id != APIUser.SYSTEM_USER_ID)
                Action = openProfile;
        }

        private void openProfile()
        {
            if (user.Id > 1 || !string.IsNullOrEmpty(user.Username))
                game?.ShowUser(user);
        }
    }
}
