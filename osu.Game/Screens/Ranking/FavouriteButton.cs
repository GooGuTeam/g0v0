// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Drawables.Cards;
using osu.Game.Graphics;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.Sprites;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;
using osu.Game.Resources.Localisation.Web;
using osuTK;

namespace osu.Game.Screens.Ranking
{
    public partial class FavouriteButton : GrayButton
    {
        public readonly BeatmapSetInfo BeatmapSetInfo;

        /// <summary>
        /// Shows the server-provided favourite count beside the heart in the overview header.
        /// </summary>
        public bool ShowCount { get; init; }

        private TruncatingSpriteText? countText;
        private APIBeatmapSet? beatmapSet;
        private readonly Bindable<BeatmapSetFavouriteState> current;

        private PostBeatmapFavouriteRequest? favouriteRequest;
        private LoadingLayer loading = null!;

        private readonly IBindable<APIUser> localUser = new Bindable<APIUser>();

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        [Resolved]
        private INotificationOverlay? notifications { get; set; }

        public FavouriteButton(BeatmapSetInfo beatmapSetInfo)
            : base(FontAwesome.Regular.Heart)
        {
            BeatmapSetInfo = beatmapSetInfo;
            current = new BindableWithCurrent<BeatmapSetFavouriteState>(new BeatmapSetFavouriteState(false, 0));

            Size = new Vector2(75, 30);
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            if (ShowCount)
            {
                Icon.Anchor = Icon.Origin = Anchor.CentreLeft;
                Icon.X = 8;
                Icon.Size = new Vector2(16);
                Icon.Shear = -Shear;
                Add(countText = new TruncatingSpriteText
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    X = 29,
                    MaxWidth = Width - 34,
                    Font = OsuFont.Default.With(size: 14, weight: FontWeight.SemiBold),
                    Text = "—",
                    Shadow = false,
                    Shear = -Shear,
                });
            }

            Add(loading = new LoadingLayer(true, false));

            Action = toggleFavouriteStatus;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            current.BindValueChanged(_ => updateState(), true);

            localUser.BindTo(api.LocalUser);
            localUser.BindValueChanged(_ => updateUser(), true);
        }

        private void getBeatmapSet()
        {
            GetBeatmapSetRequest beatmapSetRequest = new GetBeatmapSetRequest(BeatmapSetInfo.OnlineID);

            loading.Show();
            beatmapSetRequest.Success += beatmapSet =>
            {
                this.beatmapSet = beatmapSet;
                current.Value = new BeatmapSetFavouriteState(this.beatmapSet.HasFavourited, this.beatmapSet.FavouriteCount);

                loading.Hide();
                Enabled.Value = true;
            };
            beatmapSetRequest.Failure += e =>
            {
                Logger.Log($"Favourite button failed to fetch beatmap info: {e}", LoggingTarget.Network);

                Schedule(() =>
                {
                    loading.Hide();
                    Enabled.Value = false;
                    TooltipText = "this beatmap cannot be favourited";
                });
            };
            api.Queue(beatmapSetRequest);
        }

        private void toggleFavouriteStatus()
        {
            if (beatmapSet == null)
                return;

            Enabled.Value = false;
            loading.Show();

            var actionType = current.Value.Favourited ? BeatmapFavouriteAction.UnFavourite : BeatmapFavouriteAction.Favourite;

            favouriteRequest?.Cancel();
            favouriteRequest = new PostBeatmapFavouriteRequest(beatmapSet.OnlineID, actionType);

            favouriteRequest.Success += () =>
            {
                bool favourited = actionType == BeatmapFavouriteAction.Favourite;

                current.Value = new BeatmapSetFavouriteState(favourited, current.Value.FavouriteCount + (favourited ? 1 : -1));

                Enabled.Value = true;
                loading.Hide();
                api.LocalUserState.UpdateFavouriteBeatmapSets();
            };
            favouriteRequest.Failure += e =>
            {
                notifications?.Post(new SimpleNotification
                {
                    Text = e.Message,
                    Icon = FontAwesome.Solid.Times,
                });

                Schedule(() =>
                {
                    Enabled.Value = true;
                    loading.Hide();
                });
            };

            api.Queue(favouriteRequest);
        }

        private void updateUser()
        {
            if (!(localUser.Value is GuestUser) && BeatmapSetInfo.OnlineID > 0)
                getBeatmapSet();
            else
            {
                Enabled.Value = false;
                current.Value = new BeatmapSetFavouriteState(false, 0);
                updateState();
                TooltipText = BeatmapsetsStrings.ShowDetailsFavouriteLogin;
            }
        }

        private void updateState()
        {
            countText?.Text = beatmapSet != null ? current.Value.FavouriteCount.ToString("N0") : "—";

            if (current.Value.Favourited)
            {
                Background.Colour = colours.Green;
                Icon.Icon = FontAwesome.Solid.Heart;
                TooltipText = BeatmapsetsStrings.ShowDetailsUnfavourite;
            }
            else
            {
                Background.Colour = ShowCount ? Colour4.FromHex("#293A35") : colours.Gray4;
                Icon.Icon = FontAwesome.Regular.Heart;
                TooltipText = BeatmapsetsStrings.ShowDetailsFavourite;
            }
        }
    }
}
