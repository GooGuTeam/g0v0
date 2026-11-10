// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;

namespace osu.Game.Rulesets.Osu.Objects.Drawables
{
    /// <summary>
    /// Simulates osu!stable's spinner rotation model for the Classic mod.
    /// </summary>
    /// <remarks>
    /// Stable does not count cursor rotation directly. On each scorable (60 Hz) frame it converts the cursor angle change
    /// into a target angular velocity. The spinner's velocity moves towards that target with a limited acceleration and is
    /// capped at <see cref="MAX_VELOCITY"/>. The integrated velocity counts half rotations, and the scoring count increases
    /// by at most one per scorable frame.
    /// Scorable frames are simulated on a fixed grid anchored at the spinner's start time. Each grid point uses the latest input
    /// sample at or before it, so the result does not depend on the update rate. A grid point stands for a 60 Hz frame
    /// in real time, so it is <c>1000 / 60 * rate</c> gameplay milliseconds long.
    /// </remarks>
    public class LegacySpinnerRotation
    {
        /// <summary>
        /// The maximum angular velocity of the spinner, in radians per millisecond (about 477 RPM).
        /// </summary>
        public const double MAX_VELOCITY = 0.05;

        /// <summary>
        /// The velocity used by Spun Out, in radians per millisecond.
        /// </summary>
        public const double SPUN_OUT_VELOCITY = 0.03;

        private const double scorable_frame_length = 1000 / 60.0;

        private readonly double startTime;
        private readonly double endTime;
        private readonly double maxAcceleration;

        private State state;

        private readonly List<State> snapshots = new List<State>();

        public LegacySpinnerRotation(double startTime, double endTime)
        {
            this.startTime = startTime;
            this.endTime = endTime;

            // Stable uses integral millisecond durations.
            maxAcceleration = 8E-05 + Math.Max(0, (5000 - (int)(endTime - startTime)) / 1000.0 / 2000.0);

            state = new State { NextFrameTime = startTime, LastProcessedTime = double.NegativeInfinity };
        }

        /// <summary>
        /// The number of half rotations counted for scoring (stable's <c>scoringRotationCount</c>).
        /// </summary>
        public int ScoringHalfRotations => state.ScoringHalfRotations;

        /// <summary>
        /// The signed visual rotation of the spinner, in radians. Positive values are clockwise on screen.
        /// </summary>
        public double VisualRotation => state.VisualRotation;

        /// <summary>
        /// The current angular velocity, in radians per millisecond.
        /// </summary>
        public double Velocity => state.VelocityCurrent;

        /// <summary>
        /// The latest time passed to <see cref="Advance"/>.
        /// </summary>
        public double LastProcessedTime => state.LastProcessedTime;

        /// <summary>
        /// Processes all scorable frames before <paramref name="time"/>.
        /// </summary>
        /// <param name="time">The current gameplay time.</param>
        /// <param name="rate">The rate of gameplay relative to real time (from rate-adjusting mods).</param>
        /// <param name="sample">The latest input sample, taken before <paramref name="time"/>.</param>
        /// <param name="onHalfRotation">Invoked with the frame time each time the scoring count increases.</param>
        public void Advance(double time, double rate, InputSample? sample, Action<double>? onHalfRotation = null)
        {
            rate = Math.Abs(rate);

            if (rate > 0)
            {
                double frameLength = scorable_frame_length * rate;

                while (state.NextFrameTime < time && state.NextFrameTime < endTime)
                {
                    processFrame(state.NextFrameTime, rate, sample, onHalfRotation);
                    state.NextFrameTime += frameLength;
                    snapshots.Add(state);
                }
            }

            state.LastProcessedTime = Math.Max(state.LastProcessedTime, time);
        }

        /// <summary>
        /// Reverts all scorable frames after <paramref name="time"/>.
        /// </summary>
        /// <returns>The number of half rotations removed from the scoring count.</returns>
        public int RewindTo(double time)
        {
            int previousCount = state.ScoringHalfRotations;

            while (snapshots.Count > 0 && snapshots[^1].FrameTime > time)
                snapshots.RemoveAt(snapshots.Count - 1);

            state = snapshots.Count > 0
                ? snapshots[^1]
                : new State { NextFrameTime = startTime, LastProcessedTime = double.NegativeInfinity };

            state.LastProcessedTime = time;

            return previousCount - state.ScoringHalfRotations;
        }

        private void processFrame(double frameTime, double rate, InputSample? sample, Action<double>? onHalfRotation)
        {
            // Stable integrates every frame between scorable frames, with the target velocity from the previous scorable frame.
            if (state.FrameTime is double previousFrameTime)
                integrate(Math.Max(previousFrameTime, startTime), Math.Min(frameTime, endTime), rate, sample?.SpunOut == true);

            state.FrameTime = frameTime;

            updateTargetVelocity(frameTime, sample);

            int rotationCount = (int)state.HalfRotations;

            if (rotationCount == state.LastRotationCount)
                return;

            state.ScoringHalfRotations++;
            state.LastRotationCount = rotationCount;
            onHalfRotation?.Invoke(frameTime);
        }

        /// <summary>
        /// Matches <c>SpinnerOsu.GetScorePoints</c>.
        /// </summary>
        private void updateTargetVelocity(double frameTime, InputSample? sample)
        {
            if (sample is not InputSample input)
            {
                state.VelocityTheoretical = state.ZeroCount++ < 1 ? state.VelocityTheoretical / 3 : 0;
                return;
            }

            double angle = input.Angle;
            double delta = angle - state.LastAngle;

            if (angle - state.LastAngle < -Math.PI)
                delta = Math.PI * 2 + angle - state.LastAngle;
            else if (state.LastAngle - angle < -Math.PI)
                delta = Math.PI * -2 - state.LastAngle + angle;

            if (delta == 0)
                state.VelocityTheoretical = state.ZeroCount++ < 1 ? state.VelocityTheoretical / 3 : 0;
            else
            {
                state.ZeroCount = 0;

                if (!input.Pressed || frameTime < startTime || frameTime > endTime)
                    delta = 0;

                // Frames are evenly spaced 60 Hz frames, so stable's frame variance check always selects the nominal frame length.
                state.VelocityTheoretical = Math.Abs(delta) < Math.PI ? delta / scorable_frame_length : 0;
            }

            state.LastAngle = angle;
        }

        /// <summary>
        /// Integrates the velocity between two times exactly, matching the limit of stable's per-frame integration.
        /// </summary>
        private void integrate(double from, double to, double rate, bool spunOut)
        {
            double duration = to - from;

            if (duration <= 0)
                return;

            if (spunOut)
            {
                state.VelocityCurrent = SPUN_OUT_VELOCITY;
                addRotation(SPUN_OUT_VELOCITY, SPUN_OUT_VELOCITY, duration);
                return;
            }

            double target = Math.Clamp(state.VelocityTheoretical, -MAX_VELOCITY, MAX_VELOCITY);
            double v0 = Math.Clamp(state.VelocityCurrent, -MAX_VELOCITY, MAX_VELOCITY);

            // Stable applies the rate to the acceleration per gameplay millisecond.
            double acceleration = maxAcceleration / rate;
            double timeToTarget = Math.Abs(target - v0) / acceleration;

            if (timeToTarget >= duration)
            {
                double v1 = v0 + Math.Sign(target - v0) * acceleration * duration;
                addRotation(v0, v1, duration);
                state.VelocityCurrent = v1;
            }
            else
            {
                addRotation(v0, target, timeToTarget);
                addRotation(target, target, duration - timeToTarget);
                state.VelocityCurrent = target;
            }
        }

        /// <summary>
        /// Adds the rotation of a linear velocity change from <paramref name="v0"/> to <paramref name="v1"/>.
        /// </summary>
        private void addRotation(double v0, double v1, double duration)
        {
            if (duration <= 0)
                return;

            state.VisualRotation += (v0 + v1) / 2 * duration;

            double absolute;

            if (Math.Sign(v0) * Math.Sign(v1) >= 0)
                absolute = Math.Abs(v0 + v1) / 2 * duration;
            else
            {
                // Stable accumulates the absolute rotation of each frame, so a change of direction is not cancelled out.
                double crossing = duration * Math.Abs(v0) / (Math.Abs(v0) + Math.Abs(v1));
                absolute = Math.Abs(v0) * crossing / 2 + Math.Abs(v1) * (duration - crossing) / 2;
            }

            // Stable counts half rotations.
            state.HalfRotations += absolute / Math.PI;
        }

        /// <summary>
        /// An input sample for the spinner.
        /// </summary>
        /// <param name="Angle">The cursor angle around the spinner centre, in radians, as <c>atan2(y, x)</c> in screen orientation.</param>
        /// <param name="Pressed">Whether a gameplay button was held and the spinner could accept input.</param>
        /// <param name="SpunOut">Whether Spun Out drives the spinner.</param>
        public readonly record struct InputSample(double Angle, bool Pressed, bool SpunOut);

        private struct State
        {
            public double NextFrameTime;
            public double? FrameTime;
            public double LastProcessedTime;

            // Stable starts with a cursor angle of zero, so the first scorable frame can produce a velocity.
            public double LastAngle;
            public double VelocityTheoretical;
            public double VelocityCurrent;
            public int ZeroCount;
            public double HalfRotations;
            public int LastRotationCount;
            public int ScoringHalfRotations;
            public double VisualRotation;
        }
    }
}
