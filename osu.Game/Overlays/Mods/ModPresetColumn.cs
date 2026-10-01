// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Input.Events;
using osu.Game.Database;
using osu.Game.Graphics;
using osu.Game.Localisation;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osuTK;
using osuTK.Input;

namespace osu.Game.Overlays.Mods
{
    public partial class ModPresetColumn : ModSelectColumn
    {
        [Resolved]
        private IModPresetStore modPresetStore { get; set; } = null!;

        [Resolved]
        private IBindable<RulesetInfo> ruleset { get; set; } = null!;

        private const float contracted_width = WIDTH - 120;

        private readonly Key[] toggleKeys = { Key.Number1, Key.Number2, Key.Number3, Key.Number4, Key.Number5, Key.Number6, Key.Number7, Key.Number8, Key.Number9, Key.Number0 };

        [BackgroundDependencyLoader]
        private void load(OsuColour colours)
        {
            AccentColour = colours.Orange1;
            HeaderText = ModSelectOverlayStrings.PersonalPresets;

            AddPresetButton addPresetButton;
            ItemsFlow.Add(addPresetButton = new AddPresetButton());
            ItemsFlow.SetLayoutPosition(addPresetButton, float.PositiveInfinity);
        }

        private IDisposable? presetSubscription;

        protected override void LoadComplete()
        {
            base.LoadComplete();

            presetSubscription = modPresetStore.Subscribe(() => Schedule(loadPanels));

            ruleset.BindValueChanged(_ => loadPanels(), true);

            Width = contracted_width;
        }

        private void loadPanels()
        {
            var presets = modPresetStore.GetAllUsableDetached()
                                        .Where(p => p.Ruleset.ShortName == ruleset.Value.ShortName)
                                        .OrderBy(p => p.Name)
                                        .ToList();

            asyncLoadPanels(presets);
        }

        private CancellationTokenSource? cancellationTokenSource;

        private Task? latestLoadTask;
        internal bool ItemsLoaded => latestLoadTask?.IsCompleted == true;

        private void asyncLoadPanels(List<ModPreset> presets)
        {
            cancellationTokenSource?.Cancel();

            bool hasPresets = presets.Count > 0;

            this.ResizeWidthTo(hasPresets ? WIDTH : contracted_width, 200, Easing.OutQuint);

            if (!hasPresets)
            {
                removeAndDisposePresetPanels();
                return;
            }

            var panels = new List<ModPresetPanel>();

            for (int i = 0; i < presets.Count; i++)
            {
                var preset = presets[i];

                panels.Add(new ModPresetPanel(preset)
                {
                    Index = i < 10 ? (i + 1) % 10 : null,
                    Shear = Vector2.Zero
                });
            }

            latestLoadTask = LoadComponentsAsync(panels, loaded =>
            {
                removeAndDisposePresetPanels();
                ItemsFlow.AddRange(loaded);
            }, (cancellationTokenSource = new CancellationTokenSource()).Token);

            void removeAndDisposePresetPanels()
            {
                foreach (var panel in ItemsFlow.OfType<ModPresetPanel>().ToArray())
                    panel.RemoveAndDisposeImmediately();
            }
        }

        protected override bool OnKeyDown(KeyDownEvent e)
        {
            if (e.ControlPressed || e.AltPressed || e.SuperPressed || e.Repeat)
                return false;

            int index = Array.IndexOf(toggleKeys, e.Key);
            if (index < 0)
                return false;

            var panel = ItemsFlow.OfType<ModPresetPanel>().ElementAtOrDefault(index);
            if (panel == null)
                return false;

            panel.Toggle();

            return true;
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            presetSubscription?.Dispose();
        }
    }
}
