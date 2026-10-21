---
name: localisation-sync
description: Keep the localisation of a g0v0 fork change in step with the separate resources repository. Use whenever a change adds, renames or removes user-visible text - a label, tooltip, settings entry, notification or error message - and also when checking whether a *Strings.cs key has its English and Chinese entries in g0v0-resources. Covers finding the resources clone, regenerating the base .resx, writing the zh / zh-Hant translations, and a read-only coverage check.
---

# Localisation sync (g0v0 fork)

The fork keeps its localisation in two repositories, and they are edited together:

| | repository | holds |
|---|---|---|
| `*Strings.cs` classes | `GooGuTeam/g0v0`, under `osu.Game/Localisation/` | the key, the English source text, and the runtime fallback |
| `.resx` resources | `GooGuTeam/g0v0-resources`, under `osu.Game.Resources/Localisation/` | the English entry the translation pipeline reads, and the translations |

A string changed in only one of them is half done. Only in the class, and the user keeps seeing the
old translation or falls back to English. Only in the resources, and the key becomes an orphan that
nothing asks for. **Both, in the same piece of work, before committing.**

## The resources repository is a separate clone

It is not a submodule and not inside this repository. `osu.Game.Resources.Custom/` is a leftover
build-output directory, not it. Find the clone before doing anything:

```bash
ls -d ../g0v0-resources ../../g0v0-resources ../osu-resources ./osu.Game.Resources 2>/dev/null
```

**If none of them exists, stop and ask the user.** Do not clone into this repository, and do not
reconstruct a resx path from memory. Hand them the command instead:

```bash
git clone https://github.com/GooGuTeam/g0v0-resources.git ../g0v0-resources
```

Everything below is read-only until step 6. Never push, tag or release without being asked.

## Procedure

1. **Find the keys the change touches.** Every user-visible string goes through
   `getKey(@"snake_case_key")`. `scripts/check-strings.sh` extracts them from a diff and does the
   whole coverage report for you — start there. Keys must be snake_case (CI rule L10N007).

2. **Edit the class.** All `*Strings.cs` live under `osu.Game/Localisation/`, ruleset ones in the
   `Catch` / `HUD` / `Mania` / `Osu` / `SkinComponents` / `Taiko` subdirectories. The class's
   `private const string prefix` is the **only** authoritative namespace, and its last segment names
   the resource file: `osu.Game.Resources.Localisation.Catch.Action` → `Localisation/Catch/Action.resx`.
   Do not infer the file from the class's own path.

3. **Regenerate the base resx.** The base file (the one whose **name contains exactly one dot**:
   `Common.resx` yes, `Common.zh.resx` no) is generated, so do not hand-copy it:

   ```bash
   dotnet tool restore
   DOTNET_ROLL_FORWARD=LatestMajor dotnet localisation to-resx osu.Game/osu.Game.csproj --output /tmp/resx
   ```

   `DOTNET_ROLL_FORWARD` is needed because the tool targets net8 and developer machines here have
   only the net10 runtime.

   Write to a temporary directory and copy across what changed. Never point `--output` straight at
   the resources clone: it rewrites all 167 base files at once, and **the tool does not finish on
   this tree** — it throws `NullReferenceException` on `ChatStrings.cs`, which is the first file
   containing a C# raw string literal (`"""`), leaving the files it already wrote behind. For
   `ChatStrings.cs` and `GeneralSettingsStrings.cs`, add the one `<data>` element by hand in the
   base file, matching the surrounding format.

4. **Add the translations.** In `<X>.zh.resx` and `<X>.zh-Hant.resx`, add the same
   `<data name="...">` with the translated text. A new key must reach at least base and `zh`; the
   fork's convention is base:zh = 167:167 complete (`zh-Hant` trails at 161:167). The other ~40
   upstream locales are maintained by Crowdin on the `l10n_master` branch — **never hand-write
   those** — but `zh` and `zh-Hant` are the fork's own and are written by hand here.

   - resx files are **UTF-8 with BOM**. Keep the BOM, keep `xml:space="preserve"` on `<data>`, and
     keep entries in the same order as the base file.
   - Leave line endings alone. Neither repository has a `.gitattributes`, and machines here run with
     `core.autocrlf=true`, so the working tree shows CRLF while git stores LF. Do not "fix" it and do
     not add a `.gitattributes` for it.

5. **Check coverage.** Re-run the script; it reports every key against base, `zh` and `zh-Hant`:

   ```bash
   bash .agents/skills/localisation-sync/scripts/check-strings.sh --base <ref>
   ```

   The cross-checker behind CI can also be run locally, and is what the annotations come from:

   ```bash
   dotnet run --project ../g0v0-l10n-checker/src/g0v0.LocalisationChecker -- \
     --osu . --resources ../g0v0-resources --format human
   ```

6. **Release, only if asked.** Resource changes are committed, tagged `v<version>-g0v0` and
   published as a new `g0v0.osu.Game.Resources` package from that repository; the client then bumps
   `G0V0ResourcesVersion` in `osu.Game/osu.Game.csproj`. Both directions are human decisions.

## Report shape

```markdown
## 本地化同步（base: <ref> → working tree）

| 文件 | key | base | zh | zh-Hant |
|---|---|---|---|---|
| osu.Game/Localisation/<X>Strings.cs | <key> | yes | no | no |

### 需要补的条目
- `g0v0-resources`: `osu.Game.Resources/Localisation/<X>.resx` — `<key>`
- `g0v0-resources`: `osu.Game.Resources/Localisation/<X>.zh.resx` — `<key>`（译文：…）

### 被删除的 key（对应 resx 条目会变成孤儿 L10N002）
- <key>

需要我把中文译文也补上吗？（是 / 只补英文 / 先不动）
```

Report counts, not impressions: how many keys were found, how many are missing where, and say
explicitly when a check found nothing. "0 missing" is a valid, useful result.

## Pitfalls

- If the resources clone is missing, **say so instead of working around it**. A hand-written resx in
  the wrong repository looks like it works and produces an orphan entry that CI will flag.
- The class file is named after the resource file only by convention. A prefix such as
  `osu.Game.Resources.Localisation.BeatmapOverlayStrings` yields `BeatmapOverlayStrings.resx`, but the
  fork has prefixes that end in `Strings` and others that do not — always read the prefix.
- Do not treat the base resx as hand-written. Editing it directly is fine for a single entry when the
  generator cannot run, but any bulk edit should come from `to-resx` or it will drift.
- Chinese is not a machine step here. Adding a key without a `zh` entry leaves the fork's own primary
  audience on English text; the pipeline will not fill it in.
- The `.resx` English text is what the pipeline reads, not the C# fallback. The two can legitimately
  differ after rebranding (`g0v0!` vs `osu!`), so do not "align" them.
- The CI job runs on both repositories (`.github/workflows/localisation.yml`, implemented in
  `GooGuTeam/g0v0-l10n-checker`) and suppresses the known historical differences listed in
  `.localisation-baseline.json`. New findings are what fail a build.
