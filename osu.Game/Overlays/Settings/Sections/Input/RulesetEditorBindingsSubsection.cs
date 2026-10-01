// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Input.Bindings;
using osu.Framework.Localisation;
using osu.Game.Database;
using osu.Game.Input.Bindings;
using osu.Game.Rulesets;

namespace osu.Game.Overlays.Settings.Sections.Input
{
    public partial class RulesetEditorBindingsSubsection : KeyBindingsSubsection
    {
        protected override LocalisableString Header { get; }

        public RulesetInfo Ruleset { get; }

        private const int variant = Rulesets.Ruleset.EDITOR_VARIANT;

        public RulesetEditorBindingsSubsection(RulesetInfo ruleset)
        {
            Ruleset = ruleset;

            var rulesetInstance = ruleset.CreateInstance();

            Header = ruleset.Name;
            Defaults = rulesetInstance.GetDefaultKeyBindings(variant);
        }

        protected override IEnumerable<RealmKeyBinding> GetKeyBindings(IKeyBindingStore store)
        {
            string rulesetName = Ruleset.ShortName;

            return store.GetAllDetached()
                        .Where(b => b.RulesetName == rulesetName && b.Variant == variant);
        }

        protected override KeyBindingRow CreateKeyBindingRow(object action, IEnumerable<KeyBinding> defaults)
            => new KeyBindingRow(action)
            {
                AllowMainMouseButtons = true,
                Defaults = defaults.Select(d => d.KeyCombination),
            };
    }
}
