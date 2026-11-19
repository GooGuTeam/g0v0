// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Input.Events;
using osu.Framework.IO.Network;
using osu.Framework.Localisation;
using osu.Framework.Platform;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Input.Bindings;
using osu.Game.IO;
using osu.Game.Localisation;
using osu.Game.Online;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;
using osu.Game.Skinning;
using osu.Game.Screens.Backgrounds;
using Image = osu.Framework.Imaging.Image;
using Rgba32 = osu.Framework.Imaging.Rgba32;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Menu
{
    public partial class BackgroundEditorOverlay : OsuFocusedOverlayContainer
    {
        private const float background_option_height = 128;
        private const float header_height = 40;
        private const double carousel_interval = 3000;

        private readonly FillFlowContainer optionsFlow;
        private readonly FormEnumDropdown<BackgroundSource> sourceDropdown;
        private readonly FormEnumDropdown<SeasonalBackgroundMode> seasonalDropdown;

        private BackgroundOption? skinOption;
        private BackgroundOption? beatmapOption;
        private BackgroundOption? seasonalRandomOption;
        private readonly List<BackgroundOption> pinnedOptions = new List<BackgroundOption>();

        /// <summary>
        /// Keeps one reference to every seasonal background texture to prevent the LargeTextureStore from disposing it.
        /// </summary>
        private readonly List<Texture> seasonalPins = new List<Texture>();

        /// <summary>
        /// Seasonal backgrounds which were successfully loaded, used to drive the random entry's carousel.
        /// </summary>
        private readonly List<string> carouselUrls = new List<string>();

        private int carouselIndex;

        private bool optionsBuilt;

        /// <summary>
        /// Incremented whenever the available seasonal backgrounds change, so that results of an
        /// outdated background texture load are discarded rather than written into new options.
        /// </summary>
        private int seasonalLoadGeneration;

        private Bindable<BackgroundSource> source = null!;
        private Bindable<SeasonalBackgroundMode> seasonalMode = null!;
        private Bindable<bool> pinnedSeasonal = null!;
        private Bindable<int> pinnedSeasonalIndex = null!;
        private Bindable<APISeasonalBackgrounds> seasonalBackgrounds = null!;

        [Resolved]
        private SkinManager skinManager { get; set; } = null!;

        [Resolved]
        private IBindable<WorkingBeatmap> beatmap { get; set; } = null!;

        [Resolved]
        private LargeTextureStore textures { get; set; } = null!;

        [Resolved]
        private Storage storage { get; set; } = null!;

        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        [Resolved]
        private INotificationOverlay notifications { get; set; } = null!;

        [Resolved]
        private OsuGameBase game { get; set; } = null!;

        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        [Resolved]
        private SessionStatics sessionStatics { get; set; } = null!;

        [Cached]
        private readonly OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Purple);

        private Storage exportStorage = null!;

        /// <summary>
        /// The default background screen of the main menu, used to resolve the currently displayed
        /// background for export. Set by the owning screen.
        /// </summary>
        private BackgroundScreenDefault? defaultScreen;

        public BackgroundEditorOverlay()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Anchor = Anchor.BottomCentre;
            Origin = Anchor.BottomCentre;

            State.Value = Visibility.Hidden;

            InternalChildren = new Drawable[]
            {
                new Container
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Masking = true,
                    CornerRadius = 10,
                    CornerExponent = 2.5f,
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = colourProvider.Background5,
                        },
                        new FillFlowContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Direction = FillDirection.Vertical,
                            Children = new Drawable[]
                            {
                                createHeader(),
                                new Container
                                {
                                    RelativeSizeAxes = Axes.X,
                                    AutoSizeAxes = Axes.Y,
                                    Padding = new MarginPadding(15),
                                    Child = new GridContainer
                                    {
                                        RelativeSizeAxes = Axes.X,
                                        AutoSizeAxes = Axes.Y,
                                        RowDimensions = [new Dimension(GridSizeMode.AutoSize)],
                                        ColumnDimensions =
                                        [
                                            new Dimension(),
                                            new Dimension(GridSizeMode.AutoSize),
                                        ],
                                        Content = new Drawable[][]
                                        {
                                            [
                                                // a scroll container cannot auto-size along its scroll axis,
                                                // so the height is fixed to the option height plus the scrollbar.
                                                new OsuScrollContainer(Direction.Horizontal)
                                                {
                                                    RelativeSizeAxes = Axes.X,
                                                    ScrollbarOverlapsContent = false,
                                                    Height = background_option_height + 24,
                                                    Child = optionsFlow = new FillFlowContainer
                                                    {
                                                        AutoSizeAxes = Axes.Both,
                                                        Direction = FillDirection.Horizontal,
                                                        Spacing = new Vector2(8),
                                                    },
                                                },
                                                // Settings area.
                                                new FillFlowContainer
                                                {
                                                    Width = 380,
                                                    AutoSizeAxes = Axes.Y,
                                                    Direction = FillDirection.Vertical,
                                                    Spacing = new Vector2(8),
                                                    Padding = new MarginPadding { Left = 8 },
                                                    Children = new Drawable[]
                                                    {
                                                        sourceDropdown = new FormEnumDropdown<BackgroundSource>
                                                        {
                                                            Caption = UserInterfaceStrings.BackgroundSource,
                                                            RelativeSizeAxes = Axes.X,
                                                        },
                                                        seasonalDropdown = new FormEnumDropdown<SeasonalBackgroundMode>
                                                        {
                                                            Caption = UserInterfaceStrings.SeasonalBackgrounds,
                                                            RelativeSizeAxes = Axes.X,
                                                        },
                                                    },
                                                },
                                            ],
                                        },
                                    },
                                },
                            },
                        },
                    },
                },
            };

            // start off-screen below the bottom edge; PopIn slides it up.
            Y = 600;
        }

        private Drawable createHeader() => new Container
        {
            RelativeSizeAxes = Axes.X,
            Height = header_height,
            Children = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = colourProvider.Background3,
                },
                new GridContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    ColumnDimensions =
                    [
                        new Dimension(GridSizeMode.Absolute, 50),
                        new Dimension(),
                        new Dimension(GridSizeMode.AutoSize),
                    ],
                    Content = new Drawable[][]
                    {
                        [
                            new SpriteIcon
                            {
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre,
                                Icon = FontAwesome.Solid.Image,
                                Size = new Vector2(20),
                            },
                            new OsuSpriteText
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                Text = UserInterfaceStrings.BackgroundEditor,
                                Font = OsuFont.GetFont(size: 16, weight: FontWeight.SemiBold),
                                Margin = new MarginPadding { Bottom = 2 },
                            },
                            new FillFlowContainer
                            {
                                Anchor = Anchor.CentreRight,
                                Origin = Anchor.CentreRight,
                                AutoSizeAxes = Axes.Both,
                                Direction = FillDirection.Horizontal,
                                Spacing = new Vector2(4),
                                Padding = new MarginPadding { Right = 4 },
                                Children = new Drawable[]
                                {
                                    new IconButton
                                    {
                                        Icon = FontAwesome.Solid.Download,
                                        Size = new Vector2(32),
                                        IconScale = new Vector2(0.8f),
                                        TooltipText = UserInterfaceStrings.ExportBackground,
                                        Action = exportBackground,
                                    },
                                    new IconButton
                                    {
                                        Icon = FontAwesome.Solid.SignOutAlt,
                                        Size = new Vector2(32),
                                        IconScale = new Vector2(0.8f),
                                        TooltipText = UserInterfaceStrings.SaveAndExit,
                                        Action = Hide,
                                    },
                                },
                            },
                        ],
                    },
                },
            },
        };

        [BackgroundDependencyLoader]
        private void load()
        {
            source = config.GetBindable<BackgroundSource>(OsuSetting.MenuBackgroundSource);
            seasonalMode = config.GetBindable<SeasonalBackgroundMode>(OsuSetting.SeasonalBackgroundMode);
            pinnedSeasonal = config.GetBindable<bool>(OsuSetting.PinnedSeasonalBackground);
            pinnedSeasonalIndex = config.GetBindable<int>(OsuSetting.PinnedSeasonalBackgroundIndex);
            seasonalBackgrounds = sessionStatics.GetBindable<APISeasonalBackgrounds>(Static.SeasonalBackgrounds);

            exportStorage = (storage as OsuStorage)?.GetExportStorage() ?? storage.GetStorageForDirectory(@"exports");

            // these dropdowns are the same as those in the settings sidebar; keep the same bindables
            // so changes apply live and are visible in both places.
            sourceDropdown.Current = source;
            seasonalDropdown.Current = seasonalMode;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            source.BindValueChanged(_ => Schedule(refreshSelection), true);
            seasonalMode.BindValueChanged(_ => Schedule(refreshSelection));
            pinnedSeasonal.BindValueChanged(_ => Schedule(refreshSelection));
            pinnedSeasonalIndex.BindValueChanged(_ => Schedule(refreshSelection));

            // the previews may change while the editor is closed; only the affected thumbnail is refreshed.
            beatmap.BindValueChanged(_ => Schedule(refreshBeatmapThumbnail));
            skinManager.CurrentSkin.BindValueChanged(_ => Schedule(refreshSkinThumbnail));

            seasonalBackgrounds.BindValueChanged(_ => Schedule(() =>
            {
                if (!optionsBuilt)
                    return;

                rebuildOptions();
                refreshSelection();
            }), true);

            // rotates the random seasonal entry's preview through every available image.
            Scheduler.AddDelayed(carouselTick, carousel_interval);
        }

        private void carouselTick()
        {
            if (State.Value == Visibility.Visible && carouselUrls.Count > 1)
            {
                carouselIndex = (carouselIndex + 1) % carouselUrls.Count;
                showCarouselImage(carouselUrls[carouselIndex]);
            }

            Scheduler.AddDelayed(carouselTick, carousel_interval);
        }

        private void showCarouselImage(string url) => seasonalRandomOption?.SetTexture(textures.Get(url), url);

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (isDisposing)
                releaseSeasonalPins();
        }

        private void releaseSeasonalPins()
        {
            foreach (var pin in seasonalPins)
                pin.Dispose();

            seasonalPins.Clear();
        }

        /// <summary>
        /// Provide the default background screen, used for resolving the currently displayed background on export.
        /// </summary>
        public void SetDefaultScreen(BackgroundScreenDefault? screen) => defaultScreen = screen;

        protected override void PopIn()
        {
            // built once on first open; subsequent openings reuse the existing options (and their thumbnails).
            if (!optionsBuilt)
            {
                rebuildOptions();
                optionsBuilt = true;
            }

            this.MoveToY(0, 400, Easing.OutQuint);
            this.FadeIn(400, Easing.OutQuint);
        }

        protected override void PopOut()
        {
            this.MoveToY(DrawHeight, 400, Easing.InSine);
            this.FadeOut(400, Easing.OutQuint);
        }

        public override bool OnPressed(KeyBindingPressEvent<GlobalAction> e)
        {
            if (e.Repeat)
                return false;

            if (e.Action == GlobalAction.Select)
            {
                // exiting the editor saves changes implicitly, as all changes apply live.
                Hide();
                return true;
            }

            // back is handled by the base class (also hides the overlay).
            return base.OnPressed(e);
        }

        private bool seasonalDataAvailable => seasonalBackgrounds.Value?.Backgrounds?.Any() == true;

        private bool seasonalDisplayed => seasonalMode.Value != SeasonalBackgroundMode.Never;

        private void rebuildOptions()
        {
            // any seasonal texture load still in flight belongs to the options about to be discarded.
            Interlocked.Increment(ref seasonalLoadGeneration);

            optionsFlow.Clear();
            pinnedOptions.Clear();
            carouselUrls.Clear();
            carouselIndex = 0;

            // released after the options (and therefore the sprites referencing those textures) are gone.
            releaseSeasonalPins();

            skinOption = new BackgroundOption(BackgroundOptionType.Skin, getSkinTexture(), FontAwesome.Solid.PaintBrush)
            {
                TooltipText = UserInterfaceStrings.SkinBackground,
                Action = () =>
                {
                    if (source.Value != BackgroundSource.Skin)
                        source.Value = BackgroundSource.Skin;

                    seasonalMode.Value = SeasonalBackgroundMode.Never;
                },
            };
            optionsFlow.Add(skinOption);

            beatmapOption = new BackgroundOption(BackgroundOptionType.Beatmap, beatmap.Value.GetBackground(), FontAwesome.Solid.Music)
            {
                TooltipText = UserInterfaceStrings.BeatmapBackground,
                Action = () =>
                {
                    if (source.Value != BackgroundSource.Beatmap && source.Value != BackgroundSource.BeatmapWithStoryboard)
                        source.Value = BackgroundSource.Beatmap;

                    seasonalMode.Value = SeasonalBackgroundMode.Never;
                },
            };
            optionsFlow.Add(beatmapOption);

            // seasonal random entry: corresponds to setting "seasonal backgrounds" to "always".
            // its preview cycles through every available seasonal background.
            seasonalRandomOption = new BackgroundOption(BackgroundOptionType.SeasonalRandom, badgeIcon: FontAwesome.Solid.Random)
            {
                TooltipText = seasonalDataAvailable ? UserInterfaceStrings.RandomSeasonalBackground : UserInterfaceStrings.Unavailable,
                Enabled = { Value = seasonalDataAvailable },
                Action = () =>
                {
                    pinnedSeasonal.Value = false;
                    seasonalMode.Value = SeasonalBackgroundMode.Always;
                },
            };
            optionsFlow.Add(seasonalRandomOption);

            if (seasonalDataAvailable)
            {
                int count = seasonalBackgrounds.Value!.Backgrounds!.Count;

                for (int i = 0; i < count; i++)
                {
                    int index = i;

                    var option = new BackgroundOption(BackgroundOptionType.SeasonalPinned)
                    {
                        Index = index,
                        TooltipText = UserInterfaceStrings.PinnedSeasonalBackgroundItem(index + 1),
                        Action = () =>
                        {
                            pinnedSeasonal.Value = true;
                            pinnedSeasonalIndex.Value = index;
                            seasonalMode.Value = SeasonalBackgroundMode.Always;
                        },
                    };

                    pinnedOptions.Add(option);
                    optionsFlow.Add(option);
                }
            }

            beginSeasonalTextureLoad();
        }

        private Texture getSkinTexture() => skinManager.CurrentSkin.Value.GetTexture(@"menu-background")
                                            ?? textures.Get(@"Menu/menu-background-1");

        private void refreshSkinThumbnail() => skinOption?.SetTexture(getSkinTexture());

        private void refreshBeatmapThumbnail() => beatmapOption?.SetTexture(beatmap.Value.GetBackground());

        /// <summary>
        /// Fetches the thumbnails of all available seasonal backgrounds and hands them to the options
        /// as they arrive, so that the previews fill in progressively rather than all at once.
        /// </summary>
        private void beginSeasonalTextureLoad()
        {
            var backgrounds = seasonalBackgrounds.Value?.Backgrounds;

            if (backgrounds == null || backgrounds.Count == 0)
                return;

            int generation = Interlocked.Increment(ref seasonalLoadGeneration);
            string[] urls = backgrounds.Select(b => b.Url).ToArray();

            seasonalRandomOption?.BeginAsyncLoad();

            foreach (var option in pinnedOptions)
                option.BeginAsyncLoad();

            // the texture store blocks internally when a lookup for the same name is already in flight
            // (which happens whenever the menu background is downloading the very same image). Blocking
            // is only permitted from a non-thread-pool thread, so the whole batch gets a dedicated thread.
            Task.Factory.StartNew(() =>
            {
                for (int i = 0; i < urls.Length; i++)
                {
                    if (generation != Volatile.Read(ref seasonalLoadGeneration))
                        return;

                    Texture? pin;

                    try
                    {
                        pin = textures.Get(urls[i]);
                    }
                    catch
                    {
                        // a single failing image should not prevent the rest from being displayed.
                        pin = null;
                    }

                    if (pin == null)
                        continue;

                    string url = urls[i];
                    int index = i;

                    Scheduler.Add(() =>
                    {
                        if (generation != Volatile.Read(ref seasonalLoadGeneration))
                        {
                            pin.Dispose();
                            return;
                        }

                        // held until the options are rebuilt, keeping the image cached and therefore
                        // free (and non-blocking) to look up again for each display.
                        seasonalPins.Add(pin);
                        carouselUrls.Add(url);

                        if (carouselUrls.Count == 1)
                        {
                            carouselIndex = 0;
                            showCarouselImage(url);
                        }

                        if (index < pinnedOptions.Count)
                            pinnedOptions[index].SetTexture(textures.Get(url), url);
                    });
                }

                Scheduler.Add(() =>
                {
                    if (generation != Volatile.Read(ref seasonalLoadGeneration))
                        return;

                    // whatever is still spinning has either failed or is not going to arrive.
                    seasonalRandomOption?.EndAsyncLoad();

                    foreach (var option in pinnedOptions)
                        option.EndAsyncLoad();
                });
            }, TaskCreationOptions.LongRunning);
        }

        private void refreshSelection()
        {
            int availableCount = seasonalBackgrounds.Value?.Backgrounds?.Count ?? 0;
            bool pinnedIndexValid = pinnedSeasonal.Value && pinnedSeasonalIndex.Value >= 0 && pinnedSeasonalIndex.Value < availableCount;

            foreach (var option in optionsFlow.Children.OfType<BackgroundOption>())
            {
                option.Selected = option.Type switch
                {
                    BackgroundOptionType.Skin => source.Value == BackgroundSource.Skin,
                    BackgroundOptionType.Beatmap => source.Value is BackgroundSource.Beatmap or BackgroundSource.BeatmapWithStoryboard,
                    BackgroundOptionType.SeasonalRandom => seasonalDisplayed && !pinnedIndexValid,
                    BackgroundOptionType.SeasonalPinned => seasonalDisplayed && pinnedIndexValid && pinnedSeasonalIndex.Value == option.Index,
                    _ => false,
                };
            }
        }

        private void exportBackground()
        {
            var notification = new ProgressNotification
            {
                State = ProgressNotificationState.Active,
                Text = UserInterfaceStrings.ExportBackground,
                CompletionText = UserInterfaceStrings.ExportBackground,
            };

            notifications.Post(notification);

            void onCompletion(string filename)
            {
                notification.CompletionClickAction = () =>
                {
                    exportStorage.PresentFileExternally(filename);
                    return true;
                };

                notification.CompletionText = UserInterfaceStrings.BackgroundExported(filename);
                notification.Progress = 1;
                notification.State = ProgressNotificationState.Completed;
            }

            void onFailure()
            {
                notification.Text = UserInterfaceStrings.BackgroundExportFailed;
                notification.State = ProgressNotificationState.Cancelled;
            }

            // all exceptions are handled inside, so the task itself can be safely discarded.
            _ = Task.Run(async () =>
            {
                try
                {
                    using Stream? stream = await getCurrentBackgroundStreamAsync().ConfigureAwait(false);

                    if (stream == null)
                    {
                        onFailure();
                        return;
                    }

                    using (stream)
                    using (var image = Image.Load<Rgba32>(stream))
                    {
                        string filename = $"background_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.png";

                        using (var outputStream = exportStorage.GetStream(filename, FileAccess.Write, FileMode.Create))
                            await image.SaveAsPngAsync(outputStream).ConfigureAwait(false);

                        Scheduler.Add(() => onCompletion(filename));
                    }
                }
                catch (Exception)
                {
                    Scheduler.Add(onFailure);
                }
            });
        }

        private async Task<Stream?> getCurrentBackgroundStreamAsync()
        {
            // if a seasonal background is currently displayed, download it directly.
            string? seasonalUrl = defaultScreen?.CurrentSeasonalBackgroundUrl;

            if (seasonalUrl != null)
            {
                var request = new WebRequest(seasonalUrl);
                await request.PerformAsync().ConfigureAwait(false);
                return new MemoryStream(request.GetResponseData() ?? []);
            }

            switch (source.Value)
            {
                case BackgroundSource.Skin when (localUser.Value?.IsSupporter == true):
                {
                    var skin = skinManager.CurrentSkin.Value;

                    // default skins don't provide a menu background; the default rotation is used instead.
                    if (skin is not TrianglesSkin and not ArgonSkin and not DefaultLegacySkin)
                    {
                        var skinStore = new RealmBackedResourceStore<SkinInfo>(skin.SkinInfo, new RealmFileStore(realm, storage).Store, realm);

                        foreach (string extension in (string[])[@".jpg", @".png", @".jpeg"])
                        {
                            var stream = skinStore.GetStream($@"menu-background{extension}");

                            if (stream != null)
                                return stream;
                        }
                    }

                    break;
                }

                case BackgroundSource.Beatmap or BackgroundSource.BeatmapWithStoryboard:
                {
                    var working = beatmap.Value;
                    string? backgroundFile = working.Metadata?.BackgroundFile;

                    if (!string.IsNullOrEmpty(backgroundFile))
                    {
                        string? path = working.BeatmapSetInfo.GetPathForFile(backgroundFile);
                        var stream = working.GetStream(path);

                        if (stream != null)
                            return stream;
                    }

                    break;
                }
            }

            // fall back to the default background rotation.
            return game.Resources.GetStream(@"Menu/menu-background-1");
        }

        private enum BackgroundOptionType
        {
            Skin,
            Beatmap,
            SeasonalRandom,
            SeasonalPinned,
        }

        private partial class BackgroundOption : ClickableContainer, IHasTooltip
        {
            private const float crossfade_duration = 300;

            public BackgroundOptionType Type { get; }
            public int Index { get; init; }

            public LocalisableString TooltipText { get; set; }

            private readonly Sprite firstSprite;
            private readonly Sprite secondSprite;
            private readonly Container selectionBorder;
            private readonly Box hoverLayer;
            private readonly Box flashLayer;
            private readonly LoadingSpinner loadingSpinner;

            /// <summary>
            /// The sprite currently showing an image. Textures are always written into the other one,
            /// as a <see cref="Sprite"/> disposes the texture it replaces.
            /// </summary>
            private Sprite visibleSprite;

            /// <summary>
            /// Identifies the image currently displayed, used to avoid restarting a transition for the same image.
            /// </summary>
            private string? displayedKey;

            [Resolved]
            private OverlayColourProvider colourProvider { get; set; } = null!;

            public BackgroundOption(BackgroundOptionType type, Texture? texture = null, IconUsage? badgeIcon = null)
            {
                Type = type;

                Size = new Vector2(180, background_option_height);
                Masking = true;
                CornerRadius = 6;
                CornerExponent = 2.5f;

                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Color4.Black.Opacity(0.4f),
                    },
                    // two sprites are crossfaded whenever the image changes. A texture instance is never
                    // shared between them (or handed out twice), because assigning a texture to a sprite
                    // disposes the one it replaces.
                    firstSprite = new Sprite
                    {
                        RelativeSizeAxes = Axes.Both,
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        FillMode = FillMode.Fill,
                        Texture = texture,
                    },
                    secondSprite = new Sprite
                    {
                        RelativeSizeAxes = Axes.Both,
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        FillMode = FillMode.Fill,
                        Alpha = 0,
                    },
                    badgeIcon != null ? createBadge(badgeIcon.Value) : Empty(),
                    hoverLayer = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Alpha = 0,
                        Colour = Color4.White.Opacity(0.12f),
                    },
                    loadingSpinner = new LoadingSpinner
                    {
                        Size = new Vector2(24),
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                    },
                    flashLayer = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Alpha = 0,
                        Colour = Color4.White.Opacity(0.25f),
                    },
                    selectionBorder = new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Masking = true,
                        CornerRadius = 6,
                        CornerExponent = 2.5f,
                        BorderThickness = 3,
                        Alpha = 0,
                        Child = new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Alpha = 0
                        },
                    },
                };

                visibleSprite = firstSprite;
            }

            /// <summary>
            /// A small marker in the bottom right corner denoting where the preview's image comes from.
            /// </summary>
            private static Container createBadge(IconUsage icon) => new Container
            {
                Anchor = Anchor.BottomRight,
                Origin = Anchor.BottomRight,
                Size = new Vector2(22),
                Margin = new MarginPadding(6),
                Masking = true,
                CornerRadius = 4,
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Color4.Black.Opacity(0.6f),
                    },
                    new SpriteIcon
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Size = new Vector2(12),
                        Icon = icon,
                    },
                },
            };

            protected override void LoadComplete()
            {
                base.LoadComplete();

                selectionBorder.BorderColour = colourProvider.Highlight1;
            }

            /// <summary>
            /// Show a loading indicator, for textures which are fetched asynchronously.
            /// </summary>
            public void BeginAsyncLoad() => loadingSpinner.Show();

            /// <summary>
            /// Stop the loading indicator without setting a texture (i.e. the fetch failed).
            /// </summary>
            public void EndAsyncLoad() => loadingSpinner.Hide();

            /// <summary>
            /// Set (or replace) the displayed texture, fading between the two images.
            /// The option takes ownership of <paramref name="texture"/>.
            /// </summary>
            /// <param name="texture">The texture to display.</param>
            /// <param name="key">Identifies the image, so that re-showing the current one does not restart a transition.</param>
            public void SetTexture(Texture? texture, string? key = null)
            {
                loadingSpinner.Hide();

                if (texture == null)
                    return;

                // a texture may only ever be owned by a single sprite, as that sprite disposes it once replaced.
                // callers can hand back a texture already on display (i.e. a skin caching its textures); ignore it
                // rather than move it across sprites.
                if (ReferenceEquals(firstSprite.Texture, texture) || ReferenceEquals(secondSprite.Texture, texture))
                    return;

                if (key != null && key == displayedKey)
                {
                    // the store handed out a (new) reference to an image which is already displayed.
                    texture.Dispose();
                    return;
                }

                displayedKey = key;

                Sprite target = ReferenceEquals(visibleSprite, firstSprite) ? secondSprite : firstSprite;

                target.Texture = texture;
                target.Alpha = 0;
                target.FadeIn(crossfade_duration, Easing.OutQuint);

                visibleSprite.FadeOut(crossfade_duration, Easing.OutQuint);
                visibleSprite = target;
            }

            public bool Selected
            {
                set => selectionBorder.FadeTo(value ? 1 : 0, 150, Easing.OutQuint);
            }

            protected override bool OnHover(HoverEvent e)
            {
                if (!Enabled.Value)
                    return base.OnHover(e);

                hoverLayer.FadeIn(120, Easing.OutQuint);
                return true;
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                base.OnHoverLost(e);

                hoverLayer.FadeOut(120, Easing.OutQuint);
            }

            protected override bool OnClick(ClickEvent e)
            {
                if (!base.OnClick(e))
                    return false;

                flashLayer.FadeOutFromOne(300, Easing.OutQuint);
                return true;
            }
        }
    }
}
