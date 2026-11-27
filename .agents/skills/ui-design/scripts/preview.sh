#!/usr/bin/env bash
#
# Offscreen UI preview runner for the g0v0 client (see ../SKILL.md).
#
# Renders client UI on an offscreen GPU device and saves PNG captures with no visible window.
# Everything after the script name is forwarded to the previewer verbatim, so the whole CLI
# reference in SKILL.md works here unchanged; captures land in tools/Previewer/<--out>.
#
# Usage:
#   preview.sh --scene=<TestSceneName|editor> [--test=<Method>] [--case=<N>] [--crop=<Type,Type>]
#   preview.sh --scene=TestSceneSettingsPanel --crop=SettingsPanel --locale=zh
#   preview.sh --scene=TestSceneSongSelect --size=1280x720
#   preview.sh --scene=TestSceneImagePreviewOverlay --test=TestShowAndHide --capture-before="hide overlay"
#   preview.sh --scripts=compose,timing [--ruleset=osu] [--seek=13000] [--locale=zh]
#
#   preview.sh --build   # force a rebuild before running
#   preview.sh --help    # print this text
#
set -uo pipefail

ROOT="$(git rev-parse --show-toplevel 2>/dev/null)" || { echo "not inside a git repository" >&2; exit 2; }
cd "$ROOT" || exit 2

PREVIEWER_PROJ=".agents/skills/ui-design/tools/Previewer/Previewer.csproj"
PREVIEWER_DIR=".agents/skills/ui-design/tools/Previewer"

IS_WINDOWS=0
if [ "${OS:-}" = "Windows_NT" ] || uname -s 2>/dev/null | grep -qi "mingw\|msys\|cygwin"; then
  IS_WINDOWS=1
  EXE_PATH="$PREVIEWER_DIR/bin/Debug/net10.0/win-x64/Previewer.exe"
else
  EXE_PATH="$PREVIEWER_DIR/bin/Debug/net10.0/Previewer"
fi

DO_BUILD=0
FORWARD_ARGS=()

for arg in "$@"; do
  case "$arg" in
    --build)
      DO_BUILD=1
      ;;
    -h|--help)
      # The header block is everything after the shebang up to the first line that is not a comment.
      awk 'NR > 1 { if ($0 !~ /^#/) { exit } if ($0 == "#") { if (started) { print "" } next } sub(/^# ?/, ""); print; started = 1 }' "$0"
      exit 0
      ;;
    *)
      FORWARD_ARGS+=("$arg")
      ;;
  esac
done

# Build when asked to, or when there is nothing to run yet.
if [ "$DO_BUILD" -eq 1 ] || [ ! -f "$EXE_PATH" ]; then
  echo "Building UI Previewer..."
  dotnet build "$PREVIEWER_PROJ" -c Debug || { echo "Previewer build failed" >&2; exit 1; }
fi

# Run from the previewer directory so that a relative --out lands beside the executable, as SKILL.md describes.
(
  cd "$PREVIEWER_DIR" || exit 1
  if [ "$IS_WINDOWS" -eq 1 ]; then
    ./bin/Debug/net10.0/win-x64/Previewer.exe "${FORWARD_ARGS[@]}"
  else
    ./bin/Debug/net10.0/Previewer "${FORWARD_ARGS[@]}"
  fi
)
