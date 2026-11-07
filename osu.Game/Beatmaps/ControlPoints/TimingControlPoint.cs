// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

using System;
using Newtonsoft.Json;
using osu.Framework.Bindables;
using osu.Game.Beatmaps.Timing;
using osu.Game.Graphics;
using osuTK.Graphics;

namespace osu.Game.Beatmaps.ControlPoints
{
    public class TimingControlPoint : ControlPoint, IEquatable<TimingControlPoint>
    {
        /// <summary>
        /// The time signature at this control point.
        /// </summary>
        public readonly Bindable<TimeSignature> TimeSignatureBindable = new Bindable<TimeSignature>(TimeSignature.SimpleQuadruple);

        /// <summary>
        /// Whether the first bar line of this control point is ignored.
        /// </summary>
        public readonly BindableBool OmitFirstBarLineBindable = new BindableBool();

        /// <summary>
        /// Default length of a beat in milliseconds. Used whenever there is no beatmap or track playing.
        /// </summary>
        private const double default_beat_length = 60000.0 / 60.0;

        public override Color4 GetRepresentingColour(OsuColour colours) => colours.Red2;

        public static readonly TimingControlPoint DEFAULT = new TimingControlPoint
        {
            BeatLengthBindable =
            {
                Value = default_beat_length,
                Disabled = true
            },
            OmitFirstBarLineBindable = { Disabled = true },
            TimeSignatureBindable = { Disabled = true }
        };

        /// <summary>
        /// The time signature at this control point.
        /// </summary>
        public TimeSignature TimeSignature
        {
            get => TimeSignatureBindable.Value;
            set
            {
                TimeSignatureBindable.Value = value;
                LegacyTimeSignature = null;
            }
        }

        private int? legacyTimeSignature;

        /// <summary>
        /// The signed meter of an imported legacy timing point which cannot be represented by
        /// <see cref="TimeSignature"/>. Legacy encoding and Nightcore use this value without changing its sign.
        /// The positive native signature remains an editor and bar-line stepping projection, not the source meter.
        /// </summary>
        /// <remarks>
        /// Assigning <see cref="TimeSignature"/> clears this override, even when unchanged. Bindable changes
        /// clear it only when the value changes. Restore this after the native signature when copying/importing.
        /// </remarks>
        [JsonProperty(Order = 1)]
        public int? LegacyTimeSignature
        {
            get => legacyTimeSignature;
            set
            {
                if (legacyTimeSignature == value)
                    return;

                if (TimeSignatureBindable.Disabled)
                    throw new InvalidOperationException("Cannot change legacy meter while time signature is disabled.");

                legacyTimeSignature = value;
                RaiseChanged();
            }
        }

        /// <summary>
        /// Whether the first bar line of this control point is ignored.
        /// </summary>
        public bool OmitFirstBarLine
        {
            get => OmitFirstBarLineBindable.Value;
            set => OmitFirstBarLineBindable.Value = value;
        }

        public const double DEFAULT_BEAT_LENGTH = 1000;

        /// <summary>
        /// The beat length at this control point.
        /// </summary>
        public readonly BindableDouble BeatLengthBindable = new BindableDouble(DEFAULT_BEAT_LENGTH)
        {
            MinValue = 6,
            MaxValue = 60000
        };

        /// <summary>
        /// The beat length at this control point.
        /// </summary>
        public double BeatLength
        {
            get => BeatLengthBindable.Value;
            set
            {
                BeatLengthBindable.Value = value;
                LegacyBeatLength = null;
            }
        }

        private double? legacyBeatLength;

        /// <summary>
        /// The raw finite beat length of an imported legacy redline, or null for an edited/native timing point.
        /// Only standard legacy slider timing and legacy encoding use this value; <see cref="BeatLength"/>
        /// remains bounded for editing, display and other shared timing consumers.
        /// </summary>
        /// <remarks>
        /// Assigning <see cref="BeatLength"/> explicitly clears this override, even if the bounded value is unchanged.
        /// Direct bindable changes clear it only when the value changes. Set this after <see cref="BeatLength"/>
        /// when restoring imported timing.
        /// </remarks>
        [JsonProperty(Order = 1)]
        public double? LegacyBeatLength
        {
            get => legacyBeatLength;
            set
            {
                if (value.HasValue && !double.IsFinite(value.Value))
                    throw new ArgumentOutOfRangeException(nameof(value), "Legacy beat length must be finite.");

                if (legacyBeatLength == value)
                    return;

                if (BeatLengthBindable.Disabled)
                    throw new InvalidOperationException("Cannot change legacy timing while beat length is disabled.");

                legacyBeatLength = value;
                RaiseChanged();
            }
        }

        /// <summary>
        /// The BPM at this control point.
        /// </summary>
        public double BPM => 60000 / BeatLength;

        public TimingControlPoint()
        {
            TimeSignatureBindable.BindValueChanged(change =>
            {
                if (!change.NewValue.Equals(change.OldValue))
                    LegacyTimeSignature = null;

                RaiseChanged();
            });
            OmitFirstBarLineBindable.BindValueChanged(_ => RaiseChanged());
            BeatLengthBindable.BindValueChanged(change =>
            {
                if (change.NewValue != change.OldValue)
                    LegacyBeatLength = null;

                RaiseChanged();
            });
        }

        // Timing points are never redundant as they can change the time signature.
        public override bool IsRedundant(ControlPoint? existing) => false;

        public override void CopyFrom(ControlPoint other)
        {
            if (ReferenceEquals(this, other))
                return;

            TimeSignature = ((TimingControlPoint)other).TimeSignature;
            LegacyTimeSignature = ((TimingControlPoint)other).LegacyTimeSignature;
            OmitFirstBarLine = ((TimingControlPoint)other).OmitFirstBarLine;
            BeatLength = ((TimingControlPoint)other).BeatLength;
            LegacyBeatLength = ((TimingControlPoint)other).LegacyBeatLength;

            base.CopyFrom(other);
        }

        public override bool Equals(ControlPoint? other)
            => other is TimingControlPoint otherTimingControlPoint
               && Equals(otherTimingControlPoint);

        public bool Equals(TimingControlPoint? other)
            => base.Equals(other)
               && TimeSignature.Equals(other.TimeSignature)
               && LegacyTimeSignature == other.LegacyTimeSignature
               && OmitFirstBarLine == other.OmitFirstBarLine
               && BeatLength.Equals(other.BeatLength)
               && LegacyBeatLength == other.LegacyBeatLength;

        public override int GetHashCode() => HashCode.Combine(base.GetHashCode(), TimeSignature, LegacyTimeSignature, BeatLength, LegacyBeatLength, OmitFirstBarLine);
    }
}
