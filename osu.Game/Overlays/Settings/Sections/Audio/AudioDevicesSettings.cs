// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using System.Collections.Generic;
using System.Linq;
using osu.Framework;
using osu.Framework.Bindables;
using osu.Framework.Development;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Localisation;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osuTK;
using osu.Game.Overlays.Settings.Entries;

namespace osu.Game.Overlays.Settings.Sections.Audio
{
    public partial class AudioDevicesSettings : SettingsSubsection
    {
        protected override LocalisableString Header => AudioSettingsStrings.AudioDevicesHeader;

        [Resolved]
        private AudioManager audio { get; set; } = null!;

        private DropdownEntry<string> dropdown = null!;

        /// <summary>
        /// The inverse of <see cref="AudioManager.UseWasapi"/>, shown as the "legacy audio" toggle.
        /// </summary>
        private readonly BindableBool legacyAudioMode = new BindableBool();

        private Bindable<bool> useWasapi = null!;

        private CheckboxEntry? legacyAudio;
        private CheckboxEntry? exclusive;
        private CheckboxEntry? autoSharedOnBackground;
        private SliderEntry<double>? buffer;
        private SliderEntry<double>? period;
        private FillFlowContainer? wasapiSettings;

        [BackgroundDependencyLoader]
        private void load()
        {
            Children = new Drawable[]
            {
                CreateItem(dropdown = new DropdownEntry<string>
                {
                    Caption = AudioSettingsStrings.OutputDevice,
                    ItemLabel = item => string.IsNullOrEmpty(item) ? CommonStrings.Default : item,
                    Keywords = new[] { "speaker", "headphone", "output" },
                }),
            };

            if (RuntimeInfo.OS == RuntimeInfo.Platform.Windows)
            {
                Add(CreateItem(legacyAudio = new CheckboxEntry
                {
                    Caption = AudioSettingsStrings.LegacyAudioLabel,
                    HintText = AudioSettingsStrings.LegacyAudioTooltip,
                    Current = legacyAudioMode,
                    Keywords = new[] { "wasapi", "latency", "exclusive", "legacy", "experimental" },
                }));
                Add(CreateItem(new ButtonEntry
                {
                    Keywords = new[] { "audio" },
                    Action = () =>
                    {
                        audio.RestartAudioEngine();
                        onDeviceChanged(string.Empty);
                    },
                    Caption = AudioSettingsStrings.RestartAudioEngine,
                }));

                wasapiSettings = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, SettingsSection.ITEM_SPACING_V2),
                    Masking = true,
                    Children = new Drawable[]
                    {
                        CreateItem(exclusive = new CheckboxEntry
                        {
                            Current = audio.WasapiIsExclusive,
                            Caption = AudioSettingsStrings.ExclusiveAudioMode,
                            HintText = AudioSettingsStrings.ExclusiveAudioModeTooltip,
                            Keywords = new[] { "wasapi", "latency", "exclusive" },
                            CanBeShown = { BindTarget = audio.UseWasapi },
                        }),
                        CreateItem(autoSharedOnBackground = new CheckboxEntry
                        {
                            Current = audio.WasapiAutoSharedOnBackground,
                            Caption = AudioSettingsStrings.AutoSharedAudioOnBackground,
                            HintText = AudioSettingsStrings.AutoSharedAudioOnBackgroundTooltip,
                            Keywords = new[] { "wasapi", "latency", "exclusive", "background", "shared" },
                            CanBeShown = { BindTarget = audio.UseWasapi },
                        }),
                        CreateItem(buffer = new SliderEntry<double>
                        {
                            Current = audio.WasapiBufferMilliseconds,
                            Caption = AudioSettingsStrings.BufferSize,
                            HintText = AudioSettingsStrings.BufferSizeTooltip,
                            Keywords = new[] { "wasapi", "latency", "exclusive" },
                            CanBeShown = { BindTarget = audio.UseWasapi },
                        }),
                        CreateItem(period = new SliderEntry<double>
                        {
                            Current = audio.WasapiPeriodMilliseconds,
                            Caption = AudioSettingsStrings.Period,
                            HintText = AudioSettingsStrings.PeriodTooltip,
                            Keywords = new[] { "wasapi", "latency", "exclusive" },
                            CanBeShown = { BindTarget = audio.UseWasapi },
                        }),
                    }
                };
                Add(wasapiSettings);

                legacyAudio.Current.ValueChanged += _ => onDeviceChanged(string.Empty);
            }

            audio.OnNewDevice += onDeviceChanged;
            audio.OnLostDevice += onDeviceChanged;
            dropdown.Current = audio.AudioDevice;

            onDeviceChanged(string.Empty);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            if (wasapiSettings != null)
            {
                // Manual two-way binding because the toggle is the inverse of what the framework exposes.
                useWasapi = audio.UseWasapi.GetBoundCopy();
                legacyAudioMode.ValueChanged += legacy => useWasapi.Value = !legacy.NewValue;
                useWasapi.BindValueChanged(experimental =>
                {
                    if (ThreadSafety.IsUpdateThread)
                        legacyAudioMode.Value = !experimental.NewValue;
                    else
                        Scheduler.AddOnce(v => legacyAudioMode.Value = v, !experimental.NewValue);
                }, true);

                audio.UseWasapi.BindValueChanged(updateWasapiVisibility, true);

                if (autoSharedOnBackground != null)
                    audio.WasapiIsExclusive.BindValueChanged(v => autoSharedOnBackground.Current.Disabled = !v.NewValue, true);
            }
        }

        private void updateWasapiVisibility(ValueChangedEvent<bool> state)
        {
            wasapiSettings!.ClearTransforms();

            if (!state.NewValue)
            {
                wasapiSettings.AutoSizeAxes = Axes.None;
                if (state.NewValue == state.OldValue)
                    wasapiSettings.Height = 0;
                else
                    wasapiSettings.ResizeHeightTo(0, 300, Easing.OutQuint);
            }
            else
            {
                wasapiSettings.AutoSizeDuration = state.NewValue == state.OldValue ? 0 : 300;
                wasapiSettings.AutoSizeEasing = Easing.OutQuint;
                wasapiSettings.AutoSizeAxes = Axes.Y;

                ScheduleAfterChildren(() => wasapiSettings.AutoSizeDuration = 0);
            }
        }

        private void onDeviceChanged(string _) => Scheduler.AddOnce(updateItems);

        private void updateItems()
        {
            var deviceItems = new List<string> { string.Empty };
            deviceItems.AddRange(audio.AudioDeviceNames);

            string preferredDeviceName = audio.AudioDevice.Value;
            if (deviceItems.All(kv => kv != preferredDeviceName))
                deviceItems.Add(preferredDeviceName);

            // The option dropdown for audio device selection lists all audio
            // device names. Dropdowns, however, may not have multiple identical
            // keys. Thus, we remove duplicate audio device names from
            // the dropdown. BASS does not give us a simple mechanism to select
            // specific audio devices in such a case anyways. Such
            // functionality would require involved OS-specific code.
            dropdown.Items = deviceItems
                             // Dropdown doesn't like null items. Somehow we are seeing some arrive here (see https://github.com/ppy/osu/issues/21271)
                             .Where(i => i.IsNotNull())
                             .Distinct()
                             .ToList();
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (audio.IsNotNull())
            {
                audio.OnNewDevice -= onDeviceChanged;
                audio.OnLostDevice -= onDeviceChanged;
            }
        }

    }

    public partial class LegacyAudioCheckbox : FormCheckBox
    {
        private Bindable<bool> configExperimentalAudio = null!;

        public LegacyAudioCheckbox()
        {
            Caption = AudioSettingsStrings.LegacyAudioLabel;
            HintText = AudioSettingsStrings.LegacyAudioTooltip;
        }

        [BackgroundDependencyLoader]
        private void load(AudioManager audio)
        {
            configExperimentalAudio = audio.UseWasapi.GetBoundCopy();
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            // Manual two-way binding because we're inverting what the framework exposes.
            Current.ValueChanged += legacy =>
            {
                configExperimentalAudio.Value = !legacy.NewValue;
            };

            configExperimentalAudio.BindValueChanged(experimental =>
            {
                if (ThreadSafety.IsUpdateThread)
                    Current.Value = !experimental.NewValue;
                else
                    Scheduler.AddOnce(updateValue, !experimental.NewValue);
            }, true);
        }

        private void updateValue(bool value) => Current.Value = value;
    }
}
