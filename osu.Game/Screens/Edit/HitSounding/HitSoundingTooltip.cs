// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Localisation;
using osu.Framework.Utils;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Localisation;
using osu.Game.Overlays;
using osuTK;

namespace osu.Game.Screens.Edit.HitSounding
{
    /// <summary>
    /// The set of shortcut hints a HitSounding hover target can display.
    /// </summary>
    public enum HitSoundingTooltipKind
    {
        Bank,
        Addition,
        DisabledAddition,
        Volume,
        VolumeSplit,
        Alias
    }

    /// <summary>
    /// A shortcut tooltip matching the HitSounding editor hover panels, with one keycap per shortcut row.
    /// </summary>
    /// <remarks>
    /// A tooltip instance is cached by <see cref="TooltipContainer"/> for its target and refreshed through
    /// <see cref="SetContent"/>. The hover target changing between two HitSounding elements of the same kind therefore
    /// reuses this instance, so the kind is also resolved from the hint text passed to <see cref="SetContent"/>.
    /// </remarks>
    public partial class HitSoundingTooltip : TooltipContainer.Tooltip
    {
        /// <summary>
        /// The widest a shortcut row's description is allowed to be before wrapping (without a keycap).
        /// </summary>
        private const float text_width = 500;

        /// <summary>
        /// The widest a shortcut row's description is allowed to be before wrapping (with a keycap).
        /// </summary>
        private const float keycap_text_width = 440;

        private static readonly OverlayColourProvider colour_provider = new OverlayColourProvider(OverlayColourScheme.Aquamarine);

        private readonly Box background;
        private readonly FillFlowContainer rows;

        private Colour4 textColour;
        private Colour4 keycapBackground;
        private Colour4 keycapTextColour;

        private HitSoundingTooltipKind kind;
        private bool instantMovement = true;

        public HitSoundingTooltip(HitSoundingTooltipKind kind)
        {
            this.kind = kind;

            AutoSizeAxes = Axes.Both;
            Masking = true;
            CornerRadius = 5;
            EdgeEffect = new EdgeEffectParameters
            {
                Type = EdgeEffectType.Shadow,
                Colour = Colour4.Black.Opacity(0.4f),
                Radius = 5,
            };

            Children = new Drawable[]
            {
                background = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                },
                rows = new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Vertical,
                    Padding = new MarginPadding(5),
                    Spacing = new Vector2(0, 5),
                },
            };
        }

        [BackgroundDependencyLoader]
        private void load(OsuColour colours)
        {
            background.Colour = colours.Gray3;
            background.Alpha = 0.9f;

            textColour = colour_provider.Content1;
            keycapBackground = colour_provider.Background4;
            keycapTextColour = colour_provider.Light2;

            rebuildRows();
        }

        /// <summary>
        /// Updates the displayed shortcuts. The hint text identifies which set of shortcuts to display, allowing a
        /// cached tooltip to follow a hover target whose shortcuts changed (for example while Ctrl is held over the
        /// volume graph).
        /// </summary>
        public override void SetContent(LocalisableString content)
        {
            HitSoundingTooltipKind? resolved = resolveKind(content);

            if (resolved == null || resolved == kind)
                return;

            kind = resolved.Value;

            if (IsLoaded)
                rebuildRows();
        }

        private static HitSoundingTooltipKind? resolveKind(LocalisableString content)
        {
            if (content.Equals(HitSoundingStrings.BankHint))
                return HitSoundingTooltipKind.Bank;

            if (content.Equals(HitSoundingStrings.AdditionHint))
                return HitSoundingTooltipKind.Addition;

            if (content.Equals(HitSoundingStrings.ConfigureAdditionHint))
                return HitSoundingTooltipKind.DisabledAddition;

            if (content.Equals(HitSoundingStrings.VolumeHint))
                return HitSoundingTooltipKind.Volume;

            if (content.Equals(HitSoundingStrings.CtrlSplitHint))
                return HitSoundingTooltipKind.VolumeSplit;

            if (content.Equals(HitSoundingStrings.TooltipFileTileAlias))
                return HitSoundingTooltipKind.Alias;

            return null;
        }

        private void rebuildRows()
        {
            rows.Clear();

            switch (kind)
            {
                case HitSoundingTooltipKind.Bank:
                    rows.Add(createTextRow(HitSoundingStrings.TooltipClickEditBank));
                    rows.Add(createKeycapRow(@"CTRL+M1", HitSoundingStrings.TooltipSelectSamplePoint));
                    rows.Add(createTextRow(HitSoundingStrings.TooltipRestoreDefaults));
                    rows.Add(createKeycapRow(@"CTRL+WHEEL", HitSoundingStrings.TooltipCycleSampleSets));
                    break;

                case HitSoundingTooltipKind.Addition:
                    rows.Add(createTextRow(HitSoundingStrings.TooltipToggleAddition));
                    rows.Add(createKeycapRow(@"CTRL+M1", HitSoundingStrings.TooltipSelectSamplePoint));
                    break;

                case HitSoundingTooltipKind.DisabledAddition:
                    rows.Add(createTextRow(HitSoundingStrings.ConfigureAdditionHint));
                    break;

                case HitSoundingTooltipKind.Volume:
                    rows.Add(createTextRow(HitSoundingStrings.TooltipDragVolume));
                    rows.Add(createTextRow(HitSoundingStrings.TooltipDoubleClickVolume));
                    rows.Add(createKeycapRow(@"CTRL+M1", HitSoundingStrings.TooltipEditVolumeFromPoint));
                    break;

                case HitSoundingTooltipKind.VolumeSplit:
                    rows.Add(createKeycapRow(@"CTRL+M1", HitSoundingStrings.TooltipEditVolumeFromPointOnwards));
                    break;

                case HitSoundingTooltipKind.Alias:
                    rows.Add(createTextRow(HitSoundingStrings.TooltipFileTileAlias));
                    rows.Add(createKeycapRow(HitSoundingStrings.AliasConfirmHotkey, HitSoundingStrings.AliasConfirm));
                    break;
            }
        }

        private Drawable createTextRow(LocalisableString text) => new OsuTextFlowContainer(t =>
        {
            // FontUsage sizes describe line height; the Figma 16px face has a 21px line box.
            t.Font = OsuFont.GetFont(size: 21, weight: FontWeight.Regular);
            t.Colour = textColour;
        })
        {
            AutoSizeAxes = Axes.Both,
            MaximumSize = new Vector2(text_width, float.PositiveInfinity),
            Text = text,
        };

        private Drawable createKeycapRow(LocalisableString keycapText, LocalisableString text)
        {
            var keycap = new Container
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                AutoSizeAxes = Axes.Both,
                Masking = true,
                CornerRadius = 3,
                Margin = new MarginPadding { Right = 5 },
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = keycapBackground,
                    },
                    new OsuSpriteText
                    {
                        Text = keycapText,
                        Font = OsuFont.GetFont(size: 16, weight: FontWeight.Bold),
                        Colour = keycapTextColour,
                        Margin = new MarginPadding { Horizontal = 5, Bottom = 1 },
                    },
                }
            };

            return new GridContainer
            {
                AutoSizeAxes = Axes.Both,
                ColumnDimensions = new[]
                {
                    new Dimension(GridSizeMode.AutoSize),
                    new Dimension(GridSizeMode.AutoSize),
                },
                RowDimensions = new[] { new Dimension(GridSizeMode.AutoSize) },
                Content = new[]
                {
                    new Drawable[]
                    {
                        keycap,
                        new OsuTextFlowContainer(t =>
                        {
                            t.Font = OsuFont.GetFont(size: 21, weight: FontWeight.Regular);
                            t.Colour = textColour;
                        })
                        {
                            AutoSizeAxes = Axes.Both,
                            MaximumSize = new Vector2(keycap_text_width, float.PositiveInfinity),
                            Text = text,
                        },
                    },
                },
            };
        }

        protected override void PopIn()
        {
            instantMovement |= !IsPresent;
            this.FadeIn(300, Easing.OutQuint);
        }

        protected override void PopOut() => this.Delay(150).FadeOut(300, Easing.OutQuint);

        public override void Move(Vector2 pos)
        {
            if (instantMovement)
            {
                Position = pos;
                instantMovement = false;
            }
            else
            {
                // This method is called every frame so we can do this safely here.
                Position = Interpolation.ValueAt(Time.Elapsed, Position, pos, 0, 120, Easing.OutQuint);
            }
        }
    }
}
