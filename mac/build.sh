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

PLIST="$APP/Contents/Info.plist"
commit="unknown"
contributors=""
if hash="$(git -C "$MAC/.." rev-parse --short HEAD 2>/dev/null)" && [[ -n "$hash" ]]; then
    commit="$hash"
    if [[ -n "$(git -C "$MAC/.." status --porcelain 2>/dev/null || true)" ]]; then commit="$hash-dirty"; fi
    contributors="$(git -C "$MAC/.." log --format='%(trailers:key=Co-authored-by,valueonly)' 2>/dev/null |
        sed -e 's/<[^>]*>//g' -e 's/[\\"]//g' -e 's/^ *//; s/ *$//' | grep -v '^$' | sort -u || true)"
fi
/usr/libexec/PlistBuddy -c "Add :CCCCBuildCommit string $commit" "$PLIST"
/usr/libexec/PlistBuddy -c "Add :CCCCBuildDate string $(date +%Y-%m-%d)" "$PLIST"
/usr/libexec/PlistBuddy -c "Add :CCCCContributors array" "$PLIST"
while IFS= read -r name; do
    if [[ -n "$name" ]]; then /usr/libexec/PlistBuddy -c "Add :CCCCContributors: string \"$name\"" "$PLIST"; fi
done <<< "$contributors"

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

codesign --force --options runtime --entitlements "$MAC/CCCC.entitlements" --sign "$ID" "${TS[@]}" "$APP"
codesign --verify --strict --verbose=2 "$APP"
echo "$APP"
