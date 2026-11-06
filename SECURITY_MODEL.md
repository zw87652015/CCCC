# CCCC threat model

Repository: `zw87652015/CCCC` (built from the code, `README.md`, `DESIGN.md`, `AGENTS.md` and the owner's answers). Website context: `zw87652015/zw87652015.github.io` (static site; links to `https://downloads.luojiajiang.uk/CCCC.exe` and `CCCC-macOS.dmg`; does not configure the download host). No infrastructure code exists.

## System overview

CCCC is a local desktop tray/menu-bar app that shows a card when the user copies. Two native ports with the same behavior:
- Windows: C# 5 / .NET Framework 4, single `CCCC.exe`, runs as the user (`app.manifest`: `asInvoker`). Portable build and Microsoft Store MSIX (`store/AppxManifest.xml`, `runFullTrust`, `location`, `wiFiControl`).
- macOS: Swift/AppKit SwiftPM app in `mac/`, hardened runtime, Location entitlement (`mac/CCCC.entitlements`), needs Input Monitoring and (optionally) Accessibility.

No server, no accounts, no network service. Network use is only the updater (GitHub API + download host) and opening links in the browser.

## Components

| Component | Code | Notes |
| --- | --- | --- |
| Clipboard | `src/Clip.cs`, `src/Sources.cs`, `mac/Sources/CCCC/Clip.swift`, `mac/Sources/CCCC/Source.swift` | Reads and parses clipboard (text, URL, HTML `SourceURL`, PNG/bitmap, file lists, private-content markers); finds source app/window/site. |
| Card UI | `src/Card.cs`, `src/Gfx.cs`, `src/Anchor.cs`, `src/Icons.cs`, `mac/Sources/CCCC/Card.swift`, `mac/Sources/CCCC/Gfx.swift`, `mac/Sources/CCCC/Icons.swift` | Renders previews; click can open the copied URL (`OpenLinkOnClick`, `Process.Start(... UseShellExecute)` / `NSWorkspace`). |
| Settings | `src/Settings.cs`, `src/Customize.cs`, `src/Customize.xaml`, `src/Audio.cs`, `mac/Sources/CCCC/Settings.swift`, `mac/Sources/CCCC/Customize.swift`, `mac/Sources/CCCC/Audio.swift` | Registry `HKCU\Software\CCCC` / `UserDefaults`; JSON import/export (`format: cccc-settings`, `version: 1`); custom sound file paths (`SoundOkFile`, `SoundFailFile`), font, colors, `IgnoreApps`. |
| Updater | `src/Updater.cs`, `mac/Sources/CCCC/Updater.swift`, update flow in `src/App.cs` | Windows: GitHub `releases/latest` metadata, downloads `https://downloads.luojiajiang.uk/CCCC.exe?v=<tag>`, checks size, `MZ`, and SHA-256 only if GitHub gives a `digest`; replaces the running exe. macOS: downloads `CCCC-macOS.zip` from GitHub, checks size/SHA-256, `ditto` unpack, verifies Developer ID requirement (`uk.luojiajiang.cccc`, team `BHG2P58XCR`). Off in the Store build (`Native.Packaged`). |
| System hooks | `src/Native.cs`, `src/App.cs`, `src/Wifi.cs`, `mac/Sources/CCCC/App.swift`, `mac/Sources/CCCC/Wifi.swift`, `mac/Sources/CCCC/main.swift` | `WH_KEYBOARD_LL` hook / macOS event tap (Ctrl/Cmd+C detection only, no keystroke logging), Caps Lock, Wi-Fi SSID (location permission), audio output, autostart (Run key / `StartupTask` / `SMAppService`), debug log (`CCCC_DEBUG=1` → `%TEMP%\CCCC\debug.log`). |
| Build and release | `build.ps1`, `store.ps1`, `mac/build.sh`, `mac/release.sh`, `src/Sheet.cs`, `src/Shots.cs`, `mac/Sources/CCCC/Sheet.swift` | Out of scope (user). Includes dev-only CLI flags `--sheet`, `--shots`, `--export-icon`, `--export-png`. |

## Trust boundaries and entry points

1. **Clipboard content → CCCC** (main boundary). Any website, document or app can put text, HTML, images, file lists and custom formats on the clipboard. CCCC parses all of it on every copy (`ClipReader.Read`, `Sources.SiteFromHtml`, image decode via `Image.FromStream` / `NSImage`, URL detection `Clip.Url`). Reachable by a remote attacker who gets the user to copy from a web page or document.
2. **Settings JSON file → CCCC.** User imports a `.json` file in Customize. Values go straight into the registry/UserDefaults and are read back with range/format checks. String fields include file paths (`SoundOkFile`, `SoundFailFile`), `FontFamily`, colors, `Lang`, `IgnoreApps`.
3. **Network → Updater.** GitHub API response and the downloaded binary (portable Windows and macOS builds only).
4. **Card → OS shell.** Clicking a card opens the copied URL with the shell.
5. **OS → CCCC.** Window titles, process names/paths, file descriptions and icons of other apps, Wi-Fi SSID, audio device names are shown on cards.

## Assets

- **Clipboard privacy**: content marked private (`ExcludeClipboardContentFromMonitorProcessing`, `Clipboard Viewer Ignore`, `CanIncludeInClipboardHistory=0`, macOS `org.nspasteboard.ConcealedType` etc.) must never be shown, logged, or kept. Other clipboard content must not leave the machine or be written to disk.
- **Code execution as the user**: from crafted clipboard content, a crafted settings file, or the updater.
- **User's system and credentials**: e.g. NTLM hash leak via a UNC path, opening dangerous URL schemes/files.
- **Location data** (Wi-Fi SSID) and the keyboard hook's input (must not be recorded).
- **App availability**: crash/hang of the tray app from crafted clipboard data or settings (low value).

## Attackers that matter (user-confirmed)

1. **Malicious clipboard content** — a web page, document or other app the user copies from. Can control every clipboard format, HTML `SourceURL`, image bytes, file names, window titles.
2. **Malicious settings JSON** — a file a user is tricked into importing.

Not named by the user as priorities (kept at scanner severity): network attacker or compromised download host against the updater; malicious window titles/app metadata.

## Out of scope / accepted risk (user)

- **Attackers already running code as the same user** (they can already read the clipboard, registry, `UserDefaults`, `%TEMP%`, replace the exe, inject input). Findings that need same-user local code execution are accepted risk.
- **Build/release scripts** (`build.ps1`, `store.ps1`, `mac/build.sh`, `mac/release.sh`).
- **Dev-only CLI flags** (`--sheet`, `--shots`, `--export-icon`, `--export-png`) and their code (`Sheet.*`, `Shots.cs`).
- **Microsoft Store build's updater** (off when `Native.Packaged`). Store-specific updater findings are not issues; the portable updater still is.

## Existing controls

- Private-content markers respected; private cards show app name only (`Sources.Text(..., secret)`).
- URL opening limited to `http`/`https` (`Clip.Url`, `Sources.Host`), and only on click when `OpenLinkOnClick` is on.
- Keyboard hook only compares clipboard sequence numbers; no keystrokes stored (`DESIGN.md` §5).
- Settings import checks `format`/`version`, macOS caps the file at 1 MiB; values are re-validated on load.
- Updater: TLS 1.2, size + `MZ` + optional SHA-256 (Windows); size + SHA-256 + Developer ID signature requirement (macOS); user confirmation before install.
- Product rule: never allow hiding the tray icon (`AGENTS.md`).

## Open questions

- Windows updater trusts `downloads.luojiajiang.uk` with no code-signature check when GitHub gives no `digest`. Is the release asset always uploaded so a digest exists? Who controls the download host?
- Are settings JSON files shared between users (e.g. theme packs)? That raises the weight of the settings-import surface.
