#!/usr/bin/env bash
set -euo pipefail

if [[ $# -ne 1 ]]; then
  echo "usage: $0 /path/to/FreeRDP"
  exit 1
fi

ROOT="$1"
OVERLAY_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)/overlay/client/iOS/SurfaceMode"
TARGET_DIR="$ROOT/client/iOS/SurfaceMode"
CMAKELISTS="$ROOT/client/iOS/CMakeLists.txt"
EXTERNAL_OPUS="$ROOT/client/iOS/cmake/ExternalOpus.cmake"

mkdir -p "$TARGET_DIR"
cp -f "$OVERLAY_DIR"/* "$TARGET_DIR"/

python3 - "$CMAKELISTS" <<'PY'
import pathlib
import sys

path = pathlib.Path(sys.argv[1])
text = path.read_text(encoding="utf-8")

marker = "    AppDelegate.m\n"
addition = """    SurfaceMode/AppSettingsController+SurfaceMode.h\n    SurfaceMode/AppSettingsController+SurfaceMode.m\n    SurfaceMode/SurfaceModeBootstrap.h\n    SurfaceMode/SurfaceModeBootstrap.m\n    SurfaceMode/SurfaceModeControlClient.h\n    SurfaceMode/SurfaceModeControlClient.m\n    SurfaceMode/SurfaceModeSettings.h\n    SurfaceMode/SurfaceModeSettings.m\n    SurfaceMode/SurfaceModeStatusCenter.h\n    SurfaceMode/SurfaceModeStatusCenter.m\n"""

if "SurfaceMode/SurfaceModeBootstrap.m" not in text:
    if marker not in text:
        raise SystemExit("Could not find insertion point in CMakeLists.txt")
    text = text.replace(marker, marker + addition, 1)

path.write_text(text, encoding="utf-8")
PY

python3 - "$EXTERNAL_OPUS" <<'PY'
import pathlib
import sys

path = pathlib.Path(sys.argv[1])
text = path.read_text(encoding="utf-8")

old = "https://github.com/xiph/opus/archive/refs/tags/${OPUS_VERSION}.tar.gz"
new = "https://github.com/xiph/opus/releases/download/v${OPUS_VERSION}/opus-${OPUS_VERSION}.tar.gz"

if old in text:
    text = text.replace(old, new)

path.write_text(text, encoding="utf-8")
PY
