// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Localisation;
using osu.Game.Overlays.Settings.Entries;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using Realms;

namespace osu.Game.Overlays.Settings.Sections.Gameplay
{
    public partial class ModsSettings : SettingsSubsection
    {
        protected override LocalisableString Header => GameplaySettingsStrings.ModsHeader;

        public override IEnumerable<LocalisableString> FilterTerms => base.FilterTerms.Concat(new LocalisableString[] { "mod" });

        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        [Resolved]
        private RulesetStore rulesets { get; set; } = null!;

        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        private readonly Bindable<SettingsNote.Data?> noPresetsNote = new Bindable<SettingsNote.Data?>();

        private readonly List<(RulesetInfo Ruleset, DropdownEntry<ModPresetChoice> Entry, Bindable<ModPresetChoice> Bindable)> rulesetDropdowns =
            new List<(RulesetInfo, DropdownEntry<ModPresetChoice>, Bindable<ModPresetChoice>)>();

        private Bindable<bool> useDefaultPresets = null!;
        private Bindable<string> defaultPresetsConfig = null!;
        private IDisposable? realmSubscription;
        private bool updatingDropdowns;

        [BackgroundDependencyLoader]
        private void load()
        {
            useDefaultPresets = config.GetBindable<bool>(OsuSetting.UseDefaultModPresets);
            defaultPresetsConfig = config.GetBindable<string>(OsuSetting.DefaultModPresets);

            var initialSnapshots = realm.Run(r => snapshotPresets(r.All<ModPreset>().Where(p => !p.DeletePending)));

            noPresetsNote.Value = initialSnapshots.Count == 0
                ? new SettingsNote.Data(GameplaySettingsStrings.NoModPresetsHint, SettingsNote.Type.Informational)
                : null;

            var children = new List<Drawable>
            {
                CreateItem(new CheckboxEntry
                {
                    Caption = GameplaySettingsStrings.IncreaseFirstObjectVisibility,
                    Current = config.GetBindable<bool>(OsuSetting.IncreaseFirstObjectVisibility),
                    Keywords = new[] { @"approach", @"circle", @"hidden" },
                }),
                CreateItem(new CheckboxEntry
                {
                    Caption = GameplaySettingsStrings.ApplyDefaultModPresets,
                    Current = useDefaultPresets,
                    HintText = GameplaySettingsStrings.ApplyDefaultModPresetsDescription,
                    Keywords = new[] { @"preset", @"default", @"startup", @"sv1", @"scorev1", @"classic" },
                    Note = { BindTarget = noPresetsNote },
                }),
            };

            foreach (var ruleset in rulesets.AvailableRulesets)
            {
                var choices = buildChoices(initialSnapshots, ruleset);
                Guid? configuredId = DefaultModPresetHelper.GetPresetId(config, ruleset);
                ModPresetChoice selected = configuredId.HasValue
                    ? choices.FirstOrDefault(c => c.ID == configuredId.Value, ModPresetChoice.NONE)
                    : ModPresetChoice.NONE;

                var bindable = new Bindable<ModPresetChoice>(selected)
                {
                    Default = ModPresetChoice.NONE,
                };

                var entry = new DropdownEntry<ModPresetChoice>
                {
                    Caption = GameplaySettingsStrings.DefaultModPresetForRuleset(ruleset.Name),
                    Current = bindable,
                    Items = choices,
                    ItemLabel = c => c.Label,
                    Keywords = new[] { @"preset", @"default", @"startup", @"sv1", @"scorev1", @"classic", ruleset.ShortName },
                };

                var capturedRuleset = ruleset;
                bindable.BindValueChanged(e =>
                {
                    if (updatingDropdowns)
                        return;

                    DefaultModPresetHelper.SetPresetId(config, capturedRuleset, e.NewValue.ID);
                });

                rulesetDropdowns.Add((ruleset, entry, bindable));
                children.Add(CreateItem(entry));
            }

            Children = children;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            useDefaultPresets.BindValueChanged(_ => updateDropdownDisabledState(), true);
            defaultPresetsConfig.BindValueChanged(_ => syncDropdownSelectionsFromConfig());

            realmSubscription = realm.RegisterForNotifications(r => r.All<ModPreset>().Where(p => !p.DeletePending), presetsChanged);
        }

        private void presetsChanged(IRealmCollection<ModPreset> sender, ChangeSet? changes)
        {
            var snapshots = snapshotPresets(sender);
            Schedule(() => applySnapshots(snapshots));
        }

        private void applySnapshots(List<PresetSnapshot> snapshots)
        {
            updatingDropdowns = true;

            try
            {
                noPresetsNote.Value = snapshots.Count == 0
                    ? new SettingsNote.Data(GameplaySettingsStrings.NoModPresetsHint, SettingsNote.Type.Informational)
                    : null;

                foreach (var (ruleset, entry, bindable) in rulesetDropdowns)
                {
                    var choices = buildChoices(snapshots, ruleset);
                    Guid? configuredId = DefaultModPresetHelper.GetPresetId(config, ruleset);
                    ModPresetChoice target = configuredId.HasValue
                        ? choices.FirstOrDefault(c => c.ID == configuredId.Value, ModPresetChoice.NONE)
                        : ModPresetChoice.NONE;

                    bindable.Disabled = false;
                    entry.SetItems(choices);
                    bindable.Value = target;
                    bindable.Disabled = !useDefaultPresets.Value;
                }
            }
            finally
            {
                updatingDropdowns = false;
            }
        }

        private void syncDropdownSelectionsFromConfig()
        {
            if (updatingDropdowns)
                return;

            updatingDropdowns = true;

            try
            {
                foreach (var (ruleset, entry, bindable) in rulesetDropdowns)
                {
                    Guid? configuredId = DefaultModPresetHelper.GetPresetId(config, ruleset);
                    ModPresetChoice target = configuredId.HasValue
                        ? entry.ItemSource.FirstOrDefault(c => c.ID == configuredId.Value, ModPresetChoice.NONE)
                        : ModPresetChoice.NONE;

                    bindable.Disabled = false;
                    bindable.Value = target;
                    bindable.Disabled = !useDefaultPresets.Value;
                }
            }
            finally
            {
                updatingDropdowns = false;
            }
        }

        private void updateDropdownDisabledState()
        {
            bool disabled = !useDefaultPresets.Value;

            foreach (var (_, _, bindable) in rulesetDropdowns)
                bindable.Disabled = disabled;
        }

        private static List<PresetSnapshot> snapshotPresets(IEnumerable<ModPreset> presets)
        {
            var list = new List<PresetSnapshot>();

            foreach (var preset in presets)
            {
                if (preset.DeletePending || string.IsNullOrEmpty(preset.Ruleset?.ShortName))
                    continue;

                string[] acronyms;

                try
                {
                    acronyms = preset.Mods.Select(m => m.Acronym).ToArray();
                }
                catch
                {
                    acronyms = Array.Empty<string>();
                }

                string label = acronyms.Length > 0
                    ? $"{preset.Name} ({string.Join(@", ", acronyms)})"
                    : preset.Name;

                list.Add(new PresetSnapshot(preset.ID, preset.Ruleset.ShortName, label));
            }

            return list;
        }

        private static List<ModPresetChoice> buildChoices(List<PresetSnapshot> snapshots, RulesetInfo ruleset)
        {
            var choices = new List<ModPresetChoice> { ModPresetChoice.NONE };

            choices.AddRange(snapshots
                             .Where(s => string.Equals(s.RulesetShortName, ruleset.ShortName, StringComparison.OrdinalIgnoreCase))
                             .OrderBy(s => s.Label, StringComparer.OrdinalIgnoreCase)
                             .Select(s => new ModPresetChoice(s.ID, s.Label)));

            return choices;
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            realmSubscription?.Dispose();
        }

        private readonly record struct PresetSnapshot(Guid ID, string RulesetShortName, string Label);

        public readonly struct ModPresetChoice : IEquatable<ModPresetChoice>
        {
            public static readonly ModPresetChoice NONE = new ModPresetChoice(null, @"None");

            private readonly string? label;

            public Guid? ID { get; }

            public string Label => label ?? @"None";

            public ModPresetChoice(Guid? id, string label)
            {
                ID = id;
                this.label = label;
            }

            public bool Equals(ModPresetChoice other) => ID == other.ID;

            public override bool Equals(object? obj) => obj is ModPresetChoice other && Equals(other);

            public override int GetHashCode() => ID?.GetHashCode() ?? 0;

            public override string ToString() => Label;
        }
    }
}
