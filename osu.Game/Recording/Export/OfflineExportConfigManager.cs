// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Globalization;
using osu.Framework.Bindables;
using osu.Game.Configuration;

namespace osu.Game.Recording.Export
{
    /// <summary>
    /// A detached snapshot that cannot modify the running client's settings file.
    /// </summary>
    public sealed class OfflineExportConfigManager : OsuConfigManager
    {
        protected override string Filename => string.Empty;

        public OfflineExportConfigManager(IDictionary<OsuSetting, string> values)
            : base(null!)
        {
            foreach (var pair in values)
            {
                if (ConfigStore.TryGetValue(pair.Key, out var bindable) && bindable is IParseable parseable)
                    parseable.Parse(pair.Value, CultureInfo.InvariantCulture);
            }
            SetValue(OsuSetting.GameplayLeaderboard, false);
        }
    }
}
