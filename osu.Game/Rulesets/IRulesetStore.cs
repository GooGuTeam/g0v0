// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;

namespace osu.Game.Rulesets
{
    public interface IRulesetStore
    {
        /// <summary>
        /// Retrieve a ruleset using a known ID.
        /// </summary>
        /// <param name="id">The ruleset's internal ID.</param>
        /// <returns>A ruleset, if available, else null.</returns>
        IRulesetInfo? GetRuleset(int id);

        /// <summary>
        /// Retrieve a ruleset using a known short name.
        /// </summary>
        /// <param name="shortName">The ruleset's short name.</param>
        /// <returns>A ruleset, if available, else null.</returns>
        IRulesetInfo? GetRuleset(string shortName);

        /// <summary>
        /// All available rulesets.
        /// </summary>
        IEnumerable<IRulesetInfo> AvailableRulesets { get; }

        /// <summary>
        /// A chronological list of ruleset loading events.
        /// </summary>
        IEnumerable<RulesetEvent> Events { get; }

        /// <summary>
        /// Invoked when a ruleset is successfully loaded.
        /// </summary>
        event Action<RulesetLoadEvent>? OnLoaded;

        /// <summary>
        /// Invoked when a ruleset fails to load.
        /// </summary>
        event Action<RulesetErrorEvent>? OnError;
    }
}
