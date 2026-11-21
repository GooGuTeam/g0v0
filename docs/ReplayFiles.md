# Replay files

The game owns replay persistence. Rulesets own their input frames.

`ScoreReplayFormat` is the shared file-codec registry used by the player, score importer,
database replay loader and score exporter. Built-in codecs are `.osr` for legacy-compatible
rulesets and `.g0r` for native frames. `Register()` can add another application-level codec
without changing these consumers. Extensions are matched case-insensitively.

When no registered codec can encode a ruleset, local score statistics are still saved,
but no replay is stored or available for playback or export. Non-legacy rulesets must
provide `CreateReplayFormat()` to persist their replay frames.

A ruleset opts into native replay storage by implementing:

```csharp
public override RulesetReplayFormat CreateReplayFormat()
    => new RulesetReplayFormat<MyReplayFrame>();
```

`MyReplayFrame` derives from `ReplayFrame` and holds the ruleset's inputs. Recording and
playback remain ruleset responsibilities, through `CreateReplayRecorder` and
`CreateReplayInputHandler` on the drawable ruleset. The standard generic frame codec
preserves these fields, including multiple contacts or other custom inputs. It rejects
non-finite frame times and retains equal-time frames in their original order.

The `.g0r` container is UTF-8 JSON with an explicit `g0v0ReplayFormat` version. It stores
the ruleset short name, beatmap MD5 and content hash, scoring version, score/user metadata,
Mod configurations, judgement statistics, health graph, pauses, frame-format version and frames.
The installed ruleset supplies the frame codec; the file cannot name arbitrary CLR types.
Both container and frame versions are validated before playback. Beatmap hashes must match
the installed content. Unknown rulesets or versions fail import instead of losing inputs.

`ModelExporter<T>` selects its extension per model. `ScoreExporter` retains the stored codec's
extension and copies the saved replay bytes unchanged. `LegacyExporter`, `LegacyScoreExporter`
and `LegacyDatabasedScore` remain compatibility entry points for existing callers.

`.g0r` is a local replay format. Online spectator protocols and server-side replay storage
still use their existing codecs and require a separate protocol agreement to support native frames.
