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

        private IEnumerable<ImportCheckbox> contentCheckboxes => Content.Children.OfType<ImportCheckbox>();

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
                new OverlaySortTabControl<ImportSource>
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
            source.BindValueChanged(_ => onSourceChanged(), true);
        }

        [Resolved(canBeNull: true)]
        private OsuGame? game { get; set; }

        private void onSourceChanged()
        {
            description.Text = source.Value == ImportSource.Stable
                ? FirstRunOverlayImportFromStableScreenStrings.Description
                : ScreenImportStrings.LazerDescription;

            updateSourcePath();
        }

        private void updateSourcePath()
        {
            bool available = !string.IsNullOrEmpty(legacyImportManager.GetCurrentStoragePath(source.Value));

            if (!available)
            {
                toggleInteraction(false);
                locatorTextBox.Current.Disabled = false;
                copyInformation.Text = string.Empty;

                foreach (var c in contentCheckboxes)
                    c.ResetCount();

                return;
            }

            foreach (var c in contentCheckboxes)
            {
                c.Current.Disabled = false;
                c.UpdateCount();
            }

            toggleInteraction(true);
            importButton.Enabled.Value = true;

            updateCopyInformation();
            verifySourceReadable();
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

            legacyImportManager.CheckSourceReadableAsync(ImportSource.Lazer).ContinueWith(t => Schedule(() =>
            {
                // The user may have switched away from the source while the check was running.
                if (source.Value != ImportSource.Lazer || t.GetResultSafely())
                    return;

                copyInformation.Text = ScreenImportStrings.DatabaseUnreadable;
                importButton.Enabled.Value = false;
            }));
        }

        private void runImport()
        {
            toggleInteraction(false);
            progressText.FadeIn(1000, Easing.OutQuint);

            ImportContent importableContent = 0;

            foreach (var c in contentCheckboxes.Where(c => c.Current.Value))
                importableContent |= c.ImportContent;

            legacyImportManager.ImportAsync(source.Value, importableContent, false).ContinueWith(t => Schedule(() =>
            {
                progressText.FadeOut(500, Easing.OutQuint);

                if (t.IsCompletedSuccessfully)
                    importButton.Complete();
                else
                {
                    toggleInteraction(true);
                    importButton.Abort();
                }
            }));
        }

        private void toggleInteraction(bool allow)
        {
            importButton.Enabled.Value = allow;
            locatorTextBox.Current.Disabled = !allow;
            foreach (var c in contentCheckboxes)
                c.Current.Disabled = !allow;
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
                LabelText = title;
            }

            public void UpdateCount()
            {
                LabelText = LocalisableString.Interpolate($"{title} ({FirstRunOverlayImportFromStableScreenStrings.Calculating})");

                countUpdateCancellation?.Cancel();
                countUpdateCancellation = new CancellationTokenSource();

                var cancellation = countUpdateCancellation;

                legacyImportManager.GetImportCount(source.Value, ImportContent, cancellation.Token)
                                   .ContinueWith(task => Schedule(() =>
                                   {
                                       if (task.IsCanceled)
                                           return;

                                       int count = task.GetResultSafely();

                                       LabelText = LocalisableString.Interpolate($"{title} ({CommonStrings.ItemsCount(count)})");
                                   }));
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
