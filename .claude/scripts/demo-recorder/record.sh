#!/bin/bash
# Records the README animations into docs/images (needs ffmpeg on PATH and Microsoft Edge for the web scene).
#
#   record.sh [scene ...]        scenes: focus pause web (default: all), each in English and Portuguese
#
# While it runs, three demo windows, Capture Manager and (web scene) an Edge window open and take the focus.
# Frames are built from pictures of those windows only; the screen itself is never captured.
HERE="$(cd "$(dirname "$0")" && pwd)"
REPO="$(cd "$HERE/../../.." && pwd)"
WORK="${TEMP:-/tmp}/ccm-demo"
OUT="$REPO/docs/images"
SCENES="${*:-focus pause web}"

taskkill //IM CodescoveryCaptureManager.App.exe //F >/dev/null 2>&1
mkdir -p "$WORK" "$OUT"
VERSION="$(git -C "$REPO" describe --tags --abbrev=0 2>/dev/null | sed 's/^v//')"
dotnet build "$REPO/src/Presentation/CodescoveryCaptureManager.App" -o "$WORK/app" -p:Version="${VERSION:-1.0.0}" --nologo -v q | grep -E " error |Web UI not built" | sort -u
dotnet build "$HERE" -o "$WORK/rec" --nologo -v q | grep -E " error " | sort -u

for scene in $SCENES; do
  for lang in en pt; do
    frames="$WORK/$scene-$lang"
    rm -rf "$frames"
    "$WORK/rec/ccm-demo.exe" --scene "$scene" --lang "$lang" \
      --app "$(cygpath -w "$WORK/app/CodescoveryCaptureManager.App.exe")" --out "$(cygpath -w "$frames")" 2>/dev/null
    if [ $? -ne 0 ]; then echo "FAILED $scene-$lang: $(head -2 "$frames/error.txt")"; continue; fi
    ffmpeg -loglevel error -y -framerate 10 -i "$frames/%04d.png" \
      -vf "scale=960:-1:flags=lanczos,split[a][b];[a]palettegen=max_colors=128:stats_mode=diff[p];[b][p]paletteuse=dither=bayer:bayer_scale=5:diff_mode=rectangle" \
      -loop 0 "$OUT/$scene.$lang.gif"
    echo "$scene.$lang.gif  $(ls "$frames"/*.png | wc -l) frames  $(du -k "$OUT/$scene.$lang.gif" | cut -f1) KB"
  done
done
