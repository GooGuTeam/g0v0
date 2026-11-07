// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.Overlays.Settings.Rendering;

namespace osu.Game.Overlays.Settings.Entries
{
    /// <summary>
    /// A value picked from a list of items.
    /// </summary>
    public class DropdownEntry<T> : ValueSettingsEntry<T>
        where T : notnull
    {
        /// <summary>
        /// The items which can be picked. Renderers stay bound to this list, so it can be changed at any time
        /// (directly, through <see cref="Items"/>, or by binding it to another list).
        /// </summary>
        public BindableList<T> ItemSource { get; } = new BindableList<T>();

        /// <summary>
        /// The items which can be picked. Assigning replaces the contents of <see cref="ItemSource"/>; <c>null</c> clears it.
        /// </summary>
        [AllowNull]
        public IEnumerable<T> Items
        {
            get => ItemSource;
            set => SetItems(value ?? Enumerable.Empty<T>());
        }

        /// <summary>
        /// Whether the search bar is always visible, rather than only once the user starts typing.
        /// </summary>
        public bool AlwaysShowSearchBar { get; init; }

        /// <summary>
        /// Whether the search matches items whose text contains the typed characters in order but not next to each other.
        /// </summary>
        public bool AllowNonContiguousMatching { get; init; }

        /// <summary>
        /// A custom label for each item. When <c>null</c>, the renderer's default text generation is used.
        /// </summary>
        public Func<T, LocalisableString>? ItemLabel { get; init; }

        /// <summary>
        /// Replaces all items with the given ones.
        /// </summary>
        public void SetItems(IEnumerable<T> items)
        {
            var list = items.ToList();

            // Publish one replacement notification so renderers never observe a transient empty list.
            // Rebuild even when identities are unchanged: item labels may have changed (for example a skin rename).
            ItemSource.ReplaceRange(0, ItemSource.Count, list);
        }

        /// <summary>
        /// Gets a label for the given item, for renderers which do not provide their own text generation.
        /// </summary>
        public LocalisableString GetItemLabel(T item)
        {
            if (ItemLabel != null)
                return ItemLabel(item);

            switch (item)
            {
                case Enum e:
                    return e.GetLocalisableDescription();

                default:
                    return item.ToString() ?? string.Empty;
            }
        }

        public override IEnumerable<LocalisableString> FilterTerms => base.FilterTerms.Concat(ItemSource.Select(GetItemLabel));

        public override Drawable CreateDrawable(ISettingsRenderer renderer) => renderer.CreateDropdown(this);

        public override void Unbind()
        {
            base.Unbind();
            ItemSource.UnbindAll();
        }
    }
}
