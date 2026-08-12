# g0v0!

> This is a rhythm game client based on the open-source osu! codebase developed by ppy Pty Ltd and maintained by GooGuTeam.
> It includes the following changes on top of the upstream client to work with [g0v0-server](https://github.com/GooGuTeam/g0v0-server).

## Changes from upstream

### Custom API server

- Players can specify a custom API server URL in **Settings > Online > Web**, allowing connection to third-party servers.
- URL validation, debouncing, and a restart-required notice are provided in the settings UI.

### Special rulesets (Relax / Autopilot)

- Added first-class support for **Relax (RX)** and **Autopilot (AP)** as special game modes.
- Dedicated difficulty and performance (PP) calculators for the Relax mode, including a standalone `RelaxAimEvaluator`, `RelaxRhythmEvaluator`, and `Relax` difficulty skill.
- Profile and overlay ruleset selectors display special rulesets with a popover menu, and user statistics are fetched per special ruleset.

### Ruleset hash validation

- The client computes and sends SHA-256 hashes of loaded rulesets to the server via API requests and SignalR headers, enabling server-side integrity verification.
- When the server reports a ruleset is outdated, players receive an in-game notification with a direct download link.

### Server info notification

- On startup, a notification is displayed showing the currently connected server information.

### Issue reporting

- The "Report an issue" button now opens a confirmation dialog before redirecting to the GooGuTeam GitHub Issues page.

### Update source

- Desktop update checks and mobile update notifications point to the **GooGuTeam/osu** GitHub repository.
- A new **Disable automatic updates** option is available in the update settings.

### Error reporting

- Client-side crash reports are sent to a GooGuTeam-hosted Glitchtip instance instead of the upstream Sentry endpoint.

---

*All other gameplay, UI, and framework behaviour remains identical to upstream.*

## Licence

This project is based on the open-source codebase originally developed by ppy Pty Ltd.

GooGuTeam has made modifications, additions, and removals to the original codebase.
All original osu! code and framework remain licensed under the MIT License.
See [LICENSE-OSU](./LICENSE-OSU) for details.

All modifications and original contributions made by GooGuTeam are licensed under the MIT License.
See [LICENSE](./LICENSE) for details.

The "osu!" or "ppy" name, trademarks, logos, and other brand assets remain the property of ppy Pty Ltd.
This project is not affiliated with, endorsed by, or sponsored by ppy Pty Ltd.
