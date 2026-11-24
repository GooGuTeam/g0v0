// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Localisation;
using osu.Framework.Screens;
using osu.Game.Database;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.IO;
using osu.Game.Localisation;
using osu.Game.Online.Chat;
using osu.Game.Overlays.Settings;
using osu.Game.Overlays.Settings.Sections.Maintenance;
using osu.Game.Screens.Edit.Setup;
using osuTK;

namespace osu.Game.Overlays.FirstRunSetup
{
    [LocalisableDescription(typeof(FirstRunOverlayImportFromStableScreenStrings), nameof(FirstRunOverlayImportFromStableScreenStrings.Header))]
    public partial class ScreenImport : WizardScreen
    {
        private static readonly Vector2 button_size = new Vector2(400, 50);

        private readonly Bindable<ImportSource> source = new Bindable<ImportSource>(ImportSource.Stable);

        private ProgressRoundedButton importButton = null!;

        private OsuTextFlowContainer progressText = null!;

        [Resolved]
        private LegacyImportManager legacyImportManager { get; set; } = null!;

        private ImportLocatorLabelledTextBox locatorTextBox = null!;

        private LinkFlowContainer description = null!;

        private LinkFlowContainer copyInformation = null!;

        private SourceTabControl sourceTabControl = null!;

        private IEnumerable<ImportCheckbox> contentCheckboxes => Content.Children.OfType<ImportCheckbox>();

        /// <summary>
        /// The sources content has already been imported from.
        /// </summary>
        private readonly HashSet<ImportSource> importedSources = new HashSet<ImportSource>();

        /// <summary>
        /// The source an import is currently running for, if any. No other import can be started while this is set.
        /// </summary>
        private ImportSource? importingSource;

        private ImportButtonState buttonState;

        /// <summary>
        /// The installation whose readability has already been determined.
        /// </summary>
        /// <remarks>
        /// Checking may involve copying the whole database of the installation, so each one is only ever checked once.
        /// </remarks>
        private LazerStorage? readabilityCheckedStorage;

        /// <summary>
        /// Whether <see cref="readabilityCheckedStorage"/> could be read.
        /// </summary>
        private bool readabilityCheckPassed = true;

        /// <summary>
        /// Whether the data of the currently selected source can be read.
        /// </summary>
        /// <remarks>
        /// Installations of other clients are only known to be unreadable once that has been attempted, so they are assumed
        /// to be readable while the check is still running.
        /// </remarks>
        private bool sourceReadable
        {
            get
            {
                // Stable data is read from files directly, so it cannot be unreadable in this sense.
                if (source.Value != ImportSource.Lazer)
                    return true;

                LazerStorage? storage = legacyImportManager.GetCurrentLazerStorage();

                // A newly selected installation has not been checked yet, and is therefore not ruled out.
                return storage == null || !ReferenceEquals(storage, readabilityCheckedStorage) || readabilityCheckPassed;
            }
        }

        [BackgroundDependencyLoader(permitNulls: true)]
        private void load()
        {
            Content.Children = new Drawable[]
            {
                description = new LinkFlowContainer(cp => cp.Font = OsuFont.Default.With(size: CONTENT_FONT_SIZE))
                {
                    Colour = OverlayColourProvider.Content1,
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y
                },
                sourceTabControl = new SourceTabControl
                {
                    Title = ScreenImportStrings.SourceLabel,
                    Current = source
                },
                locatorTextBox = new ImportLocatorLabelledTextBox(source),
                new ImportCheckbox(CommonStrings.Beatmaps, ImportContent.Beatmaps, source),
                new ImportCheckbox(CommonStrings.Scores, ImportContent.Scores, source),
                new ImportCheckbox(CommonStrings.Skins, ImportContent.Skins, source),
                new ImportCheckbox(CommonStrings.Collections, ImportContent.Collections, source),
                copyInformation = new LinkFlowContainer(cp => cp.Font = OsuFont.Default.With(size: CONTENT_FONT_SIZE))
                {
                    Colour = OverlayColourProvider.Content1,
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y
                },
                importButton = new ProgressRoundedButton
                {
                    Size = button_size,
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    Text = FirstRunOverlayImportFromStableScreenStrings.ImportButton,
                    Action = runImport
                },
                progressText = new OsuTextFlowContainer(cp => cp.Font = OsuFont.Default.With(size: CONTENT_FONT_SIZE))
                {
                    Colour = OverlayColourProvider.Content1,
                    Text = FirstRunOverlayImportFromStableScreenStrings.ImportInProgress,
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Alpha = 0,
                },
            };

            locatorTextBox.Current.BindValueChanged(_ => updateSourcePath());
            source.BindValueChanged(onSourceChanged, true);
        }

        [Resolved(canBeNull: true)]
        private OsuGame? game { get; set; }

        private void onSourceChanged(ValueChangedEvent<ImportSource> e)
        {
            description.Text = e.NewValue == ImportSource.Stable
                ? FirstRunOverlayImportFromStableScreenStrings.Description
                : ScreenImportStrings.LazerDescription;

            updateSourcePath();
        }

        private void updateSourcePath(bool updateCounts = true)
        {
            bool available = !string.IsNullOrEmpty(legacyImportManager.GetCurrentStoragePath(source.Value));
            bool imported = importedSources.Contains(source.Value);
            bool importing = importingSource != null;

            // Pin the current source tab when an import is in progress.
            sourceTabControl.Current.Disabled = importing;

            // The directory can still be changed when nothing is selected yet, but not once content came from there.
            locatorTextBox.Current.Disabled = importing || imported;

            if (!available)
            {
                copyInformation.Text = string.Empty;

                foreach (var c in contentCheckboxes)
                {
                    c.Current.Disabled = true;
                    c.ResetCount();
                }
            }
            else
            {
                foreach (var c in contentCheckboxes)
                {
                    c.Current.Disabled = importing || imported;

                    if (updateCounts)
                        c.UpdateCount();
                }

                // Describes the currently selected source, so it has to follow the selection even while an import runs.
                if (sourceReadable)
                    updateCopyInformation();
                else
                    copyInformation.Text = ScreenImportStrings.DatabaseUnreadable;

                if (!importing && !imported)
                    verifySourceReadable();
            }

            updateImportButton();
        }

        /// <summary>
        /// Applies the state belonging to the source which is currently selected.
        /// </summary>
        private void updateImportButton()
        {
            bool available = !string.IsNullOrEmpty(legacyImportManager.GetCurrentStoragePath(source.Value));
            bool imported = importedSources.Contains(source.Value);

            if (imported)
                setButtonState(ImportButtonState.Completed);
            else if (importingSource != null)
                setButtonState(ImportButtonState.Running);
            else
                setButtonState(ImportButtonState.Idle);

            // Everything which may keep the button from being usable is derived here, so that no caller can leave it
            // in a state which contradicts the rest of the screen.
            importButton.Enabled.Value = available && sourceReadable && !imported && importingSource == null;
        }

        private void setButtonState(ImportButtonState state)
        {
            // Skip when unchanged, as re-applying a state would restart its animations.
            if (buttonState == state)
                return;

            buttonState = state;

            switch (state)
            {
                case ImportButtonState.Idle:
                    importButton.Reset();
                    break;

                case ImportButtonState.Completed:
                    importButton.Complete();
                    break;

                case ImportButtonState.Running:
                    // The button puts itself into this state when pressed.
                    break;
            }
        }

        private void updateCopyInformation()
        {
            copyInformation.Text = string.Empty;

            if (legacyImportManager.CheckHardLinkAvailability(source.Value))
            {
                copyInformation.Text = FirstRunOverlayImportFromStableScreenStrings.DataMigrationNoExtraSpace;
                copyInformation.AddText(@" "); // just to ensure correct spacing
                copyInformation.AddLink(FirstRunOverlayImportFromStableScreenStrings.LearnAboutHardLinks, LinkAction.OpenWiki, @"Client/Release_stream/Lazer/File_storage#via-hard-links");
                return;
            }

            if (!RuntimeInfo.IsDesktop)
            {
                copyInformation.Text = FirstRunOverlayImportFromStableScreenStrings.LightweightLinkingNotSupported;
                return;
            }

            copyInformation.Text = source.Value == ImportSource.Stable
                ? (RuntimeInfo.OS == RuntimeInfo.Platform.Windows
                    ? FirstRunOverlayImportFromStableScreenStrings.SecondCopyWillBeMadeWindows
                    : FirstRunOverlayImportFromStableScreenStrings.SecondCopyWillBeMadeOtherPlatforms)
                : ScreenImportStrings.SecondCopyWillBeMade;

            copyInformation.AddText(@" "); // just to ensure correct spacing

            copyInformation.AddLink(GeneralSettingsStrings.ChangeFolderLocation, () =>
            {
                game?.PerformFromScreen(menu => menu.Push(new MigrationSelectScreen()));
            });
        }

        /// <summary>
        /// The data of an installation has to be read to import it, which can fail for installations of other clients.
        /// </summary>
        private void verifySourceReadable()
        {
            if (!legacyImportManager.SupportsImportFromLazer || source.Value != ImportSource.Lazer)
                return;

            // Identifies the installation the check is about: the cached storage is replaced as soon as another one is
            // selected, so comparing it afterwards tells whether the result still belongs to the selection on screen.
            LazerStorage? checkedStorage = legacyImportManager.GetCurrentLazerStorage();

            // An installation may need to be copied before it can be read, so it is only ever checked once.
            if (checkedStorage == null || ReferenceEquals(checkedStorage, readabilityCheckedStorage))
                return;

            legacyImportManager.CheckSourceReadableAsync(ImportSource.Lazer).ContinueWith(t => Schedule(() =>
            {
                bool readable = t.GetResultSafely();

                // The installation may have been changed for a different one of the same source kind.
                if (!ReferenceEquals(checkedStorage, legacyImportManager.GetCurrentLazerStorage()))
                    return;

                // The result describes the installation rather than the selection, so it is kept even if the user switched
                // away from it in the meantime. This also saves checking it again when they switch back.
                readabilityCheckedStorage = checkedStorage;
                readabilityCheckPassed = readable;

                // The user may have switched away from the source, or already imported from it, in which case there is nothing
                // left to explain about a database they cannot use.
                if (readable || source.Value != ImportSource.Lazer || importedSources.Contains(ImportSource.Lazer))
                    return;

                // Re-derives both the button and the explanation from the readability which just became known.
                updateSourcePath(false);
            }));
        }

        private void runImport()
        {
            ImportSource importSource = source.Value;

            importingSource = importSource;

            // Derive the locked interaction state from the import running, rather than disabling widgets by hand.
            updateSourcePath(false);

            progressText.FadeIn(1000, Easing.OutQuint);

            ImportContent importableContent = 0;

            foreach (var c in contentCheckboxes.Where(c => c.Current.Value))
                importableContent |= c.ImportContent;

            legacyImportManager.ImportAsync(importSource, importableContent, false).ContinueWith(t => Schedule(() =>
            {
                importingSource = null;

                progressText.FadeOut(500, Easing.OutQuint);

                if (t.IsCompletedSuccessfully)
                    importedSources.Add(importSource);

                // Re-derives the state of whichever source is selected now. The imported source keeps its completed state
                // for when the user switches back to it.
                updateSourcePath();
            }));
        }

        private enum ImportButtonState
        {
            Idle,
            Running,
            Completed
        }

        public override void OnSuspending(ScreenTransitionEvent e)
        {
            locatorTextBox.HidePopover();
            base.OnSuspending(e);
        }

        public override bool OnExiting(ScreenExitEvent e)
        {
            locatorTextBox.HidePopover();
            return base.OnExiting(e);
        }

        /// <summary>
        /// A tab control whose tabs cannot be cycled between, so that the source can only change by activating a tab.
        /// </summary>
        private partial class SourceTabControl : OverlaySortTabControl<ImportSource>
        {
            protected override SortTabControl CreateControl() => new NonSwitchableSortTabControl();

            private partial class NonSwitchableSortTabControl : SortTabControl
            {
                protected override TabItem<ImportSource> CreateTabItem(ImportSource value) => new NonSwitchableTabItem(value);
            }

            private partial class NonSwitchableTabItem : SortTabItem
            {
                public NonSwitchableTabItem(ImportSource value)
                    : base(value)
                {
                }

                // Cycles only consider switchable tabs, so this leaves nothing to switch to.
                public override bool IsSwitchable => false;
            }
        }

        private partial class ImportCheckbox : SettingsCheckbox
        {
            public readonly ImportContent ImportContent;

            private readonly LocalisableString title;

            [Resolved]
            private LegacyImportManager legacyImportManager { get; set; } = null!;

            private readonly Bindable<ImportSource> source;

            private CancellationTokenSource? countUpdateCancellation;

            public ImportCheckbox(LocalisableString title, ImportContent importContent, Bindable<ImportSource> source)
            {
                this.title = title;
                this.source = source;

                ImportContent = importContent;

                Current.Default = true;
                Current.Value = true;

                LabelText = title;
            }

            /// <summary>
            /// Clears any previously displayed count, for when no installation is selected.
            /// </summary>
            public void ResetCount()
            {
                countUpdateCancellation?.Cancel();
                countUpdateCancellation = null;
                LabelText = title;
            }

            public void UpdateCount()
            {
                LabelText = LocalisableString.Interpolate($"{title} ({FirstRunOverlayImportFromStableScreenStrings.Calculating})");

                countUpdateCancellation?.Cancel();

                var cancellation = countUpdateCancellation = new CancellationTokenSource();

                legacyImportManager.GetImportCount(source.Value, ImportContent, cancellation.Token)
                                   .ContinueWith(task => Schedule(() =>
                                   {
                                       // A newer request has been made since this one (the source was switched, or no installation
                                       // is selected anymore), so this count belongs to a source which is no longer displayed.
                                       if (cancellation != countUpdateCancellation || task.IsCanceled)
                                           return;

                                       int count = task.GetResultSafely();

                                       LabelText = LocalisableString.Interpolate($"{title} ({CommonStrings.ItemsCount(count)})");
                                   }), cancellation.Token);
            }
        }

        internal partial class ImportLocatorLabelledTextBox : LabelledTextBoxWithPopover, ICanAcceptFiles
        {
            [Resolved]
            private LegacyImportManager legacyImportManager { get; set; } = null!;

            public IEnumerable<string> HandledExtensions { get; } = new[] { string.Empty };

            private readonly Bindable<ImportSource> source;

            private readonly Bindable<DirectoryInfo?> currentDirectory = new Bindable<DirectoryInfo?>();

            [Resolved(canBeNull: true)] // Can't really be null but required to handle potential of disposal before DI completes.
            private OsuGameBase? game { get; set; }

            private bool changingDirectory;

            public ImportLocatorLabelledTextBox(Bindable<ImportSource> source)
            {
                this.source = source;
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                game?.RegisterImportHandler(this);

                currentDirectory.BindValueChanged(onDirectorySelected);
                source.BindValueChanged(_ => updateFromStorage(), true);
            }

            private void updateFromStorage()
            {
                Label = source.Value == ImportSource.Stable
                    ? FirstRunOverlayImportFromStableScreenStrings.LocateDirectoryLabel
                    : ScreenImportStrings.LocateLazerDirectoryLabel;

                PlaceholderText = source.Value == ImportSource.Stable
                    ? FirstRunOverlayImportFromStableScreenStrings.LocateDirectoryPlaceholder
                    : ScreenImportStrings.LocateLazerDirectoryPlaceholder;

                string? fullPath = legacyImportManager.GetCurrentStoragePath(source.Value);

                if (fullPath == null)
                {
                    Current.Value = string.Empty;
                    currentDirectory.Value = null;
                    return;
                }

                currentDirectory.Value = new DirectoryInfo(fullPath);
            }

            private void onDirectorySelected(ValueChangedEvent<DirectoryInfo?> directory)
            {
                if (changingDirectory)
                    return;

                try
                {
                    changingDirectory = true;

                    if (directory.NewValue == null)
                    {
                        Current.Value = string.Empty;
                        return;
                    }

                    // DirectorySelectors can trigger a noop value changed, but `DirectoryInfo` equality doesn't catch this.
                    if (directory.OldValue?.FullName == directory.NewValue.FullName)
                        return;

                    if (legacyImportManager.IsUsableAsImportSource(source.Value, directory.NewValue, out var root))
                    {
                        this.HidePopover();

                        string path = root.FullName;

                        legacyImportManager.UpdateStorage(source.Value, path);
                        Current.Value = path;
                        currentDirectory.Value = root;
                    }
                }
                finally
                {
                    changingDirectory = false;
                }
            }

            Task ICanAcceptFiles.Import(params string[] paths)
            {
                Schedule(() => currentDirectory.Value = new DirectoryInfo(paths.First()));
                return Task.CompletedTask;
            }

            Task ICanAcceptFiles.Import(ImportTask[] tasks, ImportParameters parameters) => throw new NotImplementedException();

            protected override void Dispose(bool isDisposing)
            {
                base.Dispose(isDisposing);
                game?.UnregisterImportHandler(this);
            }

            public override Popover GetPopover() => new DirectoryChooserPopover(currentDirectory);

            private partial class DirectoryChooserPopover : OsuPopover
            {
                public DirectoryChooserPopover(Bindable<DirectoryInfo?> currentDirectory)
                    : base(false)
                {
                    Child = new Container
                    {
                        Size = new Vector2(600, 400),
                        Child = new OsuDirectorySelector(currentDirectory.Value?.FullName)
                        {
                            RelativeSizeAxes = Axes.Both,
                            CurrentPath = { BindTarget = currentDirectory }
                        },
                    };
                }

                [BackgroundDependencyLoader]
                private void load(OverlayColourProvider colourProvider)
                {
                    Body.BorderColour = colourProvider.Highlight1;
                    Body.BorderThickness = 2;
                }
            }
        }
    }
}
