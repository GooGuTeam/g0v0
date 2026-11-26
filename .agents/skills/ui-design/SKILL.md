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
   - A capture that still shows a loading spinner or placeholder glyphs is a timing artefact, not a defect:
     raise `--wait` (for example `--wait=5000`) and capture again before judging anything.
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
   - `dotnet build osu.Desktop.slnf -c Debug -warnaserror`.

User-visible text changes must be synchronized via the `localisation-sync` skill (class and `.resx`
files together). Check `git diff` before restoring UI to ensure it was not removed intentionally.

## The offscreen previewer

### Running via helper scripts

Both scripts build the previewer when the executable is missing (or when you ask them to), then run it from
`tools/Previewer`, so a relative `--out` lands beside the executable — the git-ignored `captures/` folder.
Everything in the CLI reference below is reachable from both:

- PowerShell mirrors each argument as a parameter: `-Scene`, `-Test`, `-Case`, `-Crop`, `-Scripts`,
  `-StopBefore`, `-CaptureBefore`, `-Out`, `-Size`, `-Wait`, `-Locale`, `-Ruleset`, `-Seek`, `-Build`.
- bash takes the previewer's own flags and forwards the command line verbatim.
- Two defaults differ: PowerShell defaults `-Locale zh` and `-Seek 13000` and always passes them (pass
  `-Locale ""` to keep the system locale); bash passes only what it is given.
- `-Help` / `--help` prints the script's own usage without running anything.
- If PowerShell refuses to start the script at all (`因为在此系统上禁止运行脚本`), the execution policy is
  blocking it: run `Set-ExecutionPolicy -Scope Process Bypass` first, or invoke it as
  `powershell -ExecutionPolicy Bypass -File <script> ...`.
- `-Build` / `--build` forces a rebuild. Without it an existing executable is reused even after you edit
  previewer sources, which silently captures the old behaviour.
- Both exit with the previewer's exit code, so a run can be chained (`&&`): non-zero means the scene or
  script does not exist, a step failed, or the captures could not be written.

```bash
# PowerShell
powershell -File .agents/skills/ui-design/scripts/preview.ps1 -Scene SettingsPanel -Crop SettingsPanel
powershell -File .agents/skills/ui-design/scripts/preview.ps1 -Scene TestSceneImagePreviewOverlay -Test TestShowAndHide -CaptureBefore "hide overlay"
powershell -File .agents/skills/ui-design/scripts/preview.ps1 -Scripts "compose,timing"

# bash (Linux / Git Bash / macOS)
bash .agents/skills/ui-design/scripts/preview.sh --scene=SettingsPanel --crop=SettingsPanel
bash .agents/skills/ui-design/scripts/preview.sh --scene=TestSceneImagePreviewOverlay --test=TestShowAndHide --capture-before="hide overlay"
bash .agents/skills/ui-design/scripts/preview.sh --scripts=compose,timing
```

### Building and running directly

Build (outputs stay in `tools/Previewer/bin/`, `obj/` and `captures/`, which are git-ignored; the project
references the `g0v0.Framework` NuGet package, and pins its own `G0V0FrameworkVersion`):

```bash
dotnet build .agents/skills/ui-design/tools/Previewer -c Debug
```

Run from the previewer directory (`bin/Debug/net10.0/win-x64/` on Windows, `bin/Debug/net10.0/` elsewhere):

```bash
cd .agents/skills/ui-design/tools/Previewer
./bin/Debug/net10.0/win-x64/Previewer.exe --scene=TestSceneSettingsPanel --crop=SettingsPanel --out=captures/settings --locale=zh

# several states in one run: before each matching step, plus the end state
./bin/Debug/net10.0/win-x64/Previewer.exe --scene=TestSceneSettingsRenderers --test=TestStableSidebarExpandsOnHover "--capture-before=hover a tab|move away" --out=captures/sidebar
```

The previewer is muted (a zero volume adjustment on its audio), and each run uses its own temporary storage folder, so runs can follow
each other or overlap. The test browser's step column is shifted out of the frame and its "overriding local
input" banner is hidden, so a capture shows the scene the way the game itself would draw it.

### CLI argument reference

| Argument | Description | Default |
|---|---|---|
| `--scene=` | Any visual test scene in `osu.Game.Tests` / ruleset tests, or `editor`. The `TestScene` prefix is optional (`SettingsPanel` finds `TestSceneSettingsPanel`); an ambiguous name lists the full names | `editor` |
| `--test=` | Test method name; the `Test` prefix is optional (`ShowAndHide` finds `TestShowAndHide`) | All / default |
| `--case=` | Test case index when the method has several from `[TestCase]`, `[TestCaseSource]` or `[Values]` | `0` |
| `--crop=` | Comma-separated drawable type names; saves each present match (exact type or any base class, 6px margin) as `<name>-<Type>.png`, with `-2`, `-3` for further matches | None (full frame) |
| `--scripts=` | Comma-separated editor scripts to run sequentially when `--scene=editor` | `compose` |
| `--stop-before=` | Test scenes only: case-insensitive substring of step text; stops **instead of** running that step and captures there | None (runs to end) |
| `--capture-before=` | Test scenes only: `\|`-separated substrings of step text; captures before each matching step, then keeps running (files get a `-NN-<step-slug>` suffix; the end state is still captured). Editor scripts capture their own named files instead | None |
| `--out=` | Output directory for PNG captures, relative to the working directory — the helper scripts run from `tools/Previewer` | `captures` |
| `--size=` | Frame resolution (`WIDTHxHEIGHT`) | `1920x1080` |
| `--locale=` | Culture code (`zh`, `zh-Hant`, `en`); written to a clean `framework.ini` | System locale (PowerShell script passes `zh`) |
| `--ruleset=` | Ruleset short name (`osu`, `taiko`, `catch`, `mania`); editor scenes only | `osu` |
| `--seek=` | Editor only: where the editor's clock is placed before scripts run, in ms (`0` leaves it alone) | `13000` |
| `--wait=` | Settle delay in ms before capturing a test scene | `3000` |

A successful run ends with `Captures saved to <dir>`; a failure prints its reason and exits 1. The host log
in between is verbose — grep for those two lines rather than reading the whole thing.

### Where the previewer's code lives

| File | Responsibility |
|---|---|
| `Program.cs` | Parses the command line into `PreviewOptions`, prepares per-run storage, starts the host |
| `PreviewGame.cs` | `PreviewGame` (option caching, mute) and `PreviewRunner` (loads the scene, drives its steps, decides when to capture) |
| `TestScenes.cs` | Finds visual test scenes and replays their `[SetUp]`, `[SetUpSteps]` and test method steps |
| `FrameCapture.cs` | Grabs a frame from the offscreen host and saves it, whole or cropped to an area |
| `SceneSteps.cs` | Steps any scene can add: wait, find by type name, capture named PNGs |
| `SuppressTakeControlOverlay.cs` | Keeps the test browser's "overriding local input" banner out of captures |
| `EditorPreviewScene.cs` | Editor scripts only: which screen to show and which parts to capture |

Scenes describe *what* to show; steps and captures describe *how*. Adding a screen should never need a new capture
path — when it feels like it does, the missing piece belongs in `SceneSteps`.

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
| `context-menu` | Right-click object context menu, and the menu item under the cursor |
| `bar-popover`, `timestamp-input` | Timeline bar properties popover, timestamp entry |
| `timing` | Timing control point list and settings |
| `verify` | Verify issue list, severity filters, issue selection details |
| `setup` | Song setup screen, metadata section, difficulty section |
| `design` | Design (storyboard) screen as it opens |
| `compose-screen` | Helper step to switch mode back to Compose when chaining scripts |

A script only names what to show; adding steps, looking controls up by type name and saving frames is handled
generically by `SceneSteps` and `FrameCapture`, so a new script is a handful of lines in
`tools/Previewer/EditorPreviewScene.cs`. Whether a control exists is answered by type name at runtime, so a
script that names something the editor no longer has fails loudly instead of capturing a stale picture.

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
