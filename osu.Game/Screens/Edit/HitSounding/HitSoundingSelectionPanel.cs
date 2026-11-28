// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Framework.Utils;
using osu.Game.Audio;
using osu.Game.Extensions;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Overlays;
using osu.Game.Rulesets.Edit;
using osu.Game.Rulesets.Objects;
using osuTK;

namespace osu.Game.Screens.Edit.HitSounding
{
    /// <summary>
    /// Edits the bank and additions of the selected hitsound nodes, mirroring the values shown by the sample point cells.
    /// </summary>
    /// <remarks>
    /// Every field edits all <see cref="HitSoundingEditModel.SelectedNodes"/> at once. Unsupported nodes are skipped by the
    /// model, so the controls are only disabled when no selected node supports the edit at all. The displayed values follow the
    /// first selected node, matching the editor's batch-edit convention, instead of inventing a mixed-state label.
    /// </remarks>
    public partial class HitSoundingSelectionPanel : CompositeDrawable
    {
        private const float panel_padding = 10;
        private const float section_spacing = 10;
        private const float label_spacing = 4;
        private const float toggle_height = 64;
        private const float toggle_spacing = 5;
        private const float time_label_size = 14;
        private const float time_value_size = 20;

        private static readonly string[] addition_banks = [HitObjectComposer.HIT_BANK_AUTO, .. HitSampleInfo.ALL_BANKS];

        private readonly HitSoundingEditModel model;

        private TruncatingSpriteText timeValue = null!;
        private SetDropdown setDropdown = null!;
        private BankDropdown bankDropdown = null!;
        private AdditionBankDropdown additionDropdown = null!;
        private AdditionToggleButton whistleToggle = null!;
        private AdditionToggleButton finishToggle = null!;
        private AdditionToggleButton clapToggle = null!;

        private int[] setItems = [];

        private EditorBeatmapSkin? subscribedSkin;

        private bool refreshing;
        private bool refreshQueued;

        [Resolved]
        private EditorBeatmap beatmap { get; set; } = null!;

        [Resolved(canBeNull: true)]
        private HitSoundingSampleLibrary? library { get; set; }

        public HitSoundingSelectionPanel(HitSoundingEditModel model)
        {
            this.model = model;
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colours, OsuColour osuColour)
        {
            Masking = true;
            CornerRadius = 5;

            InternalChildren = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = colours.Background5
                },
                new OsuScrollContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    // The scrollbar overlays the panel's own right padding so the 180px controls keep their designed width.
                    ScrollbarOverlapsContent = true,
                    Child = new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Spacing = new Vector2(0, section_spacing),
                        Padding = new MarginPadding(panel_padding),
                        Children =
                        [
                            createTimeBlock(colours),
                            new Tooltipped(
                                setDropdown = new SetDropdown
                                {
                                    Caption = HitSoundingStrings.SetCaption,
                                    Items = new[] { 0, 1 },
                                },
                                new SelectionHint(HitSoundingStrings.TooltipSelectedSet)),
                            new Tooltipped(
                                bankDropdown = new BankDropdown
                                {
                                    Caption = EditorStrings.NormalBank,
                                    Items = HitSampleInfo.ALL_BANKS,
                                },
                                new SelectionHint(HitSoundingStrings.TooltipSelectedBank,
                                    (@"SHIFT+W", HitSoundingStrings.Normal),
                                    (@"SHIFT+E", HitSoundingStrings.Soft),
                                    (@"SHIFT+R", HitSoundingStrings.Drum))),
                            new Tooltipped(
                                additionDropdown = new AdditionBankDropdown
                                {
                                    Caption = EditorStrings.AdditionBank,
                                    Items = addition_banks,
                                },
                                new SelectionHint(HitSoundingStrings.TooltipSelectedAddition,
                                    (@"ALT+Q", HitSoundingStrings.Auto),
                                    (@"ALT+W", HitSoundingStrings.Normal),
                                    (@"ALT+E", HitSoundingStrings.Soft),
                                    (@"ALT+R", HitSoundingStrings.Drum))),
                            new GridContainer
                            {
                                RelativeSizeAxes = Axes.X,
                                Height = toggle_height,
                                ColumnDimensions = new[]
                                {
                                    new Dimension(GridSizeMode.Distributed),
                                    new Dimension(GridSizeMode.Absolute, toggle_spacing),
                                    new Dimension(GridSizeMode.Distributed),
                                    new Dimension(GridSizeMode.Absolute, toggle_spacing),
                                    new Dimension(GridSizeMode.Distributed),
                                },
                                RowDimensions = new[] { new Dimension(GridSizeMode.Absolute, toggle_height) },
                                Content = new[]
                                {
                                    new[]
                                    {
                                        whistleToggle = new AdditionToggleButton(OsuIcon.EditorWhistle, @"W", HitSoundingStrings.Whistle, osuColour.Blue,
                                            new SelectionHint(HitSoundingStrings.TooltipWhistle, (@"W", HitSoundingStrings.HotkeyToggleWhistle)))
                                        {
                                            Action = () => model.ToggleAddition(model.SelectedNodes, HitSampleInfo.HIT_WHISTLE)
                                        },
                                        Empty(),
                                        finishToggle = new AdditionToggleButton(OsuIcon.EditorFinish, @"E", HitSoundingStrings.Finish, osuColour.Yellow,
                                            new SelectionHint(HitSoundingStrings.TooltipFinish, (@"E", HitSoundingStrings.HotkeyToggleFinish)))
                                        {
                                            Action = () => model.ToggleAddition(model.SelectedNodes, HitSampleInfo.HIT_FINISH)
                                        },
                                        Empty(),
                                        clapToggle = new AdditionToggleButton(OsuIcon.EditorClap, @"R", HitSoundingStrings.Clap, osuColour.Pink,
                                            new SelectionHint(default, (@"R", HitSoundingStrings.HotkeyToggleClap)))
                                        {
                                            Action = () => model.ToggleAddition(model.SelectedNodes, HitSampleInfo.HIT_CLAP)
                                        },
                                    }
                                }
                            }
                        ]
                    }
                }
            };

            setDropdown.Current.BindValueChanged(e =>
            {
                if (IsLoaded && !refreshing)
                    model.SetSetIndex(model.SelectedNodes, e.NewValue);
            });

            bankDropdown.Current.BindValueChanged(e =>
            {
                if (!IsLoaded || refreshing || string.IsNullOrEmpty(e.NewValue))
                    return;

                model.SetBank(model.SelectedNodes, e.NewValue, -1, false);
            });

            additionDropdown.Current.BindValueChanged(e =>
            {
                if (!IsLoaded || refreshing || string.IsNullOrEmpty(e.NewValue))
                    return;

                model.SetBank(model.SelectedNodes, e.NewValue, -1, true);
            });
        }

        private Drawable createTimeBlock(OverlayColourProvider colours) => new FillFlowContainer
        {
            RelativeSizeAxes = Axes.X,
            AutoSizeAxes = Axes.Y,
            Direction = FillDirection.Vertical,
            Spacing = new Vector2(0, label_spacing),
            Children = new Drawable[]
            {
                new OsuSpriteText
                {
                    Text = HitSoundingStrings.Time,
                    Font = OsuFont.Default.With(size: time_label_size),
                    Colour = colours.Content2
                },
                new Tooltipped(timeValue = new TruncatingSpriteText
                {
                    RelativeSizeAxes = Axes.X,
                    Font = OsuFont.Default.With(size: time_value_size, weight: FontWeight.Medium),
                    Colour = colours.Content1
                }, new SelectionHint(HitSoundingStrings.TooltipTime))
            }
        };

        protected override void LoadComplete()
        {
            base.LoadComplete();

            refresh();

            model.Changed += scheduleRefresh;
            model.SelectedNodes.CollectionChanged += selectionChanged;
            if (library != null)
                library.Changed += scheduleRefresh;

            if (beatmap.BeatmapSkin != null)
            {
                subscribedSkin = beatmap.BeatmapSkin;
                subscribedSkin.BeatmapSkinChanged += scheduleRefresh;
            }
        }

        private void selectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => scheduleRefresh();

        private void scheduleRefresh()
        {
            if (refreshQueued)
                return;

            refreshQueued = true;
            Schedule(() =>
            {
                refreshQueued = false;
                refresh();
            });
        }

        private void refresh()
        {
            if (!IsLoaded || refreshing)
                return;

            refreshing = true;

            try
            {
                refreshTime();

                // A bindable refuses value writes while disabled, so the fields are enabled for the sync below and the
                // disabled state is re-applied once every displayed value has been refreshed.
                setDropdown.Current.Disabled = false;
                bankDropdown.Current.Disabled = false;
                additionDropdown.Current.Disabled = false;

                var targets = model.SelectedNodes;

                var sets = new SortedSet<int> { 0, 1 };
                if (library != null)
                    sets.UnionWith(library.Sets);

                if (beatmap.BeatmapSkin != null)
                {
                    foreach (var set in beatmap.BeatmapSkin.GetAvailableSampleSets())
                        sets.Add(set.SampleSetIndex);
                }

                foreach (var node in targets)
                    sets.Add(node.SetIndex);

                int[] newSetItems = sets.ToArray();

                if (!setItems.SequenceEqual(newSetItems))
                {
                    setItems = newSetItems;
                    setDropdown.Items = newSetItems;
                }

                setDropdown.Current.Value = targets.Count > 0 ? targets[0].SetIndex : 0;

                bool anyEditable = false;
                bool anyWithAddition = false;
                bool anyWhistle = false;
                bool anyFinishOrClap = false;

                foreach (var node in targets)
                {
                    if (!node.CanEditSamples)
                        continue;

                    anyEditable = true;
                    anyWhistle = true;

                    if (!node.IsBody)
                        anyFinishOrClap = true;

                    if (!anyWithAddition && hasAnyAddition(node))
                        anyWithAddition = true;
                }

                bankDropdown.Current.Value = targets.Count > 0 ? targets[0].GetBank(false) : HitSampleInfo.BANK_NORMAL;
                additionDropdown.Current.Value = targets.Count > 0 ? targets[0].GetBank(true) : HitObjectComposer.HIT_BANK_AUTO;

                setDropdown.Current.Disabled = !anyEditable;
                bankDropdown.Current.Disabled = !anyEditable;
                additionDropdown.Current.Disabled = !anyWithAddition;

                updateAdditionToggle(whistleToggle, HitSampleInfo.HIT_WHISTLE, bodySupported: true, anyWhistle);
                updateAdditionToggle(finishToggle, HitSampleInfo.HIT_FINISH, bodySupported: false, anyFinishOrClap);
                updateAdditionToggle(clapToggle, HitSampleInfo.HIT_CLAP, bodySupported: false, anyFinishOrClap);
            }
            finally
            {
                refreshing = false;
            }
        }

        private void refreshTime()
        {
            var targets = model.SelectedNodes;

            if (targets.Count == 0)
            {
                timeValue.Text = @"-";
                return;
            }

            if (targets.Count == 1 && !targets[0].IsBody)
            {
                timeValue.Text = targets[0].Time.ToEditorFormattedString();
                return;
            }

            double start = double.MaxValue;
            double end = double.MinValue;

            foreach (var node in targets)
            {
                start = Math.Min(start, node.IsBody ? node.HitObject.StartTime : node.Time);
                end = Math.Max(end, node.IsBody ? node.HitObject.GetEndTime() : node.Time);
            }

            timeValue.Text = HitSoundingStrings.TimeRange(start.ToEditorFormattedString(), end.ToEditorFormattedString());
        }

        private void updateAdditionToggle(AdditionToggleButton toggle, string sampleName, bool bodySupported, bool anySupported)
        {
            int applicable = 0;
            int present = 0;

            foreach (var node in model.SelectedNodes)
            {
                if (!node.CanEditSamples)
                    continue;

                if (node.IsBody && !bodySupported)
                    continue;

                applicable++;

                if (node.HasAddition(sampleName))
                    present++;
            }

            toggle.SetState(present == applicable, present > 0 && present < applicable, anySupported && applicable > 0);
        }

        private static bool hasAnyAddition(HitSoundingNode node)
        {
            foreach (string addition in HitSampleInfo.ALL_ADDITIONS)
            {
                if (node.HasAddition(addition))
                    return true;
            }

            return false;
        }

        protected override void Dispose(bool isDisposing)
        {
            model.Changed -= scheduleRefresh;
            model.SelectedNodes.CollectionChanged -= selectionChanged;
            if (library != null)
                library.Changed -= scheduleRefresh;

            if (subscribedSkin != null)
                subscribedSkin.BeatmapSkinChanged -= scheduleRefresh;

            base.Dispose(isDisposing);
        }

        /// <summary>
        /// The custom tooltip content of a selection panel control: one description line plus optional shortcut rows.
        /// </summary>
        private readonly struct SelectionHint
        {
            public readonly LocalisableString Text;
            public readonly (string Key, LocalisableString Text)[]? Shortcuts;

            public SelectionHint(LocalisableString text, params (string Key, LocalisableString Text)[] shortcuts)
            {
                Text = text;
                Shortcuts = shortcuts.Length == 0 ? null : shortcuts;
            }
        }

        /// <summary>
        /// Wraps a control so that hovering anywhere over it shows a <see cref="SelectionHint"/>.
        /// </summary>
        private partial class Tooltipped : Container, IHasCustomTooltip<SelectionHint>
        {
            private readonly SelectionHint hint;

            public Tooltipped(Drawable child, SelectionHint hint)
            {
                this.hint = hint;

                RelativeSizeAxes = Axes.X;
                AutoSizeAxes = Axes.Y;
                Child = child;
            }

            SelectionHint IHasCustomTooltip<SelectionHint>.TooltipContent => hint;

            ITooltip<SelectionHint> IHasCustomTooltip<SelectionHint>.GetCustomTooltip() => new SelectionTooltip();
        }

        /// <summary>
        /// A compact icon / shortcut / label toggle matching the W, F and C addition buttons of the design.
        /// </summary>
        private partial class AdditionToggleButton : OsuClickableContainer, IHasCustomTooltip<SelectionHint>
        {
            private readonly SelectionHint hint;
            private readonly Colour4 accent;
            private readonly Box background;
            private readonly Box hoverLayer;
            private readonly SpriteIcon icon;
            private readonly OsuSpriteText shortcut;
            private readonly OsuSpriteText label;

            private OverlayColourProvider colours = null!;

            private bool on;
            private bool mixed;

            public AdditionToggleButton(IconUsage icon, string shortcut, LocalisableString label, Colour4 accent, SelectionHint hint)
                : base(HoverSampleSet.Button)
            {
                this.accent = accent;
                this.hint = hint;

                Masking = true;
                CornerRadius = 5;

                Content.Children = new Drawable[]
                {
                    background = new Box { RelativeSizeAxes = Axes.Both },
                    new FillFlowContainer
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        AutoSizeAxes = Axes.Both,
                        Direction = FillDirection.Vertical,
                        Spacing = new Vector2(0, 6),
                        Children = new Drawable[]
                        {
                            new FillFlowContainer
                            {
                                Anchor = Anchor.TopCentre,
                                Origin = Anchor.TopCentre,
                                AutoSizeAxes = Axes.Both,
                                Direction = FillDirection.Horizontal,
                                Spacing = new Vector2(4, 0),
                                Children = new Drawable[]
                                {
                                    this.icon = new SpriteIcon
                                    {
                                        Icon = icon,
                                        Size = new Vector2(20)
                                    },
                                    this.shortcut = new OsuSpriteText
                                    {
                                        Text = shortcut,
                                        Font = OsuFont.GetFont(size: 14, weight: FontWeight.Bold)
                                    }
                                }
                            },
                            this.label = new OsuSpriteText
                            {
                                Anchor = Anchor.TopCentre,
                                Origin = Anchor.TopCentre,
                                Text = label,
                                Font = OsuFont.GetFont(size: 12, weight: FontWeight.Medium)
                            }
                        }
                    },
                    hoverLayer = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Colour4.White,
                        Alpha = 0
                    }
                };
            }

            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider colours)
            {
                this.colours = colours;
                updateColours();
            }

            public void SetState(bool on, bool mixed, bool enabled)
            {
                this.on = on;
                this.mixed = mixed;

                Enabled.Value = enabled;
                Alpha = enabled ? 1 : 0.3f;

                if (IsLoaded)
                    updateColours();
            }

            private void updateColours()
            {
                background.Colour = mixed ? accent.Darken(0.5f) : on ? accent : colours.Background3;

                Colour4 contentColour = on || mixed ? colours.Background5 : colours.Light3;
                icon.Colour = contentColour;
                shortcut.Colour = contentColour;
                label.Colour = contentColour;
            }

            protected override bool OnHover(HoverEvent e)
            {
                if (Enabled.Value)
                    hoverLayer.FadeTo(0.08f, 100, Easing.OutQuint);

                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                hoverLayer.FadeOut(100, Easing.OutQuint);
                base.OnHoverLost(e);
            }

            SelectionHint IHasCustomTooltip<SelectionHint>.TooltipContent => hint;

            ITooltip<SelectionHint> IHasCustomTooltip<SelectionHint>.GetCustomTooltip() => new SelectionTooltip();
        }

        /// <summary>
        /// The shortcut tooltip used by the selection panel, matching the hitsounding hover panels of the design.
        /// </summary>
        private partial class SelectionTooltip : VisibilityContainer, ITooltip<SelectionHint>
        {
            private const float text_width = 500;
            private const float keycap_text_width = 440;

            private static readonly OverlayColourProvider colour_provider = new OverlayColourProvider(OverlayColourScheme.Aquamarine);

            private readonly Box background;
            private readonly FillFlowContainer rows;

            private Colour4 textColour;
            private Colour4 keycapBackground;
            private Colour4 keycapTextColour;

            private SelectionHint hint;
            private bool instantMovement = true;

            public SelectionTooltip()
            {
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
                    background = new Box { RelativeSizeAxes = Axes.Both },
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

            public void SetContent(SelectionHint content)
            {
                hint = content;

                if (IsLoaded)
                    rebuildRows();
            }

            private void rebuildRows()
            {
                rows.Clear();
                if (!hint.Text.Equals(default))
                    rows.Add(createTextRow(hint.Text));

                if (hint.Shortcuts == null)
                    return;

                foreach (var (key, text) in hint.Shortcuts)
                    rows.Add(createKeycapRow(key, text));
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

            private Drawable createKeycapRow(string keycapText, LocalisableString text)
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
                            Colour = keycapBackground
                        },
                        new OsuSpriteText
                        {
                            Text = keycapText,
                            Font = OsuFont.GetFont(size: 16, weight: FontWeight.Bold),
                            Colour = keycapTextColour,
                            Margin = new MarginPadding { Horizontal = 5, Bottom = 1 }
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

            public void Move(Vector2 pos)
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

        private partial class SetDropdown : FormDropdown<int>
        {
            protected override LocalisableString GenerateItemText(int item)
                => item == 0 ? HitSoundingStrings.UserSkin : HitSoundingStrings.Set(item);
        }

        private partial class BankDropdown : FormDropdown<string>
        {
            protected override LocalisableString GenerateItemText(string item) => item switch
            {
                HitSampleInfo.BANK_NORMAL => HitSoundingStrings.Normal,
                HitSampleInfo.BANK_SOFT => HitSoundingStrings.Soft,
                HitSampleInfo.BANK_DRUM => HitSoundingStrings.Drum,
                _ => HitSoundingStrings.Auto
            };
        }

        private partial class AdditionBankDropdown : FormDropdown<string>
        {
            protected override LocalisableString GenerateItemText(string item) => item switch
            {
                HitObjectComposer.HIT_BANK_AUTO => HitSoundingStrings.Auto,
                HitSampleInfo.BANK_NORMAL => HitSoundingStrings.Normal,
                HitSampleInfo.BANK_SOFT => HitSoundingStrings.Soft,
                _ => HitSoundingStrings.Drum
            };
        }
    }
}
