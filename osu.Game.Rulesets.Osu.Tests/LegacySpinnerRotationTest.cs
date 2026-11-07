// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Game.Rulesets.Osu.Objects.Drawables;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Osu.Tests
{
    [TestFixture]
    public class LegacySpinnerRotationTest
    {
        private const double start_time = 1000;
        private const double end_time = 4000;

        // Later stable behaviour: zero-required spinners receive 300 even without input.
        [TestCase(0, 0, HitResult.Great)]
        [TestCase(1, 0, HitResult.Great)]
        [TestCase(2, 0, HitResult.Great)]
        [TestCase(9, 10, HitResult.Miss)]
        [TestCase(3, 10, HitResult.Miss)]
        [TestCase(10, 10, HitResult.Meh)]
        [TestCase(11, 10, HitResult.Ok)]
        [TestCase(12, 10, HitResult.Great)]
        [TestCase(40, 10, HitResult.Great)]
        public void TestClassicResult(int halfSpins, int required, HitResult expected)
            => Assert.That(DrawableSpinner.GetClassicResult(halfSpins, required), Is.EqualTo(expected));

        [Test]
        public void TestNotPressedDoesNotRotate()
        {
            var rotation = run(0.05, pressed: false, updateTimes: range(start_time - 500, end_time + 100, 1));

            Assert.That(rotation.ScoringHalfRotations, Is.Zero);
            Assert.That(rotation.Velocity, Is.Zero);
        }

        [Test]
        public void TestVelocityIsCapped()
        {
            var atCap = run(0.05, true, range(start_time - 500, end_time + 100, 1));
            var faster = run(0.15, true, range(start_time - 500, end_time + 100, 1));

            Assert.That(atCap.Velocity, Is.EqualTo(LegacySpinnerRotation.MAX_VELOCITY).Within(1e-9));
            Assert.That(faster.Velocity, Is.EqualTo(LegacySpinnerRotation.MAX_VELOCITY).Within(1e-9));
            Assert.That(faster.ScoringHalfRotations, Is.EqualTo(atCap.ScoringHalfRotations));

            // About 3000 ms at 0.05 rad/ms, less the acceleration and the first frame.
            Assert.That(atCap.ScoringHalfRotations, Is.InRange(44, 47));
        }

        [Test]
        public void TestAccelerationLimitsShortBursts()
        {
            // Stable spins up gradually, so a slow spin counts less than its cursor rotation.
            var rotation = run(0.02, true, range(start_time - 500, end_time + 100, 1));

            // Samples land on whole milliseconds, so a 60 Hz frame sees 16 or 17 ms of cursor movement.
            Assert.That(rotation.Velocity, Is.EqualTo(0.02).Within(0.001));
            Assert.That(rotation.ScoringHalfRotations, Is.LessThan(0.02 * (end_time - start_time) / Math.PI));
        }

        [Test]
        public void TestResultIndependentOfUpdateRate()
        {
            // Input frames every 16 ms (as recorded in a replay); the slow run only updates on frame times.
            double[] inputFrames = range(start_time - 500, end_time + 100, 16).ToArray();

            var slow = run(0.04, true, inputFrames, inputFrames);
            var fast = run(0.04, true, range(start_time - 500, end_time + 100, 1), inputFrames);
            var uneven = run(0.04, true, range(start_time - 500, end_time + 100, 7), inputFrames);

            Assert.That(fast.ScoringHalfRotations, Is.EqualTo(slow.ScoringHalfRotations));
            Assert.That(uneven.ScoringHalfRotations, Is.EqualTo(slow.ScoringHalfRotations));
            Assert.That(fast.VisualRotation, Is.EqualTo(slow.VisualRotation).Within(1e-6));
        }

        [Test]
        public void TestRewindRestoresState()
        {
            var straight = run(0.04, true, range(start_time - 500, end_time + 100, 2));

            var rotation = new LegacySpinnerRotation(start_time, end_time);
            int count = 0;

            simulate(rotation, 0.04, true, range(start_time - 500, 3000, 2), null, () => count++);
            int beforeRewind = rotation.ScoringHalfRotations;

            int removed = rotation.RewindTo(2000);
            count -= removed;

            Assert.That(removed, Is.GreaterThan(0));
            Assert.That(rotation.ScoringHalfRotations, Is.EqualTo(beforeRewind - removed));

            simulate(rotation, 0.04, true, range(2000, end_time + 100, 2), null, () => count++);

            Assert.That(rotation.ScoringHalfRotations, Is.EqualTo(straight.ScoringHalfRotations));
            Assert.That(count, Is.EqualTo(rotation.ScoringHalfRotations));
        }

        [Test]
        public void TestHalfRotationsCountAtMostOncePerFrame()
        {
            var times = new List<double>();
            var rotation = new LegacySpinnerRotation(start_time, end_time);

            simulate(rotation, 0.05, true, range(start_time - 500, end_time + 100, 1), null, () => times.Add(0));

            int frames = (int)Math.Ceiling((end_time - start_time) / (1000 / 60.0));
            Assert.That(rotation.ScoringHalfRotations, Is.LessThanOrEqualTo(frames));
            Assert.That(times.Count, Is.EqualTo(rotation.ScoringHalfRotations));
        }

        [Test]
        public void TestZeroLengthSpinner()
        {
            var rotation = new LegacySpinnerRotation(start_time, start_time);
            simulate(rotation, 0.05, true, range(start_time - 500, start_time + 500, 1), null, null);

            Assert.That(rotation.ScoringHalfRotations, Is.Zero);
        }

        private static LegacySpinnerRotation run(double angularVelocity, bool pressed, IEnumerable<double> updateTimes, double[]? inputFrames = null)
        {
            var rotation = new LegacySpinnerRotation(start_time, end_time);
            simulate(rotation, angularVelocity, pressed, updateTimes, inputFrames, null);
            return rotation;
        }

        /// <summary>
        /// Updates the rotation at each update time, with the cursor circling at <paramref name="angularVelocity"/> rad/ms.
        /// When <paramref name="inputFrames"/> is given, the cursor only moves on those times, as during replay playback.
        /// </summary>
        private static void simulate(LegacySpinnerRotation rotation, double angularVelocity, bool pressed, IEnumerable<double> updateTimes, double[]? inputFrames, Action? onHalfRotation)
        {
            LegacySpinnerRotation.InputSample? sample = null;

            foreach (double time in updateTimes)
            {
                rotation.Advance(time, 1, sample, _ => onHalfRotation?.Invoke());

                double inputTime = inputFrames == null ? time : inputFrames.LastOrDefault(f => f <= time, inputFrames[0]);
                double angle = Math.IEEERemainder(angularVelocity * inputTime, Math.PI * 2);

                sample = new LegacySpinnerRotation.InputSample(angle, pressed, false);
            }
        }

        private static IEnumerable<double> range(double from, double to, double step)
        {
            for (double t = from; t <= to; t += step)
                yield return t;
        }
    }
}
