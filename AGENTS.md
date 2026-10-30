# CCCC

- Windows app: C# in `src/`, build with `build.ps1` (Windows only).
- macOS app: Swift/AppKit SwiftPM package in `mac/` (behavioral port of `src/`).
  - Test build: `mac/build.sh` → `mac/build/CCCC.app` (universal, signed with Developer ID if available).
  - Design sheet: `mac/build/CCCC.app/Contents/MacOS/CCCC --sheet /tmp/sheet.png` (`CCCC_LANG=en|zh`).
  - Debug log: run the bundle binary with `CCCC_DEBUG=1`.
  - Release (sign + notarize + staple, zip + dmg into `dist/`): `mac/release.sh` (uses the shared `ILM-notary` notarytool keychain profile; override with `NOTARY_PROFILE`).
  - `CCCC_ARCHS` (default `arm64 x86_64`) picks the architectures; `CCCC_SIGN_ID=-` forces ad-hoc signing.
  - `mac/CCCC.entitlements` (Location, needed under hardened runtime for the Wi-Fi SSID) is applied by `build.sh`.
- Windows design sheet: `bin\CCCC.exe --sheet out.png` (`CCCC_LANG=en|zh`).
- Windows debug log: run with `CCCC_DEBUG=1` → `%TEMP%\CCCC\debug.log`.

# Rules
- Never add an option to hide the tray icon (or run with no visible UI). It made antivirus flag CCCC as a keylogger.
- Reply in precise and simple language. As simple as possible.