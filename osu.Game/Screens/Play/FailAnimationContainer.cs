// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using ManagedBass.Fx;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.Track;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Utils;
using osu.Game.Audio;
using osu.Game.Audio.Effects;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.UI;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Play
{
    /// <summary>
    /// Manage the animation to be applied when a player fails. Applies the animation to children.
    /// Single use and automatically disposed after use.
    /// </summary>
    public partial class FailAnimationContainer : Container
    {
        public Action? OnComplete;

        private readonly DrawableRuleset drawableRuleset;
        private readonly BindableDouble trackFreq = new BindableDouble(1);
        private readonly BindableDouble volumeAdjustment = new BindableDouble(0.5);

        private Container filters = null!;

        private Box redFlashLayer = null!;

        private Track track = null!;

        private AudioFilter failLowPassFilter = null!;
        private AudioFilter failHighPassFilter = null!;

        private const float duration = 2500;

        private SkinnableSound failSample = null!;

        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        protected override Container<Drawable> Content { get; } = new Container
        {
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            RelativeSizeAxes = Axes.Both,
        };

        /// <summary>
        /// The player screen background, used to adjust appearance on failing.
        /// </summary>
        public BackgroundScreen? Background { private get; set; }

        public FailAnimationContainer(DrawableRuleset drawableRuleset)
        {
            this.drawableRuleset = drawableRuleset;

            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load(AudioManager audio, IBindable<WorkingBeatmap> beatmap)
        {
            track = beatmap.Value.Track;
            AddInternal(failSample = new SkinnableSound(new SampleInfo("Gameplay/failsound")));

            AddRangeInternal(new Drawable[]
            {
                filters = new Container
                {
                    Children = new Drawable[]
                    {
                        failLowPassFilter = new AudioFilter(audio.TrackMixer),
                        failHighPassFilter = new AudioFilter(audio.TrackMixer, BQFType.HighPass),
                    },
                },
                Content,
                redFlashLayer = new Box
                {
                    Colour = Color4.Red.Opacity(0.6f),
                    RelativeSizeAxes = Axes.Both,
                    Blending = BlendingParameters.Additive,
                    Depth = float.MinValue,
                    Alpha = 0
                },
            });
        }

        private bool started;
        private bool filtersRemoved;
        private bool legacyAnimation;
        private double legacyStartTime;
        private const double legacy_frame_duration = 1000.0 / 60;
        // The track API exposes a relative frequency, not the stream's native sample rate.
        // Use stable's 44.1 kHz reference for the 300 Hz-per-frame slowdown.
        private const double legacy_duration = 147 * legacy_frame_duration;

        /// <summary>
        /// Start the fail animation playing.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if started more than once.</exception>
        public void Start()
        {
            if (started) throw new InvalidOperationException("Animation cannot be started more than once.");
            if (filtersRemoved) throw new InvalidOperationException("Animation cannot be started after filters have been removed.");

            started = true;
            legacyAnimation = config.Get<bool>(OsuSetting.ToriiLegacyGameplayScreens);
            legacyStartTime = Time.Current;

            if (legacyAnimation)
            {
                this.TransformBindableTo(trackFreq, 0, legacy_duration).OnComplete(_ =>
                {
                    removeFilters(false);
                    OnComplete?.Invoke();
                });
                track.AddAdjustment(AdjustableProperty.Frequency, trackFreq);
                failSample.Play();
                updatePlayfields();
                return;
            }

            this.TransformBindableTo(trackFreq, 0, duration).OnComplete(_ =>
            {
                // Don't reset frequency as the pause screen may appear post transform, causing a second frequency sweep.
                removeFilters(false);
                OnComplete?.Invoke();
            });

            failHighPassFilter.CutoffTo(300);
            failLowPassFilter.CutoffTo(300, duration, Easing.OutCubic);
            failSample.Play();

            track.AddAdjustment(AdjustableProperty.Frequency, trackFreq);
            track.AddAdjustment(AdjustableProperty.Volume, volumeAdjustment);

            updatePlayfields();
            drawableRuleset.Playfield.HitObjectContainer.FadeOut(duration / 2);

            if (config.Get<bool>(OsuSetting.FadePlayfieldWhenHealthLow))
                redFlashLayer.FadeOutFromOne(1000);

            Content.Masking = true;

            Content.Add(new Box
            {
                Colour = Color4.Black,
                RelativeSizeAxes = Axes.Both,
                Depth = float.MaxValue
            });

            Content.ScaleTo(0.85f, duration, Easing.OutQuart);
            Content.RotateTo(1, duration, Easing.OutQuart);
            Content.FadeColour(Color4.Gray, duration);

            // Will be restored by `ApplyToBackground` logic in `SongSelect`.
            Background?.FadeColour(OsuColour.Gray(0.3f), 60);
        }

        /// <summary>
        /// Stops any and all persistent effects added by the ongoing fail animation.
        /// </summary>
        public void Stop()
        {
            failSample.Stop();
            removeFilters();
            clearAppliedObjects();
        }

        public override void FinishTransforms(bool propagateChildren = false, string? targetMember = null)
        {
            if (started && legacyAnimation && string.IsNullOrEmpty(targetMember))
            {
                legacyStartTime = Time.Current - legacy_duration;
                updatePlayfields();
            }

            base.FinishTransforms(propagateChildren, targetMember);
        }

        private void removeFilters(bool resetTrackFrequency = true)
        {
            filtersRemoved = true;

            if (!started)
                return;

            if (resetTrackFrequency)
                track.RemoveAdjustment(AdjustableProperty.Frequency, trackFreq);

            track.RemoveAdjustment(AdjustableProperty.Volume, volumeAdjustment);

            if (filters.Parent == null)
                return;

            RemoveInternal(filters, true);
        }

        protected override void Update()
        {
            base.Update();

            if (!started)
                return;

            updatePlayfields();
        }

        private sealed class AppliedObject
        {
            public required double FailTime { get; init; }
            public required float RandomRotation { get; init; }
            public required Vector2 OriginalScale { get; init; }
            public required Vector2 OriginalPosition { get; init; }
            public required float OriginalRotation { get; init; }
            public Action<DrawableHitObject, ArmedState>? Handler { get; set; }
        }

        private readonly Dictionary<DrawableHitObject, AppliedObject> appliedObjects = new Dictionary<DrawableHitObject, AppliedObject>();
        private readonly HashSet<DrawableHitObject> currentAliveObjects = new HashSet<DrawableHitObject>();

        private void updatePlayfields()
        {
            currentAliveObjects.Clear();
            applyToPlayfield(drawableRuleset.Playfield);

            if (appliedObjects.Count > currentAliveObjects.Count)
            {
                foreach (var (obj, state) in appliedObjects.Where(kvp => !currentAliveObjects.Contains(kvp.Key)).ToList())
                {
                    releaseAppliedObject(obj, state);
                    appliedObjects.Remove(obj);
                }
            }
        }

        private void applyToPlayfield(Playfield playfield)
        {
            double failTime = playfield.Time.Current;

            if (legacyAnimation)
            {
                double elapsed = Math.Max(0, Time.Current - legacyStartTime);
                playfield.HitObjectContainer.Alpha = elapsed >= legacy_duration
                    ? 0
                    : Math.Max(0, 1 - (float)(elapsed / legacy_frame_duration) * 0.007f);
            }

            foreach (var nested in playfield.NestedPlayfields)
                applyToPlayfield(nested);

            foreach (var entry in playfield.HitObjectContainer.AliveEntries)
            {
                var obj = entry.Value;
                currentAliveObjects.Add(obj);

                if (!appliedObjects.TryGetValue(obj, out var state))
                {
                    float rotation = legacyAnimation
                        ? (RNG.NextBool() ? 1f : -1f) * RNG.NextSingle(0.005f, 0.02f)
                        : RNG.NextSingle(-90, 90);

                    state = new AppliedObject
                    {
                        FailTime = failTime,
                        RandomRotation = rotation,
                        OriginalPosition = obj.Position,
                        OriginalScale = obj.Scale,
                        OriginalRotation = obj.Rotation,
                    };

                    // need to reapply the fail drop after judgement state changes
                    state.Handler = (_, _) => dropOffScreen(obj, state);
                    obj.ApplyCustomUpdateState += state.Handler;
                    appliedObjects[obj] = state;

                    dropOffScreen(obj, state);
                }
                else if (legacyAnimation)
                {
                    // Drive legacy drop directly from wall-clock Time.Current each frame so the decelerating/stopped
                    // gameplay audio clock never freezes or desynchronises hit object transforms.
                    dropOffScreen(obj, state);
                }
            }
        }

        private void dropOffScreen(DrawableHitObject obj, AppliedObject state)
        {
            if (legacyAnimation)
            {
                // stable updates each sprite at 60 Hz, without shrinking the whole playfield.
                // Rotation values in stable are radians; framework drawable rotations are degrees.
                double elapsed = Math.Clamp(Time.Current - legacyStartTime, 0, legacy_duration);
                float elapsedFrames = (float)(elapsed / legacy_frame_duration);

                // Ensure objects at Y <= 0 (such as Taiko hit objects or circles near the top edge) still accelerate downwards.
                float dropBase = MathF.Max(0f, state.OriginalPosition.Y) + 150f;
                float y = state.OriginalPosition.Y + dropBase * (MathF.Pow(1.01f, elapsedFrames) - 1f);
                float radians = state.RandomRotation + MathF.Sign(state.RandomRotation) * 0.01f * elapsedFrames;

                obj.Position = new Vector2(state.OriginalPosition.X, y);
                obj.Rotation = state.OriginalRotation + radians * (180f / MathF.PI);
                return;
            }

            using (obj.BeginAbsoluteSequence(state.FailTime))
            {
                obj.RotateTo(state.RandomRotation, duration);
                obj.ScaleTo(state.OriginalScale * 0.5f, duration);
                obj.MoveTo(state.OriginalPosition + new Vector2(0, 400), duration);
            }
        }

        private void clearAppliedObjects()
        {
            foreach (var (obj, state) in appliedObjects)
                releaseAppliedObject(obj, state);

            appliedObjects.Clear();
            currentAliveObjects.Clear();
        }

        private void releaseAppliedObject(DrawableHitObject obj, AppliedObject state)
        {
            if (state.Handler != null)
                obj.ApplyCustomUpdateState -= state.Handler;

            if (legacyAnimation)
            {
                obj.Position = state.OriginalPosition;
                obj.Rotation = state.OriginalRotation;
                obj.Scale = state.OriginalScale;
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            clearAppliedObjects();
        }
    }
}
