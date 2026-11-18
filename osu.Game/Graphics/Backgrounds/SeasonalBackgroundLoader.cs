// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

#nullable disable

using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Textures;
using osu.Framework.Utils;
using osu.Game.Configuration;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;

namespace osu.Game.Graphics.Backgrounds
{
    public partial class SeasonalBackgroundLoader : Component
    {
        /// <summary>
        /// Fired when background should be changed due to receiving backgrounds from API
        /// or when the user setting is changed (as it might require unloading the seasonal background).
        /// </summary>
        public event Action SeasonalBackgroundChanged;

        [Resolved]
        private IAPIProvider api { get; set; }

        private Bindable<SeasonalBackgroundMode> seasonalBackgroundMode;
        private Bindable<APISeasonalBackgrounds> seasonalBackgrounds;

        private int current;

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config, SessionStatics sessionStatics)
        {
            seasonalBackgroundMode = config.GetBindable<SeasonalBackgroundMode>(OsuSetting.SeasonalBackgroundMode);
            seasonalBackgroundMode.BindValueChanged(_ => SeasonalBackgroundChanged?.Invoke());

            seasonalBackgrounds = sessionStatics.GetBindable<APISeasonalBackgrounds>(Static.SeasonalBackgrounds);
            seasonalBackgrounds.BindValueChanged(_ =>
            {
                if (shouldShowSeasonal)
                    SeasonalBackgroundChanged?.Invoke();
            });

            fetchSeasonalBackgrounds();
        }

        private void fetchSeasonalBackgrounds()
        {
            if (seasonalBackgrounds.Value != null)
                return;

            var request = new GetSeasonalBackgroundsRequest();
            request.Success += response =>
            {
                seasonalBackgrounds.Value = response;
                current = RNG.Next(0, response.Backgrounds?.Count ?? 0);
            };

            api.PerformAsync(request);
        }

        public SeasonalBackground LoadNextBackground()
        {
            if (!shouldShowSeasonal)
                return null;

            var backgrounds = seasonalBackgrounds.Value.Backgrounds;

            current = (current + 1) % backgrounds.Count;
            string url = backgrounds[current].Url;

            return new SeasonalBackground(url);
        }

        /// <summary>
        /// The number of seasonal backgrounds currently available from the API, or zero if none have been fetched yet.
        /// </summary>
        public int AvailableCount => seasonalBackgrounds.Value?.Backgrounds?.Count ?? 0;

        /// <summary>
        /// Retrieve the image URL of the seasonal background at the given index, if available.
        /// </summary>
        public string GetUrl(int index)
        {
            var backgrounds = seasonalBackgrounds.Value?.Backgrounds;

            if (backgrounds == null || index < 0 || index >= backgrounds.Count)
                return null;

            return backgrounds[index].Url;
        }

        /// <summary>
        /// Load the seasonal background pinned to the given index, bypassing the user's seasonal background mode setting.
        /// </summary>
        /// <remarks>
        /// This method returns null when the index is invalid for the background list, in which case the caller
        /// should fall back to the regular background source chain.
        /// </remarks>
        public SeasonalBackground LoadPinnedBackground(int index)
        {
            string url = GetUrl(index);

            return url == null ? null : new SeasonalBackground(url);
        }

        private bool shouldShowSeasonal => seasonalBackgroundMode.Value switch
        {
            SeasonalBackgroundMode.Never => false,
            SeasonalBackgroundMode.Sometimes when !isInSeason => false,
            _ => seasonalBackgrounds.Value?.Backgrounds?.Any() == true,
        };

        private bool isInSeason => seasonalBackgrounds.Value != null && DateTimeOffset.Now < seasonalBackgrounds.Value.EndDate;
    }

    [LongRunningLoad]
    public partial class SeasonalBackground : Background
    {
        private const string fallback_texture_name = @"Backgrounds/bg1";

        /// <summary>
        /// The remote URL this background's texture is loaded from.
        /// </summary>
        public string Url { get; }

        public SeasonalBackground(string url)
        {
            Url = url;
        }

        [BackgroundDependencyLoader]
        private void load(LargeTextureStore textures)
        {
            Sprite.Texture = textures.Get(Url) ?? textures.Get(fallback_texture_name);
        }

        public override bool Equals(Background other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;

            return other.GetType() == GetType() && ((SeasonalBackground)other).Url == Url;
        }
    }
}
