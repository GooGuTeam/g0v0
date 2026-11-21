// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Configuration;

namespace osu.Game.Recording.Export
{
    /// <summary>
    /// Owns export-only ruleset settings, with no database writes or live bindable links.
    /// </summary>
    public sealed class OfflineRulesetConfigCache : IRulesetConfigCache, IDisposable
    {
        private readonly Dictionary<Type, IRulesetConfigManager?> configurations = new Dictionary<Type, IRulesetConfigManager?>();
        private readonly Type rulesetType;
        private readonly IReadOnlyDictionary<string, string>? snapshot;

        public OfflineRulesetConfigCache(Type rulesetType, IReadOnlyDictionary<string, string>? snapshot)
        {
            this.rulesetType = rulesetType;
            this.snapshot = snapshot;
        }

        public IRulesetConfigManager? GetConfigFor(Ruleset ruleset)
        {
            if (ruleset.GetType() != rulesetType)
                throw new InvalidOperationException(@"No configuration snapshot exists for this ruleset.");

            if (!configurations.TryGetValue(ruleset.GetType(), out var config))
            {
                config = ruleset.CreateConfig(null);
                try
                {
                    if (snapshot != null && config is IRulesetConfigSnapshotSource target)
                        target.ApplySnapshot(snapshot);
                    else if (snapshot != null || config != null)
                        throw new InvalidOperationException(@"The ruleset configuration cannot restore its snapshot.");

                    configurations.Add(ruleset.GetType(), config);
                }
                catch
                {
                    config?.Dispose();
                    throw;
                }
            }
            return config;
        }

        public void Dispose()
        {
            foreach (var config in configurations.Values)
                (config as IDisposable)?.Dispose();
            configurations.Clear();
        }
    }
}
