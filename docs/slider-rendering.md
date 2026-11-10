# Slider rendering

## Settings

In the osu! ruleset settings (`OsuSettingsSubsection`, stored by `OsuRulesetConfigManager`):

| Setting | Default | Effect |
| --- | --- | --- |
| Slider viewport mode / custom size (`SliderViewportMode`, `SliderViewportCustomSize`) | Default / `1920x1080` | Default reproduces the tested stable viewport ceiling; Custom requests independent X/Y limits |
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
The follow-up covers actual published Release binaries with Argon Pro and classic
at 720p/1080p, plus a truly MultiThreaded offscreen host.

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

Validation: 6 objects × 3 progress states × 2 display conditions have matching
prepared line counts and viewport bounds versus the original 20260924.1 IL.
The staircase at 149574ms and ellipse at 213173ms have restored large-scale
appearance at 720p/1080p on D3D11 and Vulkan. Gradient/raster edge differences are
measured separately; a coarse silhouette pass is not pixel identity.
