// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Input.Bindings;
using osu.Framework.Localisation;
using osu.Game.Database;
using osu.Game.Input.Bindings;
using osu.Game.Localisation;
using osu.Game.Overlays.Settings.Entries;
using osuTK;
using Realms;

namespace osu.Game.Overlays.Settings.Sections.Input
{
    public abstract partial class KeyBindingsSubsection : SettingsSubsection
    {
        private ButtonEntry resetEntry = null!;

        /// <summary>
        /// After a successful binding, automatically select the next binding row to make quickly
        /// binding a large set of keys easier on the user.
        /// </summary>
        protected virtual bool AutoAdvanceTarget => false;

        protected IEnumerable<KeyBinding> Defaults { get; init; } = Array.Empty<KeyBinding>();

        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        protected KeyBindingsSubsection()
        {
            FlowContent.Spacing = new Vector2(0, 3);
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            var bindings = getAllBindings();

            resetEntry = new ButtonEntry
            {
                Caption = InputSettingsStrings.ResetSectionButton,
                Dangerous = true,
                Action = resetAllBindings,
            };

            // stable rows are ordinary option elements, spaced like every other subsection.
            if (Renderer.UseStableLayouts)
                FlowContent.Spacing = new Vector2(0, SettingsSection.ITEM_SPACING_V2);

            foreach (var defaultGroup in Defaults.GroupBy(d => d.Action))
            {
                int intKey = (int)defaultGroup.Key;

                var row = CreateKeyBindingRow(defaultGroup.Key, defaultGroup)
                    .With(row =>
                    {
                        row.BindingUpdated = onBindingUpdated;
                        row.GetAllSectionBindings = getAllBindings;
                    });

                row.KeyBindings.AddRange(bindings.Where(b => b.ActionInt.Equals(intKey)));

                Add(row);

                row.IsDefault.BindValueChanged(_ => updateDefaultButtonState());
            }

            Add(Renderer.CreateKeyBindingResetButton(resetEntry));

            updateDefaultButtonState();
        }

        private void resetAllBindings()
        {
            realm.Write(r =>
            {
                // can't use `RestoreDefaults()` for each key binding row here as it might trigger binding conflicts along the way.
                foreach (var row in Children.OfType<KeyBindingRow>())
                {
                    foreach (var (currentBinding, defaultBinding) in row.KeyBindings.Zip(row.Defaults))
                        r.Find<RealmKeyBinding>(currentBinding.ID)!.KeyCombinationString = defaultBinding.ToString();
                }
            });
            reloadAllBindings();
        }

        private void updateDefaultButtonState()
        {
            resetEntry.Enabled.Value = !Children.OfType<KeyBindingRow>().All(r => r.IsDefault.Value);
        }

        protected abstract IEnumerable<RealmKeyBinding> GetKeyBindings(Realm realm);

        private List<RealmKeyBinding> getAllBindings() => realm.Run(r =>
        {
            r.Refresh();
            return GetKeyBindings(r).Detach();
        });

        protected virtual KeyBindingRow CreateKeyBindingRow(object action, IEnumerable<KeyBinding> defaults)
            => new KeyBindingRow(action)
            {
                AllowMainMouseButtons = false,
                Defaults = defaults.Select(d => d.KeyCombination),
            };

        private void reloadAllBindings()
        {
            var bindings = getAllBindings();

            foreach (var row in Children.OfType<KeyBindingRow>())
            {
                row.KeyBindings.Clear();
                row.KeyBindings.AddRange(bindings.Where(b => b.ActionInt.Equals((int)row.Action)));
            }
        }

        private void onBindingUpdated(KeyBindingRow sender, KeyBindingRow.KeyBindingUpdatedEventArgs args)
        {
            if (args.BindingConflictResolved)
                reloadAllBindings();

            if (AutoAdvanceTarget && args.CanAdvanceToNextBinding)
            {
                var next = Children.SkipWhile(c => c != sender).Skip(1).FirstOrDefault();
                if (next != null)
                    GetContainingFocusManager()?.ChangeFocus(next);
            }
        }
    }

    public partial class ResetButton : DangerousSettingsButtonV2
    {
        [BackgroundDependencyLoader]
        private void load()
        {
            Text = InputSettingsStrings.ResetSectionButton;
            RelativeSizeAxes = Axes.X;
            Anchor = Anchor.TopCentre;
            Origin = Anchor.TopCentre;
            Margin = new MarginPadding { Top = 5 };
            Height = 40;

            Content.CornerRadius = 5;
        }

        // Empty FilterTerms so that the ResetButton is visible only when the whole subsection is visible.
        public override IEnumerable<LocalisableString> FilterTerms => Enumerable.Empty<LocalisableString>();
    }
}
