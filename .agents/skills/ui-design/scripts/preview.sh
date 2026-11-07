#!/usr/bin/env bash
#
# Offscreen UI preview runner for the g0v0 client (see ../SKILL.md).
#
# Renders client UI on an offscreen GPU device and saves PNG captures with no visible window.
#
# Usage:
#   # General visual test scenes (Settings, SongSelect, Ranking, Overlays, UI components):
#   preview.sh --scene=<TestSceneName> [--crop=<Type>] [--out=<dir>] [--locale=<zh|en>]
#   preview.sh --scene=TestSceneSettingsPanel --crop=SettingsPanel --locale=zh
#   preview.sh --scene=TestSceneSongSelect --size=1280x720
#
#   # Scripted editor workflows:
#   preview.sh --scripts=compose,timing,verify [--out=<dir>] [--locale=zh]
#
#   # Rebuilding & options:
#   preview.sh --build                    # force build before running
#   preview.sh --help                     # print help
#
set -uo pipefail

ROOT="$(git rev-parse --show-toplevel 2>/dev/null)" || { echo "not inside a git repository" >&2; exit 2; }
cd "$ROOT" || exit 2

PREVIEWER_PROJ=".agents/skills/ui-design/tools/Previewer/Previewer.csproj"
PREVIEWER_DIR=".agents/skills/ui-design/tools/Previewer"

EXE_NAME="Previewer"
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
      sed -n '3,18p' "$0" | sed 's/^# \?//'
      exit 0
      ;;
    *)
      FORWARD_ARGS+=("$arg")
      ;;
  esac
done

# Check if build is needed
if [ "$DO_BUILD" -eq 1 ] || [ ! -f "$EXE_PATH" ]; then
  echo "Building UI Previewer..."
  BUILD_ARGS=("build" "$PREVIEWER_PROJ" "-c" "Debug")

  # If sibling repositories exist or source build is enabled, use source build
  if [ "${G0V0SourceBuild:-}" = "true" ] || [ -d "../g0v0-framework" ] || [ -d "../../g0v0-framework" ]; then
    BUILD_ARGS+=("-p:G0V0SourceBuild=true")
  fi

  dotnet "${BUILD_ARGS[@]}" || { echo "Previewer build failed" >&2; exit 1; }
fi

# Run the previewer from its directory so relative paths resolve cleanly
(
  cd "$PREVIEWER_DIR" || exit 1
  if [ "$IS_WINDOWS" -eq 1 ]; then
    ./bin/Debug/net10.0/win-x64/Previewer.exe "${FORWARD_ARGS[@]}"
  else
    ./bin/Debug/net10.0/Previewer "${FORWARD_ARGS[@]}"
  fi
)
