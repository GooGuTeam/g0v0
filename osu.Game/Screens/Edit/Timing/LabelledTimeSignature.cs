// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Globalization;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Game.Beatmaps.Timing;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;

namespace osu.Game.Screens.Edit.Timing
{
    public partial class LabelledTimeSignature : LabelledComponent<LabelledTimeSignature.TimeSignatureBox, TimeSignature>
    {
        public LabelledTimeSignature()
            : base(false)
        {
        }

        /// <summary>
        /// Invoked on a valid user commit, including committing the currently displayed signature.
        /// </summary>
        public Action<TimeSignature>? OnCommit
        {
            get => Component.OnCommit;
            set => Component.OnCommit = value;
        }

        protected override TimeSignatureBox CreateComponent() => new TimeSignatureBox();

        public partial class TimeSignatureBox : CompositeDrawable, IHasCurrentValue<TimeSignature>
        {
            private readonly BindableWithCurrent<TimeSignature> current = new BindableWithCurrent<TimeSignature>(TimeSignature.SimpleQuadruple);

            public Bindable<TimeSignature> Current
            {
                get => current.Current;
                set => current.Current = value;
            }

            public Action<TimeSignature>? OnCommit;

            private TimeSignatureNumberBox numeratorBox = null!;
            private string lastDisplayedText = string.Empty;
            private bool updatingFromCurrent;
            private bool textChangedSinceDisplay;

            [BackgroundDependencyLoader]
            private void load()
            {
                AutoSizeAxes = Axes.Both;
                InternalChild = new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Horizontal,
                    Children = new Drawable[]
                    {
                        numeratorBox = new TimeSignatureNumberBox
                        {
                            Width = 40,
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            CornerRadius = CORNER_RADIUS,
                            CommitOnFocusLost = true
                        },
                        new OsuSpriteText
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Margin = new MarginPadding
                            {
                                Left = 5,
                                Right = CONTENT_PADDING_HORIZONTAL
                            },
                            Text = "/ 4",
                            Font = OsuFont.Default.With(size: 20)
                        }
                    }
                };
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                numeratorBox.Current.BindValueChanged(_ =>
                {
                    if (!updatingFromCurrent)
                        textChangedSinceDisplay = true;
                });
                Current.BindValueChanged(_ => updateFromCurrent(), true);
                numeratorBox.OnCommit += (_, _) => updateFromNumeratorBox();
            }

            private void updateFromCurrent()
            {
                updatingFromCurrent = true;
                lastDisplayedText = Current.Value.Numerator.ToString(CultureInfo.InvariantCulture);
                numeratorBox.Current.Value = lastDisplayedText;
                textChangedSinceDisplay = false;
                updatingFromCurrent = false;
            }

            private void updateFromNumeratorBox()
            {
                if (numeratorBox.CommittingFromFocusLoss && !textChangedSinceDisplay && numeratorBox.Current.Value == lastDisplayedText)
                    return;

                if (int.TryParse(numeratorBox.Current.Value, out int numerator) && numerator > 0)
                {
                    Current.Value = new TimeSignature(numerator);
                    updateFromCurrent();
                    OnCommit?.Invoke(Current.Value);
                }
                else
                {
                    // trigger `Current` change to restore the numerator box's text to a valid value.
                    Current.TriggerChange();
                }
            }

            private partial class TimeSignatureNumberBox : OsuNumberBox
            {
                public bool CommittingFromFocusLoss { get; private set; }
                private bool insideExplicitCommit;

                protected override void Commit()
                {
                    bool wasFocusLoss = CommittingFromFocusLoss && !insideExplicitCommit;
                    insideExplicitCommit = true;
                    CommittingFromFocusLoss = wasFocusLoss;

                    try
                    {
                        base.Commit();
                    }
                    finally
                    {
                        insideExplicitCommit = false;
                        CommittingFromFocusLoss = false;
                    }
                }

                protected override void OnFocusLost(FocusLostEvent e)
                {
                    if (!insideExplicitCommit)
                        CommittingFromFocusLoss = true;

                    try
                    {
                        base.OnFocusLost(e);
                    }
                    finally
                    {
                        CommittingFromFocusLoss = false;
                    }
                }
            }
        }
    }
}
