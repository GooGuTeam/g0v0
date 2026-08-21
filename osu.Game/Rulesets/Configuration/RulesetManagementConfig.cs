using System;
using System.Collections.Generic;
using osu.Framework.Bindables;

namespace osu.Game.Rulesets.Configuration
{
    [Serializable]
    public class RulesetManagementConfig
    {
        /// <summary>
        /// Newly added rulesets won't be applied to the game, unless the user explicitly enable them.
        /// </summary>
        public BindableBool BlockUnseenRulesets = new BindableBool();

        /// <summary>
        /// Formed enabled and loaded rulesets (trusted).
        /// </summary>
        public List<RulesetInfo> KnownRulesets { get; } = [];

        /// <summary>
        /// Rulesets disabled by the user.
        /// </summary>
        public List<RulesetInfo> DisabledRulesets { get; } = [];

        /// <summary>
        /// Filenames of broken rulesets assemblies.
        /// </summary>
        public List<string> BrokenRulesetFilenames { get; } = [];

        /// <summary>
        /// Is the specified ruleset trusted, i.e. formly enabled by the user (or <see cref="BlockUnseenRulesets"/> is disabled).
        /// </summary>
        /// <param name="ruleset"></param>
        /// <returns></returns>
        public bool IsTrusted(RulesetInfo ruleset) => !BlockUnseenRulesets.Value || KnownRulesets.Contains(ruleset);

        /// <summary>
        /// Should the specified ruleset be loaded into the game.
        /// </summary>
        public bool ShouldBeLoaded(RulesetInfo ruleset) => IsTrusted(ruleset) && !DisabledRulesets.Contains(ruleset);
    }
}
