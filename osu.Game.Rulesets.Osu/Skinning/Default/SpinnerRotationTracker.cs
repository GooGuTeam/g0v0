// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Diagnostics;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input.Events;
using osu.Framework.Utils;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Osu.Objects.Drawables;
using osu.Game.Screens.Play;
using osuTK;

namespace osu.Game.Rulesets.Osu.Skinning.Default
{
    public partial class SpinnerRotationTracker : CircularContainer
    {
        public override bool IsPresent => true; // handle input when hidden

        private readonly DrawableSpinner drawableSpinner;

        private Vector2? mousePosition;
        private float? lastAngle;

        private float currentRotation;
        private bool rotationTransferred;

        [Resolved(canBeNull: true)]
        private IGameplayClock? gameplayClock { get; set; }

        public SpinnerRotationTracker(DrawableSpinner drawableSpinner)
        {
            this.drawableSpinner = drawableSpinner;
            drawableSpinner.HitObjectApplied += resetState;

            RelativeSizeAxes = Axes.Both;
        }

        public override bool ReceivePositionalInputAt(Vector2 screenSpacePos) => true;

        public bool Tracking { get; set; }

        /// <summary>
        /// Whether the spinning is spinning at a reasonable speed to be considered visually spinning.
        /// </summary>
        public readonly BindableBool IsSpinning = new BindableBool();

        /// <summary>
        /// Whether currently in the correct time range to allow spinning.
        /// </summary>
        public bool IsSpinnableTime => drawableSpinner.HitObject.StartTime <= Time.Current && drawableSpinner.HitObject.EndTime > Time.Current;

        protected override bool OnMouseMove(MouseMoveEvent e)
        {
            mousePosition = Parent!.ToLocalSpace(e.ScreenSpaceMousePosition);
            return base.OnMouseMove(e);
        }

        /// <summary>
        /// Whether Spun Out drives a Classic spinner at stable's fixed velocity instead of calling <see cref="AddRotation"/>.
        /// </summary>
        public bool LegacySpunOut { get; set; }

        private LegacySpinnerRotation? legacyRotation;
        private LegacySpinnerRotation.InputSample? legacySample;
        private float legacyRotationOrigin;
        private double lastRotationTime = double.NegativeInfinity;

        protected override void Update()
        {
            base.Update();

            if (drawableSpinner.HitObject?.ClassicSpinnerBehaviour == true)
                updateLegacyRotation();
            else
                updateRotation();

            IsSpinning.Value = IsSpinnableTime && Math.Abs(currentRotation - Rotation) > 10f;
            Rotation = (float)Interpolation.Damp(Rotation, currentRotation, 0.99, Math.Abs(Time.Elapsed));
        }

        internal void ProcessLegacyRotation(double? targetTime = null) => updateLegacyRotation(targetTime);

        private void updateLegacyRotation(double? targetTime = null)
        {
            var spinner = drawableSpinner.HitObject;

            if (legacyRotation == null)
            {
                legacyRotation = drawableSpinner.Result.LegacyRotation ??= new LegacySpinnerRotation(spinner.StartTime, spinner.EndTime);
                legacyRotationOrigin = Rotation - (float)double.RadiansToDegrees(legacyRotation.VisualRotation);
            }

            double time = targetTime ?? Time.Current;
            bool isPaused = gameplayClock?.IsPaused.Value == true;
            bool spunOut = LegacySpunOut || drawableSpinner.SpunOut;
            LegacySpinnerRotation.InputSample? currentSample = mousePosition.HasValue || spunOut
                ? new LegacySpinnerRotation.InputSample(legacyAngle(mousePosition), (Tracking && !isPaused) || spunOut, spunOut)
                : null;

            if (time < legacyRotation.LastProcessedTime)
            {
                int removed = legacyRotation.RewindTo(time);
                if (removed > 0)
                    drawableSpinner.Result.History.ReportDelta(time, -180f * removed);
            }
            else
            {
                double rate = gameplayClock?.GetTrueGameplayRate() ?? (Precision.AlmostEquals(Clock.Rate, 0) ? 1 : Clock.Rate);
                LegacySpinnerRotation.InputSample? sampleForAdvance = legacySample == null || spunOut ? currentSample : legacySample;

                // Each scorable frame uses the sample taken by the previous update, the latest one at or before that frame.
                legacyRotation.Advance(time, rate, sampleForAdvance, frameTime => drawableSpinner.Result.History.ReportDelta(frameTime, 180));
            }

            legacySample = currentSample;

            currentRotation = legacyRotationOrigin + (float)double.RadiansToDegrees(legacyRotation.VisualRotation);
        }

        /// <summary>
        /// The cursor angle as stable measures it: <c>atan2(y, x)</c> of the cursor relative to the spinner centre.
        /// </summary>
        private double legacyAngle(Vector2? position)
        {
            if (position is not Vector2 pos)
                return 0;

            return Math.Atan2(pos.Y - DrawSize.Y / 2, pos.X - DrawSize.X / 2);
        }

        private void updateRotation()
        {
            var spinner = drawableSpinner.HitObject;
            var history = drawableSpinner.Result.History;
            double time = Time.Current;
            bool rewinding = gameplayClock?.IsRewinding == true || time < lastRotationTime || time < history.LastReportTime;

            if (rewinding)
            {
                float previousAccumulated = history.TotalAccumulatedRotation;
                history.RewindTo(time);
                currentRotation += history.TotalAccumulatedRotation - previousAccumulated;

                if (time <= spinner.StartTime)
                {
                    currentRotation = 0;
                    rotationTransferred = false;
                    lastAngle = null;
                }
                else if (mousePosition is Vector2 rewindPos)
                {
                    lastAngle = -float.RadiansToDegrees(MathF.Atan2(rewindPos.X - DrawSize.X / 2, rewindPos.Y - DrawSize.Y / 2));
                }
                else
                {
                    lastAngle = null;
                }

                lastRotationTime = time;
                return;
            }

            if (mousePosition is Vector2 pos)
            {
                float thisAngle = -float.RadiansToDegrees(MathF.Atan2(pos.X - DrawSize.X / 2, pos.Y - DrawSize.Y / 2));
                float delta = lastAngle == null ? 0 : thisAngle - lastAngle.Value;

                // Normalise the delta to -180 .. 180
                if (delta > 180) delta -= 360;
                if (delta < -180) delta += 360;

                if (Tracking)
                    AddRotation(delta);

                // Stable initializes the angle on the first input at or after the spinner's start.
                // The cursor approach during fade-in must not contribute a scoring delta.
                lastAngle = spinner.LegacySpinnerScoring && time < spinner.StartTime
                    ? null
                    : thisAngle;
            }

            lastRotationTime = time;
        }

        /// <summary>
        /// Rotate the disc by the provided angle (in addition to any existing rotation).
        /// </summary>
        /// <remarks>
        /// Will be a no-op if not a valid time to spin.
        /// </remarks>
        /// <param name="delta">The delta angle.</param>
        public void AddRotation(float delta)
        {
            if (!IsSpinnableTime)
                return;

            if (!rotationTransferred)
            {
                currentRotation = Rotation;
                rotationTransferred = true;
            }

            Debug.Assert(Math.Abs(delta) <= 180);

            double rate = gameplayClock?.GetTrueGameplayRate() ?? Clock.Rate;
            delta = (float)(delta * Math.Abs(rate));

            currentRotation += delta;
            drawableSpinner.Result.History.ReportDelta(Time.Current, delta);
        }

        private void resetState(DrawableHitObject obj)
        {
            Tracking = false;
            IsSpinning.Value = false;
            mousePosition = null;
            lastAngle = null;

            float existingRotation = drawableSpinner.Result?.History.TotalAccumulatedRotation ?? 0;
            currentRotation = existingRotation;
            Rotation = existingRotation;
            rotationTransferred = existingRotation != 0;

            LegacySpunOut = drawableSpinner.SpunOut && drawableSpinner.HitObject?.ClassicSpinnerBehaviour == true;
            legacyRotation = null;
            legacySample = null;
            legacyRotationOrigin = 0;
            lastRotationTime = double.NegativeInfinity;
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (drawableSpinner.IsNotNull())
                drawableSpinner.HitObjectApplied -= resetState;
        }
    }
}
