// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Utils;

namespace osu.Game.Rulesets.Mods
{
    public static class DefaultModPresetHelper
    {
        public static Dictionary<string, Guid> ParseMap(string? raw)
        {
            var result = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

            if (string.IsNullOrWhiteSpace(raw))
                return result;

            try
            {
                var parsed = JsonConvert.DeserializeObject<Dictionary<string, string>>(raw);
                if (parsed == null)
                    return result;

                foreach (var (key, value) in parsed)
                {
                    if (!string.IsNullOrWhiteSpace(key) && Guid.TryParse(value, out Guid id))
                        result[key] = id;
                }
            }
            catch
            {
                // Ignore malformed config payloads and treat as empty.
            }

            return result;
        }

        public static Guid? GetPresetId(OsuConfigManager? config, RulesetInfo? ruleset)
        {
            if (config == null || ruleset == null || string.IsNullOrEmpty(ruleset.ShortName))
                return null;

            var map = ParseMap(config.Get<string>(OsuSetting.DefaultModPresets));
            return map.TryGetValue(ruleset.ShortName, out Guid id) ? id : null;
        }

        public static void SetPresetId(OsuConfigManager config, RulesetInfo ruleset, Guid? presetId)
        {
            if (string.IsNullOrEmpty(ruleset.ShortName))
                return;

            var map = ParseMap(config.Get<string>(OsuSetting.DefaultModPresets));

            if (presetId.HasValue)
                map[ruleset.ShortName] = presetId.Value;
            else
                map.Remove(ruleset.ShortName);

            string serialised = map.Count == 0
                ? string.Empty
                : JsonConvert.SerializeObject(map.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToString(), StringComparer.OrdinalIgnoreCase));

            config.SetValue(OsuSetting.DefaultModPresets, serialised);
        }

        public static IReadOnlyList<Mod>? GetDefaultMods(OsuConfigManager? config, RealmAccess? realm, RulesetInfo? ruleset, Func<Mod, bool>? isValidMod = null)
        {
            if (config == null || realm == null || ruleset == null || !config.Get<bool>(OsuSetting.UseDefaultModPresets))
                return null;

            Guid? id = GetPresetId(config, ruleset);
            if (id == null)
                return null;

            List<Mod>? mods = realm.Run(r =>
            {
                var preset = r.Find<ModPreset>(id.Value);
                if (preset == null || preset.DeletePending)
                    return null;

                return preset.Mods.Select(m => m.DeepClone()).ToList();
            });

            if (mods == null)
                return null;

            mods.RemoveAll(m => !m.UserPlayable || (isValidMod != null && !isValidMod(m)));

            if (!ModUtils.CheckValidForGameplay(mods, out var invalid))
                invalid.ForEach(m => mods.Remove(m));

            if (!ModUtils.CheckCompatibleSet(mods, out var incompatible))
                mods = mods.Except(incompatible).ToList();

            return mods;
        }

        public static bool MatchesMods(IReadOnlyList<Mod> current, IReadOnlyList<Mod> target)
        {
            if (current.Count != target.Count)
                return false;

            return current.All(c => target.Any(t => t.Equals(c)))
                   && target.All(t => current.Any(c => c.Equals(t)));
        }

        public static IReadOnlyList<Mod> ComputeResetMods(IReadOnlyList<Mod> currentMods, OsuConfigManager? config, RealmAccess? realm, RulesetInfo? ruleset, Func<Mod, bool>? isValidMod = null)
        {
            var presetMods = GetDefaultMods(config, realm, ruleset, isValidMod);

            if (presetMods != null && presetMods.Count > 0 && !MatchesMods(currentMods, presetMods))
                return presetMods;

            return Array.Empty<Mod>();
        }
    }
}
