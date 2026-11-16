// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Rulesets.Objects;

namespace osu.Game.Rulesets.Edit.Native
{
    /// <summary>
    /// A kind of object that a <see cref="NativeHitObjectComposer{TObject,TAction}"/> can place.
    /// Each template becomes one composition tool.
    /// </summary>
    public class NativeObjectTemplate<TObject>
        where TObject : HitObject
    {
        public LocalisableString Name { get; }

        public IconUsage Icon { get; }

        /// <summary>
        /// Creates a fresh object with type-specific defaults (e.g. a hold's default duration).
        /// Time and position are applied by the composer afterwards.
        /// </summary>
        public Func<TObject> Create { get; }

        public NativeObjectTemplate(LocalisableString name, IconUsage icon, Func<TObject> create)
        {
            Name = name;
            Icon = icon;
            Create = create;
        }
    }
}
