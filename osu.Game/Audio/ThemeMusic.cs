// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Database;

namespace osu.Game.Audio
{
    public static class ThemeMusic
    {
        public const double PREVIEW_OFFSET = -600d;

        /// <summary>
        /// The beatmap set currently selected as theme music, if any.
        /// </summary>
        public static Guid? GetConfiguredBeatmapSetId(OsuConfigManager config)
        {
            string? stored = config.Get<string>(OsuSetting.ThemeMusicBeatmapSetId);

            return Guid.TryParse(stored, out Guid id) ? id : null;
        }

        /// <summary>
        /// Retrieve the custom theme music beatmap set.
        /// </summary>
        /// <param name="config">The config manager to read the designation from.</param>
        /// <param name="realm">The realm access used to look up the beatmap set.</param>
        /// <param name="isAvailable">Whether the returned beatmap set can actually be played.</param>
        /// <returns>The detached beatmap set, or <c>null</c> if none is selected or it no longer exists.</returns>
        public static BeatmapSetInfo? GetBeatmapSet(OsuConfigManager config, RealmAccess realm, out bool isAvailable)
        {
            isAvailable = false;

            if (GetConfiguredBeatmapSetId(config) is not Guid id)
                return null;

            bool available = false;

            var beatmapSet = realm.Run(r =>
            {
                BeatmapSetInfo? set = r.Find<BeatmapSetInfo>(id);

                if (set == null)
                    return null;

                // files are dropped when detaching, so the audio file has to be looked up while the set is still managed.
                string audioFile = set.Metadata.AudioFile;

                available = !set.DeletePending
                            && !set.Protected
                            && set.Beatmaps.Count > 0
                            && !string.IsNullOrEmpty(audioFile)
                            && set.GetFile(audioFile) != null;

                return set.Detach();
            });

            isAvailable = available;

            return beatmapSet;
        }

        /// <summary>
        /// Select a beatmap set as theme music and enable its usage.
        /// </summary>
        public static void SetBeatmapSet(OsuConfigManager config, BeatmapSetInfo beatmapSet)
        {
            config.SetValue(OsuSetting.ThemeMusicBeatmapSetId, beatmapSet.ID.ToString());
            config.SetValue(OsuSetting.UseCustomThemeMusic, true);
        }

        /// <summary>
        /// Clear the current theme music beatmap set settings.
        /// </summary>
        public static void Clear(OsuConfigManager config)
        {
            config.SetValue(OsuSetting.ThemeMusicBeatmapSetId, string.Empty);
        }

        /// <summary>
        /// Whether to use the exclusive offset for custom theme music.
        /// </summary>
        public static bool UseSpecificOffset(WorkingBeatmap workingBeatmap, OsuConfigManager config)
        {
            if (!config.Get<bool>(OsuSetting.UseCustomThemeMusic))
                return false;

            return GetConfiguredBeatmapSetId(config) == workingBeatmap.BeatmapSetInfo.ID;
        }
    }
}
