// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Mods;

namespace osu.Game.Database
{
    /// <summary>
    /// Storage for user mod presets.
    /// </summary>
    public interface IModPresetStore : ISoftDeletableStore<ModPreset>
    {
    }
}
