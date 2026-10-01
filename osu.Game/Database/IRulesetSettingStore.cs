// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Database
{
    /// <summary>
    /// Storage for per-ruleset settings, keyed by (ruleset, variant, key).
    /// Exposed as plain key-value pairs rather than a model type, since these
    /// settings have no identity of their own.
    /// </summary>
    public interface IRulesetSettingStore
    {
        /// <summary>
        /// Retrieve a setting value.
        /// </summary>
        /// <param name="rulesetName">Short name of the ruleset.</param>
        /// <param name="variant">Ruleset variant; 0 is the default variant.</param>
        /// <param name="key">The setting key.</param>
        /// <returns>The stored value, or null if not set.</returns>
        string? GetValue(string rulesetName, int variant, string key);

        /// <summary>
        /// Store a setting value, replacing any existing value for the same key.
        /// </summary>
        void SetValue(string rulesetName, int variant, string key, string value);

        /// <summary>
        /// Remove a setting.
        /// </summary>
        /// <returns>false if the setting was not present.</returns>
        bool Delete(string rulesetName, int variant, string key);
    }
}
