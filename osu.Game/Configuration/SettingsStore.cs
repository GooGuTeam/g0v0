// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

using osu.Game.Database;

namespace osu.Game.Configuration
{
    public class SettingsStore
    {
        // this class mostly exists as a wrapper to avoid breaking the ruleset API (see usage in RulesetConfigManager).
        // it may cease to exist going forward, depending on how the structure of the config data layer changes.

        public readonly RealmAccess Realm;

        /// <summary>
        /// The backend-selected per-ruleset setting store.
        /// </summary>
        public readonly IRulesetSettingStore Settings;

        public SettingsStore(RealmAccess realm, IRulesetSettingStore settings)
        {
            Realm = realm;
            Settings = settings;
        }
    }
}
