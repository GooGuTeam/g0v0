# Sliderball visual position

`DrawableSliderBall` samples the supplied stable 20260924.1 original-IL reference's movement timing through
`LegacySliderBallPosition`. This affects the skin's ball and follow circle. It does
not change slider bodies, geometry, duration, checkpoints, tracking or scoring.

The sampler reproduces these movement-only rules from `SliderOsu.UpdateCalculations`,
`pSprite.UpdateTransformations`, `OsuMathHelper.TweenValues(Vector2)` and
`AudioEngine.UpdateAudioTime`:

- Round the sampled clock to an integer millisecond, using ties-to-even.
- Accumulate edge durations as doubles in traversal order; truncate each movement's
  start and end separately. The cumulative-length difference is a float, but the
  x86 JIT does not round the following multiplication by 1000 to float before its
  double division. Use a double product (exact for float × 1000) to preserve that
  behaviour on modern runtimes. Premature float rounding can shift a boundary
  by 1ms, selecting an unrelated huge-coordinate edge.
- At shared millisecond boundaries, use the first matching movement, including
  zero-duration movements. A zero-duration vector tween yields its start position.
- Keep endpoint coordinate subtraction in double for the vector tween, matching
  the x86 intermediate passed into `ApplyEasing`. Rounding the difference to float
  before interpolation can lose whole pixels on huge-coordinate edges.
- Before the animations, use the first start; after them, use the last endpoint.
- Select the same movement after seeking backwards as when visiting that time forwards.

The cache stores one path's edge durations and one span's integer endpoints. It
does not expand edges times repeats into movement objects. Span lookup preserves
cumulative floating-point addition, skipping repeated forward/backward pairs only
within a uniform double-precision interval when the repeated increment is established.
Search hints are capped at 256 entries.

The visual ball container moves directly to the sampled position, avoiding a
continuous-position-plus-offset cancellation at huge coordinates. Gameplay
consumers use the separate continuous `GameplayPosition` / `GameplayScreenSpaceCentre`
for Magnetised, Repel and sample panning. Non-legacy skins retain the existing
continuous tangent rotation. Legacy skins apply the original active movement's
angle, including phase-preserving reverse handling, relative to that rotation. Out-of-range integer timing,
non-finite timing and missing geometry fall back to continuous visual positioning.

Tests compare the sampler with a deliberately slow movement-list reference across
random paths, curves, boundaries, seeks and large repeat counts. Fixed 20260924.1
original-IL golden samples additionally cover collapsed boundaries, ties-to-even
and huge-coordinate interpolation. The external offscreen fixture compared
21,594 clocks across 13 real objects, including unordered seeks, with zero
position differences above 0.01 field units after the precision correction. The offscreen
`TestSceneLegacySliderBallPosition` checks the visual shift while the gameplay
position remains unchanged across five skins. Audio smoothing and a complete gameplay/audio state machine are outside this
change's scope.

## Legacy rotation, flip and animation frames

`LegacySliderBallAnimation` implements the original `pAnimation.Reverse` phase
adjustment before sampling frames. `LegacySliderBallTiming` builds a separate
visual tick cursor; it does not alter lazer's judgement events. The previous
update's cursor determines reversal. Repeated updates at the same integer clock
therefore still matter; this state must not be cached by clock alone.

When no movement is active, preserve the previous rotation instead of inventing
a tangent at the start/end. The initial vertical flip is derived from the first
original edge. `SliderBallFlip` defaults to false; the old `SliderBallDontRotate`
alias overrides it when supplied, matching the original skin parser. Shadow and
specular layers remain unrotated. Pool reuse, path edits, velocity/repeat changes
reset the visual state. Tick reconstruction has bounded work/storage and falls
back to the normal animation when its inputs are unsupported.

The original-IL fixture checks forward, seek, held-clock and 60 Hz update histories,
with tracking both off and on. Actual Drawable captures additionally replay their
recorded update history through the original target: frame, angle and flip are
compared under identical history, rather than comparing a seek to a 1ms walk.

## Skin-branch body follow-up

The Argon/Argon Pro oversized-body follow-up changes no sliderball source.
Its published Release ruleset passes the same 21,594 position samples and the
37 position/animation/mesh regression tests.
