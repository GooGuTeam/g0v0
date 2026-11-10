# Recording, streaming and replay export

## Controls

- **Start / stop video recording** and **Open streaming dialog / stop stream** are configurable general key-binding actions, unbound by default. Recording files are saved under `recordings/` in client storage.
- The streaming dialog masks the publishing address, supports RTMP/RTMPS and keeps credentials in memory.
- **Settings → Graphics → Recording and live streaming** provides capture controls and output settings.
- The **video button in the V2 results footer** exports a replay to `exports/`. Remote replays are downloaded first. Progress appears in notifications; close the progress notification to cancel, or click the completion notification to locate the file.
- **Autoplay videos**: on the results screen of an Autoplay play the same button generates the video again from the Autoplay mod, since Autoplay keeps no replay. **Create autoplay video** in a beatmap's song-select context menu does the same for the selected ruleset and mods; the ruleset's Autoplay mod is added (replacing incompatible mods) unless a mod that creates replay data, such as Cinema, is already selected.

Window recording and streaming are mutually exclusive. Replay export runs independently on a private worker, renderer and audio graph. The final export file is published after encoding and muxing complete; managed failure/cancellation removes partial files.

## Code layout

Everything lives in `osu.Game/Recording` and is reached from a few hooks (`OsuGame`, `OsuConfigManager`, `GlobalActionContainer`, `GraphicsSection`, `V2ResultsFooter`, `SoloSongSelect`):

| Path | Contents |
| --- | --- |
| `Recording/` | Settings enums, `RecordingConfiguration` snapshot, `RecordingBackend.Resolve()` |
| `Recording/Live/` | `LiveCaptureManager` (cached by `OsuGame`), `ILiveCapture` with `MediaFoundationLiveCapture` and `FfmpegLiveCapture`, streaming dialog |
| `Recording/Export/` | `ReplayVideoExporter` (cached by `OsuGame`), `ReplayExportRequest` (`FromScore`, `FromAutoplay`), the export session, scene, player and their private services |

## Settings

Values are captured when each task starts. Changing settings leaves running tasks unchanged.

| Setting | Values / default | Scope |
| --- | --- | --- |
| Video bitrate | 1–100 Mbps; default 12 | All output paths |
| Frame rate | 30 / 60 / 120 FPS; default 60 | All output paths |
| H.264 profile | Automatic / Baseline / Main / High | All output paths |
| Resolution | 720p / 1080p / 1440p / 2160p; default 1080p | All output paths, 16:9 |
| Encoding backend | Automatic / Media Foundation / FFmpeg | All output paths |
| FFmpeg executable | `ffmpeg` from PATH or an executable path | FFmpeg backend |

Replay export renders directly at the selected resolution. Window capture preserves aspect ratio with black borders (scaled on the GPU by Media Foundation, by FFmpeg's `scale`/`pad` filters otherwise). Resizing the window stops live capture. Live frame availability may fall below the requested rate under load. Bitrate is a target, not an exact file-size guarantee.

| Path | Windows (Automatic) | Linux / macOS (Automatic) |
| --- | --- | --- |
| Window recording | Media Foundation (`DesktopVideoRecording`) | FFmpeg (`FfmpegLiveCapture`) |
| Live streaming | Media Foundation + `RtmpVideoPublisher` | FFmpeg publishing FLV over RTMP/RTMPS |
| Replay export | Media Foundation (`OfflineVideoRecording`) | FFmpeg (`FfmpegOfflineVideoRecording`) |

Selecting FFmpeg uses it on Windows too; selecting Media Foundation elsewhere fails at startup. Encoder failures are reported without silent fallback. Media Foundation selects the hardware encoder; vendor selection is not exposed. Unsupported format/profile combinations fail at startup.

## Media Foundation setup (Windows)

The Media Foundation backend requires a compatible hardware H.264 encoder. D3D11 capture is supported. Vulkan capture additionally requires local Veldrid interop, compatible same-adapter sharing and `VulkanCaptureInterop = true` before graphics initialisation.

Close the client, then build and deploy from the main repository:

```powershell
../g0v0-framework/native/build-recording.ps1 -Zig C:/tools/zig/zig.exe -VulkanHeaders C:/tools/Vulkan-Headers/include -DeployDirectory ./osu.Desktop/bin/Debug/net10.0
```

The script builds libraries only. `-DeployDirectory` copies both DLLs to an existing output directory and verifies their SHA-256 hashes. Restart the client afterwards. Omitting this parameter leaves client DLLs unchanged. An `EntryPointNotFoundException` for `*_open_configured` indicates an outdated GPU recording DLL.

Live WASAPI capture copies final device PCM through the native callback's bounded queue. No recording DSP is installed on its decode mixers, preserving the native playback callback. Deploy `g0v0-wasapi-capture.dll` beside the application, or select a trusted absolute path with `G0V0_WASAPI_CAPTURE_LIBRARY`. A missing bridge preserves playback but leaves WASAPI recording audio silent and logs a warning. Ordinary non-WASAPI backends retain the cross-platform BASS DSP taps.

Capture intent survives device reinitialisation. A dedicated capture worker converts each device generation to fixed 44.1 kHz stereo float PCM, reanchors its sample positions without reopening the encoder or publisher, and fills missing/overflowed intervals with silence. Conversion currently uses linear interpolation, duplicates mono and selects the front pair of multichannel output; it does not provide surround downmixing or high-quality band-limited resampling. Callback QPC timestamps represent mixing completion, not speaker presentation, so precise AV alignment remains unverified.

The capture worker drains up to 64 native blocks per iteration and waits up to 2 ms before the next drain. Its fixed two-second PCM ring uses absolute sample-position tags, so overwritten data becomes silence rather than stale audio. The encoder allows 100 ms for audio arrival; this buffering affects encoding only, never playback. Reader scheduling and GC pauses can still cause native-ring overflow; the worker does not provide a real-time guarantee. No extra playback mixer is created. Capture includes game output, not microphone or other applications. Offline export retains its private 48 kHz mixer, which is not registered with the live audio thread and does not restart when the playback device changes.

Audio capture has no Windows-specific API dependency and is shared by both live backends. Linux/macOS audio behaviour, restart continuity, effects/volume parity and long-run AV drift have build/static validation only.

## FFmpeg setup

Install FFmpeg with libx264 and AAC support, then select its executable in settings. FFmpeg is not bundled or downloaded. FFmpeg runs without a shell. Cancellation terminates owned processes.

**Replay export.** Private rendering uses D3D11 on Windows, Vulkan on Linux and Metal on macOS; a working graphics driver is required. The exporter sends RGBA video through standard input, spools 48 kHz stereo float PCM to disk, and muxes AAC after video encoding, which avoids two-input pipe deadlocks. Allow space for the temporary video, PCM (about 23 MB/minute) and final MP4. GPU readback and software encoding can be slower than native encoding. Process crashes can leave temporary files.

**Window recording and streaming** (`FfmpegLiveCapture`). Frames come from the framework's GPU capture on the draw thread (D3D11 or Vulkan renderers; Metal and OpenGL are not supported) and are read back through three staging textures without waiting for the GPU. A pacing thread writes the newest frame to FFmpeg's standard input at the configured rate, repeating it when the game draws slower and skipping frames after falling a second behind, so video stays on the wall clock. Game audio (44.1 kHz stereo float, the same capture as Media Foundation) reaches FFmpeg through a loopback TCP connection, its second input; only the first connection is accepted. Recording writes fragmented MP4 (playable if interrupted); streaming publishes FLV to the RTMP/RTMPS address. libx264 runs with `veryfast`/`zerolatency` and a bitrate cap. CPU readback and software encoding cost more than Media Foundation.

The streaming address, which contains the stream key, is passed to FFmpeg as a command-line argument, so other programs on the same computer can read it while the stream runs. The streaming dialog says so when FFmpeg will publish. FFmpeg error output is reported with the address removed.

## Replay behaviour and limitations

- Uses production replay input, ruleset/score/health processing, private skins, HUD, background and storyboard.
- Autoplay exports generate the replay with the mod's `CreateScoreFromReplayData` on the playable beatmap, as the game's Autoplay does, and keep the Autoplay mod in the score, so the HUD and player name show Autoplay.
- General settings, ruleset settings and the selected skin are snapshotted. Custom ruleset configurations require `IRulesetConfigSnapshotSource`.
- Audio/simulation advance from output sample positions. Constant-rate mods are supported; variable-rate mods are rejected.
- Cursor trails use output time independently of render speed and mod rate. Skin lookup and continuous/disjoint trail rules are preserved.
- Gameplay runs in `ExportReplayPlayer`, a `ReplayPlayer` in a private screen stack, so mods, HUD, background, storyboard, breaks and fail handling follow the normal player lifecycle, including `IApplicableToPlayer` (No Scope, Bloom, Mania Hidden, Cinema, Relax) and the fail animation. The player differs only in its deterministic clock driven by the export, private skins, a background loaded before the first frame, no entry transition and hidden replay controls.
- The beatmap is presented unmodded, as in the game (`ExportWorkingBeatmap`), so the player applies its own mod instances and components that apply mods themselves do not apply them twice. Only its track, skin and background are replaced by the export's private ones.
- The scene is laid out like the game's: the 1024x768 draw-size-preserving root with the user's UI scale, then the screen stack with its background stack and parallax.
- Skin fallbacks follow `SkinManager`: the classic skin only behind legacy user skins, then triangles. Fonts and resource stores match `OsuGameBase`, including the CJK fallback fonts.
- Difficulty for the PP and beatmap attribute displays is precomputed synchronously while the scene is built, without the export's cancellation token.
- Beat-synchronised amplitudes are empty. Avatars load like in the game but only from local textures, so they show the guest avatar and may appear a few frames late; online team logos and spectators are omitted.
- Full replay fidelity, storyboard video timing, audio balance, long-run AV drift, repeated-export cleanup and Linux/macOS behaviour require runtime acceptance.
- Simultaneous recording/streaming fanout and platform account integration are not implemented.

## Export scene dependencies

`OfflineReplayExportSession` and `OfflineGameplayScene` provide what the game provides to a player, without sharing live objects across threads. Everything in the player's own scope (`GameplayState`, `DrawableRuleset`, score and health processors, `IGameplayClock`, `HUDOverlay`, leaderboard provider) is created by the player as in the game.

| Dependency | Normal gameplay | Offline export |
| --- | --- | --- |
| `GameHost` | The running host | `OfflineSceneHost` from `OfflineRenderContext.CreateSceneHost`: never runs, exposes the private renderer, the game storage and default texture loaders, and has no window, input handlers or update thread (`ApplyGameWideClock` then keeps the scene clock) |
| `IRenderer`, texture/font/shader stores | Host/game resources | Private renderer and stores from the export session; includes `OsuIconStore` and FontAwesome |
| `AudioManager`, `ISampleStore` | Host audio graph | Private pull-driven audio manager and sample store |
| Beatmap track, skin and background | Loaded through the game's audio and renderer | Private track, beatmap skin and background texture served by `ExportWorkingBeatmap` |
| `IStorageResourceProvider` | Game/skin manager | Private renderer/audio with borrowed asset byte stores |
| `RealmAccess` | Game database service | Shared access service; the worker marks itself with `RealmAccess.IsPrivateSceneThread`, so it always gets its own Realm instance even in single-threaded execution, where it is otherwise indistinguishable from the update thread |
| `OsuConfigManager`, `IGameplaySettings` | Live configuration | Detached configuration snapshot |
| `SessionStatics` | Client session | Export-owned instance |
| `IRulesetConfigCache` | Persistent user settings | Export-owned configurations restored from detached user-setting snapshots |
| `Bindable<WorkingBeatmap>`, `Bindable<RulesetInfo>`, `Bindable<IReadOnlyList<Mod>>` | Game bindables leased by screens | Scene-owned bindables holding the export beatmap, replay ruleset and replay mods |
| `IGameplayClock`, `GameplayClockContainer` | `MasterGameplayClockContainer` on the beatmap track | `GameplayClockContainer` on the export clock, without wall-clock interpolation |
| `ReplayExportPresentationClock` | Absent; cursor trails retain their own real-time clock | Private output-time clock for trails, driven by audio sample positions independently of mod rate |
| `IBeatSyncProvider` | `OsuGameBase` (global beatmap clock) | Offline scene with beatmap control points and export clock; amplitude values remain empty |
| `GlobalActionContainer` | Game | Scene-owned container without a handler (export has no input) |
| `Framework.Game` | The game | Unloaded placeholder; osu-framework's `Screen` requires one, and none of the screens used here access it |
| `ISkinSource` | Skin hierarchy | Worker-owned skin hierarchy, including selected/classic/beatmap skins |
| `ISafeArea` | Window safe area | Explicit private safe area |
| Timed difficulty / star difficulty | `BeatmapDifficultyCache` | `ReplayExportDifficultyCache`, a subclass serving values precomputed synchronously before export; its bindables are filled after the first scene update, before the first frame |
| `IAPIProvider` | Online API | Offline `DummyAPIAccess`; identity displays use the score's user, so it is never asked for anything |
| `UserLookupCache` | Online user lookup | `OfflineUserLookupCache`, which finds nothing (team flags show only what the score records) |

`OsuGameBase`, `MusicController`, `ScoreManager`, `SkinManager`, spectator/multiplayer clients and online asset caching are not injected into the export scene; reusing the live instances would cross thread/resource ownership boundaries, and they have no cheap offline stand-in. `OsuScreen`, `Player`, `SaveFailedScoreButton`, `SpectatorList` and the skin providers treat these as optional for that reason; in the game they are always present. Host, API, user-lookup and difficulty consumers (mania columns, playfield border, scaling, screen suspension, storyboard, ruleset resources, player avatar/name/flags, leaderboard scores, PP counter, beatmap attribute text) need no export-specific code: the stand-ins above serve them.

## Validation and release

On Windows with D3D11, the explicit `TestSceneOfflineReplayExport` passes in both `SingleThread` and `MultiThreaded` execution: four seconds through `ExportReplayPlayer` with Relax, Bloom and No Scope, an Autoplay export with Hidden, and an Autoplay export encoded to an MP4 with Media Foundation. `ReplayExportRequestTest` covers Autoplay replay generation and mod handling. The FFmpeg backends (export and live), live recording/streaming on any backend, external streaming endpoints and Linux/macOS have not been run.

`OfflineSceneHost` and `OfflineRenderContext.CreateSceneHost` are new framework APIs. Until a `g0v0.Framework` package carrying them is published and referenced, the client builds only against a local framework checkout. The new strings need a `g0v0.osu.Game.Resources` release for their translations. See the framework's [video and capture design](../../g0v0-framework/docs/video-and-capture.md).
