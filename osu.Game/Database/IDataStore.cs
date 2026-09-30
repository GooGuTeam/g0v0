// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.Database
{
    /// <summary>
    /// Root of the backend-neutral data storage abstraction.
    /// Aggregates all per-model stores and exposes cross-store units of work.
    /// </summary>
    /// <remarks>
    /// This is the seam for swapping the storage backend (currently Realm, planned SQLite).
    /// Implementations: one wrapping <see cref="RealmAccess"/>, one backed by SQLite.
    /// Model types referenced by the child stores are currently still Realm objects;
    /// decoupling the models themselves from Realm is a separate follow-up step.
    /// </remarks>
    public interface IDataStore : IDisposable
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

        /// <summary>
        /// Hash-addressed file blob storage backing models implementing <see cref="IHasRealmFiles"/>.
        /// </summary>
        IFileStore Files { get; }

        /// <summary>
        /// Run <paramref name="action"/> inside a single write transaction spanning all stores.
        /// Nested calls join the ambient transaction.
        /// </summary>
        void Write(Action action);

        /// <summary>
        /// Run <paramref name="action"/> inside a single write transaction spanning all stores
        /// and return its result.
        /// </summary>
        TResult Write<TResult>(Func<TResult> action);
    }
}
