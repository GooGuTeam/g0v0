// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
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
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Localisation;
using osu.Game.Rulesets.Edit;
using osu.Game.Rulesets.Objects.Types;
using osuTK.Input;

namespace osu.Game.Screens.Edit.HitSounding
{
    /// <summary>
    /// An editable cell at an existing sound point, including cells with no visible badge.
    /// </summary>
    public partial class HitSoundingSamplePoint : CompositeDrawable, IHasPopover, IHasCustomTooltip<LocalisableString>
    {
        /// <summary>
        /// The node displayed by this control. Reassigned through <see cref="BindNode"/> when the control is reused.
        /// </summary>
        public HitSoundingNode Node { get; private set; }

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
        private IBindable<HitSoundingNormalVisibility> normalVisibility = null!;

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        public bool IsEnabled => Node.CanEditSamples
                                 && (Lane != HitSoundingLane.Additions || HitSampleInfo.ALL_ADDITIONS.Any(Node.HasAddition))
                                 && (!Node.IsBody || Lane is HitSoundingLane.SampleSet or HitSoundingLane.Additions or HitSoundingLane.Whistle);

        public bool IsEmpty => Lane switch
        {
            // This is a display convention, not a change to the stored sample format.
            HitSoundingLane.SampleSet => Node.GetBank(false) == HitSampleInfo.BANK_NORMAL && model.NormalVisibility.Value switch
            {
                HitSoundingNormalVisibility.HideNormal0 => Node.SetIndex == 0,
                HitSoundingNormalVisibility.HideNormal1 => Node.SetIndex == 1,
                _ => false
            },
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

        [Resolved(canBeNull: true)]
        private HitSoundingSampleLibrary? library { get; set; }

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
                        Anchor = Node.IsBody ? Anchor.Centre : Anchor.TopCentre,
                        Origin = Node.IsBody ? Anchor.CentreLeft : Anchor.TopCentre,
                        Y = Node.IsBody ? 0 : 3,
                        Font = OsuFont.Default.With(size: HitSoundingLayout.SAMPLE_FONT_SIZE, weight: FontWeight.Bold),
                        Colour = colours.B5
                    },
                    setLabel = new OsuSpriteText
                    {
                        Anchor = Node.IsBody ? Anchor.Centre : Anchor.BottomCentre,
                        Origin = Node.IsBody ? Anchor.CentreLeft : Anchor.BottomCentre,
                        Y = Node.IsBody ? 0 : -3,
                        Font = OsuFont.Default.With(size: 14, weight: FontWeight.SemiBold),
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
            normalVisibility = model.NormalVisibility.GetBoundCopy();
            normalVisibility.BindValueChanged(_ => Refresh());
        }

        /// <summary>
        /// Rebinds this control to a different node, allowing a single control to be reused across nodes.
        /// The node must share this control's lane and body kind, as the drawable structure is built once on load.
        /// </summary>
        internal void BindNode(HitSoundingNode node)
        {
            ArgumentNullException.ThrowIfNull(node);

            if (node.IsBody != Node.IsBody)
                throw new InvalidOperationException(@"A sample point can only be reused for a node with the same body kind.");

            Node = node;

            // load() does not run again for a reused control, so every node-dependent property has to be rebuilt here.
            // Refresh() no-ops before load has created the visuals, in which case load() applies the new node instead.
            Refresh();

            // The body span is owned by the roll and is reapplied by it directly after this call.
        }

        /// <summary>
        /// Clears node-dependent transient state so this control can be detached and reused for another node.
        /// Must be called while the control is still in the hierarchy so an open popover can be dismissed.
        /// </summary>
        internal void ResetForReuse()
        {
            // HidePopover() throws without a PopoverContainer ancestor, and it would also close a popover
            // belonging to a different control, so only dismiss the one this control actually opened.
            var popoverContainer = this.FindClosestParent<PopoverContainer>();

            if (popoverContainer != null && ReferenceEquals(popoverContainer.CurrentTarget, this))
                this.HidePopover();

            if (playbackFlash == null)
                return;

            // A fade started for the previous node must not carry over as a glow on the reused control.
            playbackFlash.ClearTransforms();
            playbackFlash.Alpha = 0;
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
            // The label only needs to fit between the inner edges of the first span's node badges.
            badge.Alpha = markerX * 2 >= HitSoundingLayout.SAMPLE_WIDTH + badge.Width ? 1 : 0;
        }

        public void Refresh()
        {
            if (background == null || badge == null)
                return;

            bool bankLane = Lane is HitSoundingLane.SampleSet or HitSoundingLane.Additions;
            bool showBank = !IsEmpty;
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

        protected override void UpdateAfterChildren()
        {
            base.UpdateAfterChildren();

            if (!Node.IsBody || badge == null)
                return;

            float textWidth = bankLabel.DrawWidth + 8 + setLabel.DrawWidth;
            badge.Width = Math.Max(56, textWidth + 20);
            bankLabel.X = -textWidth / 2;
            setLabel.X = bankLabel.X + bankLabel.DrawWidth + 8;
            UpdateBodyLayout(Width, bodyMarkerX);
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
                    HitSoundingLane.SampleSet => selected || IsHovered ? 0.35f : 0.25f,
                    HitSoundingLane.Additions => hasAdditionEndpoint() ? selected || IsHovered ? 0.35f : 0.25f : 0,
                    HitSoundingLane.Whistle => Node.HasAddition(HitSampleInfo.HIT_WHISTLE) ? 0.66f : IsHovered ? 0.2f : hasEndpointSample(HitSampleInfo.HIT_WHISTLE) ? 0.08f : 0,
                    _ => 0
                }
                : Lane == HitSoundingLane.Additions && Node.CanEditSamples && hasAdditionEndpoint()
                    ? 0.25f
                    : 0;
        }

        private bool hasAdditionEndpoint()
        {
            foreach (string addition in HitSampleInfo.ALL_ADDITIONS)
            {
                if (hasEndpointSample(addition))
                    return true;
            }

            return false;
        }

        private bool hasEndpointSample(string sample)
        {
            if (Node.HitObject is not IHasRepeats repeats || repeats.NodeSamples.Count == 0)
                return false;

            return containsSample(repeats.NodeSamples[0], sample) || containsSample(repeats.NodeSamples[^1], sample);
        }

        private static bool containsSample(IList<HitSampleInfo> samples, string name)
        {
            for (int i = 0; i < samples.Count; i++)
            {
                if (samples[i].Name == name)
                    return true;
            }

            return false;
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
            if (library != null)
                indices.UnionWith(library.Sets);

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
