#!/usr/bin/env bash
set -euo pipefail

MAC="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
APP="$MAC/build/CCCC.app"
ARCHS=()
for a in ${CCCC_ARCHS:-arm64 x86_64}; do ARCHS+=(--arch "$a"); done

swift build -c release "${ARCHS[@]}" --package-path "$MAC"
BIN="$(swift build -c release "${ARCHS[@]}" --package-path "$MAC" --show-bin-path)/CCCC"

rm -rf "$APP"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
cp "$BIN" "$APP/Contents/MacOS/CCCC"
cp "$MAC/Info.plist" "$APP/Contents/Info.plist"

TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT
"$APP/Contents/MacOS/CCCC" --export-iconset "$TMP/AppIcon.iconset"
iconutil -c icns "$TMP/AppIcon.iconset" -o "$APP/Contents/Resources/AppIcon.icns"

ID="${CCCC_SIGN_ID:-}"
if [[ -z "$ID" ]]; then
    ID="$(security find-identity -v -p codesigning | sed -n 's/.*"\(Developer ID Application:[^"]*\)".*/\1/p' | head -1)"
fi
ID="${ID:--}"
TS=(--timestamp=none)
if [[ "${CCCC_TIMESTAMP:-0}" == 1 ]]; then TS=(--timestamp); fi

codesign --force --options runtime --sign "$ID" "${TS[@]}" "$APP"
codesign --verify --strict --verbose=2 "$APP"
echo "$APP"
