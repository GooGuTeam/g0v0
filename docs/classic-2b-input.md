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

- shown only while the owner is applied to a hit object, alive, and within its current `[LifetimeStart, LifetimeEnd)`;
- a nested owner (slider head, tail, tick, repeat) also requires its parent slider to be alive and within its current lifetime; cached `IsAlive` alone is insufficient after a judgement, seek, or parent removal;
- zero-length sliders stay hidden unless "show hidden sliders" is enabled;
- repeat arrows are hidden once their hit animation (at most 300 ms) has finished.

The rendering mode is captured when a playfield loads. Changing "Use stable-style 2B rendering" takes effect in the next playfield, not halfway through the current one: proxy creation is single-use, and pooled drawables must not mix proxied and unproxied modes. Existing proxies continue updating and unregistering until their playfield is disposed.

Legacy reverse arrows already create a proxy to escape their repeat's local container. In 2B mode that existing proxy is registered directly with `OsuVisualOrdering`, rather than left inside the slider's overlay layer. Proxying only the enclosing skinnable drawable does not change the arrow's drawing order. Outside 2B mode the existing slider overlay path is unchanged.

The framework validates a proxy's source draw node against the current rendering frame, preventing reuse of an old frame's image when the source is no longer traversed. This does not replace lifetime gating: an object's lifetime can change after its container has cached `IsAlive`, while the source is still traversed in that same frame.

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

### Lifetimes and diagnostics

Containers update their children's `IsAlive` before those children change their own lifetimes (when judged, or when a seek reverts a judgement). The ordering layer runs after the hit-object subtree and checks the updated lifetime bounds as well as `IsAlive`, closing expired gates immediately rather than waiting for another container update. This is independent of settings changes. Editor `KeepAlive` explicitly expands the entry's lifetime and remains supported; it is not inferred from an alive object being outside its lifetime.

`OsuVisualOrdering` optionally compares each gate with its owner's and parent's lifetimes and checks source/parent ownership. Enable this with `G0V0_CHECK_VISUAL_LIFETIMES=1` or `CheckLifetimes = true` in tests. Findings are logged once per source/problem (at most 20 log messages) as `Visual ordering: …` lines and retained in `LifetimeViolations` (at most 100). Enabled gates outside their current lifetime are reported immediately. The two-update/about-17-ms grace period applies only to unexpectedly hidden gates, not to expired gates. The previous blanket exemption for `IsAlive` outside the lifetime hid the very stale-state condition being investigated.

`TestSceneVisualOrderingFrameLifetimes` independently inspects gates at the end of each observed frame, after playfield updates, including approach-circle fallback gates. It covers fixed-settings continuous playback and deterministic seeks, legacy and triangles skins, with and without `FrameStabilityContainer`, same-update lifetime shortening for top-level objects, nested objects and their parents, and editor keep-alive. Against the pre-fix ordering code, both direct-seek cases and all three lifetime-shortening cases fail; a frame-stable legacy seek also failed in a recorded run. Ordinary continuous playback did not reproduce a failure. An enabled gate is not by itself evidence of visible stale pixels: the source may already be transparent or lack a validated draw node. Offscreen before/after captures of the controlled same-update lifetime-shortening case do demonstrate an expired circle being drawn before the fix and suppressed after it; this is not a reproduction of the user's specific gameplay sequence.

`TestSceneVisualOrderingLifetimes` additionally exercises the actual player with Classic and autoplay, overlapping circles/sliders, backwards/forwards seeks, and legacy skin replacement. These tests are regression coverage, not proof that every reported intermittent visual problem has been reproduced. For remaining cases, capture the affected time, skin, beatmap and runtime log.
