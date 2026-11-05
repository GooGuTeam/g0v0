// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Audio;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Localisation;
using osu.Game.Rulesets.Edit;
using osuTK.Input;

namespace osu.Game.Screens.Edit.HitSounding
{
    /// <summary>
    /// An editable cell at an existing sound point, including cells with no visible badge.
    /// </summary>
    public partial class HitSoundingSamplePoint : CompositeDrawable, IHasPopover, IHasCustomTooltip<LocalisableString>
    {
        public readonly HitSoundingNode Node;
        public readonly HitSoundingLane Lane;

        private readonly HitSoundingEditModel model;
        private Container? badge;
        private Box? background;
        private Container? bodyFill;
        private Container playbackFlash = null!;
        private float bodyMarkerX;
        private OsuSpriteText bankLabel = null!;
        private OsuSpriteText setLabel = null!;
        private bool selected;

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        public bool IsEnabled => Node.CanEditSamples
                                 && (Lane != HitSoundingLane.Additions || HitSampleInfo.ALL_ADDITIONS.Any(Node.HasAddition))
                                 && (!Node.IsBody || Lane is HitSoundingLane.SampleSet or HitSoundingLane.Additions or HitSoundingLane.Whistle);

        public bool IsEmpty => Lane switch
        {
            // This is a display convention, not a change to the stored sample format.
            HitSoundingLane.SampleSet => Node.GetBank(false) == HitSampleInfo.BANK_NORMAL && Node.SetIndex == model.DefaultSampleSetIndex,
            HitSoundingLane.Additions => Node.GetBank(true) == HitObjectComposer.HIT_BANK_AUTO,
            _ => !Node.HasAddition(sampleName)
        };

        private string sampleName => Lane switch
        {
            HitSoundingLane.Whistle => HitSampleInfo.HIT_WHISTLE,
            HitSoundingLane.Finish => HitSampleInfo.HIT_FINISH,
            HitSoundingLane.Clap => HitSampleInfo.HIT_CLAP,
            _ => string.Empty
        };

        public LocalisableString TooltipText => Lane switch
        {
            HitSoundingLane.Additions when !HitSampleInfo.ALL_ADDITIONS.Any(Node.HasAddition) => HitSoundingStrings.ConfigureAdditionHint,
            HitSoundingLane.SampleSet or HitSoundingLane.Additions => HitSoundingStrings.BankHint,
            _ => HitSoundingStrings.AdditionHint
        };

        public LocalisableString TooltipContent => TooltipText;

        public ITooltip<LocalisableString> GetCustomTooltip() => new HitSoundingTooltip(
            Lane == HitSoundingLane.Additions && !IsEnabled
                ? HitSoundingTooltipKind.DisabledAddition
                : Lane is HitSoundingLane.SampleSet or HitSoundingLane.Additions
                    ? HitSoundingTooltipKind.Bank
                    : HitSoundingTooltipKind.Addition);

        public HitSoundingSamplePoint(HitSoundingNode node, HitSoundingEditModel model, HitSoundingLane lane)
        {
            Node = node;
            Lane = lane;
            this.model = model;
            Origin = Anchor.Centre;
            Size = new osuTK.Vector2(HitSoundingLayout.SAMPLE_WIDTH, HitSoundingLayout.SAMPLE_HEIGHT);
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            if (Node.IsBody)
            {
                AddInternal(bodyFill = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Masking = true,
                    CornerRadius = HitSoundingLayout.SAMPLE_CORNER_RADIUS,
                    Child = new Box { RelativeSizeAxes = Axes.Both }
                });
            }

            AddInternal(badge = new Container
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                RelativeSizeAxes = Node.IsBody ? Axes.None : Axes.Both,
                Size = Node.IsBody ? new osuTK.Vector2(56, 28) : osuTK.Vector2.One,
                Masking = true,
                CornerRadius = Node.IsBody ? 0 : HitSoundingLayout.SAMPLE_CORNER_RADIUS,
                BorderColour = osuTK.Graphics.Color4.White,
                Children = new Drawable[]
                {
                    background = new Box { RelativeSizeAxes = Axes.Both },
                    bankLabel = new OsuSpriteText
                    {
                        Anchor = Node.IsBody ? Anchor.CentreLeft : Anchor.TopCentre,
                        Origin = Node.IsBody ? Anchor.CentreLeft : Anchor.TopCentre,
                        X = Node.IsBody ? 10 : 0, Y = Node.IsBody ? 0 : 3,
                        Font = OsuFont.Default.With(size: HitSoundingLayout.SAMPLE_FONT_SIZE, weight: FontWeight.Bold),
                        Colour = colours.B5
                    },
                    setLabel = new OsuSpriteText
                    {
                        Anchor = Node.IsBody ? Anchor.CentreRight : Anchor.BottomCentre,
                        Origin = Node.IsBody ? Anchor.CentreRight : Anchor.BottomCentre,
                        X = Node.IsBody ? -10 : 0, Y = Node.IsBody ? 0 : -3,
                        Font = OsuFont.Default.With(size: 16, weight: FontWeight.SemiBold),
                        Colour = colours.B5
                    }
                }
            });
            AddInternal(playbackFlash = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Alpha = 0,
                Children = new Drawable[]
                {
                    new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Masking = true,
                        CornerRadius = HitSoundingLayout.SAMPLE_CORNER_RADIUS,
                        EdgeEffect = new EdgeEffectParameters
                        {
                            Type = EdgeEffectType.Glow,
                            Colour = new osuTK.Graphics.Color4(1, 1, 1, 0.55f),
                            Radius = 12,
                            Hollow = true
                        }
                    },
                    new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Masking = true,
                        CornerRadius = HitSoundingLayout.SAMPLE_CORNER_RADIUS,
                        BorderThickness = 1.5f,
                        BorderColour = osuTK.Graphics.Color4.White,
                        EdgeEffect = new EdgeEffectParameters
                        {
                            Type = EdgeEffectType.Glow,
                            Colour = new osuTK.Graphics.Color4(1, 1, 1, 0.9f),
                            Radius = 4,
                            Hollow = true
                        },
                        Child = new Box { RelativeSizeAxes = Axes.Both, Alpha = HitSoundingLayout.PLAYBACK_FLASH_OPACITY }
                    }
                }
            });
            if (Node.IsBody)
                UpdateBodyLayout(Width, bodyMarkerX);
            Refresh();
        }

        internal void UpdateBodyLayout(float width, float markerX)
        {
            Width = Math.Max(0, width);
            bodyMarkerX = markerX;

            if (bodyFill != null)
            {
                bodyFill.RelativeSizeAxes = Axes.Y;
                bodyFill.X = -HitSoundingLayout.SAMPLE_WIDTH / 2;
                bodyFill.Width = Width + HitSoundingLayout.SAMPLE_WIDTH;
            }

            if (badge == null)
                return;

            badge.X = markerX - Width / 2;
            // Leave room for the head/repeat badges as well as the body capsule within the first span.
            badge.Alpha = markerX >= HitSoundingLayout.SAMPLE_WIDTH + badge.Width / 2 + 4 ? 1 : 0;
        }

        public void Refresh()
        {
            if (background == null || badge == null)
                return;

            bool bankLane = Lane is HitSoundingLane.SampleSet or HitSoundingLane.Additions;
            bool showBank = !IsEmpty || (Node.IsBody && Lane == HitSoundingLane.SampleSet);
            string bank = Node.GetBank(Lane == HitSoundingLane.Additions);
            background.Colour = bankLane
                ? bank switch
                {
                    HitSampleInfo.BANK_SOFT => colours.GreenLight,
                    HitSampleInfo.BANK_DRUM => colours.YellowLight,
                    _ => colours.BlueLight
                }
                : Lane switch
                {
                    HitSoundingLane.Whistle => colours.Blue,
                    HitSoundingLane.Finish => colours.Yellow,
                    _ => colours.Pink
                };
            background.Alpha = IsEnabled && showBank ? 1 : IsHovered && IsEnabled ? 0.2f : 0;

            if (bodyFill != null)
            {
                bodyFill.Colour = background.Colour;
                background.Alpha = 0;
                bankLabel.Colour = setLabel.Colour = background.Colour;
            }

            bankLabel.Text = bank switch
            {
                HitSampleInfo.BANK_SOFT => "S",
                HitSampleInfo.BANK_DRUM => "D",
                _ => "N"
            };
            setLabel.Text = Node.SetIndex.ToString(System.Globalization.CultureInfo.InvariantCulture);
            bankLabel.Alpha = setLabel.Alpha = bankLane && IsEnabled && showBank ? 1 : 0;
            badge.EdgeEffect = !Node.IsBody && background.Alpha == 1
                ? new EdgeEffectParameters
                {
                    Type = EdgeEffectType.Glow,
                    Colour = new osuTK.Graphics.Color4(1, 1, 1, 0.12f),
                    Radius = 4
                }
                : default;
            updateSelection();
        }

        protected override void Update()
        {
            base.Update();
            if (selected != model.SelectedNodes.Contains(Node))
                updateSelection();
        }

        private void updateSelection()
        {
            selected = model.SelectedNodes.Contains(Node);

            if (badge == null)
                return;

            badge.BorderThickness = selected ? 2 : 0;
            bodyFill?.Alpha = IsEnabled
                ? Lane switch
                {
                    HitSoundingLane.SampleSet or HitSoundingLane.Additions => selected || IsHovered ? 0.35f : 0.25f,
                    HitSoundingLane.Whistle => Node.HasAddition(HitSampleInfo.HIT_WHISTLE) ? 0.66f : IsHovered ? 0.2f : 0.08f,
                    _ => 0
                }
                : 0;
        }

        internal void FlashPlayback()
        {
            if (!IsEnabled || (!Node.IsBody && IsEmpty))
                return;

            playbackFlash.FadeOutFromOne(HitSoundingLayout.PLAYBACK_FLASH_DURATION, Easing.OutQuint);
        }

        protected override bool OnScroll(ScrollEvent e)
        {
            if (!e.ControlPressed || e.AltPressed || Lane is not (HitSoundingLane.SampleSet or HitSoundingLane.Additions))
                return false;
            if (!IsEnabled || e.ScrollDelta.Y == 0)
                return true;

            CycleSet(e.ScrollDelta.Y);
            return true;
        }

        internal void CycleSet(float direction)
        {
            var indices = new SortedSet<int> { 0, 1 };
            var beatmap = Dependencies.Get<EditorBeatmap>();

            if (beatmap.BeatmapSkin != null)
            {
                foreach (var set in beatmap.BeatmapSkin.GetAvailableSampleSets())
                    indices.Add(set.SampleSetIndex);
            }

            foreach (var node in model.Nodes)
                indices.Add(node.SetIndex);

            int[] available = indices.ToArray();
            int index = Array.IndexOf(available, Node.SetIndex);
            int steps = (int)Math.CopySign(Math.Max(1, Math.Round(Math.Abs(direction))), direction);
            int next = ((index + steps) % available.Length + available.Length) % available.Length;
            model.SetBank(model.SelectedNodes.Contains(Node) ? model.SelectedNodes : new[] { Node },
                Node.GetBank(Lane == HitSoundingLane.Additions), available[next], Lane == HitSoundingLane.Additions);
        }

        private void selectForEdit()
        {
            if (model.SelectedNodes.Contains(Node))
                return;

            model.SelectedNodes.Clear();
            model.SelectedNodes.Add(Node);
        }

        protected override bool OnHover(HoverEvent e)
        {
            Refresh();
            return true;
        }

        protected override void OnHoverLost(HoverLostEvent e) => Refresh();

        protected override bool OnMouseDown(MouseDownEvent e)
        {
            if (e.Button == MouseButton.Right && IsEnabled && Lane is HitSoundingLane.SampleSet or HitSoundingLane.Additions)
            {
                this.HidePopover();
                selectForEdit();
                var targets = model.SelectedNodes.ToArray();
                model.SetBank(targets, Lane == HitSoundingLane.Additions ? HitObjectComposer.HIT_BANK_AUTO : HitSampleInfo.BANK_NORMAL,
                    Lane == HitSoundingLane.Additions ? -1 : model.DefaultSampleSetIndex, Lane == HitSoundingLane.Additions);
                return true;
            }

            return e.Button == MouseButton.Left;
        }

        protected override bool OnClick(ClickEvent e)
        {
            if (e.Button != MouseButton.Left)
                return false;

            if (e.ControlPressed)
            {
                if (!model.SelectedNodes.Remove(Node))
                    model.SelectedNodes.Add(Node);

                return true;
            }

            if (!IsEnabled)
                return true;

            selectForEdit();

            if (Lane is HitSoundingLane.SampleSet or HitSoundingLane.Additions)
                this.ShowPopover();
            else
                model.ToggleAddition(model.SelectedNodes, sampleName);

            return true;
        }

        public Popover GetPopover() => new HitSoundingSamplePopover(Node, model, Lane == HitSoundingLane.Additions);
    }
}
