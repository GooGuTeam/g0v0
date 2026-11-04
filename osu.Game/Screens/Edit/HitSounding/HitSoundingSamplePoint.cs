// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
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
    public partial class HitSoundingSamplePoint : CompositeDrawable, IHasPopover, IHasTooltip
    {
        public readonly HitSoundingNode Node;
        public readonly HitSoundingLane Lane;

        private readonly HitSoundingEditModel model;
        private Container badge = null!;
        private Box background = null!;
        private Box? bodyFill;
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
                AddInternal(bodyFill = new Box { RelativeSizeAxes = Axes.Both });

            AddInternal(badge = new Container
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                RelativeSizeAxes = Node.IsBody ? Axes.None : Axes.Both,
                Size = Node.IsBody ? new osuTK.Vector2(56, 28) : osuTK.Vector2.One,
                Masking = true,
                CornerRadius = Node.IsBody ? 14 : HitSoundingLayout.SAMPLE_CORNER_RADIUS,
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
                        Font = OsuFont.Default.With(size: 14, weight: FontWeight.SemiBold),
                        Colour = colours.B5
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
            if (badge == null)
                return;

            badge.X = markerX - Width / 2;
            // Leave room for the head/repeat badges as well as the body capsule within the first span.
            badge.Alpha = markerX >= HitSoundingLayout.SAMPLE_WIDTH + badge.Width / 2 + 4 ? 1 : 0;
        }

        public void Refresh()
        {
            if (background == null)
                return;

            bool bankLane = Lane is HitSoundingLane.SampleSet or HitSoundingLane.Additions;
            bool showBank = !IsEmpty || (Node.IsBody && Lane == HitSoundingLane.SampleSet);
            string bank = Node.GetBank(Lane == HitSoundingLane.Additions);
            background.Colour = bankLane ? bank switch
            {
                HitSampleInfo.BANK_SOFT => colours.GreenLight,
                HitSampleInfo.BANK_DRUM => colours.YellowLight,
                _ => colours.BlueLight
            } : Lane switch
            {
                HitSoundingLane.Whistle => colours.Blue,
                HitSoundingLane.Finish => colours.Yellow,
                _ => colours.Pink
            };
            background.Alpha = IsEnabled && showBank ? 1 : IsHovered && IsEnabled ? 0.2f : 0;
            if (bodyFill != null)
            {
                bodyFill.Colour = background.Colour;
                background.Alpha = IsEnabled ? showBank ? 1 : 0.2f : 0;
            }
            bankLabel.Text = bank switch
            {
                HitSampleInfo.BANK_SOFT => "S",
                HitSampleInfo.BANK_DRUM => "D",
                _ => "N"
            };
            setLabel.Text = Node.SetIndex.ToString(System.Globalization.CultureInfo.InvariantCulture);
            bankLabel.Alpha = setLabel.Alpha = bankLane && IsEnabled && showBank ? 1 : 0;
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
            badge.BorderThickness = selected ? 2 : 0;
            if (bodyFill != null)
                bodyFill.Alpha = IsEnabled ? selected ? 0.25f : IsHovered ? 0.2f : 0.1f : 0;
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
                var targets = model.SelectedNodes.Contains(Node) ? model.SelectedNodes.ToArray() : new[] { Node };
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
            if (!model.SelectedNodes.Contains(Node))
            {
                model.SelectedNodes.Clear();
                model.SelectedNodes.Add(Node);
            }
            if (Lane is HitSoundingLane.SampleSet or HitSoundingLane.Additions)
                this.ShowPopover();
            else
                model.ToggleAddition(model.SelectedNodes, sampleName);
            return true;
        }

        public Popover GetPopover() => new HitSoundingSamplePopover(Node, model, Lane == HitSoundingLane.Additions);
    }
}
