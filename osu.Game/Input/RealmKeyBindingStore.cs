// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Input;
using osu.Framework.Input.Bindings;
using osu.Framework.Localisation;
using osu.Game.Database;
using osu.Game.Input.Bindings;
using osu.Game.Localisation;
using osu.Game.Rulesets;

namespace osu.Game.Input
{
    public class RealmKeyBindingStore
    {
        private readonly ReadableKeyCombinationProvider keyCombinationProvider;
        private readonly DataStoreSelector dataStoreSelector;

        /// <summary>
        /// The backend-selected raw key binding store. Owned by <see cref="dataStoreSelector"/>.
        /// </summary>
        public IKeyBindingStore BackingStore => dataStoreSelector.GetKeyBindingStore();

        /// <summary>
        /// Creates a store over the provided backend selector.
        /// </summary>
        public RealmKeyBindingStore(DataStoreSelector dataStoreSelector, ReadableKeyCombinationProvider keyCombinationProvider)
        {
            this.dataStoreSelector = dataStoreSelector;
            this.keyCombinationProvider = keyCombinationProvider;
        }

        /// <summary>
        /// Creates a store using the given realm as backend.
        /// </summary>
        public RealmKeyBindingStore(RealmAccess realm, ReadableKeyCombinationProvider keyCombinationProvider)
            : this(new DataStoreSelector(realm, null, DataStoreBackend.Realm), keyCombinationProvider)
        {
        }

        /// <summary>
        /// For a given <see cref="GlobalAction"/>, return a human-readable string representing the bindings bound to the action.
        /// </summary>
        public LocalisableString GetBindingsStringFor(GlobalAction globalAction)
        {
            var combinations = GetReadableKeyCombinationsFor(globalAction);

            if (combinations.Count == 0)
                return ToastStrings.NoKeyBound;

            return string.Join(" / ", combinations);
        }

        /// <summary>
        /// Retrieve all user-defined key combinations (in a format that can be displayed) for a specific action.
        /// </summary>
        /// <param name="globalAction">The action to lookup.</param>
        /// <returns>A set of display strings for all the user's key configuration for the action.</returns>
        public IReadOnlyList<string> GetReadableKeyCombinationsFor(GlobalAction globalAction)
        {
            List<string> combinations = new List<string>();

            foreach (var action in BackingStore.GetAllDetached().Where(b => string.IsNullOrEmpty(b.RulesetName) && (GlobalAction)b.ActionInt == globalAction))
            {
                string str = keyCombinationProvider.GetReadableString(action.KeyCombination);

                // even if found, the readable string may be empty for an unbound action.
                if (str.Length > 0)
                    combinations.Add(str);
            }

            return combinations;
        }

        /// <summary>
        /// Retrieve all user-defined key combinations (in a format that can be displayed) for a specific ruleset action.
        /// </summary>
        /// <param name="ruleset">The <see cref="RulesetInfo.ShortName"/> of the ruleset.</param>
        /// <param name="variant">The ID of the key binding variant to look up.</param>
        /// <param name="action">The ID of the specific action to look up.</param>
        /// <returns></returns>
        public IReadOnlyList<string> GetReadableKeyCombinationsFor(string ruleset, int variant, int action)
        {
            List<string> combinations = new List<string>();

            foreach (var binding in BackingStore.GetAllDetached().Where(b => b.RulesetName == ruleset && b.Variant == variant && b.ActionInt == action))
            {
                string str = keyCombinationProvider.GetReadableString(binding.KeyCombination);

                // even if found, the readable string may be empty for an unbound action.
                if (str.Length > 0)
                    combinations.Add(str);
            }

            return combinations;
        }

        /// <summary>
        /// Register all defaults for this store.
        /// </summary>
        /// <param name="container">The container to populate defaults from.</param>
        /// <param name="rulesets">The rulesets to populate defaults from.</param>
        public void Register(KeyBindingContainer container, IEnumerable<RulesetInfo> rulesets)
        {
            // intentionally flattened to a list rather than querying per binding, as the lookup is much cheaper this way.
            var existingBindings = BackingStore.GetAllDetached();

            insertDefaults(BackingStore, existingBindings, container.DefaultKeyBindings);

            foreach (var ruleset in rulesets)
            {
                var instance = ruleset.CreateInstance();

                foreach (int variant in instance.AllVariants)
                    insertDefaults(BackingStore, existingBindings, instance.GetDefaultKeyBindings(variant), ruleset.ShortName, variant);
            }
        }

        private static void insertDefaults(IKeyBindingStore store, List<RealmKeyBinding> existingBindings, IEnumerable<IKeyBinding> defaults, string? rulesetName = null, int? variant = null)
        {
            // compare counts in database vs defaults for each action type.
            foreach (var defaultsForAction in defaults.GroupBy(k => k.Action))
            {
                var existing = existingBindings.Where(k =>
                    k.RulesetName == rulesetName
                    && k.Variant == variant
                    && k.ActionInt == (int)defaultsForAction.Key).ToArray();

                int defaultsCount = defaultsForAction.Count();
                int existingCount = existing.Length;

                if (defaultsCount > existingCount)
                {
                    // insert any defaults which are missing.
                    foreach (var newBinding in defaultsForAction.Skip(existingCount))
                        store.Add(new RealmKeyBinding(newBinding.Action, newBinding.KeyCombination, rulesetName, variant));
                }
                else if (defaultsCount < existingCount)
                {
                    // generally this shouldn't happen, but if the user has more key bindings for an action than we expect,
                    // remove the last entries until the count matches for sanity.
                    foreach (var k in existing.TakeLast(existingCount - defaultsCount))
                    {
                        store.Delete(k.ID);

                        // Remove from the local flattened/cached list so future lookups don't see now deleted rows.
                        existingBindings.Remove(k);
                    }
                }
            }
        }

        /// <summary>
        /// Keys which should not be allowed for gameplay input purposes.
        /// </summary>
        private static readonly IEnumerable<InputKey> banned_keys = new[]
        {
            InputKey.MouseWheelDown,
            InputKey.MouseWheelLeft,
            InputKey.MouseWheelUp,
            InputKey.MouseWheelRight
        };

        public static bool CheckValidForGameplay(KeyCombination combination)
        {
            foreach (var key in banned_keys)
            {
                if (combination.Keys.Contains(key))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Clears all <see cref="RealmKeyBinding.KeyCombination"/>s from the provided <paramref name="keyBindings"/>
        /// which are assigned to more than one binding.
        /// </summary>
        /// <param name="keyBindings">The <see cref="RealmKeyBinding"/>s to de-duplicate.</param>
        /// <returns>Number of bindings cleared.</returns>
        public static int ClearDuplicateBindings(IEnumerable<IKeyBinding> keyBindings)
        {
            int countRemoved = 0;

            var lookup = keyBindings.ToLookup(kb => kb.KeyCombination);

            foreach (var group in lookup)
            {
                if (group.Select(kb => kb.Action).Distinct().Count() <= 1)
                    continue;

                foreach (var binding in group)
                    binding.KeyCombination = new KeyCombination(InputKey.None);

                countRemoved += group.Count();
            }

            return countRemoved;
        }
    }
}
