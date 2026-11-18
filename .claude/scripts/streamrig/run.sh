#!/bin/bash
# Starts Capture Manager (hidden in the tray, isolated data folder) and the stream rig window, already bound by a
# profile and in "keep last" mode so the stream keeps running when the rig window loses focus.
#
#   run.sh [width] [height] [ENV=value ...]      e.g.  run.sh 1920 1080 CAPTURE_MANAGER_SOFTWARE_ENCODER=1
#
# Then open http://127.0.0.1:47811/?viewers=1&seconds=15 in Chrome/Edge: the page connects as a viewer, decodes the
# stream and prints fps, frame gaps and paint-to-decoded latency. See .claude/docs/stream-testing.md.
# Needs a full build of the app (no -p:SkipWebBuild=true if the real viewer page is wanted too).
HERE="$(cd "$(dirname "$0")" && pwd)"
REPO="$(cd "$HERE/../../.." && pwd)"
OUT="${TEMP:-/tmp}/ccm-streamrig"
WIDTH=${1:-1280}; HEIGHT=${2:-720}; shift 2 2>/dev/null

taskkill //IM CodescoveryCaptureManager.App.exe //F >/dev/null 2>&1
taskkill //IM ccm-streamrig.exe //F >/dev/null 2>&1

dotnet build "$REPO/src/Presentation/CodescoveryCaptureManager.App" -o "$OUT/app" --nologo -v q | grep -E " error |Build succeeded" | sort -u
dotnet build "$HERE" -o "$OUT/rig" --nologo -v q | grep -E " error |Build succeeded" | sort -u

DATA="$OUT/data"; rm -rf "$DATA"; mkdir -p "$DATA"
ID="7d0c2f0e-5a54-4b7e-9a35-1f6f3f2a9b11"
printf '[{"id":"%s","name":"rig","rules":[{"processName":"ccm-streamrig","titleMatch":"Contains","titlePattern":"ccm-stream-rig","autoAttach":false}]}]' "$ID" > "$DATA/profiles.json"
printf '{"lastProfileId":"%s","fallbackMode":"KeepLast"}' "$ID" > "$DATA/settings.json"
export CAPTURE_MANAGER_DATA_DIR="$(cygpath -w "$DATA")"
for assignment in "$@"; do export "$assignment"; done

"$OUT/app/CodescoveryCaptureManager.App.exe" --tray >/dev/null 2>&1 &
sleep 5
"$OUT/rig/ccm-streamrig.exe" --width "$WIDTH" --height "$HEIGHT" >/dev/null 2>&1 &
sleep 4
echo "rig: $(curl -s http://127.0.0.1:47811/status)"
echo "app log: $DATA/logs/capture-manager.log"
grep -v "^    " "$DATA/logs/capture-manager.log"
echo "open http://127.0.0.1:47811/?viewers=1&seconds=15   (stop: taskkill //IM ccm-streamrig.exe //F; taskkill //IM CodescoveryCaptureManager.App.exe //F)"
