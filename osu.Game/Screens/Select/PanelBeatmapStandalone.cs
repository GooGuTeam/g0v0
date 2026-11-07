// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Localisation;
using osu.Framework.Threading;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Drawables;
using osu.Game.Graphics;
using osu.Game.Graphics.Carousel;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays;
using osu.Game.Skinning;
using osu.Game.Resources.Localisation.Web;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Scoring;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Select
{
    public partial class PanelBeatmapStandalone : Panel
    {
        public const float HEIGHT = CarouselItem.DEFAULT_HEIGHT * 1.6f;
        public const float LEGACY_HEIGHT = 101;

        // stable draws menu-button-background (699 x 103) at its own size.
        public const float LEGACY_WIDTH = 699;

        // BeatmapTreeItem.PopulateSprites: text starts 75 + 8 units in, or 75 + 25 while the item shows its rank (x1.6 to lazer units).
        private const float legacy_text_inset = 133;
        private const float legacy_rank_text_shift = 17 * 1.6f;
        private const float legacy_rank_x = 79 * 1.6f;

        // stable's song list thumbnail (BeatmapTreeItem.PopulateSprites): the 160x120 "l" thumb at 1.425 * 0.5 scale, i.e. 71.25 x 53.4 stable units,
        // 5.2 units in from the panel's left edge and 0.25 below its centre (x1.6 to lazer units).
        private const float legacy_thumbnail_x = 5.2f * 1.6f;
        private const float legacy_thumbnail_y = 0.25f * 1.6f;
        private const float legacy_thumbnail_width = 114;
        private const float legacy_thumbnail_height = 85.5f;

        // BeatmapTreeItem's background colours: a collapsed set is orange once played (pink while new), the expanded set's difficulties are blue,
        // and the selected difficulty is white.
        private static readonly Color4 legacy_colour_played = new Color4(233, 104, 0, 240);
        private static readonly Color4 legacy_colour_unplayed = new Color4(235, 73, 153, 240);
        private static readonly Color4 legacy_colour_expanded = new Color4(0, 150, 236, 240);
        private static readonly Color4 legacy_colour_selected = new Color4(255, 255, 255, 220);

        // the thumbnail is dimmed and additive until its set is expanded.
        private static readonly Color4 legacy_thumbnail_inactive = new Color4(50, 50, 50, 255);

        [Resolved]
        private IBindable<RulesetInfo> ruleset { get; set; } = null!;

        [Resolved]
        private IBindable<IReadOnlyList<Mod>> mods { get; set; } = null!;

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;

        [Resolved]
        private ISongSelect? songSelect { get; set; }

        [Resolved]
        private BeatmapManager beatmaps { get; set; } = null!;

        [Resolved]
        private BeatmapDifficultyCache difficultyCache { get; set; } = null!;

        private IBindable<StarDifficulty>? starDifficultyBindable;
        private CancellationTokenSource? starDifficultyCancellationSource;

        private PanelSetBackground beatmapBackground = null!;
        private ScheduledDelegate? scheduledBackgroundRetrieval;

        private OsuSpriteText titleText = null!;
        private OsuSpriteText artistText = null!;
        private PanelUpdateBeatmapButton updateButton = null!;
        private BeatmapSetOnlineStatusPill statusPill = null!;

        private ConstrainedIconContainer difficultyIcon = null!;
        private StarRatingDisplay starRatingDisplay = null!;
        private SpreadDisplay spreadDisplay = null!;
        private PanelLocalRankDisplay localRank = null!;
        private OsuSpriteText keyCountText = null!;
        private OsuSpriteText difficultyText = null!;
        private OsuSpriteText authorText = null!;
        private FillFlowContainer mainFill = null!;

        private Box backgroundBorder = null!;

        [Resolved]
        private ISkinSource skinSource { get; set; } = null!;

        [Resolved]
        private SkinManager skins { get; set; } = null!;

        private Container modernContent = null!;
        private Container legacyContent = null!;
        private Sprite legacyBackground = null!;
        private OsuSpriteText legacyTitleText = null!;
        private OsuSpriteText legacyArtistText = null!;
        private OsuSpriteText legacyDifficultyText = null!;
        private LegacyPanelThumbnail legacyThumbnail = null!;
        private Container legacyDetails = null!;
        private Container legacyMetadata = null!;
        private Container legacyDifficulty = null!;
        private Sprite legacyRank = null!;
        private ScoreRank? legacyDisplayedRank;
        private bool legacyRankValid;
        private readonly List<Sprite> legacyStars = new List<Sprite>();
        private readonly List<Sprite> legacyStarBackgrounds = new List<Sprite>();
        private double legacyStarCount;

        // the stars shown (-1 while hidden), the background colour faded to, and whether the thumbnail shows its set expanded.
        private double legacyDisplayedStars = -1;
        private Color4 legacyBackgroundColour;
        private Color4 legacyActiveText = Color4.Black;
        private Color4 legacyInactiveText = Color4.White;
        private bool? legacyThumbnailExpanded;

        // set when the panel is taken from the pool, so the states it shows in its first frame apply instantly (stable loads items scrolled into view instantly).
        private bool legacyJustBound;

        private BeatmapInfo beatmap => ((GroupedBeatmap)Item!.Model).Beatmap;

        public PanelBeatmapStandalone()
        {
            PanelXOffset = 20;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Height = HEIGHT;

            Icon = difficultyIcon = new ConstrainedIconContainer
            {
                Size = new Vector2(12),
                Margin = new MarginPadding { Left = 4f, Right = 3f },
                Colour = colourProvider.Background5,
            };

            Background = backgroundBorder = new Box
            {
                RelativeSizeAxes = Axes.Both,
            };

            Content.Children = new Drawable[]
            {
                legacyContent = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Alpha = 0,
                    Children = new Drawable[]
                    {
                        legacyBackground = new Sprite
                        {
                            RelativeSizeAxes = Axes.Both,
                            FillMode = FillMode.Stretch,
                        },
                        legacyThumbnail = new LegacyPanelThumbnail
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Position = new Vector2(legacy_thumbnail_x, legacy_thumbnail_y),
                            Size = new Vector2(legacy_thumbnail_width, legacy_thumbnail_height),
                        },
                        legacyMetadata = new Container
                        {
                            RelativeSizeAxes = Axes.Both,
                            Children = new Drawable[]
                            {
                                legacyTitleText = createLegacyLabel(25.6f, FontWeight.Regular, -25.6f),
                                legacyArtistText = createLegacyLabel(19.2f, FontWeight.Regular, -6.4f),
                            },
                        },
                        legacyDetails = new Container
                        {
                            RelativeSizeAxes = Axes.Both,
                            Children = new Drawable[]
                            {
                                legacyRank = new Sprite
                                {
                                    Anchor = Anchor.CentreLeft,
                                    Origin = Anchor.CentreLeft,
                                    X = legacy_rank_x,
                                },
                                legacyDifficulty = new Container
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Children = new Drawable[]
                                    {
                                        legacyDifficultyText = createLegacyLabel(19.2f, FontWeight.Bold, 11.2f),
                                        createLegacyStarRow(),
                                    },
                                },
                            },
                        },
                    },
                },
                modernContent = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Children = new Drawable[]
                    {
                beatmapBackground = new PanelSetBackground(),
                new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Spacing = new Vector2(5),
                    Margin = new MarginPadding { Left = 6.5f },
                    Direction = FillDirection.Horizontal,
                    Children = new Drawable[]
                    {
                        localRank = new PanelLocalRankDisplay
                        {
                            Scale = new Vector2(0.8f),
                            Origin = Anchor.CentreLeft,
                            Anchor = Anchor.CentreLeft,
                        },
                        mainFill = new FillFlowContainer
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Direction = FillDirection.Vertical,
                            Padding = new MarginPadding { Bottom = 4.8f },
                            AutoSizeAxes = Axes.Both,
                            Children = new Drawable[]
                            {
                                titleText = new OsuSpriteText
                                {
                                    Font = OsuFont.Style.Heading2.With(typeface: Typeface.MapleMono, weight: FontWeight.Bold),
                                },
                                artistText = new OsuSpriteText
                                {
                                    Font = OsuFont.Style.Caption1.With(weight: FontWeight.SemiBold),
                                    Padding = new MarginPadding { Top = -2 },
                                },
                                new FillFlowContainer
                                {
                                    Direction = FillDirection.Horizontal,
                                    AutoSizeAxes = Axes.Both,
                                    Padding = new MarginPadding { Top = 2, Bottom = 2 },
                                    Children = new Drawable[]
                                    {
                                        statusPill = new BeatmapSetOnlineStatusPill
                                        {
                                            Animated = false,
                                            Origin = Anchor.BottomLeft,
                                            Anchor = Anchor.BottomLeft,
                                            TextSize = OsuFont.Style.Caption2.Size,
                                            Margin = new MarginPadding { Right = 4f },
                                        },
                                        updateButton = new PanelUpdateBeatmapButton
                                        {
                                            Scale = new Vector2(0.8f),
                                            Anchor = Anchor.BottomLeft,
                                            Origin = Anchor.BottomLeft,
                                            Margin = new MarginPadding { Right = 4f, Bottom = -1f },
                                        },
                                        keyCountText = new OsuSpriteText
                                        {
                                            Font = OsuFont.Style.Body.With(weight: FontWeight.SemiBold),
                                            Anchor = Anchor.BottomLeft,
                                            Origin = Anchor.BottomLeft,
                                            Alpha = 0,
                                        },
                                        difficultyText = new OsuSpriteText
                                        {
                                            Font = OsuFont.Style.Body.With(weight: FontWeight.SemiBold),
                                            Anchor = Anchor.BottomLeft,
                                            Origin = Anchor.BottomLeft,
                                            Margin = new MarginPadding { Right = 3f },
                                        },
                                        authorText = new OsuSpriteText
                                        {
                                            Colour = colourProvider.Content2,
                                            Font = OsuFont.Style.Caption1.With(weight: FontWeight.SemiBold),
                                            Anchor = Anchor.BottomLeft,
                                            Origin = Anchor.BottomLeft
                                        }
                                    }
                                },
                                new FillFlowContainer
                                {
                                    Direction = FillDirection.Horizontal,
                                    Spacing = new Vector2(3),
                                    AutoSizeAxes = Axes.Both,
                                    Children = new Drawable[]
                                    {
                                        starRatingDisplay = new StarRatingDisplay(default, StarRatingDisplaySize.Small, animated: true)
                                        {
                                            Origin = Anchor.CentreLeft,
                                            Anchor = Anchor.CentreLeft,
                                            Scale = new Vector2(0.875f),
                                        },
                                        spreadDisplay = new SpreadDisplay
                                        {
                                            Origin = Anchor.CentreLeft,
                                            Anchor = Anchor.CentreLeft,
                                            Selected = { BindTarget = Selected },
                                        }
                                    },
                                }
                            }
                        }
                    }
                }
                    },
                }
            };
        }

        private static OsuSpriteText createLegacyLabel(float size, FontWeight weight, float y) => new OsuSpriteText
        {
            Anchor = Anchor.CentreLeft,
            Origin = Anchor.CentreLeft,
            Position = new Vector2(legacy_text_inset, y),
            Font = LegacyFonts.Get(size, weight),
        };

        // the skin's star at 0.6 scale, ten in a row 18 units below the centre: dim additive copies at 0.35 behind, and the filled stars
        // scaled by how much of each star the rating covers (BeatmapTreeItem.UpdateSprites, UpdateStarDifficultyDisplay).
        private Container createLegacyStarRow()
        {
            var stars = new Container
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                Position = new Vector2(legacy_text_inset, 28.8f),
            };

            for (int i = 0; i < 10; i++)
            {
                var background = createLegacyStar();
                background.Blending = BlendingParameters.Additive;
                background.Colour = new Color4(1f, 1f, 1f, 30f / 255);
                background.Scale = new Vector2(0.35f);
                legacyStarBackgrounds.Add(background);
                stars.Add(background);

                var filled = createLegacyStar();
                filled.Scale = Vector2.Zero;
                legacyStars.Add(filled);
                stars.Add(filled);
            }

            return stars;
        }

        private static Sprite createLegacyStar() => new Sprite
        {
            Anchor = Anchor.CentreLeft,
            Origin = Anchor.Centre,
        };

        private void updateLegacyStarTexture()
        {
            var texture = skinSource.GetStableTexture(@"star") ?? skins.DefaultClassicSkin.GetStableTexture(@"star");
            var size = texture == null ? Vector2.Zero : new Vector2(texture.DisplayWidth, texture.DisplayHeight) * 0.6f;

            for (int i = 0; i < legacyStars.Count; i++)
            {
                foreach (var star in new[] { legacyStars[i], legacyStarBackgrounds[i] })
                {
                    star.Texture = texture;
                    star.Size = size;
                    star.X = (i + 0.5f) * size.X;
                }
            }
        }

        private void updateLegacyRank()
        {
            var rank = localRank.Rank;

            if (legacyRankValid && rank == legacyDisplayedRank)
                return;

            legacyRankValid = true;
            legacyDisplayedRank = rank;

            var texture = rank == null ? null : skinSource.GetStableTexture($@"ranking-{rank}-small") ?? skins.DefaultClassicSkin.GetStableTexture($@"ranking-{rank}-small");
            legacyRank.Texture = texture;
            legacyRank.Size = texture == null ? Vector2.Zero : new Vector2(texture.DisplayWidth, texture.DisplayHeight);

            updateLegacyEntryState();
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            ruleset.BindValueChanged(_ => updateKeyCount());
            mods.BindValueChanged(_ => updateKeyCount(), true);

            Selected.BindValueChanged(s =>
            {
                // while legacy song select collapses sets, the carousel expands the selected set's difficulties itself.
                if (!LegacySetsCollapsed)
                    Expanded.Value = s.NewValue;
                updateLegacyEntryState();
            }, true);

            Expanded.BindValueChanged(_ => updateLegacyEntryState());

            skinSource.SourceChanged += updateLegacyBackground;
            updateLegacyBackground();
        }

        protected override void PrepareForUse()
        {
            base.PrepareForUse();

            var beatmapSet = beatmap.BeatmapSet!;

            legacyJustBound = true;
            legacyDisplayedStars = -1;
            legacyThumbnailExpanded = null;
            legacyRankValid = false;

            updateLegacyBackground();

            scheduledBackgroundRetrieval = Scheduler.AddDelayed(b =>
            {
                var working = beatmaps.GetWorkingBeatmap(b);
                beatmapBackground.Beatmap = working;
                legacyThumbnail.Beatmap = working;
            }, beatmap, 50);

            titleText.Text = new RomanisableString(beatmapSet.Metadata.TitleUnicode, beatmapSet.Metadata.Title);
            artistText.Text = new RomanisableString(beatmapSet.Metadata.ArtistUnicode, beatmapSet.Metadata.Artist);
            legacyTitleText.Text = new RomanisableString(beatmapSet.Metadata.TitleUnicode, beatmapSet.Metadata.Title);
            legacyArtistText.Text = new RomanisableString(beatmapSet.Metadata.ArtistUnicode, beatmapSet.Metadata.Artist) + $" // {beatmap.Metadata.Author.Username}";
            legacyDifficultyText.Text = beatmap.DifficultyName;
            updateButton.BeatmapSet = beatmapSet;
            statusPill.Status = beatmap.Status;

            difficultyIcon.Icon = beatmap.Ruleset.CreateInstance().CreateIcon();
            difficultyIcon.Show();

            localRank.Beatmap = beatmap;
            difficultyText.Text = beatmap.DifficultyName;
            authorText.Text = BeatmapsetsStrings.ShowDetailsMappedBy(beatmap.Metadata.Author.Username);

            computeStarRating();
            spreadDisplay.Beatmap.Value = beatmap;
            updateKeyCount();
        }

        protected override void FreeAfterUse()
        {
            base.FreeAfterUse();

            scheduledBackgroundRetrieval?.Cancel();
            scheduledBackgroundRetrieval = null;
            beatmapBackground.Beatmap = null;
            legacyThumbnail.Beatmap = null;
            updateButton.BeatmapSet = null;
            localRank.Beatmap = null;
            starDifficultyBindable = null;
            spreadDisplay.Beatmap.Value = null;

            starDifficultyCancellationSource?.Cancel();
        }

        private void computeStarRating()
        {
            starDifficultyCancellationSource?.Cancel();
            starDifficultyCancellationSource = new CancellationTokenSource();

            if (Item == null)
                return;

            starDifficultyBindable = difficultyCache.GetBindableDifficulty(beatmap, starDifficultyCancellationSource.Token, SongSelect.DIFFICULTY_CALCULATION_DEBOUNCE);
            starDifficultyBindable.BindValueChanged(starDifficulty =>
            {
                starRatingDisplay.Current.Value = starDifficulty.NewValue;
                spreadDisplay.StarDifficulty.Value = starDifficulty.NewValue;
                legacyStarCount = starDifficulty.NewValue.Stars;
                updateLegacyEntryState();
            }, true);
        }

        protected override void Update()
        {
            base.Update();

            if (Item?.IsVisible != true)
            {
                starDifficultyCancellationSource?.Cancel();
                starDifficultyCancellationSource = null;
            }

            // Dirty hack to make sure we don't take up spacing in parent fill flow when not displaying a rank.
            // I can't find a better way to do this.
            mainFill.Margin = new MarginPadding { Left = 1 / starRatingDisplay.Scale.X * (localRank.HasRank ? 0 : -3) };

            var diffColour = starRatingDisplay.DisplayedDifficultyColour;

            AccentColour = diffColour;
            spreadDisplay.Current.Colour = diffColour;

            backgroundBorder.Colour = diffColour;
            difficultyIcon.Colour = starRatingDisplay.DisplayedDifficultyTextColour;

            if (IsLegacySongSelectAppearance)
            {
                backgroundBorder.Alpha = 0;
                updateLegacyRank();
            }
            else
                backgroundBorder.Alpha = 1;

            legacyJustBound = false;
        }

        private void updateKeyCount()
        {
            if (Item == null)
                return;

            var rulesetInstance = ruleset.Value.CreateInstance();

            if (rulesetInstance.GameplayVariants.Count() > 1)
            {
                int variant = rulesetInstance.GetVariantForBeatmap(beatmap, mods.Value);
                var variantName = rulesetInstance.GetVariantName(variant);

                keyCountText.Alpha = 1;
                keyCountText.Text = LocalisableString.Interpolate($"[{variantName}] ");

                // stable appends the key count: "Version (7K)".
                legacyDifficultyText.Text = LocalisableString.Interpolate($"{beatmap.DifficultyName} ({variantName})");
            }
            else
            {
                keyCountText.Alpha = 0;
                legacyDifficultyText.Text = beatmap.DifficultyName;
            }
        }

        protected override void OnLegacyAppearanceChanged(bool legacy)
        {
            // Renderer ownership is atomic, including when a pooled row is first presented.
            // Entrance animations belong to the panel, not to two overlapping presentations.
            legacyContent.FadeTo(legacy ? 1 : 0);
            modernContent.FadeTo(legacy ? 0 : 1);
            beatmapBackground.FadeTo(legacy ? 0 : 1);
            backgroundBorder.FadeTo(legacy ? 0 : 1);

            if (legacy)
                updateLegacyEntryState();
        }

        private void updateLegacyBackground()
        {
            if (legacyBackground == null || skinSource == null || skins == null)
                return;

            legacyBackground.Texture = skinSource.GetStableTexture("menu-button-background") ?? skins.DefaultClassicSkin.GetStableTexture("menu-button-background");

            // the skin's SongSelectActiveText / SongSelectInactiveText (black and white by default).
            legacyActiveText = skinSource.GetConfig<SkinCustomColourLookup, Color4>(new SkinCustomColourLookup(@"SongSelectActiveText"))?.Value ?? Color4.Black;
            legacyInactiveText = skinSource.GetConfig<SkinCustomColourLookup, Color4>(new SkinCustomColourLookup(@"SongSelectInactiveText"))?.Value ?? Color4.White;
            updateLegacyStarTexture();

            // the skin may have its own rank textures.
            legacyRankValid = false;

            updateLegacyEntryState();
        }

        // stable flashes a newly hovered item's background a little lighter (BeatmapTreeManager.UpdateItemPositions, ColourHelper.Lighten2).
        protected override void OnLegacyHovered()
        {
            if (Item == null)
                return;

            const float amount = 0.3f * 0.5f;
            var c = legacyBackgroundColour;

            legacyBackground.FlashColour(new Color4(
                Math.Min(1, c.R * (1 + 0.5f * amount) + amount),
                Math.Min(1, c.G * (1 + 0.5f * amount) + amount),
                Math.Min(1, c.B * (1 + 0.5f * amount) + amount),
                c.A), 1000);
        }

        private void updateLegacyEntryState()
        {
            if (legacyTitleText == null || Item == null)
                return;

            // stable's tree item states (BeatmapTreeItem.UpdateSprites): the selected difficulty (active), the other difficulties of the
            // expanded set, a single difficulty that shows its details (when sets are not collapsed), and a collapsed set.
            bool active = Selected.Value;
            bool expanded = active || Expanded.Value;
            bool showDetails = expanded || !LegacySetsCollapsed;

            // a panel just taken from the pool shows its state at once, unless it belongs to the expanded set (stable loads those animated).
            bool instant = legacyJustBound && !expanded;
            double duration = instant ? 0 : 300;

            // the text moves right to make room for the rank while the item shows its details (UpdateHorizontalLayout).
            float textShift = showDetails && legacyRank.Texture != null ? legacy_rank_text_shift : 0;
            legacyMetadata.X = legacyDifficulty.X = textShift;

            // SongSelectActiveText / SongSelectInactiveText; the expanded set fades its title and artist to 50 alpha.
            Color4 textColour = active ? legacyActiveText : legacyInactiveText;
            Color4 metadataColour = active ? legacyActiveText : expanded ? new Color4(legacyInactiveText.R, legacyInactiveText.G, legacyInactiveText.B, 50f / 255) : legacyInactiveText;
            legacyTitleText.Colour = legacyArtistText.Colour = metadataColour;
            legacyDifficultyText.Colour = textColour;

            legacyDetails.FadeTo(showDetails ? 1 : 0, duration);

            // the thumbnail fades in afresh over a second when its set expands, and dims back to additive grey when it collapses.
            if (legacyThumbnailExpanded != expanded)
            {
                if (expanded && legacyThumbnailExpanded != null && !instant)
                    legacyThumbnail.FadeInFromZero(1000);

                legacyThumbnailExpanded = expanded;
                legacyThumbnail.Blending = expanded ? BlendingParameters.Inherit : BlendingParameters.Additive;
                legacyThumbnail.FadeColour(expanded ? Color4.White : legacy_thumbnail_inactive, duration);
            }

            foreach (var star in legacyStars)
                star.Colour = textColour;

            updateLegacyStars(showDetails, instant);

            legacyBackgroundColour = active ? legacy_colour_selected
                : expanded ? legacy_colour_expanded
                : beatmap.LastPlayed != null ? legacy_colour_played : legacy_colour_unplayed;

            // the item picked with the keyboard (but not yet selected) is lightened (BeatmapTreeManager.UpdateKeyHoverColour, ColourHelper.Lighten).
            bool keyboardHovered = KeyboardSelected.Value && !active;

            if (keyboardHovered)
            {
                var c = legacyBackgroundColour;
                legacyBackgroundColour = new Color4(Math.Min(1, c.R * 1.4f), Math.Min(1, c.G * 1.4f), Math.Min(1, c.B * 1.4f), c.A);
            }

            legacyBackground?.FadeColour(legacyBackgroundColour, keyboardHovered && !instant ? 50 : duration);
        }

        protected override void OnLegacyKeyboardSelectedChanged() => updateLegacyEntryState();

        /// <summary>
        /// Scales the filled stars to the rating. Each star past the first grows a little more, and each one that changes eases out with a little
        /// overshoot 80ms after the one before it, from the lowest changed star up, or from the highest down (BeatmapTreeItem.UpdateStarDifficultyDisplay).
        /// </summary>
        private void updateLegacyStars(bool shown, bool instant)
        {
            if (!shown)
            {
                // stable drops the star sprites while the details are hidden, so they grow from nothing when shown again.
                if (legacyDisplayedStars >= 0)
                {
                    foreach (var star in legacyStars)
                    {
                        star.ClearTransforms(targetMember: nameof(Scale));
                        star.Scale = Vector2.Zero;
                    }
                }

                legacyDisplayedStars = -1;
                return;
            }

            double stars = Math.Clamp(legacyStarCount, 0, 10);

            if (legacyDisplayedStars == stars)
                return;

            double previous = legacyDisplayedStars;
            legacyDisplayedStars = stars;

            if (previous < 0)
            {
                foreach (var background in legacyStarBackgrounds)
                    background.FadeInFromZero(instant ? 0 : 600);
            }

            for (int i = 0; i < legacyStars.Count; i++)
            {
                double fraction = stars - i;
                // the filled star's scale on top of its 0.6 size: 0.6 for a whole star, at least half that for part of one.
                float target = fraction <= 0 ? 0 : 0.6f * (float)Math.Max(0.5, Math.Min(1, fraction) + i * 0.04);
                var star = legacyStars[i];

                star.ClearTransforms(targetMember: nameof(Scale));

                if (instant)
                {
                    star.Scale = new Vector2(target);
                    continue;
                }

                int fromLowest = (int)Math.Floor(i - Math.Min(stars, previous));
                int order = previous <= stars ? fromLowest : (int)Math.Floor(previous - stars) - fromLowest - 1;

                star.Delay(Math.Max(0, order * 80 + 50)).ScaleTo(target, 500, Easing.OutBack);
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            if (skinSource != null)
                skinSource.SourceChanged -= updateLegacyBackground;

            base.Dispose(isDisposing);
        }

        public override MenuItem[] ContextMenuItems
        {
            get
            {
                if (Item == null)
                    return Array.Empty<MenuItem>();

                List<MenuItem> items = new List<MenuItem>();

                if (songSelect != null)
                    items.AddRange(songSelect.GetForwardActions(beatmap));

                return items.ToArray();
            }
        }
    }
}
