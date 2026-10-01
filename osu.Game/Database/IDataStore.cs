// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Database
{
    /// <summary>
    /// Root of the backend-neutral data storage abstraction.
    /// Aggregates the per-domain stores; each store operation is self-contained and atomic.
    /// </summary>
    /// <remarks>
    /// This is the seam for swapping the storage backend (currently Realm, planned SQLite).
    /// Intentionally offers no cross-store unit of work: callers that need to touch multiple
    /// domains perform separate atomic operations per store.
    /// Model types referenced by the child stores are currently still Realm objects;
    /// decoupling the models themselves from Realm is a separate follow-up step.
    /// </remarks>
    public interface IDataStore
    {
        /// <summary>
        /// Beatmap sets and their contained beatmaps.
        /// </summary>
        IBeatmapStore Beatmaps { get; }

        /// <summary>
        /// Locally stored scores.
        /// </summary>
        IScoreStore Scores { get; }

        /// <summary>
        /// Locally stored skins.
        /// </summary>
        ISkinStore Skins { get; }

        /// <summary>
        /// User beatmap collections.
        /// </summary>
        IBeatmapCollectionStore Collections { get; }

        /// <summary>
        /// Key bindings (global and per-ruleset).
        /// </summary>
        IKeyBindingStore KeyBindings { get; }

        /// <summary>
        /// Per-ruleset settings (key-value).
        /// </summary>
        IRulesetSettingStore RulesetSettings { get; }

        /// <summary>
        /// User mod presets.
        /// </summary>
        IModPresetStore ModPresets { get; }
    }
}
