#!/usr/bin/env bash
set -euo pipefail

MAC="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DIST="$(dirname "$MAC")/dist"
APP="$MAC/build/CCCC.app"
NOTARY_PROFILE="${NOTARY_PROFILE:-ILM-notary}"

ID="${CCCC_SIGN_ID:-}"
if [[ -z "$ID" ]]; then
    ID="$(security find-identity -v -p codesigning | sed -n 's/.*"\(Developer ID Application:[^"]*\)".*/\1/p' | head -1)"
fi
if [[ -z "$ID" ]]; then
    echo "error: no Developer ID Application identity found (set CCCC_SIGN_ID)" >&2
    exit 1
fi

TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

notarize() {
    local file="$1" out id
    out="$(xcrun notarytool submit "$file" --keychain-profile "$NOTARY_PROFILE" --wait 2>&1)" || true
    echo "$out"
    id="$(printf '%s\n' "$out" | sed -n 's/^ *id: //p' | head -1)"
    if ! printf '%s\n' "$out" | grep -q 'status: Accepted'; then
        echo "error: notarization of $(basename "$file") was not accepted" >&2
        if [[ -n "$id" ]]; then
            xcrun notarytool log "$id" --keychain-profile "$NOTARY_PROFILE" || true
        fi
        exit 1
    fi
}

CCCC_SIGN_ID="$ID" CCCC_TIMESTAMP=1 bash "$MAC/build.sh"

ditto -c -k --keepParent "$APP" "$TMP/CCCC-notary.zip"
notarize "$TMP/CCCC-notary.zip"
xcrun stapler staple "$APP"
spctl -a -vvv -t exec "$APP"

mkdir -p "$DIST"
rm -f "$DIST/CCCC-macOS.zip" "$DIST/CCCC-macOS.dmg"
ditto -c -k --keepParent "$APP" "$DIST/CCCC-macOS.zip"

STAGE="$TMP/dmg"
mkdir -p "$STAGE"
ditto "$APP" "$STAGE/CCCC.app"
ln -s /Applications "$STAGE/Applications"
DMG="$DIST/CCCC-macOS.dmg"
hdiutil create -volname CCCC -srcfolder "$STAGE" -ov -format UDZO "$DMG"
codesign --force --sign "$ID" --timestamp "$DMG"
notarize "$DMG"
xcrun stapler staple "$DMG"
spctl -a -vvv -t open --context context:primary-signature "$DMG"

shasum -a 256 "$DIST/CCCC-macOS.zip" "$DMG"
