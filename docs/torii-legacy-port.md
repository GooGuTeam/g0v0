# Torii legacy interface port (work in progress)

## Pinned sources

- Code: https://github.com/ShikkesoraSIM/torii-osu at `8d7f1f25b737263dd140bbe59b0d51b999a7fc24` (MIT).
- Resources: https://github.com/ShikkesoraSIM/torii-resources at `b2d1d824275f6a52d97761841605db03321d7625` (CC BY-NC 4.0; individual font licences may differ).

Preserve upstream copyright notices. This is a port, not an original implementation by GooGuTeam.

## Implementation

The dedicated legacy song-select, ranking, and footer components are copied from the pinned source, not replaced with a simplified layout. Shared integration includes:

- `SongSelect`: live configuration, skin-change rebuilds, group collapse, legacy drag handling, footer hotkey forwarding, fixed-aspect presentation and lifecycle cleanup.
- `ScreenStackFooter`: owns the legacy footer, watches skin/configuration changes, yields to footer overlays, and forwards to real footer actions.
- `ScreenFooter`: original legacy takeover hooks; the existing default background is retained rather than bringing in Torii's unrelated global glass theme.
- `ResultsScreen`: legacy details overlay, direct solo results, original applause/flair handling and back/select behaviour; existing modern results remain separately available.
- Original legacy settings and `NewFeature` dependencies, original replay-render and score-note buttons, overlays, and API request classes.
- `ScalingContainer` and skin background lookup support used by legacy aspect locking.

## Configuration

The original Torii keys are used:

- `ToriiLegacyFooterUseSkin`
- `ToriiLegacySongSelectFooter`
- `ToriiStableResults`
- `ToriiLegacyFont`

The simplified `UseLegacySongSelect` / `UseLegacyResultsScreen` keys from the first port attempt were removed before release. The original settings subsection is mounted under the existing User Interface section; it does not replace other settings.

## Resource integration

The sibling `g0v0-resources` checkout contains 106 added original textures, including the ranking panel/dialog, song-select bars, mod icons and retry/replay buttons. Existing resources are not overwritten.

`-p:UseLocalResources=true` selects the sibling resource project for local development. The default remains the published `G0V0ResourcesVersion`; it does NOT include the unpublished resource additions. Publishing the resources and updating the pinned package version are separate, approval-required steps.

## Outstanding dependencies / differences

- Aller font support and its configuration are retained, but its binary font files have not been imported: the separate redistribution licence is unresolved. Do not claim that enabling Aller reproduces the upstream font yet.
- `OsuFont.GetFont()` still has this fork's existing default font mapping. Restoring Torii's default font globally would alter the modern UI; this needs a scoped decision, not an unannounced global replacement.
- Replay-render and score-note features use the original `torii/*` API routes on the configured server. Client code alone cannot provide backend support. No credentials or scores have been sent to Torii as part of this migration.
- Torii's automatic toolbar/feature promotional popups and its global theme system have not been integrated; the screen-specific UI should not be described as a complete Torii client.
- Newly imported original UI includes Torii wording/icons in auxiliary dialogs. No branding was silently removed or renamed.
- No build, test, or InspectCode run was performed during the latest continuation, per user request. Earlier smoke results do not validate the current complete integration.

## Verification still required when requested

Actual visual comparison with the pinned source; classic/default/custom skins; live skin and setting changes; footer takeover during mod selection; F1/F2/F3 and reverse random; aspect ratio / resize / navigation; solo and multiplayer ranking details; retry/replay and below-fold navigation; missing resource and font handling; modern UI/audio regression.
