// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Localisation;
using osu.Game.Configuration;

namespace osu.Game.Rulesets.Edit.Native
{
    /// <summary>
    /// Marks a hit object property as editable in the <see cref="EditablePropertyInspector"/> of a <see cref="NativeHitObjectComposer{TObject,TAction}"/>.
    /// Supported property types are <see cref="bool"/>, enums, <see cref="int"/>, <see cref="float"/> and <see cref="double"/>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class EditablePropertyAttribute : Attribute
    {
        public LocalisableString Label { get; }

        /// <summary>Lower bound for numeric properties. A slider is shown when both bounds are finite, a number box otherwise.</summary>
        public double Min { get; init; } = double.NegativeInfinity;

        /// <summary>Upper bound for numeric properties.</summary>
        public double Max { get; init; } = double.PositiveInfinity;

        /// <summary>Step for slider-backed numeric properties.</summary>
        public double Precision { get; init; } = 0.01;

        /// <summary>Display order within the inspector.</summary>
        public int Order { get; init; }

        public EditablePropertyAttribute(string label)
        {
            Label = label;
        }

        public EditablePropertyAttribute(Type declaringType, string label)
        {
            Label = new SettingSourceAttribute(declaringType, label).Label;
        }
    }
}
