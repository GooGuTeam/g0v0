# Slider rendering

## Settings

In the osu! ruleset settings (`OsuSettingsSubsection`, stored by `OsuRulesetConfigManager`):

| Setting | Default | Effect |
| --- | --- | --- |
| Slider viewport size (`SliderViewportSize`) | 0 (0–65536 px) | Size limit for slider bodies; 0 uses the renderer's maximum texture size |
| Use stable-style 2B rendering (`KeepOverlapCirclesVisible`) | off | Orders slider bodies, circles and markers by stable's visual priorities ([classic-2b-input.md](classic-2b-input.md)) |
| Show hidden sliders (`ShowHiddenSliders`) | off | Draws sliders shorter than 1 ms, with their circles and markers |

None of these change hit judgement.

### Viewport limit

A slider body wider or taller on screen than the limit is drawn scaled down by `limit / size`, anchored at the body's top-left corner on screen. This simulates stable's viewport limit, to reproduce how very large sliders look on Aspire maps. Only the drawn stroke is scaled; positions used for judgement and snaking are unchanged (`PlaySliderBody.UpdateAfterChildren`).

## Path rendering

A slider body keeps the full path geometry and bounds fixed; snaking only changes the visible range (`Path.DrawRange`). `SliderPath.GetPathToProgressRange` finds that range by binary search over cumulative segment lengths.
