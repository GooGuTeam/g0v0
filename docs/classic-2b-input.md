# 2B and Aspire: classic input and visual ordering

How g0v0 handles overlapping objects ("2B" maps) and extreme sliders (Aspire maps) with the Classic mod. The goal is maps that are readable and playable with deterministic, explainable rules; matching osu!stable exactly is not required, and known differences are listed below. Slider sizes on Aspire maps are covered by the slider viewport limit in [slider-rendering.md](slider-rendering.md#viewport-limit).

## Input

Beatmap 5564129 (set 90935) used to miss slider heads with Classic, including on Autoplay.

A judged slider head yields a press when an alive, visible, unjudged successor is hovered, has a valid hit result at the current time, passes the remaining legacy hit policy, and starts strictly after the old slider starts but before it ends. Successors are slider heads and ordinary hit circles (`LegacyHitPolicy.ShouldYieldSliderHeadInput`). This checks all alive objects, not just adjacent ones. Simultaneous starts do not qualify.

The stacked predecessor guard uses the same overlap relationship. Normal preceding-object checks and target hit windows remain. Nested slider heads are located through their parent in the top-level alive list.

The former broad rule that stopped blocking after the old head's late hit window has been removed. Input handling is shared by player, replay and Autoplay; no score or Autoplay-only exceptions are used. The change is independent of the circle rendering option.

This is intentional fork behaviour and may differ from stable. It can affect ordinary maps with the same overlap structure. Tick/tail success is not guaranteed, because Autoplay cursor tracking has independent limitations. Input-order validation against stable replays remains outstanding.

## Visual ordering

### Why proxies

Upstream draws each hit object as one drawable, so a slider's body, head, ticks and arrows always stay together in depth, and 2B maps hide circles behind other sliders' bodies ([ppy/osu#5539](https://github.com/ppy/osu/issues/5539), [#28624](https://github.com/ppy/osu/issues/28624)). In osu!stable the pieces of different objects interleave, and the visual order is independent of input priority: a slider drawn underneath can still take input first. g0v0 therefore reorders only the drawing, through proxies, and leaves input, judgement and pooling with the original drawables.

### Sort order

`OsuVisualOrdering` draws hit objects through proxies in one sorted container. With "Use stable-style 2B rendering" (`KeepOverlapCirclesVisible`, off by default) each piece (circle sprites, overlays, numbers, slider bodies, balls, ticks, repeat arrows) gets its own proxy and is ordered individually; with it off, whole objects are ordered with earlier objects on top, as upstream. The originals keep input, transforms and pooling. Each proxy stays alive for its whole registration, because a drawable can create only one proxy, so its visibility is set every frame from its owner:

- shown only while the owner is applied to a hit object and alive;
- a nested owner (slider head, tail, tick, repeat) is also hidden when its parent slider is not alive, since nothing updates a nested object's `IsAlive` after its parent leaves the hit object container;
- zero-length sliders stay hidden unless "show hidden sliders" is enabled;
- repeat arrows are hidden once their hit animation (at most 300 ms) has finished.

A proxy of an object that is not drawn this frame draws nothing (the framework only draws a proxy when its original was drawn in the same frame), so a stale proxy cannot show an old image.

Proxies are ordered by a total key (`VisualKey`), from back to front:

1. **Group:** slider balls are drawn above everything else.
2. **Time, later first:** a body uses its slider's end time, a tail its slider's end time, and every other piece its own object's start time (heads, circles, ticks, repeats). Earlier events are therefore drawn on top, and a body that ends earlier is drawn above one that ends later.
3. **Phase** at equal time: body, circle base, then number and overlay (in the skin's order), then arrows and kiai flashes, then ticks.
4. **Role** at equal time and phase: tail, ordinary circle, head, repeat.
5. **Declaration order** (position in the beatmap): a later object is drawn above an earlier one, except tails, where the earlier object is on top.
6. **Event ordinal** within the slider, then the piece kind.

Approach circles are ordered by declaration alone. Because the key is a total order, the result does not depend on pooling or registration order; the former pairwise overlap comparison was not transitive and has been removed.

### Basis

The rules were chosen from:

- **Screenshots of osu!stable** (build `b20260924.1`, Stable40) of fixed test maps, from replays and the editor. They show body order following end times in the "Window" maps, ticks drawn independently of their body, and same-time ties: later-declared circles, heads and repeats in front, earlier-declared tails in front.
- **Upstream investigations:** the per-sprite depth analysis in [ppy/osu#14561](https://github.com/ppy/osu/pull/14561), the circle sprite order in [#38245](https://github.com/ppy/osu/pull/38245) (included), and the 2024 correction in [#28624](https://github.com/ppy/osu/issues/28624#issuecomment-2208427381) that bodies are not always below circles.
- **danser-go** (commit `7cd9032`) as a design reference only: it orders a slider body by end time and the other pieces by start time. danser is GPLv3; none of its code, shaders or assets is used.

### Known differences and open questions

- Same-time ties between overlays, numbers and arrows could not be read from stable screenshots (identical white textures); the order used is the project's choice.
- Bodies with equal end times, more multi-span combinations, fade-out ("hidden sliders") settings and other skins were not compared with stable.
- Stable's notelock edge cases, slider head input consumption ([#24762](https://github.com/ppy/osu/pull/24762), [#27270](https://github.com/ppy/osu/issues/27270)) and tail check timing ([#32359](https://github.com/ppy/osu/issues/32359)) are separate from drawing and only partly addressed by the input rule above.
- Aspire path geometry, numeric precision and event generation follow lazer; only the slider viewport limit simulates stable's rendering.

### Lifetimes


### Lifetime checks

`OsuVisualOrdering` compares every proxy's visibility with the lifetimes of its owner and parent object, and checks that a nested object belongs to the slider it is drawn under and that each proxied piece is still part of its owner. The checks are enabled in debug builds, or in any build with the environment variable `G0V0_CHECK_VISUAL_LIFETIMES=1`. Findings are logged once per piece (at most 20) as `Visual ordering: …` lines in the runtime log and kept in `OsuVisualOrdering.LifetimeViolations`.

Containers update their children's `IsAlive` before those children change their own lifetimes (when judged, or when a seek reverts a judgement), so visibility can trail a lifetime change by one frame, as it does in upstream gameplay. While seeking, the clock can also sit just past a boundary for several frames before the container updates again. A mismatch is reported only when it lasts two frames or more and the time is more than one 60 Hz frame (about 17 ms) from the owner's and parent's lifetime boundaries; without that margin `TestSeeking` failed intermittently on a slider tick 0.3 ms past its end.

`TestSceneVisualOrderingLifetimes` plays a circle inside a slider, a slider inside a slider with repeats, a stack on a slider head, and a simultaneous circle and slider on autoplay with Classic. It covers a full play-through, seeking backwards and forwards across judgements, and switching to the classic skin mid-play (which re-registers the legacy circle pieces) followed by more seeks. No lasting violation occurs in these cases. Reports of lifetime errors in other patterns should include the logged `Visual ordering:` lines.

