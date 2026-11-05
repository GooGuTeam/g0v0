# Recording, streaming and replay export

## Controls

- **Start / stop video recording** and **Open streaming dialog / stop stream** are configurable general key-binding actions, unbound by default. Recording files are saved under `recordings/` in client storage.
- The streaming dialog masks the publishing address, supports RTMP/RTMPS and keeps credentials in memory.
- **Settings → Graphics → Recording and live streaming** provides capture controls and output settings.
- The **video button in the V2 results footer** exports a replay to `exports/`. Remote replays are downloaded first. Progress appears in notifications; close the progress notification to cancel, or click the completion notification to locate the file.

Window recording and streaming are mutually exclusive. Replay export runs independently on a private worker, renderer and audio graph. The final export file is published after encoding and muxing complete; managed failure/cancellation removes partial files.

## Settings

Values are captured when each task starts. Changing settings leaves running tasks unchanged.

| Setting | Values / default | Scope |
| --- | --- | --- |
| Video bitrate | 1–100 Mbps; default 12 | All output paths |
| Frame rate | 30 / 60 / 120 FPS; default 60 | All output paths |
| H.264 profile | Automatic / Baseline / Main / High | All output paths |
| Resolution | 720p / 1080p / 1440p / 2160p; default 1080p | All output paths, 16:9 |
| Replay backend | Automatic / Native / FFmpeg | Replay export |
| FFmpeg executable | `ffmpeg` from PATH or an executable path | FFmpeg replay export |

Replay export renders directly at the selected resolution. Native window capture scales on the GPU, preserving aspect ratio with black borders. Resizing the window stops live capture. Live frame availability may fall below the requested rate under load. Bitrate is a target, not an exact file-size guarantee.

Automatic backend selection uses native encoding on Windows and FFmpeg on Linux/macOS. Encoder failures are reported without silent fallback. Media Foundation selects the hardware encoder; vendor selection is not exposed. Unsupported format/profile combinations fail at startup.

## Native setup

Window recording, streaming and native replay encoding require Windows and a compatible Media Foundation hardware H.264 encoder. D3D11 capture is supported. Vulkan capture additionally requires local Veldrid interop, compatible same-adapter sharing and `VulkanCaptureInterop = true` before graphics initialisation.

Close the client, then build and deploy from the main repository:

```powershell
../g0v0-framework/native/build-recording.ps1 -Zig C:/tools/zig/zig.exe -VulkanHeaders C:/tools/Vulkan-Headers/include -DeployDirectory ./osu.Desktop/bin/Debug/net10.0
```

The script builds libraries only. `-DeployDirectory` copies both DLLs to an existing output directory and verifies their SHA-256 hashes. Restart the client afterwards. Omitting this parameter leaves client DLLs unchanged. An `EntryPointNotFoundException` for `*_open_configured` indicates an outdated GPU recording DLL.

Live WASAPI capture copies final device PCM through the native callback's bounded queue. No recording DSP is installed on its decode mixers, preserving the native playback callback. Deploy `g0v0-wasapi-capture.dll` beside the application, or select a trusted absolute path with `G0V0_WASAPI_CAPTURE_LIBRARY`. A missing bridge preserves playback but leaves WASAPI recording audio silent and logs a warning. Ordinary non-WASAPI backends retain the cross-platform BASS DSP taps.

Capture intent survives device reinitialisation. A dedicated capture worker converts each device generation to fixed 44.1 kHz stereo float PCM, reanchors its sample positions without reopening the encoder or publisher, and fills missing/overflowed intervals with silence. Conversion currently uses linear interpolation, duplicates mono and selects the front pair of multichannel output; it does not provide surround downmixing or high-quality band-limited resampling. Callback QPC timestamps represent mixing completion, not speaker presentation, so precise AV alignment remains unverified.

The capture worker drains up to 64 native blocks per iteration and waits up to 2 ms before the next drain. Its fixed two-second PCM ring uses absolute sample-position tags, so overwritten data becomes silence rather than stale audio. The encoder allows 100 ms for audio arrival; this buffering affects encoding only, never playback. Reader scheduling and GC pauses can still cause native-ring overflow; the worker does not provide a real-time guarantee. No extra playback mixer is created. Capture includes game output, not microphone or other applications. Offline export retains its private 48 kHz mixer, which is not registered with the live audio thread and does not restart when the playback device changes.

Audio capture has no Windows-specific API dependency. Full live recording/streaming remains Windows-only because the video encoder and capture path still require native Windows support. Linux/macOS audio behaviour, restart continuity, effects/volume parity and long-run AV drift have build/static validation only.

## FFmpeg setup

Install FFmpeg with libx264 and AAC support, then select its executable in settings. FFmpeg is not bundled or downloaded. Private rendering uses D3D11 on Windows, Vulkan on Linux and Metal on macOS; a working graphics driver is required.

The exporter sends RGBA video through standard input, spools 48 kHz stereo float PCM to disk, and muxes AAC after video encoding. This avoids two-input pipe deadlocks. Allow space for the temporary video, PCM (about 23 MB/minute) and final MP4. GPU readback and software encoding can be slower than native encoding.

FFmpeg runs without a shell and receives no publishing URLs or credentials. Cancellation terminates owned processes and removes temporary files. Process crashes can leave temporary files. FFmpeg currently supports replay export only; window recording and streaming still require Windows.

## Replay behaviour and limitations

- Uses production replay input, ruleset/score/health processing, private skins, HUD, background and storyboard.
- General settings, ruleset settings and the selected skin are snapshotted. Custom ruleset configurations require `IRulesetConfigSnapshotSource`.
- Audio/simulation advance from output sample positions. Constant-rate mods are supported; variable-rate mods are rejected.
- Cursor trails use output time independently of render speed and mod rate. Skin lookup and continuous/disjoint trail rules are preserved.
- The scene is laid out like the game's: the 1024x768 draw-size-preserving root with the user's UI scale, the beatmap background with user dim, blur and break lightening, the dimmed storyboard with its overlay layer above the playfield, break overlay, letterbox, kiai fountains and combo-break effects.
- Skin fallbacks follow `SkinManager`: the classic skin only behind legacy user skins, then triangles. Fonts and resource stores match `OsuGameBase`, including the CJK fallback fonts.
- Player-specific mod callbacks (`IApplicableToPlayer`), the fail animation, sample playback disabling and the rest of the normal player lifecycle are not reproduced. Mods relying on them (No Scope, Bloom, Mania Hidden, Cinema) can look different in exports.
- Difficulty for the PP and beatmap attribute displays is precomputed synchronously while the scene is built, without the export's cancellation token.
- Beat-synchronised amplitudes are empty. Online avatars use local placeholders; online team logos and spectators are omitted.
- Full replay fidelity, storyboard video timing, audio balance, long-run AV drift, repeated-export cleanup and Linux/macOS behaviour require runtime acceptance.
- Simultaneous recording/streaming fanout and platform account integration are not implemented.

## Export scene dependencies

`OfflineGameplayScene` and `OfflineReplayExportSession` provide what `Player` and the game provide in gameplay, without sharing live objects across threads:

| Dependency | Normal gameplay | Offline export |
| --- | --- | --- |
| `IRenderer`, texture/font/shader stores | Host/game resources | Private renderer and stores from the export session; includes `OsuIconStore` and FontAwesome |
| `AudioManager`, `ISampleStore` | Host audio graph | Private pull-driven audio manager and sample store |
| `IStorageResourceProvider` | Game/skin manager | Private renderer/audio with borrowed asset byte stores |
| `RealmAccess` | Game database service | Shared access service; per-thread Realm access is used, not a live managed Realm object |
| `OsuConfigManager`, `IGameplaySettings` | Live configuration | Detached configuration snapshot |
| `SessionStatics` | Client session | Export-owned instance |
| `IRulesetConfigCache` | Persistent user settings | Export-owned configurations restored from detached user-setting snapshots |
| `IBindable<WorkingBeatmap>` | Screen/game dependencies | Export session bindable; the working beatmap itself is shared |
| `IBindable<RulesetInfo>` | Screen/game dependencies | Local selected replay ruleset bindable |
| `IBindable<IReadOnlyList<Mod>>` | Screen/game dependencies | Bindable containing the export's cloned mods |
| `GameplayState`, `DrawableRuleset`, `ScoreProcessor`, `HealthProcessor` | Player scope | Private scene scope |
| `IScrollingInfo` | Player, for scrolling rulesets | Private scene, for scrolling rulesets |
| `IBeatmap`, `IReadOnlyList<Mod>` | Drawable ruleset child scope | Provided by the same production drawable ruleset; storyboard has its own mod cache |
| `IGameplayClock`, `GameplayClockContainer` | Gameplay clock container | Deterministic clock container; no wall-clock interpolation |
| `IFrameStableClock` for HUD | Player publishes the loaded ruleset clock | HUD-only child scope populated after the ruleset loads |
| `ReplayExportPresentationClock` | Absent; cursor trails retain their own real-time clock | Private output-time clock for trails, driven by audio sample positions independently of mod rate |
| `IBeatSyncProvider` | Master gameplay clock | Offline scene with beatmap control points and export clock; amplitude values remain empty |
| `HUDOverlay` and HUD controllers | Cached by HUD/Player | HUD caches itself and its controllers for its own descendants |
| `ISkinSource` | Skin hierarchy | Worker-owned skin hierarchy, including selected/classic/beatmap skins |
| Beatmap skins/colours/hitsounds bindings | Player binds game settings | Bound to the detached export config |
| `ISafeArea` | Window safe area | Explicit private safe area |
| `IGameplayLeaderboardProvider` | Solo/multiplayer provider | Private solo provider containing the replay score |
| Timed difficulty / star difficulty | `BeatmapDifficultyCache` | Precomputed `ReplayExportDifficulty` shared by PP and beatmap-attribute displays |
| Storyboard / trigger controller | Storyboard child scope | Production storyboard container supplies both locally |

The HUD clock registration must not be placed above the ruleset: `FrameStabilityContainer` resolves a parent `IGameplayClock`, so publishing its own clock there would introduce self-reference. The export loads the ruleset first and adds a separate HUD dependency scope afterwards.

`GameHost`, `OsuGame`, `Player`, `IAPIProvider`, spectator/multiplayer clients, online asset caching, user lookup, music controller and screen-navigation services are not injected into the export scene. Reusing the live instances would cross thread/resource ownership boundaries. Host-dependent visual clocks have explicit windowless handling. Offline identity visuals use replay data/local assets; online-only team logos and spectator displays are omitted.

## Validation and release

Joint managed builds, native library builds and lightweight static checks passed. Tests, probes and external streaming connections were not run. A successful full export after the latest fixes has not been observed; historical recordings do not validate this integration.

The client references `g0v0.Framework` 2026.1028.1, which carries these APIs, so it builds from packages (see [building.md](building.md)). The new strings still need a `g0v0.osu.Game.Resources` release for their translations. See the framework's [video and capture design](../../g0v0-framework/docs/video-and-capture.md).
