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
- Store screenshots: `bin\CCCC.exe --shots <dir>` (`CCCC_LANG=en|zh`) → `01–06.png` at 1920×1080 × monitor DPI scale; shot 6 briefly shows the real Customize window and screen-captures it. Current set: `store\screenshots\{en,zh}`.
- Super Paste tests: quit CCCC, then `./test-super-paste.ps1` (works in PowerShell 5.1; renders settings-page PNGs to the printed temp dir).
- Windows debug log: run with `CCCC_DEBUG=1` → `%TEMP%\CCCC\debug.log`.
- `build.ps1` needs the Windows 10 SDK (`UnionMetadata\10.*\Windows.winmd`, for the MSIX `StartupTask`).
- Store package: `store.ps1` → `dist\CtrlCCCC_<ver>.msixbundle` (x64 + arm64, unsigned; Partner Center signs it). Manifest template: `store\AppxManifest.xml`. It runs `build.ps1` first, which fails if `dist\CCCC.exe` is running.
- Packaged builds (`Native.Packaged`): autostart uses `StartupTask` (TaskId `CCCC`), not the Run key; the updater is off.

# Rules
- Never add an option to hide the tray icon (or run with no visible UI). It made antivirus flag CCCC as a keylogger.
- Reply in precise and simple language. As simple as possible.