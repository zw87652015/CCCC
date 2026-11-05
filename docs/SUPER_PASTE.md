# Windows Super Paste implementation notes

## Contract

- Windows only; disabled by default. Default shortcut: Ctrl+Shift+Alt+V. Plain Ctrl+V cannot be captured.
- Default mode: Markdown. Default rule mask: 257 (remove six private-use bullets and final newlines).
- Rule bits 0–8: symbols, tabs, leading whitespace, trailing whitespace, CJK/ASCII boundary whitespace,
  repeated spaces, repeated newlines, insert blank lines, final newlines.
- Stored settings: SuperPasteEnabled, SuperPasteKey (virtual key), SuperPasteModifiers (Win32 Alt=1/Ctrl=2/Shift=4),
  SuperPasteMarkdown, SuperPasteOptions. Existing registry and JSON portability handle these values.
- SuperPasteText is a pure source-preserving scanner. Protected code/table regions retain their original line endings.
  Ambiguous Markdown lines are skipped conservatively; this is not a complete CommonMark/GFM parser or formatter.
- SuperPaste coordinates an IPasteDesktop transaction. It checks focus and sequence before mutation, waits for modifier
  and trigger-key release, caps clipboard retries at one second and waiting at two seconds, and sends Ctrl+V only once.
- WindowsPasteDesktop uses native Unicode clipboard reads/writes and SendInput. It does not snapshot or restore rich
  content. Data-provider latency inside a Windows clipboard API itself cannot be bounded by the retry timer.
- A successful SendInput does not prove a target accepted the paste. UIPI/elevated targets can block injected input;
  failures leave the cleaned text ready for manual paste. Clipboard content is never logged.

## Validation

`./test-super-paste.ps1` compiles sources with the production references, then runs isolated preference,
engine, deterministic transaction, hotkey registration and WPF UI tests. It renders Chinese and English examples.
A real SendInput test focuses a temporary receiving TextBox and intercepts WM_PASTE, verifying a single delivery
using in-memory content rather than reading/modifying the user's clipboard. The preceding foreground window is restored.
Registration tests temporarily use Ctrl+Alt+Shift+F9/F10/F11 and release all registrations afterward.

The test runner does not automate existing documents or modify real user preferences. Word, browser,
Markdown-editor and Notepad end-to-end interoperability with actual rich clipboard providers still needs manual
validation. Do not describe the in-memory receiver test as full clipboard-provider or cross-app acceptance testing.

No version bump, GitHub release or upstream PR is part of this implementation.
