# Windows Super Paste implementation notes

## Contract

- Windows only; disabled by default. Default shortcuts: Ctrl+Shift+Alt+V and Shift+Windows+Insert. Settings support adding, editing and removing multiple shortcuts (at least one). Plain Ctrl+V cannot be captured.
- Default mode: Markdown. Default rule mask: 257 (remove six private-use bullets and final newlines).
- Rule bits 0–8: symbols, tabs, leading whitespace, trailing whitespace, CJK/ASCII boundary whitespace,
  repeated spaces, repeated newlines, insert blank lines, final newlines.
- Stored settings: SuperPasteEnabled, SuperPasteShortcuts (list of "modifiers:virtualKey" strings; Win32 Alt=1/Ctrl=2/Shift=4/Windows=8),
  SuperPasteMarkdown, SuperPasteOptions. Existing registry and JSON portability handle these values.
- Legacy SuperPasteKey/SuperPasteModifiers remain readable: migration keeps the old chord and adds Shift+Windows+Insert.
  A saved list is authoritative, so removed defaults stay removed. Duplicate/invalid imported entries are discarded.
- Shortcut registration is atomic: new chords are staged before obsolete registrations are released; any failure
  releases staged additions and keeps the previous set. Capture temporarily suspends registrations and restores the
  previous set before applying changes. Each trigger reports its own key for release detection; busy transactions ignore reentry.
- Unicode text is used even if the provider also advertises HTML, RTF or bitmap previews. File lists, textless images
  and privacy-marked data are rejected.
- SuperPasteText is a pure source-preserving scanner. Protected code/table regions retain their original line endings.
  HTML raw blocks/comments, multiline links, list/quote continuations and blank lines inside indented code are protected.
  Newline rebuilding and divider detection use linear scans. Ambiguous Markdown lines are skipped conservatively; this is not a complete CommonMark/GFM parser or formatter.
- SuperPaste coordinates an IPasteDesktop transaction. It checks focus and sequence before mutation, waits for modifier
  and trigger-key release, caps clipboard retries at one second and waiting at two seconds, and sends Ctrl+V only once.
- WindowsPasteDesktop uses native Unicode clipboard reads/writes and SendInput. It does not snapshot or restore rich
  content. Data-provider latency inside a Windows clipboard API itself cannot be bounded by the retry timer.
- A successful SendInput does not prove a target accepted the paste. UIPI/elevated targets can block injected input;
  failures leave the cleaned text ready for manual paste. Clipboard content is never logged.

## Validation

Close CCCC before running the suite, since it temporarily registers the default Windows-key shortcut.
`./test-super-paste.ps1` compiles sources with the production references, then runs isolated preference,
engine, deterministic transaction, hotkey registration and WPF UI tests. It renders Chinese and English examples.
A real SendInput test focuses a temporary receiving TextBox and intercepts WM_PASTE, verifying a single delivery
using in-memory content rather than reading/modifying the user's clipboard. The preceding foreground window is restored.
Registration tests temporarily use Ctrl+Alt+Shift+F8/F9/F10/F11 and Shift+Windows+Insert, then release all registrations.
The current suite has 139 checks, including a 30,000-line cleanup/idempotence regression.
`CCCC.exe --super-paste-settings` opens the page in a new or existing instance for manual verification.

The test runner does not automate existing documents or modify real user preferences. Word, browser,
Markdown-editor and Notepad end-to-end interoperability with actual rich clipboard providers still needs manual
validation. Do not describe the in-memory receiver test as full clipboard-provider or cross-app acceptance testing.

No version bump or GitHub release is part of this change.
The user has confirmed automatic paste and shortcut setup in their environment; the automated receiver is still
not a substitute for a complete Word/browser/Markdown-editor compatibility matrix.
