# g0v0!

> [!IMPORTANT]
> **g0v0! is an unofficial osu!-related client based on the open-source [upstream codebase](https://github.com/ppy/osu).**
>
> - We (GooGuTeam) are not affiliated with ppy Pty Ltd, peppy, or team osu!. This project is neither supported nor endorsed by them.
> - **osu!** and **ppy** are registered trademarks of **ppy Pty Ltd**.
> - Please support the original project: play on the [official servers](https://osu.ppy.sh) and consider becoming a [supporter](https://osu.ppy.sh/home/support) to help fund upstream development.
> - For upstream issues and improvements, file issues / PRs on the [upstream repo](https://github.com/ppy/osu) or use the [upstream feedback forum](https://osu.ppy.sh/community/forums/5), rather than contacting the upstream team about g0v0!-specific problems. Report those to the relevant GooGuTeam repository instead.
> - If you have not played the original game, please [download and play the official client](https://osu.ppy.sh/home/download) first. This project is not a replacement for the original.

g0v0! is a free and open rhythm game client maintained by GooGuTeam, designed to connect to
[g0v0-server](https://github.com/GooGuTeam/g0v0-server). It extends the upstream client with custom server
integration, Relax / Autopilot special rulesets, and additional interface and gameplay features.

## Differences from upstream

The full list of changes is maintained on the
documentation site: [Differences from upstream](https://docs.g0v0.top/g0v0/difference.html).

The server-communication changes form the **g0v0! Standard** — a set of server extension conventions defined by
g0v0-server. Any client that implements the g0v0! Standard is fully compatible with every g0v0!-based
server, and g0v0! is the reference implementation. g0v0! Standard changes have been flagged in the differences list
above.

## Download

g0v0! is available for Windows (x64 / ARM64), macOS (Intel / Apple Silicon), Linux (x64 / ARM64), Android, and iOS. Grab
the latest build from [GitHub Releases](https://github.com/GooGuTeam/g0v0/releases/latest):

| Platform              | File                                                                   |
| --------------------- | ---------------------------------------------------------------------- |
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

- [**The upstream project**](https://github.com/ppy/osu) — the original client code, gameplay, rulesets,
  framework and resources on which this project is based. Our thanks to the upstream authors and community.
- [**Torii**](https://lazer.shikkesora.com) — the optional stable-style legacy interfaces (song select, results, footer)
  are ported
  from [torii](https://github.com/ShikkesoraSIM/torii) by ShikkesoraSIM, MIT licensed. Our thanks to
  ShikkesoraSIM for the
  legacy interface work.

## Licence

The code is licensed under the MIT Licence: the upstream portions under [LICENCE-OSU](./LICENCE-OSU), and GooGuTeam's
additions under [LICENCE](./LICENCE).

Game resources are derived from the [upstream resource repository](https://github.com/ppy/osu-resources) and are licensed under CC-BY-NC 4.0 for non-commercial use only.
See [LICENCE in g0v0-resources](https://github.com/GooGuTeam/g0v0-resources/blob/master/LICENCE.md) for details.
