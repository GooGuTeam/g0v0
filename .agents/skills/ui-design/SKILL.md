---
name: ui-design
description: Design, polish, style, and inspect client UI across all screens (Song Select, Settings, Results/Ranking, Overlays, Dialogs, Toolbars, HUD, and Editor) in the g0v0 client. Use whenever a request is about how something looks or is laid out ("this is ugly", "make it cleaner", misaligned or overflowing controls, inconsistent corner radii, text that should be a tooltip, a cramped or empty area, responsive layout fixes), and before claiming a UI change looks right. Covers offscreen GPU rendering/captures across 580+ visual test scenes, design system tokens (OverlayColourProvider, OsuColour, OsuFont), component reuse, responsive layout rules, and defect checklists.
---

# UI design (g0v0 client)

Judge UI by looking at it, never by reading layout code. The offscreen previewer in `tools/Previewer`
runs the client UI on an offscreen GPU device (`OffscreenGameHost`), drives it with test scenes or
scripted steps, and saves PNGs with **no visible window**. Use it for every visual change across the
entire client: capture before, modify code, capture after, and compare.

Never open a visible game window for inspection, and keep preview runs focused.

## The universal design loop

1. **Identify the component or screen**:
   - Determine which visual test scene (`TestScene*`, over 580 available across `osu.Game.Tests`)
     hosts the control, or use `--scene=editor` for editor screens.
2. **Capture the current state ("before")**:
   - Capture at default desktop resolution (`1920x1080`) and compact resolution (`1280x720`).
   - Use `--locale=zh` (and `--locale=en`) if the screen contains translated or localized text.
3. **Audit concrete defects**:
   - Write down explicit defects before touching code: misaligned elements, wasted space vs cramped
     padding, uneven corner radii, inconsistent heights, unhandled text overflow, or wrong contrast.
4. **Change the code**:
   - Reuse standard design system components and tokens (`OverlayColourProvider`, `OsuFont`, form
     controls). Do not invent custom one-off boxes or hardcode literal colors.
5. **Capture again ("after") and compare**:
   - Re-run the previewer with identical parameters. Read the PNGs and verify that defects are
     resolved without clipping, overflow, or layout regression.
6. **Verify code sanity and build**:
   - `powershell -File ./CheckSanity.ps1 <dir>` (or `bash CheckSanity.sh <dir>`).
   - `dotnet build osu.Desktop.slnf -c Debug -warnaserror` (add `-p:G0V0SourceBuild=true` when
     working against unpublished sibling repository changes).

User-visible text changes must be synchronized via the `localisation-sync` skill (class and `.resx`
files together). Check `git diff` before restoring UI to ensure it was not removed intentionally.

## The offscreen previewer

### Running via helper scripts

Convenience scripts are provided in `scripts/`:

```bash
# Windows PowerShell
powershell -File .agents/skills/ui-design/scripts/preview.ps1 -Scene TestSceneSettingsPanel -Crop SettingsPanel -Locale zh
powershell -File .agents/skills/ui-design/scripts/preview.ps1 -Scripts "compose,timing" -Locale zh

# Bash (Linux / Git Bash / macOS)
bash .agents/skills/ui-design/scripts/preview.sh --scene=TestSceneSettingsPanel --crop=SettingsPanel --locale=zh
bash .agents/skills/ui-design/scripts/preview.sh --scripts=compose,timing --locale=zh
```

The runner scripts automatically detect local sibling repositories (like `../g0v0-framework`), apply
`-p:G0V0SourceBuild=true` when necessary, and build the previewer on demand.

### Building and running directly

Build (outputs stay in `tools/Previewer/bin/`, `obj/` and `captures/`, which are git-ignored):

```bash
dotnet build .agents/skills/ui-design/tools/Previewer -c Debug -p:G0V0SourceBuild=true
```

Run from the previewer directory:

```bash
cd .agents/skills/ui-design/tools/Previewer
./bin/Debug/net10.0/win-x64/Previewer.exe --scene=TestSceneSettingsPanel --crop=SettingsPanel --out=captures/settings --locale=zh

# several states in one run: before each matching step, plus the end state
./bin/Debug/net10.0/win-x64/Previewer.exe --scene=TestSceneSettingsRenderers --test=TestStableSidebarExpandsOnHover "--capture-before=hover a tab|move away" --out=captures/sidebar
```

The previewer is muted (a zero volume adjustment on its audio), and each run uses its own temporary storage folder, so runs can follow
each other or overlap.

### CLI argument reference

| Argument | Description | Default |
|---|---|---|
| `--scene=` | Name of any `TestScene*` in `osu.Game.Tests` / ruleset tests, or `editor` | `editor` |
| `--crop=` | Comma-separated type names to crop and save separately (matches exact type or base class) | None (full frame) |
| `--scripts=` | Comma-separated editor scripts to run sequentially when `--scene=editor` | `compose` |
| `--test=` | Test method name in the visual test scene | All / default |
| `--case=` | Test case index when the method defines multiple `[TestCase]` | `0` |
| `--stop-before=` | Substring of step text to stop execution at before capturing | None (runs to end) |
| `--capture-before=` | `\|`-separated substrings of step text; captures before each matching step, then keeps running (files get a `-NN-<step>` suffix; the end state is still captured) | None |
| `--out=` | Output directory for PNG captures (relative to working directory) | `captures` |
| `--size=` | Frame resolution (`WIDTHxHEIGHT`) | `1920x1080` |
| `--locale=` | Culture code (`zh`, `zh-Hant`, `en`); written to a clean `framework.ini` | System default / `zh` |
| `--ruleset=` | Ruleset short name (`osu`, `taiko`, `catch`, `mania`) | `osu` |
| `--wait=` | Settle delay in ms before capturing a test scene | `1500` |

## Visual test scene directory (580+ scenes)

Any visual test scene in the test assemblies can be rendered offscreen. Key test scenes by client
area:

| Subsystem | Key test scenes | Useful crops (`--crop=`) |
|---|---|---|
| **Settings & Flags** | `TestSceneSettingsPanel`, `TestSceneSettingsItem`, `TestSceneSettingsNumberBox`, `TestSceneSettingsCheckbox`, `TestSceneSettingsColour` | `SettingsPanel`, `SettingsSubSection`, `SettingsItem` |
| **Song Select** | `TestSceneSongSelect`, `TestSceneBeatmapCarousel`, `TestSceneBeatmapDetails`, `TestSceneWedge`, `TestSceneFooter` | `BeatmapCarousel`, `Wedge`, `BeatmapDetailArea` |
| **Ranking & Results** | `TestSceneRanking`, `TestSceneResultsScreen`, `TestSceneAccuracyCircle` | `ResultsScreen`, `AccuracyCircle` |
| **Overlays & Dialogs** | `TestSceneToolbar`, `TestSceneNotificationOverlay`, `TestSceneChatOverlay`, `TestSceneNowPlayingOverlay`, `TestSceneUserProfileOverlay`, `TestSceneDialogOverlay` | `Toolbar`, `NotificationOverlay`, `PopupDialog` |
| **Core UI Controls** | `TestSceneButtons`, `TestSceneDropdown`, `TestSceneTextBox`, `TestSceneContextMenu`, `TestScenePopover`, `TestSceneTooltip`, `TestSceneTabControl` | `RoundedButton`, `FormDropdown`, `Popover` |
| **Gameplay & HUD** | `TestSceneHudOverlay`, `TestSceneSongProgressBar`, `TestSceneScoreCounter`, `TestSceneHealthDisplay` | `SongProgressBar`, `ScoreCounter` |
| **Mods** | `TestSceneModSelectScreen`, `TestSceneModCustomisation` | `ModSelectScreen`, `ModPanel` |
| **Editor** | `--scene=editor --scripts=<scripts>` (see below) | See editor scripts |

### Editor scripts (`--scene=editor --scripts=...`)

| Script | Screens and controls captured |
|---|---|
| `compose` | Compose screen root, left and right toolboxes, bottom bar, timeline area, menu bar |
| `toolbox-hover` | Expanded toolbox states (right and left) |
| `context-menu` | Right-click object context menu and hovered AI item |
| `ai-settings`, `generation`, `gen-mode` | Mapping assistant popover, generation progress, and review mode |
| `bar-popover`, `timestamp-input` | Timeline bar properties popover, timestamp entry |
| `timing`, `timing-signature`, `ticks` | Timing control point list, settings, signature fields, grouping dropdown, timeline ticks |
| `verify` | Verify issue list, severity filters, issue selection details |
| `setup` | Song setup screen, metadata section, difficulty section |
| `compose-screen` | Helper step to switch mode back to Compose when chaining scripts |

## Design system & rules

These rules align with the client architecture and established conventions.

### 1. Colours and surfaces

- **Surfaces**: Derive from `OverlayColourProvider`:
  - `Background1`: Darkest base background (overlay backing, main viewport).
  - `Background2`–`Background3`: Primary card, panel, and container background.
  - `Background4`–`Background6`: Elevated cards, hovered surfaces, secondary controls.
- **Content and text**:
  - `Content1`: Primary text, active icons, high-contrast headings.
  - `Content2`: Secondary text, inactive captions, subheadings, unit labels.
  - `Content3`: Disabled text and borders.
- **Accents**:
  - `Colour1`–`Colour4`: Theme accent variations.
  - `Highlight1`: Focus rings, active selection glow, tabs highlight.
- **Semantic indicators (`OsuColour`)**:
  - Severity: `Red` (critical / error), `Yellow` (warning), `Green` (pass / online), `Blue` (info).
  - Grades: `Gray` (F), `Red` (D), `Orange` (C), `Yellow` (B), `Green` (A), `Silver`/`Gold` (S/SS).
- **Rule**: Never use arbitrary hex colors (for example `Color4.FromHex("#...")`) for surfaces,
  text, or borders. Always resolve dynamically via `OverlayColourProvider` or `OsuColour`.

### 2. Typography (`OsuFont`)

- **Font families**:
  - `OsuFont.Default` (Inter): Default UI typeface for all text and labels.
  - `OsuFont.Numeric` / `MapleMono`: Monospace/tabular digits for counters, BPM, timestamps, statistics.
  - `OsuFont.Torus`: Large headers, modal titles, brand elements.
- **Font sizes and weights**:
  - Tag / Micro: 10–12px (`Font = OsuFont.GetFont(size: 10, weight: FontWeight.SemiBold)`).
  - Caption / Footnote: 12–14px.
  - Standard Body: 14–16px (standard for field captions, dropdown values, button labels).
  - Subheading: 18–20px.
  - Section Heading: 24–28px.
- **Truncation and tooltips**:
  - Set `Truncate = true` on any text container displaying localized, translated, or user-supplied
    strings that could exceed column width.
  - Always pair truncated text with `TooltipText` so the complete string remains readable.

### 3. Layout, spacing, and dimensions

- **Spacing scale**:
  - Tight / micro padding: `4px`
  - Control internal gap: `8px`
  - Container / card padding: `10px`, `15px`, `20px`
  - Section / flow spacing: `25px`, `30px`
- **Control heights**:
  - Standard form controls (`FormTextBox`, `FormDropdown`, `RoundedButton`): `40px`.
  - Compact toolbar rows (Editor toolbars, sub-bars): `28px`.
  - All controls in the same row must share an identical height and vertical alignment (`Anchor = Anchor.CentreLeft`, `Origin = Anchor.CentreLeft`).
- **Corner radii**:
  - Badges, pills, tags: `4px`.
  - Textboxes, dropdowns, standard buttons: `6px`.
  - Cards, panels, toolboxes: `8px`–`10px`.
  - Major dialogs and screen overlays: `15px`–`20px`.
  - Any drawable specifying a `CornerRadius` MUST enable `Masking = true`.

### 4. Component catalog & reuse

- **Buttons**:
  - `RoundedButton`: Standard action button. Use default accent for the primary action; use
    `Background3` or `Background4` for secondary/cancel actions to maintain visual hierarchy.
  - `IconButton`: Compact icon-only button (always provide `TooltipText`).
  - `DangerousRoundedButton`: Destructive actions (delete, clear, reset) with red styling.
  - `ShearedButton`: Diagonal-cut action button for Song Select, ranking, and lazer footers.
- **Form controls**:
  - Use the form suite (`FormTextBox`, `FormNumberBox`, `FormDropdown<T>`, `FormCheckBox`,
    `FormSliderBar<T>`).
  - Explanations belong in tooltips: use `HintText` on form controls (renders as the ⓘ icon) or
    `TooltipText`. Avoid multi-line explanatory paragraphs underneath input fields.
  - In a `FormDropdown<T>`, items must not be `null` (key collision); use a sentinel value for "All".
- **Input validation & defaults**:
  - Invalid input should flash the control (for example `FlashOnInputError`) and revert to the last
    valid value; never fail silently.
  - Provide domain-smart defaults labelled clearly (for example `"Auto (...)"`).

### 5. Responsive layout & screen sizes

- Always check captures at both `1920x1080` and `1280x720`.
- Narrow or small window sizes must gracefully compact or hide secondary details (e.g. collapsed
  toolboxes hiding child controls via `IExpandingContainer.Expanded`), rather than squeezing text or
  causing control overlap.

## File hygiene & sanity checks

- Keep file encodings, BOMs, and line endings intact:
  - C# source files (`.cs`) and PowerShell scripts (`.ps1`) require **CRLF**.
  - Shell scripts (`.sh`) require **LF**.
- New files created for g0v0 use the single header:
  `// Copyright (c) GooGuTeam. Licensed under the MIT Licence.`
- Upstream files modified for g0v0 use the dual header:
  `// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.`
- Run sanity check before completing:
  `powershell -File ./CheckSanity.ps1 <directory>`
