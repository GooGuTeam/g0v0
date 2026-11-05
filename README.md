# g0v0!

g0v0! is a rhythm game client built and maintained by GooGuTeam on top of the
open-source [ppy/osu](https://github.com/ppy/osu) (osu!lazer) codebase, ready to work
with [g0v0-server](https://github.com/GooGuTeam/g0v0-server) out of the box.
g0v0! keeps all gameplay, UI, and framework behaviour of osu!lazer intact and only changes the parts that talk to the
server (for example a custom API server and Relax / Autopilot special rulesets).

## Differences from upstream

The full list of changes is maintained on the
documentation site: [Differences from osu!(lazer)](https://docs.g0v0.top/g0v0/difference.html).

The server-communication changes form the **g0v0! Standard** — a set of server extension conventions defined by
g0v0-server. Any client that implements the g0v0! Standard is fully compatible with every g0v0!-based
server, and g0v0! is the reference implementation. g0v0! Standard changes have been flagged in the differences list
above.

## Hitsounding

The editor includes a dedicated hitsounding mode. Every object gets the same five compact rows below
**Objects** and **Volume**: **Sample set** (the normal bank), **Additions** (the addition bank), then **Whistle**,
**Finish** and **Clap**. The rows keep a fixed height rather than stretching to fill the screen. Below them,
the selection panel edits the selected points' shared set, normal bank and addition bank, with Whistle / Finish / Clap
toggles (default shortcuts W / E / R).
Custom sample sets are created and configured in the editor's Setup screen, under the Resources section.

The Sample set and Additions rows show a compact, fixed-size badge — the bank letter (`N` / `S` / `D`) above the
numeric set index, with room for two characters and horizontal padding. Only discrete sound point times get
vertical guides, and neither the guides nor badge sizes depend on Compose's beat divisor. Alt + mouse wheel
smoothly changes the time spacing without shrinking badges or text; rapid wheel input accumulates towards one
zoom target. Hit samples store one set index per object, shared by all of its samples, so
changing the set on any row changes it for the whole object. **View → Default Normal**, directly below **Timeline**, offers three
mutually exclusive display modes: **Do not hide**, **Hide Normal0** (the initial setting), and **Hide Normal1**.
The preference is saved and updates badges immediately, without changing selection, sample assignments or playback.
Other bank and set combinations remain visible — for example, Normal set 2 reads `N` over `2`.
Resetting the Sample set row still chooses beatmap set 1 when custom hitsound files exist, otherwise user skin set 0;
this is independent of the visibility preference. A blank Additions badge means the addition bank follows the normal
bank (Auto); an object needs at least one addition sound before its addition bank can be configured. Resetting
Additions to Auto leaves the shared set index untouched.

Whistle, Finish and Clap toggle the matching addition sound on the selection; the object's own body sample supports
only the whistle, so Finish and Clap exist purely as additions. Clicking an unselected point targets that point;
clicking a selected point edits the current selection. Ctrl-click or a box selection collects several points.
The existing bank and addition keyboard shortcuts also work on the hovered point without selecting it first.
Ctrl + mouse wheel over a bank badge cycles the available sample sets without changing the selection.
Bank and set edits and every toggle are undoable as one change. Cells that cannot be edited — file-backed samples,
or slider nodes that were never materialised — are disabled. Hover panels show each available action with its shortcut
in a separate keycap; holding Ctrl over the Volume graph switches the panel to the sample-point split action.

Slider body samples use a translucent, clickable rectangle spanning the slider's duration instead of a middle
vertical guide. The body bank appears as a horizontal capsule (`N 1`, with the bank on the left and set on the
right), with matching positions in the Additions and Whistle rows. Finish and Clap remain disabled for bodies.
Capsules disappear when a slider span is too narrow to fit them between its hit nodes, but the interval remains
clickable for bank edits, whistle toggles and selection. Head, repeat and tail cells stay above the fill and keep
their own independent editing targets.

Explicit sample edits audition the resulting sounds without moving the playhead; unchanged values and undo do not
trigger an audition. Normal playback lights sounding points with a soft white edge bloom and a 70% white
overlay, fading together over 700 ms with OutQuint easing. Slider heads flash independently, while the body
keeps a much fainter contour during playback. At the tail, the body and tail brighten together and share
the same 700 ms fade. The body overlay includes the complete head badge. Its 5 px rounded outer
contour retains space for the soft bloom rather than clipping it into square corners. Repeat nodes
emphasise their top and bottom edges, with much fainter side edges.
All slider highlights follow the outer contour without bright internal joins. Body fills extend to the
same endpoint edges as their contour. Each lane only flashes its own sounding nodes; head-only additions
never create an empty body or tail outline. The playhead stays above these highlights. Seeking and edit
auditions do not trigger playback flashes. The workspace background is 90% opaque.
The Volume graph uses compact numeric labels, pink selected runs and a white-ringed point for precise Ctrl-click
editing; dragging changes a run, while double-clicking opens its precise volume editor.

The Objects row retains its left header and reuses Compose's complete TimelineArea, including its zoom buttons,
beat-divisor slider, custom divisor input and preset selector. Volume and sample lanes use the timeline's current
animated zoom and scroll coordinates, keeping objects, sound points and the playhead aligned. Bar, beat and
subdivision ticks extend through Volume to the bottom of Clap, but never into the sample workspace.
These Objects ticks are the only timing grid; sound points do not add independent vertical guide lines.
The mouse wheel still seeks the cursor, snapping to the closest sound point rather than to the editor's beat grid;
Alt-wheel uses the same zoom as the Compose toolbar.

The lower **Preview** workspace lists user skin set 0, beatmap set 1 and available custom sets in a wrapping grid
of up to five columns. Filter cards by set number and adjust the preview radius in seconds (default ±3 seconds).
The Preview tab stays visible while its settings and sample cards scroll together, keeping the sets reachable
when a short window forces the settings onto multiple rows.
The optional range highlight spans the selected points, including a slider body's full duration. Playing a card
starts the editor's music over that range and temporarily replaces only the selected points' sample set; their
banks, additions and volumes remain intact. Every unselected point is silent during Preview, including slider
heads, repeats, tails and body loops. Preview does not edit the beatmap. It stops at the range boundary;
selecting other points, editing, seeking or changing mode
clears the temporary override. Set-only edits in the selection panel preserve independently assigned banks and
remain undoable as one change.

## Download

g0v0! is available for Windows (x64 / ARM64), macOS (Intel / Apple Silicon), Linux (x64 / ARM64), Android, and iOS. Grab
the latest build from [GitHub Releases](https://github.com/GooGuTeam/g0v0/releases/latest):

| Platform              | File                                                                   |
|-----------------------|------------------------------------------------------------------------|
| Windows x64           | `g0v0-win-Setup.exe` (installer) or `g0v0-win-Portable.zip` (portable) |
| Windows ARM64         | `g0v0-win-arm64-Setup.exe` or `g0v0-win-arm64-Portable.zip`            |
| macOS (Intel)         | `g0v0.app.Intel.zip`                                                   |
| macOS (Apple Silicon) | `g0v0.app.Apple.Silicon.zip` or `g0v0-osx-arm64-Setup.pkg`             |
| Linux x64             | `g0v0.AppImage`                                                        |
| Linux ARM64           | `g0v0-arm64.AppImage`                                                  |
| Android               | `g0v0.apk`                                                             |
| iOS                   | `g0v0-ios.ipa`                                                         |

Desktop builds (Windows / macOS / Linux) ship with automatic updates and will follow new releases after installation;
automatic updates can also be disabled in the update settings. To connect the client to your own server,
see [Connect from the Client](https://docs.g0v0.top/en/lazer/deploy/login-from-client.html).

## Credits

- [**osu!**](https://osu.ppy.sh) — this client is built on the open-source [ppy/osu](https://github.com/ppy/osu) (osu!
  lazer) codebase
  developed by ppy Pty Ltd. Gameplay, rulesets, UI framework and game resources come from the osu! project; our thanks
  to ppy Pty Ltd and the osu! community for the original work this project is based on.
- [**Torii**](https://lazer.shikkesora.com) — the optional stable-style legacy interfaces (song select, results, footer)
  are ported
  from [torii-osu](https://github.com/ShikkesoraSIM/torii-osu) by ShikkesoraSIM, MIT licensed. Our thanks to
  ShikkesoraSIM for the
  legacy interface work.

## Licence

The code is licensed under the MIT Licence: the upstream ppy parts under [LICENCE-OSU](./LICENCE-OSU), and GooGuTeam's
additions under [LICENCE](./LICENCE).

Game resources are derived from ppy/osu-resources and are licensed under CC-BY-NC 4.0 for non-commercial use only.
See [LICENCE in g0v0-resources](https://github.com/GooGuTeam/g0v0-resources/blob/master/LICENCE.md) for details.

The "osu!" name, trademarks, and brand assets remain the property of ppy Pty Ltd; this project is not affiliated with or
endorsed by ppy Pty Ltd.
