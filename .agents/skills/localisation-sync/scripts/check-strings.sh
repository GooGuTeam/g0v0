#!/usr/bin/env bash
#
# Localisation coverage check for the g0v0 fork (see ../SKILL.md).
#
# READ-ONLY. Never writes to either repository, never stages anything. It lists the localisation
# keys a diff adds or changes and reports, per key, whether the resource files carry it.
#
# Usage:
#   check-strings.sh                        diff the working tree against HEAD
#   check-strings.sh --base <ref>           diff <ref> against the working tree (use before a commit)
#   check-strings.sh --resources <dir>      where the g0v0-resources clone is
#   G0V0_RESOURCES_DIR=<path> check-strings.sh
#
# Exit codes: 0 base and zh are complete | 1 a key is missing | 2 usage or setup problem
#
set -uo pipefail

BASE=""
RESOURCES_DIR="${G0V0_RESOURCES_DIR:-}"

while [ $# -gt 0 ]; do
  case "$1" in
    --base) BASE="${2:-}"; shift 2 ;;
    --resources) RESOURCES_DIR="${2:-}"; shift 2 ;;
    -h|--help) sed -n '2,14p' "$0"; exit 0 ;;
    *) echo "unknown argument: $1" >&2; exit 2 ;;
  esac
done

ROOT="$(git rev-parse --show-toplevel 2>/dev/null)" || { echo "not inside a git repository" >&2; exit 2; }
cd "$ROOT" || exit 2

# The resources repository is a separate clone; it is never inside this one. Look where the fork's
# documentation puts it, and say what to do rather than guess if none of them is present.
if [ -z "$RESOURCES_DIR" ]; then
  for candidate in ../g0v0-resources ../../g0v0-resources ../osu-resources osu.Game.Resources; do
    if [ -d "$candidate/osu.Game.Resources/Localisation" ]; then
      RESOURCES_DIR="$candidate"
      break
    fi
  done
fi

if [ -z "$RESOURCES_DIR" ] || [ ! -d "$RESOURCES_DIR/osu.Game.Resources/Localisation" ]; then
  {
    echo "resources clone not found."
    echo "Ask the user to clone it, then re-run:"
    echo "  git clone https://github.com/GooGuTeam/g0v0-resources.git ../g0v0-resources"
    echo "or point at an existing clone with --resources <dir> / G0V0_RESOURCES_DIR."
  } >&2
  exit 2
fi

RESX_ROOT="$RESOURCES_DIR/osu.Game.Resources/Localisation"
PREFIX_ROOT="osu.Game.Resources.Localisation."

# Literal attribute match, so a key that merely starts with another key's name is not counted.
has_key() {
  [ -f "$1" ] && grep -qF "name=\"$2\"" "$1"
}

# A class's `private const string prefix` is the only authoritative namespace, and its last segment
# names the resource file. A key's home therefore comes from the class, not from its folder.
prefix_of() {
  if [ -f "$1" ]; then
    sed -n 's/^[[:space:]]*private const string prefix = @"\(.*\)";.*$/\1/p' "$1" | head -n 1
  elif [ -n "$BASE" ]; then
    git show "$BASE:$1" 2>/dev/null | sed -n 's/^[[:space:]]*private const string prefix = @"\(.*\)";.*$/\1/p' | head -n 1
  fi
}

# Keys only ever appear as a getKey(...) argument, so the diff is exactly the change surface. Both
# the verbatim (`@"key"`) and plain (`"key"`) forms are used in the tree.
extract_keys() {
  local sign="$1"
  git diff --unified=0 --no-color ${BASE:+"$BASE"} -- osu.Game/Localisation \
    | awk -v sign="$sign" '
      /^\+\+\+ / { file = $2; sub(/^b\//, "", file); next }
      /^--- /    { next }
      substr($0, 1, 1) == sign {
        line = substr($0, 2)
        while (match(line, /getKey\(@?"[^"]*"\)/)) {
          key = substr(line, RSTART, RLENGTH)
          sub(/^getKey\(@?"/, "", key)
          sub(/"\)$/, "", key)
          if (file ~ /^osu\.Game\/Localisation\//) print file "\t" key
          line = substr(line, RSTART + RLENGTH)
        }
      }' \
    | sort -u
}

ADDED="$(extract_keys '+')"
# A key that reappears in a rewritten line is not a removal; `comm` drops those from the second list.
REMOVED="$(comm -13 <(printf '%s\n' "$ADDED" | sort -u) <(extract_keys '-' | sort -u))"

count_lines() { printf '%s\n' "$1" | grep -c . || true; }

printf 'repo:      %s @ %s\n' "$ROOT" "$(git rev-parse --short HEAD)"
printf 'resources: %s\n' "$RESX_ROOT"
printf 'range:     %s..working tree\n\n' "${BASE:-(HEAD)}"

added_total=$(count_lines "$ADDED")
missing_base=0
missing_zh=0
missing_hant=0

echo "===== keys added or changed by this diff ====="
if [ "$added_total" -eq 0 ]; then
  echo "(none)"
else
  printf '%-56s %-40s %s\n' file key 'base/zh/zh-Hant'
  while IFS=$'\t' read -r file key; do
    [ -n "$file" ] || continue

    prefix="$(prefix_of "$file")"
    if [ -z "$prefix" ]; then
      printf '%-56s %-40s %s\n' "$file" "$key" '?? no "prefix" constant found - check this class by hand'
      continue
    fi

    rel="${prefix#"$PREFIX_ROOT"}"
    rel="${rel//./\/}"

    if has_key "$RESX_ROOT/$rel.resx" "$key"; then b=yes; else b=no; missing_base=$((missing_base + 1)); fi
    if has_key "$RESX_ROOT/$rel.zh.resx" "$key"; then z=yes; else z=no; missing_zh=$((missing_zh + 1)); fi
    if has_key "$RESX_ROOT/$rel.zh-Hant.resx" "$key"; then h=yes; else h=no; missing_hant=$((missing_hant + 1)); fi

    printf '%-56s %-40s %s/%s/%s\n' "$file" "$key" "$b" "$z" "$h"
  done <<<"$ADDED"
fi

echo
echo "===== keys this diff removes ====="
echo "(their entries in the resource files become orphans - L10N002 - if they are left behind)"
if [ "$(count_lines "$REMOVED")" -eq 0 ]; then
  echo "(none)"
else
  while IFS=$'\t' read -r file key; do
    [ -n "$file" ] || continue
    printf '%-56s %s\n' "$file" "$key"
  done <<<"$REMOVED"
fi

echo
echo "===== summary ====="
echo "keys added/changed  : $added_total"
echo "missing from base   : $missing_base"
echo "missing from zh     : $missing_zh"
echo "missing from zh-Hant: $missing_hant"

if [ "$added_total" -eq 0 ] || { [ "$missing_base" -eq 0 ] && [ "$missing_zh" -eq 0 ]; }; then
  echo
  echo "base and zh are complete for this diff."
  exit 0
fi

echo
echo "Add the missing entries to the resources clone before committing. Translations are not"
echo "generated: write the zh (and zh-Hant) text yourself - see ../SKILL.md."
exit 1
