# Sliderball visual position

`DrawableSliderBall` samples the supplied stable reference's movement timing through
`LegacySliderBallPosition`. This affects the skin's ball and follow circle. It does
not change slider bodies, geometry, duration, checkpoints, tracking or scoring.

The sampler reproduces these movement-only rules from `SliderOsu.UpdateCalculations`,
`pSprite.UpdateTransformations`, `OsuMathHelper.TweenValues(Vector2)` and
`AudioEngine.UpdateAudioTime`:

- Round the sampled clock to an integer millisecond, using ties-to-even.
- Accumulate edge durations as doubles in traversal order; truncate each movement's
  start and end separately. Preserve the float multiplication used by stable.
- At shared millisecond boundaries, use the first matching movement, including
  zero-duration movements. A zero-duration vector tween yields its start position.
- Before the animations, use the first start; after them, use the last endpoint.
- Select the same movement after seeking backwards as when visiting that time forwards.

The cache stores one path's edge durations and one span's integer endpoints. It
does not expand edges times repeats into movement objects. Span lookup preserves
cumulative floating-point addition, skipping repeated forward/backward pairs only
within a uniform double-precision interval when the repeated increment is established.
Search hints are capped at 256 entries.

Only an inner visual container moves to the sampled position. `Ball.Position` and
`Ball.DrawPosition` remain continuous for Magnetised, Repel and sample panning.
The inner offset compensates for circle-size scaling. Ball rotation and texture
animation retain their existing implementations. Out-of-range integer timing,
non-finite timing and missing geometry fall back to continuous visual positioning.

Tests compare the sampler with a deliberately slow movement-list reference across
random paths, curves, boundaries, seeks and large repeat counts. The offscreen
`TestSceneLegacySliderBallPosition` checks the visual shift while the gameplay
position remains unchanged across five skins. Actual stable geometry approximation,
audio smoothing and frame scheduling are outside this change's scope.
