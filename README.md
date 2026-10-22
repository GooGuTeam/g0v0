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

## Experimental SQLite storage

The debug settings backend selector requires a restart. Key bindings, mod presets,
ruleset settings, beatmap collections, and local scores use the selected backend.
Beatmaps, skins, and the remaining domains still use Realm.

SQLite scores are stored in `scores.db`, with replay content in the separate
`score-files/` directory so Realm file cleanup cannot remove SQLite-owned replays.
Local score lists, replay availability, deletion/restore, replay export, and background
score reprocessing use the selected score store. Beatmap associations are resolved
from the stored beatmap hash against the current Realm beatmap database.

Switching backends does not copy existing Realm data into SQLite. Each backend keeps
its own scores and replay files; switching back restores access to the original data.

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
