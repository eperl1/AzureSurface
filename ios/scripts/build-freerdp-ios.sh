#!/usr/bin/env bash
set -euo pipefail

if [[ $# -lt 2 ]]; then
  echo "usage: $0 /path/to/FreeRDP SIMULATORARM64|OS64 [build-dir]"
  exit 1
fi

ROOT="$1"
PLATFORM="$2"
BUILD_DIR="${3:-$ROOT/build/ios-$PLATFORM}"
DEPLOYMENT_TARGET="${DEPLOYMENT_TARGET:-15.0}"

bash "$(dirname "${BASH_SOURCE[0]}")/apply-freerdp-overlay.sh" "$ROOT"

cmake -S "$ROOT/client/iOS" \
  -B "$BUILD_DIR" \
  -DCMAKE_TOOLCHAIN_FILE="$ROOT/cmake/ios.toolchain.cmake" \
  -DPLATFORM="$PLATFORM" \
  -DDEPLOYMENT_TARGET="$DEPLOYMENT_TARGET" \
  -G Xcode

cmake --build "$BUILD_DIR" --config Release
