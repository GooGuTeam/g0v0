# Slider rendering

## Known compatibility limitation

The oversized-body compatibility work is **not visually complete**. The exterior
fill around the ellipse at 213173ms has been reported as incorrect compared with
stable gameplay. Earlier isolated-renderer/screenshot agreement did not validate
stable's complete rendering/composition path and is not acceptance evidence for
that fill. Historical body screenshot generation has been suspended at the user's
request; do not use its outputs as golden images.

## Settings

In the osu! ruleset settings (`OsuSettingsSubsection`, stored by `OsuRulesetConfigManager`):

| Setting | Default | Effect |
| --- | --- | --- |
| Slider viewport mode / custom size (`SliderViewportMode`, `SliderViewportCustomSize`) | Default / `1920x1080` | Default currently emulates the historical diagnostic ANGLE viewport ceiling, not a universal stable limit; Custom requests independent X/Y limits |
| Use stable-style 2B rendering (`KeepOverlapCirclesVisible`) | off | Orders slider bodies, circles and markers by stable's visual priorities ([classic-2b-input.md](classic-2b-input.md)) |
| Show hidden sliders (`ShowHiddenSliders`) | off | Draws sliders shorter than 1 ms, with their circles and markers |

None of these change hit judgement.

### Viewport limit

Ordinary slider bodies use the range mesh and existing viewport handling in
`PlaySliderBody`. Oversized bodies in every skin use `LegacySliderBodyRenderer`: original
absolute lines, stable's reduction/forced-end flags, screen-space bounds and a
ridge/cap mesh. They do not pass viewport compression through `Drawable.Scale`
(which clamps tiny scales) or through an inverse huge-bounds transform (which can
lose visible coordinates).

Only rendering changes. Original gameplay/snaking endpoints and input geometry
remain untouched. The legacy mesh renders into a screen-sized cached FBO, then
composites at the clipped integer viewport origin. The projection is cropped to
emulate `limit / requestedSize`; a huge physical viewport is not submitted to the
driver. The ridge mesh uses D32; this is not a claim of identical D16
rasterisation on every GPU. A separate framework fix defers old depth attachment
disposal on FBO resize, matching the lifetime of the old framebuffer. The original
immediate disposal caused Vulkan device loss in the in-frame resize regression;
with deferred disposal, both D16 and D32 pass on the tested device.
The shadow/border/body gradient follows the legacy profile; ordinary bodies keep
their existing implementation and allocate no extra oversized renderer resources.

The oversized branch is owned by `PlaySliderBody`, not `LegacySliderBody`: this is
essential for Argon/Argon Pro and triangles skins to receive the same large-path
geometry fix. Legacy bodies keep the stable gradient; other skins provide their
existing `DrawableSliderPath.ColourAt` profile through an internal sampler. This
keeps each skin's accent, alpha, radius and border rather than recolouring Argon
to look like classic. The gradient delegate is cached per path and reset on pool
return. Colour/profile/border changes invalidate the cached FBO.

The first external delivery only attached the renderer to `LegacySliderBody`;
Argon Pro consequently retained the staircase stretching and ellipse fragments.
The follow-up exercised actual published Release binaries with Argon Pro and classic
at 720p/1080p, plus a truly MultiThreaded offscreen host. These runs established
branch coverage, not correctness of the disputed ellipse fill or intermittent
behaviour during gameplay.

## Oversized classic gradient

`LegacySliderBodyGradient` isolates the classic oversized body's 128-texel ramp.
It starts at transparent **black** (osuTK's `Color4.Transparent` is transparent
white), computes the original byte-domain colour endpoints, then interpolates
at texel centres. Darkening divides by 1.1; lightening uses `value * 1.125 + 63.75`.
Both endpoints are truncated to bytes before interpolation, with body alpha 180.
The shadow alpha is 64. Anti-aliasing uses the screen-space radius and is clamped
to `[3/256, 1/16]`, as in stable's `DefaultRadius` setup. The gradient cache includes
that radius, so display-scale changes cannot retain a gradient for the old scale.

These corrections apply only to the default oversized classic profile. Ordinary
classic paths retain their distance-field/filtering pipeline, and other skins
retain their supplied profile, alpha and cache behaviour. No global gamma,
blending, mesh, viewport or sliderball changes accompany this correction.

Regression coverage includes original-IL byte endpoints, all 256 input channel
values, seven complete CPU ramps at 720p/1080p, border alpha and display-scale
cache invalidation. The ramp expectations model ideal interpolation, not an
assertion that every GL driver rasterises the gradient identically. Scoped
before/after captures check the change; they do not resolve the exterior-fill
limitation described above.

## Path rendering

A slider body keeps full gameplay path geometry and bounds fixed; snaking changes
its visible range (`Path.DrawRange`). `SliderPath.GetPathToProgressRange` finds that
range by binary search over cumulative lengths. Oversized rendering uses
those progress endpoints but reduces the independent original edge list, keeping
gaps which cannot be represented by a contiguous vertex chain.

The cached render data stores compact lines, not expanded cap triangles. Vertices
are streamed through fixed-size renderer batch chunks only when the FBO needs
redrawing. The backend may retain multiple chunks for GPU in-flight safety, so
GPU batch storage still scales with visible mesh complexity; this is not a claim
of a constant total GPU-memory bound.
The FBO is shared between the drawable's draw-node copies; shader/texture/FBO
resources are disposed through the renderer. `RecyclePath` drops the disposed
renderer so pooled sliders do not reuse dead resources.

Historical preparation checks: 6 objects × 3 progress states × 2 display
conditions had matching prepared line counts and viewport bounds versus the
isolated original 20260924.1 IL. This is geometry-preparation evidence only.
The previous claim that the ellipse's large-scale appearance was restored is
withdrawn: the exterior fill is a known unresolved discrepancy, not merely an AA
or border difference. The shared FBO/depth lifetime regression is separate from
visual acceptance of this object.

CPU investigation of the published Release mesh has traced selected filled pixels
to very thin horizontal path segments after viewport compression. Thus stale FBO
contents are not required to explain those pixels. Changing the emulated limit
from 32767 to 32768 changes their gradient samples but does not by itself remove
their coverage. A separate no-op centre-anchor transform loses precision and is
currently mistaken for shake displacement. Neither observation alone establishes
the cause of intermittent real-gameplay behaviour. In particular, gameplay's
head-hit-dependent snaking-out changes the remaining path bounds; the historical
body capture forced snaking-out off and could not exercise this distinction.
